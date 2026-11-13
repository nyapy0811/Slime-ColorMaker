using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class ConveyorRulesTests
    {
        // 그림의 모서리: 칸 (0,0)은 아래(−z)로 흐르고, 오른쪽 칸 (1,0)이 왼쪽으로 흘러들어온다. 대각선(늘인 직선)은 x + z = 0.
        static readonly Vector2Int Corner = new(0, 0);
        static readonly Vector2Int Incoming = new(1, 0);

        static Vector2Int FlowAt(Dictionary<Vector2Int, Vector2Int> belts, Vector2Int cell) => belts.TryGetValue(cell, out var flow) ? flow : Vector2Int.zero;

        static Vector2Int Push(Dictionary<Vector2Int, Vector2Int> belts, Vector2 center, params Vector2Int[] touched) =>
            ConveyorRules.PushDirection(center, touched, cell => FlowAt(belts, cell));

        static Dictionary<Vector2Int, Vector2Int> CornerBelts() => new()
        {
            { Corner, Vector2Int.down },
            { Incoming, Vector2Int.left },
            { new Vector2Int(2, 0), Vector2Int.left },
            { new Vector2Int(0, -1), Vector2Int.down },
        };

        [Test]
        public void NothingTouched_DoesNotPush()
        {
            Assert.AreEqual(Vector2Int.zero, Push(CornerBelts(), Vector2.zero));
        }

        [Test]
        public void OnIncomingBelt_KeepsIncomingDirection()
        {
            Assert.AreEqual(Vector2Int.left, Push(CornerBelts(), new Vector2(1.2f, 0f), Incoming));
            Assert.AreEqual(Vector2Int.left, Push(CornerBelts(), new Vector2(0.6f, -0.2f), Incoming, Corner));
        }

        [Test]
        public void InCorner_SplitByDiagonal()
        {
            Assert.AreEqual(Vector2Int.left, Push(CornerBelts(), new Vector2(0.3f, 0f), Corner, Incoming));
            Assert.AreEqual(Vector2Int.left, Push(CornerBelts(), new Vector2(-0.2f, 0.3f), Corner));
            Assert.AreEqual(Vector2Int.down, Push(CornerBelts(), Vector2.zero, Corner, Incoming));
            Assert.AreEqual(Vector2Int.down, Push(CornerBelts(), new Vector2(0.2f, -0.3f), Corner));
        }

        [Test]
        public void EmptyCornerBetweenBelts_UsesExtendedDiagonal()
        {
            // 두 벨트 사이 빈 칸 (1,−1)에 중심점이 있고 몸만 둘에 걸침.
            Assert.AreEqual(Vector2Int.left, Push(CornerBelts(), new Vector2(0.75f, -0.6f), Incoming, Corner));
            Assert.AreEqual(Vector2Int.down, Push(CornerBelts(), new Vector2(0.6f, -0.75f), Incoming, Corner));
        }

        [Test]
        public void CornerFromLeft_MirrorsDiagonal()
        {
            var belts = new Dictionary<Vector2Int, Vector2Int> { { Corner, Vector2Int.down }, { new Vector2Int(-1, 0), Vector2Int.right } };
            Assert.AreEqual(Vector2Int.right, Push(belts, new Vector2(-0.3f, 0.1f), Corner));
            Assert.AreEqual(Vector2Int.down, Push(belts, new Vector2(0.1f, 0f), Corner));
        }

        [Test]
        public void Straight_DoesNotTurn()
        {
            var belts = new Dictionary<Vector2Int, Vector2Int> { { Corner, Vector2Int.down }, { new Vector2Int(0, 1), Vector2Int.down } };
            Assert.AreEqual(Vector2Int.down, Push(belts, new Vector2(0.4f, 0.4f), Corner, new Vector2Int(0, 1)));
        }

        [Test]
        public void ParallelBelts_NearestToCenterWins()
        {
            var belts = new Dictionary<Vector2Int, Vector2Int> { { new Vector2Int(0, 0), Vector2Int.up }, { new Vector2Int(1, 0), Vector2Int.down } };
            Assert.AreEqual(Vector2Int.up, Push(belts, new Vector2(0.4f, 0f), new Vector2Int(0, 0), new Vector2Int(1, 0)));
            Assert.AreEqual(Vector2Int.down, Push(belts, new Vector2(0.6f, 0f), new Vector2Int(0, 0), new Vector2Int(1, 0)));
        }
    }
}
