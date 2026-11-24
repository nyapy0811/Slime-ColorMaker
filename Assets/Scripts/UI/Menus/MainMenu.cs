using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 메인 메뉴(MainMenu 씬). Play는 맵 선택 화면으로 바로 가고,
    /// '이어하기'는 저장된 체크포인트가 있을 때만 보인다. 그 맵이 맵 목록에 없거나 맵 버전이 다르면
    /// 기록을 버리고 버튼만 숨긴다. '설정'은 UI 씬의 설정 화면(<see cref="SettingsScreen"/>)을 연다.
    /// 열리면 상태를 MainMenu로 맞추고 커서를 보인다. 마지막 맵의 클리어 화면에서 오면 맵 선택 화면을 바로 연다.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] MapList mapList;
        [SerializeField] GameObject panel;
        [SerializeField] Button playButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] MapSelectScreen mapSelect;

        void Awake()
        {
            playButton.onClick.AddListener(OpenMapSelect);
            continueButton.onClick.AddListener(Continue);
            settingsButton.onClick.AddListener(() => SettingsScreen.Current.Open());
            quitButton.onClick.AddListener(GameManager.Instance.QuitGame);
        }

        void Start()
        {
            GameManager.Instance.ChangeState(GameState.MainMenu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var progress = ProgressStore.Current;
            if (progress.hasCheckpoint && progress.DiscardStaleCheckpoint(mapList.FindById(progress.checkpoint.mapId)?.version))
                ProgressStore.Save();
            continueButton.gameObject.SetActive(SavedMap() != null);

            if (SceneFlow.TakeMapSelectRequest()) OpenMapSelect();
        }

        MapListEntry SavedMap()
        {
            var progress = ProgressStore.Current;
            return progress.hasCheckpoint ? mapList.FindById(progress.checkpoint.mapId) : null;
        }

        void OpenMapSelect()
        {
            panel.SetActive(false);
            mapSelect.Open(() => panel.SetActive(true));
        }

        void Continue() => SceneFlow.StartMap(SavedMap(), true);
    }
}
