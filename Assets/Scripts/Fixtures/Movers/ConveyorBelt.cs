using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SlimeColorMaker
{
    /// <summary>
    /// 컨베이어 벨트(이동 계열, 프리팹 `ConveyorBelt` 하나). 블록 자리(칸 하나)에 깔리고 윗면이 블록 윗면과 같은 높이다.
    /// 모양(<see cref="Shape"/>: 직진·우회전·좌회전)마다 무늬 모델 넷(직선 `ConveyorBelt1~4`, 우회전 `ConveyorBeltCurve1~4`, 좌회전 `ConveyorBeltCurveL1~4`)을 두고,
    /// 지금 모양의 넷만 1 → 2 → 3 → 4 → 1 차례로 하나씩 켠다(다른 모양의 무늬는 모두 끔).
    /// 차례는 게임 시간으로 정해 씬의 모든 벨트가 같은 순간에 같은 번호를 보이고, 일시정지하면 멈춘다. 모델 기준 흐름(나가는 쪽)은 −Z이고 놓을 때 Y축으로 90°씩 돌린다.
    /// 캐릭터 중심점이 벨트 구역(난간 빼고) 안에 있으면 <see cref="ThirdPersonController"/>가 <see cref="TryPush"/>만큼 민다(벨트 속도).
    /// 직선은 똑바로, 꺾임은 안쪽 모서리를 중심으로 돌림판처럼 돌린다(<see cref="ConveyorRules"/>). 켜져 있는 벨트는 칸으로 찾을 수 있게 등록해 둔다.
    /// </summary>
    public class ConveyorBelt : FixtureBase
    {
        /// <summary>무늬 한 장마다 흐르는 거리(복셀 한 칸, 유닛). 바닥 흔적의 픽셀 한 칸과 같다.</summary>
        public const float StepPerFrame = 1f / 16f;

        static readonly Dictionary<Vector3Int, ConveyorBelt> ByCell = new();
        static readonly Func<Vector2Int, ConveyorCell> BeltAtLevel = cell =>
            ByCell.TryGetValue(new Vector3Int(cell.x, level, cell.y), out var belt) ? belt.Belt : default;
        static int level; // BeltAtLevel이 찾는 칸 높이

        [SerializeField] ConveyorBeltSettings settings;

        [Tooltip("직진 무늬 모델(ConveyorBelt1~4 차례). 하나씩 켠다.")]
        [FormerlySerializedAs("frames")]
        [SerializeField] GameObject[] straightFrames;

        [Tooltip("우회전 무늬 모델(ConveyorBeltCurve1~4 차례). 모델 기준 −X 쪽에서 들어와 −Z로 나간다.")]
        [SerializeField] GameObject[] rightFrames;

        [Tooltip("좌회전 무늬 모델(ConveyorBeltCurveL1~4 차례). 모델 기준 +X 쪽에서 들어와 −Z로 나간다.")]
        [SerializeField] GameObject[] leftFrames;

        [Tooltip("모양: 직진(Straight), 우회전(Right), 좌회전(Left). 큐브 맵 편집의 컨베이어 붓이 놓을 때 정한다.")]
        [SerializeField] ConveyorShape shape;

        Vector3Int cell;
        int shown = -1;

        /// <summary>나가는 방향(바닥 평면 x, z의 4방향 단위).</summary>
        Vector2Int Flow
        {
            get
            {
                var direction = transform.rotation * Vector3.back;
                return new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
            }
        }

        ConveyorCell Belt => new(Flow, shape);

        /// <summary>모양. 바꾸면 그 모양의 첫 무늬만 켠다(에디터에서 놓을 때는 프리팹 오버라이드로 남겨야 하므로 편집 도구가 기록한다).</summary>
        public ConveyorShape Shape
        {
            get => shape;
            set
            {
                shape = value;
                ShowShape();
            }
        }

        GameObject[] Frames => shape switch
        {
            ConveyorShape.Right => rightFrames,
            ConveyorShape.Left => leftFrames,
            _ => straightFrames,
        };

        /// <summary>모든 무늬 모델(세 모양).</summary>
        public IEnumerable<GameObject> AllFrames => straightFrames.Concat(rightFrames).Concat(leftFrames);

        /// <summary>지금 모양의 첫 무늬만 켜고 나머지(다른 모양 포함)는 끈다.</summary>
        public void ShowShape()
        {
            var frames = Frames;
            foreach (var frame in AllFrames)
                if (frame != null) frame.SetActive(frames.Length > 0 && frame == frames[0]);
            shown = -1;
        }

        /// <summary>
        /// 캐릭터 중심점(발밑, y = 바닥 높이) <paramref name="center"/>를 벨트가 이번 프레임(<paramref name="deltaTime"/>초)에 미는 속도(월드, 유닛/초).
        /// 중심점이 벨트 구역 밖이거나 시간이 흐르지 않으면 false(0). 꺾임은 돌림판으로 돈 만큼의 이동을 속도로 바꾼다.
        /// <paramref name="facing"/>·<paramref name="lockedAxis"/>는 마주 보는 벨트(<see cref="ConveyorRules.TryPush"/>, 축은 월드 단위 벡터, 없으면 0).
        /// </summary>
        public static bool TryPush(Vector3 center, float deltaTime, out Vector3 velocity, out bool facing, out Vector3 lockedAxis)
        {
            velocity = lockedAxis = Vector3.zero;
            facing = false;
            var below = CellBelow(center);
            if (deltaTime <= 0f || !ByCell.TryGetValue(below, out var belt)) return false;
            level = below.y;
            if (!ConveyorRules.TryPush(new Vector2(center.x, center.z), BeltAtLevel, belt.settings.speed * deltaTime, out var step, out facing, out var axis)) return false;
            velocity = new Vector3(step.x, 0f, step.y) / deltaTime;
            lockedAxis = new Vector3(axis.x, 0f, axis.y);
            return true;
        }

        /// <summary>캐릭터 중심점(발밑)이 벨트 구역 안인가.</summary>
        public static bool IsOnBelt(Vector3 center)
        {
            var below = CellBelow(center);
            return ByCell.TryGetValue(below, out var belt) && ConveyorRules.InZone(new Vector2Int(below.x, below.z), belt.Belt, new Vector2(center.x, center.z));
        }

        /// <summary>
        /// 무늬가 몇 칸 흘렀는지(게임 시간 × 속도 ÷ 1/16). 모든 벨트가 같은 값을 쓰고, 이 값이 하나 늘 때 무늬와 벨트 위 바닥 흔적이 한 칸 움직인다.
        /// 켜진 벨트가 없으면 false.
        /// </summary>
        public static bool TryGetStep(out int step)
        {
            foreach (var belt in ByCell.Values)
            {
                step = belt.Step;
                return true;
            }
            step = 0;
            return false;
        }

        /// <summary>바닥 위 점(y = 바닥 높이) 바로 아래 칸.</summary>
        public static Vector3Int CellBelow(Vector3 point) => CubeGridRules.CellAt(point + Vector3.down * 0.01f);

        /// <summary>바닥 위 점 바로 아래 칸이 벨트인가.</summary>
        public static bool IsBeltBelow(Vector3 point) => ByCell.ContainsKey(CellBelow(point));

        /// <summary>
        /// 바닥 위 점(바닥 흔적 픽셀)을 벨트를 따라 <paramref name="distance"/>(유닛)만큼 옮긴 자리(<see cref="ConveyorRules.TryAdvance"/>, 높이는 그대로).
        /// 점이 벨트 구역 밖(벨트가 아니거나 난간)이면 false.
        /// </summary>
        public static bool TryAdvance(Vector3 point, float distance, out Vector3 moved)
        {
            level = CellBelow(point).y;
            bool on = ConveyorRules.TryAdvance(new Vector2(point.x, point.z), distance, BeltAtLevel, out var flat);
            moved = new Vector3(flat.x, point.y, flat.y);
            return on;
        }

        /// <summary>꺾임 벨트 칸 <paramref name="cell"/>에서 점 (x, z)가 든 벨트 자리. 꺾임 벨트가 아니거나 난간이면 false.</summary>
        public static bool TryCurveSlot(Vector3Int cell, Vector3 point, out Vector2Int slot)
        {
            slot = default;
            return ByCell.TryGetValue(cell, out var belt) && belt.shape != ConveyorShape.Straight
                && ConveyorRules.TryCurveSlot(new Vector2Int(cell.x, cell.z), belt.Belt, new Vector2(point.x, point.z), out slot);
        }

        /// <summary>꺾임 벨트 칸 <paramref name="cell"/>의 벨트 자리 가운데(y = <paramref name="height"/>). 꺾임 벨트 칸에서만 부른다.</summary>
        public static Vector3 CurveSlotCenter(Vector3Int cell, Vector2Int slot, float height)
        {
            var point = ConveyorRules.CurveSlotCenter(new Vector2Int(cell.x, cell.z), ByCell[cell].Belt, slot);
            return new Vector3(point.x, height, point.y);
        }

        int Step => (int)(Time.time * settings.speed / StepPerFrame);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying) ShowShape(); // 다른 모양의 무늬가 켜져 있으면 끈다(Update는 지금 모양만 넘긴다).
            cell = CubeGridRules.CellAt(transform.position);
            ByCell[cell] = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (ByCell.TryGetValue(cell, out var registered) && registered == this) ByCell.Remove(cell);
        }

#if UNITY_EDITOR
        // 인스펙터에서 모양을 바꾸면(Play 밖) 그 모양의 무늬로 바꿔 보이고 오버라이드로 남긴다. 씬을 열 때는 이미 맞으므로 아무것도 안 한다.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !FramesMismatch()) return;
                foreach (var frame in AllFrames) if (frame != null) UnityEditor.Undo.RecordObject(frame, "컨베이어 모양");
                ShowShape();
                foreach (var frame in AllFrames) if (frame != null) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
            };
        }

        bool FramesMismatch()
        {
            var frames = Frames;
            if (frames == null || frames.Length == 0 || straightFrames == null || rightFrames == null || leftFrames == null) return false;
            return AllFrames.Any(frame => frame != null && frame.activeSelf && System.Array.IndexOf(frames, frame) < 0) || !frames.Any(frame => frame != null && frame.activeSelf);
        }
#endif

        void Update()
        {
            var frames = Frames;
            int index = Step % frames.Length;
            if (index == shown) return;
            for (int i = 0; i < frames.Length; i++) frames[i].SetActive(i == index);
            shown = index;
        }
    }
}
