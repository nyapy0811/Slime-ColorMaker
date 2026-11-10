using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 프리팹(애셋은 Assets/Data). 블록은 아직 한 종류이고, 블록과 계단의 겉모습은 임시 복셀 모델이다.
    /// 기물 프리팹은 큐브 맵 편집 도구의 팔레트가 쓴다(지금은 체크포인트·색 원천·목적지·차단기, 2026-11-08 체크포인트·차단기 더함, 시작 지점은 MapRoot 안으로 옮겨 뺌).
    /// </summary>
    [CreateAssetMenu(menuName = "Slime ~ColorMaker~/큐브 맵 설정", fileName = "CubeGridConfig")]
    public class CubeGridConfig : ScriptableObject
    {
        [Tooltip("블록(1×1×1, 피벗은 가운데, 콜라이더 포함, static)")]
        public GameObject blockPrefab;

        [Tooltip("계단(한 칸 안에서 한 칸 높이를 오름, 피벗은 칸 가운데, +z로 오름, 밟는 면은 45° 경사 콜라이더, static)")]
        public GameObject stairPrefab;

        [Tooltip("맵 기본 프리팹(MapRoot, 시작 지점 포함). 메뉴 Slime ~ColorMaker~/새 맵 만들기가 새 씬에 넣는다.")]
        public GameObject mapRootPrefab;

        [Header("팔레트 기물 (피벗은 바닥 가운데)")]
        public GameObject checkpointPrefab;
        public GameObject colorSourcePrefab;
        public GameObject goalPrefab;

        [Tooltip("차단기(레이저 벽 역할, 칸 뒤쪽 가장자리에 섬, 앞(+z)으로 지나감)")]
        public GameObject boomGatePrefab;
    }
}
