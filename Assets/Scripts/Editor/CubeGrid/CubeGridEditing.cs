using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorMaze
{
    /// <summary>
    /// 큐브 맵 편집 창과 씬 뷰 도구가 함께 쓰는 편집 동작(v1 MazeGeneratorEditor 방식을 v2 구조로).
    /// 고른 붓으로 칸에 블록·계단·기물·시험 모델을 놓고 지운다. 한 칸에는 하나만 두며, 블록과 계단은 큐브 격자 데이터에, 기물과 시험 모델은
    /// 씬의 오브젝트로 칸 바닥 가운데에 놓는다(맵 안 id는 기물을 놓을 때 고침). 계단은 씬 카메라가 보는 쪽으로 오르게, 차단기는 씬 카메라가
    /// 보는 쪽으로 지나가게(앞이 그쪽) 놓고, '돌리기'로 90°씩 더 돌린다(둘이 같은 돌림 값을 씀). 편집 한 번이 Undo 하나다.
    /// </summary>
    public static class CubeGridEditing
    {
        public enum Brush { Block, Stair, Checkpoint, ColorSource, Goal, BoomGate, TestModel }

        public static readonly string[] BrushNames = { "블록", "계단", "체크포인트", "색 원천", "목적지", "차단기", "시험 모델" };

        const string TestModelsFolder = "TestModels";

        /// <summary>시험 모델 붓으로 놓을 모델(.vox나 프리팹, 기준점은 바닥면 가운데).</summary>
        public static GameObject TestModelAsset
        {
            get => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(SessionState.GetString("ColorMaze.CubeGrid.TestModel", "")));
            set
            {
                SessionState.SetString("ColorMaze.CubeGrid.TestModel", value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "");
                BrushChanged?.Invoke();
            }
        }

        public static Brush SelectedBrush
        {
            get => (Brush)SessionState.GetInt("ColorMaze.CubeGrid.Brush", 0);
            set
            {
                SessionState.SetInt("ColorMaze.CubeGrid.Brush", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>계단·차단기를 카메라 방향에서 90°씩 더 돌린 횟수(창의 '돌리기').</summary>
        public static int StairTurnOffset
        {
            get => SessionState.GetInt("ColorMaze.CubeGrid.StairTurn", 0);
            set
            {
                SessionState.SetInt("ColorMaze.CubeGrid.StairTurn", CubeGridRules.Turn(value));
                BrushChanged?.Invoke();
            }
        }

        /// <summary>지금 놓을 계단·차단기의 방향: 씬 카메라가 보는 쪽 + 돌린 횟수.</summary>
        public static int StairTurn(Vector3 cameraForward) => CubeGridRules.Turn(CubeGridRules.TurnFacing(cameraForward) + StairTurnOffset);

        public static event Action BrushChanged;

        public static CubeGridConfig FindConfig()
        {
            var guids = AssetDatabase.FindAssets("t:CubeGridConfig");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<CubeGridConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>새 맵의 큐브 격자를 만든다. 하늘·조명은 MapRoot의 맵 환경이 맡는다(2026-11-08).</summary>
        public static CubeGrid CreateGrid()
        {
            var go = new GameObject("CubeGrid");
            Undo.RegisterCreatedObjectUndo(go, "큐브 격자 만들기");
            Selection.activeGameObject = go;
            return go.AddComponent<CubeGrid>();
        }

        /// <summary>씬의 기물과 시험 모델이 든 칸(칸 바닥 가운데에 놓인 자리 기준).</summary>
        public static Dictionary<Vector3Int, GameObject> ObjectCells(CubeGrid grid)
        {
            var cells = new Dictionary<Vector3Int, GameObject>();
            var scene = grid.gameObject.scene;
            foreach (var fixture in FixtureIdAssigner.FixturesIn(scene))
                cells.TryAdd(CubeGridRules.CellAt(fixture.transform.position), fixture.gameObject);
            foreach (var model in TestModelsIn(scene))
                cells.TryAdd(CubeGridRules.CellAt(model.transform.position), model.gameObject);
            return cells;
        }

        public static IEnumerable<CubeTestModel> TestModelsIn(UnityEngine.SceneManagement.Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeTestModel>(true));

        public static bool IsFilled(CubeGrid grid, Dictionary<Vector3Int, GameObject> objects, Vector3Int cell) =>
            CubeGridRules.IsOccupied(grid.Data, cell) || objects.ContainsKey(cell);

        /// <summary>빈 칸이면 고른 붓으로 놓는다. 계단은 <paramref name="stairTurn"/> 방향으로 오르고, 차단기는 그 방향으로 지나간다.</summary>
        public static void Place(CubeGrid grid, Vector3Int cell, int stairTurn) => PlaceAll(grid, new[] { cell }, "칸에 놓기", stairTurn: stairTurn);

        /// <summary>범위의 빈 칸에 블록을 놓는다(범위 놓기는 블록만).</summary>
        public static void PlaceBlocks(CubeGrid grid, IEnumerable<Vector3Int> cells) => PlaceAll(grid, cells, "블록 범위 놓기", blocksOnly: true);

        /// <summary>칸의 블록·계단·기물·시험 모델을 지운다. 시작 지점(MapRoot 안, 원점)은 지우지 않는다.</summary>
        public static void Remove(CubeGrid grid, IEnumerable<Vector3Int> cells)
        {
            var config = RequireConfig();
            if (config == null) return;
            var objects = ObjectCells(grid);
            Edit(grid, "칸 지우기", () =>
            {
                foreach (var cell in cells)
                {
                    CubeGridRules.Remove(grid.Data, cell);
                    if (objects.Remove(cell, out var placed) && !placed.TryGetComponent<StartPoint>(out _)) Undo.DestroyObjectImmediate(placed);
                }
            }, config);
        }

        public static void Rebuild(CubeGrid grid)
        {
            var config = RequireConfig();
            if (config == null) return;
            Edit(grid, "블록 다시 만들기", () => { }, config, fullRebuild: true);
        }

        static void PlaceAll(CubeGrid grid, IEnumerable<Vector3Int> cells, string undoName, bool blocksOnly = false, int stairTurn = 0)
        {
            var config = RequireConfig();
            if (config == null) return;
            var brush = blocksOnly ? Brush.Block : SelectedBrush;
            var prefab = PrefabFor(config, brush);
            if (prefab == null)
            {
                Debug.LogError(brush == Brush.TestModel
                    ? "[큐브 맵 편집] 시험 모델을 고르지 않았다(큐브 맵 편집 창의 붓 아래 칸)."
                    : $"[큐브 맵 편집] 큐브 맵 설정에 '{BrushNames[(int)brush]}' 프리팹이 없다.", config);
                return;
            }
            var objects = ObjectCells(grid);
            Edit(grid, undoName, () =>
            {
                foreach (var cell in cells)
                {
                    if (IsFilled(grid, objects, cell)) continue;
                    if (brush == Brush.Block) CubeGridRules.AddBlock(grid.Data, cell);
                    else if (brush == Brush.Stair) CubeGridRules.AddStair(grid.Data, cell, stairTurn);
                    else if (brush == Brush.TestModel) objects[cell] = PlaceTestModel(grid, prefab, cell);
                    else objects[cell] = PlaceFixture(grid, prefab, brush, cell, stairTurn).gameObject;
                }
                if (IsFixture(brush)) FixtureIdAssigner.FixIds(grid.gameObject.scene);
            }, config);
        }

        static FixtureBase PlaceFixture(CubeGrid grid, GameObject prefab, Brush brush, Vector3Int cell, int turn)
        {
            var parent = FolderFor(grid.gameObject.scene, brush);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "기물 놓기");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "기물 놓기");
            var rotation = brush == Brush.BoomGate ? CubeGridRules.StairRotation(turn) : Quaternion.identity;
            go.transform.SetPositionAndRotation(CubeGridRules.CellBottom(cell), rotation);
            Selection.activeGameObject = go;
            return go.GetComponent<FixtureBase>();
        }

        // 시험 모델: 한 칸 상자 콜라이더를 단 바깥 오브젝트 아래에 모델을 둔다(모델 모양과 상관없이 충돌은 한 칸).
        static GameObject PlaceTestModel(CubeGrid grid, GameObject model, Vector3Int cell)
        {
            var scene = grid.gameObject.scene;
            var holder = new GameObject($"Test {model.name} {cell.x},{cell.y},{cell.z}");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, scene);
            Undo.RegisterCreatedObjectUndo(holder, "시험 모델 놓기");
            Undo.SetTransformParent(holder.transform, FolderFor(scene, Brush.TestModel), "시험 모델 놓기");
            holder.transform.SetPositionAndRotation(CubeGridRules.CellBottom(cell), Quaternion.identity);
            var box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = Vector3.one;
            holder.AddComponent<CubeTestModel>();
            var view = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.transform);
            view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Selection.activeGameObject = holder;
            return holder;
        }

        // 기물은 종류별 묶음 오브젝트 아래에 둔다(시험 맵의 구성과 같음).
        static Transform FolderFor(UnityEngine.SceneManagement.Scene scene, Brush brush)
        {
            string name = brush switch { Brush.Checkpoint => "Checkpoints", Brush.ColorSource => "ColorSources", Brush.Goal => "Activators", Brush.BoomGate => "Gates", Brush.TestModel => TestModelsFolder, _ => null };
            if (name == null) return null;
            var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) return existing.transform;
            var folder = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(folder, scene);
            Undo.RegisterCreatedObjectUndo(folder, "기물 묶음 만들기");
            return folder.transform;
        }

        static bool IsFixture(Brush brush) => brush is not (Brush.Block or Brush.Stair or Brush.TestModel);

        static GameObject PrefabFor(CubeGridConfig config, Brush brush) => brush switch
        {
            Brush.Stair => config.stairPrefab,
            Brush.TestModel => TestModelAsset,
            Brush.Checkpoint => config.checkpointPrefab,
            Brush.ColorSource => config.colorSourcePrefab,
            Brush.Goal => config.goalPrefab,
            Brush.BoomGate => config.boomGatePrefab,
            _ => config.blockPrefab,
        };

        static CubeGridConfig RequireConfig()
        {
            var config = FindConfig();
            if (config == null) Debug.LogError("[큐브 맵 편집] 큐브 맵 설정 애셋(CubeGridConfig)이 없다. Assets/Data에 만든다.");
            return config;
        }

        static void Edit(CubeGrid grid, string undoName, Action change, CubeGridConfig config, bool fullRebuild = false)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(grid, undoName);
            change();
            if (fullRebuild) CubeGridBuilder.Rebuild(grid, config);
            else CubeGridBuilder.Build(grid, config);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        }
    }
}
