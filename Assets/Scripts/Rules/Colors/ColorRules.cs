using System;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 색 값의 규칙. 채널 한도는 채널마다 따로인 값(<see cref="ColorValue"/>)을 인자로 받는다(쓰는 곳은 고정 한도 <see cref="Limit"/>를 넘김).
    /// </summary>
    public static class ColorRules
    {
        /// <summary>채널의 가장 큰 값. 모든 채널, 모든 맵이 같고 바뀌지 않는다(2026-11-08 색 시스템 수정: 0~5에서 0~15로).</summary>
        public const int ChannelMax = 15;

        /// <summary>채널 한도 (15, 15, 15). 넘치면 순환하지 않고 여기서 자른다.</summary>
        public static readonly ColorValue Limit = new(ChannelMax, ChannelMax, ChannelMax);

        /// <summary>모든 채널이 0 ~ 그 채널의 한도인가.</summary>
        public static bool IsWithin(ColorValue value, ColorValue limit) =>
            value.R >= 0 && value.R <= limit.R &&
            value.G >= 0 && value.G <= limit.G &&
            value.B >= 0 && value.B <= limit.B;

        /// <summary>채널마다 0 ~ 그 채널의 한도로 자른다(음수는 0, 넘치면 한도). 흡수로 넘친 값은 사라진다.</summary>
        public static ColorValue Clamp(ColorValue value, ColorValue limit) => new(
            Math.Clamp(value.R, 0, limit.R),
            Math.Clamp(value.G, 0, limit.G),
            Math.Clamp(value.B, 0, limit.B));

        /// <summary>모든 채널이 cost의 그 채널 이상인가(레이저 벽을 지날 수 있는가).</summary>
        public static bool IsAtLeast(ColorValue value, ColorValue cost) =>
            value.R >= cost.R && value.G >= cost.G && value.B >= cost.B;

        /// <summary>문·목적지의 요구 색으로 쓸 수 있는가: 모든 채널 1 이상, 세 채널이 모두 같지 않음, 한도 안(지금 색이 닿을 수 있음).</summary>
        public static bool IsValidRequired(ColorValue value, ColorValue limit) =>
            value.R >= 1 && value.G >= 1 && value.B >= 1 && !IsAllEqual(value) && IsWithin(value, limit);

        /// <summary>색 원천의 값으로 쓸 수 있는가: 음수가 없고, (0,0,0)을 포함해 세 채널이 모두 같지 않음.</summary>
        public static bool IsValidSourceValue(ColorValue value) =>
            value.R >= 0 && value.G >= 0 && value.B >= 0 && !IsAllEqual(value);

        /// <summary>
        /// 값의 색. 채널마다 값 ÷ <see cref="ChannelMax"/>를 그대로 밝기로 쓴다(2026-11-08, 정규화 RGB에서 바뀜):
        /// (15,0,0)은 밝은 빨강, (3,0,0)은 어두운 빨강, (15,15,15)는 흰색, (0,0,0)은 검정. 한도를 넘는 값은 1로 자른다.
        /// </summary>
        public static Color ToColor(ColorValue value) => new(
            Mathf.Clamp01((float)value.R / ChannelMax),
            Mathf.Clamp01((float)value.G / ChannelMax),
            Mathf.Clamp01((float)value.B / ChannelMax));

        /// <summary>
        /// 흑백 색(흡수한 색 원천). 채도만 0으로 내리고 밝기는 세 채널의 평균이라,
        /// 순수한 빨강·초록·파랑이 모두 같은 회색이 된다(밝기 가중이 아님, 코어 루프 4단계).
        /// </summary>
        public static Color ToGrayscale(Color color)
        {
            float gray = (color.r + color.g + color.b) / 3f;
            return new Color(gray, gray, gray, color.a);
        }

        static bool IsAllEqual(ColorValue value) => value.R == value.G && value.G == value.B;
    }
}
