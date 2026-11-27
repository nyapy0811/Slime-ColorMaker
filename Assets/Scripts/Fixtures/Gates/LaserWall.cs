using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 레이저 벽(문 계열). 막음을 맡는다: 칸 기물 방벽(프리팹 `Barrier`, 겉모습 <see cref="BarrierView"/>, 칸의 한 면을 덮는 반투명 판,
    /// 지나갈 수 있는지와 상관없이 늘 보임).
    /// 요구 색은 빼는 색이다: 지금 색의 모든 채널이 빼는 색 이상이면 막지 않고, 판을 다 지나 반대쪽으로 나가면 지금 색에서 빼고
    /// '레이저 통과'를 보낸다(들어갔다 되돌아오면 빼지 않음). 모자라면 막는다. 상호작용은 없다.
    /// 이어진 방벽 묶음(<see cref="BarrierGroup"/>, 판이 맞닿은 같은 색 방벽)은 한 벽이다: 지나가면 한 번만 빼고, 캐릭터가 묶음 안에 있는 동안은 다시 막지 않는다.
    /// 지나감은 막는 상자와 같은 자리의 트리거로 잰다: 판의 얇은 축 기준으로 묶음에 들어온 쪽과 마지막으로 나간 쪽이 다르면 지나간 것이다.
    /// 저장할 상태는 없다(지금 색에서 다시 계산). 체크포인트로 돌아갈 때는 지나감 추적만 비운다:
    /// 순간이동은 트리거 나감을 보내지 않아, 판 안에서 돌아가면 '안에 있음'이 남아 다시 막히지 않는다.
    /// </summary>
    public class LaserWall : GateBase, ISnapshotable
    {
        BoxCollider blocker;
        BoxCollider trigger;
        CellFaceMount mount;
        BarrierGroup group;
        bool canPass;

        /// <summary>이 방벽이 든 이어진 방벽 묶음.</summary>
        public BarrierGroup Group
        {
            get => group = BarrierGroup.Of(this, group);
            internal set => group = value;
        }

        /// <summary>판의 경계 상자(늘 켜진 트리거라 지나갈 수 있어 막는 상자가 꺼져 있어도 맞음).</summary>
        public Bounds BlockerBox => trigger.bounds;

        // 트리거 알림을 이 컴포넌트가 받도록 루트에 막는 상자(판)와 같은 자리·크기의 트리거를 단다.
        // 판은 90°씩만 돌아가 있어(CellFaceMount) 루트 축에 맞춘 상자로 그대로 옮길 수 있다.
        protected override void Awake()
        {
            base.Awake();
            mount = GetComponent<CellFaceMount>();
            blocker = Blockers[0];
            var turned = Quaternion.Inverse(transform.rotation) * blocker.transform.rotation * Vector3.Scale(blocker.size, blocker.transform.lossyScale);
            trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center));
            trigger.size = new Vector3(Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            BarrierGroup.Register(this);
            EventBus.Subscribe<CurrentColorChanged>(OnCurrentColorChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            BarrierGroup.Unregister(this);
            EventBus.Unsubscribe<CurrentColorChanged>(OnCurrentColorChanged);
        }

        /// <summary>판 네 꼭짓점(두 배 정수 좌표, <see cref="BarrierGroup.FaceCorners"/>). 칸 면에 붙지 않았으면 false.</summary>
        public bool TryGetCorners(out Vector3Int[] corners)
        {
            corners = mount != null ? BarrierGroup.FaceCorners(transform.position, mount.Face) : null;
            return corners != null;
        }

        void OnCurrentColorChanged(CurrentColorChanged e)
        {
            canPass = ColorRules.IsAtLeast(e.Value, RequiredColor);
            Refresh();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerColor _)) return;
            Group.Enter(other.transform.position);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out PlayerColor player)) return;
            if (Group.Exit(this, other.transform.position))
            {
                player.Current.Pass(RequiredColor);
                EventBus.Publish(new LaserPassed());
            }
            if (!Group.IsInside)
                foreach (var member in Group.Members) member.Refresh();
        }

        void Refresh()
        {
            bool blocking = !canPass;
            if (blocking == IsBlocking || (blocking && Group.IsInside)) return;
            SetBlocking(blocking);
        }

        public string CaptureState() => null;

        public void RestoreState(string state)
        {
            Group.ResetInside();
            Refresh();
        }

        /// <summary>점이 판의 어느 쪽에 있는가(판의 얇은 축 기준 +1 / −1).</summary>
        public float Side(Vector3 position)
        {
            var size = blocker.size;
            var axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            var normal = blocker.transform.TransformDirection(axis);
            return Mathf.Sign(Vector3.Dot(position - blocker.transform.TransformPoint(blocker.center), normal));
        }
    }
}
