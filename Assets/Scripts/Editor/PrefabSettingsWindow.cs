using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 프리팹 설정 창(메뉴 Slime~ColorMaker~/프리팹 설정). 왼쪽에 기물·장식·블록 프리팹 목록(폴더별), 오른쪽에 고른 프리팹의
    /// '기물 값'(맨 위 오브젝트 컴포넌트의 값: 수·시간·켜기/끄기·설정 애셋 등. 설정 애셋은 펼쳐서 그 값도 고침)과
    /// '모델 겉모습'(<see cref="ModelLookGUI"/>: 프리셋과 그 값, 발광 부위가 있으면 발광·광원 값)을 보여 주고, 고치면 바로 저장한다(되돌리기 가능).
    /// 놓을 때마다 정하는 값(<see cref="PerPlacementAttribute"/>: 색·방향·맵 안 id 등)과 씬 안 연결 참조는 숨긴다.
    /// 놓인 프리팹은 덮어쓴 값이 없으면 함께 바뀐다.
    /// </summary>
    public class PrefabSettingsWindow : EditorWindow
    {
        static readonly string[] Folders = { "Assets/Prefabs/Fixtures", "Assets/Prefabs/Decoration", "Assets/Prefabs/CubeGrid" };

        [SerializeField] string selected;
        [SerializeField] string search = "";
        Vector2 listScroll, detailScroll;
        List<string> prefabs;

        [MenuItem("Slime~ColorMaker~/프리팹 설정")]
        static void Open() => GetWindow<PrefabSettingsWindow>("프리팹 설정");

        void OnEnable() => EditorApplication.projectChanged += Reload;
        void OnDisable() => EditorApplication.projectChanged -= Reload;

        void Reload()
        {
            prefabs = null;
            Repaint();
        }

        void OnGUI()
        {
            prefabs ??= AssetDatabase.FindAssets("t:Prefab", Folders.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToList();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(220f)))
                {
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    DrawList();
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.Box(GUIContent.none, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    DrawDetail();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawList()
        {
            string folder = null;
            foreach (var path in prefabs)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (search.Length > 0 && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string group = Path.GetDirectoryName(path).Replace('\\', '/').Replace("Assets/Prefabs/", "");
                if (group != folder)
                {
                    folder = group;
                    EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                }
                var icon = AssetPreview.GetAssetPreview(AssetDatabase.LoadAssetAtPath<GameObject>(path)) ?? AssetPreview.GetMiniThumbnail(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var style = new GUIStyle(EditorStyles.label) { fixedHeight = 36f, imagePosition = ImagePosition.ImageLeft };
                var rect = GUILayoutUtility.GetRect(new GUIContent(name), style, GUILayout.Height(36f));
                if (path == selected) EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                if (icon != null) GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, 32f, 32f), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(rect.x + 40f, rect.y, rect.width - 40f, rect.height), name, new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft });
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    selected = path;
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        void DrawDetail()
        {
            var root = selected != null ? AssetDatabase.LoadAssetAtPath<GameObject>(selected) : null;
            if (root == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 프리팹을 고른다.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(root.name, EditorStyles.largeLabel);
                if (GUILayout.Button("Project에서 보기", GUILayout.Width(110f))) EditorGUIUtility.PingObject(root);
                if (GUILayout.Button("프리팹 열기", GUILayout.Width(90f))) AssetDatabase.OpenAsset(root);
            }
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("기물 값", EditorStyles.boldLabel);
            bool any = false;
            foreach (var component in root.GetComponents<MonoBehaviour>())
            {
                if (component == null || component is VoxPaint) continue;
                any |= DrawComponent(component);
            }
            if (!any) EditorGUILayout.LabelField("(고칠 값 없음)", EditorStyles.miniLabel);

            if (root.GetComponentInChildren<VoxPaint>(true) != null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("모델 겉모습", EditorStyles.boldLabel);
                ModelLookGUI.Draw(root, selected);

            }
        }

        // 설정 애셋의 값을 그 자리에서 고친다(같이 쓰는 곳이 모두 바뀜).
        static void DrawAsset(ScriptableObject asset)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"애셋 '{asset.name}' (같이 쓰는 곳이 모두 바뀜)", EditorStyles.miniLabel);
            var serialized = new SerializedObject(asset);
            serialized.Update();
            var property = serialized.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(asset);
            EditorGUI.indentLevel--;
        }

        // 컴포넌트의 값(연결 참조 빼고)을 그린다. 그린 것이 있으면 true.
        static bool DrawComponent(MonoBehaviour component)
        {
            var serialized = new SerializedObject(component);
            serialized.Update();
            var shown = new List<SerializedProperty>();
            var property = serialized.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
                if (property.name != "m_Script" && IsValue(component.GetType(), property.name)) shown.Add(property.Copy());
            if (shown.Count == 0) return false;

            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(component.GetType().Name), EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var settings = new List<ScriptableObject>();
            foreach (var shownProperty in shown)
            {
                EditorGUILayout.PropertyField(shownProperty, true);
                if (shownProperty.propertyType == SerializedPropertyType.ObjectReference && shownProperty.objectReferenceValue is ScriptableObject asset)
                    settings.Add(asset);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(component.gameObject);
            }
            foreach (var asset in settings) DrawAsset(asset);
            EditorGUI.indentLevel--;
            return true;
        }

        // 값으로 보일 필드인가: 놓을 때마다 정하는 값([PerPlacement])은 숨기고, 오브젝트 참조는 애셋(설정·재질 등)만 보인다(씬 안 연결은 숨김).
        static bool IsValue(Type type, string fieldName)
        {
            FieldInfo field = null;
            for (var t = type; t != null && field == null; t = t.BaseType)
                field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) return true;
            if (field.IsDefined(typeof(PerPlacementAttribute), true)) return false;
            var fieldType = field.FieldType;
            if (fieldType.IsArray) fieldType = fieldType.GetElementType();
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)) fieldType = fieldType.GetGenericArguments()[0];
            return !typeof(Component).IsAssignableFrom(fieldType) && fieldType != typeof(GameObject);
        }
    }
}
