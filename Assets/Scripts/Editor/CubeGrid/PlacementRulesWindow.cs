using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 설치 규칙 창(메뉴 Slime~ColorMaker~/설치 규칙). 큐브 맵 붓으로 놓는 모든 것(지형·기물·이동·장식)의 프리팹을 묶음별로 한 줄씩 늘어놓고,
    /// 프리팹마다 설치 규칙(<see cref="PlacementRules"/>)을 [검사][방향][✕] 줄로 고친다. 고치면 프리팹에 바로 저장한다.
    /// 규칙 컴포넌트가 없는 프리팹은 어디든 놓이고, '+ 규칙'으로 붙인다.
    /// </summary>
    public class PlacementRulesWindow : EditorWindow
    {
        static readonly (string group, CubeGridEditing.Brush[] brushes)[] Groups =
        {
            ("지형", new[] { CubeGridEditing.Brush.Block, CubeGridEditing.Brush.Stair }),
            ("기물", new[] { CubeGridEditing.Brush.Checkpoint, CubeGridEditing.Brush.ColorSource, CubeGridEditing.Brush.Goal, CubeGridEditing.Brush.Gate, CubeGridEditing.Brush.Barrier }),
            ("이동", new[] { CubeGridEditing.Brush.ConveyorBelt }),
            ("장식", new[] { CubeGridEditing.Brush.Pillar, CubeGridEditing.Brush.Light, CubeGridEditing.Brush.Crate, CubeGridEditing.Brush.Railing, CubeGridEditing.Brush.Panel }),
        };

        const float Margin = 12f;
        const float Icon = 32f;
        const float Indent = Margin + Icon + 10f;

        Vector2 scroll;

        [MenuItem("Slime~ColorMaker~/설치 규칙")]
        static void Open() => GetWindow<PlacementRulesWindow>("설치 규칙");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "규칙 중 하나라도 맞으면 놓인다(규칙이 없으면 어디든). 받침이 사라지면 이어서 지워진다.\n" +
                "완전한 면: 그 방향 옆 칸 지형의 칸 경계 면이 꽉 참 · 같은 종류에 연결: 그 방향 옆에 같은 종류.\n" +
                "(세계) 방향은 놓인 방향과 상관없고, (기물) 방향은 놓인 회전을 따라 돈다.",
                MessageType.None);
            var config = CubeGridEditing.FindConfig();
            if (config == null)
            {
                EditorGUILayout.HelpBox("큐브 맵 설정 애셋(CubeGridConfig)이 없다.", MessageType.Warning);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var (group, brushes) in Groups)
            {
                GroupHeader(group);
                for (int i = 0; i < brushes.Length; i++)
                {
                    if (i > 0) Line(new Color(0.5f, 0.5f, 0.5f, 0.25f), Indent);
                    DrawPrefab(CubeGridEditing.BrushNames[(int)brushes[i]], CubeGridEditing.PrefabFor(config, brushes[i]));
                }
            }
            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        static void GroupHeader(string group)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
            Line(new Color(0.5f, 0.5f, 0.5f, 0.6f), 0f);
        }

        static void Line(Color color, float indent)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            rect.xMin += indent;
            EditorGUI.DrawRect(rect, color);
        }

        static void DrawPrefab(string label, GameObject prefab)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(Margin);
                var iconRect = GUILayoutUtility.GetRect(Icon, Icon, GUILayout.Width(Icon), GUILayout.Height(Icon));
                var icon = prefab != null ? AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab) : null;
                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUILayout.Space(6f);
                var nameStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
                if (GUILayout.Button(label, nameStyle, GUILayout.Height(Icon)) && prefab != null) EditorGUIUtility.PingObject(prefab);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(Indent);
                using (new EditorGUILayout.VerticalScope())
                {
                    if (prefab == null) EditorGUILayout.LabelField("(큐브 맵 설정에 프리팹이 없다)", EditorStyles.miniLabel);
                    else DrawRules(prefab);
                }
            }
            EditorGUILayout.Space(4f);
        }

        static void DrawRules(GameObject prefab)
        {
            if (!prefab.TryGetComponent(out PlacementRules rules))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("규칙 없음 (어디든)", EditorStyles.miniLabel, GUILayout.Width(110f));
                    if (GUILayout.Button("+ 규칙", GUILayout.Width(70f))) AddRules(prefab);
                    GUILayout.FlexibleSpace();
                }
                return;
            }
            var serialized = new SerializedObject(rules);
            serialized.Update();
            var list = serialized.FindProperty("rules");
            int remove = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var rule = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("check"), GUIContent.none, GUILayout.Width(130f));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("direction"), GUIContent.none, GUILayout.Width(130f));
                    if (GUILayout.Button("✕", GUILayout.Width(24f))) remove = i;
                    GUILayout.FlexibleSpace();
                }
            }
            if (list.arraySize == 0) EditorGUILayout.LabelField("규칙 없음 (어디든)", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 규칙", GUILayout.Width(70f)))
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    var added = list.GetArrayElementAtIndex(list.arraySize - 1);
                    added.FindPropertyRelative("check").intValue = (int)PlacementCheck.FullFace;
                    added.FindPropertyRelative("direction").intValue = (int)PlacementDirection.Down;
                }
                GUILayout.FlexibleSpace();
            }
            if (remove >= 0) list.DeleteArrayElementAtIndex(remove);
            if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(prefab);
        }

        // 프리팹 맨 위(Transform 바로 아래 칸)에 설치 규칙을 붙이고 첫 규칙(완전한 면 · 아랫방향)을 넣는다.
        static void AddRules(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = root.AddComponent<PlacementRules>();
                while (UnityEditorInternal.ComponentUtility.MoveComponentUp(component)) { }
                component.Set(new List<PlacementRule> { new(PlacementCheck.FullFace, PlacementDirection.Down) });
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
