using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 씬 뷰의 노드 편집 도구. 노드 그래프를 고르면 씬 뷰 툴바에 나타난다(노드 편집 창의 '씬 뷰 도구 켜기'로도 켬).
    /// 빈 곳 클릭: 노드 찍기(정수 스냅) / 노드·벽 클릭: 고르기 /
    /// Shift+클릭: 고른 노드에서 벽 잇기(빈 곳이면 노드를 찍고 이어 연달아 그림) / 끌기: 노드 옮기기(놓을 때 다시 만듦) /
    /// Delete: 고른 노드(붙은 벽 포함)나 벽 지우기. 노드와 벽은 벽에 가려지지 않게 바닥 높이에 겹쳐 그린다.
    /// </summary>
    [EditorTool("노드 편집", typeof(NodeGraph))]
    public class NodeGraphSceneTool : EditorTool
    {
        const float PickPixels = 10f;

        static readonly Color WallColor = new(1f, 1f, 1f, 0.8f);
        static readonly Color LaserColor = new(1f, 0.3f, 0.3f, 0.9f);
        static readonly Color DoorColor = new(0.3f, 0.9f, 1f, 1f);
        static readonly Color SelectedColor = new(1f, 0.85f, 0.1f, 1f);

        int draggingNodeId;
        Vector3Int dragTarget;
        bool dragged;
        Vector3Int? hover;

        public override GUIContent toolbarIcon => new("노드", "노드 편집 (Slime ~ColorMaker~)");

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView) return;
            var graph = (NodeGraph)target;
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(control); // 클릭이 다른 오브젝트를 고르지 않게 한다.
                    break;
                case EventType.MouseMove:
                    hover = GroundPoint(graph, e.mousePosition);
                    window.Repaint();
                    break;
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    OnMouseDown(graph, e, control);
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control && draggingNodeId != 0:
                    var point = GroundPoint(graph, e.mousePosition);
                    if (point.HasValue && point.Value != dragTarget)
                    {
                        dragTarget = point.Value;
                        dragged = true;
                    }
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    if (dragged) NodeGraphEditing.SelectNode(NodeGraphEditing.MoveNode(graph, draggingNodeId, dragTarget));
                    draggingNodeId = 0;
                    e.Use();
                    break;
                // 씬 뷰의 Delete는 명령으로 온다. 가로채지 않으면 노드 그래프 오브젝트가 지워진다.
                case EventType.ValidateCommand when IsDelete(e):
                    e.Use();
                    break;
                case EventType.ExecuteCommand when IsDelete(e):
                    DeleteSelected(graph);
                    e.Use();
                    break;
                case EventType.Repaint:
                    Draw(graph);
                    break;
            }
        }

        void OnMouseDown(NodeGraph graph, Event e, int control)
        {
            int nodeId = PickNode(graph, e.mousePosition);
            int wallId = nodeId == 0 ? PickWall(graph, e.mousePosition) : 0;
            var ground = GroundPoint(graph, e.mousePosition);
            int selected = NodeGraphEditing.SelectedNodeId;

            if (e.shift && selected != 0 && NodeGraphRules.FindNode(graph.Data, selected) != null)
            {
                if (nodeId != 0)
                {
                    NodeGraphEditing.Connect(graph, selected, nodeId);
                    NodeGraphEditing.SelectNode(nodeId);
                }
                else if (ground.HasValue)
                {
                    NodeGraphEditing.SelectNode(NodeGraphEditing.ConnectToNewNode(graph, selected, ground.Value));
                }
            }
            else if (nodeId != 0)
            {
                NodeGraphEditing.SelectNode(nodeId);
                draggingNodeId = nodeId;
                dragTarget = NodeGraphRules.FindNode(graph.Data, nodeId).position;
                dragged = false;
                GUIUtility.hotControl = control;
            }
            else if (wallId != 0)
            {
                NodeGraphEditing.SelectWall(wallId);
            }
            else if (ground.HasValue)
            {
                NodeGraphEditing.SelectNode(NodeGraphEditing.AddNode(graph, ground.Value));
            }
            e.Use();
        }

        static bool IsDelete(Event e) => e.commandName is "Delete" or "SoftDelete";

        static void DeleteSelected(NodeGraph graph)
        {
            if (NodeGraphEditing.SelectedNodeId != 0) NodeGraphEditing.RemoveNode(graph, NodeGraphEditing.SelectedNodeId);
            else if (NodeGraphEditing.SelectedWallId != 0) NodeGraphEditing.RemoveWall(graph, NodeGraphEditing.SelectedWallId);
            NodeGraphEditing.ClearSelection();
        }

        // 마우스 아래 바닥(높이 0의 평면)의 정수 좌표. 노드 그래프 기준이다.
        static Vector3Int? GroundPoint(NodeGraph graph, Vector2 mouse)
        {
            var root = graph.transform;
            var plane = new Plane(root.up, root.position);
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (!plane.Raycast(ray, out float distance)) return null;
            var local = root.InverseTransformPoint(ray.GetPoint(distance));
            return new Vector3Int(Mathf.RoundToInt(local.x), 0, Mathf.RoundToInt(local.z));
        }

        static int PickNode(NodeGraph graph, Vector2 mouse)
        {
            int picked = 0;
            float best = PickPixels;
            foreach (var node in graph.Data.nodes)
            {
                float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(World(graph, node.position)), mouse);
                if (distance < best)
                {
                    best = distance;
                    picked = node.id;
                }
            }
            return picked;
        }

        static int PickWall(NodeGraph graph, Vector2 mouse)
        {
            int picked = 0;
            float best = PickPixels;
            foreach (var wall in graph.Data.walls)
            {
                if (!TryEnds(graph, wall, out var start, out var end)) continue;
                float distance = HandleUtility.DistancePointLine(mouse, HandleUtility.WorldToGUIPoint(start), HandleUtility.WorldToGUIPoint(end));
                if (distance < best)
                {
                    best = distance;
                    picked = wall.id;
                }
            }
            return picked;
        }

        void Draw(NodeGraph graph)
        {
            var data = graph.Data;
            var zTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            using (new Handles.DrawingScope(Color.white))
            {
                foreach (var wall in data.walls)
                {
                    if (!TryEnds(graph, wall, out var start, out var end)) continue;
                    bool selected = wall.id == NodeGraphEditing.SelectedWallId;
                    Handles.color = selected ? SelectedColor : wall.kind == WallKind.Laser ? LaserColor : WallColor;
                    Handles.DrawLine(start, end, selected ? 5f : 3f);
                    if (wall.hasDoor && wall.kind == WallKind.Normal) DrawDoor(wall, start, end);
                }
                foreach (var node in data.nodes)
                {
                    var position = World(graph, node.position);
                    Handles.color = node.id == NodeGraphEditing.SelectedNodeId ? SelectedColor : Color.white;
                    Handles.DotHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.06f, EventType.Repaint);
                }
                if (draggingNodeId != 0 && dragged)
                {
                    Handles.color = SelectedColor;
                    var target = World(graph, dragTarget);
                    Handles.DotHandleCap(0, target, Quaternion.identity, HandleUtility.GetHandleSize(target) * 0.06f, EventType.Repaint);
                    foreach (var wall in data.walls)
                    {
                        int other = wall.startNodeId == draggingNodeId ? wall.endNodeId : wall.endNodeId == draggingNodeId ? wall.startNodeId : 0;
                        var otherNode = NodeGraphRules.FindNode(data, other);
                        if (otherNode != null) Handles.DrawDottedLine(target, World(graph, otherNode.position), 4f);
                    }
                }
                if (hover.HasValue && draggingNodeId == 0)
                {
                    Handles.color = new Color(1f, 1f, 1f, 0.5f);
                    var point = World(graph, hover.Value);
                    Handles.DrawWireDisc(point, graph.transform.up, HandleUtility.GetHandleSize(point) * 0.08f);
                }
            }
            Handles.zTest = zTest;
        }

        static void DrawDoor(WallData wall, Vector3 start, Vector3 end)
        {
            var config = NodeGraphEditing.FindConfig();
            if (config == null) return;
            float length = Vector3.Distance(start, end);
            float doorStart = NodeGraphRules.DoorStart(length, wall.door.offsetModules);
            float doorEnd = doorStart + NodeGraphRules.DoorModules(config.doorWidth) * NodeGraphRules.ModuleLength;
            var direction = (end - start) / length;
            Handles.color = DoorColor;
            Handles.DrawLine(start + direction * doorStart, start + direction * doorEnd, 7f);
        }

        static bool TryEnds(NodeGraph graph, WallData wall, out Vector3 start, out Vector3 end)
        {
            var a = NodeGraphRules.FindNode(graph.Data, wall.startNodeId);
            var b = NodeGraphRules.FindNode(graph.Data, wall.endNodeId);
            start = a != null ? World(graph, a.position) : default;
            end = b != null ? World(graph, b.position) : default;
            return a != null && b != null;
        }

        static Vector3 World(NodeGraph graph, Vector3Int position) => graph.transform.TransformPoint(position);
    }
}
