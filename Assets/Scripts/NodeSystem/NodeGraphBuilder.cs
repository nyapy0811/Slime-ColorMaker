using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 벽 빌더: 노드 그래프의 데이터로 벽·끼운 문·레이저 벽·기둥·바닥·천장을 만든다. 유니티 에디터 맵(에디터에서 미리 만들어
    /// 씬과 함께 저장, Undo 기록)과 UGC 맵(불러올 때 런타임에 만듦)이 같은 코드를 쓴다.
    /// 벽은 데이터 id로 지금 오브젝트와 비교한다: 남은 id는 루트를 남기고 모양만 다시 만들고, 없어진 id는 지우고, 새 id는 만든다.
    /// 기둥·바닥·천장은 손으로 넣는 값이 없어 통째로 다시 만든다. static 표시는 프리팹에 해 둔다(문짝·레이저는 움직이므로 뺌).
    /// </summary>
    public static class NodeGraphBuilder
    {
        public static void Build(NodeGraph graph, NodeBuildConfig config)
        {
            var data = graph.Data;
            var positions = new Dictionary<int, Vector3Int>();
            foreach (var node in data.nodes) positions[node.id] = node.position;

            var existing = new Dictionary<int, NodeObjectBase>();
            foreach (Transform child in graph.transform)
                if (child.TryGetComponent(out NodeObjectBase built)) existing[built.DataId] = built;

            var kept = new HashSet<int>();
            foreach (var wall in data.walls)
            {
                // 없는 노드를 가리키는 벽은 맵 검사가 알린다.
                if (!positions.TryGetValue(wall.startNodeId, out var start) || !positions.TryGetValue(wall.endNodeId, out var end)) continue;
                kept.Add(wall.id);
                existing.TryGetValue(wall.id, out var current);
                if (wall.kind == WallKind.Laser)
                    Reuse<LaserSegment>(current, () => CreateLaser(graph, wall.id, config)).Build(start, end, config);
                else
                    Reuse<WallSegment>(current, () => CreateWall(graph, wall.id)).Build(wall, start, end, config);
            }
            foreach (var (id, built) in existing)
                if (!kept.Contains(id)) NodeBuildOps.Destroy(built.gameObject);

            BuildPillarsAndPlates(graph, config);
        }

        // 같은 id의 오브젝트가 같은 종류면 남기고, 종류가 바뀌었으면(일반 벽 ↔ 레이저 벽) 지우고 새로 만든다.
        static T Reuse<T>(NodeObjectBase current, System.Func<T> create) where T : NodeObjectBase
        {
            if (current is T same) return same;
            if (current != null) NodeBuildOps.Destroy(current.gameObject);
            return create();
        }

        static WallSegment CreateWall(NodeGraph graph, int id)
        {
            var root = NodeBuildOps.NewChild($"Wall {id}", graph.transform);
            var wall = root.gameObject.AddComponent<WallSegment>();
            wall.SetDataId(id);
            NodeBuildOps.RegisterCreated(root.gameObject);
            return wall;
        }

        static LaserSegment CreateLaser(NodeGraph graph, int id, NodeBuildConfig config)
        {
            var laser = NodeBuildOps.Spawn(config.laserPrefab, graph.transform).GetComponent<LaserSegment>();
            laser.name = $"Laser {id}";
            laser.SetDataId(id);
            NodeBuildOps.RegisterCreated(laser.gameObject);
            return laser;
        }

        // 기둥은 모든 노드에, 바닥·천장 판은 모든 노드를 감싸는 사각형 안의 1×1 칸에 둔다(판 위쪽 면이 그 높이).
        static void BuildPillarsAndPlates(NodeGraph graph, NodeBuildConfig config)
        {
            var parts = NodeBuildOps.ReplaceChild(graph.transform, NodeObjectBase.GeneratedPartsName);
            int height = config.wallHeight;
            foreach (var node in graph.Data.nodes)
            {
                var pillar = NodeBuildOps.Spawn(config.pillarPrefab, parts).transform;
                pillar.localPosition = node.position + Vector3.up * (height / 2f);
                pillar.localScale = new Vector3(config.pillarSize, height, config.pillarSize);
            }
            foreach (var cell in NodeGraphRules.PlateCells(graph.Data, height))
            {
                var plate = NodeBuildOps.Spawn(config.floorTilePrefab, parts).transform;
                plate.localPosition = new Vector3(cell.x + 0.5f, cell.y - config.plateThickness / 2f, cell.z + 0.5f);
                plate.localScale = new Vector3(1f, config.plateThickness, 1f);
            }
            NodeBuildOps.RegisterCreated(parts.gameObject);
        }
    }
}
