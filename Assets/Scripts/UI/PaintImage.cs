using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// UI 픽셀아트(.pxo) 그림을 그리는 이미지. 재질이 UI Paint 셰이더(<see cref="MaterialPath"/>)라
    /// 그림의 Color 부분에만 이미지 색(<see cref="Graphic.color"/>)이 곱해지고 Gray 부분은 회색 그대로다(Docs/v2 UI 픽셀아트 규칙 2절).
    /// 이미지 색의 알파는 그림 전체에 곱한다. 9-슬라이스, 버튼 상태 스프라이트 바꾸기 등은 보통의 Image와 같다.
    /// 붙이거나 재질이 비면 에디터가 공용 재질을 넣는다(씬·프리팹에 저장되므로 빌드에도 들어감).
    /// </summary>
    [AddComponentMenu("UI/Paint Image (Slime~ColorMaker~)")]
    public class PaintImage : Image
    {
        /// <summary>UI Paint 셰이더를 쓰는 공용 재질.</summary>
        public const string MaterialPath = "Assets/Material/UIPaint.mat";

#if UNITY_EDITOR
        const string ShaderPath = "Assets/Shaders/UIPaint.shader";

        protected override void Reset()
        {
            base.Reset();
            AssignMaterial();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_Material == null) EditorApplication.delayCall += () => { if (this != null) AssignMaterial(); };
        }

        void AssignMaterial()
        {
            if (m_Material != null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                if (shader == null) return;
                material = new Material(shader) { name = "UIPaint" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Undo.RecordObject(this, "UI Paint 재질");
            this.material = material;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
