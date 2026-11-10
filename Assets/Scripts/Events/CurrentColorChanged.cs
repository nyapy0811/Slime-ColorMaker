using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>
    /// 지금 색이 바뀜. 플레이어가 시작할 때도 한 번 보낸다(처음부터 다시 해도 UIScene은 남아 있음).
    /// 보내는 곳: 플레이어의 지금 색. 받는 곳: 지금 색 표시, 캐릭터 몸 색, 레이저 벽(막을지 다시 봄).
    /// </summary>
    public readonly struct CurrentColorChanged : IEvent
    {
        public readonly ColorValue Value;

        public CurrentColorChanged(ColorValue value) => Value = value;
    }
}
