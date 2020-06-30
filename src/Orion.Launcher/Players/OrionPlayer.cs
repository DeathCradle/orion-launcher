﻿// Copyright (c) 2020 Pryaxis & Orion Contributors
// 
// This file is part of Orion.
// 
// Orion is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Orion is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Orion.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Destructurama.Attributed;
using Orion.Core.Entities;
using Orion.Core.Events;
using Orion.Core.Events.Packets;
using Orion.Core.Packets;
using Orion.Core.Players;
using Orion.Core.Utils;
using Orion.Launcher.Entities;
using Serilog;

namespace Orion.Launcher.Players
{
    [LogAsScalar]
    internal sealed class OrionPlayer : OrionEntity<Terraria.Player>, IPlayer
    {
        private readonly IEventManager _events;
        private readonly ILogger _log;

        public OrionPlayer(int playerIndex, Terraria.Player terrariaPlayer, IEventManager events, ILogger log)
            : base(playerIndex, terrariaPlayer)
        {
            Debug.Assert(events != null);
            Debug.Assert(log != null);

            _events = events;
            _log = log;

            Character = new OrionCharacter(terrariaPlayer);
            Buffs = new BuffArray(terrariaPlayer);
        }

        public OrionPlayer(Terraria.Player terrariaPlayer, IEventManager events, ILogger log)
            : this(-1, terrariaPlayer, events, log) { }

        public override string Name
        {
            get => Wrapped.name;
            set => Wrapped.name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ICharacter Character { get; }

        public int Health
        {
            get => Wrapped.statLife;
            set => Wrapped.statLife = value;
        }

        public int MaxHealth
        {
            get => Wrapped.statLifeMax;
            set => Wrapped.statLifeMax = value;
        }

        public int Mana
        {
            get => Wrapped.statMana;
            set => Wrapped.statMana = value;
        }

        public int MaxMana
        {
            get => Wrapped.statManaMax;
            set => Wrapped.statManaMax = value;
        }

        public IArray<Buff> Buffs { get; }

        public bool IsInPvp
        {
            get => Wrapped.hostile;
            set => Wrapped.hostile = value;
        }

        public Team Team
        {
            get => (Team)Wrapped.team;
            set => Wrapped.team = (int)value;
        }

        public void ReceivePacket<TPacket>(TPacket packet) where TPacket : IPacket
        {
            if (packet is null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var evt = new PacketReceiveEvent<TPacket>(packet, this);
            _events.Raise(evt, _log);
            if (evt.IsCanceled)
            {
                return;
            }

            packet = evt.Packet;

            var buffer = Terraria.NetMessage.buffer[Index];

            // To simulate the receival of the packet, we must swap out the read buffer and reader, and call `GetData()`
            // while ensuring that the next `ReceiveDataHandler()` call is ignored.
            var oldReadBuffer = buffer.readBuffer;
            var oldReader = buffer.reader;

            var pool = ArrayPool<byte>.Shared;
            var receiveBuffer = pool.Rent(65536);

            try
            {
                // Write the packet using the `Client` context since we're receiving this packet.
                var packetLength = packet.Write(receiveBuffer, PacketContext.Client);

                // Ignore the next `GetData` call so that there isn't an infinite loop.
                OrionPlayerService._ignoreGetData = true;
                buffer.readBuffer = receiveBuffer;
                buffer.reader = new BinaryReader(new MemoryStream(buffer.readBuffer), Encoding.UTF8);
                buffer.GetData(2, packetLength - 2, out _);
                OrionPlayerService._ignoreGetData = false;
            }
            finally
            {
                pool.Return(receiveBuffer);

                buffer.readBuffer = oldReadBuffer;
                buffer.reader = oldReader;
            }
        }

        public void SendPacket<TPacket>(TPacket packet) where TPacket : IPacket
        {
            if (packet is null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var terrariaClient = Terraria.Netplay.Clients[Index];
            if (!terrariaClient.IsConnected())
            {
                return;
            }

            var evt = new PacketSendEvent<TPacket>(packet, this);
            _events.Raise(evt, _log);
            if (evt.IsCanceled)
            {
                return;
            }

            packet = evt.Packet;

            var pool = ArrayPool<byte>.Shared;
            var sendBuffer = pool.Rent(65536);
            var wasSuccessful = false;

            try
            {
                // Write the packet using the `Server` context since we're sending this packet.
                var packetLength = packet.Write(sendBuffer, PacketContext.Server);
                terrariaClient.Socket.AsyncSend(sendBuffer, 0, packetLength, state =>
                {
                    ArrayPool<byte>.Shared.Return((byte[])state);
                    terrariaClient.ServerWriteCallBack(null!);
                }, sendBuffer);

                wasSuccessful = true;
            }
            catch (IOException)
            {
            }
            finally
            {
                // To prevent leakage, return the buffer if the send wasn't successful.
                if (!wasSuccessful)
                {
                    pool.Return(sendBuffer);
                }
            }
        }

        private sealed class OrionCharacter : ICharacter
        {
            private readonly Terraria.Player _wrapped;

            public OrionCharacter(Terraria.Player terrariaPlayer)
            {
                Debug.Assert(terrariaPlayer != null);

                _wrapped = terrariaPlayer;
            }

            public CharacterDifficulty Difficulty
            {
                get => (CharacterDifficulty)_wrapped.difficulty;
                set => _wrapped.difficulty = (byte)value;
            }
        }

        private class BuffArray : IArray<Buff>
        {
            private readonly Terraria.Player _wrapped;

            public BuffArray(Terraria.Player terrariaPlayer)
            {
                Debug.Assert(terrariaPlayer != null);

                _wrapped = terrariaPlayer;
            }

            public Buff this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new IndexOutOfRangeException($"Index out of range (expected: 0 to {Count - 1})");
                    }

                    var ticks = _wrapped.buffTime[index];
                    if (ticks <= 0)
                    {
                        return default;
                    }

                    var id = (BuffId)_wrapped.buffType[index];
                    return new Buff(id, ticks);
                }

                set
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new IndexOutOfRangeException($"Index out of range (expected: 0 to {Count - 1})");
                    }

                    _wrapped.buffType[index] = (int)value.Id;
                    _wrapped.buffTime[index] = value.Ticks;
                }
            }

            public int Count => Terraria.Player.maxBuffs;

            public IEnumerator<Buff> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                {
                    yield return this[i];
                }
            }

            [ExcludeFromCodeCoverage]
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
