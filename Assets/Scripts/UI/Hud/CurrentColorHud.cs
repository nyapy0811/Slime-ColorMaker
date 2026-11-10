using Framework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 지금 색 표시(UIScene HUD, 왼쪽 아래, 임시 자리·모양). '지금 색 바뀜'을 받아 색 견본(가장 큰 채널을 최대 밝기로 한 비율, (0,0,0)은 검정)과
    /// 'R G B' 값을 늘 보인다. 플레이어가 시작할 때도 값을 보내므로 처음부터 다시 해도 맞춰진다.
    /// </summary>
    public class CurrentColorHud : MonoBehaviour
    {
        [SerializeField] Image swatch;
        [SerializeField] ColorValueLabel label;

        void OnEnable() => EventBus.Subscribe<CurrentColorChanged>(OnCurrentColorChanged);
        void OnDisable() => EventBus.Unsubscribe<CurrentColorChanged>(OnCurrentColorChanged);

        void OnCurrentColorChanged(CurrentColorChanged e)
        {
            swatch.color = ColorRules.ToColor(e.Value);
            label.Show(e.Value);
        }
    }
}
