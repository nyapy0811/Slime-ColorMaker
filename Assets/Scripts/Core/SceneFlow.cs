using Framework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeColorMaker
{
    /// <summary>
    /// 씬 사이 이동(진행 시스템 3단계, 1단계의 '처음부터'를 넓힘): 맵 시작(처음부터·이어하기), '처음부터', 메인 메뉴로.
    /// 모두 검은 화면으로 어두워진 뒤 시간을 되돌리고 씬을 불러온다. UI 씬은 UI 관리자가 다시 얹고(새 화면 페이드가 검은 화면에서 밝아짐),
    /// 상태는 맵 매니저(Playing)나 메인 메뉴(MainMenu)가 맞춘다.
    /// 불러온 씬에 넘길 것(이어서 시작할지, 맵 선택 화면을 바로 열지)은 요청으로 남기고 받는 쪽이 한 번 꺼낸다.
    /// 요청 없이 맵이 열리면(에디터의 '열린 맵에서 바로 시작') 처음부터 시작한다(3단계 구상 채팅).
    /// </summary>
    public static class SceneFlow
    {
        const string MainMenuScene = "MainMenu";

        static bool continueRequested;
        static bool mapSelectRequested;

        /// <summary>맵 매니저가 시작할 때 부른다: 체크포인트부터 이어서 시작할지.</summary>
        public static bool TakeContinueRequest()
        {
            bool requested = continueRequested;
            continueRequested = false;
            return requested;
        }

        /// <summary>메인 메뉴가 열릴 때 부른다: 맵 선택 화면을 바로 열지(마지막 맵의 클리어 화면에서 옴).</summary>
        public static bool TakeMapSelectRequest()
        {
            bool requested = mapSelectRequested;
            mapSelectRequested = false;
            return requested;
        }

        /// <summary>맵을 처음부터 시작한다. 필요하면 먼저 확인 창으로 경고하고(2차 Q449, Q456), 그 맵의 체크포인트 기록은 지운다.</summary>
        public static void AskAndStartMap(MapListEntry entry)
        {
            switch (ProgressStore.Current.StartWarning(entry.mapId))
            {
                case MapStartWarning.ClearsThisCheckpoint:
                    Confirm.Ask("이 맵에 저장된 체크포인트가 지워집니다.\n처음부터 시작할까요?", () => StartMap(entry, false));
                    break;
                case MapStartWarning.OverwritesOtherCheckpoint:
                    Confirm.Ask("다른 맵의 체크포인트가 저장되어 있습니다.\n이 맵의 체크포인트에 닿으면 그 기록은 지워집니다.\n시작할까요?", () => StartMap(entry, false));
                    break;
                default:
                    StartMap(entry, false);
                    break;
            }
        }

        /// <summary>맵을 시작한다. 처음부터면 그 맵의 체크포인트 기록을 지우고 바로 저장한다(이어하기가 지운 기록을 읽지 않게).</summary>
        public static void StartMap(MapListEntry entry, bool continueFromCheckpoint)
        {
            if (!continueFromCheckpoint && ProgressStore.Current.ClearCheckpoint(entry.mapId)) ProgressStore.Save();
            FadeAndLoad(entry.sceneName, () => continueRequested = continueFromCheckpoint);
        }

        /// <summary>'처음부터': 그 맵의 체크포인트 기록을 지우고(2차 Q449) 지금 맵 씬을 다시 불러온다(씬 재로드).</summary>
        public static void Restart()
        {
            if (MapManager.Current != null && ProgressStore.Current.ClearCheckpoint(MapManager.Current.MapId)) ProgressStore.Save();
            FadeAndLoad(SceneManager.GetActiveScene().name, () => continueRequested = false);
        }

        /// <summary>메인 메뉴로 간다. 마지막 체크포인트 뒤의 진행은 저장하지 않는다(저장 규칙, 결정 로그 3절).</summary>
        public static void GoToMainMenu(bool openMapSelect = false) =>
            FadeAndLoad(MainMenuScene, () => mapSelectRequested = openMapSelect);

        // 요청은 검은 화면이 된 뒤에 남긴다(페이드 중이라 이동이 무시되면 요청도 남지 않음).
        static void FadeAndLoad(string sceneName, System.Action request) => ScreenFade.FadeOut(() =>
        {
            request();
            Time.timeScale = 1f;
            SceneLoader.Instance.Load(sceneName);
        });
    }
}
