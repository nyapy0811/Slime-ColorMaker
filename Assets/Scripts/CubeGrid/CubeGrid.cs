using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵마다 하나 있는 큐브 격자(v1 큐브 맵 방식, 2026-11-07). 블록 칸 데이터를 들고, 빌더(<see cref="CubeGridBuilder"/>)가
    /// 이 오브젝트 아래에 블록을 쌓는다. 칸 좌표는 월드 좌표라 이 오브젝트는 원점에 두고 돌리거나 늘리지 않는다(맵 검사가 알림).
    /// 만든 블록이 프리팹 오버라이드가 되지 않게 맵 기본 프리팹(MapRoot) 밖에 따로 둔다. 데이터는 메뉴 Slime ~ColorMaker~/큐브 맵 편집에서 고친다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CubeGrid : MonoBehaviour
    {
        [SerializeField] CubeGridData data = new();

        CubeFaceCuller culler;

        public CubeGridData Data => data;

        // Play 중에만(에디터 편집 화면은 모든 면 그대로, 만든 메시가 씬에 저장되지 않게): 맞닿은 면에서 이웃이 덮는 부분을 그리지 않는다.
        void Awake()
        {
            culler = new CubeFaceCuller();
            culler.Apply(this);
        }

        void OnDestroy() => culler?.Dispose();
    }
}
