using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>문이 열림(한 번 열리면 계속 열려 있음). 보내는 곳: 문. 받는 곳: 소리 연결.</summary>
    public readonly struct DoorOpened : IEvent
    {
    }
}
