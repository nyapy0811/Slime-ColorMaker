using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 확인 창(UIScene). 일시정지 메뉴·클리어 화면보다 위, 화면 페이드보다 아래에 그린다.
    /// 열 때 뒤로 가기 스택에 '취소'를 넣어 ESC로 닫힌다. 시간을 쓰지 않으므로 일시정지 중에도 동작하고,
    /// 커서가 이미 보이는 화면(일시정지, 메뉴, 클리어 화면)에서만 열리므로 커서와 입력 잠금은 건드리지 않는다.
    /// 한 번에 하나만 연다(열려 있으면 다른 요청은 무시).
    /// </summary>
    public class ConfirmDialog : MonoBehaviour, IConfirmDialog
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;

        Action onConfirm;

        void Awake()
        {
            panel.SetActive(false);
            confirmButton.onClick.AddListener(ConfirmAndClose);
            cancelButton.onClick.AddListener(Close);
        }

        void OnEnable() => Confirm.Register(this);

        void OnDisable()
        {
            BackStack.Remove(Close);
            Confirm.Unregister(this);
        }

        public void Ask(string message, Action onConfirm)
        {
            if (panel.activeSelf) return;
            this.onConfirm = onConfirm;
            messageText.text = message;
            panel.SetActive(true);
            BackStack.Push(Close);
        }

        void Close()
        {
            BackStack.Remove(Close);
            panel.SetActive(false);
            onConfirm = null;
        }

        void ConfirmAndClose()
        {
            var confirmed = onConfirm;
            Close();
            confirmed?.Invoke();
        }
    }
}
