using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// Blockbench 모델(.bbmodel)의 칠하기, 반투명 정도, 발광 세기. 가져오기 코드가 모든 모델의 맨 위 오브젝트에 붙인다
    /// (Docs/v2 블록벤치 모델링 규칙 3절: Color = ValueColor, Glass = GlassValueColor, GrayGlass = Glass, Light = Light 렌더러).
    /// 칠할 색은 값 색(ValueColor)과 반투명 값 색(GlassValueColor) 렌더러에, 반투명 정도는 두 반투명 렌더러(Glass, GlassValueColor)에
    /// <c>_BaseColor</c>로 준다(밝기 텍스처와 곱해짐). Play 모드가 아니어도 인스펙터에서 바꾸면 바로 보인다.
    /// 이 모델을 칠하는 곳은 여기 하나로 둔다: 기물·캐릭터 코드는 렌더러를 직접 칠하지 않고 <see cref="Color"/>를 바꾼다.
    /// 부위(일반·값 색·반투명)마다 재질감(<see cref="SurfaceFinish"/>, 광택·반사)을 고른다: 에디터가 렌더러에 '원래 재질 + 재질감' 재질을 끼워
    /// 프리팹에 저장하므로 실행 중에는 할 일이 없다. 비우면 가져온 재질 그대로(광택 0.2, 반사 0, 빛 맺힘·주변 반사 켬)다.
    /// 발광 부분이 있으면 가져오기 코드가 만든 광원(자식 Light)도 여기서 맞춘다: 색 = 칠하는 색, 세기 = 발광 세기 × 광원 세기 배율, 범위·그림자.
    /// 기물이 <see cref="Glowing"/>을 끄면 발광과 광원이 모두 꺼진다(흡수한 색 원천).
    /// 겉모습 프리셋(<see cref="ModelLook"/>)을 고르면 반투명 정도·발광 세기·재질감은 자기 값 대신 프리셋 값을 쓴다(비우면 자기 값).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VoxPaint : MonoBehaviour
    {
        public const string ValuePartName = "ValueColor";
        public const string GlassPartName = "Glass";
        public const string GlassValuePartName = "GlassValueColor";
        public const string LightPartName = "Light";

        /// <summary>처음 반투명 정도(불투명도). 반투명 재질의 기본값도 이 값이다.</summary>
        public const float DefaultOpacity = 0.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("값 색(33~40)과 반투명 값 색(41~48) 부분을 칠하는 색. 흰색이면 칠하지 않은 것과 같다(알파는 쓰지 않음).")]
        [SerializeField] Color color = Color.white;

        [Tooltip("겉모습 프리셋(Assets/Data/Looks). 고르면 아래 반투명 정도·발광·광원·재질감 대신 프리셋 값을 쓴다. 비우면 자기 값.")]
        [SerializeField] ModelLook look;

        [Tooltip("반투명 부분(41~56)의 불투명도. 0이면 안 보이고 1이면 불투명하다.")]
        [Range(0f, 1f)]
        [SerializeField] float opacity = DefaultOpacity;

        [Tooltip("발광 부분(Blockbench Light, 렌더러 Light)의 발광 세기. 발광 = 칠하는 색 × 휘도 × 이 값. 0이면 빛나지 않는다.")]
        [Min(0f)]
        [SerializeField] float lightIntensity = 1f;

        [Tooltip("광원(발광 부분마다 하나, 포인트 라이트)이 닿는 거리(칸). 이 거리에서 0이 된다.")]
        [Min(0.01f)]
        [SerializeField] float lightRange = 3f;

        [Tooltip("광원 세기 = 발광 세기 × 이 값. 0이면 광원을 끈다(발광·번짐은 그대로).")]
        [Min(0f)]
        [SerializeField] float lightScale = 0.5f;

        [Tooltip("광원 그림자. 없음이 가장 가볍다(빛이 블록을 뚫고 닿음). 켜면 광원마다 그림자를 그려 무거워진다.")]
        [SerializeField] LightShadows lightShadows = LightShadows.None;

        [Header("재질감 (비우면 광택 0.2 · 반사 0 · 빛 맺힘 · 주변 반사 켬)")]
        [Tooltip("일반 칸(값 색·반투명이 아닌 칸)의 재질감. 메뉴 Create > Slime~ColorMaker~ > 재질감으로 만든 애셋(Assets/Data/Finishes).")]
        [SerializeField] SurfaceFinish bodyFinish;

        [Tooltip("칠하는 값 색 칸(33~40, 렌더러 ValueColor)의 재질감.")]
        [SerializeField] SurfaceFinish valueFinish;

        [Tooltip("반투명 칸(41~56, 렌더러 Glass·GlassValueColor)의 재질감.")]
        [SerializeField] SurfaceFinish glassFinish;

        [Tooltip("발광 부분(렌더러 Light)의 재질감.")]
        [SerializeField] SurfaceFinish lightFinish;

        MaterialPropertyBlock block;
        bool glowing = true;
        float fade = 1f;

#if UNITY_EDITOR
        /// <summary>인스펙터에서 값이 바뀌었다(에디터가 고른 재질감대로 렌더러 재질을 끼운다).</summary>
        public static event Action<VoxPaint> FinishesChanged;
#endif

        /// <summary>렌더러가 속한 부위의 재질감(이름으로 부위를 가림: ValueColor, Glass·GlassValueColor, 나머지는 일반).</summary>
        public SurfaceFinish FinishFor(Renderer target) => target.name switch
        {
            ValuePartName => look != null ? look.valueFinish : valueFinish,
            GlassPartName or GlassValuePartName => look != null ? look.glassFinish : glassFinish,
            LightPartName => look != null ? look.lightFinish : lightFinish,
            _ => look != null ? look.bodyFinish : bodyFinish,
        };

        /// <summary>겉모습 프리셋. 비었으면 자기 값을 쓴다.</summary>
        public ModelLook Look
        {
            get => look;
            set
            {
                look = value;
                Apply();
            }
        }

        public Color Color
        {
            get => color;
            set
            {
                color = value;
                Apply();
            }
        }

        /// <summary>쓰는 반투명 정도(프리셋이 있으면 프리셋 값).</summary>
        public float Opacity => look != null ? look.opacity : opacity;

        /// <summary>쓰는 발광 세기(프리셋이 있으면 프리셋 값).</summary>
        public float LightIntensity => look != null ? look.lightIntensity : lightIntensity;

        /// <summary>반투명 정도에 곱하는 흐려짐(저장하지 않음, 처음은 1). 기물 연출이 바꾼다(방벽이 열리며 사라짐).</summary>
        public float Fade
        {
            get => fade;
            set
            {
                fade = Mathf.Clamp01(value);
                Apply();
            }
        }

        /// <summary>발광 부분과 광원이 켜져 있는가(저장하지 않음, 처음은 켬). 기물이 상태에 따라 끈다.</summary>
        public bool Glowing
        {
            get => glowing;
            set
            {
                glowing = value;
                Apply();
            }
        }

        void OnEnable()
        {
            ModelLook.Changed += OnLookChanged;
            Apply();
        }

        void OnDisable() => ModelLook.Changed -= OnLookChanged;

        void OnLookChanged(ModelLook changed)
        {
            if (changed == look) Apply();
        }

        void OnValidate()
        {
            Apply();
#if UNITY_EDITOR
            FinishesChanged?.Invoke(this);
#endif
        }

        /// <summary>칠할 색, 반투명 정도, 발광을 렌더러와 광원에 다시 준다.</summary>
        public void Apply()
        {
            block ??= new MaterialPropertyBlock();
            var paint = new Color(color.r, color.g, color.b, 1f);
            float glow = glowing ? LightIntensity : 0f;
            float alpha = Opacity * fade;
            foreach (var target in GetComponentsInChildren<Renderer>(true))
            {
                Color? value = target.name switch
                {
                    ValuePartName or LightPartName => paint,
                    GlassValuePartName => new Color(paint.r, paint.g, paint.b, alpha),
                    GlassPartName => new Color(1f, 1f, 1f, alpha),
                    _ => null,
                };
                if (value == null) continue;
                target.GetPropertyBlock(block);
                block.SetColor(BaseColorId, value.Value);
                if (target.name == LightPartName) block.SetColor(EmissionColorId, paint * glow);
                target.SetPropertyBlock(block);
            }

            foreach (var source in GetComponentsInChildren<Light>(true))
            {
                float intensity = glow * (look != null ? look.lightScale : lightScale);
                source.enabled = intensity > 0f;
                source.color = paint;
                source.intensity = intensity;
                source.range = look != null ? look.lightRange : lightRange;
                source.shadows = look != null ? look.lightShadows : lightShadows;
            }
        }
    }
}
