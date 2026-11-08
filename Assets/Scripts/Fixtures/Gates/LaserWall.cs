using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 레이저 벽(6.5). 막음을 맡고, 겉모습은 <see cref="PassableView"/>가 맡는다: 노드 시스템의 레이저 면(<see cref="LaserSegment"/>,
    /// <see cref="LaserWallView"/>)이나 칸 기물 차단기(2026-11-08, 프리팹 `BoomGate`, <see cref="BoomGateView"/>, 막대가 들리고 내려옴).
    /// 요구 색은 빼는 색이다: 지금 색의 모든 채널이 빼는 색 이상이면 막지 않고(레이저가 흐려짐), 레이저를 다 지나 반대쪽으로
    /// 나가면 지금 색에서 빼고 '레이저 통과'를 보낸다(들어갔다 되돌아오면 빼지 않음). 모자라면 막는다. 상호작용은 없다.
    /// 지나감은 막는 콜라이더와 같은 자리의 트리거로 잰다: 트리거 가운데를 기준으로 들어온 쪽과 나간 쪽(앞 축)이 다르면 지나간 것이다.
    /// 플레이어가 레이저 자리에 있는 동안은 막지 않던 레이저를 켜지 않는다(플레이어 위로 켜지지 않게).
    /// 저장할 상태는 없다(지금 색에서 다시 계산). 체크포인트로 돌아갈 때는 지나감 추적만 비운다:
    /// 순간이동은 트리거 나감을 보내지 않아, 레이저 안에서 돌아가면 '안에 있음'이 남아 레이저가 다시 켜지지 않는다.
    /// </summary>
    public class LaserWall : GateBase, ISnapshotable
    {
        [SerializeField] PassableView view;

        BoxCollider trigger;
        bool canPass;
        bool inside;
        float entrySide;

        // 막는 콜라이더는 루트 바로 아래 자식이고(돌리지 않음) 루트는 늘이지 않는다(레이저 면, 차단기의 막는 상자).
        protected override void Awake()
        {
            base.Awake();
            var blocker = Blockers[0];
            trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center));
            trigger.size = Vector3.Scale(blocker.size, blocker.transform.localScale);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<CurrentColorChanged>(OnCurrentColorChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<CurrentColorChanged>(OnCurrentColorChanged);
        }

        void OnCurrentColorChanged(CurrentColorChanged e)
        {
            canPass = ColorRules.IsAtLeast(e.Value, RequiredColor);
            Refresh();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerColor _)) return;
            inside = true;
            entrySide = Side(other.transform.position);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out PlayerColor player)) return;
            inside = false;
            if (Side(other.transform.position) != entrySide)
            {
                player.Current.Pass(RequiredColor);
                EventBus.Publish(new LaserPassed());
            }
            Refresh();
        }

        void Refresh()
        {
            bool blocking = !canPass;
            if (blocking == IsBlocking || (blocking && inside)) return;
            SetBlocking(blocking);
            view.SetPassable(!blocking);
        }

        public string CaptureState() => null;

        public void RestoreState(string state)
        {
            inside = false;
            Refresh();
        }

        // 차단기는 칸 가장자리에 서 있어 루트가 아니라 트리거 가운데를 기준으로 한다.
        float Side(Vector3 position) => Mathf.Sign(Vector3.Dot(position - transform.TransformPoint(trigger.center), transform.forward));
    }
}
