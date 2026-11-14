using Framework.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 클리어 화면(UIScene, 겉모습은 UI 디자인 때). '맵 클리어'를 받아 클리어 시간과 이 맵의 최고 시간을 보이고
    /// (이번이 최고면 '새 기록'), 시간을 멈추고 커서를 보인다. 통계(수집률)는 수집 요소를 넣을 때 더한다.
    /// 버튼은 다음 맵·다시 하기·메인 메뉴다. 마지막 맵에서는 '다음 맵'이 '맵 선택'이 된다.
    /// 클리어 기록은 이미 저장했으므로 확인 창 없이 간다(다음 맵을 시작할 때의 체크포인트 경고만 거침).
    /// 상태가 Cleared라 ESC로 일시정지가 열리지 않는다.
    /// </summary>
    public class MapClearScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text nextLabel;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainMenuButton;

        MapListEntry next;

        void Awake()
        {
            panel.SetActive(false);
            nextButton.onClick.AddListener(GoNext);
            retryButton.onClick.AddListener(SceneFlow.Restart);
            mainMenuButton.onClick.AddListener(() => SceneFlow.GoToMainMenu());
        }

        void OnEnable() => EventBus.Subscribe<MapCleared>(OnMapCleared);
        void OnDisable() => EventBus.Unsubscribe<MapCleared>(OnMapCleared);

        // 맵 매니저가 기록을 저장한 뒤 보내므로 최고 시간은 이번 기록까지 들어 있다.
        void OnMapCleared(MapCleared e)
        {
            var map = MapManager.Current;
            float best = ProgressStore.Current.FindMap(e.MapId).bestTime;
            timeText.text = $"클리어 시간 {TimeFormat.ClearTime(e.ClearTime)}";
            bestText.text = best >= e.ClearTime ? "새 기록!" : $"최고 기록 {TimeFormat.ClearTime(best)}";
            next = map.MapList.NextAfter(map.Entry);
            nextLabel.text = next != null ? "다음 맵" : "맵 선택";

            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void GoNext()
        {
            if (next != null) SceneFlow.AskAndStartMap(next);
            else SceneFlow.GoToMainMenu(openMapSelect: true);
        }
    }
}
