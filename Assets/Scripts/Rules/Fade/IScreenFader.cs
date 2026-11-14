using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 검은 화면 페이드(UIScene의 화면 페이드). 체크포인트 복귀와 맵 초기화 때 쓴다.
    /// 월드는 UI 클래스를 모르므로 코어 규칙 쪽에 두고 <see cref="ScreenFade"/>로 접근한다.
    /// </summary>
    public interface IScreenFader
    {
        bool IsFading { get; }

        /// <summary>검게 어두워지면 <paramref name="atBlack"/>을 부르고 다시 밝아진다(체크포인트 복귀).</summary>
        void FadeThrough(Action atBlack);

        /// <summary>검게 어두워지면 <paramref name="atBlack"/>을 부르고 검은 채로 둔다(씬을 다시 불러올 때, 새 UI 씬이 검은 화면에서 밝아짐).</summary>
        void FadeOut(Action atBlack);
    }
}
