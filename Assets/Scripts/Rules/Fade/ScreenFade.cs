using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 화면 페이드의 정적 접근점. UIScene의 화면 페이드가 켜질 때 등록하고 꺼질 때 뺀다.
    /// 등록된 페이드가 없으면(UI 씬이 없음) 오류 로그를 남기고 할 일을 바로 한다.
    /// </summary>
    public static class ScreenFade
    {
        static IScreenFader fader;

        public static bool IsFading => fader != null && fader.IsFading;

        public static void Register(IScreenFader screenFader) => fader = screenFader;

        public static void Unregister(IScreenFader screenFader)
        {
            if (fader == screenFader) fader = null;
        }

        public static void FadeThrough(Action atBlack)
        {
            if (Missing()) atBlack();
            else fader.FadeThrough(atBlack);
        }

        public static void FadeOut(Action atBlack)
        {
            if (Missing()) atBlack();
            else fader.FadeOut(atBlack);
        }

        static bool Missing()
        {
            if (fader != null) return false;
            Debug.LogError("[ScreenFade] 화면 페이드가 없다. UI 씬이 얹혀 있는지 확인한다.");
            return true;
        }
    }
}
