using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 일시정지 메뉴(UIScene). ESC를 읽는 곳이기도 하다:
    /// 뒤로 가기 스택에 닫을 화면이 있으면 맨 위를 닫고, 스택이 비어 있고 게임 중일 때만 일시정지를 연다.
    /// 일시정지는 잠금이 아니라 <c>Time.timeScale = 0</c>으로 멈추고, 열린 동안 스택에 '계속하기'를 넣어 ESC로 닫힌다.
    /// 버튼은 계속하기·처음부터·체크포인트로·메인메뉴이고, 진행이 사라지는 세 버튼은 확인 창을 거친다.
    /// '설정'은 설정 화면을 만들 때 더한다. 화면 페이드 중에는 ESC를 받지 않는다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button checkpointButton;
        [SerializeField] Button mainMenuButton;

        void Awake()
        {
            panel.SetActive(false);
            resumeButton.onClick.AddListener(Resume);
            restartButton.onClick.AddListener(AskRestart);
            checkpointButton.onClick.AddListener(AskReturnToCheckpoint);
            mainMenuButton.onClick.AddListener(AskMainMenu);
        }

        void OnDisable() => BackStack.Remove(Resume);

        void Update()
        {
            if (!InputManager.Instance.ReadCancel() || ScreenFade.IsFading) return;
            if (BackStack.TryCloseTop()) return;
            if (GameManager.Instance.State == GameState.Playing) Pause();
        }

        void Pause()
        {
            GameManager.Instance.ChangeState(GameState.Paused);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panel.SetActive(true);
            BackStack.Push(Resume);
        }

        void Resume()
        {
            BackStack.Remove(Resume);
            panel.SetActive(false);
            Time.timeScale = 1f;
            GameManager.Instance.ChangeState(GameState.Playing);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 씬을 불러오면 UI 씬도 다시 얹히므로 메뉴는 검은 화면 아래에 그대로 둔다.
        void AskRestart() => Confirm.Ask("처음부터 다시 시작할까요?\n저장된 체크포인트도 지워집니다.", () =>
        {
            BackStack.Remove(Resume);
            SceneFlow.Restart();
        });

        // 맵 씬은 그대로이므로 메뉴를 닫고 게임으로 돌아간 뒤 되돌린다. 체크포인트가 없으면 '처음부터'와 같다.
        void AskReturnToCheckpoint()
        {
            string message = MapManager.Current.HasCheckpoint
                ? "마지막 체크포인트로 돌아갈까요?\n그 뒤의 진행은 사라집니다."
                : "닿은 체크포인트가 없어 처음부터 다시 시작합니다.\n돌아갈까요?";
            Confirm.Ask(message, () =>
            {
                Resume();
                MapManager.Current.ReturnToCheckpoint();
            });
        }

        void AskMainMenu() => Confirm.Ask("메인 메뉴로 갈까요?\n마지막 체크포인트 뒤의 진행은 사라집니다.", () =>
        {
            BackStack.Remove(Resume);
            SceneFlow.GoToMainMenu();
        });
    }
}
