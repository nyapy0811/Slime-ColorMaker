using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵 편집 창(메뉴 Slime~ColorMaker~/큐브 맵 편집). 지금 맵의 큐브 격자를 찾아 씬 뷰 도구를 켜고 끄고, 블록을 다시 만들고,
    /// 붓을 드롭다운(지형·기물·이동·장식·시험 묶음)에서 고른다. 고른 붓에 필요한 설정(방향, 방벽 면, 색, 기둥 머리·받침, 시험 모델)만
    /// 그 아래 상자에 보인다(컨베이어는 모양 직진·좌회전·우회전도). 색 원천 값과 요구 색은 놓기 전에 정한다(놓은 뒤에는 기물을 골라 인스펙터에서 고침). 붓별 규칙은 코드 흐름 문서 11절.
    /// </summary>
    public class CubeGridWindow : EditorWindow
    {
        // 붓 드롭다운의 묶음(묶음 사이에 구분선).
        static readonly (string name, CubeGridEditing.Brush[] brushes)[] BrushGroups =
        {
            ("지형", new[] { CubeGridEditing.Brush.Block, CubeGridEditing.Brush.Stair }),
            ("기물", new[] { CubeGridEditing.Brush.Checkpoint, CubeGridEditing.Brush.ColorSource, CubeGridEditing.Brush.Goal, CubeGridEditing.Brush.BoomGate, CubeGridEditing.Brush.Barrier }),
            ("이동", new[] { CubeGridEditing.Brush.ConveyorBelt }),
            ("장식", new[] { CubeGridEditing.Brush.Pillar }),
            ("시험", new[] { CubeGridEditing.Brush.TestModel }),
        };

        static readonly string[] TurnNames = { "0°", "90°", "180°", "270°" };

        const float SettingsLabelWidth = 90f;

        [MenuItem("Slime~ColorMaker~/큐브 맵 편집")]
        static void Open() => GetWindow<CubeGridWindow>("큐브 맵 편집");

        void OnEnable()
        {
            CubeGridEditing.BrushChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
            ToolManager.activeToolChanged += Repaint;
        }

        void OnDisable()
        {
            CubeGridEditing.BrushChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
            ToolManager.activeToolChanged -= Repaint;
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
                bool toolOn = ToolManager.activeToolType == typeof(CubeGridSceneTool);
                if (GUILayout.Button(toolOn ? "씬 뷰 도구 끄기" : "씬 뷰 도구 켜기"))
                {
                    if (toolOn)
                    {
                        // 켤 때 고른 큐브 격자도 놓는다: 골라져 있으면 Unity가 자식(블록·계단·기둥)의 콜라이더를 초록 선으로 계속 그린다.
                        ToolManager.RestorePreviousPersistentTool();
                        if (Selection.activeGameObject == grid.gameObject) Selection.activeObject = null;
                    }
                    else
                    {
                        // 고른 오브젝트는 다음 에디터 갱신에서야 도구의 대상이 된다(바로 켜면 '편집할 오브젝트 없음'으로 실패) → 한 번 미뤄 켠다.
                        Selection.activeGameObject = grid.gameObject;
                        EditorApplication.delayCall += () =>
                        {
                            if (grid != null && Selection.activeGameObject == grid.gameObject) ToolManager.SetActiveTool<CubeGridSceneTool>();
                        };
                    }
                }
                if (GUILayout.Button("다시 만들기")) CubeGridEditing.Rebuild(grid);
            }

            EditorGUILayout.Space();
            DrawBrushPicker();
            DrawSettings(CubeGridEditing.SelectedBrush);

            EditorGUILayout.HelpBox(
                "클릭: 놓기   Ctrl+클릭: 지우기\n" +
                "Shift+드래그: 범위에 블록 놓기   Shift+Ctrl+드래그: 범위 지우기\n" +
                "방향은 씬 카메라가 보는 쪽 기준",
                MessageType.None);

            EditorGUILayout.LabelField($"블록 {grid.Data.blocks.Count}개, 계단 {grid.Data.stairs.Count}개, 기둥 {grid.Data.pillars.Count}개");
        }

        // 붓 드롭다운: 묶음 이름(고를 수 없음) 아래에 그 묶음의 붓, 묶음 사이에 구분선.
        static void DrawBrushPicker()
        {
            var current = CubeGridEditing.SelectedBrush;
            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("붓"));
            if (!EditorGUI.DropdownButton(rect, new GUIContent($"{GroupName(current)} · {CubeGridEditing.BrushNames[(int)current]}"), FocusType.Keyboard)) return;
            var menu = new GenericMenu();
            for (int i = 0; i < BrushGroups.Length; i++)
            {
                if (i > 0) menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent(BrushGroups[i].name));
                foreach (var brush in BrushGroups[i].brushes)
                {
                    var picked = brush;
                    menu.AddItem(new GUIContent("    " + CubeGridEditing.BrushNames[(int)brush]), brush == current, () => CubeGridEditing.SelectedBrush = picked);
                }
            }
            menu.DropDown(rect);
        }

        static string GroupName(CubeGridEditing.Brush brush)
        {
            foreach (var (name, brushes) in BrushGroups)
                if (System.Array.IndexOf(brushes, brush) >= 0) return name;
            return "";
        }

        // 고른 붓에 필요한 설정만 한 상자에. 설정이 없는 붓(블록, 체크포인트)은 상자를 그리지 않는다.
        static void DrawSettings(CubeGridEditing.Brush brush)
        {
            bool hasValue = CubeGridEditing.ValueField(brush) != null;
            bool turns = brush is CubeGridEditing.Brush.Stair or CubeGridEditing.Brush.BoomGate || CubeGridEditing.IsConveyor(brush)
                || (brush == CubeGridEditing.Brush.Barrier && CubeGridEditing.SelectedBarrierSide == CubeGridEditing.BarrierSide.Side);
            bool other = brush is CubeGridEditing.Brush.Barrier or CubeGridEditing.Brush.Pillar or CubeGridEditing.Brush.TestModel;
            if (!hasValue && !turns && !other) return;

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = SettingsLabelWidth;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (brush == CubeGridEditing.Brush.TestModel)
                {
                    var model = (GameObject)EditorGUILayout.ObjectField("모델", CubeGridEditing.TestModelAsset, typeof(GameObject), false);
                    if (model != CubeGridEditing.TestModelAsset) CubeGridEditing.TestModelAsset = model;
                }
                if (brush == CubeGridEditing.Brush.Pillar)
                {
                    bool top = EditorGUILayout.Toggle("머리 두기", CubeGridEditing.PillarHasTop);
                    if (top != CubeGridEditing.PillarHasTop) CubeGridEditing.PillarHasTop = top;
                    bool bottom = EditorGUILayout.Toggle("받침 두기", CubeGridEditing.PillarHasBase);
                    if (bottom != CubeGridEditing.PillarHasBase) CubeGridEditing.PillarHasBase = bottom;
                }
                if (brush == CubeGridEditing.Brush.Barrier)
                {
                    int side = LabeledToolbar("면", (int)CubeGridEditing.SelectedBarrierSide, CubeGridEditing.BarrierSideNames);
                    if (side != (int)CubeGridEditing.SelectedBarrierSide) CubeGridEditing.SelectedBarrierSide = (CubeGridEditing.BarrierSide)side;
                }
                if (CubeGridEditing.IsConveyor(brush))
                {
                    int shape = System.Array.IndexOf(CubeGridEditing.ConveyorShapeOrder, CubeGridEditing.SelectedConveyorShape);
                    int picked = LabeledToolbar("모양", shape, CubeGridEditing.ConveyorShapeNames);
                    if (picked != shape) CubeGridEditing.SelectedConveyorShape = CubeGridEditing.ConveyorShapeOrder[picked];
                }
                if (turns)
                {
                    int turn = LabeledToolbar("방향", CubeGridEditing.StairTurnOffset, TurnNames);
                    if (turn != CubeGridEditing.StairTurnOffset) CubeGridEditing.StairTurnOffset = turn;
                }
                if (hasValue) DrawBrushValue(brush);
            }
            EditorGUIUtility.labelWidth = labelWidth;
        }

        static int LabeledToolbar(string label, int selected, string[] names)
        {
            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(label));
            return GUI.Toolbar(rect, selected, names);
        }

        // 붓으로 놓을 값(채널마다 0 ~ 한도, 붓마다 따로 기억): 색 미리보기 + R·G·B, 쓸 수 없는 값이면 경고.
        static void DrawBrushValue(CubeGridEditing.Brush brush)
        {
            var value = CubeGridEditing.BrushValue(brush);
            bool source = brush == CubeGridEditing.Brush.ColorSource;

            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(source ? "더하는 색" : "요구 색"));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, 40f, rect.height - 2f), ColorRules.ToColor(value));
            EditorGUI.LabelField(new Rect(rect.x + 46f, rect.y, rect.width - 46f, rect.height), $"({value.R}, {value.G}, {value.B})");

            int r = EditorGUILayout.IntSlider("R", value.R, 0, ColorRules.ChannelMax);
            int g = EditorGUILayout.IntSlider("G", value.G, 0, ColorRules.ChannelMax);
            int b = EditorGUILayout.IntSlider("B", value.B, 0, ColorRules.ChannelMax);
            var changed = new ColorValue(r, g, b);
            if (changed != value) CubeGridEditing.SetBrushValue(brush, changed);

            if (source ? !ColorRules.IsValidSourceValue(changed) : !ColorRules.IsValidRequired(changed, ColorRules.Limit))
                EditorGUILayout.HelpBox(source ? "쓸 수 없는 값: 세 채널이 모두 같으면 안 된다((0, 0, 0) 포함)." : "쓸 수 없는 값: 모든 채널이 1 이상이고, 세 채널이 모두 같으면 안 된다.", MessageType.Warning);
        }
    }
}
