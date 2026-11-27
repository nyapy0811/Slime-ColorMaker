using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 격자 빌더: 데이터의 블록 칸마다 블록 프리팹을, 계단마다 계단 프리팹을, 기둥마다 기둥 프리팹을, 천장 조명마다 조명 프리팹을 큐브 격자 아래 '만든 부분'의 종류별 묶음에 놓는다.
    /// 기둥의 놓인 모양·커넥터는 지을 때마다 옆 칸으로 계산해(<see cref="CubeGridRules.PillarLayouts"/>) 칸마다 <see cref="PillarView.Layout"/>로 맞춘다(값이 바뀌어도 다시 만들지 않음).
    /// 묶음은 '만든 부분' 바로 아래 한 단계다(지형 면 숨기기 <see cref="CubeFaceCuller"/>가 묶음 아래 오브젝트를 칸 하나로 본다).
    /// 블록 윗면 코팅은 '코팅' 오브젝트의 <see cref="SlipperyCoatingView"/>가 데이터에서 다시 그린다.
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

            BuildPillars(Child(parts, "기둥"), CubeGridRules.PillarLayouts(grid.Data), config.pillarPrefab);
            BuildLights(Child(parts, "조명"), grid.Data, config);

            var coating = Child(parts, "코팅");
            if (!coating.TryGetComponent(out SlipperyCoatingView view)) view = coating.gameObject.AddComponent<SlipperyCoatingView>();
            view.Settings = config.coatingSettings;
            view.Rebuild();
        }

        // 기둥: 칸마다 프리팹 하나를 두고 겉모습 값만 맞춘다.
        static void BuildPillars(Transform parent, Dictionary<Vector3Int, PillarLayout> layouts, GameObject prefab)
        {
            var wanted = new Dictionary<Vector3Int, Quaternion>();
            foreach (var cell in layouts.Keys) wanted.Add(cell, Quaternion.identity);
            BuildKind(parent, wanted, prefab, "Pillar");
            foreach (Transform part in parent)
                if (part.TryGetComponent(out PillarView view) && layouts.TryGetValue(CubeGridRules.CellAt(part.position), out var layout))
                    SetPillarLayout(view, layout);
        }

        static void SetPillarLayout(PillarView view, PillarLayout layout)
        {
            if (view.Layout.Equals(layout) && !view.LayoutMismatch()) return;
#if UNITY_EDITOR
            if (Recording)
            {
                Undo.RecordObject(view, UndoName);
                foreach (var model in view.Models) if (model != null) Undo.RecordObject(model, UndoName);
                view.Layout = layout;
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                foreach (var model in view.Models) if (model != null) PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                return;
            }
#endif
            view.Layout = layout;
        }

        // 천장 조명: 칸마다 방향대로 돌려 놓고, 위 칸 아랫면 높이까지 모델을 올리고(CubeGridVoxels, 맞지 않으면 칸 맨 위), 빛 색(VoxPaint 칠하는 색)을 맞춘다.
        static void BuildLights(Transform parent, CubeGridData data, CubeGridConfig config)
        {
            var wanted = new Dictionary<Vector3Int, Quaternion>();
            var found = new Dictionary<Vector3Int, CubeLight>();
            foreach (var light in data.lights)
                if (wanted.TryAdd(light.cell, CubeGridRules.LightRotation(light.axis))) found[light.cell] = light;
            BuildKind(parent, wanted, config.lightPrefab, "Light");
            var voxels = new CubeGridVoxels(config, data, CubeGridVoxels.PlacedObjects(parent.gameObject.scene));
            foreach (Transform part in parent)
            {
                var paint = part.GetComponentInChildren<VoxPaint>(true);
                if (paint == null || !found.TryGetValue(CubeGridRules.CellAt(part.position), out var light)) continue;
                int height = voxels.FitLight(light.cell, light.axis, out int fit) ? fit : 0;
                var position = new Vector3(0f, -0.5f + height / (float)CubeGridRules.CellPixels, 0f); // 모델 원점은 칸 바닥 가운데
                bool move = (paint.transform.localPosition - position).sqrMagnitude > 1e-8f;
                if (!move && paint.Color == light.color) continue;
#if UNITY_EDITOR
                if (Recording)
                {
                    Undo.RecordObject(paint, UndoName);
                    Undo.RecordObject(paint.transform, UndoName);
                }
#endif
                paint.Color = light.color;
                paint.transform.localPosition = position;
#if UNITY_EDITOR
                if (Recording)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(paint);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(paint.transform);
                }
#endif
            }
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

        /// <summary>블록과 계단, 기둥, 조명, 코팅을 모두 지우고 다시 만든다(프리팹을 바꾼 뒤).</summary>
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
