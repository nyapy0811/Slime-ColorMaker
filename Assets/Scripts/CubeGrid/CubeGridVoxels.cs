using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 격자의 픽셀 모양(칸마다 16 × 16 × 16). 지형(블록·계단·기둥과 그 칸의 커넥터)과 상자의 픽셀을
    /// 프리팹 모델의 큐브 상자(<see cref="VoxelBoxes"/>)에서 놓인 값대로 센다. 모델을 고치면 모양도 따라간다.
    /// 설치 규칙의 완전한 면(<see cref="FullFace"/>), 같은 칸 계기판끼리의 겹침, 상자 붓의 가리키기(<see cref="Raycast"/>)를
    /// 편집 도구가 이것으로 본다. 데이터를 바꾸면 새로 만든다.
    /// </summary>
    public sealed class CubeGridVoxels
    {
        const int N = CubeGridRules.CellPixels;
        static readonly bool[] Empty = new bool[N * N * N];

        readonly CubeGridConfig config;
        readonly CubeGridData data;
        readonly Dictionary<Vector3Int, PillarLayout> layouts;
        readonly Dictionary<Vector3Int, bool[]> solidCells = new();
        readonly Dictionary<string, bool[]> shapes = new();
        readonly Dictionary<string, List<Vector3Int>> pixelLists = new();

        public CubeGridVoxels(CubeGridConfig config, CubeGridData data)
        {
            this.config = config;
            this.data = data;
            layouts = CubeGridRules.PillarLayouts(data);
        }

        /// <summary>칸의 <paramref name="toward"/> 쪽 옆 칸 지형이 칸 경계에 꽉 찬 면을 가졌나(설치 규칙의 완전한 면).</summary>
        public bool FullFace(Vector3Int cell, Vector3Int toward)
        {
            var next = TerrainOnly(cell + toward);
            return CubeGridRules.FullFace(toward, p => next[Index(p)]);
        }

        /// <summary>칸의 <paramref name="side"/> 면에 놓을 계기판이 같은 칸의 다른 면 계기판과 픽셀이 겹치나(모서리).</summary>
        public bool PanelOverlaps(Vector3Int cell, int side)
        {
            var mine = new HashSet<Vector3Int>(SidePiecePixels(config.panelPrefab, side));
            return data.panels.Exists(panel => panel.cell == cell && panel.side != side && SidePiecePixels(config.panelPrefab, panel.side).Exists(mine.Contains));
        }

        /// <summary>조명 칸 기준 조명 픽셀(칸 맨 위에 붙은 모양).</summary>
        public List<Vector3Int> LightPixels(CellAxis axis) =>
            Pixels("light" + axis, config.lightPrefab, CubeGridRules.LightRotation(axis), Vector3.zero);

        /// <summary>옆면 장식(난간·계기판, 남쪽 면 기준 모델)을 <paramref name="side"/> 면에 놓았을 때 칸 기준 픽셀.</summary>
        public List<Vector3Int> SidePiecePixels(GameObject prefab, int side) =>
            prefab == null ? new List<Vector3Int>() : Pixels($"side{prefab.name}_{CubeGridRules.Turn(side)}", prefab, CubeGridRules.SideRotation(side), Vector3.zero);

        /// <summary>
        /// 격자 전체 픽셀 좌표(칸 (0,0,0)의 바닥 모서리가 0, 1픽셀 = 1/16)로 광선이 처음 지나는 지형·상자 픽셀과 들어간 면.
        /// 상자 붓이 반 칸을 가리킬 때 쓴다.
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3Int pixel, out Vector3Int normal)
        {
            pixel = default;
            normal = default;
            if (direction == Vector3.zero) return false;
            direction.Normalize();
            var start = ToPixels(origin);
            var p = Vector3Int.FloorToInt(start);
            var step = new Vector3Int(Math.Sign(direction.x), Math.Sign(direction.y), Math.Sign(direction.z));
            var tMax = Vector3.zero;
            var tDelta = Vector3.zero;
            for (int a = 0; a < 3; a++)
            {
                if (step[a] == 0)
                {
                    tMax[a] = float.PositiveInfinity;
                    tDelta[a] = float.PositiveInfinity;
                    continue;
                }
                float boundary = step[a] > 0 ? p[a] + 1 : p[a];
                tMax[a] = (boundary - start[a]) / direction[a];
                tDelta[a] = Mathf.Abs(1f / direction[a]);
            }
            float limit = maxDistance * N;
            while (true)
            {
                if (SolidAt(p))
                {
                    pixel = p;
                    return true;
                }
                int axis = tMax.x < tMax.y ? (tMax.x < tMax.z ? 0 : 2) : (tMax.y < tMax.z ? 1 : 2);
                if (tMax[axis] > limit) return false;
                p[axis] += step[axis];
                normal = Vector3Int.zero;
                normal[axis] = -step[axis];
                tMax[axis] += tDelta[axis];
            }
        }

        /// <summary>월드 점의 격자 전체 픽셀 좌표(실수).</summary>
        public static Vector3 ToPixels(Vector3 world) => new Vector3(world.x + 0.5f, world.y, world.z + 0.5f) * N;

        /// <summary>격자 전체 픽셀이 든 반 칸 좌표.</summary>
        public static Vector3Int HalfOf(Vector3Int pixel) => new(FloorDiv(pixel.x, N / 2), FloorDiv(pixel.y, N / 2), FloorDiv(pixel.z, N / 2));

        bool SolidAt(Vector3Int global)
        {
            var cell = new Vector3Int(FloorDiv(global.x, N), FloorDiv(global.y, N), FloorDiv(global.z, N));
            return Solid(cell)[Index(global - cell * N)];
        }

        // 칸의 지형 + 상자 픽셀.
        bool[] Solid(Vector3Int cell)
        {
            if (solidCells.TryGetValue(cell, out var mask)) return mask;
            mask = TerrainOnly(cell);
            var crates = data.crates.Where(half => CubeGridRules.CrateCell(half) == cell).ToList();
            if (crates.Count > 0)
            {
                mask = (bool[])mask.Clone();
                foreach (var half in crates)
                {
                    var offset = CubeGridRules.CrateBottom(half) - CubeGridRules.CellCenter(cell);
                    var crate = Shape($"crate{half - cell * 2}", config.cratePrefab, Quaternion.identity, null, offset);
                    for (int i = 0; i < mask.Length; i++) mask[i] |= crate[i];
                }
            }
            solidCells[cell] = mask;
            return mask;
        }

        bool[] TerrainOnly(Vector3Int cell)
        {
            if (CubeGridRules.HasBlock(data, cell)) return Shape("block", config.blockPrefab, Quaternion.identity, null, Vector3.zero);
            if (data.stairs.FindIndex(s => s.cell == cell) is int stair and >= 0)
            {
                int turn = CubeGridRules.Turn(data.stairs[stair].turn);
                return Shape("stair" + turn, config.stairPrefab, CubeGridRules.StairRotation(turn), null, Vector3.zero);
            }
            if (layouts.TryGetValue(cell, out var layout) && config.pillarPrefab != null && config.pillarPrefab.TryGetComponent(out PillarView view))
            {
                var key = $"pillar{(int)layout.pose}{(int)layout.north}{(int)layout.east}{(int)layout.south}{(int)layout.west}";
                var models = view.ModelsFor(layout).ToList();
                return Shape(key, config.pillarPrefab, Quaternion.identity, t => models.Any(model => t == model.transform || t.IsChildOf(model.transform)), Vector3.zero);
            }
            return Empty;
        }

        List<Vector3Int> Pixels(string key, GameObject prefab, Quaternion rotation, Vector3 offset)
        {
            if (pixelLists.TryGetValue(key, out var pixels)) return pixels;
            var mask = Shape(key, prefab, rotation, null, offset);
            pixels = new List<Vector3Int>();
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) pixels.Add(new Vector3Int(i % N, i / N % N, i / (N * N)));
            pixelLists[key] = pixels;
            return pixels;
        }

        // 프리팹(루트 = 칸 가운데 + offset)을 돌려 놓았을 때 그 칸에서 찬 픽셀. include가 있으면 그 부품의 상자만 센다.
        bool[] Shape(string key, GameObject prefab, Quaternion rotation, Func<Transform, bool> include, Vector3 offset)
        {
            if (shapes.TryGetValue(key, out var mask)) return mask;
            mask = new bool[N * N * N];
            shapes[key] = mask;
            if (prefab == null) return mask;
            var root = prefab.transform;
            foreach (var boxes in prefab.GetComponentsInChildren<VoxelBoxes>(true))
            {
                if (include != null && !include(boxes.transform)) continue;
                var toRoot = root.worldToLocalMatrix * boxes.transform.localToWorldMatrix;
                foreach (var box in boxes.Boxes)
                {
                    var (from, to) = PixelRange(box, corner => (rotation * toRoot.MultiplyPoint3x4(corner) + offset + Vector3.one * 0.5f) * N); // 칸 바닥 모서리 기준
                    for (int z = Mathf.Max(from.z, 0); z < Mathf.Min(to.z, N); z++)
                    for (int y = Mathf.Max(from.y, 0); y < Mathf.Min(to.y, N); y++)
                    for (int x = Mathf.Max(from.x, 0); x < Mathf.Min(to.x, N); x++)
                        mask[x + N * (y + N * z)] = true;
                }
            }
            return mask;
        }

        static (Vector3Int from, Vector3Int to) PixelRange(Bounds box, Func<Vector3, Vector3> toPixels)
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
                var p = toPixels(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return (Vector3Int.RoundToInt(min), Vector3Int.RoundToInt(max));
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        static int Index(Vector3Int p) => p.x + N * (p.y + N * p.z);
    }
}
