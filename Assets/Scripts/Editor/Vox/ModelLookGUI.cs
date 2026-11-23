using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 프리팹 하나의 '모델 겉모습' 칸: 그 아래 모든 복셀 모델(<see cref="VoxPaint"/>)의 겉모습 프리셋(<see cref="ModelLook"/>)을 한 번에 고르고,
    /// 고른 프리셋의 값(모델들에 실제로 있는 부위의 것만)을 그 자리에서 고친다. 프리셋 값을 고치면 같이 쓰는 곳이 모두 바뀐다.
    /// 프리팹 맨 위 오브젝트의 인스펙터 머리 부분(<see cref="ModelLookHeader"/>)과 프리팹 설정 창이 같이 쓴다.
    /// </summary>
    public static class ModelLookGUI
    {
        static readonly Dictionary<string, bool> perModelOpen = new();

        // 발광 부위가 있을 때 보이는 값(프리셋과 VoxPaint가 같은 이름).
        static readonly (string field, string label)[] LightFields =
        {
            ("lightIntensity", "발광 세기"), ("lightRange", "광원 범위(칸)"), ("lightScale", "광원 세기 배율"), ("lightShadows", "광원 그림자"),
        };
        static Dictionary<ModelLook, List<string>> users;

        static ModelLookGUI() => EditorApplication.projectChanged += () => users = null;

        /// <summary>
        /// 프리팹의 모델 겉모습 칸을 그린다. <paramref name="prefabPath"/>가 있으면 그 프리팹 애셋을 고치고(씬에서 골랐을 때도),
        /// null이면 <paramref name="root"/>를 바로 고친다(프리팹 편집 화면).
        /// </summary>
        public static void Draw(GameObject root, string prefabPath)
        {
            var paints = root.GetComponentsInChildren<VoxPaint>(true);
            if (paints.Length == 0) return;
            string prefabName = prefabPath != null ? Path.GetFileNameWithoutExtension(prefabPath) : root.name;

            var looks = paints.Select(paint => paint.Look).Distinct().ToList();
            bool mixed = looks.Count > 1;
            var current = looks[0];

            var rect = EditorGUILayout.GetControlRect();
            const float buttonWidth = 72f;
            var fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - 4f, rect.height);
            var buttonRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = mixed;
            var picked = (ModelLook)EditorGUI.ObjectField(fieldRect, new GUIContent("프리셋", "이 프리팹의 모든 모델이 쓰는 겉모습 프리셋(Assets/Data/Looks). 비우면 모델마다 자기 값."),
                mixed ? null : current, typeof(ModelLook), false);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) SetLook(root, prefabPath, paints, picked);
            if (GUI.Button(buttonRect, new GUIContent("새로 만들기", $"{ModelLook.Folder}에 첫 모델의 지금 값으로 프리셋을 만들어 이 프리팹의 모든 모델에 넣는다.")))
                SetLook(root, prefabPath, paints, Create(prefabName, paints[0]));

            if (!mixed && current != null)
            {
                var others = UsersOf(current).Where(path => path != prefabPath).ToList();
                if (others.Count > 0)
                    EditorGUILayout.HelpBox($"같이 쓰는 프리팹 {others.Count}개: {string.Join(", ", others.Select(Path.GetFileNameWithoutExtension))} (값을 고치면 함께 바뀜)", MessageType.None);
                DrawValues(current, paints);
            }
            else if (mixed)
                EditorGUILayout.HelpBox("모델마다 프리셋이 다르다. 아래 '모델마다'에서 하나씩 고르거나 위에서 하나로 고른다.", MessageType.None);
            else
                EditorGUILayout.HelpBox("프리셋이 없어 모델마다 자기 값(모델 오브젝트의 VoxPaint)을 쓴다.", MessageType.None);

            if (paints.Length > 1)
            {
                string key = prefabPath ?? root.name;
                perModelOpen.TryGetValue(key, out bool open);
                open = EditorGUILayout.Foldout(open, $"모델마다 ({paints.Length}개)", true);
                perModelOpen[key] = open;
                if (open)
                {
                    EditorGUI.indentLevel++;
                    foreach (var paint in paints)
                    {
                        EditorGUI.BeginChangeCheck();
                        var one = (ModelLook)EditorGUILayout.ObjectField(paint.name, paint.Look, typeof(ModelLook), false);
                        if (EditorGUI.EndChangeCheck()) SetLook(root, prefabPath, new[] { paint }, one);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        /// <summary>프리셋의 값 중 모델들에 실제로 있는 부위의 것만 그린다(바로 프리셋에 저장).</summary>
        public static void DrawValues(ModelLook look, IReadOnlyCollection<VoxPaint> paints)
        {
            var renderers = paints.SelectMany(paint => paint.GetComponentsInChildren<Renderer>(true)).ToList();
            bool HasPart(params string[] names) => renderers.Any(renderer => names.Contains(renderer.name));
            bool hasGlass = HasPart(VoxPaint.GlassPartName, VoxPaint.GlassValuePartName);
            bool hasLight = HasPart(VoxPaint.LightPartName);
            bool hasValue = HasPart(VoxPaint.ValuePartName);
            bool hasBody = renderers.Any(renderer => renderer.name is not (VoxPaint.ValuePartName or VoxPaint.GlassPartName or VoxPaint.GlassValuePartName or VoxPaint.LightPartName));

            var serialized = new SerializedObject(look);
            serialized.Update();
            EditorGUI.indentLevel++;
            if (hasGlass) EditorGUILayout.PropertyField(serialized.FindProperty("transparency"), new GUIContent("투명도", serialized.FindProperty("transparency").tooltip));
            if (hasGlass && paints.Any(paint => paint.GetComponentInParent<IUsesEdgeTransparency>(true) != null))
                EditorGUILayout.PropertyField(serialized.FindProperty("edgeTransparency"), new GUIContent("가장자리 투명도", serialized.FindProperty("edgeTransparency").tooltip));
            if (hasLight)
                foreach (var (field, label) in LightFields)
                    EditorGUILayout.PropertyField(serialized.FindProperty(field), new GUIContent(label, serialized.FindProperty(field).tooltip));
            serialized.ApplyModifiedProperties();
            string name = look.name;
            if (hasBody) FinishRow(look, "bodyFinish", "재질감: 일반", $"{name}Body");
            if (hasValue) FinishRow(look, "valueFinish", "재질감: 값 색", $"{name}Value");
            if (hasGlass) FinishRow(look, "glassFinish", "재질감: 반투명", $"{name}Glass");
            if (hasLight) FinishRow(look, "lightFinish", "재질감: 발광", $"{name}Light");
            EditorGUI.indentLevel--;
        }

        static void FinishRow(ModelLook look, string field, string label, string newName)
        {
            var serialized = new SerializedObject(look);
            var property = serialized.FindProperty(field);
            if (!SurfaceFinishGUI.Field(label, (SurfaceFinish)property.objectReferenceValue, false, newName, $"{AssetDatabase.GetAssetPath(look)}/{field}", out var picked)) return;
            property.objectReferenceValue = picked;
            serialized.ApplyModifiedProperties();
        }

        static void SetLook(GameObject root, string prefabPath, IEnumerable<VoxPaint> paints, ModelLook look)
        {
            if (prefabPath == null)
            {
                foreach (var paint in paints) SetOne(paint, look, undo: true);
                return;
            }
            // 프리팹 애셋은 열어서 고친다(안의 모델은 중첩 프리팹이라 바꾼 값이 덮어쓰기로 저장됨). 모델은 경로(이름)로 다시 찾는다.
            var names = paints.Select(paint => PathOf(paint.transform, root.transform)).ToHashSet();
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            foreach (var paint in scope.prefabContentsRoot.GetComponentsInChildren<VoxPaint>(true))
                if (names.Contains(PathOf(paint.transform, scope.prefabContentsRoot.transform))) SetOne(paint, look, undo: false);
            users = null;
        }

        static void SetOne(VoxPaint paint, ModelLook look, bool undo)
        {
            var serialized = new SerializedObject(paint);
            serialized.FindProperty("look").objectReferenceValue = look;
            if (undo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
            SurfaceFinishMaterials.Apply(paint);
        }

        static string PathOf(Transform target, Transform root) =>
            target == root ? "" : PathOf(target.parent, root) + "/" + target.name;

        /// <summary>모델의 지금 값(투명도·발광 세기·재질감)으로 프리셋 애셋을 만든다.</summary>
        public static ModelLook Create(string name, VoxPaint from)
        {
            if (!AssetDatabase.IsValidFolder(ModelLook.Folder)) AssetDatabase.CreateFolder("Assets/Data", "Looks");
            var look = ScriptableObject.CreateInstance<ModelLook>();
            var source = new SerializedObject(from);
            look.transparency = source.FindProperty("transparency").floatValue;
            look.edgeTransparency = look.transparency;
            look.lightIntensity = source.FindProperty("lightIntensity").floatValue;
            look.lightRange = source.FindProperty("lightRange").floatValue;
            look.lightScale = source.FindProperty("lightScale").floatValue;
            look.lightShadows = (LightShadows)source.FindProperty("lightShadows").enumValueIndex;
            look.bodyFinish = (SurfaceFinish)source.FindProperty("bodyFinish").objectReferenceValue;
            look.valueFinish = (SurfaceFinish)source.FindProperty("valueFinish").objectReferenceValue;
            look.glassFinish = (SurfaceFinish)source.FindProperty("glassFinish").objectReferenceValue;
            look.lightFinish = (SurfaceFinish)source.FindProperty("lightFinish").objectReferenceValue;
            AssetDatabase.CreateAsset(look, AssetDatabase.GenerateUniqueAssetPath($"{ModelLook.Folder}/{name}.asset"));
            users = null;
            return look;
        }

        /// <summary>이 프리셋을 쓰는 프리팹 애셋 경로들.</summary>
        public static List<string> UsersOf(ModelLook look)
        {
            users ??= BuildUsers();
            return users.TryGetValue(look, out var list) ? list : new List<string>();
        }

        static Dictionary<ModelLook, List<string>> BuildUsers()
        {
            var result = new Dictionary<ModelLook, List<string>>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(dependency) != typeof(ModelLook)) continue;
                    var look = AssetDatabase.LoadAssetAtPath<ModelLook>(dependency);
                    if (!result.TryGetValue(look, out var list)) result[look] = list = new List<string>();
                    list.Add(path);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 프리팹 맨 위 오브젝트의 인스펙터 머리 부분에 '모델 겉모습' 칸(<see cref="ModelLookGUI"/>)을 그린다(컴포넌트를 더하지 않음).
    /// 프리팹 애셋(Project 창), 프리팹 편집 화면의 맨 위, 씬에 놓인 프리팹의 맨 위를 고르면 나온다. 씬에서 골랐으면 그 프리팹 애셋을 고친다(놓인 것 모두 바뀜).
    /// </summary>
    [InitializeOnLoad]
    static class ModelLookHeader
    {
        static ModelLookHeader() => Editor.finishedDefaultHeaderGUI += OnHeader;

        static void OnHeader(Editor editor)
        {
            if (editor.targets.Length != 1 || editor.target is not GameObject go) return;
            if (!TryGetPrefab(go, out string prefabPath)) return;
            if (go.GetComponentInChildren<VoxPaint>(true) == null) return;

            EditorGUILayout.Space(2f);
            string title = prefabPath != null && !EditorUtility.IsPersistent(go) ? $"모델 겉모습 (프리팹 '{Path.GetFileNameWithoutExtension(prefabPath)}'에 적용)" : "모델 겉모습";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            ModelLookGUI.Draw(go, prefabPath);
            EditorGUILayout.Space(2f);
        }

        // 칸을 보일 오브젝트: 프리팹 편집 화면의 맨 위(경로 없음, 바로 고침), 프리팹 애셋의 맨 위, 씬의 가장 바깥 프리팹 인스턴스 맨 위.
        // 가져온 모델(.bbmodel) 자체는 고칠 수 없어 뺀다.
        static bool TryGetPrefab(GameObject go, out string prefabPath)
        {
            prefabPath = null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot == go) return true;
            if (EditorUtility.IsPersistent(go))
            {
                prefabPath = AssetDatabase.GetAssetPath(go);
                return go.transform.parent == null && prefabPath.EndsWith(".prefab");
            }
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return false;
            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return prefabPath.EndsWith(".prefab");
        }
    }
}
