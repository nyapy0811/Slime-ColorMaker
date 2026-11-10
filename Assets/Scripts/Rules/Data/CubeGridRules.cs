using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 순수 계산(v1 칸 규칙). 한 칸은 1×1×1이고 칸 좌표는 정수 (x, y, z)다. 칸은 x·z가 좌표 ±0.5, y가 좌표 ~ 좌표 + 1인
    /// 상자라 칸 중심은 (x, y + 0.5, z), 칸 바닥 가운데는 (x, y, z)다. 그래서 y = −1 칸의 블록 윗면이 높이 0이다.
    /// 높이 제한과 천장은 없다. 한 칸에는 블록, 계단, 기물 중 하나만 둔다(기물 검사는 편집 도구와 맵 검사).
    /// </summary>
    public static class CubeGridRules
    {
        const float Epsilon = 1e-4f;

        public static Vector3 CellCenter(Vector3Int cell) => new(cell.x, cell.y + 0.5f, cell.z);

        /// <summary>칸 바닥 가운데. 바닥에 서는 기물(색 원천, 시작 지점, 목적지)을 여기 놓는다.</summary>
        public static Vector3 CellBottom(Vector3Int cell) => new(cell.x, cell.y, cell.z);

        /// <summary>점이 든 칸. 칸 바닥(y가 정수)에 놓인 점은 그 칸이다.</summary>
        public static Vector3Int CellAt(Vector3 point) =>
            new(Mathf.RoundToInt(point.x), Mathf.FloorToInt(point.y + Epsilon), Mathf.RoundToInt(point.z));

        /// <summary>점이 칸 바닥 가운데에 맞춰져 있는가(x, y, z가 모두 정수).</summary>
        public static bool IsOnCellBottom(Vector3 point) =>
            Mathf.Abs(point.x - Mathf.Round(point.x)) < Epsilon
            && Mathf.Abs(point.y - Mathf.Round(point.y)) < Epsilon
            && Mathf.Abs(point.z - Mathf.Round(point.z)) < Epsilon;

        public static bool HasBlock(CubeGridData data, Vector3Int cell) => data.blocks.Contains(cell);

        public static bool HasStair(CubeGridData data, Vector3Int cell) => data.stairs.Exists(stair => stair.cell == cell);

        /// <summary>블록이나 계단이 칸을 차지하는가.</summary>
        public static bool IsOccupied(CubeGridData data, Vector3Int cell) => HasBlock(data, cell) || HasStair(data, cell);

        /// <summary>블록을 놓는다. 칸이 차 있으면 false.</summary>
        public static bool AddBlock(CubeGridData data, Vector3Int cell)
        {
            if (IsOccupied(data, cell)) return false;
            data.blocks.Add(cell);
            return true;
        }

        /// <summary>계단을 놓는다(방향은 0~3으로 맞춤). 칸이 차 있으면 false.</summary>
        public static bool AddStair(CubeGridData data, Vector3Int cell, int turn)
        {
            if (IsOccupied(data, cell)) return false;
            data.stairs.Add(new CubeStair(cell, Turn(turn)));
            return true;
        }

        /// <summary>칸의 블록이나 계단을 지운다. 없었으면 false.</summary>
        public static bool Remove(CubeGridData data, Vector3Int cell) =>
            data.blocks.Remove(cell) | data.stairs.RemoveAll(stair => stair.cell == cell) > 0;

        /// <summary>방향 횟수를 0~3으로 맞춘다.</summary>
        public static int Turn(int turn) => (turn % 4 + 4) % 4;

        /// <summary>계단의 회전(모델은 +z로 오름).</summary>
        public static Quaternion StairRotation(int turn) => Quaternion.Euler(0f, 90f * Turn(turn), 0f);

        /// <summary>수평 방향에 가장 가까운 계단 방향(그쪽으로 오름). 편집 도구가 카메라가 보는 쪽으로 놓을 때 쓴다.</summary>
        public static int TurnFacing(Vector3 direction) =>
            Turn(Mathf.RoundToInt(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / 90f));

        /// <summary>두 칸을 모서리로 하는 상자 안의 모든 칸(직사각형 범위 놓기·지우기).</summary>
        public static List<Vector3Int> Box(Vector3Int a, Vector3Int b)
        {
            var min = Vector3Int.Min(a, b);
            var max = Vector3Int.Max(a, b);
            var cells = new List<Vector3Int>();
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
                cells.Add(new Vector3Int(x, y, z));
            return cells;
        }

        /// <summary>가장 낮은 블록·계단 칸의 y. 둘 다 없으면 false.</summary>
        public static bool TryLowestY(CubeGridData data, out int lowest)
        {
            lowest = int.MaxValue;
            foreach (var cell in data.blocks) lowest = Math.Min(lowest, cell.y);
            foreach (var stair in data.stairs) lowest = Math.Min(lowest, stair.cell.y);
            return data.blocks.Count + data.stairs.Count > 0;
        }

        /// <summary>
        /// 광선이 처음 지나는 '찬 칸'을 칸 단위로 찾는다(콜라이더 없이 격자로 계산). <paramref name="normal"/>은 광선이 들어간 면의
        /// 바깥 방향이라 '그 면 쪽 이웃 칸' = <paramref name="cell"/> + <paramref name="normal"/>이다. 시작점이 든 칸은 보지 않는다.
        /// </summary>
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, Func<Vector3Int, bool> isFilled,
            out Vector3Int cell, out Vector3Int normal)
        {
            normal = Vector3Int.zero;
            var dir = direction.normalized;
            // 칸 경계가 정수가 되게 옮긴 좌표(x·z를 0.5 밀어 칸이 [n, n + 1)이 되게 함).
            var p = new Vector3(origin.x + 0.5f, origin.y, origin.z + 0.5f);
            cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
            if (dir == Vector3.zero) return false;

            var step = new Vector3Int(Math.Sign(dir.x), Math.Sign(dir.y), Math.Sign(dir.z));
            var tMax = new Vector3(FirstCrossing(p.x, dir.x, cell.x), FirstCrossing(p.y, dir.y, cell.y), FirstCrossing(p.z, dir.z, cell.z));
            var tDelta = new Vector3(Delta(dir.x), Delta(dir.y), Delta(dir.z));

            int guard = (int)(maxDistance * 3f) + 3;
            while (guard-- > 0)
            {
                int axis = tMax.x <= tMax.y && tMax.x <= tMax.z ? 0 : tMax.y <= tMax.z ? 1 : 2;
                float t = tMax[axis];
                if (t > maxDistance) return false;
                cell[axis] += step[axis];
                normal = Vector3Int.zero;
                normal[axis] = -step[axis];
                tMax[axis] += tDelta[axis];
                if (isFilled(cell)) return true;
            }
            return false;
        }

        static float FirstCrossing(float p, float d, int cell) =>
            d > 0f ? (cell + 1 - p) / d : d < 0f ? (p - cell) / -d : float.PositiveInfinity;

        static float Delta(float d) => d != 0f ? 1f / Math.Abs(d) : float.PositiveInfinity;
    }
}
