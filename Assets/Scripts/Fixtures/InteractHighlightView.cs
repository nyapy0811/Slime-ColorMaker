using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 상호작용 강조의 겉모습. 상호작용할 수 있는 동안 늘 약하게 강조한다.
    /// 임시 효과: 렌더러의 자체 발광을 숨 쉬듯 약하게 올렸다 내린다(재질에 발광이 켜져 있어야 함).
    /// 강조 방법과 색이 정해지면 이 컴포넌트만 바꾼다. 켜고 끄는 것은 기물 기반이 한다.
    /// </summary>
    public class InteractHighlightView : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] renderers;
        [SerializeField] Color color = Color.white;
        [Tooltip("가장 밝을 때의 발광 세기")]
        [SerializeField] float maxIntensity = 0.25f;
        [Tooltip("한 번 숨 쉬는 시간(초)")]
        [SerializeField] float period = 2f;

        MaterialPropertyBlock block;

        public void SetOn(bool on)
        {
            enabled = on;
            if (!on) Apply(Color.black);
        }

        void Update()
        {
            float breath = (1f - Mathf.Cos(Time.time * 2f * Mathf.PI / period)) / 2f;
            Apply(color * (breath * maxIntensity));
        }

        void Apply(Color emission)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var target in renderers)
            {
                target.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                target.SetPropertyBlock(block);
            }
        }
    }
}
