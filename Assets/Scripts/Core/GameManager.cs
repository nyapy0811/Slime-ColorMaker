using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// Framework.Core의 범용 GameManagerBase를 이 게임의 GameState로 구체화한 전역 상태 관리자.
    /// 부팅 직후 상태는 Boot이고, 메인 메뉴가 열리면 MainMenu, 맵이 시작되면 맵 매니저가 Playing으로 바꾸고, 클리어하면 Cleared로 바꾼다.
    /// 맵 사이의 전역 진행(클리어 기록 등)은 진행 저장소(<see cref="ProgressStore"/>)가 맡는다.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager, GameState>
    {
        public void StartGame() => ChangeState(GameState.Playing);

        /// <summary>맵 클리어. 게임 중일 때만 바꾼다.</summary>
        public void ClearMap()
        {
            if (State == GameState.Playing) ChangeState(GameState.Cleared);
        }

        /// <summary>게임을 끈다(에디터에서는 Play를 멈춤). 종료 전 저장 같은 마무리는 저장을 만들 때 Quitting에 더한다.</summary>
        public void QuitGame()
        {
            ChangeState(GameState.Quitting);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
