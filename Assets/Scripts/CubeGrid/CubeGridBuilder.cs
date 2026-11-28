using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 격자 빌더: 데이터의 블록 칸마다 블록 프리팹을, 계단마다 계단 프리팹을, 기둥마다 기둥 프리팹을, 천장 조명·상자·난간·계기판마다 그 프리팹을 큐브 격자 아래 '만든 부분'의 종류별 묶음에 놓는다.
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
            BuildCrates(Child(parts, "상자"), grid.Data, config.cratePrefab);
            BuildSidePieces(Child(parts, "난간"), grid.Data.railings, config.railingPrefab, "Railing", CubeGridRules.RailingEastPosts(grid.Data));
            BuildSidePieces(Child(parts, "계기판"), grid.Data.panels, config.panelPrefab, "Panel", null);

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

        // 천장 조명: 칸마다 방향대로 돌려 놓고(모델은 칸 맨 위), 빛 색(VoxPaint 칠하는 색)을 맞춘다.
        static void BuildLights(Transform parent, CubeGridData data, CubeGridConfig config)
        {
            var wanted = new Dictionary<Vector3Int, Quaternion>();
            var found = new Dictionary<Vector3Int, CubeLight>();
            foreach (var light in data.lights)
                if (wanted.TryAdd(light.cell, CubeGridRules.LightRotation(light.axis))) found[light.cell] = light;
            BuildKind(parent, wanted, config.lightPrefab, "Light");
            foreach (Transform part in parent)
            {
                var paint = part.GetComponentInChildren<VoxPaint>(true);
                if (paint == null || !found.TryGetValue(CubeGridRules.CellAt(part.position), out var light)) continue;
                var position = new Vector3(0f, -0.5f, 0f); // 모델 원점은 칸 바닥 가운데
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

        // 상자: 반 칸마다 하나, 루트는 상자 바닥 가운데.
        static void BuildCrates(Transform parent, CubeGridData data, GameObject prefab)
        {
            var wanted = new List<(Vector3, Quaternion, string)>();
            foreach (var half in new HashSet<Vector3Int>(data.crates)) wanted.Add((CubeGridRules.CrateBottom(half), Quaternion.identity, $"Crate {half.x},{half.y},{half.z}"));
            BuildAt(parent, wanted, prefab);
        }

        // 옆면 장식(난간·계기판): 칸 가운데에 옆면대로 돌려 놓는다. 난간이면 동쪽 끝 기둥을 켜고 끈다(eastPosts).
        static void BuildSidePieces(Transform parent, List<CubeSidePiece> pieces, GameObject prefab, string namePrefix, Dictionary<CubeSidePiece, bool> eastPosts)
        {
            var unique = new List<CubeSidePiece>(new HashSet<CubeSidePiece>(pieces));
            var wanted = new List<(Vector3, Quaternion, string)>();
            foreach (var piece in unique)
                wanted.Add((CubeGridRules.CellCenter(piece.cell), CubeGridRules.SideRotation(piece.side), $"{namePrefix} {piece.cell.x},{piece.cell.y},{piece.cell.z} {piece.side}"));
            var parts = BuildAt(parent, wanted, prefab);
            if (eastPosts == null) return;
            for (int i = 0; i < unique.Count; i++)
                if (parts[i].TryGetComponent(out RailingView view)) SetEastPost(view, !eastPosts.TryGetValue(unique[i], out bool shown) || shown);
        }

        static void SetEastPost(RailingView view, bool shown)
        {
            if (view.EastPostShown == shown && (view.EastPost == null || view.EastPost.activeSelf == shown)) return;
#if UNITY_EDITOR
            if (Recording)
            {
                Undo.RecordObject(view, UndoName);
                if (view.EastPost != null) Undo.RecordObject(view.EastPost, UndoName);
            }
#endif
            view.EastPostShown = shown;
#if UNITY_EDITOR
            if (Recording)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                if (view.EastPost != null) PrefabUtility.RecordPrefabInstancePropertyModifications(view.EastPost);
            }
#endif
        }

        // 자리·방향으로 맞추는 종류(한 칸에 여럿): 같은 자리·방향의 것은 남기고, 나머지는 지우고, 없는 것만 만든다. 결과는 wanted 차례.
        static List<Transform> BuildAt(Transform parent, List<(Vector3 position, Quaternion rotation, string name)> wanted, GameObject prefab)
        {
            var result = new Transform[wanted.Count];
            var stale = new List<GameObject>();
            foreach (Transform part in parent)
            {
                int match = -1;
                for (int i = 0; i < wanted.Count; i++)
                    if (result[i] == null && (part.position - wanted[i].position).sqrMagnitude < 1e-6f && Quaternion.Angle(part.rotation, wanted[i].rotation) < 1f)
                    {
                        match = i;
                        break;
                    }
                if (match >= 0) result[match] = part;
                else stale.Add(part.gameObject);
            }
            foreach (var part in stale) Destroy(part);
            for (int i = 0; i < wanted.Count; i++)
            {
                if (result[i] != null) continue;
                var part = Spawn(prefab, parent);
                part.name = wanted[i].name;
                part.transform.SetPositionAndRotation(wanted[i].position, wanted[i].rotation);
                RegisterCreated(part);
                result[i] = part.transform;
            }
            return new List<Transform>(result);
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

        /// <summary>블록과 계단, 기둥, 조명, 상자·난간·계기판, 코팅을 모두 지우고 다시 만든다(프리팹을 바꾼 뒤).</summary>
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
