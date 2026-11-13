using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 컨베이어 벨트가 미는 방향(2026-11-13). 바닥 평면(x, z)을 <see cref="Vector2"/>·<see cref="Vector2Int"/>(x, y)로 쓰고, 칸 (x, z)의 가운데는 (x, z)다.
    /// 캐릭터 발바닥이 걸친 벨트 중 중심점에 가장 가까운 벨트가 방향을 정한다(그 벨트가 흘러가는 다음 칸이 꺾이는 벨트이고 그것도 걸쳤으면 그쪽).
    /// 꺾이는 칸(옆에서 직각으로 흘러들어오는 이웃 벨트가 있는 칸)에는 가상의 대각선이 있고, 그 선을 무한히 늘인 직선의 어느 쪽에 중심점이 있는지로
    /// 들어오던 방향(선 앞)과 그 칸의 방향(선에 닿거나 넘음)을 고른다. 대각선은 '들어오는 쪽 면과 나가는 쪽 면이 만나는 꼭짓점'과 그 맞은편 꼭짓점을 잇는다.
    /// </summary>
    public static class ConveyorRules
    {
        static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        /// <summary>
        /// 중심점 <paramref name="center"/>에서 밀 방향(4방향 단위, 걸친 벨트가 없으면 0). <paramref name="touched"/>는 발바닥이 걸친 벨트 칸,
        /// <paramref name="flowAt"/>은 칸의 벨트가 흐르는 방향(벨트가 없으면 0)이다.
        /// </summary>
        public static Vector2Int PushDirection(Vector2 center, IReadOnlyList<Vector2Int> touched, Func<Vector2Int, Vector2Int> flowAt)
        {
            if (touched.Count == 0) return Vector2Int.zero;
            var target = Nearest(center, touched);
            var flow = flowAt(target);
            var next = target + flow;
            var nextFlow = flowAt(next);
            if (nextFlow != Vector2Int.zero && Dot(nextFlow, flow) == 0 && Contains(touched, next))
            {
                target = next;
                flow = nextFlow;
            }
            return Turn(center - target, flow, target, flowAt);
        }

        // 꺾이는 칸이면 대각선(늘인 직선) 앞에서 들어오던 방향. 들어오는 쪽이 둘이면 중심점에 가까운 쪽.
        static Vector2Int Turn(Vector2 offset, Vector2Int flow, Vector2Int cell, Func<Vector2Int, Vector2Int> flowAt)
        {
            bool found = false;
            Vector2Int nearest = default;
            float nearestDot = float.NegativeInfinity;
            foreach (var side in Sides)
            {
                if (Dot(side, flow) != 0 || flowAt(cell + side) != -side) continue; // 직각으로 흘러들어오는 이웃만(뒤에서 똑바로 들어오면 꺾지 않음).
                float dot = Vector2.Dot(offset, side);
                if (dot > nearestDot)
                {
                    nearestDot = dot;
                    nearest = side;
                    found = true;
                }
            }
            return found && nearestDot > Vector2.Dot(offset, flow) ? -nearest : flow;
        }

        // 중심점에서 칸(1 × 1)까지 거리가 가장 짧은 칸(칸 안이면 0).
        static Vector2Int Nearest(Vector2 center, IReadOnlyList<Vector2Int> cells)
        {
            var best = cells[0];
            float bestDistance = float.PositiveInfinity;
            foreach (var cell in cells)
            {
                float dx = Mathf.Max(Mathf.Abs(center.x - cell.x) - 0.5f, 0f);
                float dy = Mathf.Max(Mathf.Abs(center.y - cell.y) - 0.5f, 0f);
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }
            return best;
        }

        static bool Contains(IReadOnlyList<Vector2Int> cells, Vector2Int cell)
        {
            foreach (var c in cells)
                if (c == cell) return true;
            return false;
        }

        static int Dot(Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;
    }
}
