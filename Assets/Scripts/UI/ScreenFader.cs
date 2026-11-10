using System;
using System.Collections;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 검은 화면 페이드(UIScene, 2차 Q202). 체크포인트 복귀와 맵 초기화 때 쓴다. 맨 위 캔버스의 검은 판을 투명도로 보이고,
    /// 일시정지 중에도 돌도록 실제 시간으로 잰다. 페이드 중에는 게임플레이 입력을 잠그고 클릭을 막는다.
    /// UI 씬은 맵 씬을 불러올 때마다 다시 얹히므로 검은 화면으로 시작해 밝아진다(맵 초기화의 나머지 절반).
    /// </summary>
    public class ScreenFader : MonoBehaviour, IScreenFader
    {
        [SerializeField] CanvasGroup group;

        [Tooltip("어두워지거나 밝아지는 데 걸리는 시간(초, 실제 시간)")]
        [SerializeField] float duration = 0.3f;

        public bool IsFading { get; private set; }

        void Awake()
        {
            group.alpha = 1f;
            Begin();
        }

        void OnEnable() => ScreenFade.Register(this);

        void OnDisable()
        {
            ScreenFade.Unregister(this);
            if (IsFading && InputManager.Instance != null) InputManager.Instance.UnlockGameplay(this);
        }

        void Start() => StartCoroutine(Fade(0f, End));

        public void FadeThrough(Action atBlack)
        {
            if (IsFading) return;
            Begin();
            StartCoroutine(Fade(1f, () =>
            {
                atBlack();
                StartCoroutine(Fade(0f, End));
            }));
        }

        public void FadeOut(Action atBlack)
        {
            if (IsFading) return;
            Begin();
            StartCoroutine(Fade(1f, atBlack));
        }

        void Begin()
        {
            IsFading = true;
            group.blocksRaycasts = true;
            InputManager.Instance.LockGameplay(this);
        }

        void End()
        {
            IsFading = false;
            group.blocksRaycasts = false;
            InputManager.Instance.UnlockGameplay(this);
        }

        IEnumerator Fade(float target, Action done)
        {
            while (group.alpha != target)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / duration);
                yield return null;
            }
            done();
        }
    }
}
