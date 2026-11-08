using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorMaze
{
    /// <summary>
    /// 맵 검사(첫 버전). 메뉴 ColorMaze/맵 검사로 지금 맵을 검사해 콘솔에 알린다(항목을 누르면 그 오브젝트로 감).
    /// 맵 씬을 저장할 때는 가벼운 검사(맵 안 id)만 자동으로 한다. 검사 항목은 기물을 만드는 단계마다 더한다.
    /// </summary>
    [InitializeOnLoad]
    public static class MapValidator
    {
        static MapValidator()
        {
            EditorSceneManager.sceneSaving += (scene, _) =>
            {
                if (IsMapScene(scene)) CheckFixtureIds(scene);
            };
        }

        [MenuItem("ColorMaze/맵 검사")]
        static void ValidateActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            int problems = Validate(scene);
            if (problems == 0) Debug.Log($"[맵 검사] {scene.name}: 문제 없음.");
            else Debug.LogWarning($"[맵 검사] {scene.name}: 문제 {problems}개.");
        }

        /// <summary>씬을 검사하고 문제 수를 돌려준다. 문제마다 콘솔에 남긴다.</summary>
        public static int Validate(Scene scene)
        {
            int problems = 0;

            var managers = Roots(scene).SelectMany(root => root.GetComponentsInChildren<MapManager>(true)).ToList();
            if (managers.Count != 1)
            {
                Debug.LogError($"[맵 검사] 맵 매니저가 {managers.Count}개다(1개여야 함).");
                problems++;
            }

            problems += CheckFixtureIds(scene);

            var starts = FixtureIdAssigner.FixturesIn(scene).OfType<StartPoint>().ToList();
            if (starts.Count != 1)
            {
                Debug.LogError($"[맵 검사] 시작 지점이 {starts.Count}개다(1개여야 함).", starts.FirstOrDefault());
                problems++;
            }

            var goals = FixtureIdAssigner.FixturesIn(scene).OfType<Goal>().ToList();
            if (goals.Count != 1)
            {
                Debug.LogError($"[맵 검사] 목적지가 {goals.Count}개다(1개여야 함).", goals.FirstOrDefault());
                problems++;
            }

            foreach (var source in FixtureIdAssigner.FixturesIn(scene).OfType<ColorSource>().Where(s => !ColorRules.IsValidSourceValue(s.Value)))
            {
                Debug.LogError($"[맵 검사] 색 원천 '{source.name}'의 값 {source.Value}은 쓸 수 없다((0,0,0)이나 세 채널이 같은 값).", source);
                problems++;
            }

            problems += CheckRequiredColors(scene, ColorRules.Limit);
            problems += CheckFixtureLayers(scene);

            problems += CheckCubeGrid(scene);

            if (managers.Count == 1) problems += CheckMapList(scene, managers[0].MapList);
            if (managers.Count == 1) problems += CheckSharedLight(scene, managers[0]);

            return problems;
        }

        /// <summary>맵 목록: 맵 매니저에 연결됐는지, 이 씬이 목록에 있는지, 맵 id가 비었거나 겹치는지.</summary>
        static int CheckMapList(Scene scene, MapList mapList)
        {
            if (mapList == null)
            {
                Debug.LogError("[맵 검사] 맵 매니저에 맵 목록(Assets/Data/MapList)이 연결되지 않았다.");
                return 1;
            }
            int problems = 0;
            if (mapList.FindByScene(scene.name) == null)
            {
                Debug.LogError($"[맵 검사] 맵 목록에 이 씬('{scene.name}')이 없다.", mapList);
                problems++;
            }
            foreach (var entry in mapList.maps.Where(entry => string.IsNullOrWhiteSpace(entry.mapId)))
            {
                Debug.LogError($"[맵 검사] 맵 목록의 '{entry.sceneName}' 항목에 맵 id가 없다.", mapList);
                problems++;
            }
            foreach (var group in mapList.maps.Where(entry => !string.IsNullOrWhiteSpace(entry.mapId)).GroupBy(entry => entry.mapId).Where(g => g.Count() > 1))
            {
                Debug.LogError($"[맵 검사] 맵 목록에 맵 id '{group.Key}'가 {group.Count()}개다(맵마다 달라야 함).", mapList);
                problems++;
            }
            return problems;
        }

        /// <summary>상호작용하거나 값을 보이는 기물의 콜라이더는 기물 레이어에 있어야 캐릭터가 닿고 값을 본다(2026-11-08).</summary>
        static int CheckFixtureLayers(Scene scene)
        {
            int layer = LayerMask.NameToLayer(FixtureBase.LayerName);
            if (layer < 0)
            {
                Debug.LogError($"[맵 검사] '{FixtureBase.LayerName}' 레이어가 없다(Project Settings > Tags and Layers).");
                return 1;
            }
            int problems = 0;
            foreach (var fixture in FixtureIdAssigner.FixturesIn(scene).Where(f => f is IInteractable or IValueProvider))
            {
                foreach (var collider in fixture.GetComponentsInChildren<Collider>(true).Where(c => c.gameObject.layer != layer))
                {
                    Debug.LogError($"[맵 검사] '{fixture.name}'의 콜라이더 '{collider.name}'가 '{FixtureBase.LayerName}' 레이어가 아니다(캐릭터가 닿지 못하고 값도 안 보임). 프리팹의 레이어를 고친다.", collider);
                    problems++;
                }
            }
            return problems;
        }

        /// <summary>요구 색: 문·목적지는 요구 색 규칙, 레이저 벽의 빼는 색은 색 원천 값 규칙에 더해 한도 안이어야 지날 수 있다.</summary>
        static int CheckRequiredColors(Scene scene, ColorValue limit)
        {
            int problems = 0;
            foreach (var fixture in FixtureIdAssigner.FixturesIn(scene).OfType<RequiredColorBase>())
            {
                var color = fixture.RequiredColor;
                if (fixture is LaserWall)
                {
                    if (ColorRules.IsValidSourceValue(color) && ColorRules.IsWithin(color, limit)) continue;
                    Debug.LogError($"[맵 검사] 레이저 벽 '{fixture.name}'의 빼는 색 {color}은 쓸 수 없다((0,0,0)이나 세 채널이 같은 값, 또는 한도 {limit}를 넘음).", fixture);
                }
                else
                {
                    if (ColorRules.IsValidRequired(color, limit)) continue;
                    Debug.LogError($"[맵 검사] '{fixture.name}'의 요구 색 {color}은 쓸 수 없다(모든 채널 1 이상, 세 채널이 모두 같지 않고, 한도 {limit} 안이어야 함).", fixture);
                }
                problems++;
            }
            return problems;
        }

        /// <summary>
        /// 큐브 격자 1개(원점에 두고 돌리거나 늘리지 않음), 칸 바닥 가운데에 놓이지 않은 기물, 한 칸에 둘 이상인 기물,
        /// 블록이나 계단과 같은 칸에 있는 기물(v1 칸 규칙, 한 칸에 하나). 시험 모델도 기물처럼 본다.
        /// </summary>
        static int CheckCubeGrid(Scene scene)
        {
            var grids = Roots(scene).SelectMany(root => root.GetComponentsInChildren<CubeGrid>(true)).ToList();
            if (grids.Count != 1)
            {
                Debug.LogError($"[맵 검사] 큐브 격자가 {grids.Count}개다(1개여야 함). ColorMaze/큐브 맵 편집 창에서 만든다.");
                return 1;
            }

            int problems = 0;
            var grid = grids[0];
            var root = grid.transform;
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.lossyScale != Vector3.one)
            {
                Debug.LogError("[맵 검사] 큐브 격자는 원점에 두고 돌리거나 늘리지 않는다(칸 좌표가 월드 좌표).", grid);
                problems++;
            }

            var blocks = new HashSet<Vector3Int>(grid.Data.blocks);
            foreach (var stair in grid.Data.stairs) blocks.Add(stair.cell);
            var occupied = new Dictionary<Vector3Int, Component>();
            var placed = FixtureIdAssigner.FixturesIn(scene).Cast<Component>().Concat(CubeGridEditing.TestModelsIn(scene));
            foreach (var fixture in placed)
            {
                var position = fixture.transform.position;
                if (!CubeGridRules.IsOnCellBottom(position))
                {
                    Debug.LogError($"[맵 검사] '{fixture.name}'이 칸에 맞지 않는다({position}). 칸 바닥 가운데(x, y, z가 정수)에 둔다.", fixture);
                    problems++;
                    continue;
                }
                var cell = CubeGridRules.CellAt(position);
                if (blocks.Contains(cell))
                {
                    Debug.LogError($"[맵 검사] '{fixture.name}'이 블록이나 계단과 같은 칸 {cell}에 있다.", fixture);
                    problems++;
                }
                if (occupied.TryGetValue(cell, out var other))
                {
                    Debug.LogError($"[맵 검사] 칸 {cell}에 기물이나 시험 모델이 둘 있다: '{other.name}', '{fixture.name}'.", fixture);
                    problems++;
                }
                else occupied[cell] = fixture;
            }
            return problems;
        }

        /// <summary>
        /// 방향광은 맵 기본 프리팹(MapRoot) 안의 것 하나를 모든 맵이 같이 쓴다(2026-11-07). 그 밖의 방향광, 공유 빛이 없음,
        /// 이 맵에서만 바꾼 공유 빛 설정(프리팹 오버라이드)을 알린다. 설정은 프리팹에서 고치거나 오버라이드를 프리팹에 적용한다.
        /// </summary>
        static int CheckSharedLight(Scene scene, MapManager manager)
        {
            int problems = 0;
            var directional = Roots(scene).SelectMany(root => root.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional).ToList();
            var shared = directional.Where(l => l.transform.IsChildOf(manager.transform)).ToList();
            foreach (var other in directional.Except(shared))
            {
                Debug.LogError($"[맵 검사] 공유 빛(MapRoot 안) 말고 방향광 '{other.name}'이 있다. 지운다(새 씬에 기본으로 생기는 빛 포함).", other);
                problems++;
            }
            if (shared.Count != 1)
            {
                Debug.LogError($"[맵 검사] MapRoot 안의 공유 방향광이 {shared.Count}개다(1개여야 함).", manager);
                return problems + 1;
            }

            var light = shared[0].gameObject;
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(manager.gameObject);
            if (instanceRoot == null) return problems;
            bool changedHere = PrefabUtility.GetObjectOverrides(instanceRoot).Any(o =>
                o.instanceObject is Component c ? c.gameObject == light : o.instanceObject == light)
                || PrefabUtility.GetAddedComponents(instanceRoot).Any(a => a.instanceComponent.gameObject == light);
            if (changedHere)
            {
                Debug.LogError($"[맵 검사] 공유 빛 '{light.name}'을 이 맵에서만 바꿨다(프리팹 오버라이드). 모든 맵에 쓰려면 인스펙터의 Overrides → Apply, 아니면 Revert.", light);
                problems++;
            }
            return problems;
        }

        /// <summary>맵 안 id의 빈 id와 겹침을 알리고 문제 수를 돌려준다.</summary>
        static int CheckFixtureIds(Scene scene)
        {
            int problems = 0;
            var fixtures = FixtureIdAssigner.FixturesIn(scene).ToList();
            foreach (var fixture in fixtures.Where(f => f.FixtureId <= 0))
            {
                Debug.LogError($"[맵 검사] '{fixture.name}'의 맵 안 id가 비어 있다. ColorMaze/맵 안 id 고치기로 고친다.", fixture);
                problems++;
            }
            foreach (var group in fixtures.Where(f => f.FixtureId > 0).GroupBy(f => f.FixtureId).Where(g => g.Count() > 1))
            {
                Debug.LogError($"[맵 검사] 맵 안 id {group.Key}가 겹친다: {string.Join(", ", group.Select(f => $"'{f.name}'"))}. ColorMaze/맵 안 id 고치기로 고친다.", group.First());
                problems++;
            }
            return problems;
        }

        static bool IsMapScene(Scene scene) => Roots(scene).Any(root => root.GetComponentInChildren<MapManager>(true) != null);

        static GameObject[] Roots(Scene scene) => scene.GetRootGameObjects();
    }
}
