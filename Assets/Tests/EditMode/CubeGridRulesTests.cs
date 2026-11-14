using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class CubeGridRulesTests
    {
        [Test]
        public void CellPositions_FollowV1Rule()
        {
            var cell = new Vector3Int(2, -1, 3);
            Assert.AreEqual(new Vector3(2f, -0.5f, 3f), CubeGridRules.CellCenter(cell));
            Assert.AreEqual(new Vector3(2f, -1f, 3f), CubeGridRules.CellBottom(cell));
            Assert.AreEqual(cell, CubeGridRules.CellAt(CubeGridRules.CellCenter(cell)));
            Assert.AreEqual(cell, CubeGridRules.CellAt(CubeGridRules.CellBottom(cell)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), CubeGridRules.CellAt(new Vector3(0.49f, 0.99f, -0.49f)));
        }

        [Test]
        public void IsOnCellBottom_NeedsIntegerCoordinates()
        {
            Assert.IsTrue(CubeGridRules.IsOnCellBottom(new Vector3(3f, 0f, -2f)));
            Assert.IsFalse(CubeGridRules.IsOnCellBottom(new Vector3(3.5f, 0f, -2f)));
            Assert.IsFalse(CubeGridRules.IsOnCellBottom(new Vector3(3f, 0.5f, -2f)));
        }

        [Test]
        public void AddAndRemoveBlock_KeepOneBlockPerCell()
        {
            var data = new CubeGridData();
            var cell = new Vector3Int(1, 0, 1);
            Assert.IsTrue(CubeGridRules.AddBlock(data, cell));
            Assert.IsFalse(CubeGridRules.AddBlock(data, cell));
            Assert.AreEqual(1, data.blocks.Count);
            Assert.IsTrue(CubeGridRules.Remove(data, cell));
            Assert.IsFalse(CubeGridRules.Remove(data, cell));
            Assert.AreEqual(0, data.blocks.Count);
        }

        [Test]
        public void Stair_SharesOneThingPerCellWithBlocks()
        {
            var data = new CubeGridData();
            var block = new Vector3Int(0, 0, 0);
            var stair = new Vector3Int(1, 0, 0);
            Assert.IsTrue(CubeGridRules.AddBlock(data, block));
            Assert.IsFalse(CubeGridRules.AddStair(data, block, 0));
            Assert.IsTrue(CubeGridRules.AddStair(data, stair, 5));
            Assert.AreEqual(1, data.stairs[0].turn);
            Assert.IsFalse(CubeGridRules.AddStair(data, stair, 0));
            Assert.IsFalse(CubeGridRules.AddBlock(data, stair));
            Assert.IsTrue(CubeGridRules.IsOccupied(data, stair));
            Assert.IsTrue(CubeGridRules.Remove(data, stair));
            Assert.IsFalse(CubeGridRules.IsOccupied(data, stair));
        }

        [Test]
        public void TurnFacing_SnapsToNearestAxis()
        {
            Assert.AreEqual(0, CubeGridRules.TurnFacing(new Vector3(0.3f, -1f, 1f)));
            Assert.AreEqual(1, CubeGridRules.TurnFacing(new Vector3(1f, 0f, 0.4f)));
            Assert.AreEqual(2, CubeGridRules.TurnFacing(new Vector3(-0.2f, 0f, -1f)));
            Assert.AreEqual(3, CubeGridRules.TurnFacing(new Vector3(-1f, 0f, -0.3f)));
            var up = CubeGridRules.StairRotation(1) * Vector3.forward;
            Assert.AreEqual(1f, up.x, 1e-5f);
        }

        [Test]
        public void Box_CoversBothCornersInAnyOrder()
        {
            var cells = CubeGridRules.Box(new Vector3Int(2, -1, 1), new Vector3Int(0, -1, 0));
            Assert.AreEqual(6, cells.Count);
            Assert.Contains(new Vector3Int(0, -1, 0), cells);
            Assert.Contains(new Vector3Int(2, -1, 1), cells);
        }

        [Test]
        public void TryLowestY_FindsLowestBlock()
        {
            var data = new CubeGridData();
            Assert.IsFalse(CubeGridRules.TryLowestY(data, out _));
            data.blocks.Add(new Vector3Int(0, 2, 0));
            data.blocks.Add(new Vector3Int(5, -3, 1));
            Assert.IsTrue(CubeGridRules.TryLowestY(data, out int lowest));
            Assert.AreEqual(-3, lowest);
            data.stairs.Add(new CubeStair(new Vector3Int(1, -4, 0), 0));
            Assert.IsTrue(CubeGridRules.TryLowestY(data, out lowest));
            Assert.AreEqual(-4, lowest);
        }

        [Test]
        public void Raycast_Down_HitsTopFace()
        {
            var data = new CubeGridData();
            data.blocks.Add(new Vector3Int(2, -1, 3));
            bool hit = CubeGridRules.Raycast(new Vector3(2.2f, 5f, 2.9f), Vector3.down, 100f, c => CubeGridRules.HasBlock(data, c), out var cell, out var normal);
            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3Int(2, -1, 3), cell);
            Assert.AreEqual(Vector3Int.up, normal);
        }

        [Test]
        public void Raycast_Sideways_HitsSideFaceAndStopsAtFirstBlock()
        {
            var data = new CubeGridData();
            data.blocks.Add(new Vector3Int(4, 0, 0));
            data.blocks.Add(new Vector3Int(6, 0, 0));
            bool hit = CubeGridRules.Raycast(new Vector3(0f, 0.5f, 0f), Vector3.right, 100f, c => CubeGridRules.HasBlock(data, c), out var cell, out var normal);
            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3Int(4, 0, 0), cell);
            Assert.AreEqual(Vector3Int.left, normal);
        }

        [Test]
        public void Raycast_Diagonal_HitsAndMissesByDistance()
        {
            var data = new CubeGridData();
            data.blocks.Add(new Vector3Int(3, -1, 3));
            var origin = new Vector3(0.1f, 2.5f, 0.2f); // 칸 모서리를 정확히 지나지 않게 조금 비킨다.
            var direction = new Vector3(3f, -3f, 3f);
            Assert.IsTrue(CubeGridRules.Raycast(origin, direction, 100f, c => CubeGridRules.HasBlock(data, c), out var cell, out _));
            Assert.AreEqual(new Vector3Int(3, -1, 3), cell);
            Assert.IsFalse(CubeGridRules.Raycast(origin, direction, 2f, c => CubeGridRules.HasBlock(data, c), out _, out _));
            Assert.IsFalse(CubeGridRules.Raycast(origin, Vector3.up, 100f, c => CubeGridRules.HasBlock(data, c), out _, out _));
        }

        [Test]
        public void Pillar_SharesOneThingPerCell()
        {
            var data = new CubeGridData();
            var cell = new Vector3Int(0, 0, 0);
            Assert.IsTrue(CubeGridRules.AddPillar(data, cell, true, true));
            Assert.IsFalse(CubeGridRules.AddBlock(data, cell));
            Assert.IsFalse(CubeGridRules.AddPillar(data, cell, false, false));
            Assert.IsTrue(CubeGridRules.IsOccupied(data, cell));
            Assert.IsTrue(CubeGridRules.TryLowestY(data, out int lowest));
            Assert.AreEqual(0, lowest);
            Assert.IsTrue(CubeGridRules.Remove(data, cell));
            Assert.IsFalse(CubeGridRules.IsOccupied(data, cell));
        }

        [Test]
        public void PillarShapes_SingleCellFollowsSetting()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, new Vector3Int(0, 0, 0), true, true);
            CubeGridRules.AddPillar(data, new Vector3Int(2, 0, 0), false, true);
            CubeGridRules.AddPillar(data, new Vector3Int(4, 0, 0), true, false);
            CubeGridRules.AddPillar(data, new Vector3Int(6, 0, 0), false, false);
            var shapes = CubeGridRules.PillarShapes(data);
            Assert.AreEqual(PillarShape.Full, shapes[new Vector3Int(0, 0, 0)]);
            Assert.AreEqual(PillarShape.Base, shapes[new Vector3Int(2, 0, 0)]);
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(4, 0, 0)]);
            Assert.AreEqual(PillarShape.Shaft, shapes[new Vector3Int(6, 0, 0)]);
        }

        [Test]
        public void PillarShapes_GroupPutsBaseBottomAndTopTop()
        {
            var data = new CubeGridData();
            for (int y = 0; y < 4; y++) CubeGridRules.AddPillar(data, new Vector3Int(0, y, 0), true, true);
            CubeGridRules.AddPillar(data, new Vector3Int(1, 0, 0), true, true); // 옆 칸은 다른 그룹
            var shapes = CubeGridRules.PillarShapes(data);
            Assert.AreEqual(PillarShape.Base, shapes[new Vector3Int(0, 0, 0)]);
            Assert.AreEqual(PillarShape.Shaft, shapes[new Vector3Int(0, 1, 0)]);
            Assert.AreEqual(PillarShape.Shaft, shapes[new Vector3Int(0, 2, 0)]);
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(0, 3, 0)]);
            Assert.AreEqual(PillarShape.Full, shapes[new Vector3Int(1, 0, 0)]);
        }

        [Test]
        public void PillarShapes_DifferentSettingsAreDifferentGroups()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, new Vector3Int(0, 0, 0), true, true);
            CubeGridRules.AddPillar(data, new Vector3Int(0, 1, 0), true, true);
            CubeGridRules.AddPillar(data, new Vector3Int(0, 2, 0), true, false);
            CubeGridRules.AddPillar(data, new Vector3Int(0, 3, 0), true, false);
            var shapes = CubeGridRules.PillarShapes(data);
            Assert.AreEqual(PillarShape.Base, shapes[new Vector3Int(0, 0, 0)]);
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(0, 1, 0)]);   // 아래 그룹의 맨 위
            Assert.AreEqual(PillarShape.Shaft, shapes[new Vector3Int(0, 2, 0)]); // 위 그룹은 받침 없음
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(0, 3, 0)]);
        }

        [Test]
        public void PillarShapes_RemovingMiddleSplitsGroup()
        {
            var data = new CubeGridData();
            for (int y = 0; y < 5; y++) CubeGridRules.AddPillar(data, new Vector3Int(0, y, 0), true, true);
            CubeGridRules.Remove(data, new Vector3Int(0, 2, 0));
            var shapes = CubeGridRules.PillarShapes(data);
            Assert.AreEqual(PillarShape.Base, shapes[new Vector3Int(0, 0, 0)]);
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(0, 1, 0)]);
            Assert.AreEqual(PillarShape.Base, shapes[new Vector3Int(0, 3, 0)]);
            Assert.AreEqual(PillarShape.Top, shapes[new Vector3Int(0, 4, 0)]);
            Assert.IsFalse(shapes.ContainsKey(new Vector3Int(0, 2, 0)));
        }

        [Test]
        public void Coating_OnlyOnBlocksAndRemovedWithBlock()
        {
            var data = new CubeGridData();
            var cell = new Vector3Int(2, 0, -1);
            Assert.IsFalse(CubeGridRules.SetCoating(data, cell, 3, 4, CoatingKind.Oil)); // 블록 없음
            CubeGridRules.AddBlock(data, cell);
            Assert.IsTrue(CubeGridRules.SetCoating(data, cell, 3, 4, CoatingKind.Oil));
            Assert.IsFalse(CubeGridRules.SetCoating(data, cell, 3, 4, CoatingKind.Oil)); // 그대로
            Assert.IsTrue(CubeGridRules.SetCoating(data, cell, 5, 4, CoatingKind.Ice));
            Assert.AreEqual(CoatingKind.Oil, CubeGridRules.GetCoating(data, cell, 3, 4));
            Assert.AreEqual(CoatingKind.Ice, CubeGridRules.GetCoating(data, cell, 5, 4));
            Assert.AreEqual(CoatingKind.None, CubeGridRules.GetCoating(data, cell, 0, 0));
            Assert.AreEqual(1, data.coatings.Count);
            CubeGridRules.Remove(data, cell);
            Assert.AreEqual(0, data.coatings.Count);
        }

        [Test]
        public void Coating_ClearingLastPixelDropsEntry()
        {
            var data = new CubeGridData();
            var cell = new Vector3Int(0, 0, 0);
            CubeGridRules.AddBlock(data, cell);
            CubeGridRules.SetCoating(data, cell, 0, 0, CoatingKind.Ice);
            CubeGridRules.SetCoating(data, cell, 15, 15, CoatingKind.Ice);
            Assert.IsTrue(CubeGridRules.SetCoating(data, cell, 0, 0, CoatingKind.None));
            Assert.AreEqual(1, data.coatings.Count);
            Assert.IsTrue(CubeGridRules.SetCoating(data, cell, 15, 15, CoatingKind.None));
            Assert.AreEqual(0, data.coatings.Count);
            Assert.IsFalse(CubeGridRules.SetCoating(data, cell, 15, 15, CoatingKind.None));
        }

        [Test]
        public void CoatingPixelAt_MapsPointOnTopFace()
        {
            CubeGridRules.CoatingPixelAt(new Vector3(-0.5f, 1f, -0.5f), out var cell, out int x, out int z);
            Assert.AreEqual(new Vector3Int(0, 0, 0), cell);
            Assert.AreEqual((0, 0), (x, z));
            CubeGridRules.CoatingPixelAt(new Vector3(-0.51f, 1f, 0.49f), out cell, out x, out z);
            Assert.AreEqual(new Vector3Int(-1, 0, 0), cell);
            Assert.AreEqual((15, 15), (x, z));
            CubeGridRules.CoatingPixelAt(new Vector3(3.03f, 0f, -2.2f), out cell, out x, out z);
            Assert.AreEqual(new Vector3Int(3, -1, -2), cell);
            Assert.AreEqual((8, 4), (x, z));
        }

        [Test]
        public void CoatingBrush_IsRoundAndCrossesCells()
        {
            var point = new Vector3(0.03f, 1f, 0.03f); // 칸 (0, 0, 0) 픽셀 (8, 8) 안
            Assert.AreEqual(1, CubeGridRules.CoatingBrush(point, 1f, 1).Count);
            Assert.AreEqual(4, CubeGridRules.CoatingBrush(point, 1f, 2).Count);
            Assert.AreEqual(9, CubeGridRules.CoatingBrush(point, 1f, 3).Count);
            Assert.AreEqual(12, CubeGridRules.CoatingBrush(point, 1f, 4).Count); // 4 × 4에서 모서리 넷을 뺀 원
            var big = CubeGridRules.CoatingBrush(point, 1f, 16);
            Assert.IsTrue(big.Exists(p => p.cell == new Vector3Int(0, 0, 0) && p.x == 0 && p.z == 8));
            var edge = CubeGridRules.CoatingBrush(new Vector3(0.5f, 1f, 0f), 1f, 2); // 칸 경계에 걸침
            Assert.IsTrue(edge.Exists(p => p.cell.x == 0) && edge.Exists(p => p.cell.x == 1));
            Assert.IsTrue(edge.TrueForAll(p => p.cell.y == 0));
        }
    }
}
