using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>맵 선택 화면의 맵 버튼 하나: 이름과 상태(클리어·최고 시간, 체크포인트). 고른 버튼은 밝게 보인다.</summary>
    public class MapSelectItem : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Color normalColor = new(0.3f, 0.3f, 0.33f);
        [SerializeField] Color selectedColor = new(0.45f, 0.45f, 0.55f);

        public void Show(string displayName, string status, Action onClick)
        {
            nameText.text = displayName;
            statusText.text = status;
            button.onClick.AddListener(() => onClick());
        }

        public void SetSelected(bool selected) => background.color = selected ? selectedColor : normalColor;
    }
}
