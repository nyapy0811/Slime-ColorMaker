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
    }
}
