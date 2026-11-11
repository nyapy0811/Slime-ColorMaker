using System;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 문(6.5). 열림을 맡고, 겉모습은 <see cref="OpenableView"/>가 맡는다: 노드 시스템의 끼운 문(<see cref="DoorPiece"/>, <see cref="DoorView"/>)이나
    /// 칸 기물 방벽(2026-11-11, 프리팹 `Barrier`, <see cref="BarrierView"/>, 칸의 한 면을 덮는 반투명 판이 흐려지며 사라짐, 흔들리지 않음).
    /// 닿으면 지금 색이 요구 색과 정확히 같을 때 지금 색을 (0,0,0)으로 비우고 열린다. 한 번 열리면 계속 열려 있다.
    /// 다르면 '색 거부'를 보내고 흔들림이 있으면 흔든다(지금 색은 그대로). 닫혀 있는 동안만 요구 색을 보인다.
    /// 노드 끼운 문은 문 프리팹에 붙어 있어 노드 시스템이 만든 문마다 생기고, 다시 만들어도 끼운 문 id로 남아 인스펙터 값이 지켜진다.
    /// 체크포인트에는 열림 여부를 저장한다.
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

        public bool IsOpen { get; private set; }
        public bool CanInteract => !IsOpen;

        public override bool TryGetDisplayValue(out ColorValue value) => base.TryGetDisplayValue(out value) && !IsOpen;

        public void Interact(PlayerContext player)
        {
            if (IsOpen) return;
            if (!player.Color.TrySpend(RequiredColor))
            {
                if (shake != null) shake.Shake();
                EventBus.Publish(new ColorRejected());
                return;
            }
            IsOpen = true;
            SetBlocking(false);
            view.SetOpen(true);
            RefreshHighlight();
            EventBus.Publish(new DoorOpened());
        }

        public string CaptureState() => JsonUtility.ToJson(new State { open = IsOpen });

        public void RestoreState(string state)
        {
            IsOpen = JsonUtility.FromJson<State>(state).open;
            SetBlocking(!IsOpen);
            view.SetOpenImmediate(IsOpen);
            RefreshHighlight();
        }
    }
}
