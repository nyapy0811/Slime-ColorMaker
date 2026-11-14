using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class ConveyorRulesTests
    {
        const float Pixel = 1f / ConveyorRules.Slots;

        // 길: 칸 (−1,0) 직선(+x로) → 칸 (0,0) 오른쪽 꺾임(−x 쪽에서 들어와 −z로 나감) → 칸 (0,−1) 직선(−z로).
        static readonly Vector2Int Before = new(-1, 0);
        static readonly Vector2Int Curve = new(0, 0);
        static readonly Vector2Int After = new(0, -1);

        static Dictionary<Vector2Int, ConveyorCell> Track(ConveyorShape curveShape = ConveyorShape.Right) => new()
        {
            { Before, new ConveyorCell(Vector2Int.right, ConveyorShape.Straight) },
            { Curve, new ConveyorCell(Vector2Int.down, curveShape) },
            { After, new ConveyorCell(Vector2Int.down, ConveyorShape.Straight) },
        };

        static System.Func<Vector2Int, ConveyorCell> BeltAt(Dictionary<Vector2Int, ConveyorCell> belts) =>
            cell => belts.TryGetValue(cell, out var belt) ? belt : default;

        static void AreClose(Vector2 expected, Vector2 actual, float tolerance = 1e-4f) =>
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(tolerance), $"expected {expected}, actual {actual}");

        static Vector2 Push(Dictionary<Vector2Int, ConveyorCell> belts, Vector2 center, float distance)
        {
            Assert.IsTrue(ConveyorRules.TryPush(center, BeltAt(belts), distance, out var step, out _, out _), $"not on belt at {center}");
            return step;
        }

        [Test]
        public void OffBeltOrOnRail_DoesNotPush()
        {
            Assert.IsFalse(ConveyorRules.TryPush(new Vector2(3f, 3f), BeltAt(Track()), 1f, out var step, out _, out _));
            Assert.AreEqual(Vector2.zero, step);
            // 직선 칸 (−1,0)(+x로)의 난간: 폭 방향 양 끝 1/16.
            Assert.IsFalse(ConveyorRules.TryPush(new Vector2(-1f, 0.47f), BeltAt(Track()), 1f, out _, out _, out _));
            Assert.IsFalse(ConveyorRules.TryPush(new Vector2(-1f, -0.47f), BeltAt(Track()), 1f, out _, out _, out _));
        }

        [Test]
        public void Zone_MatchesRails()
        {
            var straight = new ConveyorCell(Vector2Int.right, ConveyorShape.Straight);
            Assert.IsFalse(ConveyorRules.InZone(Vector2Int.zero, straight, new Vector2(0f, 0.5f - 0.5f * Pixel)));
            Assert.IsTrue(ConveyorRules.InZone(Vector2Int.zero, straight, new Vector2(0f, 0.5f - 1.5f * Pixel)));
            Assert.IsTrue(ConveyorRules.InZone(Vector2Int.zero, straight, new Vector2(0.49f, 0f)));
            var right = new ConveyorCell(Vector2Int.down, ConveyorShape.Right);
            var corner = ConveyorRules.InnerCorner(Curve, right);
            Assert.IsFalse(ConveyorRules.InZone(Curve, right, corner + new Vector2(0.5f, 0.5f) * Pixel)); // 안쪽 모서리 한 칸
            Assert.IsTrue(ConveyorRules.InZone(Curve, right, corner + new Vector2(0f, 0.9f)));
            Assert.IsFalse(ConveyorRules.InZone(Curve, right, corner + new Vector2(0f, 0.95f))); // 바깥 호
            Assert.IsFalse(ConveyorRules.InZone(Curve, right, corner + new Vector2(0.9f, 0.9f))); // 바깥 모서리
        }

        [Test]
        public void Straight_PushesAlongFlow()
        {
            AreClose(new Vector2(0.3f, 0f), Push(Track(), new Vector2(-1f, 0.2f), 0.3f));
        }

        [Test]
        public void CenterCellDecides()
        {
            var belts = new Dictionary<Vector2Int, ConveyorCell>
            {
                { new Vector2Int(0, 0), new ConveyorCell(Vector2Int.up, ConveyorShape.Straight) },
                { new Vector2Int(1, 0), new ConveyorCell(Vector2Int.down, ConveyorShape.Straight) },
            };
            AreClose(new Vector2(0f, 0.1f), Push(belts, new Vector2(0.4f, 0f), 0.1f));
            AreClose(new Vector2(0f, -0.1f), Push(belts, new Vector2(0.6f, 0f), 0.1f));
        }

        [Test]
        public void FacingBelts_StopAtBoundaryAndLockAxis()
        {
            var belts = new Dictionary<Vector2Int, ConveyorCell>
            {
                { new Vector2Int(0, 0), new ConveyorCell(Vector2Int.right, ConveyorShape.Straight) },
                { new Vector2Int(1, 0), new ConveyorCell(Vector2Int.left, ConveyorShape.Straight) },
            };
            Assert.IsTrue(ConveyorRules.TryPush(new Vector2(0.3f, 0f), BeltAt(belts), 0.1f, out var step, out bool facing, out var locked));
            AreClose(new Vector2(0.1f, 0f), step);
            Assert.IsTrue(facing);
            Assert.AreEqual(Vector2Int.zero, locked);
            // 경계(x = 0.5)를 넘어 밀지 않는다.
            Assert.IsTrue(ConveyorRules.TryPush(new Vector2(0.45f, 0f), BeltAt(belts), 0.1f, out step, out _, out locked));
            AreClose(new Vector2(0.05f, 0f), step);
            Assert.AreEqual(Vector2Int.zero, locked);
            // 경계에 닿으면 벨트 방향 축을 막는다(양쪽 다).
            Assert.IsTrue(ConveyorRules.TryPush(new Vector2(0.495f, 0f), BeltAt(belts), 0.1f, out step, out _, out locked));
            Assert.AreEqual(Vector2Int.right, locked);
            Assert.IsTrue(ConveyorRules.TryPush(new Vector2(0.505f, 0f), BeltAt(belts), 0.1f, out step, out _, out locked));
            Assert.AreEqual(Vector2Int.left, locked);
        }

        [Test]
        public void Curve_EntrySideAndCorner()
        {
            var right = new ConveyorCell(Vector2Int.down, ConveyorShape.Right);
            var left = new ConveyorCell(Vector2Int.down, ConveyorShape.Left);
            Assert.AreEqual(Vector2Int.left, ConveyorRules.EntrySide(right));
            Assert.AreEqual(Vector2Int.right, ConveyorRules.EntrySide(left));
            AreClose(new Vector2(-0.5f, -0.5f), ConveyorRules.InnerCorner(Curve, right));
            AreClose(new Vector2(0.5f, -0.5f), ConveyorRules.InnerCorner(Curve, left));
        }

        [Test]
        public void Curve_TurnsTowardExit()
        {
            // 오른쪽 꺾임: 들어올 때는 +x로, 나갈 때는 −z로 민다.
            var step = Push(Track(), new Vector2(-0.49f, 0f), 0.01f);
            Assert.That(step.x, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(step.y), Is.LessThan(0.002f));
            step = Push(Track(), new Vector2(0f, -0.49f), 0.01f);
            Assert.That(step.y, Is.LessThan(0f));
            Assert.That(Mathf.Abs(step.x), Is.LessThan(0.002f));
            // 왼쪽 꺾임: 들어올 때는 −x로.
            step = Push(Track(ConveyorShape.Left), new Vector2(0.49f, 0f), 0.01f);
            Assert.That(step.x, Is.LessThan(0f));
        }

        [Test]
        public void Curve_CharacterKeepsRadius()
        {
            var center = new Vector2(-0.45f, 0.2f); // 반지름 약 0.702
            float radius = Vector2.Distance(center, new Vector2(-0.5f, -0.5f));
            for (int i = 0; i < 50; i++) center += Push(Track(), center, 0.01f);
            Assert.That(Vector2.Distance(center, new Vector2(-0.5f, -0.5f)), Is.EqualTo(radius).Within(1e-4f));
        }

        [Test]
        public void Curve_TakesSameDistanceAtEveryRadius()
        {
            foreach (float radius in new[] { 0.1f, 0.5f, 0.9f })
            {
                Assert.IsTrue(ConveyorRules.TryAdvance(new Vector2(-0.5f, -0.5f + radius), 1f, BeltAt(Track()), out var moved));
                AreClose(new Vector2(-0.5f + radius, -0.5f), moved);
            }
        }

        [Test]
        public void Pixels_KeepShapeThroughCurve()
        {
            // 직선 칸 가운데의 가로 한 줄(난간 뺀 픽셀 14개)이 1/16씩 32걸음(2칸) 뒤 나가는 칸 가운데에 같은 줄로 나온다.
            var belts = BeltAt(Track());
            for (int j = 1; j < ConveyorRules.Slots - 1; j++)
            {
                float lateral = (j + 0.5f) * Pixel;
                var point = new Vector2(-1f + Pixel / 2f, -0.5f + lateral);
                for (int step = 0; step < 32; step++) Assert.IsTrue(ConveyorRules.TryAdvance(point, Pixel, belts, out point));
                AreClose(new Vector2(-0.5f + lateral, -1f - Pixel / 2f), point);
            }
        }

        [Test]
        public void Pixels_LeaveBeltStraight()
        {
            var belts = new Dictionary<Vector2Int, ConveyorCell> { { Vector2Int.zero, new ConveyorCell(Vector2Int.right, ConveyorShape.Straight) } };
            Assert.IsTrue(ConveyorRules.TryAdvance(new Vector2(0.45f, 0.1f), 0.1f, BeltAt(belts), out var moved));
            AreClose(new Vector2(0.55f, 0.1f), moved);
            Assert.IsFalse(ConveyorRules.TryAdvance(new Vector2(1.2f, 0f), 0.1f, BeltAt(belts), out _));
        }

        [Test]
        public void Pixels_OnRailDoNotMove()
        {
            Assert.IsFalse(ConveyorRules.TryAdvance(new Vector2(-1f, 0.5f - Pixel / 2f), Pixel, BeltAt(Track()), out _)); // 직선 난간
            Assert.IsFalse(ConveyorRules.TryAdvance(new Vector2(0.45f, 0.45f), Pixel, BeltAt(Track()), out _)); // 꺾임 바깥 모서리
            Assert.IsFalse(ConveyorRules.TryAdvance(new Vector2(-0.5f + Pixel / 2f, -0.5f + Pixel / 2f), Pixel, BeltAt(Track()), out _)); // 꺾임 안쪽 모서리
        }

        [Test]
        public void CurveSlot_RoundTrips()
        {
            var right = new ConveyorCell(Vector2Int.down, ConveyorShape.Right);
            for (int progress = 0; progress < ConveyorRules.Slots; progress += 5)
            {
                for (int radius = 1; radius < ConveyorRules.Slots - 1; radius += 3)
                {
                    var slot = new Vector2Int(progress, radius);
                    Assert.IsTrue(ConveyorRules.TryCurveSlot(Curve, right, ConveyorRules.CurveSlotCenter(Curve, right, slot), out var found));
                    Assert.AreEqual(slot, found);
                }
            }
            Assert.IsFalse(ConveyorRules.TryCurveSlot(Curve, right, ConveyorRules.CurveSlotCenter(Curve, right, new Vector2Int(3, 0)), out _));
            Assert.IsFalse(ConveyorRules.TryCurveSlot(Curve, right, ConveyorRules.CurveSlotCenter(Curve, right, new Vector2Int(3, 15)), out _));
            Assert.IsFalse(ConveyorRules.TryCurveSlot(Curve, right, new Vector2(0.45f, 0.45f), out _));
        }

        [Test]
        public void Inertia_StopsInTimeAndSlowsDown()
        {
            // 속도 3, 관성 시간 1/3초 → 0.5칸.
            var inertia = ConveyorInertia.Start(new Vector2(3f, 0f), 1f / 3f);
            Assert.IsTrue(inertia.Active);
            var first = inertia.Step(0.02f);
            var total = first;
            Vector2 last = first;
            for (int i = 0; i < 100 && inertia.Active; i++)
            {
                last = inertia.Step(0.02f);
                total += last;
            }
            Assert.IsFalse(inertia.Active);
            AreClose(new Vector2(0.5f, 0f), total);
            Assert.That(last.x, Is.LessThan(first.x));
            // 처음 빠르기 = 밀던 빠르기.
            Assert.That(first.x / 0.02f, Is.EqualTo(3f).Within(0.1f));
        }

        [Test]
        public void Inertia_DiagonalAndNone()
        {
            // 거리 = 속도 × 시간 ÷ 2: 속도 6이면 1칸, 대각선도 같은 식.
            AreClose(new Vector2(1f, 0f), ConveyorInertia.Start(new Vector2(6f, 0f), 1f / 3f).Step(100f));
            var diagonal = new Vector2(3f, -4f); // 빠르기 5
            AreClose(diagonal.normalized * (5f / 3f / 2f), ConveyorInertia.Start(diagonal, 1f / 3f).Step(100f));
            Assert.IsFalse(ConveyorInertia.Start(Vector2.zero, 1f / 3f).Active);
            Assert.IsFalse(ConveyorInertia.Start(Vector2.one, 0f).Active);
        }
    }
}
