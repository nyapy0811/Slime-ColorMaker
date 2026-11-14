using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 씬마다 하나 있는 맵 관리자. 맵 범위의 것은 <see cref="Current"/> 하나로 찾는다(씬과 함께 생기고 사라짐).
    /// 기물 등록부(맵 안 id → 기물)를 들고, 시작할 때 상태를 Playing으로 맞추고 플레이어를 시작 지점으로 옮긴다.
    /// 클리어 시간을 재고, 목적지가 요청하면 클리어한다.
    /// 체크포인트에 처음 닿으면 스냅샷을 만들고, '체크포인트로'는 씬을 다시 불러오지 않고 기물마다 되돌린다(부분 초기화).
    /// 플레이어가 맵 밖으로 떨어지면 '체크포인트로'와 같이 마지막 체크포인트 상태로 되돌린다(없으면 '처음부터', 낙사 안전망).
    /// 떨어졌다고 보는 높이는 가장 낮은 블록의 바닥보다 낙사 깊이만큼 아래다(큐브 맵은 높이가 자유로움).
    /// 맵 id와 맵 버전은 맵 목록에서 자기 씬 이름으로 찾는다. 체크포인트·클리어는 진행 저장(<see cref="ProgressStore"/>)에 바로 쓴다.
    /// 메뉴의 '이어하기'로 열렸을 때만(<see cref="SceneFlow"/>의 요청) 저장된 체크포인트부터 시작한다.
    /// 기물이 OnEnable에서 등록하므로 기물보다 먼저 깨어나도록 실행 순서를 앞에 둔다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MapManager : MonoBehaviour
    {
        public static MapManager Current { get; private set; }

        [SerializeField] MapList mapList;
        [SerializeField] GameRulesConfig rules;
        [SerializeField] ThirdPersonController player;

        readonly Dictionary<int, FixtureBase> fixtures = new();
        PlayerColor playerColor;
        MapListEntry entry;

        /// <summary>마지막으로 닿은 체크포인트의 스냅샷(저장 파일에도 있음). 없으면 null.</summary>
        MapSnapshot checkpoint;

        /// <summary>이 높이보다 아래로 떨어지면 낙사 안전망이 옮긴다.</summary>
        float fallHeight;

        public MapList MapList => mapList;
        public MapListEntry Entry => entry;
        public string MapId => entry.mapId;

        /// <summary>닿은 체크포인트가 있는지('체크포인트로'의 확인 문구가 다름).</summary>
        public bool HasCheckpoint => checkpoint != null;
        public GameRulesConfig Rules => rules;

        /// <summary>맵을 시작한 뒤 흐른 시간(초).</summary>
        public float ClearTime { get; private set; }

        void Awake()
        {
            Current = this;
            entry = mapList.FindByScene(gameObject.scene.name);
            if (entry == null) Debug.LogError($"[MapManager] 맵 목록에 씬 '{gameObject.scene.name}'이 없다. Assets/Data/Map/MapList에 더한다.", this);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            // 열린 맵에서 바로 시작하면 메뉴를 거치지 않으므로 여기서 Playing으로 맞춘다.
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.StartGame();
            playerColor = player.GetComponent<PlayerColor>();
            var grid = FindAnyObjectByType<CubeGrid>();
            int lowest = grid != null && CubeGridRules.TryLowestY(grid.Data, out int y) ? y : 0;
            fallHeight = lowest - rules.fallDepth;
            Begin(SceneFlow.TakeContinueRequest());
        }

        // 메뉴의 '이어하기'로 열렸으면 그 맵의 체크포인트 기록으로 시작하고(플레이 시간도 이어짐), 아니면 시작 지점에서 시작한다.
        // UI 씬이 검은 화면에서 밝아지므로 페이드는 따로 없다.
        void Begin(bool continueFromCheckpoint)
        {
            if (continueFromCheckpoint)
            {
                if (ProgressStore.Current.TryGetCheckpoint(MapId, entry.version, out var saved, out bool discarded))
                {
                    checkpoint = saved;
                    ClearTime = saved.playTime;
                    Restore(saved);
                    return;
                }
                if (discarded) ProgressStore.Save();
            }
            MovePlayerToStart();
        }

        // 게임 중에만 흐른다: 일시정지(Paused)와 클리어 뒤에는 멈춘다.
        void Update()
        {
            if (GameManager.Instance.State != GameState.Playing) return;
            ClearTime += Time.deltaTime;
            if (player.transform.position.y < fallHeight) CatchFall();
        }

        /// <summary>목적지가 부른다: 클리어와 최고 시간을 기록하고 체크포인트 기록을 지운 뒤(바로 저장), '맵 클리어'를 보내고 상태를 Cleared로 바꾼다.</summary>
        public void Clear()
        {
            if (GameManager.Instance.State != GameState.Playing) return;
            var progress = ProgressStore.Current;
            progress.RecordClear(MapId, ClearTime);
            progress.ClearCheckpoint(MapId);
            ProgressStore.Save();
            EventBus.Publish(new MapCleared(MapId, ClearTime));
            GameManager.Instance.ClearMap();
        }

        public void Register(FixtureBase fixture)
        {
            if (fixtures.TryGetValue(fixture.FixtureId, out var other) && other != fixture)
            {
                Debug.LogError($"[MapManager] 맵 안 id {fixture.FixtureId}가 겹친다: '{other.name}', '{fixture.name}'. Slime~ColorMaker~/맵 안 id 고치기로 고친다.", fixture);
                return;
            }
            fixtures[fixture.FixtureId] = fixture;
        }

        public void Unregister(FixtureBase fixture)
        {
            if (fixtures.TryGetValue(fixture.FixtureId, out var registered) && registered == fixture)
                fixtures.Remove(fixture.FixtureId);
        }

        /// <summary>체크포인트가 처음 닿을 때 부른다: 활성화하고 지금 상태를 스냅샷으로 남긴다.</summary>
        public void ReachCheckpoint(Checkpoint reached)
        {
            if (GameManager.Instance.State != GameState.Playing || reached.IsActivated) return;
            reached.SetActivated(true);
            checkpoint = Capture(reached);
            ProgressStore.Current.SetCheckpoint(checkpoint);
            ProgressStore.Save();
            EventBus.Publish(new CheckpointReached());
        }

        /// <summary>
        /// '체크포인트로': 검은 화면 사이에 마지막 체크포인트의 상태로 되돌린다. 플레이 시간은 되돌리지 않는다.
        /// 아직 체크포인트에 닿지 않았으면 '처음부터'와 같다.
        /// </summary>
        public void ReturnToCheckpoint()
        {
            if (checkpoint == null) SceneFlow.Restart();
            else ScreenFade.FadeThrough(() => Restore(checkpoint));
        }

        // 플레이어는 체크포인트의 자리와 방향에 선다(시선은 체크포인트가 정함).
        MapSnapshot Capture(Checkpoint at)
        {
            var snapshot = new MapSnapshot
            {
                mapId = MapId,
                mapVersion = entry.version,
                playerPosition = at.transform.position,
                playerYaw = at.SpawnRotation.eulerAngles.y,
                currentColor = playerColor.Current.Value,
                playTime = ClearTime,
            };
            foreach (var fixture in fixtures.Values)
            {
                if (fixture is ISnapshotable saved && saved.CaptureState() is { } state)
                    snapshot.fixtures.Add(new FixtureStateEntry { fixtureId = fixture.FixtureId, state = state });
                if (fixture is Checkpoint { IsActivated: true }) snapshot.activatedCheckpointIds.Add(fixture.FixtureId);
            }
            return snapshot;
        }

        // 기물 → 플레이어 → 지금 색 순서(지금 색이 바뀌면 레이저 벽이 막을지 다시 봄).
        void Restore(MapSnapshot snapshot)
        {
            var states = new Dictionary<int, string>();
            foreach (var entry in snapshot.fixtures) states[entry.fixtureId] = entry.state;
            foreach (var fixture in fixtures.Values)
            {
                if (fixture is ISnapshotable saved) saved.RestoreState(states.GetValueOrDefault(fixture.FixtureId));
                if (fixture is Checkpoint point) point.SetActivated(snapshot.activatedCheckpointIds.Contains(fixture.FixtureId));
            }
            player.Teleport(snapshot.playerPosition, Quaternion.Euler(0f, snapshot.playerYaw, 0f));
            playerColor.Current.Restore(snapshot.currentColor);
        }

        // 낙사 안전망: '체크포인트로'와 같다(체크포인트가 없으면 '처음부터'). 페이드 중에는 다시 부르지 않는다.
        void CatchFall()
        {
            if (ScreenFade.IsFading) return;
            ReturnToCheckpoint();
        }

        void MovePlayerToStart()
        {
            foreach (var fixture in fixtures.Values)
            {
                if (fixture is StartPoint start)
                {
                    player.Teleport(start.transform.position, start.SpawnRotation);
                    return;
                }
            }
            Debug.LogError("[MapManager] 시작 지점이 없다.", this);
        }
    }
}
