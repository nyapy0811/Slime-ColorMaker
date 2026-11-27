using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 순수 계산. 한 칸은 1×1×1이고 칸 좌표는 정수 (x, y, z)다. 칸은 x·z가 좌표 ±0.5, y가 좌표 ~ 좌표 + 1인
    /// 상자라 칸 중심은 (x, y + 0.5, z), 칸 바닥 가운데는 (x, y, z)다. 그래서 y = −1 칸의 블록 윗면이 높이 0이다.
    /// 높이 제한과 천장은 없다. 한 칸에는 블록, 계단, 기둥, 기물 중 하나만 둔다(기물 검사는 편집 도구와 맵 검사).
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

        public static bool HasPillar(CubeGridData data, Vector3Int cell) => data.pillars.Exists(pillar => pillar.cell == cell);

        public static bool HasLight(CubeGridData data, Vector3Int cell) => data.lights.Exists(light => light.cell == cell);

        /// <summary>블록이나 계단, 기둥이 칸을 차지하는가(천장 조명은 차지하지 않음, 조명과의 겹침은 픽셀로 봄).</summary>
        public static bool IsOccupied(CubeGridData data, Vector3Int cell) => HasBlock(data, cell) || HasStair(data, cell) || HasPillar(data, cell);

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

        /// <summary>기둥을 놓는다(눕힐지, 방향). 칸이 차 있으면 false.</summary>
        public static bool AddPillar(CubeGridData data, Vector3Int cell, bool lying, CellAxis axis)
        {
            if (IsOccupied(data, cell)) return false;
            data.pillars.Add(new CubePillar(cell, lying, axis));
            return true;
        }

        /// <summary>천장 조명을 놓는다(방향, 빛 색). 칸에 이미 조명이 있으면 false. 천장에 닿는지·지형과 겹치는지는 픽셀로 따로 본다(<see cref="FitLight"/>).</summary>
        public static bool AddLight(CubeGridData data, Vector3Int cell, CellAxis axis, Color color)
        {
            if (HasLight(data, cell)) return false;
            data.lights.Add(new CubeLight(cell, axis, color));
            return true;
        }

        /// <summary>한 칸 한 변의 픽셀 수.</summary>
        public const int CellPixels = 16;

        /// <summary>
        /// 천장 조명이 올라갈 높이(픽셀). <paramref name="light"/>는 조명 칸 기준 조명 픽셀(칸 맨 위에 붙은 모양, 0~15),
        /// <paramref name="ownSolid"/>·<paramref name="aboveSolid"/>는 조명 칸·위 칸의 지형 픽셀(칸 기준 0~15)이다.
        /// 조명 윗면의 모든 열에서 바로 위 칸 지형의 가장 낮은 픽셀까지 올리는 양이 같으면(윗면 전부가 위 칸 아랫면에 닿음, 아랫면 높이는 0이 아니어도 됨)
        /// 그만큼 올리고, 올린 조명 픽셀이 두 칸의 지형 픽셀과 겹치지 않으면 true.
        /// </summary>
        public static bool FitLight(IEnumerable<Vector3Int> light, Func<Vector3Int, bool> ownSolid, Func<Vector3Int, bool> aboveSolid, out int height)
        {
            height = -1;
            var pixels = new List<Vector3Int>(light);
            var tops = new Dictionary<Vector2Int, int>();
            foreach (var p in pixels)
            {
                var column = new Vector2Int(p.x, p.z);
                if (!tops.TryGetValue(column, out int top) || p.y > top) tops[column] = p.y;
            }
            if (tops.Count == 0) return false;
            foreach (var (column, top) in tops)
            {
                int lowest = -1;
                for (int y = 0; y < CellPixels && lowest < 0; y++)
                    if (aboveSolid(new Vector3Int(column.x, y, column.y))) lowest = y;
                if (lowest < 0) return false;
                int lift = CellPixels + lowest - (top + 1); // 조명 윗면(top + 1)을 위 칸 아랫면(16 + lowest)에 닿게
                if (height >= 0 && height != lift) return false;
                height = lift;
            }
            foreach (var p in pixels)
            {
                int y = p.y + height;
                if (y < CellPixels ? ownSolid(new Vector3Int(p.x, y, p.z)) : aboveSolid(new Vector3Int(p.x, y - CellPixels, p.z))) return false;
            }
            return true;
        }

        /// <summary>칸의 블록이나 계단, 기둥, 천장 조명을 지운다(블록의 코팅도 함께). 없었으면 false. 천장에서 떨어진 조명은 편집 도구가 따로 지운다.</summary>
        public static bool Remove(CubeGridData data, Vector3Int cell)
        {
            data.coatings.RemoveAll(coating => coating.cell == cell);
            return data.blocks.Remove(cell) | data.stairs.RemoveAll(stair => stair.cell == cell) > 0 | data.pillars.RemoveAll(pillar => pillar.cell == cell) > 0
                | data.lights.RemoveAll(light => light.cell == cell) > 0;
        }

        /// <summary>칸의 블록이나 계단, 기둥만 지운다(코팅·조명은 그대로). 편집 도구가 막 놓은 지형을 되돌릴 때 쓴다.</summary>
        public static void RemoveTerrain(CubeGridData data, Vector3Int cell)
        {
            data.blocks.Remove(cell);
            data.stairs.RemoveAll(stair => stair.cell == cell);
            data.pillars.RemoveAll(pillar => pillar.cell == cell);
        }

        /// <summary>블록 윗면 한 변의 코팅 픽셀 수(1픽셀 = 1/16칸).</summary>
        public const int CoatingPixels = 16;

        /// <summary>블록 윗면 픽셀의 코팅 종류. 칠하지 않았으면 None.</summary>
        public static CoatingKind GetCoating(CubeGridData data, Vector3Int cell, int x, int z)
        {
            int index = data.coatings.FindIndex(coating => coating.cell == cell);
            return index < 0 ? CoatingKind.None : (CoatingKind)data.coatings[index].pixels[z * CoatingPixels + x];
        }

        /// <summary>
        /// 블록 윗면 픽셀 하나를 칠한다(None이면 지움). 블록이 없는 칸이면 칠하지 않는다. 블록의 모든 픽셀이 None이 되면 목록에서 뺀다. 바뀌었으면 true.
        /// </summary>
        public static bool SetCoating(CubeGridData data, Vector3Int cell, int x, int z, CoatingKind kind)
        {
            if (!HasBlock(data, cell)) return false;
            int index = data.coatings.FindIndex(coating => coating.cell == cell);
            if (index < 0)
            {
                if (kind == CoatingKind.None) return false;
                data.coatings.Add(new CubeCoating { cell = cell, pixels = new byte[CoatingPixels * CoatingPixels] });
                index = data.coatings.Count - 1;
            }
            var pixels = data.coatings[index].pixels;
            int at = z * CoatingPixels + x;
            if (pixels[at] == (byte)kind) return false;
            pixels[at] = (byte)kind;
            if (kind == CoatingKind.None && Array.TrueForAll(pixels, pixel => pixel == 0)) data.coatings.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 점(블록 윗면 위)이 든 코팅 픽셀: 점 바로 아래의 칸(높이 0.01 아래)과 그 칸 윗면의 픽셀 x·z. 블록이 있는지는 보지 않는다.
        /// </summary>
        public static void CoatingPixelAt(Vector3 point, out Vector3Int cell, out int x, out int z)
        {
            int gx = Mathf.FloorToInt((point.x + 0.5f) * CoatingPixels);
            int gz = Mathf.FloorToInt((point.z + 0.5f) * CoatingPixels);
            cell = new Vector3Int(FloorDiv(gx, CoatingPixels), Mathf.FloorToInt(point.y - 0.01f), FloorDiv(gz, CoatingPixels));
            x = gx - cell.x * CoatingPixels;
            z = gz - cell.z * CoatingPixels;
        }

        /// <summary>
        /// 원형 코팅 붓(지름 <paramref name="diameter"/>픽셀, 1~16)이 덮는 픽셀들. 높이 <paramref name="topY"/>의 윗면에서, 지름이 홀수면 점이 든 픽셀 가운데에,
        /// 짝수면 가장 가까운 픽셀 모서리에 중심을 두고, 가운데가 반지름 안인 픽셀을 고른다(지름 1 = 1픽셀, 2 = 2 × 2, 3 = 3 × 3).
        /// </summary>
        public static List<(Vector3Int cell, int x, int z)> CoatingBrush(Vector3 point, float topY, int diameter)
        {
            diameter = Mathf.Clamp(diameter, 1, CoatingPixels);
            float px = (point.x + 0.5f) * CoatingPixels, pz = (point.z + 0.5f) * CoatingPixels;
            float cx = diameter % 2 == 1 ? Mathf.Floor(px) + 0.5f : Mathf.Round(px);
            float cz = diameter % 2 == 1 ? Mathf.Floor(pz) + 0.5f : Mathf.Round(pz);
            float radius = diameter / 2f;
            int y = Mathf.RoundToInt(topY) - 1;
            var pixels = new List<(Vector3Int, int, int)>();
            for (int gz = Mathf.FloorToInt(cz - radius); gz <= Mathf.CeilToInt(cz + radius); gz++)
            for (int gx = Mathf.FloorToInt(cx - radius); gx <= Mathf.CeilToInt(cx + radius); gx++)
            {
                float dx = gx + 0.5f - cx, dz = gz + 0.5f - cz;
                if (dx * dx + dz * dz > radius * radius + 1e-4f) continue;
                var cell = new Vector3Int(FloorDiv(gx, CoatingPixels), y, FloorDiv(gz, CoatingPixels));
                pixels.Add((cell, gx - cell.x * CoatingPixels, gz - cell.z * CoatingPixels));
            }
            return pixels;
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>
        /// 기둥마다 칸의 겉모습 값(놓인 모양, 동서남북 커넥터). 저장하지 않고 지을 때마다 계산한다.
        /// 세운 기둥은 놓을 때 고른 방향(홈이 보는 쪽) 그대로다. 눕힌 기둥(들보)은 같은 높이 옆 칸의 기둥(세운 것·눕힌 것)이
        /// 더 많은 축으로 눕고, 두 축이 같으면(옆 칸이 없음, L자 모서리, 십자 가운데) 놓을 때 고른 방향을 쓴다.
        /// 커넥터는 옆 칸 들보의 끝이 이 칸에 닿는 면에 둔다: 세운 기둥의 평평한 면이면 1, 홈이면 2, 눕힌 기둥의 옆면이면 3.
        /// 일직선으로 이어진 들보끼리와 위·아래로 닿는 기둥에는 두지 않는다.
        /// </summary>
        public static Dictionary<Vector3Int, PillarLayout> PillarLayouts(CubeGridData data)
        {
            var byCell = new Dictionary<Vector3Int, CubePillar>();
            foreach (var pillar in data.pillars) byCell.TryAdd(pillar.cell, pillar);

            var axes = new Dictionary<Vector3Int, CellAxis>();
            foreach (var (cell, pillar) in byCell)
            {
                if (!pillar.lying)
                {
                    axes[cell] = pillar.axis;
                    continue;
                }
                int x = Count(cell + Vector3Int.right) + Count(cell + Vector3Int.left);
                int z = Count(cell + new Vector3Int(0, 0, 1)) + Count(cell + new Vector3Int(0, 0, -1));
                axes[cell] = x > z ? CellAxis.EastWest : z > x ? CellAxis.NorthSouth : pillar.axis;
            }
            int Count(Vector3Int cell) => byCell.ContainsKey(cell) ? 1 : 0;

            var layouts = new Dictionary<Vector3Int, PillarLayout>();
            foreach (var (cell, pillar) in byCell)
            {
                var axis = axes[cell];
                var layout = new PillarLayout
                {
                    pose = pillar.lying
                        ? axis == CellAxis.NorthSouth ? PillarPose.LyingNorthSouth : PillarPose.LyingEastWest
                        : axis == CellAxis.NorthSouth ? PillarPose.UprightNorthSouth : PillarPose.UprightEastWest,
                };
                for (int side = 0; side < SideSteps.Length; side++)
                {
                    var sideAxis = side % 2 == 0 ? CellAxis.NorthSouth : CellAxis.EastWest;
                    var next = cell + SideSteps[side];
                    if (!byCell.TryGetValue(next, out var neighbor) || !neighbor.lying || axes[next] != sideAxis) continue; // 옆 칸 들보의 끝이 이 칸을 보지 않음
                    if (pillar.lying && axis == sideAxis) continue; // 일직선으로 이어진 들보
                    layout[side] = pillar.lying ? PillarConnector.Beam : axis == sideAxis ? PillarConnector.Groove : PillarConnector.Flat;
                }
                layouts[cell] = layout;
            }
            return layouts;
        }

        // 옆면 0~3(북·동·남·서, CellFace 차례)의 이웃 칸 방향.
        static readonly Vector3Int[] SideSteps = { new(0, 0, 1), Vector3Int.right, new(0, 0, -1), Vector3Int.left };

        /// <summary>
        /// 덮인 빈 칸 덩어리(방·굴·처마 밑): 같은 열(x, z)의 위와 아래 둘 다에 찬 칸(블록·계단·기둥)이 있는 빈 칸을, 면으로 이어진 덩어리로 묶는다.
        /// 아래가 없는 칸(맵 밑)은 빼고, <paramref name="minCells"/>칸보다 작은 덩어리도 뺀다. 실내 반사 탐침이 덩어리마다 하나씩 놓인다.
        /// </summary>
        public static List<CoveredRegion> CoveredRegions(CubeGridData data, int minCells)
        {
            var filled = new HashSet<Vector3Int>(data.blocks);
            foreach (var stair in data.stairs) filled.Add(stair.cell);
            foreach (var pillar in data.pillars) filled.Add(pillar.cell);

            // 열마다 가장 낮은·높은 찬 칸
            var columns = new Dictionary<Vector2Int, (int low, int high)>();
            foreach (var cell in filled)
            {
                var key = new Vector2Int(cell.x, cell.z);
                columns[key] = columns.TryGetValue(key, out var range) ? (Math.Min(range.low, cell.y), Math.Max(range.high, cell.y)) : (cell.y, cell.y);
            }
            var covered = new HashSet<Vector3Int>();
            foreach (var (column, range) in columns)
                for (int y = range.low + 1; y < range.high; y++)
                {
                    var cell = new Vector3Int(column.x, y, column.y);
                    if (!filled.Contains(cell)) covered.Add(cell);
                }

            var regions = new List<CoveredRegion>();
            var queue = new Queue<Vector3Int>();
            var cells = new List<Vector3Int>();
            while (covered.Count > 0)
            {
                var start = default(Vector3Int);
                foreach (var cell in covered) { start = cell; break; }
                covered.Remove(start);
                queue.Enqueue(start);
                cells.Clear();
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    cells.Add(cell);
                    foreach (var step in FaceSteps)
                        if (covered.Remove(cell + step)) queue.Enqueue(cell + step);
                }
                if (cells.Count < Math.Max(1, minCells)) continue;

                var min = cells[0];
                var max = cells[0];
                foreach (var cell in cells) { min = Vector3Int.Min(min, cell); max = Vector3Int.Max(max, cell); }
                var middle = (Vector3)(min + max) * 0.5f;
                var center = cells[0];
                foreach (var cell in cells)
                    if ((cell - middle).sqrMagnitude < (center - middle).sqrMagnitude || ((cell - middle).sqrMagnitude == (center - middle).sqrMagnitude && Less(cell, center)))
                        center = cell;
                regions.Add(new CoveredRegion(min, max, center, cells.Count));
            }
            regions.Sort((a, b) => Less(a.center, b.center) ? -1 : a.center == b.center ? 0 : 1);
            return regions;
        }

        static readonly Vector3Int[] FaceSteps =
            { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new(0, 0, 1), new(0, 0, -1) };

        // 칸 순서(결과를 늘 같은 순서로): y, z, x 순.
        static bool Less(Vector3Int a, Vector3Int b) => a.y != b.y ? a.y < b.y : a.z != b.z ? a.z < b.z : a.x < b.x;

        /// <summary>방향 횟수를 0~3으로 맞춘다.</summary>
        public static int Turn(int turn) => (turn % 4 + 4) % 4;

        /// <summary>계단의 회전(모델은 +z로 오름).</summary>
        public static Quaternion StairRotation(int turn) => Quaternion.Euler(0f, 90f * Turn(turn), 0f);

        /// <summary>천장 조명의 회전(모델은 남북으로 긺).</summary>
        public static Quaternion LightRotation(CellAxis axis) => Quaternion.Euler(0f, axis == CellAxis.EastWest ? 90f : 0f, 0f);

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

        /// <summary>가장 낮은 블록·계단·기둥 칸의 y. 셋 다 없으면 false.</summary>
        public static bool TryLowestY(CubeGridData data, out int lowest)
        {
            lowest = int.MaxValue;
            foreach (var cell in data.blocks) lowest = Math.Min(lowest, cell.y);
            foreach (var stair in data.stairs) lowest = Math.Min(lowest, stair.cell.y);
            foreach (var pillar in data.pillars) lowest = Math.Min(lowest, pillar.cell.y);
            return data.blocks.Count + data.stairs.Count + data.pillars.Count > 0;
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
