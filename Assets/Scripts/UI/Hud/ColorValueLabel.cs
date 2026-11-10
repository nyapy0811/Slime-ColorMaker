using TMPro;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 'R G B' 값 상자(예: 2 0 0, 그림 2·3·5). 채널마다 글자가 하나라 프리팹 배치만 다르게 해서 같이 쓴다:
    /// 가까운 값 표시와 지금 색 표시가 쓴다.
    /// </summary>
    public class ColorValueLabel : MonoBehaviour
    {
        [Tooltip("R, G, B 순서의 글자")]
        [SerializeField] TMP_Text[] channels = new TMP_Text[3];

        public void Show(ColorValue value)
        {
            channels[0].text = value.R.ToString();
            channels[1].text = value.G.ToString();
            channels[2].text = value.B.ToString();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
