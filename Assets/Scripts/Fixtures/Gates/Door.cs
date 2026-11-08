using System;
using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 끼운 문(6.5). 모양은 같은 오브젝트의 <see cref="DoorPiece"/>(노드 시스템)가 맡고, 이 컴포넌트는 열림을 맡는다(11.4).
    /// 닿으면 지금 색이 요구 색과 정확히 같을 때 지금 색을 (0,0,0)으로 비우고 열린다. 한 번 열리면 계속 열려 있다.
    /// 다르면 문이 흔들리고 '색 거부'를 보낸다(지금 색은 그대로). 닫혀 있는 동안만 요구 색을 보인다.
    /// 문 프리팹에 붙어 있어 노드 시스템이 만든 문마다 생기고, 다시 만들어도 끼운 문 id로 남아 인스펙터 값이 지켜진다.
    /// 체크포인트에는 열림 여부를 저장한다.
    /// </summary>
    [RequireComponent(typeof(DoorPiece))]
    public class Door : GateBase, IInteractable, ISnapshotable
    {
        [Serializable]
        struct State
        {
            public bool open;
        }

        [SerializeField] DoorView view;
        [SerializeField] ShakeView shake;

        public bool IsOpen { get; private set; }
        public bool CanInteract => !IsOpen;

        public override bool TryGetDisplayValue(out ColorValue value) => base.TryGetDisplayValue(out value) && !IsOpen;

        public void Interact(PlayerContext player)
        {
            if (IsOpen) return;
            if (!player.Color.TrySpend(RequiredColor))
            {
                shake.Shake();
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
