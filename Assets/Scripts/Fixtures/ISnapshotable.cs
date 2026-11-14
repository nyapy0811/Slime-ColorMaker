namespace SlimeColorMaker
{
    /// <summary>
    /// 체크포인트에 상태를 저장하는 기물. 맵 매니저가 등록부에서 모아 '맵 안 id + 문자열'로 스냅샷에 담고,
    /// 체크포인트로 돌아갈 때 씬을 다시 불러오지 않고 기물마다 되돌린다(부분 초기화).
    /// </summary>
    public interface ISnapshotable
    {
        /// <summary>지금 상태를 JSON 문자열로 내보낸다. 저장할 상태가 없으면 null.</summary>
        string CaptureState();

        /// <summary>내보냈던 상태로 바로 되돌린다(연출 없이). 저장한 상태가 없었으면 null이 온다.</summary>
        void RestoreState(string state);
    }
}
