using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 상호작용 거리 안에서 값이 보이는 기물 하나(또는 이어진 방벽 묶음 하나): 경계 상자와 값.
    /// 묶음이면 판마다의 상자(<see cref="Parts"/>)도 갖고, 값 상자는 판들 위에서 캐릭터와 가장 가까운 점에 띄운다.
    /// </summary>
    public readonly struct NearbyValue
    {
        /// <summary>전체 경계 상자(기물 콜라이더 전체, 묶음이면 판 전부).</summary>
        public readonly Bounds Box;

        /// <summary>묶음이면 판마다의 상자, 기물 하나면 null.</summary>
        public readonly IReadOnlyList<Bounds> Parts;

        public readonly ColorValue Value;

        public NearbyValue(Bounds box, ColorValue value)
        {
            Box = box;
            Parts = null;
            Value = value;
        }

        public NearbyValue(IReadOnlyList<Bounds> parts, ColorValue value)
        {
            Box = parts[0];
            foreach (var part in parts) Box.Encapsulate(part);
            Parts = parts;
            Value = value;
        }

        /// <summary>
        /// 값 상자를 띄우는 자리. 기물 하나면 경계 상자 윗면 가운데. 묶음이면 판들 위에서 <paramref name="point"/>와 가장 가까운 점의
        /// 세로줄에 있는 판 중 가장 높은 윗면(옆면 벽은 윗가장자리를 따라, 바닥 판은 판 위).
        /// </summary>
        public Vector3 TopNear(Vector3 point)
        {
            if (Parts == null) return new(Box.center.x, Box.max.y, Box.center.z);
            Vector3 near = Box.center;
            float best = float.PositiveInfinity;
            foreach (var part in Parts)
            {
                var closest = part.ClosestPoint(point);
                float distance = (closest - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    near = closest;
                }
            }
            const float Margin = 0.001f;
            float top = near.y;
            foreach (var part in Parts)
                if (near.x >= part.min.x - Margin && near.x <= part.max.x + Margin && near.z >= part.min.z - Margin && near.z <= part.max.z + Margin)
                    top = Mathf.Max(top, part.max.y);
            return new(near.x, top, near.z);
        }

        /// <summary>점에서 경계 상자까지의 거리(안이면 0). 묶음이면 가장 가까운 판까지.</summary>
        public float DistanceFrom(Vector3 point)
        {
            if (Parts == null) return Mathf.Sqrt(Box.SqrDistance(point));
            float best = float.PositiveInfinity;
            foreach (var part in Parts) best = Mathf.Min(best, part.SqrDistance(point));
            return Mathf.Sqrt(best);
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
