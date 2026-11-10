using System;
using System.Collections.Generic;

namespace SlimeColorMaker
{
    /// <summary>
    /// 뒤로 가기 스택(코드 흐름 Q494). 닫을 수 있는 화면(팝업, 일시정지 메뉴, 나중에 확인 창·설정 패널)은 열 때 자기 '닫기'를
    /// 넣고 닫을 때 뺀다. ESC는 맨 위의 닫기를 부르고, 스택이 비어 있을 때만 일시정지 메뉴가 열린다.
    /// </summary>
    public static class BackStack
    {
        static readonly List<Action> closers = new();

        public static int Count => closers.Count;

        /// <summary>닫기를 맨 위에 넣는다. 이미 있으면 맨 위로 옮긴다.</summary>
        public static void Push(Action close)
        {
            closers.Remove(close);
            closers.Add(close);
        }

        public static void Remove(Action close) => closers.Remove(close);

        /// <summary>맨 위의 닫기를 부른다(닫기는 스스로 <see cref="Remove"/>를 부른다). 비어 있으면 false.</summary>
        public static bool TryCloseTop()
        {
            if (closers.Count == 0) return false;
            closers[^1].Invoke();
            return true;
        }
    }
}
