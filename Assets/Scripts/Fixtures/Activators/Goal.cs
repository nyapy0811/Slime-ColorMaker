using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 목적지(맵당 1개): 문과 같은 방식이다. 닿으면 지금 색이 요구 색과 정확히 같을 때 맵 매니저에 클리어를 요청한다.
    /// 다르면 흔들리고 '색 거부'를 보낸다. 클리어한 뒤에는 상호작용하지 않는다. 외형은 므엥킹(2026-11-08, 프리팹 `MeuengKing`): 몸의 값 색 부분이 요구 색이고, 부딪혀 색을 제출한다(막힌 상자 콜라이더).
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
