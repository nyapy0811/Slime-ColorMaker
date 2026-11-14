using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 복셀 모델(.vox)의 칠하기와 반투명 정도. 가져오기 코드가 모든 모델의 맨 위 오브젝트에 붙인다(Docs/v2 복셀 모델링 규칙 3절).
    /// 칠할 색은 값 색(ValueColor)과 반투명 값 색(GlassValueColor) 렌더러에, 반투명 정도는 두 반투명 렌더러(Glass, GlassValueColor)에
    /// <c>_BaseColor</c>로 준다(밝기 텍스처와 곱해짐). Play 모드가 아니어도 인스펙터에서 바꾸면 바로 보인다.
    /// 이 모델을 칠하는 곳은 여기 하나로 둔다: 기물·캐릭터 코드는 렌더러를 직접 칠하지 않고 <see cref="Color"/>를 바꾼다.
    /// 부위(일반·값 색·반투명)마다 재질감(<see cref="SurfaceFinish"/>, 광택·반사)을 고른다(2026-11-14): 에디터가 렌더러에 '원래 재질 + 재질감' 재질을 끼워
    /// 프리팹에 저장하므로 실행 중에는 할 일이 없다. 비우면 가져온 재질 그대로(광택 0.2, 반사 0, 빛 맺힘·주변 반사 켬)다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VoxPaint : MonoBehaviour
    {
        public const string ValuePartName = "ValueColor";
        public const string GlassPartName = "Glass";
        public const string GlassValuePartName = "GlassValueColor";

        /// <summary>처음 반투명 정도(불투명도). 반투명 재질의 기본값도 이 값이다.</summary>
        public const float DefaultOpacity = 0.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("값 색(33~40)과 반투명 값 색(41~48) 부분을 칠하는 색. 흰색이면 칠하지 않은 것과 같다(알파는 쓰지 않음).")]
        [SerializeField] Color color = Color.white;

        [Tooltip("반투명 부분(41~56)의 불투명도. 0이면 안 보이고 1이면 불투명하다.")]
        [Range(0f, 1f)]
        [SerializeField] float opacity = DefaultOpacity;

        [Header("재질감 (비우면 광택 0.2 · 반사 0 · 빛 맺힘 · 주변 반사 켬)")]
        [Tooltip("일반 칸(값 색·반투명이 아닌 칸)의 재질감. 메뉴 Create > Slime~ColorMaker~ > 재질감으로 만든 애셋(Assets/Data/Finishes).")]
        [SerializeField] SurfaceFinish bodyFinish;

        [Tooltip("칠하는 값 색 칸(33~40, 렌더러 ValueColor)의 재질감.")]
        [SerializeField] SurfaceFinish valueFinish;

        [Tooltip("반투명 칸(41~56, 렌더러 Glass·GlassValueColor)의 재질감.")]
        [SerializeField] SurfaceFinish glassFinish;

        MaterialPropertyBlock block;

#if UNITY_EDITOR
        /// <summary>인스펙터에서 값이 바뀌었다(에디터가 고른 재질감대로 렌더러 재질을 끼운다).</summary>
        public static event Action<VoxPaint> FinishesChanged;
#endif

        /// <summary>렌더러가 속한 부위의 재질감(이름으로 부위를 가림: ValueColor, Glass·GlassValueColor, 나머지는 일반).</summary>
        public SurfaceFinish FinishFor(Renderer target) => target.name switch
        {
            ValuePartName => valueFinish,
            GlassPartName or GlassValuePartName => glassFinish,
            _ => bodyFinish,
        };

        public Color Color
        {
            get => color;
            set
            {
                color = value;
                Apply();
            }
        }

        public float Opacity
        {
            get => opacity;
            set
            {
                opacity = Mathf.Clamp01(value);
                Apply();
            }
        }

        void OnEnable() => Apply();

        void OnValidate()
        {
            Apply();
#if UNITY_EDITOR
            FinishesChanged?.Invoke(this);
#endif
        }

        /// <summary>칠할 색과 반투명 정도를 렌더러에 다시 준다.</summary>
        public void Apply()
        {
            block ??= new MaterialPropertyBlock();
            var paint = new Color(color.r, color.g, color.b, 1f);
            foreach (var target in GetComponentsInChildren<Renderer>(true))
            {
                Color? value = target.name switch
                {
                    ValuePartName => paint,
                    GlassValuePartName => new Color(paint.r, paint.g, paint.b, opacity),
                    GlassPartName => new Color(1f, 1f, 1f, opacity),
                    _ => null,
                };
                if (value == null) continue;
                target.GetPropertyBlock(block);
                block.SetColor(BaseColorId, value.Value);
                target.SetPropertyBlock(block);
            }
        }
    }
}
