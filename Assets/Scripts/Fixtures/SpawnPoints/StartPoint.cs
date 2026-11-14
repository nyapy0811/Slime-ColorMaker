namespace SlimeColorMaker
{
    /// <summary>
    /// 시작 지점. 맵 기본 프리팹(MapRoot)의 자식으로 원점에 있어 모든 맵이 (0,0,0)에서 시작한다. 맵을 시작하면
    /// 플레이어가 여기에 선다. 바라보는 방향은 씬에서 이 오브젝트를 돌려 정한다(프리팹 덮어쓰기).
    /// </summary>
    public class StartPoint : SpawnPointBase
    {
    }
}
