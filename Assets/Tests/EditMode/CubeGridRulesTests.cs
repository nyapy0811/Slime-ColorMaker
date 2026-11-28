using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class CubeGridRulesTests
    {
        // 바닥(y = -1) 5×5 위에 벽·천장(y = 0~2)으로 닫힌 방: 안쪽 3×2×3 = 18칸.
        static CubeGridData ClosedRoom()
        {
            var data = new CubeGridData();
            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            for (int y = -1; y <= 2; y++)
            {
                bool shell = y == -1 || y == 2 || Mathf.Abs(x) == 2 || Mathf.Abs(z) == 2;
                if (shell) CubeGridRules.AddBlock(data, new Vector3Int(x, y, z));
            }
            return data;
        }

        [Test]
        public void CoveredRegions_FindRoomInside()
        {
            var regions = CubeGridRules.CoveredRegions(ClosedRoom(), 4);
            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(new Vector3Int(-1, 0, -1), regions[0].min);
            Assert.AreEqual(new Vector3Int(1, 1, 1), regions[0].max);
            Assert.AreEqual(18, regions[0].cellCount);
            Assert.AreEqual(new Vector3Int(0, 0, 0), regions[0].center);
        }

        [Test]
        public void CoveredRegions_RoomWithDoorStillCovered()
        {
            var data = ClosedRoom();
            CubeGridRules.Remove(data, new Vector3Int(0, 0, -2));
            CubeGridRules.Remove(data, new Vector3Int(0, 1, -2));
            var regions = CubeGridRules.CoveredRegions(data, 4);
            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(20, regions[0].cellCount); // 문 자리 두 칸도 천장 아래
        }

        [Test]
        public void CoveredRegions_SkipOpenGroundBelowMapAndSmallOnes()
        {
            var data = new CubeGridData();
            for (int x = 0; x < 3; x++) CubeGridRules.AddBlock(data, new Vector3Int(x, -1, 0));
            CubeGridRules.AddBlock(data, new Vector3Int(0, 3, 0)); // 떠 있는 블록 하나: 그 아래 세 칸
            Assert.AreEqual(0, CubeGridRules.CoveredRegions(data, 4).Count);
            Assert.AreEqual(1, CubeGridRules.CoveredRegions(data, 3).Count);
        }

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
            Assert.IsTrue(CubeGridRules.AddPillar(data, cell, false, CellAxis.NorthSouth));
            Assert.IsFalse(CubeGridRules.AddBlock(data, cell));
            Assert.IsFalse(CubeGridRules.AddPillar(data, cell, true, CellAxis.EastWest));
            Assert.IsTrue(CubeGridRules.IsOccupied(data, cell));
            Assert.IsTrue(CubeGridRules.TryLowestY(data, out int lowest));
            Assert.AreEqual(0, lowest);
            Assert.IsTrue(CubeGridRules.Remove(data, cell));
            Assert.IsFalse(CubeGridRules.IsOccupied(data, cell));
        }

        static Vector3Int C(int x, int y, int z) => new(x, y, z);

        [Test]
        public void PillarLayouts_UprightKeepsChosenAxis()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, C(0, 0, 0), false, CellAxis.NorthSouth);
            CubeGridRules.AddPillar(data, C(0, 1, 0), false, CellAxis.EastWest);
            CubeGridRules.AddPillar(data, C(1, 0, 0), false, CellAxis.NorthSouth); // 세운 기둥끼리 옆: 커넥터 없음
            var layouts = CubeGridRules.PillarLayouts(data);
            Assert.AreEqual(PillarPose.UprightNorthSouth, layouts[C(0, 0, 0)].pose);
            Assert.AreEqual(PillarPose.UprightEastWest, layouts[C(0, 1, 0)].pose);
            Assert.AreEqual(new PillarLayout { pose = PillarPose.UprightNorthSouth }, layouts[C(1, 0, 0)]);
        }

        [Test]
        public void PillarLayouts_LyingFollowsNeighborsThenChosenAxis()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, C(0, 0, 0), true, CellAxis.EastWest);   // 혼자: 고른 방향
            CubeGridRules.AddPillar(data, C(10, 0, 0), true, CellAxis.EastWest);  // 남쪽에 기둥: 남북
            CubeGridRules.AddPillar(data, C(10, 0, -1), false, CellAxis.NorthSouth);
            CubeGridRules.AddPillar(data, C(10, 1, 1), false, CellAxis.NorthSouth); // 다른 높이는 보지 않음
            var layouts = CubeGridRules.PillarLayouts(data);
            Assert.AreEqual(PillarPose.LyingEastWest, layouts[C(0, 0, 0)].pose);
            Assert.AreEqual(PillarPose.LyingNorthSouth, layouts[C(10, 0, 0)].pose);
        }

        [Test]
        public void PillarLayouts_ConnectorsOnUprightFaces()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, C(0, 0, 0), false, CellAxis.NorthSouth); // 홈이 남·북, 평평한 면이 동·서
            CubeGridRules.AddPillar(data, C(1, 0, 0), true, CellAxis.NorthSouth);  // 동쪽 들보(옆 칸으로 동서)
            CubeGridRules.AddPillar(data, C(2, 0, 0), false, CellAxis.EastWest);   // 홈이 동·서
            CubeGridRules.AddPillar(data, C(0, 0, 1), true, CellAxis.NorthSouth);  // 북쪽 들보(남북)
            var layouts = CubeGridRules.PillarLayouts(data);
            var post = layouts[C(0, 0, 0)];
            Assert.AreEqual(PillarConnector.Flat, post.east);
            Assert.AreEqual(PillarConnector.Groove, post.north);
            Assert.AreEqual(PillarConnector.None, post.south);
            Assert.AreEqual(PillarConnector.None, post.west);
            Assert.AreEqual(PillarConnector.Groove, layouts[C(2, 0, 0)].west);
            Assert.AreEqual(PillarConnector.None, layouts[C(1, 0, 0)].east); // 들보 칸에는 없음
        }

        [Test]
        public void PillarLayouts_BeamToBeamOnlyAtSide()
        {
            var data = new CubeGridData();
            for (int x = 0; x < 3; x++) CubeGridRules.AddPillar(data, C(x, 0, 0), true, CellAxis.NorthSouth); // 동서 일직선
            CubeGridRules.AddPillar(data, C(1, 0, 1), true, CellAxis.EastWest); // T자 줄기(남북)
            var layouts = CubeGridRules.PillarLayouts(data);
            Assert.AreEqual(PillarPose.LyingEastWest, layouts[C(1, 0, 0)].pose);
            Assert.AreEqual(PillarPose.LyingNorthSouth, layouts[C(1, 0, 1)].pose);
            Assert.AreEqual(PillarConnector.Beam, layouts[C(1, 0, 0)].north);
            Assert.AreEqual(PillarConnector.None, layouts[C(1, 0, 0)].east); // 일직선
            Assert.AreEqual(PillarConnector.None, layouts[C(0, 0, 0)].east);
            Assert.AreEqual(PillarConnector.None, layouts[C(1, 0, 1)].south); // 줄기 끝 칸에는 없음
        }

        [Test]
        public void PillarLayouts_CornerUsesChosenAxis()
        {
            var data = new CubeGridData();
            CubeGridRules.AddPillar(data, C(0, 0, 0), true, CellAxis.EastWest);   // L자 모서리: 고른 방향(동서)
            CubeGridRules.AddPillar(data, C(1, 0, 0), true, CellAxis.EastWest);
            CubeGridRules.AddPillar(data, C(0, 0, 1), true, CellAxis.EastWest);   // 북쪽 칸은 모서리 하나뿐이라 남북
            var layouts = CubeGridRules.PillarLayouts(data);
            Assert.AreEqual(PillarPose.LyingEastWest, layouts[C(0, 0, 0)].pose);
            Assert.AreEqual(PillarPose.LyingNorthSouth, layouts[C(0, 0, 1)].pose);
            Assert.AreEqual(PillarConnector.Beam, layouts[C(0, 0, 0)].north);
            Assert.AreEqual(PillarConnector.None, layouts[C(0, 0, 0)].east);
        }

        [Test]
        public void FullFace_NeedsWholeBoundaryLayer()
        {
            Assert.IsTrue(CubeGridRules.FullFace(Vector3Int.up, p => true));                  // 위 칸이 블록
            Assert.IsFalse(CubeGridRules.FullFace(Vector3Int.up, p => p.y >= 4));             // 아랫면이 높이 4(눕힌 기둥): 칸 경계 아님
            Assert.IsFalse(CubeGridRules.FullFace(Vector3Int.up, p => p.x != 0 || p.z != 0)); // 빈 픽셀 하나
            Assert.IsTrue(CubeGridRules.FullFace(Vector3Int.down, p => p.y == 15));           // 아래 칸 윗면 한 겹만 차도 됨
            Assert.IsTrue(CubeGridRules.FullFace(Vector3Int.back, p => p.z == 15));           // 남쪽 칸의 북쪽 끝 한 겹
            Assert.IsFalse(CubeGridRules.FullFace(Vector3Int.back, p => p.z == 0));           // 남쪽 칸의 먼 쪽 면
        }

        [Test]
        public void Supported_AnyRuleInPlacedDirection()
        {
            var rules = new List<PlacementRule> { new(PlacementCheck.FullFace, PlacementDirection.Back), new(PlacementCheck.SameKind, PlacementDirection.Down) };
            var east = CubeGridRules.SideRotation(1); // 동쪽 면 장식: 뒤 = +x
            Assert.IsTrue(CubeGridRules.Supported(rules, east, d => d == Vector3Int.right, d => false));
            Assert.IsFalse(CubeGridRules.Supported(rules, east, d => d == new Vector3Int(0, 0, -1), d => false));
            Assert.IsTrue(CubeGridRules.Supported(rules, east, d => false, d => d == Vector3Int.down));
            Assert.IsTrue(CubeGridRules.Supported(new List<PlacementRule>(), east, d => false, d => false)); // 규칙 없음 = 어디든
            var side = new List<PlacementRule> { new(PlacementCheck.SameKind, PlacementDirection.AnySide) };
            Assert.IsTrue(CubeGridRules.Supported(side, Quaternion.identity, d => false, d => d == Vector3Int.left));
            Assert.IsFalse(CubeGridRules.Supported(side, Quaternion.identity, d => false, d => d == Vector3Int.up));
        }

        [Test]
        public void Directions_WorldStaysObjectTurns()
        {
            var lying = Quaternion.Euler(0f, 0f, 90f); // 눕힌 기둥처럼 Z축 90°
            CollectionAssert.AreEqual(new[] { Vector3Int.down }, CubeGridRules.Directions(PlacementDirection.Down, lying));       // 아랫방향(세계)
            CollectionAssert.AreEqual(new[] { Vector3Int.right }, CubeGridRules.Directions(PlacementDirection.BottomFace, lying)); // 아랫면(기물)이 옆으로
            CollectionAssert.AreEqual(new[] { Vector3Int.left }, CubeGridRules.Directions(PlacementDirection.TopFace, lying));
            Assert.AreEqual(4, CubeGridRules.Directions(PlacementDirection.AnySide, lying).Count);                                  // 옆방향(세계) 넷
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, -1) }, CubeGridRules.Directions(PlacementDirection.Back, lying)); // Z축으로 돌려도 뒷면은 그대로
        }

        [Test]
        public void CrateFaceOnBoundary_ByHalf()
        {
            Assert.IsTrue(CubeGridRules.CrateFaceOnBoundary(C(0, 0, 0), Vector3Int.down));  // 칸 아래쪽 반
            Assert.IsFalse(CubeGridRules.CrateFaceOnBoundary(C(0, 1, 0), Vector3Int.down)); // 칸 위쪽 반(아래는 같은 칸)
            Assert.IsTrue(CubeGridRules.CrateFaceOnBoundary(C(0, -1, 0), Vector3Int.up));
            Assert.IsTrue(CubeGridRules.CrateFaceOnBoundary(C(-1, 0, 0), Vector3Int.right));
            Assert.IsFalse(CubeGridRules.CrateFaceOnBoundary(C(-1, 0, 0), Vector3Int.left));
        }

        [Test]
        public void Light_OnePerCellAndRemovedWithCell()
        {
            var data = new CubeGridData();
            Assert.IsTrue(CubeGridRules.AddLight(data, C(0, 0, 0), CellAxis.NorthSouth, Color.white));
            Assert.IsFalse(CubeGridRules.AddLight(data, C(0, 0, 0), CellAxis.EastWest, Color.red));
            Assert.IsFalse(CubeGridRules.IsOccupied(data, C(0, 0, 0))); // 칸을 차지하지 않음(기물과 같이)
            Assert.IsTrue(CubeGridRules.AddPillar(data, C(0, 0, 0), true, CellAxis.NorthSouth)); // 겹침은 픽셀로 따로 봄
            CubeGridRules.RemoveTerrain(data, C(0, 0, 0));
            Assert.IsTrue(CubeGridRules.HasLight(data, C(0, 0, 0)));   // 지형만 지움
            Assert.IsTrue(CubeGridRules.Remove(data, C(0, 0, 0)));
            Assert.AreEqual(0, data.lights.Count);
        }

        [Test]
        public void RailingEastPosts_HiddenWhenContinuedOrInnerCorner()
        {
            var data = new CubeGridData();
            CubeGridRules.AddSidePiece(data.railings, C(0, 0, 0), 2); // 남쪽 면, 동쪽 끝은 +x
            CubeGridRules.AddSidePiece(data.railings, C(1, 0, 0), 2);
            CubeGridRules.AddSidePiece(data.railings, C(1, 0, 0), 1); // 같은 칸 동쪽 면(안쪽 모서리)
            Assert.IsFalse(CubeGridRules.AddSidePiece(data.railings, C(1, 0, 0), 1)); // 같은 면 둘은 안 됨
            var posts = CubeGridRules.RailingEastPosts(data);
            Assert.IsFalse(posts[new CubeSidePiece(C(0, 0, 0), 2)]); // 옆 칸으로 이어짐
            Assert.IsFalse(posts[new CubeSidePiece(C(1, 0, 0), 2)]); // 안쪽 모서리
            Assert.IsTrue(posts[new CubeSidePiece(C(1, 0, 0), 1)]);  // 동쪽 면의 끝(+z 쪽)은 비었음
        }

        [Test]
        public void Decorations_CratesByHalfCellAndRemovedWithCell()
        {
            var data = new CubeGridData();
            Assert.IsTrue(CubeGridRules.AddCrate(data, C(-1, 2, 1)));
            Assert.IsFalse(CubeGridRules.AddCrate(data, C(-1, 2, 1)));
            Assert.AreEqual(C(-1, 1, 0), CubeGridRules.CrateCell(C(-1, 2, 1)));
            Assert.AreEqual(new Vector3(-0.75f, 1f, 0.25f), CubeGridRules.CrateBottom(C(-1, 2, 1)));
            CubeGridRules.AddSidePiece(data.panels, C(-1, 1, 0), 0);
            Assert.IsTrue(CubeGridRules.HasDecoration(data, C(-1, 1, 0)));
            Assert.IsFalse(CubeGridRules.IsOccupied(data, C(-1, 1, 0)));
            Assert.IsTrue(CubeGridRules.Remove(data, C(-1, 1, 0)));
            Assert.AreEqual(0, data.crates.Count + data.panels.Count);
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
