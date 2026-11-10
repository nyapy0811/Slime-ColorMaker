#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 에디터의 Play를 늘 부팅 씬(Boot)부터 시작하게 하고, 메뉴 'Slime ~ColorMaker~/Play: 열린 맵에서 바로 시작'이 켜져 있으면
    /// 부팅 뒤 Play를 누를 때 열려 있던 씬을 바로 불러온다(Build Settings에 없어도 경로로 불러옴).
    /// 부팅 순서를 그대로 쓰므로 부팅 씬에만 있는 설정이 빠지지 않는다. 빌드에서는 아무 일도 하지 않는다.
    /// </summary>
    public static class DirectPlayLoader
    {
        /// <summary>바로 시작할 씬이 있으면 불러오고 true를 돌려준다. Bootstrap이 부른다.</summary>
        public static bool TryLoadOpenedScene()
        {
#if UNITY_EDITOR
            string path = SessionState.GetString(OpenedSceneKey, "");
            SessionState.EraseString(OpenedSceneKey);
            if (string.IsNullOrEmpty(path)) return false;
            EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string MenuPath = "Slime ~ColorMaker~/Play: 열린 맵에서 바로 시작";
        const string EnabledKey = "SlimeColorMaker.DirectPlay.Enabled";
        const string OpenedSceneKey = "SlimeColorMaker.DirectPlay.OpenedScene";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [InitializeOnLoadMethod]
        static void Setup()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // Play를 누른 순간 열려 있던 씬을 기억한다. 바로 시작은 디스크의 씬을 불러오므로 고친 내용은 먼저 저장하게 한다.
        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            string opened = SceneManager.GetActiveScene().path;
            bool use = Enabled && !string.IsNullOrEmpty(opened) && opened != BootScenePath;
            if (use && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetString(OpenedSceneKey, use ? opened : "");
        }

        [MenuItem(MenuPath)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
#endif
    }
}
