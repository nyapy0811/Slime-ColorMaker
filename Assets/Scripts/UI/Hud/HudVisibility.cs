using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// HUD 묶음(UIScene의 Hud)을 맵이 있을 때만 보인다(메인 메뉴에서는 숨김).
    /// 오브젝트를 끄지 않고 투명도로 숨기므로 안의 HUD는 이벤트를 계속 받는다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HudVisibility : MonoBehaviour
    {
        CanvasGroup group;

        void Awake() => group = GetComponent<CanvasGroup>();

        void Update() => group.alpha = MapManager.Current != null ? 1f : 0f;
    }
}
