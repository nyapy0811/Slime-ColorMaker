using Framework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeColorMaker
{
    /// <summary>
    /// UI 씬을 게임 위에 additive로 얹어 둔다. 맵 씬을 Single로 불러오면 UI 씬도 함께 내려가므로,
    /// 씬이 Single로 불러와질 때마다 UI 씬이 있는지 보고 없으면 다시 얹는다.
    /// SceneLoader를 거치지 않는 '열린 맵에서 바로 시작'도 같은 길을 타도록 Unity의 씬 로드 알림을 듣는다.
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        [Tooltip("additive로 얹을 UI 씬 이름 (Build Settings에 있어야 함)")]
        [SerializeField] string uiSceneName = "UIScene";

        protected override void OnAwake() => EnsureUISceneLoaded();

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) EnsureUISceneLoaded();
        }

        // 불러오는 중인 씬도 유효한 씬으로 잡히므로 IsValid로 보면 두 번 얹지 않는다.
        void EnsureUISceneLoaded()
        {
            if (!SceneManager.GetSceneByName(uiSceneName).IsValid())
                SceneManager.LoadScene(uiSceneName, LoadSceneMode.Additive);
        }
    }
}
