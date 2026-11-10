using System;
using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 노드 편집 창과 씬 뷰 도구가 함께 쓰는 편집 동작과 선택. 편집마다 데이터를 바꾸고, 같은 자리 노드를 합치고,
    /// 벽을 다시 만드는 것을 Undo 하나로 묶는다. 선택은 id로 들고(0이면 없음) 바뀌면 알린다.
    /// </summary>
    public static class NodeGraphEditing
    {
        public static int SelectedNodeId { get; private set; }
        public static int SelectedWallId { get; private set; }

        public static event Action SelectionChanged;

        public static void SelectNode(int id) => Select(id, 0);

        public static void SelectWall(int id) => Select(0, id);

        public static void ClearSelection() => Select(0, 0);

        static void Select(int nodeId, int wallId)
        {
            SelectedNodeId = nodeId;
            SelectedWallId = wallId;
            SelectionChanged?.Invoke();
            SceneView.RepaintAll();
        }

        public static NodeBuildConfig FindConfig()
        {
            var guids = AssetDatabase.FindAssets("t:NodeBuildConfig");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<NodeBuildConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static NodeGraph CreateGraph()
        {
            var go = new GameObject("NodeGraph");
            Undo.RegisterCreatedObjectUndo(go, "노드 그래프 만들기");
            Selection.activeGameObject = go;
            return go.AddComponent<NodeGraph>();
        }

        /// <summary>새 노드를 찍고 그 id를 돌려준다. 같은 자리에 노드가 있으면 그 노드다.</summary>
        public static int AddNode(NodeGraph graph, Vector3Int position)
        {
            int id = 0;
            Edit(graph, "노드 찍기", data => id = NodeGraphRules.AddNode(data, position).id);
            return id;
        }

        /// <summary>두 노드를 잇는다. 이을 수 없으면(같은 노드, 높이가 다름, 이미 이어짐) 아무것도 하지 않는다.</summary>
        public static void Connect(NodeGraph graph, int fromNodeId, int toNodeId)
        {
            Edit(graph, "벽 잇기", data => NodeGraphRules.Connect(data, fromNodeId, toNodeId));
        }

        /// <summary>새 노드를 찍고 고른 노드와 잇는다. 새 노드의 id를 돌려준다.</summary>
        public static int ConnectToNewNode(NodeGraph graph, int fromNodeId, Vector3Int position)
        {
            int id = 0;
            Edit(graph, "벽 잇기", data =>
            {
                id = NodeGraphRules.AddNode(data, position).id;
                NodeGraphRules.Connect(data, fromNodeId, id);
            });
            return id;
        }

        /// <summary>노드를 옮긴다. 다른 노드 자리로 옮기면 합쳐지므로 옮긴 뒤 그 자리의 노드 id를 돌려준다.</summary>
        public static int MoveNode(NodeGraph graph, int nodeId, Vector3Int position)
        {
            Edit(graph, "노드 옮기기", data =>
            {
                var node = NodeGraphRules.FindNode(data, nodeId);
                if (node != null) node.position = position;
            });
            return NodeGraphRules.FindNodeAt(graph.Data, position)?.id ?? 0;
        }

        public static void RemoveNode(NodeGraph graph, int nodeId) =>
            Edit(graph, "노드 지우기", data => NodeGraphRules.RemoveNode(data, nodeId));

        public static void RemoveWall(NodeGraph graph, int wallId) =>
            Edit(graph, "벽 지우기", data => NodeGraphRules.RemoveWall(data, wallId));

        /// <summary>벽 종류를 바꾼다. 레이저 벽에는 문을 끼우지 않는다.</summary>
        public static void SetWallKind(NodeGraph graph, int wallId, WallKind kind) =>
            Edit(graph, "벽 종류 바꾸기", data =>
            {
                var wall = NodeGraphRules.FindWall(data, wallId);
                wall.kind = kind;
                if (kind == WallKind.Laser) wall.hasDoor = false;
            });

        /// <summary>문을 끼우거나 뺀다. 새로 끼우는 문은 새 id를 받고 구간 가운데에 놓인다.</summary>
        public static void SetDoor(NodeGraph graph, int wallId, bool hasDoor, NodeBuildConfig config) =>
            Edit(graph, hasDoor ? "문 끼우기" : "문 빼기", data =>
            {
                var wall = NodeGraphRules.FindWall(data, wallId);
                if (hasDoor == wall.hasDoor) return;
                wall.hasDoor = hasDoor;
                if (!hasDoor) return;
                NodeGraphRules.DoorOffsetRange(SegmentLength(data, wall), config.doorWidth, out int min, out int max);
                wall.door = new DoorData { id = NodeGraphRules.IssueId(data), offsetModules = (min + max) / 2 };
            });

        public static void SetDoorOffset(NodeGraph graph, int wallId, int offsetModules) =>
            Edit(graph, "문 옮기기", data => NodeGraphRules.FindWall(data, wallId).door.offsetModules = offsetModules);

        /// <summary>데이터는 그대로 두고 다시 만든다(기준 치수나 프리팹을 바꾼 뒤). 같은 자리 노드도 합친다.</summary>
        public static void Rebuild(NodeGraph graph) => Edit(graph, "벽 다시 만들기", _ => { });

        public static float SegmentLength(NodeGraphData data, WallData wall) =>
            NodeGraphRules.SegmentLength(NodeGraphRules.FindNode(data, wall.startNodeId).position, NodeGraphRules.FindNode(data, wall.endNodeId).position);

        static void Edit(NodeGraph graph, string undoName, Action<NodeGraphData> change)
        {
            var config = FindConfig();
            if (config == null)
            {
                Debug.LogError("[노드 편집] 기준 치수 애셋(NodeBuildConfig)이 없다. Assets/Data에 만든다.");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(graph, undoName);
            change(graph.Data);
            NodeGraphRules.MergeNodesAtSamePosition(graph.Data);
            NodeGraphBuilder.Build(graph, config);
            Undo.CollapseUndoOperations(group);
        }
    }
}
