using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 레이저 벽의 겉모습(임시): 지나갈 수 있으면 레이저 면이 흐려지고, 막으면 다시 짙어진다(v1 필터의 채움 투명도처럼).
    /// 레이저 면의 재질은 투명 재질이고, 짙을 때는 재질 색을 그대로 쓴다. 레이저 색과 미리 보이는 모양은 미정이다.
    /// </summary>
    public class LaserWallView : PassableView
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform beam;

        [Tooltip("지나갈 수 있을 때 레이저 면의 불투명도")]
        [SerializeField, Range(0f, 1f)] float passableAlpha = 0.12f;

        [Tooltip("다 흐려지거나 짙어지는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.4f;

        Renderer beamRenderer;
        MaterialPropertyBlock block;
        Color baseColor;
        float passable;
        float target;

        void Awake()
        {
            beamRenderer = beam.GetComponent<Renderer>();
            baseColor = beamRenderer.sharedMaterial.GetColor(BaseColorId);
            block = new MaterialPropertyBlock();
            enabled = false;
        }

        public override void SetPassable(bool value)
        {
            target = value ? 1f : 0f;
            enabled = true;
        }

        void Update()
        {
            passable = Mathf.MoveTowards(passable, target, Time.deltaTime / duration);
            var color = baseColor;
            color.a = Mathf.Lerp(baseColor.a, passableAlpha, passable);
            beamRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            beamRenderer.SetPropertyBlock(block);
            if (passable == target) enabled = false;
        }
    }
}
