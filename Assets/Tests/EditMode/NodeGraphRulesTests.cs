using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class NodeGraphRulesTests
    {
        static NodeData Node(NodeGraphData data, int x, int z)
        {
            var node = new NodeData { id = NodeGraphRules.IssueId(data), position = new Vector3Int(x, 0, z) };
            data.nodes.Add(node);
            return node;
        }

        static WallData Wall(NodeGraphData data, NodeData start, NodeData end)
        {
            var wall = new WallData { id = NodeGraphRules.IssueId(data), startNodeId = start.id, endNodeId = end.id };
            data.walls.Add(wall);
            return wall;
        }

        [Test]
        public void IssueId_StartsAtOneAndIncreases()
        {
            var data = new NodeGraphData();
            Assert.AreEqual(1, NodeGraphRules.IssueId(data));
            Assert.AreEqual(2, NodeGraphRules.IssueId(data));
        }

        [Test]
        public void RefreshNextId_ContinuesAfterLargestIdIncludingDoors()
        {
            var data = new NodeGraphData { nextId = 1 };
            data.nodes.Add(new NodeData { id = 4 });
            data.walls.Add(new WallData { id = 2, hasDoor = true, door = new DoorData { id = 9 } });
            NodeGraphRules.RefreshNextId(data);
            Assert.AreEqual(10, data.nextId);
        }

        [Test]
        public void Merge_KeepsFirstNodeAndRewiresWalls()
        {
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 3, 0);
            var c = Node(data, 0, 2);
            var moved = Node(data, 3, 0);
            var ab = Wall(data, a, b);
            var movedC = Wall(data, moved, c);

            Assert.AreEqual(1, NodeGraphRules.MergeNodesAtSamePosition(data));

            Assert.AreEqual(3, data.nodes.Count);
            Assert.IsFalse(data.nodes.Contains(moved));
            Assert.AreEqual(b.id, ab.endNodeId);
            Assert.AreEqual(b.id, movedC.startNodeId);
        }

        [Test]
        public void Merge_RemovesWallBetweenMergedNodes()
        {
            var data = new NodeGraphData();
            var a = Node(data, 1, 1);
            var b = Node(data, 1, 1);
            Wall(data, a, b);

            NodeGraphRules.MergeNodesAtSamePosition(data);

            Assert.AreEqual(1, data.nodes.Count);
            Assert.AreEqual(0, data.walls.Count);
        }

        [Test]
        public void Merge_KeepsOnlyFirstWallWhenTwoWallsJoinSameNodes()
        {
            // a–b와 c–b가 있을 때 c를 a 자리로 옮기면 두 벽이 같은 두 노드를 잇게 된다.
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 2, 0);
            var c = Node(data, 0, 0);
            var ab = Wall(data, a, b);
            Wall(data, c, b);

            NodeGraphRules.MergeNodesAtSamePosition(data);

            Assert.AreEqual(1, data.walls.Count);
            Assert.AreSame(ab, data.walls[0]);
        }

        [Test]
        public void AddNode_ReturnsExistingNodeAtSamePosition()
        {
            var data = new NodeGraphData();
            var first = NodeGraphRules.AddNode(data, new Vector3Int(1, 0, 2));
            var again = NodeGraphRules.AddNode(data, new Vector3Int(1, 0, 2));
            var other = NodeGraphRules.AddNode(data, new Vector3Int(2, 0, 2));

            Assert.AreSame(first, again);
            Assert.AreNotEqual(first.id, other.id);
            Assert.AreEqual(2, data.nodes.Count);
        }

        [Test]
        public void RemoveNode_AlsoRemovesAttachedWalls()
        {
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 2, 0);
            var c = Node(data, 2, 2);
            Wall(data, a, b);
            var bc = Wall(data, b, c);

            NodeGraphRules.RemoveNode(data, a.id);

            Assert.AreEqual(2, data.nodes.Count);
            Assert.AreEqual(1, data.walls.Count);
            Assert.AreSame(bc, data.walls[0]);
        }

        [Test]
        public void Connect_AddsWallWithNewId()
        {
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 3, 0);

            var wall = NodeGraphRules.Connect(data, a.id, b.id);

            Assert.IsNotNull(wall);
            Assert.AreEqual(3, wall.id);
            Assert.AreEqual(WallKind.Normal, wall.kind);
            Assert.AreSame(wall, NodeGraphRules.FindWall(data, wall.id));
        }

        [Test]
        public void Connect_RejectsSameNodeAndExistingWall()
        {
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 3, 0);
            NodeGraphRules.Connect(data, a.id, b.id);

            Assert.IsNull(NodeGraphRules.Connect(data, a.id, a.id));
            Assert.IsNull(NodeGraphRules.Connect(data, b.id, a.id));
            Assert.AreEqual(1, data.walls.Count);
        }

        [Test]
        public void RemoveWall_KeepsNodes()
        {
            var data = new NodeGraphData();
            var a = Node(data, 0, 0);
            var b = Node(data, 3, 0);
            var wall = Wall(data, a, b);

            NodeGraphRules.RemoveWall(data, wall.id);

            Assert.AreEqual(0, data.walls.Count);
            Assert.AreEqual(2, data.nodes.Count);
        }

        [Test]
        public void ModuleCount_FillsWithQuarterModulesAndLeavesShortRemainder()
        {
            Assert.AreEqual(8, NodeGraphRules.ModuleCount(2f));
            Assert.AreEqual(0f, NodeGraphRules.Remainder(2f), 1e-5f);

            float diagonal = NodeGraphRules.SegmentLength(new Vector3Int(1, 0, 1), new Vector3Int(3, 0, 5));
            Assert.AreEqual(Mathf.Sqrt(20f), diagonal, 1e-5f);
            Assert.AreEqual(17, NodeGraphRules.ModuleCount(diagonal));
            Assert.Less(NodeGraphRules.Remainder(diagonal), NodeGraphRules.ModuleLength);
        }

        [Test]
        public void CanPlaceDoor_NeedsHalfDoorWidthOfWallOnBothSides()
        {
            // 문 폭 1(모듈 4개)이면 양옆에 모듈 2개씩 필요하다. 2유닛 구간(모듈 8개)에는 가운데만 된다.
            Assert.IsTrue(NodeGraphRules.CanPlaceDoor(2f, 1f, 2));
            Assert.IsFalse(NodeGraphRules.CanPlaceDoor(2f, 1f, 1));
            Assert.IsFalse(NodeGraphRules.CanPlaceDoor(2f, 1f, 3));
            Assert.IsFalse(NodeGraphRules.CanPlaceDoor(1.75f, 1f, 2));
            Assert.IsTrue(NodeGraphRules.CanPlaceDoor(4f, 1f, 10));
            Assert.IsFalse(NodeGraphRules.CanPlaceDoor(4f, 1f, 11));
        }

        [Test]
        public void DoorOffsetRange_GivesPlaceableOffsets()
        {
            Assert.IsTrue(NodeGraphRules.DoorOffsetRange(4f, 1f, out int min, out int max));
            Assert.AreEqual(2, min);
            Assert.AreEqual(10, max);
            Assert.IsFalse(NodeGraphRules.DoorOffsetRange(1.75f, 1f, out _, out _));
        }

        [Test]
        public void FirstModuleStart_SplitsRemainderBetweenBothEnds()
        {
            Assert.AreEqual(0f, NodeGraphRules.FirstModuleStart(2f), 1e-5f);
            // 2.1유닛: 모듈 8개(2유닛), 남는 0.1을 양 끝에 0.05씩
            Assert.AreEqual(0.05f, NodeGraphRules.FirstModuleStart(2.1f), 1e-5f);
        }

        [Test]
        public void SolidSpans_WithoutDoor_CoversNodeToNode()
        {
            var spans = NodeGraphRules.SolidSpans(3f, false, 1f, 0);
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(0f, spans[0].from, 1e-5f);
            Assert.AreEqual(3f, spans[0].to, 1e-5f);
        }

        [Test]
        public void SolidSpans_WithDoor_LeavesDoorGap()
        {
            // 4유닛 구간, 문 폭 1, 위치 6모듈: 문은 1.5에서 2.5까지
            var spans = NodeGraphRules.SolidSpans(4f, true, 1f, 6);
            Assert.AreEqual(2, spans.Count);
            Assert.AreEqual(0f, spans[0].from, 1e-5f);
            Assert.AreEqual(1.5f, spans[0].to, 1e-5f);
            Assert.AreEqual(2.5f, spans[1].from, 1e-5f);
            Assert.AreEqual(4f, spans[1].to, 1e-5f);
        }

        [Test]
        public void PlateCells_FillNodeBoundsWithFloorAndCeiling()
        {
            var data = new NodeGraphData();
            Node(data, 0, 0);
            Node(data, 2, 1);

            var cells = NodeGraphRules.PlateCells(data, 3);

            // 2×1칸 × (바닥 + 천장)
            Assert.AreEqual(4, cells.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), cells[0]);
            Assert.AreEqual(new Vector3Int(1, 0, 0), cells[1]);
            Assert.AreEqual(new Vector3Int(0, 3, 0), cells[2]);
            Assert.AreEqual(new Vector3Int(1, 3, 0), cells[3]);
        }
    }
}
