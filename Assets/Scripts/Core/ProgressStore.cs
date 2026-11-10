using System;
using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>
    /// v2 진행 저장(colormaze_save.json, 결정 로그 3절): 맵별 기록과 마지막 체크포인트. 설정(<see cref="GameSettings"/>)과 같은 방식으로
    /// SaveManager의 범용 JSON API 위에서 동작하고, GameManager는 게임 상태만 맡는다(진행 시스템 2단계 구상 채팅 ← 2차 Q293).
    /// 처음 읽을 때 v2 저장 파일이 없으면 v1 세이브를 알리지 않고 지운다(settings.json은 남김, 2차 Q452).
    /// 기록을 바꾼 곳은 바로 <see cref="Save"/>를 부른다(지운 기록으로 이어하지 않게).
    /// </summary>
    public static class ProgressStore
    {
        const string FileName = "colormaze_save.json";
        static readonly string[] V1Files = { "save.json", "stage_attempts.json" };

        static ProgressSave current;

        public static ProgressSave Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        public static void Save()
        {
            Current.savedAt = DateTime.UtcNow.ToString("o");
            SaveManager.Instance.SaveJson(FileName, Current);
        }

        static void Load()
        {
            var save = SaveManager.Instance;
            if (!save.HasJson(FileName))
                foreach (var file in V1Files) save.DeleteJson(file);
            current = save.LoadJson<ProgressSave>(FileName);
        }
    }
}
