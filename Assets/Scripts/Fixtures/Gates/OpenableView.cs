using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 문(<see cref="Door"/>)의 겉모습 기반(2026-11-11): 열림을 받는다. 노드 끼운 문(<see cref="DoorView"/>)과 칸 기물 방벽(<see cref="BarrierView"/>)이 상속한다.
    /// </summary>
    public abstract class OpenableView : MonoBehaviour
    {
        /// <summary>연출하며 열리거나 닫힌다.</summary>
        public abstract void SetOpen(bool open);

        /// <summary>연출 없이 바로 열리거나 닫힌 모양이 된다(체크포인트 복귀).</summary>
        public abstract void SetOpenImmediate(bool open);
    }
}
