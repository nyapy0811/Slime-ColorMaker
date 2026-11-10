using TMPro;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 진행 시간(화면 오른쪽 위, mm:ss)과 그 아래 이 맵의 최고 클리어 시간(mm:ss.ss, 클리어 화면과 같은 자리수).
    /// 진행 시간은 맵 매니저의 클리어 시간을 그대로 보이므로 일시정지·클리어 뒤에는 멈추고 '체크포인트로' 뒤에도 이어서 흐른다.
    /// 최고 시간은 진행 저장소의 맵 기록이고, 클리어해서 줄면 바로 바뀐다. 기록이 없으면 '--:--'.
    /// 맵이 없으면(메뉴 등) 숨긴다. 값이 바뀔 때만 글자를 고친다.
    /// </summary>
    public class PlayTimeHud : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text bestLabel;

        int shownSeconds = -1;
        float shownBest = -1f;

        void Update()
        {
            var map = MapManager.Current;
            label.enabled = bestLabel.enabled = map != null;
            if (map == null) return;

            int seconds = (int)map.ClearTime;
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                label.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            var record = ProgressStore.Current.FindMap(map.MapId);
            float best = record is { cleared: true } ? record.bestTime : 0f;
            if (best == shownBest) return;
            shownBest = best;
            bestLabel.text = best > 0f ? $"최고 {TimeFormat.ClearTime(best)}" : "최고 --:--";
        }
    }
}
