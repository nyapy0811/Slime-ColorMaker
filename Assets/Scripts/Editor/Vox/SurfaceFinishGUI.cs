using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 재질감 고르기 한 줄(2026-11-14): [펼치기] 이름 | 재질감 애셋 칸 | 새로 만들기. 펼치면 그 애셋의 값(광택·반사·빛 맺힘·주변 반사)을 그 자리에서 고친다.
    /// 프리팹 맨 위 오브젝트의 '재질감' 칸(<see cref="SurfaceFinishHeader"/>)과 미끄러운 코팅 설정 인스펙터가 같이 쓴다.
    /// </summary>
    public static class SurfaceFinishGUI
    {
        public const string FinishFolder = "Assets/Data/Finishes";

        static readonly Dictionary<string, bool> open = new();

        /// <summary>
        /// 재질감 한 줄을 그린다. 고른 애셋이 바뀌면(새로 만들기 포함) true와 그 애셋. <paramref name="mixed"/>면 '여러 값'으로 보인다.
        /// <paramref name="newName"/>은 새로 만들 때의 파일 이름, <paramref name="key"/>는 펼침 상태를 기억하는 이름.
        /// </summary>
        public static bool Field(string label, SurfaceFinish current, bool mixed, string newName, string key, out SurfaceFinish picked)
        {
            picked = current;
            bool changed = false;
            var rect = EditorGUILayout.GetControlRect();
            const float buttonWidth = 72f;
            var foldRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var fieldRect = new Rect(foldRect.xMax, rect.y, rect.width - EditorGUIUtility.labelWidth - buttonWidth - 4f, rect.height);
            var buttonRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);

            bool isOpen = open.TryGetValue(key, out bool remembered) && remembered;
            using (new EditorGUI.DisabledScope(current == null || mixed))
                isOpen = EditorGUI.Foldout(foldRect, isOpen && current != null && !mixed, new GUIContent(label, "펼치면 이 재질감의 값을 여기서 고친다(같은 재질감을 쓰는 곳이 모두 바뀜)."), true);
            open[key] = isOpen;

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = mixed;
            var chosen = (SurfaceFinish)EditorGUI.ObjectField(fieldRect, mixed ? null : current, typeof(SurfaceFinish), false);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                picked = chosen;
                changed = true;
            }
            if (GUI.Button(buttonRect, new GUIContent("새로 만들기", $"{FinishFolder}에 지금 모습(광택 0.2, 반사 0, 둘 다 켬)의 재질감을 만들어 넣는다.")))
            {
                picked = Create(newName);
                open[key] = true;
                changed = true;
            }

            if (isOpen && current != null && !mixed)
            {
                var finish = new SerializedObject(current);
                finish.Update();
                EditorGUI.indentLevel++;
                foreach (var name in new[] { "smoothness", "metallic", "specularHighlights", "environmentReflections" })
                    EditorGUILayout.PropertyField(finish.FindProperty(name), new GUIContent(Label(name), finish.FindProperty(name).tooltip));
                EditorGUI.indentLevel--;
                finish.ApplyModifiedProperties();
            }
            return changed;
        }

        static string Label(string property) => property switch
        {
            "smoothness" => "광택",
            "metallic" => "반사",
            "specularHighlights" => "빛 맺힘",
            _ => "주변 반사",
        };

        static SurfaceFinish Create(string name)
        {
            if (!AssetDatabase.IsValidFolder(FinishFolder)) AssetDatabase.CreateFolder("Assets/Data", "Finishes");
            var finish = ScriptableObject.CreateInstance<SurfaceFinish>();
            AssetDatabase.CreateAsset(finish, AssetDatabase.GenerateUniqueAssetPath($"{FinishFolder}/{name}.asset"));
            return finish;
        }
    }

    /// <summary>
    /// 프리팹 맨 위 오브젝트의 인스펙터 머리 부분에 '재질감' 칸을 그린다(2026-11-14, 컴포넌트를 더하지 않음).
    /// 프리팹 애셋(Project 창), 프리팹 편집 화면의 맨 위, 씬에 놓인 프리팹의 맨 위를 고르면 나온다. 그 아래 모든 복셀 모델(<see cref="VoxPaint"/>)의
    /// 부위(일반·값 색·반투명) 중 실제로 있는 것만 한 줄씩 보이고, 고르면 모든 모델의 그 부위를 함께 바꾼다(컨베이어 무늬 12장 등).
    /// 씬에서 골랐으면 그 프리팹 애셋을 고친다(놓인 것 모두 바뀜). 모델마다 다르면 '여러 값'.
    /// </summary>
    [InitializeOnLoad]
    static class SurfaceFinishHeader
    {
        static readonly string[] PartLabels = { "일반", "값 색", "반투명" };
        static readonly string[] PartFields = { "bodyFinish", "valueFinish", "glassFinish" };
        static readonly string[] PartFileNames = { "Body", "Value", "Glass" };

        static SurfaceFinishHeader() => Editor.finishedDefaultHeaderGUI += OnHeader;

        static void OnHeader(Editor editor)
        {
            if (editor.targets.Length != 1 || editor.target is not GameObject go) return;
            if (!TryGetPrefab(go, out string prefabPath)) return;
            var paints = go.GetComponentsInChildren<VoxPaint>(true);
            if (paints.Length == 0) return;

            string prefabName = prefabPath != null ? System.IO.Path.GetFileNameWithoutExtension(prefabPath) : go.name;
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(prefabPath != null && !EditorUtility.IsPersistent(go) ? $"재질감 (프리팹 '{prefabName}'에 적용)" : "재질감", EditorStyles.boldLabel);
            for (int part = 0; part < PartFields.Length; part++)
            {
                var owners = paints.Where(paint => HasPart(paint, part)).ToList();
                if (owners.Count == 0) continue;
                var values = owners.Select(paint => (SurfaceFinish)new SerializedObject(paint).FindProperty(PartFields[part]).objectReferenceValue).Distinct().ToList();
                bool mixed = values.Count > 1;
                if (SurfaceFinishGUI.Field(PartLabels[part], values[0], mixed, $"{prefabName}{PartFileNames[part]}", $"{prefabName}/{part}", out var picked))
                    Set(go, prefabPath, part, picked);
            }
            EditorGUILayout.Space(2f);
        }

        // 재질감 칸을 보일 오브젝트: 프리팹 편집 화면의 맨 위(경로 없음, 바로 고침), 프리팹 애셋의 맨 위, 씬의 가장 바깥 프리팹 인스턴스 맨 위.
        // 가져온 모델(.vox) 자체는 고칠 수 없어 뺀다.
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

        static bool HasPart(VoxPaint paint, int part) =>
            paint.GetComponentsInChildren<Renderer>(true).Any(renderer => PartOf(renderer) == part);

        static int PartOf(Renderer renderer) => renderer.name switch
        {
            VoxPaint.ValuePartName => 1,
            VoxPaint.GlassPartName or VoxPaint.GlassValuePartName => 2,
            _ => 0,
        };

        static void Set(GameObject go, string prefabPath, int part, SurfaceFinish finish)
        {
            if (prefabPath == null)
            {
                foreach (var paint in go.GetComponentsInChildren<VoxPaint>(true)) SetOne(paint, part, finish, undo: true);
                return;
            }
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            foreach (var paint in scope.prefabContentsRoot.GetComponentsInChildren<VoxPaint>(true)) SetOne(paint, part, finish, undo: false);
        }

        static void SetOne(VoxPaint paint, int part, SurfaceFinish finish, bool undo)
        {
            if (!HasPart(paint, part)) return;
            var serialized = new SerializedObject(paint);
            serialized.FindProperty(PartFields[part]).objectReferenceValue = finish;
            if (undo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
            SurfaceFinishMaterials.Apply(paint);
        }
    }
}
