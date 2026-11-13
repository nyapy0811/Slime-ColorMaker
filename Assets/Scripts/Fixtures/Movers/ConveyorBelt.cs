using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 컨베이어 벨트(2026-11-12, 이동 계열, 프리팹 `ConveyorBelt`). 블록 자리(칸 하나)에 깔리고 윗면이 블록 윗면과 같은 높이다.
    /// 무늬 모델 넷(`ConveyorBelt1~4`)을 1 → 2 → 3 → 4 → 1 차례로 하나씩 켠다. 차례는 게임 시간으로 정해 씬의 모든 벨트가 같은 순간에 같은 번호를 보이고,
    /// 일시정지하면 멈춘다. 모델 기준 흐름은 −Z이고 놓을 때 Y축으로 90°씩 돌린다.
    /// 캐릭터 발바닥이 조금이라도 걸치면 <see cref="ThirdPersonController"/>가 <see cref="PushVelocity"/>만큼 민다(벨트 속도, 2026-11-13).
    /// 방향은 걸친 벨트 중 중심점에 가까운 벨트와 꺾이는 칸의 대각선(늘인 직선)으로 정한다(<see cref="ConveyorRules"/>).
    /// 켜져 있는 벨트는 칸으로 찾을 수 있게 등록해 둔다.
    /// </summary>
    public class ConveyorBelt : FixtureBase
    {
        /// <summary>무늬 한 장마다 흐르는 거리(복셀 한 칸, 유닛). 바닥 흔적의 픽셀 한 칸과 같다.</summary>
        public const float StepPerFrame = 1f / 16f;

        static readonly Dictionary<Vector3Int, ConveyorBelt> ByCell = new();
        static readonly List<Vector2Int> TouchedCells = new();
        static readonly Func<Vector2Int, Vector2Int> FlowAtLevel = cell =>
            ByCell.TryGetValue(new Vector3Int(cell.x, level, cell.y), out var belt) ? belt.Flow : Vector2Int.zero;
        static int level; // FlowAtLevel이 찾는 칸 높이

        [SerializeField] ConveyorBeltSettings settings;

        [Tooltip("무늬 모델(ConveyorBelt1~4 차례). 하나씩 켠다.")]
        [SerializeField] GameObject[] frames;

        Vector3Int cell;
        int shown = -1;

        /// <summary>흐르는 방향(바닥 평면 x, z의 4방향 단위).</summary>
        Vector2Int Flow
        {
            get
            {
                var direction = transform.rotation * Vector3.back;
                return new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
            }
        }

        /// <summary>
        /// 발바닥이 걸친 벨트 <paramref name="touched"/>(한 높이)와 캐릭터 중심점 <paramref name="center"/>로 정한 미는 속도(월드, 유닛/초). 걸친 벨트가 없으면 0.
        /// </summary>
        public static Vector3 PushVelocity(Vector3 center, IReadOnlyList<ConveyorBelt> touched)
        {
            if (touched.Count == 0) return Vector3.zero;
            TouchedCells.Clear();
            foreach (var belt in touched) TouchedCells.Add(new Vector2Int(belt.cell.x, belt.cell.z));
            level = touched[0].cell.y;
            var direction = ConveyorRules.PushDirection(new Vector2(center.x, center.z), TouchedCells, FlowAtLevel);
            return new Vector3(direction.x, 0f, direction.y) * touched[0].settings.speed;
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

        /// <summary>
        /// 한 점(바닥 흔적의 픽셀 가운데)이 벨트를 따라 움직일 방향(바닥 평면 x, z의 4방향 단위). 점 바로 아래 칸이 벨트면
        /// 캐릭터와 같은 규칙(걸친 칸 = 그 칸 하나, 중심점 = 그 점)으로 정하고, 아니면 0.
        /// </summary>
        public static Vector2Int PointDirection(Vector3 point)
        {
            var below = CubeGridRules.CellAt(point + Vector3.down * 0.05f);
            if (!ByCell.ContainsKey(below)) return Vector2Int.zero;
            TouchedCells.Clear();
            TouchedCells.Add(new Vector2Int(below.x, below.z));
            level = below.y;
            return ConveyorRules.PushDirection(new Vector2(point.x, point.z), TouchedCells, FlowAtLevel);
        }

        int Step => (int)(Time.time * settings.speed / StepPerFrame);

        protected override void OnEnable()
        {
            base.OnEnable();
            cell = CubeGridRules.CellAt(transform.position);
            ByCell[cell] = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (ByCell.TryGetValue(cell, out var registered) && registered == this) ByCell.Remove(cell);
        }

        void Update()
        {
            int index = Step % frames.Length;
            if (index == shown) return;
            for (int i = 0; i < frames.Length; i++) frames[i].SetActive(i == index);
            shown = index;
        }
    }
}
