using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 확인 창(UIScene). 진행이 사라지는 버튼과 맵 시작 경고에 쓴다.
    /// 코어 규칙 쪽에 두고 <see cref="Confirm"/>으로 접근한다.
    /// </summary>
    public interface IConfirmDialog
    {
        /// <summary>메시지를 보이고, '확인'을 누르면 <paramref name="onConfirm"/>을 부른다. '취소'·ESC는 그냥 닫는다.</summary>
        void Ask(string message, Action onConfirm);
    }
}
