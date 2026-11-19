using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 복셀 모델(<see cref="VoxPaint"/>) 인스펙터. 겉모습 프리셋을 고르면 그 값(이 모델에 있는 부위의 것만)을 그 자리에서 고치고,
    /// 자기 값(반투명 정도·발광·광원·재질감)은 흐리게 접어 둔다(프리셋을 비우면 다시 씀).
    /// </summary>
    [CustomEditor(typeof(VoxPaint))]
    [CanEditMultipleObjects]
    class VoxPaintEditor : Editor
    {
        static readonly string[] OwnFields = { "opacity", "lightIntensity", "lightRange", "lightScale", "lightShadows", "bodyFinish", "valueFinish", "glassFinish", "lightFinish" };
        static bool ownOpen;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));

            var look = serializedObject.FindProperty("look");
            var rect = EditorGUILayout.GetControlRect();
            const float buttonWidth = 72f;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - buttonWidth - 4f, rect.height), look, new GUIContent("프리셋", look.tooltip));
            using (new EditorGUI.DisabledScope(targets.Length != 1))
                if (GUI.Button(new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height), new GUIContent("새로 만들기", $"{ModelLook.Folder}에 이 모델의 지금 값으로 프리셋을 만들어 넣는다.")))
                    look.objectReferenceValue = ModelLookGUI.Create(target.name, (VoxPaint)target);

            if (look.hasMultipleDifferentValues || look.objectReferenceValue == null)
            {
                foreach (var name in OwnFields) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            }
            else
            {
                ModelLookGUI.DrawValues((ModelLook)look.objectReferenceValue, System.Array.ConvertAll(targets, t => (VoxPaint)t));
                ownOpen = EditorGUILayout.Foldout(ownOpen, "자기 값 (프리셋을 비우면 씀)", true);
                if (ownOpen)
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.indentLevel++;
                        foreach (var name in OwnFields) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
                        EditorGUI.indentLevel--;
                    }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
