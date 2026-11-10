using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 팝업 호스트의 정적 접근점(코드 흐름 Q493). 팝업 호스트가 켜질 때 등록하고 꺼질 때 뺀다.
    /// 등록된 호스트가 없으면(UI 씬이 없음) 오류 로그로 바로 드러난다.
    /// </summary>
    public static class Popups
    {
        public static IPopupHost Host { get; private set; }

        public static void Register(IPopupHost host) => Host = host;

        public static void Unregister(IPopupHost host)
        {
            if (Host == host) Host = null;
        }

        /// <summary>세션에 맞는 팝업을 연다. 열지 못하면 false.</summary>
        public static bool TryOpen(object session, Action onClosed)
        {
            if (Host == null)
            {
                Debug.LogError("[Popups] 팝업 호스트가 없다. UI 씬이 얹혀 있는지 확인한다.");
                return false;
            }
            return Host.TryOpen(session, onClosed);
        }
    }
}
