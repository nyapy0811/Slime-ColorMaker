using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>컨베이어 벨트 모양(2026-11-14). 꺾임은 모델 기준 −X 쪽(오른쪽 꺾임) 또는 +X 쪽(왼쪽 꺾임)에서 들어와 −Z로 나간다.</summary>
    public enum ConveyorShape { Straight, Right, Left }

    /// <summary>한 칸의 벨트: 나가는 방향(바닥 평면 x, z의 4방향 단위, 벨트가 없으면 0)과 모양.</summary>
    public readonly struct ConveyorCell
    {
        public readonly Vector2Int flow;
        public readonly ConveyorShape shape;

        public ConveyorCell(Vector2Int flow, ConveyorShape shape)
        {
            this.flow = flow;
            this.shape = shape;
        }

        public bool Exists => flow != Vector2Int.zero;
    }

    /// <summary>
    /// 벨트 관성(2026-11-14). 캐릭터 중심점이 벨트 구역을 벗어나는 순간 벨트가 밀던 속도로 그 방향을 따라 똑바로 <see cref="Distance"/>만큼 가며,
    /// 일정하게 느려져 그 거리를 다 가는 순간 멈춘다(걸리는 시간 = 2 × 거리 ÷ 처음 빠르기). 플레이어 조작과 따로 더한다.
    /// </summary>
    public struct ConveyorInertia
    {
        /// <summary>관성으로 가는 거리(칸).</summary>
        public const float Distance = 0.5f;

        public Vector2 direction; // 단위 방향
        public float speed; // 지금 빠르기(칸/초)
        public float deceleration; // 느려지는 정도(칸/초²)

        public bool Active => speed > 0f;

        /// <summary>벗어나는 순간 벨트가 밀던 속도 <paramref name="velocity"/>(칸/초)로 시작한다. 0이면 관성 없음.</summary>
        public static ConveyorInertia Start(Vector2 velocity)
        {
            float speed = velocity.magnitude;
            if (speed <= 0f) return default;
            return new ConveyorInertia { direction = velocity / speed, speed = speed, deceleration = speed * speed / (2f * Distance) };
        }

        /// <summary><paramref name="deltaTime"/>초 동안 가는 이동. 다 가면 멈춘다.</summary>
        public Vector2 Step(float deltaTime)
        {
            if (speed <= 0f || deltaTime <= 0f) return Vector2.zero;
            float left = speed / deceleration; // 멈출 때까지 남은 시간
            if (deltaTime >= left)
            {
                var rest = direction * (speed * speed / (2f * deceleration));
                speed = 0f;
                return rest;
            }
            var step = direction * (speed * deltaTime - 0.5f * deceleration * deltaTime * deltaTime);
            speed -= deceleration * deltaTime;
            return step;
        }
    }

    /// <summary>
    /// 컨베이어 벨트가 미는 규칙(2026-11-13, 꺾임 벨트 2026-11-14). 바닥 평면(x, z)을 <see cref="Vector2"/>·<see cref="Vector2Int"/>(x, y)로 쓰고,
    /// 칸 (x, z)의 가운데는 (x, z)다. 거리는 칸(= 유닛) 단위다.
    /// 직선 벨트는 흐르는 방향으로 똑바로 민다. 꺾임 벨트는 안쪽 모서리(들어오는 면과 나가는 면이 만나는 꼭짓점)를 중심으로 칸 전체를 같은 각도로 돌린다(돌림판).
    /// 직선 칸 하나를 지나는 거리(1칸)만큼 밀 때 꺾임 칸은 90° 돌아, 어느 반지름에서든 꺾임 칸을 지나는 시간이 직선 칸과 같다.
    /// 꺾임 칸의 '진행'은 들어오는 면에서 0, 나가는 면에서 1(= 각도 ÷ 90°)이고, 직선 칸의 진행(들어오는 면에서 거리)과 같은 단위다.
    /// 벨트 구역(2026-11-14, 모델의 난간 복셀과 같음): 직선은 폭 16칸 중 가운데 14칸, 꺾임은 반지름 1/16 이상 15/16 미만의 띠. 나머지(난간)는 밀지 않는다.
    /// 꺾임 칸은 진행 × 반지름(0~1)을 16 × 16 벨트 자리로 나누고(바닥 흔적 픽셀 자리), 구역 안 자리(반지름 1~14번)만 쓴다.
    /// 캐릭터는 중심점이 든 칸의 벨트 구역 안에 있을 때 그 벨트가 민다. 서로 마주 흐르는 두 직선 벨트에서는 둘의 경계를 넘어 밀지 않고,
    /// 경계에 닿으면 벨트 방향으로는 움직이지 못한다(직각으로만).
    /// </summary>
    public static class ConveyorRules
    {
        /// <summary>꺾임 칸의 벨트 자리 수(진행·반지름 각각). 바닥 흔적 픽셀(1/16유닛)과 같다.</summary>
        public const int Slots = 16;

        const float QuarterTurn = Mathf.PI / 2f;

        // 한 번에 지나는 칸 수의 상한(서로 마주 흐르는 벨트 사이에서 멈추게).
        const int MaxCells = 8;

        // 난간 폭(칸).
        const float RailWidth = 1f / Slots;

        // 마주 보는 벨트의 경계에 닿았다고 보는 거리(칸).
        const float PinTolerance = 0.01f;

        /// <summary>점이 든 칸.</summary>
        public static Vector2Int CellOf(Vector2 point) => new(Mathf.FloorToInt(point.x + 0.5f), Mathf.FloorToInt(point.y + 0.5f));

        /// <summary>벨트가 들어오는 면(칸 가운데에서 그 면 쪽 4방향 단위). 직선은 흐름 반대쪽, 오른쪽 꺾임은 흐름을 시계 방향으로, 왼쪽 꺾임은 반시계 방향으로 90° 돌린 쪽.</summary>
        public static Vector2Int EntrySide(ConveyorCell belt) => belt.shape switch
        {
            ConveyorShape.Right => new Vector2Int(belt.flow.y, -belt.flow.x),
            ConveyorShape.Left => new Vector2Int(-belt.flow.y, belt.flow.x),
            _ => -belt.flow,
        };

        /// <summary>꺾임 칸의 안쪽 모서리(돌리는 중심).</summary>
        public static Vector2 InnerCorner(Vector2Int cell, ConveyorCell belt) => cell + (Vector2)(EntrySide(belt) + belt.flow) * 0.5f;

        /// <summary>꺾임 칸에서 점의 (진행, 반지름). 진행은 들어오는 면 0 ~ 나가는 면 1.</summary>
        public static Vector2 ToCurve(Vector2Int cell, ConveyorCell belt, Vector2 point)
        {
            var offset = point - InnerCorner(cell, belt);
            float angle = Mathf.Atan2(Vector2.Dot(offset, -(Vector2)EntrySide(belt)), Vector2.Dot(offset, -(Vector2)belt.flow));
            return new Vector2(angle / QuarterTurn, offset.magnitude);
        }

        /// <summary>꺾임 칸에서 (진행, 반지름)의 점.</summary>
        public static Vector2 FromCurve(Vector2Int cell, ConveyorCell belt, float progress, float radius)
        {
            float angle = progress * QuarterTurn;
            return InnerCorner(cell, belt) + radius * (Mathf.Cos(angle) * -(Vector2)belt.flow + Mathf.Sin(angle) * -(Vector2)EntrySide(belt));
        }

        /// <summary>점이 칸 <paramref name="cell"/>의 벨트 구역 안인가(직선은 가운데 14칸, 꺾임은 반지름 1/16 이상 15/16 미만). 벨트가 없으면 false.</summary>
        public static bool InZone(Vector2Int cell, ConveyorCell belt, Vector2 point)
        {
            if (!belt.Exists) return false;
            float across = belt.shape == ConveyorShape.Straight
                ? Vector2.Dot(point - cell, new Vector2(belt.flow.y, -belt.flow.x)) + 0.5f
                : ToCurve(cell, belt, point).y;
            return across >= RailWidth && across < 1f - RailWidth;
        }

        /// <summary>꺾임 칸에서 점이 든 벨트 자리 (진행 칸, 반지름 칸). 벨트 구역 밖(난간)이면 false.</summary>
        public static bool TryCurveSlot(Vector2Int cell, ConveyorCell belt, Vector2 point, out Vector2Int slot)
        {
            var curve = ToCurve(cell, belt, point);
            slot = new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(curve.x * Slots), 0, Slots - 1), Mathf.Clamp(Mathf.FloorToInt(curve.y * Slots), 0, Slots - 1));
            return curve.y >= RailWidth && curve.y < 1f - RailWidth;
        }

        /// <summary>꺾임 칸 벨트 자리의 가운데.</summary>
        public static Vector2 CurveSlotCenter(Vector2Int cell, ConveyorCell belt, Vector2Int slot) =>
            FromCurve(cell, belt, (slot.x + 0.5f) / Slots, (slot.y + 0.5f) / Slots);

        /// <summary>
        /// 캐릭터 중심점 <paramref name="center"/>를 벨트가 <paramref name="distance"/>만큼 밀 때의 이동. 중심점이 벨트 구역 밖이면 false(이동 0).
        /// <paramref name="beltAt"/>은 칸의 벨트다. 직선은 흐름 방향으로, 꺾임은 돌림판으로 돌린 만큼 옮긴다.
        /// 다음 칸이 반대로 흐르는 직선 벨트(마주 봄)면 <paramref name="facing"/>이 true이고 둘의 경계를 넘어 밀지 않으며, 경계에 닿았으면
        /// <paramref name="lockedAxis"/>에 벨트 방향(그 축으로는 움직이지 못함), 아니면 0.
        /// </summary>
        public static bool TryPush(Vector2 center, Func<Vector2Int, ConveyorCell> beltAt, float distance, out Vector2 step, out bool facing, out Vector2Int lockedAxis)
        {
            step = Vector2.zero;
            facing = false;
            lockedAxis = Vector2Int.zero;
            var cell = CellOf(center);
            var belt = beltAt(cell);
            if (!InZone(cell, belt, center)) return false;
            if (belt.shape != ConveyorShape.Straight)
            {
                var curve = ToCurve(cell, belt, center);
                step = FromCurve(cell, belt, curve.x + distance, curve.y) - center;
                return true;
            }
            var next = beltAt(cell + belt.flow);
            facing = next.shape == ConveyorShape.Straight && next.flow == -belt.flow;
            if (facing)
            {
                float gap = Mathf.Max(0f, Vector2.Dot(cell + (Vector2)belt.flow * 0.5f - center, belt.flow));
                distance = Mathf.Min(distance, gap);
                if (gap < PinTolerance) lockedAxis = belt.flow;
            }
            step = (Vector2)belt.flow * distance;
            return true;
        }

        /// <summary>
        /// 점(바닥 흔적 픽셀)을 벨트를 따라 <paramref name="distance"/>만큼 옮긴 자리. 칸 경계를 넘으면 경계까지는 그 칸 규칙으로, 남은 거리는 다음 칸 규칙으로 옮긴다
        /// (다음 칸이 벨트가 아니면 나가던 방향으로 똑바로). 점이 벨트 구역 밖(벨트가 아니거나 난간)이면 false(그대로).
        /// </summary>
        public static bool TryAdvance(Vector2 point, float distance, Func<Vector2Int, ConveyorCell> beltAt, out Vector2 moved)
        {
            moved = point;
            var cell = CellOf(point);
            var belt = beltAt(cell);
            if (!InZone(cell, belt, point)) return false;
            float remaining = distance;
            for (int i = 0; i < MaxCells && remaining > 0f; i++)
            {
                float step;
                if (belt.shape == ConveyorShape.Straight)
                {
                    float left = 1f - Vector2.Dot(moved - (cell - (Vector2)belt.flow * 0.5f), belt.flow);
                    step = Mathf.Clamp(remaining, 0f, Mathf.Max(left, 0f));
                    moved += (Vector2)belt.flow * step;
                }
                else
                {
                    var curve = ToCurve(cell, belt, moved);
                    if (curve.y > 1f) return true; // 맞지 않는 면으로 들어와 난간에 닿음: 거기서 멈춘다.
                    step = Mathf.Clamp(remaining, 0f, Mathf.Max(1f - curve.x, 0f));
                    moved = FromCurve(cell, belt, curve.x + step, curve.y);
                }
                remaining -= step;
                if (remaining <= 0f) break;
                var exit = belt.flow;
                cell += exit;
                belt = beltAt(cell);
                if (!belt.Exists)
                {
                    moved += (Vector2)exit * remaining;
                    break;
                }
            }
            return true;
        }
    }
}
