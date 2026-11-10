using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 체크포인트(출발점 계열). 플레이어가 처음 닿으면 활성화되고 맵 매니저에 스냅샷을 요청한다(이미 활성화되면 무시, 2차 Q450).
    /// 돌아오면 플레이어는 이 오브젝트의 자리와 방향에 선다. 닿음은 같은 오브젝트의 트리거 콜라이더로 잰다.
    /// 활성화 여부는 기물 상태가 아니라 스냅샷의 '활성화된 체크포인트 목록'에 든다.
    /// 겉모습은 발판 모델 둘 중 하나만 켠다(2026-11-08): 아직 안 닿음 = 비활성 발판, 닿아서 저장됨 = 활성 발판(시작 지점과 같은 모양).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : SpawnPointBase
    {
        [Tooltip("아직 닿지 않았을 때 보일 발판(PointDisactived)")]
        [SerializeField] GameObject inactiveView;

        [Tooltip("닿아서 저장됐을 때 보일 발판(PointActived)")]
        [SerializeField] GameObject activeView;

        public bool IsActivated { get; private set; }

        void Awake() => ShowState();

        void OnTriggerEnter(Collider other)
        {
            if (IsActivated || !other.TryGetComponent(out PlayerColor _)) return;
            MapManager.Current.ReachCheckpoint(this);
        }

        /// <summary>맵 매니저가 부른다(처음 닿음, 체크포인트 복귀).</summary>
        public void SetActivated(bool activated)
        {
            IsActivated = activated;
            ShowState();
        }

        void ShowState()
        {
            if (inactiveView != null) inactiveView.SetActive(!IsActivated);
            if (activeView != null) activeView.SetActive(IsActivated);
        }
    }
}
