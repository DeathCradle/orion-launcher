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
using System.IO;
using System.Linq;
using Orion.Core;
using Orion.Core.DataStructures;
using Orion.Core.Events.Packets;
using Orion.Core.Events.Players;
using Orion.Core.Packets;
using Orion.Core.Packets.Client;
using Orion.Core.Packets.Modules;
using Orion.Core.Packets.Players;
using Orion.Core.Players;
using Serilog.Core;
using Xunit;

namespace Orion.Launcher.Impl.Players
{
    // These tests depend on Terraria state.
    [Collection("TerrariaTestsCollection")]
    public class OrionPlayerServiceTests
    {
        private static readonly byte[] _serverConnectPacketBytes;
        private static readonly byte[] _playerJoinPacketBytes = { 3, 0, 6 };
        private static readonly byte[] _playerHealthPacketBytes = { 8, 0, 16, 5, 100, 0, 244, 1 };
        private static readonly byte[] _playerPvpPacketBytes = { 5, 0, 30, 5, 1 };
        private static readonly byte[] _clientPasswordPacketBytes = { 12, 0, 38, 8, 84, 101, 114, 114, 97, 114, 105, 97 };
        private static readonly byte[] _playerManaPacketBytes = { 8, 0, 42, 5, 100, 0, 200, 0 };
        private static readonly byte[] _playerTeamPacketBytes = { 5, 0, 45, 5, 1 };
        private static readonly byte[] _clientUuidPacketBytes = { 12, 0, 68, 8, 84, 101, 114, 114, 97, 114, 105, 97 };

        private static readonly byte[] _chatModuleBytes =
        {
            23, 0, 82, 1, 0, 3, 83, 97, 121, 13, 47, 99, 111, 109, 109, 97, 110, 100, 32, 116, 101, 115, 116
        };

        static OrionPlayerServiceTests()
        {
            var bytes = new byte[100];
            var packet = new ClientConnectPacket { Version = "Terraria" + Terraria.Main.curRelease };
            var packetLength = packet.WriteWithHeader(bytes, PacketContext.Client);

            _serverConnectPacketBytes = bytes[..packetLength];
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10000)]
        public void Players_Item_GetInvalidIndex_ThrowsIndexOutOfRangeException(int index)
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            Assert.Throws<IndexOutOfRangeException>(() => playerService.Players[index]);
        }

        [Fact]
        public void Players_Item_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            var player = playerService.Players[1];

            Assert.Equal(1, player.Index);
            Assert.Equal(Terraria.Main.player[1], ((OrionPlayer)player).Wrapped);
        }

        [Fact]
        public void Players_Item_GetMultipleTimes_ReturnsSameInstance()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            var player = playerService.Players[0];
            var player2 = playerService.Players[0];

            Assert.Same(player2, player);
        }

        [Fact]
        public void Players_GetEnumerator()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            var players = playerService.Players.ToList();

            for (var i = 0; i < players.Count; ++i)
            {
                Assert.Equal(Terraria.Main.player[i], ((OrionPlayer)players[i]).Wrapped);
            }
        }

        [Fact]
        public void PlayerTick_EventTriggered()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerTickEvent>(evt =>
            {
                Assert.Same(Terraria.Main.player[0], ((OrionPlayer)evt.Player).Wrapped);
                isRun = true;
            }, Logger.None);

            Terraria.Main.player[0].Update(0);

            Assert.True(isRun);
        }

        [Fact]
        public void PlayerTick_EventCanceled()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerTickEvent>(evt => evt.Cancel(), Logger.None);

            Terraria.Main.player[0].Update(0);
        }

        [Fact]
        public void ResetClient_PlayerQuitEventTriggered()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, IsActive = true };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerQuitEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                isRun = true;
            }, Logger.None);

            Terraria.Netplay.Clients[5].Reset();

            Assert.True(isRun);
        }

        [Fact]
        public void ResetClient_PlayerQuitEventNotTriggered()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerQuitEvent>(evt => isRun = true, Logger.None);

            Terraria.Netplay.Clients[5].Reset();

            Assert.False(isRun);
        }

        [Fact]
        public void PacketReceive_EventTriggered()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5 };
            Terraria.Netplay.ServerPassword = string.Empty;

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketReceiveEvent<ClientConnectPacket>>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Sender);
                Assert.Equal("Terraria" + Terraria.Main.curRelease, evt.Packet.Version);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _serverConnectPacketBytes);

            Assert.True(isRun);
            Assert.Equal(1, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_EventModified()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5 };
            Terraria.Netplay.ServerPassword = string.Empty;

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketReceiveEvent<ClientConnectPacket>>(
                evt => evt.Packet.Version = "Terraria1", Logger.None);

            TestUtils.FakeReceiveBytes(5, _serverConnectPacketBytes);

            Assert.Equal(0, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_EventCanceled()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5 };
            Terraria.Netplay.ServerPassword = string.Empty;

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketReceiveEvent<ClientConnectPacket>>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _serverConnectPacketBytes);

            Assert.Equal(0, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_UnknownPacket()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketReceiveEvent<UnknownPacket>>(evt =>
            {
                ref var packet = ref evt.Packet;
                Assert.Equal((PacketId)255, packet.Id);
                Assert.Equal(0, packet.Length);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, new byte[] { 3, 0, 255 });

            Assert.True(isRun);
        }

        [Fact]
        public void PacketReceive_UnknownModule()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketReceiveEvent<ModulePacket<UnknownModule>>>(evt =>
            {
                ref var module = ref evt.Packet.Module;
                Assert.Equal((ModuleId)65535, module.Id);
                Assert.Equal(0, module.Length);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, new byte[] { 5, 0, 82, 255, 255 });

            Assert.True(isRun);
        }

        [Fact]
        public void PacketReceive_PlayerJoin_EventTriggered()
        {
            // Set `State` to 1 so that the join packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 1 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerJoinEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerJoinPacketBytes);

            Assert.True(isRun);
            Assert.Equal(2, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_PlayerJoin_EventCanceled()
        {
            // Set `State` to 1 so that the join packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 1 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerJoinEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerJoinPacketBytes);

            Assert.Equal(1, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_PlayerHealth_EventTriggered()
        {
            // Set `State` to 10 so that the health packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerHealthEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.Health);
                Assert.Equal(500, evt.MaxHealth);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerHealthPacketBytes);

            Assert.True(isRun);
            Assert.Equal(100, Terraria.Main.player[5].statLife);
            Assert.Equal(500, Terraria.Main.player[5].statLifeMax);
        }

        [Fact]
        public void PacketReceive_PlayerHealth_EventCanceled()
        {
            // Set `State` to 10 so that the health packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerHealthEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerHealthPacketBytes);

            Assert.Equal(100, Terraria.Main.player[5].statLife);
            Assert.Equal(100, Terraria.Main.player[5].statLifeMax);
        }

        [Fact]
        public void PacketReceive_PlayerPvp_EventTriggered()
        {
            // Set `State` to 10 so that the PvP packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerPvpEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.True(evt.IsInPvp);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerPvpPacketBytes);

            Assert.True(isRun);
            Assert.True(Terraria.Main.player[5].hostile);
        }

        [Fact]
        public void PacketReceive_PlayerPvp_EventCanceled()
        {
            // Set `State` to 10 so that the PvP packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerPvpEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerPvpPacketBytes);

            Assert.False(Terraria.Main.player[5].hostile);
        }

        [Fact]
        public void PacketReceive_PlayerPassword_EventTriggered()
        {
            // Set `State` to -1 so that the password packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = -1 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Netplay.ServerPassword = "Terraria";

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerPasswordEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal("Terraria", evt.Password);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _clientPasswordPacketBytes);

            Assert.True(isRun);
            Assert.Equal(1, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_PlayerPassword_EventCanceled()
        {
            // Set `State` to -1 so that the password packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = -1 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Netplay.ServerPassword = "Terraria";

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerPasswordEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _clientPasswordPacketBytes);

            Assert.Equal(-1, Terraria.Netplay.Clients[5].State);
        }

        [Fact]
        public void PacketReceive_PlayerMana_EventTriggered()
        {
            // Set `State` to 10 so that the mana packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerManaEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.Mana);
                Assert.Equal(200, evt.MaxMana);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerManaPacketBytes);

            Assert.True(isRun);
            Assert.Equal(100, Terraria.Main.player[5].statMana);
            Assert.Equal(200, Terraria.Main.player[5].statManaMax);
        }

        [Fact]
        public void PacketReceive_PlayerMana_EventCanceled()
        {
            // Set `State` to 10 so that the mana packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerManaEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerManaPacketBytes);

            Assert.Equal(0, Terraria.Main.player[5].statMana);
            Assert.Equal(20, Terraria.Main.player[5].statManaMax);
        }

        [Fact]
        public void PacketReceive_PlayerTeam_EventTriggered()
        {
            // Set `State` to 10 so that the team packet is not ignored by the server. The socket must be set so that
            // the team message doesn't fail.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10, Socket = new TestSocket() };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerTeamEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(PlayerTeam.Red, evt.Team);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerTeamPacketBytes);

            Assert.True(isRun);
            Assert.Equal(PlayerTeam.Red, (PlayerTeam)Terraria.Main.player[5].team);
        }

        [Fact]
        public void PacketReceive_PlayerTeam_EventCanceled()
        {
            // Set `State` to 10 so that the team packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerTeamEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _playerTeamPacketBytes);

            Assert.Equal(0, Terraria.Main.player[5].team);
        }

        [Fact]
        public void PacketReceive_ClientUuid_EventTriggered()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerUuidEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal("Terraria", evt.Uuid);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _clientUuidPacketBytes);

            Assert.True(isRun);
        }

        [Fact]
        public void PacketReceive_ChatModule_EventTriggered()
        {
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5 };

            // Set up another player for the chat to be broadcast to.
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[6] = new Terraria.RemoteClient { Id = 6, State = 10, Socket = socket };
            Terraria.Main.player[6] = new Terraria.Player { whoAmI = 6 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PlayerChatEvent>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal("Say", evt.Command);
                Assert.Equal("/command test", evt.Message);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _chatModuleBytes);

            Assert.True(isRun);
            Assert.NotEmpty(socket.SendData);
        }

        [Fact]
        public void PacketReceive_ChatModule_EventCanceled()
        {
            // Set `State` to 10 so that the chat packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            // Set up another player for the chat to be broadcast to.
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[6] = new Terraria.RemoteClient { Id = 6, State = 10, Socket = socket };
            Terraria.Main.player[6] = new Terraria.Player { whoAmI = 6 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PlayerChatEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _chatModuleBytes);

            Assert.Empty(socket.SendData);
        }

        [Fact]
        public void PacketSend_EventTriggered()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketSendEvent<ClientConnectPacket>>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Receiver);
                Assert.Equal("Terraria" + Terraria.Main.curRelease, evt.Packet.Version);
                isRun = true;
            }, Logger.None);

            Terraria.NetMessage.SendData((byte)PacketId.ClientConnect, 5);

            Assert.True(isRun);
            Assert.Equal(_serverConnectPacketBytes, socket.SendData);
        }

        [Fact]
        public void PacketSend_UnknownPacket_EventTriggered()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketSendEvent<UnknownPacket>>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Receiver);
                Assert.Equal((PacketId)25, evt.Packet.Id);
                Assert.Equal(0, evt.Packet.Length);
                isRun = true;
            }, Logger.None);

            Terraria.NetMessage.SendData(25, 5);

            Assert.True(isRun);
            Assert.Equal(new byte[] { 3, 0, 25 }, socket.SendData);
        }

        [Fact]
        public void PacketSend_EventModified()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketSendEvent<ClientConnectPacket>>(
                evt => evt.Packet.Version = string.Empty, Logger.None);

            Terraria.NetMessage.SendData((byte)PacketId.ClientConnect, 5);

            Assert.NotEqual(_serverConnectPacketBytes, socket.SendData);
        }

        [Fact]
        public void PacketSend_EventCanceled()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketSendEvent<ClientConnectPacket>>(evt => evt.Cancel(), Logger.None);

            Terraria.NetMessage.SendData((byte)PacketId.ClientConnect, 5);

            Assert.Empty(socket.SendData);
        }

        [Fact]
        public void PacketSend_ThrowsIOException()
        {
            var socket = new BuggySocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            Terraria.NetMessage.SendData((byte)PacketId.ClientConnect, 5);
        }

        [Fact]
        public void ModuleSend_EventTriggered()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketSendEvent<ModulePacket<ChatModule>>>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Receiver);
                Assert.Equal(1, evt.Packet.Module.ServerAuthorIndex);
                Assert.Equal("test", evt.Packet.Module.ServerMessage);
                Assert.Equal(Color3.White, evt.Packet.Module.ServerColor);
                isRun = true;
            }, Logger.None);

            var packet = new Terraria.Net.NetPacket(1, 16);
            packet.Writer.Write((byte)1);
            Terraria.Localization.NetworkText.FromLiteral("test").Serialize(packet.Writer);
            Terraria.Utils.WriteRGB(packet.Writer, Microsoft.Xna.Framework.Color.White);
            Terraria.Net.NetManager.Instance.SendData(socket, packet);

            Assert.True(isRun);
            Assert.Equal(new byte[] { 15, 0, 82, 1, 0, 1, 0, 4, 116, 101, 115, 116, 255, 255, 255 }, socket.SendData);
        }

        [Fact]
        public void ModuleSend_UnknownModule_EventTriggered()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            var isRun = false;
            kernel.Events.RegisterHandler<PacketSendEvent<ModulePacket<UnknownModule>>>(evt =>
            {
                Assert.Same(playerService.Players[5], evt.Receiver);
                Assert.Equal((ModuleId)65535, evt.Packet.Module.Id);
                Assert.Equal(4, evt.Packet.Module.Length);
                isRun = true;
            }, Logger.None);

            var packet = new Terraria.Net.NetPacket(65535, 10);
            packet.Writer.Write(1234);
            Terraria.Net.NetManager.Instance.SendData(socket, packet);

            Assert.True(isRun);
            Assert.Equal(new byte[] { 9, 0, 82, 255, 255, 210, 4, 0, 0 }, socket.SendData);
        }

        [Fact]
        public void ModuleSend_EventModified()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketSendEvent<ModulePacket<ChatModule>>>(
                evt => evt.Packet.Module.ServerColor = Color3.Black, Logger.None);

            var packet = new Terraria.Net.NetPacket(1, 16);
            packet.Writer.Write((byte)1);
            Terraria.Localization.NetworkText.FromLiteral("test").Serialize(packet.Writer);
            Terraria.Utils.WriteRGB(packet.Writer, Microsoft.Xna.Framework.Color.White);
            Terraria.Net.NetManager.Instance.SendData(socket, packet);

            Assert.Equal(new byte[] { 15, 0, 82, 1, 0, 1, 0, 4, 116, 101, 115, 116, 0, 0, 0 }, socket.SendData);
        }

        [Fact]
        public void ModuleSend_EventCanceled()
        {
            var socket = new TestSocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            kernel.Events.RegisterHandler<PacketSendEvent<ModulePacket<ChatModule>>>(evt => evt.Cancel(), Logger.None);

            var packet = new Terraria.Net.NetPacket(1, 16);
            packet.Writer.Write((byte)1);
            Terraria.Localization.NetworkText.FromLiteral("test").Serialize(packet.Writer);
            Terraria.Utils.WriteRGB(packet.Writer, Microsoft.Xna.Framework.Color.White);
            Terraria.Net.NetManager.Instance.SendData(socket, packet);

            Assert.Empty(socket.SendData);
        }

        [Fact]
        public void ModuleSend_ThrowsIOException()
        {
            var socket = new BuggySocket { Connected = true };
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, Socket = socket };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);

            var packet = new Terraria.Net.NetPacket(1, 16);
            packet.Writer.Write((byte)1);
            Terraria.Localization.NetworkText.FromLiteral("test").Serialize(packet.Writer);
            Terraria.Utils.WriteRGB(packet.Writer, Microsoft.Xna.Framework.Color.White);
            Terraria.Net.NetManager.Instance.SendData(socket, packet);
        }

        private class TestSocket : Terraria.Net.Sockets.ISocket
        {
            public bool Connected { get; set; }
            public byte[] SendData { get; private set; } = Array.Empty<byte>();

            public void AsyncReceive(
                byte[] data, int offset, int size, Terraria.Net.Sockets.SocketReceiveCallback callback,
                object? state = null) =>
                    throw new NotImplementedException();
            public void AsyncSend(
                byte[] data, int offset, int size, Terraria.Net.Sockets.SocketSendCallback callback,
                object? state = null) =>
                    SendData = data[offset..(offset + size)];
            public void Close() => throw new NotImplementedException();
            public void Connect(Terraria.Net.RemoteAddress address) => throw new NotImplementedException();
            public Terraria.Net.RemoteAddress GetRemoteAddress() => throw new NotImplementedException();
            public bool IsConnected() => Connected;
            public bool IsDataAvailable() => throw new NotImplementedException();
            public void SendQueuedPackets() => throw new NotImplementedException();
            public bool StartListening(Terraria.Net.Sockets.SocketConnectionAccepted callback) =>
                throw new NotImplementedException();
            public void StopListening() => throw new NotImplementedException();
        }

        private class BuggySocket : Terraria.Net.Sockets.ISocket
        {
            public bool Connected { get; set; }

            public void AsyncReceive(
                byte[] data, int offset, int size, Terraria.Net.Sockets.SocketReceiveCallback callback,
                object? state = null) =>
                    throw new NotImplementedException();
            public void AsyncSend(
                byte[] data, int offset, int size, Terraria.Net.Sockets.SocketSendCallback callback,
                object? state = null) =>
                    throw new IOException();
            public void Close() => throw new NotImplementedException();
            public void Connect(Terraria.Net.RemoteAddress address) => throw new NotImplementedException();
            public Terraria.Net.RemoteAddress GetRemoteAddress() => throw new NotImplementedException();
            public bool IsConnected() => Connected;
            public bool IsDataAvailable() => throw new NotImplementedException();
            public void SendQueuedPackets() => throw new NotImplementedException();
            public bool StartListening(Terraria.Net.Sockets.SocketConnectionAccepted callback) =>
                throw new NotImplementedException();
            public void StopListening() => throw new NotImplementedException();
        }
    }
}
