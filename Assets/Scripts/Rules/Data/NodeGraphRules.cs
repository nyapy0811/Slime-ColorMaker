using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 노드 데이터의 순수 계산: id 발급, 노드·벽 편집, 같은 자리 노드 합치기, 구간의 모듈 수와 콜라이더 구간,
    /// 문을 끼울 수 있는지, 바닥·천장 판. 문 폭과 벽의 키는 아직 임시값이라 인자로 받는다.
    /// </summary>
    public static class NodeGraphRules
    {
        /// <summary>벽 모듈 하나의 길이(유닛). 끼운 문의 위치 단위이기도 하다.</summary>
        public const float ModuleLength = 0.25f;

        const float Epsilon = 1e-4f;

        /// <summary>다음 id를 주고 하나 늘린다.</summary>
        public static int IssueId(NodeGraphData data) => data.nextId++;

        public static NodeData FindNode(NodeGraphData data, int id) => data.nodes.Find(node => node.id == id);

        public static NodeData FindNodeAt(NodeGraphData data, Vector3Int position) => data.nodes.Find(node => node.position == position);

        public static WallData FindWall(NodeGraphData data, int id) => data.walls.Find(wall => wall.id == id);

        /// <summary>노드를 더한다. 같은 자리에 노드가 있으면 새로 만들지 않고 그 노드를 돌려준다.</summary>
        public static NodeData AddNode(NodeGraphData data, Vector3Int position)
        {
            var existing = FindNodeAt(data, position);
            if (existing != null) return existing;
            var node = new NodeData { id = IssueId(data), position = position };
            data.nodes.Add(node);
            return node;
        }

        /// <summary>노드와 거기 붙은 벽을 지운다.</summary>
        public static void RemoveNode(NodeGraphData data, int nodeId)
        {
            data.nodes.RemoveAll(node => node.id == nodeId);
            data.walls.RemoveAll(wall => wall.startNodeId == nodeId || wall.endNodeId == nodeId);
        }

        /// <summary>
        /// 두 노드를 일반 벽으로 잇는다. 같은 노드, 이미 벽으로 이어진 두 노드는 잇지 않고 null을 돌려준다.
        /// </summary>
        public static WallData Connect(NodeGraphData data, int startNodeId, int endNodeId)
        {
            var start = FindNode(data, startNodeId);
            var end = FindNode(data, endNodeId);
            if (start == null || end == null || start == end) return null;
            if (data.walls.Exists(wall => Pair(wall) == Pair(startNodeId, endNodeId))) return null;
            var added = new WallData { id = IssueId(data), startNodeId = startNodeId, endNodeId = endNodeId };
            data.walls.Add(added);
            return added;
        }

        public static void RemoveWall(NodeGraphData data, int wallId) => data.walls.RemoveAll(wall => wall.id == wallId);

        /// <summary>불러온 뒤: 다음 id를 가장 큰 번호 + 1로 맞춘다(노드·벽·끼운 문 전체).</summary>
        public static void RefreshNextId(NodeGraphData data)
        {
            int max = 0;
            foreach (var node in data.nodes) max = Math.Max(max, node.id);
            foreach (var wall in data.walls)
            {
                max = Math.Max(max, wall.id);
                if (wall.hasDoor) max = Math.Max(max, wall.door.id);
            }
            data.nextId = max + 1;
        }

        /// <summary>
        /// 같은 자리에 있는 노드를 하나로 합친다. 목록에서 먼저 나온 노드가 남고, 없어지는 노드를 가리키던 벽은
        /// 남는 노드를 가리키게 고친다. 그래서 양 끝이 같아진 벽(합친 두 노드 사이의 벽)은 지우고, 같은 두 노드를 잇게 된
        /// 벽이 여럿이면 먼저 나온 것만 남긴다. 합쳐 없앤 노드 수를 돌려준다.
        /// </summary>
        public static int MergeNodesAtSamePosition(NodeGraphData data)
        {
            var keptAt = new Dictionary<Vector3Int, int>();
            var replaced = new Dictionary<int, int>();
            foreach (var node in data.nodes)
            {
                if (keptAt.TryGetValue(node.position, out int keptId)) replaced[node.id] = keptId;
                else keptAt[node.position] = node.id;
            }
            if (replaced.Count == 0) return 0;

            data.nodes.RemoveAll(node => replaced.ContainsKey(node.id));
            foreach (var wall in data.walls)
            {
                if (replaced.TryGetValue(wall.startNodeId, out int start)) wall.startNodeId = start;
                if (replaced.TryGetValue(wall.endNodeId, out int end)) wall.endNodeId = end;
            }
            data.walls.RemoveAll(wall => wall.startNodeId == wall.endNodeId);
            var joined = new HashSet<(int, int)>();
            data.walls.RemoveAll(wall => !joined.Add(Pair(wall))); // 목록 순서대로 보므로 먼저 나온 벽이 남는다.
            return replaced.Count;
        }

        static (int, int) Pair(WallData wall) => Pair(wall.startNodeId, wall.endNodeId);

        static (int, int) Pair(int a, int b) => a < b ? (a, b) : (b, a);

        /// <summary>두 노드 사이 구간의 길이(유닛). 대각선이면 직선 거리다.</summary>
        public static float SegmentLength(Vector3Int start, Vector3Int end) => Vector3Int.Distance(start, end);

        /// <summary>구간을 채우는 벽 모듈 수. 남는 0.25 미만의 길이는 노드 기둥이 가린다.</summary>
        public static int ModuleCount(float segmentLength) => (int)Math.Floor(segmentLength / ModuleLength + Epsilon);

        /// <summary>모듈로 채우고 남는 길이(0.25 미만).</summary>
        public static float Remainder(float segmentLength) => Math.Max(0f, segmentLength - ModuleCount(segmentLength) * ModuleLength);

        /// <summary>구간 시작 노드에서 첫 벽 모듈까지의 거리. 남는 길이를 양 끝에 반씩 나눠 노드 기둥이 가리게 한다.</summary>
        public static float FirstModuleStart(float segmentLength) => Remainder(segmentLength) / 2f;

        /// <summary>문 폭이 차지하는 모듈 수.</summary>
        public static int DoorModules(float doorWidth) => (int)Math.Round(doorWidth / ModuleLength);

        /// <summary>구간 시작 노드에서 문의 시작 쪽 가장자리까지의 거리.</summary>
        public static float DoorStart(float segmentLength, int offsetModules) => FirstModuleStart(segmentLength) + offsetModules * ModuleLength;

        /// <summary>
        /// 문을 끼울 수 있는 위치(첫 모듈부터 센 모듈 수)의 범위. 문 양옆에 문 폭 절반 이상의 벽이 남아야 한다
        /// (문짝 반쪽이 들어갈 자리). 구간이 짧아 끼울 수 없으면 false.
        /// </summary>
        public static bool DoorOffsetRange(float segmentLength, float doorWidth, out int min, out int max)
        {
            int door = DoorModules(doorWidth);
            min = (door + 1) / 2;
            max = ModuleCount(segmentLength) - min - door;
            return min <= max;
        }

        /// <summary>
        /// 문을 이 위치에 끼울 수 있는가. <paramref name="offsetModules"/>는 첫 모듈부터 센 문의 시작 쪽 가장자리다.
        /// </summary>
        public static bool CanPlaceDoor(float segmentLength, float doorWidth, int offsetModules) =>
            DoorOffsetRange(segmentLength, doorWidth, out int min, out int max) && offsetModules >= min && offsetModules <= max;

        /// <summary>
        /// 벽 콜라이더를 둘 구간(시작 노드에서 잰 거리 범위). 문이 없으면 노드에서 노드까지 하나, 있으면 문 양옆 둘.
        /// 문 위 벽의 콜라이더는 문 자리 그대로라 따로 계산하지 않는다.
        /// </summary>
        public static List<(float from, float to)> SolidSpans(float segmentLength, bool hasDoor, float doorWidth, int offsetModules)
        {
            if (!hasDoor) return new() { (0f, segmentLength) };
            float doorStart = DoorStart(segmentLength, offsetModules);
            float doorEnd = doorStart + DoorModules(doorWidth) * ModuleLength;
            return new() { (0f, doorStart), (doorEnd, segmentLength) };
        }

        /// <summary>
        /// 바닥·천장 판을 깔 1×1 칸. 맵은 한 층이라 모든 노드를 감싸는 사각형 안의 칸에 바닥 판(높이 0)과
        /// 천장 판(높이 = 벽의 키)을 둔다. 칸은 (x, 판 높이, z)이고 x, z는 칸의 작은 쪽 모서리다. 높이, z, x 순서로 돌려준다.
        /// </summary>
        public static List<Vector3Int> PlateCells(NodeGraphData data, int wallHeight)
        {
            int xMin = int.MaxValue, xMax = int.MinValue, zMin = int.MaxValue, zMax = int.MinValue;
            foreach (var node in data.nodes)
            {
                var p = node.position;
                xMin = Math.Min(xMin, p.x);
                xMax = Math.Max(xMax, p.x);
                zMin = Math.Min(zMin, p.z);
                zMax = Math.Max(zMax, p.z);
            }

            var cells = new List<Vector3Int>();
            foreach (int y in new[] { 0, wallHeight })
                for (int z = zMin; z < zMax; z++)
                for (int x = xMin; x < xMax; x++)
                    cells.Add(new Vector3Int(x, y, z));
            return cells;
        }
    }
}
