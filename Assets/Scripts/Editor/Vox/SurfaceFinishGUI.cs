using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 재질감 고르기 한 줄: [펼치기] 이름 | 재질감 애셋 칸 | 새로 만들기. 펼치면 그 애셋의 값(광택·반사·빛 맺힘·주변 반사)을 그 자리에서 고친다.
    /// 모델 겉모습 프리셋 칸(<see cref="ModelLookGUI"/>)과 미끄러운 코팅 설정 인스펙터가 같이 쓴다.
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
}
