using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>상호작용 거리 안에서 값이 보이는 기물 하나: 그 기물 콜라이더 윗면 가운데와 값.</summary>
    public readonly struct NearbyValue
    {
        public readonly Vector3 Top;
        public readonly ColorValue Value;

        public NearbyValue(Vector3 top, ColorValue value)
        {
            Top = top;
            Value = value;
        }
    }

    /// <summary>
    /// 가까이 있어 보일 값들이 바뀜(기물이 범위에 들어오거나 나감, 값이 없어지거나 바뀜). 비어 있으면 아무것도 안 보임.
    /// 보내는 곳: 닿음 상호작용 컨트롤러. 받는 곳: 가까운 값 표시. 목록은 보내는 쪽이 재사용하므로 받는 쪽은 복사해 둔다.
    /// </summary>
    public readonly struct NearbyValuesChanged : IEvent
    {
        public readonly IReadOnlyList<NearbyValue> Values;

        public NearbyValuesChanged(IReadOnlyList<NearbyValue> values) => Values = values;
    }
}
