using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵 편집 창(메뉴 Slime ~ColorMaker~/큐브 맵 편집). 지금 맵의 큐브 격자를 찾아 붓(블록, 계단, 체크포인트, 색 원천, 목적지, 차단기, 시험 모델)을 고르고,
    /// 씬 뷰 도구를 켜고, 블록을 다시 만든다. 기물 값(색 원천 값, 요구 색 등)은 놓은 뒤 인스펙터에서 고친다.
    /// </summary>
    public class CubeGridWindow : EditorWindow
    {
        [MenuItem("Slime ~ColorMaker~/큐브 맵 편집")]
        static void Open() => GetWindow<CubeGridWindow>("큐브 맵 편집");

        void OnEnable()
        {
            CubeGridEditing.BrushChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
        }

        void OnDisable()
        {
            CubeGridEditing.BrushChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
        }

        void OnHierarchyChange() => Repaint();

        void OnGUI()
        {
            var grid = FindAnyObjectByType<CubeGrid>();
            if (grid == null)
            {
                EditorGUILayout.HelpBox("지금 씬에 큐브 격자가 없다.", MessageType.Info);
                if (GUILayout.Button("큐브 격자 만들기")) CubeGridEditing.CreateGrid();
                return;
            }
            if (CubeGridEditing.FindConfig() == null)
            {
                EditorGUILayout.HelpBox("큐브 맵 설정 애셋(CubeGridConfig)이 없다. Assets/Data에 만든다.", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("씬 뷰 도구 켜기"))
                {
                    Selection.activeGameObject = grid.gameObject;
                    ToolManager.SetActiveTool<CubeGridSceneTool>();
                }
                if (GUILayout.Button("다시 만들기")) CubeGridEditing.Rebuild(grid);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("붓", EditorStyles.boldLabel);
            int brush = GUILayout.Toolbar((int)CubeGridEditing.SelectedBrush, CubeGridEditing.BrushNames);
            if (brush != (int)CubeGridEditing.SelectedBrush) CubeGridEditing.SelectedBrush = (CubeGridEditing.Brush)brush;
            if (CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.TestModel)
            {
                var model = (GameObject)EditorGUILayout.ObjectField("놓을 모델", CubeGridEditing.TestModelAsset, typeof(GameObject), false);
                if (model != CubeGridEditing.TestModelAsset) CubeGridEditing.TestModelAsset = model;
                EditorGUILayout.HelpBox("아트 모델을 맵에서 보는 용도(기물 아님). 칸 바닥 가운데에 놓이고 충돌은 한 칸(16 × 16 × 16) 상자다. 방향은 인스펙터에서 돌린다.", MessageType.None);
            }
            if (CubeGridEditing.SelectedBrush is CubeGridEditing.Brush.Stair or CubeGridEditing.Brush.BoomGate)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int offset = CubeGridEditing.StairTurnOffset;
                    var (what, way) = CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.Stair ? ("계단", "오름") : ("차단기", "지나감");
                    EditorGUILayout.LabelField(offset == 0 ? $"{what} 방향: 카메라가 보는 쪽으로 {way}" : $"{what} 방향: 카메라가 보는 쪽에서 {offset * 90}° 돌림");
                    if (GUILayout.Button("돌리기", GUILayout.Width(60))) CubeGridEditing.StairTurnOffset = offset + 1;
                }
            }

            EditorGUILayout.HelpBox(
                "클릭: 가리킨 면 쪽 칸에 놓기 / Ctrl+클릭: 지우기 / Shift+드래그: 범위에 블록 놓기 / Shift+Ctrl+드래그: 범위 지우기\n" +
                "아무것도 가리키지 않으면 높이 0의 바닥면에 놓는다(블록은 바닥면 아래 칸). 한 칸에는 하나만 둔다.\n" +
                "계단은 씬 카메라가 보는 쪽으로 오르게 놓인다(초록 선이 경사). 점프가 없으니 위 칸으로는 계단으로만 간다.\n" +
                "차단기는 씬 카메라가 보는 쪽으로 지나가게 놓인다(칸의 카메라 쪽 가장자리에 서고, 초록 선이 막대 자리).\n" +
                "기물 값과 방향은 놓은 뒤 인스펙터에서 고친다.",
                MessageType.None);

            EditorGUILayout.LabelField($"블록 {grid.Data.blocks.Count}개, 계단 {grid.Data.stairs.Count}개");
        }
    }
}
