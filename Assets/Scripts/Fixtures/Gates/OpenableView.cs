using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 문(<see cref="Door"/>)의 겉모습 기반: 열림을 받는다. 칸 기물 게이트(<see cref="GateView"/>)가 상속한다.
    /// </summary>
    public abstract class OpenableView : MonoBehaviour
    {
        /// <summary>연출하며 열기를 끝내 다 열렸다(문이 이때 막음을 끈다). 연출 없이 바로 열 때는 보내지 않는다.</summary>
        public event Action FullyOpened;

        protected void NotifyFullyOpened() => FullyOpened?.Invoke();

        /// <summary>연출하며 열리거나 닫힌다.</summary>
        public abstract void SetOpen(bool open);

        /// <summary>연출 없이 바로 열리거나 닫힌 모양이 된다(체크포인트 복귀).</summary>
        public abstract void SetOpenImmediate(bool open);
    }
}
