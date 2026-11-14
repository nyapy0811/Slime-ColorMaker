using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 격자 빌더: 데이터의 블록 칸마다 블록 프리팹을, 계단마다 계단 프리팹을, 기둥마다 그 칸 모양의 기둥 프리팹을 큐브 격자 아래 '만든 부분'의 종류별 묶음에 놓는다.
    /// 기둥 모양은 지을 때마다 그룹으로 계산하므로(<see cref="CubeGridRules.PillarShapes"/>) 모양이 바뀐 칸은 다른 묶음으로 옮겨진다.
    /// 에디터(Play 밖)에서는 프리팹 연결을 지키고 Undo에 기록하고, 런타임(나중의 UGC 맵)에는 보통의 Instantiate·Destroy다.
    /// 지금 있는 것은 자리와 방향으로 데이터와 비교해 남은 칸은 그대로 두고, 없어지거나 돌아간 칸은 지우고, 새 칸만 만든다.
    /// </summary>
    public static class CubeGridBuilder
    {
        public const string GeneratedPartsName = "만든 부분";
        const string UndoName = "블록 만들기";

        public static void Build(CubeGrid grid, CubeGridConfig config)
        {
            var parts = Child(grid.transform, GeneratedPartsName);

            var blocks = new Dictionary<Vector3Int, Quaternion>();
            foreach (var cell in grid.Data.blocks) blocks.TryAdd(cell, Quaternion.identity);
            BuildKind(Child(parts, "블록"), blocks, config.blockPrefab, "Block");

            var stairs = new Dictionary<Vector3Int, Quaternion>();
            foreach (var stair in grid.Data.stairs) stairs.TryAdd(stair.cell, CubeGridRules.StairRotation(stair.turn));
            BuildKind(Child(parts, "계단"), stairs, config.stairPrefab, "Stair");

            var pillars = Child(parts, "기둥");
            var shapes = CubeGridRules.PillarShapes(grid.Data);
            BuildPillars(Child(pillars, "위아래"), shapes, PillarShape.Full, config.pillarPrefab, "Pillar");
            BuildPillars(Child(pillars, "아래"), shapes, PillarShape.Base, config.pillarBasePrefab, "PillarBase");
            BuildPillars(Child(pillars, "중간"), shapes, PillarShape.Shaft, config.pillarShaftPrefab, "PillarShaft");
            BuildPillars(Child(pillars, "위"), shapes, PillarShape.Top, config.pillarTopPrefab, "PillarTop");
        }

        static void BuildPillars(Transform parent, Dictionary<Vector3Int, PillarShape> shapes, PillarShape shape, GameObject prefab, string namePrefix)
        {
            var wanted = new Dictionary<Vector3Int, Quaternion>();
            foreach (var (cell, cellShape) in shapes)
                if (cellShape == shape) wanted.Add(cell, Quaternion.identity);
            BuildKind(parent, wanted, prefab, namePrefix);
        }

        // 한 종류: 칸 중심에 놓고, 자리와 방향이 데이터와 같은 것만 남긴다.
        static void BuildKind(Transform parent, Dictionary<Vector3Int, Quaternion> wanted, GameObject prefab, string namePrefix)
        {
            var built = new HashSet<Vector3Int>();
            var stale = new List<GameObject>();
            foreach (Transform part in parent)
            {
                var cell = CubeGridRules.CellAt(part.position);
                if (wanted.TryGetValue(cell, out var rotation) && Quaternion.Angle(part.rotation, rotation) < 1f && built.Add(cell)) continue;
                stale.Add(part.gameObject);
            }
            foreach (var part in stale) Destroy(part);

            foreach (var (cell, rotation) in wanted)
            {
                if (!built.Add(cell)) continue;
                var part = Spawn(prefab, parent);
                part.name = $"{namePrefix} {cell.x},{cell.y},{cell.z}";
                part.transform.SetPositionAndRotation(CubeGridRules.CellCenter(cell), rotation);
                RegisterCreated(part);
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            RegisterCreated(child.gameObject);
            return child;
        }

        /// <summary>블록과 계단, 기둥을 모두 지우고 다시 만든다(프리팹을 바꾼 뒤).</summary>
        public static void Rebuild(CubeGrid grid, CubeGridConfig config)
        {
            var parts = grid.transform.Find(GeneratedPartsName);
            if (parts != null) Destroy(parts.gameObject);
            Build(grid, config);
        }

        static bool Recording
        {
#if UNITY_EDITOR
            get => !Application.isPlaying;
#else
            get => false;
#endif
        }

        static GameObject Spawn(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            if (Recording) return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
            return Object.Instantiate(prefab, parent);
        }

        static void RegisterCreated(GameObject created)
        {
#if UNITY_EDITOR
            if (Recording) Undo.RegisterCreatedObjectUndo(created, UndoName);
#endif
        }

        static void Destroy(GameObject target)
        {
#if UNITY_EDITOR
            if (Recording)
            {
                Undo.DestroyObjectImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }
    }
}
