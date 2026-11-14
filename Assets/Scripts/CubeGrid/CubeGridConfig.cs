using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 프리팹(애셋은 Assets/Data/Map). 블록은 아직 한 종류이고, 블록과 계단의 겉모습은 임시 복셀 모델이다.
    /// 기물 프리팹은 큐브 맵 편집 도구의 팔레트가 쓴다(시작 지점은 MapRoot 안에 있어 팔레트에 없음).
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/큐브 맵 설정", fileName = "CubeGridConfig")]
    public class CubeGridConfig : ScriptableObject
    {
        [Tooltip("블록(1×1×1, 피벗은 가운데, 콜라이더 포함, static)")]
        public GameObject blockPrefab;

        [Tooltip("계단(한 칸 안에서 한 칸 높이를 오름, 피벗은 칸 가운데, +z로 오름, 밟는 면은 45° 경사 콜라이더, static)")]
        public GameObject stairPrefab;

        [Tooltip("맵 기본 프리팹(MapRoot, 시작 지점 포함). 메뉴 Slime~ColorMaker~/새 맵 만들기가 새 씬에 넣는다.")]
        public GameObject mapRootPrefab;

        [Header("팔레트 기물 (피벗은 바닥 가운데)")]
        public GameObject checkpointPrefab;
        public GameObject colorSourcePrefab;
        public GameObject goalPrefab;

        [Tooltip("차단기(레이저 벽 역할, 칸 뒤쪽 가장자리에 섬, 앞(+z)으로 지나감)")]
        public GameObject boomGatePrefab;

        [Tooltip("방벽(문 역할, 칸의 한 면을 덮는 반투명 판, 면은 CellFaceMount로 고름)")]
        public GameObject barrierPrefab;

        [Tooltip("컨베이어 벨트(이동 계열, 블록 자리에 깔림, 모델 기준 −z로 흐름). 한 프리팹이 직진·우회전·좌회전 무늬를 모두 갖고, 놓을 때 모양을 정한다.")]
        public GameObject conveyorBeltPrefab;

        [Header("장식 기둥 (피벗은 칸 가운데, 콜라이더는 모델 형태, static)")]
        [Tooltip("위아래: 받침 + 몸통 + 머리(머리·받침 설정의 한 칸 그룹)")]
        public GameObject pillarPrefab;

        [Tooltip("아래: 받침 + 몸통(받침 설정 그룹의 맨 아래 칸)")]
        public GameObject pillarBasePrefab;

        [Tooltip("중간: 몸통만(그룹의 가운데 칸, 또는 머리·받침이 없는 설정)")]
        public GameObject pillarShaftPrefab;

        [Tooltip("위: 몸통 + 머리(머리 설정 그룹의 맨 위 칸)")]
        public GameObject pillarTopPrefab;

        [Header("블록 윗면 코팅")]
        [Tooltip("미끄러운 코팅 설정(기름·얼음의 관성 시간과 재질). 빌더가 '만든 부분/코팅'의 겉모습에 넘긴다.")]
        public SlipperyCoatingSettings coatingSettings;
    }
}
