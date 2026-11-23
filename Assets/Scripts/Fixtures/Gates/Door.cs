using System;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 문(문 계열). 열림을 맡고, 겉모습은 <see cref="OpenableView"/>가 맡는다: 칸 기물 방벽(프리팹 `Barrier`, <see cref="BarrierView"/>, 칸의 한 면을 덮는 반투명 판이 흐려지며 사라짐, 흔들리지 않음).
    /// 닿으면 지금 색이 요구 색과 정확히 같을 때 지금 색을 (0,0,0)으로 한 번 비우고 이어진 방벽 묶음(<see cref="DoorGroup"/>, 판이 맞닿은 같은 색 방벽) 전체가 함께 열린다.
    /// 한 번 열리면 계속 열려 있다. 다르면 '색 거부'를 보내고 흔들림이 있으면 흔든다(지금 색은 그대로). 닫혀 있는 동안만 요구 색을 보인다.
    /// 체크포인트에는 문마다 열림 여부를 저장한다.
    /// </summary>
    public class Door : GateBase, IInteractable, ISnapshotable
    {
        [Serializable]
        struct State
        {
            public bool open;
        }

        [SerializeField] OpenableView view;

        [Tooltip("색이 다를 때 흔들 겉모습. 비우면 흔들지 않는다(방벽).")]
        [SerializeField] ShakeView shake;

        CellFaceMount mount;
        DoorGroup group;

        public bool IsOpen { get; private set; }
        public bool CanInteract => !IsOpen;

        /// <summary>이 문이 든 이어진 방벽 묶음(닫힌 문만).</summary>
        public DoorGroup Group
        {
            get => group = DoorGroup.Of(this, group);
            internal set => group = value;
        }

        /// <summary>막는 콜라이더 전체의 경계 상자.</summary>
        public Bounds BlockerBox
        {
            get
            {
                var box = new Bounds(transform.position, Vector3.zero);
                bool first = true;
                foreach (var blocker in Blockers)
                {
                    if (first) box = blocker.bounds;
                    else box.Encapsulate(blocker.bounds);
                    first = false;
                }
                return box;
            }
        }

        public override bool TryGetDisplayValue(out ColorValue value) => base.TryGetDisplayValue(out value) && !IsOpen;

        protected override void Awake()
        {
            base.Awake();
            mount = GetComponent<CellFaceMount>();
        }

        // 모든 문이 등록된 뒤라 묶음을 알 수 있다. 방벽 겉모습은 열릴 때만 켜져 자기 Start가 불리지 않으므로 여기서 가장자리를 정한다.
        void Start()
        {
            if (view is BarrierView barrier) barrier.MarkEdges();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            DoorGroup.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DoorGroup.Unregister(this);
        }

        /// <summary>판 네 꼭짓점(두 배 정수 좌표, <see cref="DoorGroup.FaceCorners"/>). 칸 면에 붙지 않은 문이면 false.</summary>
        public bool TryGetCorners(out Vector3Int[] corners)
        {
            corners = mount != null ? DoorGroup.FaceCorners(transform.position, mount.Face) : null;
            return corners != null;
        }

        public void Interact(PlayerContext player)
        {
            if (IsOpen) return;
            if (!player.Color.TrySpend(RequiredColor))
            {
                if (shake != null) shake.Shake();
                EventBus.Publish(new ColorRejected());
                return;
            }
            foreach (var member in Group.Members) member.Open();
            EventBus.Publish(new DoorOpened());
        }

        void Open()
        {
            IsOpen = true;
            SetBlocking(false);
            view.SetOpen(true);
            DoorGroup.Changed();
        }

        public string CaptureState() => JsonUtility.ToJson(new State { open = IsOpen });

        public void RestoreState(string state)
        {
            IsOpen = JsonUtility.FromJson<State>(state).open;
            SetBlocking(!IsOpen);
            view.SetOpenImmediate(IsOpen);
            DoorGroup.Changed();
        }
    }
}
