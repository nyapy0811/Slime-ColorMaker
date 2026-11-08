using System.Collections.Generic;
using Framework.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColorMaze
{
    /// <summary>
    /// 키보드·마우스 입력을 읽는 곳. 게임플레이 입력 잠금도 여기 있다: 잠근 쪽(팝업 등)이 하나라도 있으면 잠김이고,
    /// 이동·시점과 닿음 상호작용이 이것을 본다. 일시정지는 잠금이 아니라 <c>Time.timeScale = 0</c>으로 멈춘다.
    /// 읽는 키는 쓰는 단계에서 더한다(ESC는 뒤로 가기 스택).
    /// </summary>
    public class InputManager : MonoSingleton<InputManager>
    {
        readonly HashSet<object> gameplayLocks = new();

        public bool IsGameplayLocked => gameplayLocks.Count > 0;

        public void LockGameplay(object owner) => gameplayLocks.Add(owner);
        public void UnlockGameplay(object owner) => gameplayLocks.Remove(owner);

        /// <summary>WASD/방향키. x는 좌우, y는 앞뒤.</summary>
        public Vector2 ReadMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }

        /// <summary>이번 프레임의 마우스 이동량(픽셀).</summary>
        public Vector2 ReadLook()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        }

        /// <summary>뒤로 가기(ESC)를 누른 순간.</summary>
        public bool ReadCancel()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
    }
}
