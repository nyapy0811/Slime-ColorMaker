using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 레이저 벽(<see cref="LaserWall"/>)의 겉모습 기반(2026-11-08): 지나갈 수 있는지가 바뀌면 받는다.
    /// 노드 레이저 면(<see cref="LaserWallView"/>)과 칸 기물 차단기(<see cref="BoomGateView"/>)가 상속한다.
    /// </summary>
    public abstract class PassableView : MonoBehaviour
    {
        public abstract void SetPassable(bool passable);
    }
}
