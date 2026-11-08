using System;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMaze
{
    /// <summary>
    /// 팝업 호스트(UIScene, 코드 흐름 Q493). 켜질 때 정적 접근점(<see cref="Popups"/>)에 등록한다.
    /// 한 번에 팝업 하나만 열고(열려 있으면 다른 열기는 무시), 세션 종류에 맞는 화면을 고르고, 게임플레이 입력을 잠그고,
    /// 커서를 보이고, 뒤로 가기 스택에 닫기를 넣는다. ESC나 닫기 버튼으로 닫으면 화면을 숨기고 '닫힐 때 할 일'을 부른다.
    /// '팝업 열림·닫힘' 이벤트는 두지 않는다: 입력이 잠기면 가까운 값 표시가 저절로 숨는다(8단계 구상 채팅).
    /// </summary>
    public class PopupHost : MonoBehaviour, IPopupHost
    {
        [Tooltip("팝업이 열린 동안 켜는 부분(어두운 배경, 틀, 닫기 버튼)")]
        [SerializeField] GameObject root;

        [SerializeField] Button closeButton;

        [Tooltip("세션 종류마다 하나인 팝업 화면")]
        [SerializeField] PopupView[] views;

        PopupView current;
        Action onClosed;

        public bool IsOpen => current != null;

        void Awake()
        {
            root.SetActive(false);
            foreach (var view in views) view.gameObject.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        void OnEnable() => Popups.Register(this);

        // 열린 채로 씬이 내려가면(처음부터) 스택과 잠금만 치운다. 세션은 함께 버려진다.
        void OnDisable()
        {
            BackStack.Remove(Close);
            if (IsOpen && InputManager.Instance != null) InputManager.Instance.UnlockGameplay(this);
            Popups.Unregister(this);
        }

        public bool TryOpen(object session, Action onClosed)
        {
            if (IsOpen) return false;
            var view = Array.Find(views, v => v.Accepts(session));
            if (view == null)
            {
                Debug.LogError($"[PopupHost] {session.GetType().Name}을 그릴 팝업 화면이 없다.", this);
                return false;
            }

            current = view;
            this.onClosed = onClosed;
            root.SetActive(true);
            view.gameObject.SetActive(true);
            view.Show(session);
            InputManager.Instance.LockGameplay(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            BackStack.Push(Close);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            BackStack.Remove(Close);
            current.Hide();
            current.gameObject.SetActive(false);
            root.SetActive(false);
            current = null;
            InputManager.Instance.UnlockGameplay(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }
    }
}
