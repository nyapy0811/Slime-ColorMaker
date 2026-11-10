namespace SlimeColorMaker
{
    /// <summary>
    /// 상호작용할 때 닿음 상호작용 컨트롤러가 기물에 넘기는 플레이어 문맥. 기물은 전역 플레이어를 찾지 않고 이것만 쓴다
    /// (코드 흐름 Q500). 닿음 상호작용 컨트롤러가 깨어날 때 만들어 들고 있다.
    /// </summary>
    public sealed class PlayerContext
    {
        readonly PlayerColor color;

        public PlayerContext(PlayerColor color) => this.color = color;

        /// <summary>지금 색. 플레이어의 지금 색이 깨어날 때 만들어지므로 쓸 때 읽는다.</summary>
        public CurrentColor Color => color.Current;
    }
}
