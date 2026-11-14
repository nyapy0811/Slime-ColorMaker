using System;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 게임의 단일 진입점. 부팅 씬(Boot)에 두면 전역 서비스를 정해진 순서로 깨운 뒤 다음 씬을 불러온다.
    /// 다음 씬은 메인 메뉴다.
    /// 에디터에서 '열린 맵에서 바로 시작'이 켜져 있으면 열려 있던 씬을 대신 불러온다.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // 다른 스크립트가 Awake에서 싱글톤을 찾기 전에 먼저 깨운다.
    public class Bootstrap : MonoBehaviour
    {
        [Tooltip("부팅 뒤 불러올 씬 이름 (Build Settings에 있어야 함)")]
        [SerializeField] string nextSceneName = "MainMenu";

        void Awake()
        {
            // .Instance에 닿으면 각 싱글톤이 생긴다. 하나가 실패해도 나머지는 깨우도록 따로 감싼다.
            Init(() => _ = GameManager.Instance);
            Init(() => _ = SaveManager.Instance);
            Init(() => _ = AudioManager.Instance);
            Init(() => _ = GameAudio.Instance);
            Init(() => _ = SceneLoader.Instance);
            Init(() => _ = InputManager.Instance);
            Init(() => _ = UIManager.Instance); // UI 씬을 additive로 얹는다.
        }

        // 부팅 씬의 다른 오브젝트(GameAudio 등)가 Awake를 마친 뒤에 넘어간다.
        void Start()
        {
            if (DirectPlayLoader.TryLoadOpenedScene()) return;
            SceneLoader.Instance.Load(nextSceneName);
        }

        static void Init(Action init)
        {
            try { init(); }
            catch (Exception ex) { Debug.LogError($"[Bootstrap] 서비스 초기화 실패: {ex}"); }
        }
    }
}
