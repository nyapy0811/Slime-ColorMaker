namespace SlimeColorMaker
{
    /// <summary>클리어 시간 글자(mm:ss.ss). 클리어 화면, 맵 선택 화면, HUD의 최고 시간이 같은 자리수를 쓴다.</summary>
    public static class TimeFormat
    {
        public static string ClearTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return $"{minutes:00}:{seconds - minutes * 60f:00.00}";
        }
    }
}
