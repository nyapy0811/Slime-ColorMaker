using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>체크포인트에 처음 닿음. 보내는 곳: 맵 매니저. 받는 곳: 소리 연결.</summary>
    public readonly struct CheckpointReached : IEvent
    {
    }
}
