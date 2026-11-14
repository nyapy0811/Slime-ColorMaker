using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 노드 편집 창(메뉴 Slime~ColorMaker~/노드 편집). 지금 맵의 노드 그래프를 찾아 노드·벽 목록, 고른 노드·벽의 값
    /// (벽 종류, 끼운 문과 위치), '다시 만들기' 버튼을 보인다. 선택은 씬 뷰 도구와 같이 쓴다.
    /// </summary>
    public class NodeGraphWindow : EditorWindow
    {
        static readonly string[] KindNames = { "일반 벽", "레이저 벽" };

        Vector2 scroll;

        [MenuItem("Slime~ColorMaker~/노드 편집")]
        static void Open() => GetWindow<NodeGraphWindow>("노드 편집");

        void OnEnable()
        {
            NodeGraphEditing.SelectionChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
        }

        void OnDisable()
        {
            NodeGraphEditing.SelectionChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
        }

        void OnHierarchyChange() => Repaint();

        void OnGUI()
        {
            var graph = FindAnyObjectByType<NodeGraph>();
            if (graph == null)
            {
                EditorGUILayout.HelpBox("지금 씬에 노드 그래프가 없다.", MessageType.Info);
                if (GUILayout.Button("노드 그래프 만들기")) NodeGraphEditing.CreateGraph();
                return;
            }
            var config = NodeGraphEditing.FindConfig();
            if (config == null)
            {
                EditorGUILayout.HelpBox("기준 치수 애셋(NodeBuildConfig)이 없다. Assets/Data/Map에 만든다.", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("씬 뷰 도구 켜기"))
                {
                    Selection.activeGameObject = graph.gameObject;
                    ToolManager.SetActiveTool<NodeGraphSceneTool>();
                }
                if (GUILayout.Button("다시 만들기")) NodeGraphEditing.Rebuild(graph);
            }
            EditorGUILayout.HelpBox(
                "빈 곳 클릭: 노드 찍기 / 노드·벽 클릭: 고르기 / Shift+클릭: 고른 노드에서 벽 잇기 / 끌기: 노드 옮기기 / Delete: 지우기",
                MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var data = graph.Data;
            var node = NodeGraphRules.FindNode(data, NodeGraphEditing.SelectedNodeId);
            var wall = NodeGraphRules.FindWall(data, NodeGraphEditing.SelectedWallId);
            if (node != null) DrawNode(graph, node);
            else if (wall != null) DrawWall(graph, wall, config);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"노드 ({data.nodes.Count})", EditorStyles.boldLabel);
            foreach (var item in data.nodes)
                if (ListButton(item.id == NodeGraphEditing.SelectedNodeId, $"#{item.id}  {item.position}"))
                    NodeGraphEditing.SelectNode(item.id);

            EditorGUILayout.LabelField($"벽 ({data.walls.Count})", EditorStyles.boldLabel);
            foreach (var item in data.walls)
            {
                string door = item.hasDoor && item.kind == WallKind.Normal ? $"  문 #{item.door.id}" : "";
                if (ListButton(item.id == NodeGraphEditing.SelectedWallId, $"#{item.id}  노드 #{item.startNodeId} – #{item.endNodeId}  {KindNames[(int)item.kind]}{door}"))
                    NodeGraphEditing.SelectWall(item.id);
            }
            EditorGUILayout.EndScrollView();
        }

        static void DrawNode(NodeGraph graph, NodeData node)
        {
            EditorGUILayout.LabelField($"고른 노드 #{node.id}", EditorStyles.boldLabel);
            var position = new Vector2Int(node.position.x, node.position.z);
            var edited = EditorGUILayout.Vector2IntField("위치 (x, z)", position);
            if (edited != position) NodeGraphEditing.SelectNode(NodeGraphEditing.MoveNode(graph, node.id, new Vector3Int(edited.x, 0, edited.y)));
            if (GUILayout.Button("노드 지우기 (붙은 벽도)"))
            {
                NodeGraphEditing.RemoveNode(graph, node.id);
                NodeGraphEditing.ClearSelection();
            }
        }

        static void DrawWall(NodeGraph graph, WallData wall, NodeBuildConfig config)
        {
            float length = NodeGraphEditing.SegmentLength(graph.Data, wall);
            EditorGUILayout.LabelField($"고른 벽 #{wall.id}  (노드 #{wall.startNodeId} – #{wall.endNodeId}, 길이 {length:0.##})", EditorStyles.boldLabel);

            int kind = EditorGUILayout.Popup("종류", (int)wall.kind, KindNames);
            if (kind != (int)wall.kind) NodeGraphEditing.SetWallKind(graph, wall.id, (WallKind)kind);

            if (wall.kind == WallKind.Normal)
            {
                bool canPlace = NodeGraphRules.DoorOffsetRange(length, config.doorWidth, out int min, out int max);
                using (new EditorGUI.DisabledScope(!canPlace && !wall.hasDoor))
                {
                    bool hasDoor = EditorGUILayout.Toggle("문 끼우기", wall.hasDoor);
                    if (hasDoor != wall.hasDoor) NodeGraphEditing.SetDoor(graph, wall.id, hasDoor, config);
                }
                if (!canPlace)
                    EditorGUILayout.HelpBox($"구간이 짧아 문을 끼울 수 없다(문 폭 {config.doorWidth}의 두 배쯤 필요).", wall.hasDoor ? MessageType.Error : MessageType.Info);
                else if (wall.hasDoor)
                {
                    if (!NodeGraphRules.CanPlaceDoor(length, config.doorWidth, wall.door.offsetModules))
                        EditorGUILayout.HelpBox("문이 끼울 수 없는 위치에 있다. 위치를 다시 고른다.", MessageType.Error);
                    // 범위 밖 값은 슬라이더가 잘라 보이므로, 사용자가 움직였을 때만 고친다.
                    EditorGUI.BeginChangeCheck();
                    int offset = EditorGUILayout.IntSlider(
                        new GUIContent("문 위치", "시작 노드 쪽 첫 벽 모듈부터 센 0.25 단위 수"), wall.door.offsetModules, min, max);
                    if (EditorGUI.EndChangeCheck()) NodeGraphEditing.SetDoorOffset(graph, wall.id, offset);
                }
            }

            if (GUILayout.Button("벽 지우기"))
            {
                NodeGraphEditing.RemoveWall(graph, wall.id);
                NodeGraphEditing.ClearSelection();
            }
        }

        static bool ListButton(bool selected, string label)
        {
            var style = new GUIStyle(EditorStyles.label) { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
            return GUILayout.Button((selected ? "▶ " : "   ") + label, style);
        }
    }
}
