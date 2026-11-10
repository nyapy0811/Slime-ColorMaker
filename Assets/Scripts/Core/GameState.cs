namespace SlimeColorMaker
{
    /// <summary>
    /// 게임 전역 상태(v1 구조를 유지, 2차 Q287). 팝업은 상태가 아니다.
    /// v1의 맵 에디터 상태는 맵 에디터를 새로 만들 때 다시 더한다.
    /// </summary>
    public enum GameState
    {
        Boot,       // 초기화 중
        MainMenu,   // 메뉴
        Loading,    // 씬 전환 대기
        Playing,    // 게임 진행
        Paused,     // 일시정지
        Cleared,    // 맵 클리어
        Quitting    // 종료 전 저장 등 마무리
    }
}
