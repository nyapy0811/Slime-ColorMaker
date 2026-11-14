using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 미끄러운 코팅 설정 인스펙터(2026-11-14): 종류(기름·얼음)마다 관성 시간, 재질, 재질감 한 줄(애셋 칸 + 새로 만들기 + 펼쳐서 값 고치기, <see cref="SurfaceFinishGUI"/>).
    /// </summary>
    [CustomEditor(typeof(SlipperyCoatingSettings))]
    class SlipperyCoatingSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Kind("oil", "기름", "CoatingOil");
            EditorGUILayout.Space();
            Kind("ice", "얼음", "CoatingIce");
            serializedObject.ApplyModifiedProperties();
        }

        void Kind(string property, string label, string fileName)
        {
            var kind = serializedObject.FindProperty(property);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(kind.FindPropertyRelative("inertiaTime"), new GUIContent("관성 시간", kind.FindPropertyRelative("inertiaTime").tooltip));
            EditorGUILayout.PropertyField(kind.FindPropertyRelative("material"), new GUIContent("재질", kind.FindPropertyRelative("material").tooltip));
            var finish = kind.FindPropertyRelative("finish");
            if (SurfaceFinishGUI.Field("재질감", (SurfaceFinish)finish.objectReferenceValue, false, fileName, $"coating/{property}", out var picked))
                finish.objectReferenceValue = picked;
            EditorGUI.indentLevel--;
        }
    }
}
