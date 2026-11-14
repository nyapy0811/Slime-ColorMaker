namespace SlimeColorMaker
{
    /// <summary>
    /// 게임 전역 상태.
    /// </summary>
    public enum GameState
    {
        Boot,       // 초기화 중
        MainMenu,   // 메뉴
        Playing,    // 게임 진행
        Paused,     // 일시정지
        Cleared,    // 맵 클리어
        Quitting    // 종료 전 저장 등 마무리
    }
}
