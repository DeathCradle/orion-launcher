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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Orion.Core;
using Orion.Core.Events;
using Orion.Core.Events.Packets;
using Orion.Core.Events.Players;
using Orion.Core.Framework;
using Orion.Core.Packets;
using Orion.Core.Packets.Client;
using Orion.Core.Packets.Modules;
using Orion.Core.Packets.Players;
using Orion.Core.Players;
using Serilog;

namespace Orion.Launcher.Impl.Players
{
    [Binding("orion-players", Author = "Pryaxis", Priority = BindingPriority.Lowest)]
    internal sealed class OrionPlayerService : OrionExtension, IPlayerService
    {
        private delegate void PacketHandler(int playerIndex, Span<byte> span);

        private static readonly MethodInfo _onReceivePacket =
            typeof(OrionPlayerService)
                .GetMethod(nameof(OnReceivePacket), BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly MethodInfo _onSendPacket =
            typeof(OrionPlayerService)
                .GetMethod(nameof(OnSendPacket), BindingFlags.NonPublic | BindingFlags.Instance)!;

        [ThreadStatic] internal static bool _ignoreGetData;

        private readonly PacketHandler?[] _onReceivePacketHandlers = new PacketHandler?[256];
        private readonly PacketHandler?[] _onReceiveModuleHandlers = new PacketHandler?[65536];
        private readonly PacketHandler?[] _onSendPacketHandlers = new PacketHandler?[256];
        private readonly PacketHandler?[] _onSendModuleHandlers = new PacketHandler?[65536];

        public OrionPlayerService(IServer server, ILogger log) : base(server, log)
        {
            // Construct the `Players` array. Note that the last player should be ignored, as it is not a real player.
            Players = new WrappedReadOnlyList<OrionPlayer, Terraria.Player>(
                Terraria.Main.player.AsMemory(..^1),
                (playerIndex, terrariaPlayer) => new OrionPlayer(playerIndex, terrariaPlayer, server, log));

            // Construct the `_onReceivePacketHandlers` and `_onSendPacketHandlers` arrays ahead of time for the valid
            // `PacketId` instances. The invalid instances are handled by defaulting to `UnknownPacket`.
            foreach (var packetId in (PacketId[])Enum.GetValues(typeof(PacketId)))
            {
                var packetType = packetId.Type();
                _onReceivePacketHandlers[(byte)packetId] = MakeOnReceivePacketHandler(packetType);
                _onSendPacketHandlers[(byte)packetId] = MakeOnSendPacketHandler(packetType);
            }

            // Construct the `_onReceiveModuleHandlers` and `_onSendModuleHandlers` arrays ahead of time for the valid
            // `ModuleId` instances. The invalid instances are handled by defaulting to `UnknownModule`.
            foreach (var moduleId in (ModuleId[])Enum.GetValues(typeof(ModuleId)))
            {
                var packetType = typeof(ModulePacket<>).MakeGenericType(moduleId.Type());
                _onReceiveModuleHandlers[(ushort)moduleId] = MakeOnReceivePacketHandler(packetType);
                _onSendModuleHandlers[(ushort)moduleId] = MakeOnSendPacketHandler(packetType);
            }

            OTAPI.Hooks.Net.ReceiveData = ReceiveDataHandler;
            OTAPI.Hooks.Net.SendBytes = SendBytesHandler;
            OTAPI.Hooks.Net.SendNetData = SendNetDataHandler;
            OTAPI.Hooks.Player.PreUpdate = PreUpdateHandler;
            OTAPI.Hooks.Net.RemoteClient.PreReset = PreResetHandler;

            Server.Events.RegisterHandlers(this, Log);

            PacketHandler MakeOnReceivePacketHandler(Type packetType) =>
                (PacketHandler)_onReceivePacket
                    .MakeGenericMethod(packetType)
                    .CreateDelegate(typeof(PacketHandler), this);

            PacketHandler MakeOnSendPacketHandler(Type packetType) =>
                (PacketHandler)_onSendPacket
                    .MakeGenericMethod(packetType)
                    .CreateDelegate(typeof(PacketHandler), this);
        }

        public IReadOnlyList<IPlayer> Players { get; }

        public override void Dispose()
        {
            OTAPI.Hooks.Net.ReceiveData = null;
            OTAPI.Hooks.Net.SendBytes = null;
            OTAPI.Hooks.Net.SendNetData = null;
            OTAPI.Hooks.Player.PreUpdate = null;
            OTAPI.Hooks.Net.RemoteClient.PreReset = null;

            Server.Events.DeregisterHandlers(this, Log);
        }

        // =============================================================================================================
        // OTAPI hooks
        //

        private OTAPI.HookResult ReceiveDataHandler(
            Terraria.MessageBuffer buffer, ref byte packetId, ref int readOffset, ref int start, ref int length)
        {
            Debug.Assert(buffer != null);
            Debug.Assert(buffer.whoAmI >= 0 && buffer.whoAmI < Players.Count);
            Debug.Assert(start >= 0 && start + length <= buffer.readBuffer.Length);
            Debug.Assert(length > 0);

            // Check `_ignoreGetData` to prevent infinite loops.
            if (_ignoreGetData)
            {
                return OTAPI.HookResult.Continue;
            }

            PacketHandler handler;
            var span = buffer.readBuffer.AsSpan(start..(start + length));
            if (packetId == (byte)PacketId.Module)
            {
                var moduleId = Unsafe.ReadUnaligned<ushort>(ref span[1]);
                handler = _onReceiveModuleHandlers[moduleId] ?? OnReceivePacket<ModulePacket<UnknownModule>>;
            }
            else
            {
                handler = _onReceivePacketHandlers[packetId] ?? OnReceivePacket<UnknownPacket>;
            }

            handler(buffer.whoAmI, span);
            return OTAPI.HookResult.Cancel;
        }

        private OTAPI.HookResult SendBytesHandler(
            ref int playerIndex, ref byte[] data, ref int offset, ref int size,
            ref Terraria.Net.Sockets.SocketSendCallback callback, ref object state)
        {
            Debug.Assert(playerIndex >= 0 && playerIndex < Players.Count);
            Debug.Assert(data != null);
            Debug.Assert(offset >= 0 && offset + size <= data.Length);
            Debug.Assert(size >= 3);

            var span = data.AsSpan((offset + 2)..(offset + size));
            var packetId = span[0];

            // The `SendBytes` event is only triggered for non-module packets.
            var handler = _onSendPacketHandlers[packetId] ?? OnSendPacket<UnknownPacket>;
            handler(playerIndex, span);
            return OTAPI.HookResult.Cancel;
        }

        private OTAPI.HookResult SendNetDataHandler(
            Terraria.Net.NetManager manager, Terraria.Net.Sockets.ISocket socket, ref Terraria.Net.NetPacket packet)
        {
            Debug.Assert(socket != null);
            Debug.Assert(packet.Buffer.Data != null);
            Debug.Assert(packet.Writer.BaseStream.Position >= 5);

            // Since we don't have an index, scan through the clients to find the player index.
            //
            // TODO: optimize this using a hash map, if needed
            var playerIndex = -1;
            for (var i = 0; i < Terraria.Netplay.MaxConnections; ++i)
            {
                if (Terraria.Netplay.Clients[i].Socket == socket)
                {
                    playerIndex = i;
                    break;
                }
            }

            Debug.Assert(playerIndex >= 0 && playerIndex < Players.Count);

            var span = packet.Buffer.Data.AsSpan(2..((int)packet.Writer.BaseStream.Position));
            var moduleId = Unsafe.ReadUnaligned<ushort>(ref span[1]);

            // The `SendBytes` event is only triggered for module packets.
            var handler = _onSendModuleHandlers[moduleId] ?? OnSendPacket<ModulePacket<UnknownModule>>;
            handler(playerIndex, span);
            return OTAPI.HookResult.Cancel;
        }

        private OTAPI.HookResult PreUpdateHandler(Terraria.Player terrariaPlayer, ref int playerIndex)
        {
            Debug.Assert(playerIndex >= 0 && playerIndex < Players.Count);

            var evt = new PlayerTickEvent(Players[playerIndex]);
            Server.Events.Raise(evt, Log);
            return evt.IsCanceled ? OTAPI.HookResult.Cancel : OTAPI.HookResult.Continue;
        }

        private OTAPI.HookResult PreResetHandler(Terraria.RemoteClient remoteClient)
        {
            Debug.Assert(remoteClient != null);
            Debug.Assert(remoteClient.Id >= 0 && remoteClient.Id < Players.Count);

            // Check if the client was active since this gets called when setting up `RemoteClient` as well.
            if (!remoteClient.IsActive)
            {
                return OTAPI.HookResult.Continue;
            }

            var evt = new PlayerQuitEvent(Players[remoteClient.Id]);
            Server.Events.Raise(evt, Log);
            return OTAPI.HookResult.Continue;
        }

        // =============================================================================================================
        // Packet event publishers
        //

        private void OnReceivePacket<TPacket>(int playerIndex, Span<byte> span) where TPacket : struct, IPacket
        {
            Debug.Assert(playerIndex >= 0 && playerIndex < Players.Count);
            Debug.Assert(span.Length > 0);

            var packet = new TPacket();
            if (typeof(TPacket) == typeof(UnknownPacket))
            {
                Unsafe.As<TPacket, UnknownPacket>(ref packet).Id = (PacketId)span[0];
            }

            // Read the packet using the `Server` context since we're receiving this packet.
            var packetLength = packet.Read(span[1..], PacketContext.Server);
            Debug.Assert(packetLength == span.Length - 1);

            Players[playerIndex].ReceivePacket(ref packet);
        }

        private void OnSendPacket<TPacket>(int playerIndex, Span<byte> span) where TPacket : struct, IPacket
        {
            Debug.Assert(playerIndex >= 0 && playerIndex < Players.Count);
            Debug.Assert(span.Length > 0);

            var packet = new TPacket();
            if (typeof(TPacket) == typeof(UnknownPacket))
            {
                Unsafe.As<TPacket, UnknownPacket>(ref packet).Id = (PacketId)span[0];
            }

            // Read the packet using the `Client` context since we're sending this packet.
            var packetLength = packet.Read(span[1..], PacketContext.Client);
            Debug.Assert(packetLength == span.Length - 1);

            Players[playerIndex].SendPacket(ref packet);
        }

        // =============================================================================================================
        // Player event publishers
        //

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnPlayerJoinPacket(PacketReceiveEvent<PlayerJoinPacket> evt)
        {
            ForwardEvent(evt, new PlayerJoinEvent(evt.Sender));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnPlayerHealthPacket(PacketReceiveEvent<PlayerHealthPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerHealthEvent(evt.Sender, packet.Health, packet.MaxHealth));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnPlayerPvpPacket(PacketReceiveEvent<PlayerPvpPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerPvpEvent(evt.Sender, packet.IsInPvp));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnClientPasswordPacket(PacketReceiveEvent<ClientPasswordPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerPasswordEvent(evt.Sender, packet.Password));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnPlayerManaPacket(PacketReceiveEvent<PlayerManaPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerManaEvent(evt.Sender, packet.Mana, packet.MaxMana));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnPlayerTeamPacket(PacketReceiveEvent<PlayerTeamPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerTeamEvent(evt.Sender, packet.Team));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnClientUuidPacket(PacketReceiveEvent<ClientUuidPacket> evt)
        {
            ref var packet = ref evt.Packet;

            ForwardEvent(evt, new PlayerUuidEvent(evt.Sender, packet.Uuid));
        }

        [EventHandler("orion-players", Priority = EventPriority.Lowest)]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Implicitly used")]
        private void OnChatModule(PacketReceiveEvent<ModulePacket<ChatModule>> evt)
        {
            ref var module = ref evt.Packet.Module;

            ForwardEvent(evt, new PlayerChatEvent(evt.Sender, module.ClientCommand, module.ClientMessage));
        }

        // Forwards `evt` as `newEvt`.
        private void ForwardEvent<TEvent>(Event evt, TEvent newEvt) where TEvent : Event
        {
            Server.Events.Raise(newEvt, Log);
            if (newEvt.IsCanceled)
            {
                evt.Cancel(newEvt.CancellationReason);
            }
        }
    }
}
