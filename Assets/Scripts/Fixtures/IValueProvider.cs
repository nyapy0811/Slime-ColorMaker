namespace ColorMaze
{
    /// <summary>
    /// 캐릭터가 상호작용 거리 안에 오면 값 표시에 값을 보이는 기물(흡수 전 색 원천, 닫힌 문, 레이저 벽, 목적지).
    /// 상호작용과 나눈 이유: 레이저 벽은 상호작용하지 않지만 빼는 색을 보인다.
    /// </summary>
    public interface IValueProvider
    {
        /// <summary>지금 보일 값이 있으면 true와 그 값.</summary>
        bool TryGetDisplayValue(out ColorValue value);
    }
}
