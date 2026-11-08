using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 목적지(맵당 1개): 문과 같은 방식이다. 닿으면 지금 색이 요구 색과 정확히 같을 때 맵 매니저에 클리어를 요청한다.
    /// 다르면 흔들리고 '색 거부'를 보낸다. 클리어한 뒤에는 상호작용하지 않는다. 외형은 문과 다르게 한다(임시: 넓고 낮은 원판).
    /// </summary>
    public class Goal : RequiredColorBase, IInteractable
    {
        [SerializeField] ShakeView shake;

        bool cleared;

        public bool CanInteract => !cleared;

        public void Interact(PlayerContext player)
        {
            if (cleared) return;
            if (!player.Color.TrySpend(RequiredColor))
            {
                shake.Shake();
                EventBus.Publish(new ColorRejected());
                return;
            }
            cleared = true;
            RefreshHighlight();
            MapManager.Current.Clear();
        }
    }
}
