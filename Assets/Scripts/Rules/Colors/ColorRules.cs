using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 색 값의 규칙. 채널 한도는 채널마다 따로인 값(<see cref="ColorValue"/>)을 인자로 받는다(쓰는 곳은 고정 한도 <see cref="Limit"/>를 넘김).
    /// </summary>
    public static class ColorRules
    {
        /// <summary>채널의 가장 큰 값. 모든 채널, 모든 맵이 같고 바뀌지 않는다.</summary>
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
        /// 값의 색. 채널마다 값 ÷ 가장 큰 채널 값이라, 그 비율에서 가장 밝은 색이 된다:
        /// (3,0,0)과 (15,0,0)은 같은 빨강, (6,3,3)은 (1, 0.5, 0.5), 세 채널이 같으면 흰색, (0,0,0)은 검정.
        /// </summary>
        public static Color ToColor(ColorValue value)
        {
            int max = Mathf.Max(value.R, Mathf.Max(value.G, value.B));
            if (max <= 0) return Color.black;
            return new Color(Mathf.Clamp01((float)value.R / max), Mathf.Clamp01((float)value.G / max), Mathf.Clamp01((float)value.B / max));
        }

        /// <summary>
        /// 흑백 색(흡수한 색 원천). 채도만 0으로 내리고 밝기는 세 채널의 평균이라,
        /// 순수한 빨강·초록·파랑이 모두 같은 회색이 된다(밝기 가중이 아님).
        /// </summary>
        public static Color ToGrayscale(Color color)
        {
            float gray = (color.r + color.g + color.b) / 3f;
            return new Color(gray, gray, gray, color.a);
        }

        static bool IsAllEqual(ColorValue value) => value.R == value.G && value.G == value.B;
    }
}
