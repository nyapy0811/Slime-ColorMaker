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
    /// 큐브 맵 편집 창과 씬 뷰 도구가 함께 쓰는 편집 동작.
    /// 고른 붓으로 칸에 블록·계단·기둥·장식·기물·시험 모델을 놓고 지운다. 한 칸에는 한 종류만 두며(<see cref="Alone"/>), 프리팹의 설치 규칙
    /// (<see cref="PlacementRules"/>)에 맞을 때만 놓고, 편집마다 규칙에 맞지 않게 된 것은 이어서 지운다. 블록과 계단, 기둥, 장식은 큐브 격자 데이터에, 기물과 시험 모델은
    /// 씬의 오브젝트로 칸 바닥 가운데에 놓는다(맵 안 id는 기물을 놓을 때 고침). 계단은 씬 카메라가 보는 쪽으로 오르게 놓고,
    /// '돌리기'로 90°씩 더 돌린다. 편집 한 번이 Undo 하나다.
    /// </summary>
    public static class CubeGridEditing
    {
        public enum Brush { Block, Stair, Checkpoint, ColorSource, Goal, Gate, Barrier, ConveyorBelt, Pillar, Coating, TestModel, Eraser, Light, Crate, Railing, Panel }

        /// <summary>방벽을 놓을 면: 옆(카메라 쪽 옆면, 돌리기로 90°씩), 위(칸 윗면), 아래(칸 바닥면).</summary>
        public enum BarrierSide { Side, Up, Down }

        public static readonly string[] BarrierSideNames = { "옆", "위", "아래" };

        public static readonly string[] BrushNames = { "블록", "계단", "체크포인트", "색 원천", "목적지", "게이트", "방벽", "컨베이어", "기둥", "코팅", "시험 모델", "지우개", "조명", "상자", "난간", "계기판" };

        const string TestModelsFolder = "TestModels";

        /// <summary>컨베이어 붓인가. 블록 자리에 깔리고 씬 카메라가 보는 쪽으로 나가게 놓인다(모양은 <see cref="SelectedConveyorShape"/>).</summary>
        public static bool IsConveyor(Brush brush) => brush == Brush.ConveyorBelt;

        /// <summary>
        /// 놓을 컨베이어가 나가는 방향. 들어가는 방향(<paramref name="entryTurn"/>, 씬 카메라가 보는 쪽 + 돌림)에서
        /// 우회전은 90° 오른쪽(+1), 좌회전은 왼쪽(−1)으로 꺾여 나가고 직진은 그대로다.
        /// </summary>
        public static int ConveyorExitTurn(int entryTurn) =>
            CubeGridRules.Turn(entryTurn + SelectedConveyorShape switch { ConveyorShape.Right => 1, ConveyorShape.Left => -1, _ => 0 });

        /// <summary>창의 컨베이어 모양 줄 순서와 이름: 직진, 좌회전, 우회전.</summary>
        public static readonly ConveyorShape[] ConveyorShapeOrder = { ConveyorShape.Straight, ConveyorShape.Left, ConveyorShape.Right };
        public static readonly string[] ConveyorShapeNames = { "직진", "좌회전", "우회전" };

        /// <summary>놓을 컨베이어의 모양: 직진, 우회전(들어와서 오른쪽으로 꺾여 나감), 좌회전.</summary>
        public static ConveyorShape SelectedConveyorShape
        {
            get => (ConveyorShape)SessionState.GetInt("SlimeColorMaker.CubeGrid.ConveyorShape", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.ConveyorShape", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>시험 모델 붓으로 놓을 모델(.bbmodel이나 프리팹, 기준점은 바닥면 가운데).</summary>
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

        /// <summary>계단을 카메라 방향에서 90°씩 더 돌린 횟수(창의 '돌리기').</summary>
        public static int StairTurnOffset
        {
            get => SessionState.GetInt("SlimeColorMaker.CubeGrid.StairTurn", 0);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.StairTurn", CubeGridRules.Turn(value));
                BrushChanged?.Invoke();
            }
        }

        /// <summary>지금 놓을 계단의 방향: 씬 카메라가 보는 쪽 + 돌린 횟수.</summary>
        public static int StairTurn(Vector3 cameraForward) => CubeGridRules.Turn(CubeGridRules.TurnFacing(cameraForward) + StairTurnOffset);

        /// <summary>코팅 붓의 종류 이름(<see cref="CoatingKind"/>의 없음·기름·얼음 순서, 번호 = 종류 값). 없음은 지우개다.</summary>
        public static readonly string[] CoatingKindNames = { "없음", "기름", "얼음" };

        /// <summary>코팅 붓으로 칠할 종류(없음 = 지우기·기름·얼음, 기본 기름).</summary>
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

        /// <summary>놓을 기둥을 눕힐지(가로 들보, 기본 세움).</summary>
        public static bool PillarLying
        {
            get => SessionState.GetBool("SlimeColorMaker.CubeGrid.PillarLying", false);
            set
            {
                SessionState.SetBool("SlimeColorMaker.CubeGrid.PillarLying", value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>놓을 기둥의 방향: 세운 기둥은 홈이 보는 쪽, 눕힌 기둥은 옆 칸으로 정하지 못할 때의 들보 방향(기본 남북).</summary>
        public static CellAxis PillarAxis
        {
            get => (CellAxis)SessionState.GetInt("SlimeColorMaker.CubeGrid.PillarAxis", (int)CellAxis.NorthSouth);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.PillarAxis", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>놓을 천장 조명의 방향(막대 길이 방향, 기본 남북).</summary>
        public static CellAxis LightAxis
        {
            get => (CellAxis)SessionState.GetInt("SlimeColorMaker.CubeGrid.LightAxis", (int)CellAxis.NorthSouth);
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.LightAxis", (int)value);
                BrushChanged?.Invoke();
            }
        }

        /// <summary>놓을 천장 조명의 빛 색(칠하는 색, 기본 흰색).</summary>
        public static Color LightColor
        {
            get => ColorUtility.TryParseHtmlString("#" + SessionState.GetString("SlimeColorMaker.CubeGrid.LightColor", "FFFFFF"), out var color) ? color : Color.white;
            set
            {
                SessionState.SetString("SlimeColorMaker.CubeGrid.LightColor", ColorUtility.ToHtmlStringRGB(value));
                BrushChanged?.Invoke();
            }
        }

        /// <summary>놓을 게이트의 열리는 면(여럿, 세계 방향). 기본은 남·북.</summary>
        public static CellFaces GateOpenFaces
        {
            get => (CellFaces)SessionState.GetInt("SlimeColorMaker.CubeGrid.GateOpenFaces", (int)(CellFaces.North | CellFaces.South));
            set
            {
                SessionState.SetInt("SlimeColorMaker.CubeGrid.GateOpenFaces", (int)value);
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
        /// 놓을 방벽의 면. 옆이면 칸의 카메라 쪽 옆면(<paramref name="turn"/>이 지나가는 방향, 0 = +z이면 남쪽 면), 위·아래는 칸 윗면·바닥면.
        /// </summary>
        public static CellFace BarrierFace(int turn) => SelectedBarrierSide switch
        {
            BarrierSide.Up => CellFace.Up,
            BarrierSide.Down => CellFace.Down,
            _ => CubeGridRules.Turn(turn) switch { 0 => CellFace.South, 1 => CellFace.West, 2 => CellFace.North, _ => CellFace.East },
        };

        public static event Action BrushChanged;

        /// <summary>놓기 전에 값을 정하는 붓의 값 칸(색 원천은 흡수하면 더하는 색, 목적지·게이트·방벽은 요구 색). 없으면 null.</summary>
        public static string ValueField(Brush brush) => brush switch
        {
            Brush.ColorSource => "value",
            Brush.Goal or Brush.Gate or Brush.Barrier => "requiredColor",
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

        /// <summary>새 맵의 큐브 격자를 만든다. 하늘·조명은 MapRoot의 맵 환경이 맡는다.</summary>
        public static CubeGrid CreateGrid()
        {
            var go = new GameObject("CubeGrid");
            Undo.RegisterCreatedObjectUndo(go, "큐브 격자 만들기");
            Selection.activeGameObject = go;
            return go.AddComponent<CubeGrid>();
        }

        /// <summary>씬의 기물과 시험 모델이 든 칸(칸 바닥 가운데에 놓인 자리 기준). 한 칸에 여럿은 방벽끼리뿐이다.</summary>
        public static Dictionary<Vector3Int, List<GameObject>> ObjectCells(CubeGrid grid)
        {
            var cells = new Dictionary<Vector3Int, List<GameObject>>();
            void Add(Component placed)
            {
                var cell = CubeGridRules.CellAt(placed.transform.position);
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<GameObject>();
                list.Add(placed.gameObject);
            }
            var scene = grid.gameObject.scene;
            foreach (var fixture in FixtureIdAssigner.FixturesIn(scene)) Add(fixture);
            foreach (var model in TestModelsIn(scene)) Add(model);
            return cells;
        }

        public static IEnumerable<CubeTestModel> TestModelsIn(UnityEngine.SceneManagement.Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeTestModel>(true));

        public static bool IsFilled(CubeGrid grid, Dictionary<Vector3Int, List<GameObject>> objects, Vector3Int cell) =>
            CubeGridRules.IsOccupied(grid.Data, cell) || objects.ContainsKey(cell);

        /// <summary>칸의 <paramref name="face"/> 면에 방벽을 놓을 수 있나: 칸에 방벽만 있거나 비었고(한 칸 한 종류), 그 면은 비었다.</summary>
        public static bool CanAddBarrier(CubeGrid grid, Dictionary<Vector3Int, List<GameObject>> objects, Vector3Int cell, CellFace face) =>
            Alone(grid, objects, cell, Brush.Barrier) && (!objects.TryGetValue(cell, out var placed) || placed.All(go => BarrierFaceOf(go) != face));

        /// <summary>방벽이면 그 면, 아니면 null.</summary>
        public static CellFace? BarrierFaceOf(GameObject go) =>
            go.TryGetComponent(out BarrierView _) && go.TryGetComponent(out CellFaceMount mount) ? mount.Face : null;

        /// <summary>
        /// 가리킨 칸(<paramref name="hit"/>, 들어간 면 <paramref name="normal"/>)에 붓으로 놓을 칸. 보통은 그 앞 칸이고,
        /// 방벽 붓이 방벽만 든 칸을 가리켰고 놓을 면이 비었으면 그 칸 자체다(한 칸에 방벽 여럿).
        /// </summary>
        public static Vector3Int PlaceTarget(CubeGrid grid, Vector3Int hit, Vector3Int normal, int turn)
        {
            if (SelectedBrush != Brush.Barrier) return hit + normal;
            var objects = ObjectCells(grid);
            return objects.ContainsKey(hit) && CanAddBarrier(grid, objects, hit, BarrierFace(turn)) ? hit : hit + normal;
        }

        /// <summary>빈 칸이면 고른 붓으로 놓는다. 계단은 <paramref name="stairTurn"/> 방향으로 오른다.</summary>
        public static void Place(CubeGrid grid, Vector3Int cell, int stairTurn, int panelSide = -1) =>
            PlaceAll(grid, new[] { cell }, "칸에 놓기", stairTurn: stairTurn, panelSide: panelSide);

        /// <summary>난간이 붙을 옆면(방벽 옆면처럼 카메라 쪽, 돌리기로 90°씩): 0 = 북, 1 = 동, 2 = 남, 3 = 서.</summary>
        public static int RailingSide(int turn) => CubeGridRules.Turn(turn + 2);

        /// <summary>
        /// 칸에 붓의 것을 둘 수 있나(한 칸 한 종류): 칸이 비었거나, 든 것이 모두 같은 종류이고 그 종류를 한 칸에 여럿 둘 수 있다
        /// (상자는 반 칸마다, 난간·계기판·방벽은 면마다). 지형(블록·계단·기둥)·천장 조명·그 밖의 기물·시험 모델은 칸에 하나뿐이다.
        /// </summary>
        public static bool Alone(CubeGrid grid, Dictionary<Vector3Int, List<GameObject>> objects, Vector3Int cell, Brush brush)
        {
            var data = grid.Data;
            bool barriers = objects.TryGetValue(cell, out var placed);
            if (CubeGridRules.IsOccupied(data, cell) || CubeGridRules.HasLight(data, cell) || (barriers && !placed.All(go => BarrierFaceOf(go) != null))) return false;
            bool crate = CubeGridRules.HasCrate(data, cell), railing = CubeGridRules.HasRailing(data, cell), panel = CubeGridRules.HasPanel(data, cell);
            return brush switch
            {
                Brush.Crate => !railing && !panel && !barriers,
                Brush.Railing => !crate && !panel && !barriers,
                Brush.Panel => !crate && !railing && !barriers,
                Brush.Barrier => !crate && !railing && !panel,
                _ => !crate && !railing && !panel && !barriers,
            };
        }

        /// <summary>고른 붓으로 칸에 놓을 수 있나(한 칸 한 종류, 설치 규칙). 씬 뷰 미리보기가 쓴다.</summary>
        public static bool CanPlace(CubeGrid grid, Vector3Int cell, int turn, int panelSide)
        {
            var config = FindConfig();
            var prefab = config != null ? PrefabFor(config, SelectedBrush) : null;
            return prefab != null && Placeable(grid, config, ObjectCells(grid), SelectedBrush, prefab, cell, turn, panelSide);
        }

        /// <summary>반 칸에 상자를 놓을 수 있나: 그 반 칸이 비었고, 한 칸 한 종류, 상자 프리팹의 설치 규칙(반 칸 단위)에 맞는다.</summary>
        public static bool CanPlaceCrate(CubeGrid grid, Vector3Int half)
        {
            var config = FindConfig();
            return config != null && config.cratePrefab != null && !grid.Data.crates.Contains(half)
                && Alone(grid, ObjectCells(grid), CubeGridRules.CrateCell(half), Brush.Crate) && CrateSupported(grid, config, half);
        }

        /// <summary>반 칸에 상자를 놓는다(그 칸에 다른 종류가 없을 때만, 반 칸 2 × 2 × 2개까지).</summary>
        public static void PlaceCrate(CubeGrid grid, Vector3Int half)
        {
            var config = RequireConfig();
            if (config == null) return;
            if (config.cratePrefab == null)
            {
                Debug.LogError("[큐브 맵 편집] 큐브 맵 설정에 '상자' 프리팹이 없다.", config);
                return;
            }
            if (!CanPlaceCrate(grid, half)) return;
            Edit(grid, "상자 놓기", () => CubeGridRules.AddCrate(grid.Data, half), config);
        }

        /// <summary>반 칸의 상자 하나를 지운다.</summary>
        public static void RemoveCrate(CubeGrid grid, Vector3Int half)
        {
            var config = RequireConfig();
            if (config == null) return;
            Edit(grid, "상자 지우기", () => CubeGridRules.RemoveCrate(grid.Data, half), config);
        }

        /// <summary>범위의 빈 칸에 블록을 놓는다(범위 놓기는 블록만).</summary>
        public static void PlaceBlocks(CubeGrid grid, IEnumerable<Vector3Int> cells) => PlaceAll(grid, cells, "블록 범위 놓기", blocksOnly: true);

        /// <summary>칸의 블록·계단·기둥·장식·기물·시험 모델을 지운다(칸에 방벽·상자·난간·계기판이 여럿이면 모두, 받침이 사라진 것도 이어서). 시작 지점(MapRoot 안, 원점)은 지우지 않는다.</summary>
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
                    if (!objects.Remove(cell, out var placed)) continue;
                    foreach (var go in placed)
                        if (!go.TryGetComponent<StartPoint>(out _)) Undo.DestroyObjectImmediate(go);
                }
            }, config);
        }

        public static void Rebuild(CubeGrid grid)
        {
            var config = RequireConfig();
            if (config == null) return;
            Edit(grid, "블록 다시 만들기", () => { }, config, fullRebuild: true);
        }

        static void PlaceAll(CubeGrid grid, IEnumerable<Vector3Int> cells, string undoName, bool blocksOnly = false, int stairTurn = 0, int panelSide = -1)
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
                    if (!Placeable(grid, config, objects, brush, prefab, cell, stairTurn, panelSide)) continue;
                    switch (brush)
                    {
                        case Brush.Railing: CubeGridRules.AddSidePiece(grid.Data.railings, cell, RailingSide(stairTurn)); continue;
                        case Brush.Panel: CubeGridRules.AddSidePiece(grid.Data.panels, cell, panelSide); continue;
                        case Brush.Light: CubeGridRules.AddLight(grid.Data, cell, LightAxis, LightColor); continue;
                        case Brush.Block: CubeGridRules.AddBlock(grid.Data, cell); continue;
                        case Brush.Stair: CubeGridRules.AddStair(grid.Data, cell, stairTurn); continue;
                        case Brush.Pillar: CubeGridRules.AddPillar(grid.Data, cell, PillarLying, PillarAxis); continue;
                    }
                    var placed = brush == Brush.TestModel ? PlaceTestModel(grid, prefab, cell) : PlaceFixture(grid, prefab, brush, cell, stairTurn).gameObject;
                    if (!objects.TryGetValue(cell, out var list)) objects[cell] = list = new List<GameObject>();
                    list.Add(placed);
                }
                if (IsFixture(brush)) FixtureIdAssigner.FixIds(grid.gameObject.scene);
            }, config);
        }

        // 붓의 것을 칸에 놓을 수 있나: 한 칸 한 종류(Alone), 옆면 장식은 그 면이 비었고 계기판은 같은 칸 계기판과 모서리에서 겹치지 않으며, 설치 규칙에 맞는다.
        static bool Placeable(CubeGrid grid, CubeGridConfig config, Dictionary<Vector3Int, List<GameObject>> objects, Brush brush, GameObject prefab, Vector3Int cell, int turn, int panelSide)
        {
            if (!Alone(grid, objects, cell, brush)) return false;
            switch (brush)
            {
                case Brush.Railing:
                case Brush.Panel:
                    int side = brush == Brush.Railing ? RailingSide(turn) : panelSide;
                    var pieces = brush == Brush.Railing ? grid.Data.railings : grid.Data.panels;
                    if (side < 0 || pieces.Exists(piece => piece.cell == cell && piece.side == side)) return false;
                    if (brush == Brush.Panel && Voxels(grid, config).PanelOverlaps(cell, side)) return false;
                    return Supported(grid, config, objects, prefab, prefab, cell, CubeGridRules.SideRotation(side));
                case Brush.Light:
                    return Supported(grid, config, objects, prefab, prefab, cell, CubeGridRules.LightRotation(LightAxis));
                case Brush.Barrier:
                    if (!CanAddBarrier(grid, objects, cell, BarrierFace(turn))) return false;
                    break;
                case Brush.Pillar:
                    // 놓인 모양은 옆 칸으로 정해지므로 놓은 뒤의 모양으로 본다.
                    CubeGridRules.AddPillar(grid.Data, cell, PillarLying, PillarAxis);
                    var pose = CubeGridRules.PillarLayouts(grid.Data)[cell].pose;
                    grid.Data.pillars.RemoveAll(pillar => pillar.cell == cell);
                    return Supported(grid, config, objects, prefab, prefab, cell, PillarRotation(config, pose));
            }
            return Supported(grid, config, objects, prefab, prefab, cell, PlaceRotation(brush, turn));
        }

        // 놓을 때의 회전: 컨베이어는 turn 방향으로 들어간다. 모델(직선·꺾임)은 −z로 나가므로 나가는 방향에서 반 바퀴 더 돌린다. 나머지 기물은 돌리지 않는다.
        static Quaternion PlaceRotation(Brush brush, int turn) =>
            IsConveyor(brush) ? CubeGridRules.StairRotation(ConveyorExitTurn(turn) + 2) : Quaternion.identity;

        // 기둥의 놓인 회전: 그 모양 모델의 회전(세움 남북·동서, 눕힘 남북·동서).
        static Quaternion PillarRotation(CubeGridConfig config, PillarPose pose) =>
            config.pillarPrefab != null && config.pillarPrefab.TryGetComponent(out PillarView view) ? view.PoseRotation(pose) : Quaternion.identity;

        /// <summary>
        /// <paramref name="rulesOwner"/>(프리팹이나 놓인 오브젝트)의 설치 규칙에 칸이 맞는가. 완전한 면은 옆 칸 지형의 픽셀로,
        /// 같은 종류에 연결은 옆 칸에 <paramref name="kind"/>(종류 = 프리팹)가 있는지로 본다.
        /// </summary>
        static bool Supported(CubeGrid grid, CubeGridConfig config, Dictionary<Vector3Int, List<GameObject>> objects, GameObject kind, GameObject rulesOwner, Vector3Int cell, Quaternion rotation)
        {
            if (rulesOwner == null || !rulesOwner.TryGetComponent(out PlacementRules rules) || rules.Rules.Count == 0) return true;
            CubeGridVoxels voxels = null;
            return CubeGridRules.Supported(rules.Rules, rotation,
                toward => (voxels ??= Voxels(grid, config)).FullFace(cell, toward),
                toward => HasKind(grid, config, objects, kind, cell + toward));
        }

        // 상자의 설치 규칙(반 칸 단위): 완전한 면은 상자의 그 면이 칸 경계에 있을 때만, 같은 종류에 연결은 그쪽 반 칸의 상자.
        static bool CrateSupported(CubeGrid grid, CubeGridConfig config, Vector3Int half)
        {
            if (config.cratePrefab == null || !config.cratePrefab.TryGetComponent(out PlacementRules rules) || rules.Rules.Count == 0) return true;
            CubeGridVoxels voxels = null;
            var cell = CubeGridRules.CrateCell(half);
            return CubeGridRules.Supported(rules.Rules, Quaternion.identity,
                toward => CubeGridRules.CrateFaceOnBoundary(half, toward) && (voxels ??= Voxels(grid, config)).FullFace(cell, toward),
                toward => grid.Data.crates.Contains(half + toward));
        }

        // 칸에 그 종류(프리팹)가 있나: 지형·장식은 데이터로, 기물·시험 모델은 놓인 오브젝트의 원본 프리팹(모델)으로.
        static bool HasKind(CubeGrid grid, CubeGridConfig config, Dictionary<Vector3Int, List<GameObject>> objects, GameObject kind, Vector3Int cell)
        {
            var data = grid.Data;
            if (kind == null) return false;
            if (kind == config.blockPrefab) return CubeGridRules.HasBlock(data, cell);
            if (kind == config.stairPrefab) return data.stairs.Exists(stair => stair.cell == cell);
            if (kind == config.pillarPrefab) return data.pillars.Exists(pillar => pillar.cell == cell);
            if (kind == config.lightPrefab) return CubeGridRules.HasLight(data, cell);
            if (kind == config.cratePrefab) return CubeGridRules.HasCrate(data, cell);
            if (kind == config.railingPrefab) return CubeGridRules.HasRailing(data, cell);
            if (kind == config.panelPrefab) return CubeGridRules.HasPanel(data, cell);
            return objects.TryGetValue(cell, out var placed) && placed.Exists(go => go != null && SourceOf(go) == kind);
        }

        // 놓인 기물의 원본 프리팹, 시험 모델이면 그 모델.
        static GameObject SourceOf(GameObject go) => PrefabUtility.GetCorrespondingObjectFromSource(ModelOf(go));

        // 설치 규칙을 든 오브젝트: 기물은 그 자신, 시험 모델은 안의 모델.
        static GameObject ModelOf(GameObject go) =>
            go.TryGetComponent<CubeTestModel>(out _) ? (go.transform.childCount > 0 ? go.transform.GetChild(0).gameObject : go) : go;

        static FixtureBase PlaceFixture(CubeGrid grid, GameObject prefab, Brush brush, Vector3Int cell, int turn)
        {
            var parent = FolderFor(grid.gameObject.scene, brush);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "기물 놓기");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "기물 놓기");
            go.transform.SetPositionAndRotation(CubeGridRules.CellBottom(cell), PlaceRotation(brush, turn));
            if (brush == Brush.Barrier && go.TryGetComponent(out CellFaceMount mount))
            {
                mount.Face = BarrierFace(turn);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mount);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mount.Pivot);
            }
            // 게이트는 고른 열리는 면으로 정한다(면마다 판·막는 상자를 켜고 끔, 오버라이드로 남김).
            if (brush == Brush.Gate && go.TryGetComponent(out GateFaces gateFaces))
            {
                var faces = new SerializedObject(gateFaces);
                faces.FindProperty("open").intValue = (int)GateOpenFaces;
                faces.ApplyModifiedPropertiesWithoutUndo();
                gateFaces.Apply();
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            // 컨베이어는 한 프리팹이 세 모양을 갖는다: 고른 모양으로 정하고 그 모양의 무늬만 켠다(오버라이드로 남김).
            if (IsConveyor(brush) && go.TryGetComponent(out ConveyorBelt belt))
            {
                var shape = new SerializedObject(belt);
                shape.FindProperty("shape").enumValueIndex = (int)SelectedConveyorShape;
                shape.ApplyModifiedPropertiesWithoutUndo();
                belt.ShowShape();
                foreach (var frame in belt.AllFrames) PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
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
            string name = brush switch { Brush.Checkpoint => "Checkpoints", Brush.ColorSource => "ColorSources", Brush.Goal => "Activators", Brush.Gate or Brush.Barrier => "Gates", Brush.ConveyorBelt => "Movers", Brush.TestModel => TestModelsFolder, _ => null };
            if (name == null) return null;
            var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) return existing.transform;
            var folder = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(folder, scene);
            Undo.RegisterCreatedObjectUndo(folder, "기물 묶음 만들기");
            return folder.transform;
        }

        static bool IsFixture(Brush brush) => brush is not (Brush.Block or Brush.Stair or Brush.Pillar or Brush.Coating or Brush.TestModel or Brush.Eraser or Brush.Light or Brush.Crate or Brush.Railing or Brush.Panel);

        internal static GameObject PrefabFor(CubeGridConfig config, Brush brush) => brush switch
        {
            Brush.Stair => config.stairPrefab,
            Brush.TestModel => TestModelAsset,
            Brush.Checkpoint => config.checkpointPrefab,
            Brush.ColorSource => config.colorSourcePrefab,
            Brush.Goal => config.goalPrefab,
            Brush.Gate => config.gatePrefab,
            Brush.Barrier => config.barrierPrefab,
            Brush.ConveyorBelt => config.conveyorBeltPrefab,
            Brush.Pillar => config.pillarPrefab,
            Brush.Light => config.lightPrefab,
            Brush.Crate => config.cratePrefab,
            Brush.Railing => config.railingPrefab,
            Brush.Panel => config.panelPrefab,
            _ => config.blockPrefab,
        };

        static CubeGridConfig RequireConfig()
        {
            var config = FindConfig();
            if (config == null) Debug.LogError("[큐브 맵 편집] 큐브 맵 설정 애셋(CubeGridConfig)이 없다. Assets/Data/Map에 만든다.");
            return config;
        }

        /// <summary>격자의 픽셀 모양(지형·상자).</summary>
        public static CubeGridVoxels Voxels(CubeGrid grid, CubeGridConfig config) => new(config, grid.Data);

        // 받침 검사의 한 항목: 종류(프리팹), 설치 규칙, 칸(상자는 반 칸), 놓인 회전, 지우는 동작.
        sealed class Piece
        {
            public GameObject kind;
            public IReadOnlyList<PlacementRule> rules;
            public Vector3Int key;
            public bool crate;
            public Quaternion rotation;
            public Action remove;
        }

        /// <summary>
        /// 설치 규칙에 맞지 않게 된 것(받침이 사라짐)을 지운다. 완전한 면에 붙은 것(규칙이 없는 것 포함)에서 '같은 종류에 연결'로 이어진 것만 남기므로,
        /// 서로만 받치고 떠 있는 묶음(위 기둥과 옆 들보 등)도 지운다. 지운 것이 지형(기둥)이면 면이 바뀌므로 다시 본다. 시작 지점은 두고, 블록·계단은 규칙이 없다.
        /// </summary>
        static void PruneUnsupported(CubeGrid grid, CubeGridConfig config)
        {
            while (true)
            {
                var voxels = Voxels(grid, config);
                var supported = new HashSet<(GameObject, Vector3Int)>();
                var pending = new List<Piece>();
                foreach (var piece in Pieces(grid, config))
                {
                    if (Rests(piece, voxels, _ => false)) supported.Add((piece.kind, piece.key));
                    else pending.Add(piece);
                }
                for (bool grew = true; grew && pending.Count > 0;)
                {
                    grew = false;
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        var piece = pending[i];
                        if (!Rests(piece, voxels, key => supported.Contains((piece.kind, key)))) continue;
                        supported.Add((piece.kind, piece.key));
                        pending.RemoveAt(i);
                        grew = true;
                    }
                }
                if (pending.Count == 0) return;
                foreach (var piece in pending) piece.remove();
            }
        }

        // 칸의 설치 규칙에 맞나: 완전한 면은 지형 픽셀로(상자는 그 면이 칸 경계에 있을 때만), 같은 종류에 연결은 sameKindAt(옆 칸이나 반 칸 열쇠)으로.
        static bool Rests(Piece piece, CubeGridVoxels voxels, Func<Vector3Int, bool> sameKindAt) =>
            piece.rules == null || CubeGridRules.Supported(piece.rules, piece.rotation,
                toward => piece.crate
                    ? CubeGridRules.CrateFaceOnBoundary(piece.key, toward) && voxels.FullFace(CubeGridRules.CrateCell(piece.key), toward)
                    : voxels.FullFace(piece.key, toward),
                toward => sameKindAt(piece.key + toward));

        // 설치 규칙이 있을 수 있는 모든 것: 데이터의 조명·난간·계기판·기둥·상자, 씬의 기물·시험 모델(시작 지점 빼고).
        static List<Piece> Pieces(CubeGrid grid, CubeGridConfig config)
        {
            var data = grid.Data;
            var pieces = new List<Piece>();
            void Add(GameObject kind, GameObject rulesOwner, Vector3Int key, Quaternion rotation, Action remove, bool crate = false) => pieces.Add(new Piece
            {
                kind = kind,
                rules = rulesOwner != null && rulesOwner.TryGetComponent(out PlacementRules rules) ? rules.Rules : null,
                key = key,
                crate = crate,
                rotation = rotation,
                remove = remove,
            });
            foreach (var light in data.lights) Add(config.lightPrefab, config.lightPrefab, light.cell, CubeGridRules.LightRotation(light.axis), () => data.lights.Remove(light));
            foreach (var piece in data.railings) Add(config.railingPrefab, config.railingPrefab, piece.cell, CubeGridRules.SideRotation(piece.side), () => data.railings.Remove(piece));
            foreach (var piece in data.panels) Add(config.panelPrefab, config.panelPrefab, piece.cell, CubeGridRules.SideRotation(piece.side), () => data.panels.Remove(piece));
            var layouts = CubeGridRules.PillarLayouts(data);
            foreach (var pillar in data.pillars)
                Add(config.pillarPrefab, config.pillarPrefab, pillar.cell, PillarRotation(config, layouts.TryGetValue(pillar.cell, out var layout) ? layout.pose : PillarPose.UprightNorthSouth), () => data.pillars.Remove(pillar));
            foreach (var half in data.crates) Add(config.cratePrefab, config.cratePrefab, half, Quaternion.identity, () => data.crates.Remove(half), crate: true);
            foreach (var (cell, placed) in ObjectCells(grid))
                foreach (var go in placed)
                    if (!go.TryGetComponent<StartPoint>(out _))
                        Add(SourceOf(go), ModelOf(go), cell, go.transform.rotation, () => Undo.DestroyObjectImmediate(go));
            return pieces;
        }

        static void Edit(CubeGrid grid, string undoName, Action change, CubeGridConfig config, bool fullRebuild = false)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(grid, undoName);
            change();
            PruneUnsupported(grid, config);
            if (fullRebuild) CubeGridBuilder.Rebuild(grid, config);
            else CubeGridBuilder.Build(grid, config);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        }
    }
}
