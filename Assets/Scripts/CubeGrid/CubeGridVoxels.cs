using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 격자의 픽셀 모양(칸마다 16 × 16 × 16). 칸의 지형(블록·계단·기둥과 그 칸의 커넥터)과 맵에 놓인 기물·시험 모델, 천장 조명의 픽셀을
    /// 모델의 큐브 상자(<see cref="VoxelBoxes"/>, .bbmodel 가져오기가 붙임)에서 센다. 지형은 프리팹을 놓인 값대로, 기물은 놓인 그대로(켜진 모델만, 지금 자리) 센다.
    /// 모델을 고치면 모양도 따라간다.
    /// 천장 조명이 위 칸 아랫면에 닿는지·지형과 겹치는지(<see cref="CubeGridRules.FitLight"/>)를 편집 도구와 빌더가 이것으로 본다.
    /// 데이터를 바꾸면 새로 만든다(칸 모양을 처음 물을 때 세어 둠).
    /// </summary>
    public sealed class CubeGridVoxels
    {
        const int N = CubeGridRules.CellPixels;
        static readonly bool[] Empty = new bool[N * N * N];

        readonly CubeGridConfig config;
        readonly CubeGridData data;
        readonly Dictionary<Vector3Int, PillarLayout> layouts;
        readonly Dictionary<Vector3Int, bool[]> cells = new();
        readonly Dictionary<string, bool[]> shapes = new();
        readonly Dictionary<CellAxis, List<Vector3Int>> lightPixels = new();
        readonly Dictionary<Vector3Int, bool[]> objectCells = new();

        /// <param name="objects">맵에 놓인 기물·시험 모델(<see cref="PlacedObjects"/>). 없으면 지형만 센다.</param>
        public CubeGridVoxels(CubeGridConfig config, CubeGridData data, IEnumerable<GameObject> objects = null)
        {
            this.config = config;
            this.data = data;
            layouts = CubeGridRules.PillarLayouts(data);
            if (objects != null)
                foreach (var placed in objects) AddObject(placed);
        }

        /// <summary>씬에 놓인 기물(<see cref="FixtureBase"/>)과 시험 모델(<see cref="CubeTestModel"/>).</summary>
        public static List<GameObject> PlacedObjects(UnityEngine.SceneManagement.Scene scene)
        {
            var placed = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var fixture in root.GetComponentsInChildren<FixtureBase>(true)) placed.Add(fixture.gameObject);
                foreach (var model in root.GetComponentsInChildren<CubeTestModel>(true)) placed.Add(model.gameObject);
            }
            return placed;
        }

        // 놓인 오브젝트의 켜진 모델 상자를 월드 자리 그대로 칸별 픽셀에 더한다(칸을 넘는 부분은 그 칸에).
        void AddObject(GameObject placed)
        {
            foreach (var boxes in placed.GetComponentsInChildren<VoxelBoxes>(false))
            {
                var toWorld = boxes.transform.localToWorldMatrix;
                foreach (var box in boxes.Boxes)
                {
                    Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
                        var world = toWorld.MultiplyPoint3x4(corner);
                        var p = new Vector3(world.x + 0.5f, world.y, world.z + 0.5f) * N; // 격자 전체 픽셀(칸 (0,0,0)의 바닥 모서리가 0)
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                    var from = Vector3Int.RoundToInt(min);
                    var to = Vector3Int.RoundToInt(max);
                    for (int z = from.z; z < to.z; z++)
                    for (int y = from.y; y < to.y; y++)
                    for (int x = from.x; x < to.x; x++)
                    {
                        var cell = new Vector3Int(FloorDiv(x), FloorDiv(y), FloorDiv(z));
                        if (!objectCells.TryGetValue(cell, out var mask)) objectCells[cell] = mask = new bool[N * N * N];
                        mask[Index(new Vector3Int(x, y, z) - cell * N)] = true;
                    }
                }
            }
        }

        static int FloorDiv(int a) => a >= 0 ? a / N : -((-a + N - 1) / N);

        /// <summary>칸의 지형 픽셀(칸 기준 0~15)이 찼는가.</summary>
        public bool Solid(Vector3Int cell, Vector3Int pixel) => Terrain(cell)[Index(pixel)];

        /// <summary>조명이 위 칸 아랫면에 닿고 지형과 겹치지 않는가, 올라갈 높이(픽셀).</summary>
        public bool FitLight(Vector3Int cell, CellAxis axis, out int height) =>
            CubeGridRules.FitLight(LightPixels(axis), p => Solid(cell, p), p => Solid(cell + Vector3Int.up, p), out height);

        /// <summary>조명 칸 기준 조명 픽셀(올리기 전, 칸 맨 위에 붙은 모양).</summary>
        public List<Vector3Int> LightPixels(CellAxis axis)
        {
            if (lightPixels.TryGetValue(axis, out var pixels)) return pixels;
            var mask = Shape("light" + axis, config.lightPrefab, CubeGridRules.LightRotation(axis), null);
            pixels = new List<Vector3Int>();
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) pixels.Add(new Vector3Int(i % N, i / N % N, i / (N * N)));
            lightPixels[axis] = pixels;
            return pixels;
        }

        // 칸의 지형과 놓인 오브젝트를 합친 픽셀.
        bool[] Terrain(Vector3Int cell)
        {
            if (cells.TryGetValue(cell, out var mask)) return mask;
            mask = TerrainOnly(cell);
            if (objectCells.TryGetValue(cell, out var objects))
            {
                var merged = (bool[])objects.Clone();
                for (int i = 0; i < merged.Length; i++) merged[i] |= mask[i];
                mask = merged;
            }
            cells[cell] = mask;
            return mask;
        }

        bool[] TerrainOnly(Vector3Int cell)
        {
            var mask = Empty;
            if (CubeGridRules.HasBlock(data, cell)) mask = Shape("block", config.blockPrefab, Quaternion.identity, null);
            else if (data.stairs.FindIndex(s => s.cell == cell) is int stair and >= 0)
            {
                int turn = CubeGridRules.Turn(data.stairs[stair].turn);
                mask = Shape("stair" + turn, config.stairPrefab, CubeGridRules.StairRotation(turn), null);
            }
            else if (layouts.TryGetValue(cell, out var layout) && config.pillarPrefab != null && config.pillarPrefab.TryGetComponent(out PillarView view))
            {
                var key = $"pillar{(int)layout.pose}{(int)layout.north}{(int)layout.east}{(int)layout.south}{(int)layout.west}";
                mask = Shape(key, config.pillarPrefab, Quaternion.identity, view.ModelsFor(layout).ToList());
            }
            return mask;
        }

        // 프리팹(루트 = 칸 가운데)을 돌려 놓았을 때 찬 픽셀. models가 있으면 그 아래 모델만 센다.
        bool[] Shape(string key, GameObject prefab, Quaternion rotation, List<GameObject> models)
        {
            if (shapes.TryGetValue(key, out var mask)) return mask;
            mask = new bool[N * N * N];
            shapes[key] = mask;
            if (prefab == null) return mask;
            var root = prefab.transform;
            foreach (var boxes in prefab.GetComponentsInChildren<VoxelBoxes>(true))
            {
                if (models != null && !models.Any(model => boxes.transform == model.transform || boxes.transform.IsChildOf(model.transform))) continue;
                var toRoot = root.worldToLocalMatrix * boxes.transform.localToWorldMatrix;
                foreach (var box in boxes.Boxes)
                {
                    Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
                        var p = (rotation * toRoot.MultiplyPoint3x4(corner) + Vector3.one * 0.5f) * N; // 칸 바닥 모서리 기준 픽셀
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                    var from = Vector3Int.RoundToInt(min);
                    var to = Vector3Int.RoundToInt(max);
                    for (int z = Mathf.Max(from.z, 0); z < Mathf.Min(to.z, N); z++)
                    for (int y = Mathf.Max(from.y, 0); y < Mathf.Min(to.y, N); y++)
                    for (int x = Mathf.Max(from.x, 0); x < Mathf.Min(to.x, N); x++)
                        mask[x + N * (y + N * z)] = true;
                }
            }
            return mask;
        }

        static int Index(Vector3Int p) => p.x + N * (p.y + N * p.z);
    }
}
