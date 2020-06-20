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

using Moq;
using Orion.Core;
using Orion.Core.Events.World;
using Orion.Core.Events.World.Tiles;
using Orion.Core.Items;
using Orion.Core.Packets.World.Tiles;
using Orion.Core.Players;
using Orion.Core.World.Tiles;
using Orion.Launcher.Impl.Players;
using Serilog.Core;
using Xunit;

namespace Orion.Launcher.Impl.World
{
    // These tests depend on Terraria state.
    [Collection("TerrariaTestsCollection")]
    public class OrionWorldServiceTests
    {
        private static readonly byte[] _breakBlockBytes = { 11, 0, 17, 0, 100, 0, 0, 1, 0, 0, 0 };
        private static readonly byte[] _breakBlockFailureBytes = { 11, 0, 17, 0, 100, 0, 0, 1, 1, 0, 0 };
        private static readonly byte[] _placeBlockBytes = { 11, 0, 17, 1, 100, 0, 0, 1, 4, 0, 1 };
        private static readonly byte[] _breakWallBytes = { 11, 0, 17, 2, 100, 0, 0, 1, 0, 0, 0 };
        private static readonly byte[] _breakWallFailureBytes = { 11, 0, 17, 2, 100, 0, 0, 1, 1, 0, 0 };
        private static readonly byte[] _placeWallBytes = { 11, 0, 17, 3, 100, 0, 0, 1, 1, 0, 0 };
        private static readonly byte[] _breakBlockItemlessBytes = { 11, 0, 17, 4, 100, 0, 0, 1, 0, 0, 0 };
        private static readonly byte[] _breakBlockItemlessFailureBytes = { 11, 0, 17, 4, 100, 0, 0, 1, 1, 0, 0 };
        private static readonly byte[] _replaceBlockBytes = { 11, 0, 17, 21, 100, 0, 0, 1, 1, 0, 0 };
        private static readonly byte[] _replaceWallBytes = { 11, 0, 17, 22, 100, 0, 0, 1, 1, 0, 0 };

        private static readonly byte[] _tileSquareBytes =
        {
            41, 0, 20, 3, 0, 100, 0, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0, 4, 0, 1, 0, 2, 0, 4, 0, 1, 0, 8, 0, 255, 1, 0, 4, 1,
            0, 8, 1, 240, 131, 0, 0
        };

        private static readonly byte[] _tileLiquidBytes = { 9, 0, 48, 0, 1, 100, 0, 255, 2 };
        private static readonly byte[] _wireActivateBytes = { 7, 0, 59, 0, 1, 100, 0 };
        private static readonly byte[] _blockPaintBytes = { 8, 0, 63, 0, 1, 100, 0, 1 };
        private static readonly byte[] _wallPaintBytes = { 8, 0, 64, 0, 1, 100, 0, 1 };

        [Fact]
        public void Main_tile_Width_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Assert.Equal(Terraria.Main.maxTilesX, Terraria.Main.tile.Width);
        }

        [Fact]
        public void Main_tile_Height_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Assert.Equal(Terraria.Main.maxTilesY, Terraria.Main.tile.Height);
        }

        [Fact]
        public void Main_tile_type_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { BlockId = BlockId.Stone };

            Assert.Equal(BlockId.Stone, (BlockId)Terraria.Main.tile[0, 0].type);
        }

        [Fact]
        public void Main_tile_type_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].type = (ushort)BlockId.Stone;

            Assert.Equal(BlockId.Stone, worldService.World[0, 0].BlockId);
        }

        [Fact]
        public void Main_tile_wall_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { WallId = WallId.Stone };

            Assert.Equal(WallId.Stone, (WallId)Terraria.Main.tile[0, 0].wall);
        }

        [Fact]
        public void Main_tile_wall_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].wall = (ushort)WallId.Stone;

            Assert.Equal(WallId.Stone, worldService.World[0, 0].WallId);
        }

        [Fact]
        public void Main_tile_liquid_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { LiquidAmount = 100 };

            Assert.Equal(100, Terraria.Main.tile[0, 0].liquid);
        }

        [Fact]
        public void Main_tile_liquid_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].liquid = 100;

            Assert.Equal(100, worldService.World[0, 0].LiquidAmount);
        }

        [Fact]
        public void Main_tileHeader1_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HeaderPart = 12345 };

            Assert.Equal(12345, Terraria.Main.tile[0, 0].sTileHeader);
        }

        [Fact]
        public void Main_tileHeader1_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].sTileHeader = 12345;

            Assert.Equal(12345, worldService.World[0, 0].HeaderPart);
        }

        [Fact]
        public void Main_tileHeader2_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HeaderPart2 = 100 };

            Assert.Equal(100, Terraria.Main.tile[0, 0].bTileHeader);
        }

        [Fact]
        public void Main_tileHeader2_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].bTileHeader = 100;

            Assert.Equal(100, worldService.World[0, 0].HeaderPart2);
        }

        [Fact]
        public void Main_tileHeader3_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HeaderPart3 = 100 };

            Assert.Equal(100, Terraria.Main.tile[0, 0].bTileHeader3);
        }

        [Fact]
        public void Main_tileHeader3_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].bTileHeader3 = 100;

            Assert.Equal(100, worldService.World[0, 0].HeaderPart3);
        }

        [Fact]
        public void Main_tile_frameX_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { BlockFrameX = 12345 };

            Assert.Equal(12345, Terraria.Main.tile[0, 0].frameX);
        }

        [Fact]
        public void Main_tile_frameX_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].frameX = 12345;

            Assert.Equal(12345, worldService.World[0, 0].BlockFrameX);
        }

        [Fact]
        public void Main_tile_frameY_Get()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { BlockFrameY = 12345 };

            Assert.Equal(12345, Terraria.Main.tile[0, 0].frameY);
        }

        [Fact]
        public void Main_tile_frameY_Set()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[0, 0].frameY = 12345;

            Assert.Equal(12345, worldService.World[0, 0].BlockFrameY);
        }

        [Fact]
        public void Main_tile_color()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { BlockColor = PaintColor.Red };

            Assert.Equal((byte)PaintColor.Red, Terraria.Main.tile[0, 0].color());

            Terraria.Main.tile[0, 0].color((byte)PaintColor.DeepRed);

            Assert.Equal(PaintColor.DeepRed, worldService.World[0, 0].BlockColor);
        }

        [Fact]
        public void Main_tile_active()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { IsBlockActive = true };

            Assert.True(Terraria.Main.tile[0, 0].active());

            Terraria.Main.tile[0, 0].active(false);

            Assert.False(worldService.World[0, 0].IsBlockActive);
        }

        [Fact]
        public void Main_tile_inActive()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { IsBlockActuated = true };

            Assert.True(Terraria.Main.tile[0, 0].inActive());

            Terraria.Main.tile[0, 0].inActive(false);

            Assert.False(worldService.World[0, 0].IsBlockActuated);
        }

        [Fact]
        public void Main_tile_nactive()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { IsBlockActive = true };

            Assert.True(Terraria.Main.tile[0, 0].nactive());

            worldService.World[0, 0].IsBlockActuated = true;

            Assert.False(Terraria.Main.tile[0, 0].nactive());
        }

        [Fact]
        public void Main_tile_wire()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HasRedWire = true };

            Assert.True(Terraria.Main.tile[0, 0].wire());

            Terraria.Main.tile[0, 0].wire(false);

            Assert.False(worldService.World[0, 0].HasRedWire);
        }

        [Fact]
        public void Main_tile_wire2()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HasBlueWire = true };

            Assert.True(Terraria.Main.tile[0, 0].wire2());

            Terraria.Main.tile[0, 0].wire2(false);

            Assert.False(worldService.World[0, 0].HasBlueWire);
        }

        [Fact]
        public void Main_tile_wire3()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HasGreenWire = true };

            Assert.True(Terraria.Main.tile[0, 0].wire3());

            Terraria.Main.tile[0, 0].wire3(false);

            Assert.False(worldService.World[0, 0].HasGreenWire);
        }

        [Fact]
        public void Main_tile_halfBrick()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { IsBlockHalved = true };

            Assert.True(Terraria.Main.tile[0, 0].halfBrick());

            Terraria.Main.tile[0, 0].halfBrick(false);

            Assert.False(worldService.World[0, 0].IsBlockHalved);
        }

        [Fact]
        public void Main_tile_actuator()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { HasActuator = true };

            Assert.True(Terraria.Main.tile[0, 0].actuator());

            Terraria.Main.tile[0, 0].actuator(false);

            Assert.False(worldService.World[0, 0].HasActuator);
        }

        [Fact]
        public void Main_tile_slope()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Slope = Slope.BottomRight };

            Assert.Equal((byte)Slope.BottomRight, Terraria.Main.tile[0, 0].slope());

            Terraria.Main.tile[0, 0].slope((byte)Slope.BottomLeft);

            Assert.Equal(Slope.BottomLeft, worldService.World[0, 0].Slope);
        }

        [Fact]
        public void Main_tile_wallColor()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { WallColor = PaintColor.Red };

            Assert.Equal((byte)PaintColor.Red, Terraria.Main.tile[0, 0].wallColor());

            Terraria.Main.tile[0, 0].wallColor((byte)PaintColor.DeepRed);

            Assert.Equal(PaintColor.DeepRed, worldService.World[0, 0].WallColor);
        }

        [Fact]
        public void Main_tile_lava()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Liquid = Liquid.Lava };

            Assert.True(Terraria.Main.tile[0, 0].lava());

            Terraria.Main.tile[0, 0].lava(false);

            Assert.NotEqual(Liquid.Lava, worldService.World[0, 0].Liquid);

            Terraria.Main.tile[0, 0].lava(true);

            Assert.Equal(Liquid.Lava, worldService.World[0, 0].Liquid);
        }

        [Fact]
        public void Main_tile_honey()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { Liquid = Liquid.Honey };

            Assert.True(Terraria.Main.tile[0, 0].honey());

            Terraria.Main.tile[0, 0].honey(false);

            Assert.NotEqual(Liquid.Honey, worldService.World[0, 0].Liquid);

            Terraria.Main.tile[0, 0].honey(true);

            Assert.Equal(Liquid.Honey, worldService.World[0, 0].Liquid);
        }

        [Fact]
        public void Main_tile_liquidType()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { Liquid = Liquid.Lava };

            Assert.Equal((byte)Liquid.Lava, Terraria.Main.tile[0, 0].liquidType());

            Terraria.Main.tile[0, 0].liquidType((int)Liquid.Honey);

            Assert.Equal(Liquid.Honey, worldService.World[0, 0].Liquid);
        }

        [Fact]
        public void Main_tile_wire4()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { HasYellowWire = true };

            Assert.True(Terraria.Main.tile[0, 0].wire4());

            Terraria.Main.tile[0, 0].wire4(false);

            Assert.False(worldService.World[0, 0].HasYellowWire);
        }

        [Fact]
        public void Main_tile_frameNumber()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { BlockFrameNumber = 7 };

            Assert.Equal(7, Terraria.Main.tile[0, 0].frameNumber());

            Terraria.Main.tile[0, 0].frameNumber(5);

            Assert.Equal(5, worldService.World[0, 0].BlockFrameNumber);
        }

        [Fact]
        public void Main_tile_checkingLiquid()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { IsCheckingLiquid = true };

            Assert.True(Terraria.Main.tile[0, 0].checkingLiquid());

            Terraria.Main.tile[0, 0].checkingLiquid(false);

            Assert.False(worldService.World[0, 0].IsCheckingLiquid);
        }

        [Fact]
        public void Main_tile_skipLiquid()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { ShouldSkipLiquid = true };

            Assert.True(Terraria.Main.tile[0, 0].skipLiquid());

            Terraria.Main.tile[0, 0].skipLiquid(false);

            Assert.False(worldService.World[0, 0].ShouldSkipLiquid);
        }

        [Fact]
        public void Main_tile_CopyFrom_NullTile()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = (BlockId)1,
                WallId = (WallId)2,
                LiquidAmount = 3,
                HeaderPart = 12345,
                HeaderPart2 = 100,
                HeaderPart3 = 101,
                BlockFrameX = 4,
                BlockFrameY = 5
            };

            Terraria.Main.tile[0, 0].CopyFrom(null);

            Assert.Equal(BlockId.Dirt, worldService.World[0, 0].BlockId);
            Assert.Equal(WallId.None, worldService.World[0, 0].WallId);
            Assert.Equal(0, worldService.World[0, 0].LiquidAmount);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart2);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart3);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameY);
        }

        [Fact]
        public void Main_tile_CopyFrom_TileAdapter()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = BlockId.Stone,
                WallId = WallId.Dirt,
                LiquidAmount = 3,
                HeaderPart = 12345,
                HeaderPart2 = 100,
                HeaderPart3 = 101,
                BlockFrameX = 4,
                BlockFrameY = 5
            };

            Terraria.Main.tile[0, 1].CopyFrom(Terraria.Main.tile[0, 0]);

            Assert.Equal(BlockId.Stone, worldService.World[0, 1].BlockId);
            Assert.Equal(WallId.Dirt, worldService.World[0, 1].WallId);
            Assert.Equal(3, worldService.World[0, 1].LiquidAmount);
            Assert.Equal(12345, worldService.World[0, 1].HeaderPart);
            Assert.Equal(100, worldService.World[0, 1].HeaderPart2);
            Assert.Equal(101, worldService.World[0, 1].HeaderPart3);
            Assert.Equal(4, worldService.World[0, 1].BlockFrameX);
            Assert.Equal(5, worldService.World[0, 1].BlockFrameY);
        }

        [Fact]
        public void Main_tile_isTheSameAs_NullTile_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(null));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentHeader_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { IsBlockActive = true };
            worldService.World[0, 1] = new Tile();

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentHeader2_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                LiquidAmount = 1,
                HeaderPart2 = 1
            };
            worldService.World[0, 1] = new Tile
            {
                LiquidAmount = 1,
                HeaderPart2 = 2
            };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentBlockIdAndBlockActive_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Stone
            };
            worldService.World[0, 1] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Dirt
            };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentBlockIdButNotBlockActive_ReturnsTrue()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { BlockId = BlockId.Stone };
            worldService.World[0, 1] = new Tile { BlockId = BlockId.Dirt };

            Assert.True(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentBlockFrameXAndHasFrames_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameX = 1
            };
            worldService.World[0, 1] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameX = 2
            };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentBlockFrameYAndHasFrames_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameY = 1
            };
            worldService.World[0, 1] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameY = 2
            };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentBlockFramesButNotHasFrames_ReturnsTrue()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Stone,
                BlockFrameX = 1,
                BlockFrameY = 1
            };
            worldService.World[0, 1] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Stone,
                BlockFrameX = 2,
                BlockFrameY = 2
            };

            Assert.True(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentWallId_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { WallId = WallId.Stone };
            worldService.World[0, 1] = new Tile { WallId = WallId.NaturalDirt };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentLiquidAmount_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { LiquidAmount = 1 };
            worldService.World[0, 1] = new Tile { LiquidAmount = 2 };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentWallColor_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { WallColor = PaintColor.Red };
            worldService.World[0, 1] = new Tile { WallColor = PaintColor.DeepRed };

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_TileAdapterDifferentYellowWire_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { HasYellowWire = true };
            worldService.World[0, 1] = new Tile();

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentHeader_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { IsBlockActive = true };
            var tile = Mock.Of<OTAPI.Tile.ITile>();

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentHeader2_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                LiquidAmount = 1,
                HeaderPart2 = 1
            };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.liquid == 1 && t.bTileHeader == 2);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentBlockIdAndBlockActive_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Stone
            };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.sTileHeader == 32 && t.type == (ushort)BlockId.Dirt);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentBlockIdButNotBlockActive_ReturnsTrue()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { BlockId = BlockId.Stone };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.type == (ushort)BlockId.Dirt);

            Assert.True(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentBlockFrameXAndHasFrames_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameX = 1
            };
            var tile = Mock.Of<OTAPI.Tile.ITile>(
                t => t.sTileHeader == 32 && t.type == (ushort)BlockId.Torches && t.frameX == 2);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentBlockFrameYAndHasFrames_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Torches,
                BlockFrameY = 1
            };
            var tile = Mock.Of<OTAPI.Tile.ITile>(
                t => t.sTileHeader == 32 && t.type == (ushort)BlockId.Torches && t.frameY == 2);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentBlockFramesButNotHasFrames_ReturnsTrue()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                IsBlockActive = true,
                BlockId = BlockId.Stone,
                BlockFrameX = 1,
                BlockFrameY = 1
            };
            var tile = Mock.Of<OTAPI.Tile.ITile>(
                t => t.sTileHeader == 32 && t.type == (ushort)BlockId.Stone && t.frameX == 2 && t.frameY == 2);

            Assert.True(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentWallId_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { WallId = WallId.Stone };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.wall == (ushort)WallId.NaturalDirt);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentLiquidAmount_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { LiquidAmount = 1 };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.liquid == 2);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentWallColor_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { WallColor = PaintColor.Red };
            var tile = Mock.Of<OTAPI.Tile.ITile>(t => t.bTileHeader == 13);

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_isTheSameAs_ITileDifferentYellowWire_ReturnsFalse()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { HasYellowWire = true };
            var tile = Mock.Of<OTAPI.Tile.ITile>();

            Assert.False(Terraria.Main.tile[0, 0].isTheSameAs(tile));
        }

        [Fact]
        public void Main_tile_ClearEverything()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = BlockId.Stone,
                WallId = WallId.Dirt,
                LiquidAmount = 3,
                HeaderPart = 12345,
                HeaderPart2 = 100,
                HeaderPart3 = 101,
                BlockFrameX = 4,
                BlockFrameY = 5
            };

            Terraria.Main.tile[0, 0].ClearEverything();

            Assert.Equal(BlockId.Dirt, worldService.World[0, 0].BlockId);
            Assert.Equal(WallId.None, worldService.World[0, 0].WallId);
            Assert.Equal(0, worldService.World[0, 0].LiquidAmount);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart2);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart3);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameY);
        }

        [Fact]
        public void Main_tile_ClearMetadata()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = BlockId.Stone,
                WallId = WallId.Dirt,
                LiquidAmount = 3,
                HeaderPart = 12345,
                HeaderPart2 = 100,
                HeaderPart3 = 101,
                BlockFrameX = 4,
                BlockFrameY = 5
            };

            Terraria.Main.tile[0, 0].ClearMetadata();

            Assert.Equal(BlockId.Stone, worldService.World[0, 0].BlockId);
            Assert.Equal(WallId.Dirt, worldService.World[0, 0].WallId);
            Assert.Equal(0, worldService.World[0, 0].LiquidAmount);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart2);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart3);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameY);
        }

        [Fact]
        public void Main_tile_ClearTile()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                Slope = Slope.BottomRight,
                IsBlockHalved = true,
                IsBlockActive = true,
                IsBlockActuated = true
            };

            Terraria.Main.tile[0, 0].ClearTile();

            Assert.Equal(Slope.None, worldService.World[0, 0].Slope);
            Assert.False(worldService.World[0, 0].IsBlockHalved);
            Assert.False(worldService.World[0, 0].IsBlockActive);
            Assert.False(worldService.World[0, 0].IsBlockActuated);
        }

        [Fact]
        public void Main_tile_Clear_Tile()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = BlockId.Stone,
                IsBlockActive = true,
                BlockFrameX = 1,
                BlockFrameY = 2
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Tile);

            Assert.Equal(BlockId.Dirt, worldService.World[0, 0].BlockId);
            Assert.False(worldService.World[0, 0].IsBlockActive);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
        }

        [Fact]
        public void Main_tile_Clear_TilePaint()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockColor = PaintColor.Red
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.TilePaint);

            Assert.Equal(PaintColor.None, worldService.World[0, 0].BlockColor);
        }

        [Fact]
        public void Main_tile_Clear_Wall()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                WallId = WallId.Dirt
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Wall);

            Assert.Equal(WallId.None, worldService.World[0, 0].WallId);
        }

        [Fact]
        public void Main_tile_Clear_WallPaint()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                WallColor = PaintColor.Red
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.WallPaint);

            Assert.Equal(PaintColor.None, worldService.World[0, 0].WallColor);
        }

        [Fact]
        public void Main_tile_Clear_Liquid()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                LiquidAmount = 100,
                Liquid = Liquid.Honey,
                IsCheckingLiquid = true
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Liquid);

            Assert.Equal(0, worldService.World[0, 0].LiquidAmount);
            Assert.Equal(Liquid.Water, worldService.World[0, 0].Liquid);
            Assert.False(worldService.World[0, 0].IsCheckingLiquid);
        }

        [Fact]
        public void Main_tile_Clear_Wiring()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                HasRedWire = true,
                HasBlueWire = true,
                HasGreenWire = true,
                HasYellowWire = true
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Wiring);

            Assert.False(worldService.World[0, 0].HasRedWire);
            Assert.False(worldService.World[0, 0].HasBlueWire);
            Assert.False(worldService.World[0, 0].HasGreenWire);
            Assert.False(worldService.World[0, 0].HasYellowWire);
        }

        [Fact]
        public void Main_tile_Clear_Actuator()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                HasActuator = true,
                IsBlockActuated = true
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Actuator);

            Assert.False(worldService.World[0, 0].HasActuator);
            Assert.False(worldService.World[0, 0].IsBlockActuated);
        }

        [Fact]
        public void Main_tile_Clear_Slope()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                Slope = Slope.TopLeft,
                IsBlockHalved = true
            };

            Terraria.Main.tile[0, 0].Clear(Terraria.DataStructures.TileDataType.Slope);

            Assert.Equal(Slope.None, worldService.World[0, 0].Slope);
            Assert.False(worldService.World[0, 0].IsBlockHalved);
        }

        [Fact]
        public void Main_tile_ResetToType()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile
            {
                BlockId = BlockId.Stone,
                WallId = WallId.Dirt,
                LiquidAmount = 3,
                HeaderPart = 12345,
                HeaderPart2 = 100,
                HeaderPart3 = 101,
                BlockFrameX = 4,
                BlockFrameY = 5
            };

            Terraria.Main.tile[0, 0].ResetToType((ushort)BlockId.Stone);

            Assert.Equal(BlockId.Stone, worldService.World[0, 0].BlockId);
            Assert.Equal(WallId.Dirt, worldService.World[0, 0].WallId);
            Assert.Equal(0, worldService.World[0, 0].LiquidAmount);
            Assert.Equal(32, worldService.World[0, 0].HeaderPart);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart2);
            Assert.Equal(0, worldService.World[0, 0].HeaderPart3);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameX);
            Assert.Equal(0, worldService.World[0, 0].BlockFrameY);
        }

        [Theory]
        [InlineData(Slope.None, false)]
        [InlineData(Slope.TopLeft, true)]
        [InlineData(Slope.TopRight, true)]
        [InlineData(Slope.BottomLeft, false)]
        [InlineData(Slope.BottomRight, false)]
        public void Main_tile_topSlope(Slope slope, bool value)
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Slope = slope };

            Assert.Equal(value, Terraria.Main.tile[0, 0].topSlope());
        }

        [Theory]
        [InlineData(Slope.None, false)]
        [InlineData(Slope.TopLeft, false)]
        [InlineData(Slope.TopRight, false)]
        [InlineData(Slope.BottomLeft, true)]
        [InlineData(Slope.BottomRight, true)]
        public void Main_tile_bottomSlope(Slope slope, bool value)
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Slope = slope };

            Assert.Equal(value, Terraria.Main.tile[0, 0].bottomSlope());
        }

        [Theory]
        [InlineData(Slope.None, false)]
        [InlineData(Slope.TopLeft, true)]
        [InlineData(Slope.TopRight, false)]
        [InlineData(Slope.BottomLeft, true)]
        [InlineData(Slope.BottomRight, false)]
        public void Main_tile_leftSlope(Slope slope, bool value)
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Slope = slope };

            Assert.Equal(value, Terraria.Main.tile[0, 0].leftSlope());
        }

        [Theory]
        [InlineData(Slope.None, false)]
        [InlineData(Slope.TopLeft, false)]
        [InlineData(Slope.TopRight, true)]
        [InlineData(Slope.BottomLeft, false)]
        [InlineData(Slope.BottomRight, true)]
        public void Main_tile_rightSlope(Slope slope, bool value)
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            worldService.World[0, 0] = new Tile { Slope = slope };

            Assert.Equal(value, Terraria.Main.tile[0, 0].rightSlope());
        }

        [Fact]
        public void Main_tile_HasSameSlope()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { Slope = Slope.BottomRight };
            worldService.World[0, 1] = new Tile { Slope = Slope.BottomRight };

            Assert.True(Terraria.Main.tile[0, 0].HasSameSlope(Terraria.Main.tile[0, 1]));

            worldService.World[0, 1].Slope = Slope.BottomLeft;

            Assert.False(Terraria.Main.tile[0, 0].HasSameSlope(Terraria.Main.tile[0, 1]));
        }

        [Fact]
        public void Main_tile_blockType()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile();

            Assert.Equal(0, Terraria.Main.tile[0, 0].blockType());
        }

        [Fact]
        public void Main_tile_blockType_Halved()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { IsBlockHalved = true };

            Assert.Equal(1, Terraria.Main.tile[0, 0].blockType());
        }

        [Fact]
        public void Main_tile_blockType_Slope()
        {
            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);
            worldService.World[0, 0] = new Tile { Slope = Slope.BottomRight };

            Assert.Equal((int)Slope.BottomRight + 1, Terraria.Main.tile[0, 0].blockType());
        }

        [Fact]
        public void WorldSave_EventTriggered()
        {
            Terraria.IO.WorldFile._tempTime = 0.0;

            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            var isRun = false;
            kernel.Events.RegisterHandler<WorldSaveEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                isRun = true;
            }, Logger.None);

            Terraria.IO.WorldFile.SaveWorld(false, true);

            Assert.True(isRun);
            Assert.Equal(13500.0, Terraria.IO.WorldFile._tempTime);
        }

        [Fact]
        public void WorldSave_EventCanceled()
        {
            Terraria.IO.WorldFile._tempTime = 0.0;

            using var kernel = new OrionKernel(Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            kernel.Events.RegisterHandler<WorldSaveEvent>(evt => evt.Cancel(), Logger.None);

            Terraria.IO.WorldFile.SaveWorld(false, true);

            Assert.Equal(0.0, Terraria.IO.WorldFile._tempTime);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlock_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockBreakEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.False(evt.IsItemless);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockBytes);

            Assert.True(isRun);
            Assert.False(Terraria.Main.tile[100, 256].active());
            Assert.Equal(ItemId.DirtBlock, (ItemId)Terraria.Main.item[0].type);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlock_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);

            kernel.Events.RegisterHandler<BlockBreakEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockBytes);

            Assert.True(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlockFailure_EventNotTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockBreakEvent>(evt => isRun = true, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockFailureBytes);

            Assert.False(isRun);
        }

        [Fact]
        public void PacketReceive_TileModify_PlaceBlock_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 257] = new Terraria.Tile();
            Terraria.Main.tile[100, 257].active(true);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockPlaceEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.Equal(BlockId.Torches, evt.Id);
                Assert.Equal(1, evt.Style);
                Assert.False(evt.IsReplacement);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _placeBlockBytes);

            Assert.True(isRun);
            Assert.Equal(BlockId.Torches, (BlockId)Terraria.Main.tile[100, 256].type);
            Assert.True(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_PlaceBlock_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 257] = new Terraria.Tile();
            Terraria.Main.tile[100, 257].active(true);

            kernel.Events.RegisterHandler<BlockPlaceEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _placeBlockBytes);

            Assert.False(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_BreakWall_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile { wall = (ushort)WallId.Stone };

            var isRun = false;
            kernel.Events.RegisterHandler<WallBreakEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakWallBytes);

            Assert.True(isRun);
            Assert.Equal(WallId.None, (WallId)Terraria.Main.tile[100, 256].wall);
            Assert.Equal(ItemId.StoneWall, (ItemId)Terraria.Main.item[0].type);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakWall_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile { wall = (ushort)WallId.Stone };

            kernel.Events.RegisterHandler<WallBreakEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakWallBytes);

            Assert.Equal(WallId.Stone, (WallId)Terraria.Main.tile[100, 256].wall);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakWallFailure_EventNotTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile { wall = (ushort)WallId.Stone };

            var isRun = false;
            kernel.Events.RegisterHandler<WallBreakEvent>(evt => isRun = true, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakWallFailureBytes);

            Assert.False(isRun);
        }

        [Fact]
        public void PacketReceive_TileModify_PlaceWall_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();

            var isRun = false;
            kernel.Events.RegisterHandler<WallPlaceEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.Equal(WallId.Stone, evt.Id);
                Assert.False(evt.IsReplacement);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _placeWallBytes);

            Assert.True(isRun);
            Assert.Equal(WallId.Stone, (WallId)Terraria.Main.tile[100, 256].wall);
        }

        [Fact]
        public void PacketReceive_TileModify_PlaceWall_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();

            kernel.Events.RegisterHandler<WallPlaceEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _placeWallBytes);

            Assert.Equal(WallId.None, (WallId)Terraria.Main.tile[100, 256].wall);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlockItemless_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockBreakEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.True(evt.IsItemless);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockItemlessBytes);

            Assert.True(isRun);
            Assert.False(Terraria.Main.tile[100, 256].active());
            Assert.Equal(ItemId.None, (ItemId)Terraria.Main.item[0].type);
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlockItemless_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);

            kernel.Events.RegisterHandler<BlockBreakEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockItemlessBytes);

            Assert.True(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_BreakBlockItemlessFailure_EventNotTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockBreakEvent>(evt => isRun = true, Logger.None);

            TestUtils.FakeReceiveBytes(5, _breakBlockItemlessFailureBytes);

            Assert.False(isRun);
        }

        [Fact]
        public void PacketReceive_TileModify_ReplaceBlock_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);
            Terraria.Main.tile[100, 257] = new Terraria.Tile();
            Terraria.Main.tile[100, 257].active(true);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockPlaceEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.Equal(BlockId.Stone, evt.Id);
                Assert.Equal(0, evt.Style);
                Assert.True(evt.IsReplacement);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _replaceBlockBytes);

            Assert.True(isRun);
            Assert.Equal(BlockId.Stone, (BlockId)Terraria.Main.tile[100, 256].type);
            Assert.True(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_ReplaceBlock_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile();
            Terraria.Main.tile[100, 256].active(true);
            Terraria.Main.tile[100, 257] = new Terraria.Tile();
            Terraria.Main.tile[100, 257].active(true);

            kernel.Events.RegisterHandler<BlockPlaceEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _replaceBlockBytes);

            Assert.Equal(BlockId.Dirt, (BlockId)Terraria.Main.tile[100, 256].type);
            Assert.True(Terraria.Main.tile[100, 256].active());
        }

        [Fact]
        public void PacketReceive_TileModify_ReplaceWall_EventTriggered()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile { wall = (ushort)WallId.Dirt };

            var isRun = false;
            kernel.Events.RegisterHandler<WallPlaceEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                Assert.Equal(WallId.Stone, evt.Id);
                Assert.True(evt.IsReplacement);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _replaceWallBytes);

            Assert.True(isRun);
            Assert.Equal(WallId.Stone, (WallId)Terraria.Main.tile[100, 256].wall);
        }

        [Fact]
        public void PacketReceive_TileModify_ReplaceWall_EventCanceled()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };
            Terraria.Main.item[0] = new Terraria.Item { whoAmI = 0 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[100, 256] = new Terraria.Tile { wall = (ushort)WallId.Dirt };

            kernel.Events.RegisterHandler<WallPlaceEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _replaceWallBytes);

            Assert.Equal(WallId.Dirt, (WallId)Terraria.Main.tile[100, 256].wall);
        }

        [Fact]
        public void PacketReceive_TileModifyInvalidModification()
        {
            // Set `State` to 10 so that the tile modify packet is not ignored by the server, and mark the relevant
            // `TileSections` entry so that the tile modify packet is not treated with failure.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Netplay.Clients[5].TileSections[0, 1] = true;
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            TestUtils.FakeReceiveBytes(5, new byte[] { 11, 0, 17, 255, 100, 0, 0, 1, 1, 0, 0 });
        }

        [Fact]
        public void PacketReceive_TileSquare_EventTriggered()
        {
            // Set `State` to 10 so that the tile square packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            for (var i = 0; i < 3; ++i)
            {
                for (var j = 0; j < 3; ++j)
                {
                    Terraria.Main.tile[100 + i, 256 + j] = new Terraria.Tile();
                }
            }

            var isRun = false;
            kernel.Events.RegisterHandler<TileSquareEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(100, evt.X);
                Assert.Equal(256, evt.Y);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _tileSquareBytes);

            Assert.True(isRun);
            Assert.Equal(BlockId.Dirt, worldService.World[100, 256].BlockId);
            Assert.False(worldService.World[100, 256].IsBlockActive);
        }

        [Fact]
        public void PacketReceive_TileSquare_EventModified()
        {
            // Set `State` to 10 so that the tile square packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            for (var i = 0; i < 3; ++i)
            {
                for (var j = 0; j < 3; ++j)
                {
                    Terraria.Main.tile[100 + i, 256 + j] = new Terraria.Tile();
                }
            }

            kernel.Events.RegisterHandler<TileSquareEvent>(evt =>
            {
                evt.Tiles[0, 0].BlockId = BlockId.Stone;
                evt.Tiles[0, 0].IsBlockActive = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _tileSquareBytes);

            Assert.Equal(BlockId.Stone, worldService.World[100, 256].BlockId);
            Assert.True(worldService.World[100, 256].IsBlockActive);
        }

        [Fact]
        public void PacketReceive_TileSquare_EventCanceled()
        {
            // Set `State` to 10 so that the tile square packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            for (var i = 0; i < 3; ++i)
            {
                for (var j = 0; j < 3; ++j)
                {
                    Terraria.Main.tile[2206 + i, 312 + j] = new Terraria.Tile();
                }
            }

            kernel.Events.RegisterHandler<TileSquareEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _tileSquareBytes);

            Assert.False(worldService.World[2206, 312].IsBlockActive);
        }

        [Fact]
        public void PacketReceive_TileLiquid_EventTriggered()
        {
            // Set `State` to 10 so that the tile liquid packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile();

            var isRun = false;
            kernel.Events.RegisterHandler<TileLiquidEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(256, evt.X);
                Assert.Equal(100, evt.Y);
                Assert.Equal(255, evt.LiquidAmount);
                Assert.Equal(Liquid.Honey, evt.Liquid);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _tileLiquidBytes);

            Assert.True(isRun);
            Assert.Equal(255, worldService.World[256, 100].LiquidAmount);
            Assert.Equal(Liquid.Honey, worldService.World[256, 100].Liquid);
        }

        [Fact]
        public void PacketReceive_TileLiquid_EventCanceled()
        {
            // Set `State` to 10 so that the tile liquid packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile();

            kernel.Events.RegisterHandler<TileLiquidEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _tileLiquidBytes);

            Assert.Equal(0, worldService.World[256, 100].LiquidAmount);
        }

        [Fact]
        public void PacketReceive_WireActivate_EventTriggered()
        {
            // Set `State` to 10 so that the wire activate packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile { type = (ushort)BlockId.Switch };
            Terraria.Main.tile[256, 100].active(true);
            Terraria.Main.tile[256, 100].wire(true);
            Terraria.Main.tile[257, 100] = new Terraria.Tile { type = (ushort)BlockId.Stone };
            Terraria.Main.tile[257, 100].active(true);
            Terraria.Main.tile[257, 100].wire(true);
            Terraria.Main.tile[257, 100].actuator(true);

            var isRun = false;
            kernel.Events.RegisterHandler<WiringActivateEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(256, evt.X);
                Assert.Equal(100, evt.Y);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _wireActivateBytes);

            Assert.True(isRun);
            Assert.True(worldService.World[257, 100].IsBlockActuated);
        }

        [Fact]
        public void PacketReceive_WireActivate_EventCanceled()
        {
            // Set `State` to 10 so that the wire activate packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile { type = (ushort)BlockId.Switch };
            Terraria.Main.tile[256, 100].active(true);
            Terraria.Main.tile[256, 100].wire(true);
            Terraria.Main.tile[257, 100] = new Terraria.Tile { type = (ushort)BlockId.Stone };
            Terraria.Main.tile[257, 100].active(true);
            Terraria.Main.tile[257, 100].wire(true);
            Terraria.Main.tile[257, 100].actuator(true);

            kernel.Events.RegisterHandler<WiringActivateEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _wireActivateBytes);

            Assert.False(worldService.World[257, 100].IsBlockActuated);
        }

        [Fact]
        public void PacketReceive_BlockPaint_EventTriggered()
        {
            // Set `State` to 10 so that the block paint packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile();
            Terraria.Main.tile[256, 100].active(true);

            var isRun = false;
            kernel.Events.RegisterHandler<BlockPaintEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(256, evt.X);
                Assert.Equal(100, evt.Y);
                Assert.Equal(PaintColor.Red, evt.Color);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _blockPaintBytes);

            Assert.True(isRun);
            Assert.Equal(PaintColor.Red, worldService.World[256, 100].BlockColor);
        }

        [Fact]
        public void PacketReceive_BlockPaint_EventCanceled()
        {
            // Set `State` to 10 so that the block paint packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile();
            Terraria.Main.tile[256, 100].active(true);

            kernel.Events.RegisterHandler<BlockPaintEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _blockPaintBytes);

            Assert.Equal(PaintColor.None, worldService.World[256, 100].BlockColor);
        }

        [Fact]
        public void PacketReceive_WallPaint_EventTriggered()
        {
            // Set `State` to 10 so that the wall paint packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile { wall = (byte)WallId.Stone };

            var isRun = false;
            kernel.Events.RegisterHandler<WallPaintEvent>(evt =>
            {
                Assert.Same(worldService.World, evt.World);
                Assert.Same(playerService.Players[5], evt.Player);
                Assert.Equal(256, evt.X);
                Assert.Equal(100, evt.Y);
                Assert.Equal(PaintColor.Red, evt.Color);
                isRun = true;
            }, Logger.None);

            TestUtils.FakeReceiveBytes(5, _wallPaintBytes);

            Assert.True(isRun);
            Assert.Equal(PaintColor.Red, worldService.World[256, 100].WallColor);
        }

        [Fact]
        public void PacketReceive_WallPaint_EventCanceled()
        {
            // Set `State` to 10 so that the wall paint packet is not ignored by the server.
            Terraria.Netplay.Clients[5] = new Terraria.RemoteClient { Id = 5, State = 10 };
            Terraria.Main.player[5] = new Terraria.Player { whoAmI = 5 };

            using var kernel = new OrionKernel(Logger.None);
            using var playerService = new OrionPlayerService(kernel, Logger.None);
            using var worldService = new OrionWorldService(kernel, Logger.None);

            Terraria.Main.tile[256, 100] = new Terraria.Tile { wall = (byte)WallId.Stone };

            kernel.Events.RegisterHandler<WallPaintEvent>(evt => evt.Cancel(), Logger.None);

            TestUtils.FakeReceiveBytes(5, _wallPaintBytes);

            Assert.Equal(PaintColor.None, worldService.World[256, 100].WallColor);
        }
    }
}
