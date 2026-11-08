using UnityEditor;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 므엥 출렁임 설정의 Inspector(2026-11-08): 값마다 아래에 그 값의 설명(필드의 Tooltip)을 늘 보인다.
    /// Play 모드에서 보면서 고칠 때 마우스를 올리지 않아도 역할을 알 수 있게 한다.
    /// </summary>
    [CustomEditor(typeof(PlayerSquashSettings))]
    public class PlayerSquashSettingsEditor : Editor
    {
        static GUIStyle descriptionStyle;

        public override void OnInspectorGUI()
        {
            descriptionStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                if (property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
                if (string.IsNullOrEmpty(property.tooltip)) continue;
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(property.tooltip, descriptionStyle);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
