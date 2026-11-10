using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 팝업을 여는 곳(UIScene의 팝업 호스트). 기물은 세션과 '닫힐 때 할 일'만 넘기고 UI 클래스를 모른다.
    /// 세션만 주고받으므로 코어 규칙 쪽에 둔다(코드 흐름 Q493). 접근은 <see cref="Popups"/>로 한다.
    /// </summary>
    public interface IPopupHost
    {
        bool IsOpen { get; }

        /// <summary>세션에 맞는 팝업을 연다. 이미 열려 있거나 그릴 화면이 없으면 열지 않고 false를 돌려준다.</summary>
        bool TryOpen(object session, Action onClosed);

        /// <summary>열린 팝업을 닫고 '닫힐 때 할 일'을 부른다.</summary>
        void Close();
    }
}
