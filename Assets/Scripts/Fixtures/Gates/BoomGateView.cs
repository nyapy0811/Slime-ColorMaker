using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 차단기의 겉모습(2026-11-08, 모델 Models/Fixtures/BoomGate): 지나갈 수 있으면 막대가 경첩(막대 끝 칸 가운데)을 축으로
    /// 들리고, 막으면 내려온다. 경첩은 앞(+Z) 축으로 돌고, 막대는 경첩에서 −X 쪽으로 뻗어 있다.
    /// </summary>
    public class BoomGateView : PassableView
    {
        [Tooltip("막대를 돌리는 경첩(막대 끝 칸 가운데). 막대 모델은 이 아래에 있다.")]
        [SerializeField] Transform hinge;

        [Tooltip("다 들렸을 때 막대 각도(도). 90이면 똑바로 선다.")]
        [SerializeField, Range(0f, 180f)] float raisedAngle = 90f;

        [Tooltip("다 들리거나 내려오는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.4f;

        float raised;
        float target;

        void Awake() => enabled = false;

        public override void SetPassable(bool passable)
        {
            target = passable ? 1f : 0f;
            enabled = true;
        }

        void Update()
        {
            raised = Mathf.MoveTowards(raised, target, Time.deltaTime / duration);
            hinge.localRotation = Quaternion.AngleAxis(-raisedAngle * Mathf.SmoothStep(0f, 1f, raised), Vector3.forward);
            if (raised == target) enabled = false;
        }
    }
}
