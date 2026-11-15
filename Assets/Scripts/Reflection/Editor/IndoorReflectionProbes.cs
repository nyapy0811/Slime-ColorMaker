using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SlimeColorMaker
{
    /// <summary>
    /// ② 실내 반사 탐침(<see cref="ReflectionSettings"/>): 큐브 격자 데이터에서 덮인 빈 칸 덩어리(방·굴·처마 밑, <see cref="CubeGridRules.CoveredRegions"/>)를 찾아
    /// 덩어리마다 구운 반사 탐침(상자 투영)을 큐브 격자 아래 '반사 탐침' 묶음에 둔다. 그 안의 반사가 하늘 대신 그 공간을 비춘다.
    /// 조명 굽기(<see cref="MapLightBaker"/>)가 굽기 전에 맞추고(배치가 같으면 그대로) 함께 굽는다. 배치는 조명 지문에도 들어간다.
    /// </summary>
    public static class IndoorReflectionProbes
    {
        const string GroupName = "반사 탐침";
        const float Margin = 0.5f; // 탐침 범위를 덩어리 상자보다 반 칸 넓힌다(벽·바닥·천장 안쪽 면이 범위 안에 들도록)

        /// <summary>씬의 탐침을 지금 배치에 맞춘다. 바꿨으면 씬을 바뀐 것으로 표시한다.</summary>
        public static void Update(Scene scene, ReflectionSettings settings)
        {
            var grid = FindGrid(scene);
            if (grid == null) return;
            var wanted = Layout(grid, settings);
            var group = grid.transform.Find(GroupName);
            var existing = group != null ? group.GetComponentsInChildren<ReflectionProbe>(true) : Array.Empty<ReflectionProbe>();
            float blend = settings != null ? settings.indoorBlendDistance : 1f;
            if (existing.Length == wanted.Count && existing.Zip(wanted, Matches).All(same => same))
            {
                // 섞는 거리는 구운 그림과 상관없어 탐침을 그대로 두고 값만 맞춘다(다시 만들면 구운 그림이 끊긴다).
                bool changed = false;
                foreach (var probe in existing)
                    if (!Mathf.Approximately(probe.blendDistance, blend)) { probe.blendDistance = blend; changed = true; }
                if (changed) EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            if (group != null) Object.DestroyImmediate(group.gameObject);
            if (wanted.Count > 0)
            {
                group = new GameObject(GroupName).transform;
                group.SetParent(grid.transform, false);
                for (int i = 0; i < wanted.Count; i++)
                {
                    var spec = wanted[i];
                    var go = new GameObject($"{GroupName} {i + 1}");
                    go.transform.SetParent(group, false);
                    go.transform.position = spec.position;
                    var probe = go.AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Baked;
                    probe.boxProjection = true;
                    probe.size = spec.size;
                    probe.center = spec.boxCenter - spec.position;
                    probe.resolution = spec.resolution;
                    probe.blendDistance = blend;
                    probe.nearClipPlane = 0.05f;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>조명 지문에 넣을 배치 줄.</summary>
        public static IEnumerable<string> FingerprintLines(Scene scene, ReflectionSettings settings)
        {
            var grid = FindGrid(scene);
            return grid == null ? Enumerable.Empty<string>() : Layout(grid, settings).Select(spec => $"indoor probe {spec}");
        }

        static bool Matches(ReflectionProbe probe, (Vector3 position, Vector3 boxCenter, Vector3 size, int resolution) spec) =>
            probe.mode == ReflectionProbeMode.Baked && probe.resolution == spec.resolution
            && (probe.transform.position - spec.position).sqrMagnitude < 1e-6f
            && (probe.size - spec.size).sqrMagnitude < 1e-6f
            && (probe.center - (spec.boxCenter - spec.position)).sqrMagnitude < 1e-6f;

        // 덩어리마다 탐침 자리(덩어리 가운데 칸의 중심)와 범위(덩어리 상자 + 여유). 큐브 격자는 원점이라 칸 좌표가 월드 좌표다.
        static List<(Vector3 position, Vector3 boxCenter, Vector3 size, int resolution)> Layout(CubeGrid grid, ReflectionSettings settings)
        {
            var layout = new List<(Vector3, Vector3, Vector3, int)>();
            int minCells = settings != null ? settings.indoorMinCells : 4;
            if (minCells <= 0) return layout;
            int resolution = Mathf.ClosestPowerOfTwo(settings != null ? settings.indoorResolution : 64);
            foreach (var region in CubeGridRules.CoveredRegions(grid.Data, minCells))
            {
                var low = CubeGridRules.CellBottom(region.min) - new Vector3(0.5f, 0f, 0.5f) - Vector3.one * Margin;
                var high = CubeGridRules.CellBottom(region.max) + new Vector3(0.5f, 1f, 0.5f) + Vector3.one * Margin;
                layout.Add((CubeGridRules.CellCenter(region.center), (low + high) * 0.5f, high - low, resolution));
            }
            return layout;
        }

        static CubeGrid FindGrid(Scene scene) =>
            scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<CubeGrid>(true)).FirstOrDefault(grid => grid != null);
    }
}
