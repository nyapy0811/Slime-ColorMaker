using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>레이저 벽을 다 지나 빼는 색만큼 지금 색에서 뺌. 보내는 곳: 레이저 벽. 받는 곳: 소리 연결.</summary>
    public readonly struct LaserPassed : IEvent
    {
    }
}
