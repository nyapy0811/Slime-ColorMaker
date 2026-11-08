using Framework.Core;

namespace ColorMaze
{
    /// <summary>문·목적지가 지금 색을 받아들이지 않음(요구 색과 다름). 보내는 곳: 문, 목적지. 받는 곳: 소리 연결.</summary>
    public readonly struct ColorRejected : IEvent
    {
    }
}
