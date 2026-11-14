using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵 편집 창과 씬 뷰 도구가 함께 쓰는 편집 동작(v1 MazeGeneratorEditor 방식을 v2 구조로).
    /// 고른 붓으로 칸에 블록·계단·기둥·기물·시험 모델을 놓고 지운다. 한 칸에는 하나만 두며, 블록과 계단, 기둥은 큐브 격자 데이터에, 기물과 시험 모델은
    /// 씬의 오브젝트로 칸 바닥 가운데에 놓는다(맵 안 id는 기물을 놓을 때 고침). 계단은 씬 카메라가 보는 쪽으로 오르게, 차단기는 씬 카메라가
    /// 보는 쪽으로 지나가게(앞이 그쪽) 놓고, '돌리기'로 90°씩 더 돌린다(둘이 같은 돌림 값을 씀). 편집 한 번이 Undo 하나다.
    /// </summary>
    public static class CubeGridEditing
    {
        public enum Brush { Block, Stair, Checkpoint, ColorSource, Goal, BoomGate, Barrier, ConveyorBelt, Pillar, Coating, TestModel }

        /// <summary>방벽을 놓을 면: 옆(카메라 쪽 옆면, 돌리기로 90°씩), 위(칸 윗면), 아래(칸 바닥면).</summary>
        public enum BarrierSide { Side, Up, Down }

        public static readonly string[] BarrierSideNames = { "옆", "위", "아래" };

        public static readonly string[] BrushNames = { "블록", "계단", "체크포인트", "색 원천", "목적지", "차단기", "방벽", "컨베이어", "기둥", "코팅", "시험 모델" };

        const string TestModelsFolder = "TestModels";

        /// <summary>컨베이어 붓인가. 블록 자리에 깔리고 씬 카메라가 보는 쪽으로 나가게 놓인다(모양은 <see cref="SelectedConveyorShape"/>).</summary>
        public static bool IsConveyor(Brush brush) => brush == Brush.ConveyorBelt;

        /// <summary>
        /// 놓을 컨베이어가 나가는 방향. 들어가는 방향(<paramref name="entryTurn"/>, 씬 카메라가 보는 쪽 + 돌림, 2026-11-14)에서
        /// 우회전은 90° 오른쪽(+1), 좌회전은 왼쪽(−1)으로 꺾여 나가고 직진은 그대로다.
        /// </summary>
        public static int ConveyorExitTurn(int entryTurn) =>
            CubeGridRules.Turn(entryTurn + SelectedConveyorShape switch { ConveyorShape.Right => 1, ConveyorShape.Left => -1, _ => 0 });

        /// <summary>창의 컨베이어 모양 줄 순서와 이름: 직진, 좌회전, 우회전.</summary>
        public static readonly ConveyorShape[] ConveyorShapeOrder = { ConveyorShape.Straight, ConveyorShape.Left, ConveyorShape.Right };
        public static readonly string[] ConveyorShapeNames = { "직진", "좌회전", "우회전" };

        /// <summary>놓을 컨베이어의 모양: 직진, 우회전(들어와서 오른쪽으로 꺾여 나감), 좌회전(2026-11-14, 꺾임 붓 둘을 합침).</summary>
        public static ConveyorShape SelectedConveyorShape
        {
            get => (ConveyorShape)SessionState.GetInt("SlimeColorMaker.CubeGrid.ConveyorShape", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.ConveyorShape", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>시험 모델 붓으로 놓을 모델(.vox나 프리팹, 기준점은 바닥면 가운데).</summary>
        public static GameObject TestModelAsset
        {
            get => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(SessionState.GetString("SlimeColorMaker.CubeGrid.TestModel", "")));
            set
            {
                SessionState.SetString("SlimeColorMaker.CubeGrid.TestModel", value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "");
                BrushChanged?.Invoke();
            }
        }

        public static Brush SelectedBrush
        {
            get => (Brush)SessionState.GetInt("SlimeColorMaker.CubeGrid.Brush", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.Brush", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>계단·차단기를 카메라 방향에서 90°씩 더 돌린 횟수(창의 '돌리기').</summary>
        public static int StairTurnOffset
        {
            get => SessionState.GetInt("SlimeColorMaker.CubeGrid.StairTurn", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.StairTurn", CubeGridRules.Turn(value));
                BrushChanged?.Invoke();
            }
        }

        /// <summary>지금 놓을 계단·차단기의 방향: 씬 카메라가 보는 쪽 + 돌린 횟수.</summary>
        public static int StairTurn(Vector3 cameraForward) => CubeGridRules.Turn(CubeGridRules.TurnFacing(cameraForward) + StairTurnOffset);

        /// <summary>코팅 붓의 종류 이름(<see cref="CoatingKind"/>의 기름·얼음 순서).</summary>
        public static readonly string[] CoatingKindNames = { "기름", "얼음" };

        /// <summary>코팅 붓으로 칠할 종류(기름·얼음, 기본 기름).</summary>
        public static CoatingKind SelectedCoatingKind
        {
            get => (CoatingKind)SessionState.GetInt("SlimeColorMaker.CubeGrid.CoatingKind", (int)CoatingKind.Oil);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.CoatingKind", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>코팅 붓의 지름(픽셀 1~16, 원형, 기본 4).</summary>
        public static int CoatingBrushSize
        {
            get => SessionState.GetInt("SlimeColorMaker.CubeGrid.CoatingSize", 4);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.CoatingSize", Mathf.Clamp(value, 1, CubeGridRules.CoatingPixels));
                BrushChanged?.Invoke();
            }
        }

        /// <summary>
        /// 블록 윗면 픽셀들을 칠한다(None이면 지움, 블록이 없는 칸은 건너뜀). 붓 한 번(드래그 한 번)의 Undo 묶음은 씬 뷰 도구가 정한다.
        /// </summary>
        public static void PaintCoating(CubeGrid grid, List<(Vector3Int cell, int x, int z)> pixels, CoatingKind kind)
        {
            var config = RequireConfig();
            if (config == null) return;
            Undo.RecordObject(grid, "코팅 칠하기");
            bool changed = false;
            foreach (var (cell, x, z) in pixels) changed |= CubeGridRules.SetCoating(grid.Data, cell, x, z, kind);
            if (!changed) return;
            var view = grid.GetComponentInChildren<SlipperyCoatingView>(true);
            if (view == null) CubeGridBuilder.Build(grid, config);
            else view.Rebuild();
            EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        }

        /// <summary>놓을 기둥의 설정: 그룹 맨 위 칸에 머리를 둘지(기본 둠).</summary>
        public static bool PillarHasTop
        {
            get => SessionState.GetBool("SlimeColorMaker.CubeGrid.PillarTop", true);
            set
            {
                SessionState.SetBool("SlimeColorMaker.CubeGrid.PillarTop", value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>놓을 기둥의 설정: 그룹 맨 아래 칸에 받침을 둘지(기본 둠).</summary>
        public static bool PillarHasBase
        {
            get => SessionState.GetBool("SlimeColorMaker.CubeGrid.PillarBase", true);
            set
            {
                SessionState.SetBool("SlimeColorMaker.CubeGrid.PillarBase", value);
                BrushChanged?.Invoke();
            }
        }

        public static BarrierSide SelectedBarrierSide
        {
            get => (BarrierSide)SessionState.GetInt("SlimeColorMaker.CubeGrid.BarrierSide", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.BarrierSide", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>
        /// 놓을 방벽의 면. 옆이면 차단기처럼 칸의 카메라 쪽 옆면(<paramref name="turn"/>이 지나가는 방향, 0 = +z이면 남쪽 면), 위·아래는 칸 윗면·바닥면.
        /// </summary>
        public static CellFace BarrierFace(int turn) => SelectedBarrierSide switch
        {
            BarrierSide.Up => CellFace.Up,
            BarrierSide.Down => CellFace.Down,
            _ => CubeGridRules.Turn(turn) switch { 0 => CellFace.South, 1 => CellFace.West, 2 => CellFace.North, _ => CellFace.East },
        };

        public static event Action BrushChanged;

        /// <summary>놓기 전에 값을 정하는 붓의 값 칸(색 원천은 흡수하면 더하는 색, 목적지·차단기·방벽은 요구 색). 없으면 null.</summary>
        public static string ValueField(Brush brush) => brush switch
        {
            Brush.ColorSource => "value",
            Brush.Goal or Brush.BoomGate or Brush.Barrier => "requiredColor",
            _ => null,
        };

        /// <summary>붓으로 놓을 값. 이 세션에서 정한 적이 없으면 그 붓 프리팹의 값이다.</summary>
        public static ColorValue BrushValue(Brush brush)
        {
            string json = SessionState.GetString(BrushValueKey(brush), "");
            if (json.Length > 0) return JsonUtility.FromJson<ColorValue>(json);
            var config = FindConfig();
            var prefab = config != null ? PrefabFor(config, brush) : null;
            return prefab != null && ValueProperty(prefab, brush) is { } property ? ReadColor(property) : default;
        }

        public static void SetBrushValue(Brush brush, ColorValue value)
        {
            SessionState.SetString(BrushValueKey(brush), JsonUtility.ToJson(value));
            BrushChanged?.Invoke();
        }

        static string BrushValueKey(Brush brush) => $"SlimeColorMaker.CubeGrid.Value.{brush}";

        // 오브젝트 루트 컴포넌트 중 붓의 값 칸을 가진 것의 그 칸.
        static SerializedProperty ValueProperty(GameObject go, Brush brush)
        {
            if (ValueField(brush) is not { } field) return null;
            foreach (var component in go.GetComponents<MonoBehaviour>())
                if (new SerializedObject(component).FindProperty(field) is { } property) return property;
            return null;
        }

        static ColorValue ReadColor(SerializedProperty property) => new(
            property.FindPropertyRelative("r").intValue, property.FindPropertyRelative("g").intValue, property.FindPropertyRelative("b").intValue);

        static void WriteColor(SerializedProperty property, ColorValue value)
        {
            property.FindPropertyRelative("r").intValue = value.R;
            property.FindPropertyRelative("g").intValue = value.G;
            property.FindPropertyRelative("b").intValue = value.B;
            property.serializedObject.ApplyModifiedProperties(); // 프리팹 덮어쓰기와 Undo로 남고, OnValidate가 겉모습을 칠한다.
        }

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
                    else if (brush == Brush.Pillar) CubeGridRules.AddPillar(grid.Data, cell, PillarHasTop, PillarHasBase);
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
            // 컨베이어는 turn 방향으로 들어간다. 모델(직선·꺾임)은 −z로 나가므로 나가는 방향에서 반 바퀴 더 돌린다.
            var rotation = brush == Brush.BoomGate ? CubeGridRules.StairRotation(turn)
                : IsConveyor(brush) ? CubeGridRules.StairRotation(ConveyorExitTurn(turn) + 2)
                : Quaternion.identity;
            go.transform.SetPositionAndRotation(CubeGridRules.CellBottom(cell), rotation);
            if (brush == Brush.Barrier && go.TryGetComponent(out CellFaceMount mount))
            {
                mount.Face = BarrierFace(turn);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mount);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mount.Pivot);
            }
            if (ValueProperty(go, brush) is { } value) WriteColor(value, BrushValue(brush));
            // 놓은 기물은 고르지 않는다: 큐브 격자가 골라진 채여야 씬 뷰 도구가 꺼지지 않고 이어서 놓을 수 있다.
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
            return holder;
        }

        // 기물은 종류별 묶음 오브젝트 아래에 둔다(시험 맵의 구성과 같음).
        static Transform FolderFor(UnityEngine.SceneManagement.Scene scene, Brush brush)
        {
            string name = brush switch { Brush.Checkpoint => "Checkpoints", Brush.ColorSource => "ColorSources", Brush.Goal => "Activators", Brush.BoomGate or Brush.Barrier => "Gates", Brush.ConveyorBelt => "Movers", Brush.TestModel => TestModelsFolder, _ => null };
            if (name == null) return null;
            var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) return existing.transform;
            var folder = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(folder, scene);
            Undo.RegisterCreatedObjectUndo(folder, "기물 묶음 만들기");
            return folder.transform;
        }

        static bool IsFixture(Brush brush) => brush is not (Brush.Block or Brush.Stair or Brush.Pillar or Brush.Coating or Brush.TestModel);

        static GameObject PrefabFor(CubeGridConfig config, Brush brush) => brush switch
        {
            Brush.Stair => config.stairPrefab,
            Brush.TestModel => TestModelAsset,
            Brush.Checkpoint => config.checkpointPrefab,
            Brush.ColorSource => config.colorSourcePrefab,
            Brush.Goal => config.goalPrefab,
            Brush.BoomGate => config.boomGatePrefab,
            Brush.Barrier => config.barrierPrefab,
            Brush.ConveyorBelt => SelectedConveyorShape switch
            {
                ConveyorShape.Right => config.conveyorCurveRightPrefab,
                ConveyorShape.Left => config.conveyorCurveLeftPrefab,
                _ => config.conveyorBeltPrefab,
            },
            Brush.Pillar => config.pillarPrefab,
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
