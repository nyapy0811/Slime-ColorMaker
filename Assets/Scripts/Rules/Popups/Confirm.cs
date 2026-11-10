using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 확인 창의 정적 접근점(<see cref="Popups"/>와 같은 방식). UIScene의 확인 창이 켜질 때 등록하고 꺼질 때 뺀다.
    /// 등록된 확인 창이 없으면(UI 씬이 없음) 오류 로그를 남기고 묻지 않은 채 할 일을 한다.
    /// </summary>
    public static class Confirm
    {
        static IConfirmDialog dialog;

        public static void Register(IConfirmDialog confirmDialog) => dialog = confirmDialog;

        public static void Unregister(IConfirmDialog confirmDialog)
        {
            if (dialog == confirmDialog) dialog = null;
        }

        public static void Ask(string message, Action onConfirm)
        {
            if (dialog != null)
            {
                dialog.Ask(message, onConfirm);
                return;
            }
            Debug.LogError("[Confirm] 확인 창이 없다. UI 씬이 얹혀 있는지 확인한다.");
            onConfirm();
        }
    }
}
