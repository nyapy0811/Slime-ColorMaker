using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 벽 노드 시스템의 기준 치수와 프리팹(애셋은 Assets/Data). 치수는 코어 루프 9단계에서 걸어 보며 정했다(3차 Q484).
    /// 임시 회색 프리팹은 1×1×1 상자이고 벽 빌더가 이 치수로 늘린다. 모듈 길이(0.25)는 끼운 문의 위치 단위라
    /// 코어 규칙의 상수(<see cref="NodeGraphRules.ModuleLength"/>)를 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/기준 치수", fileName = "NodeBuildConfig")]
    public class NodeBuildConfig : ScriptableObject
    {
        [Header("기준 치수")]
        public float wallThickness = 0.2f;

        [Tooltip("기둥의 가로·세로. 구간 끝에 남는 길이(한쪽 0.125 미만)를 가릴 만큼 넓어야 한다.")]
        public float pillarSize = 0.3f;

        [Tooltip("벽의 키 = 천장 높이. 천장 판 칸도 정수 좌표가 되게 정수로 둔다.")]
        public int wallHeight = 3;

        [Tooltip("문 폭. 0.25 단위로 맞춘다(문 양옆이 벽 모듈 경계와 맞음).")]
        public float doorWidth = 1f;

        public float doorHeight = 2f;

        [Tooltip("바닥·천장 판의 두께. 판 위쪽 면이 바닥 높이다.")]
        public float plateThickness = 0.1f;

        [Header("프리팹")]
        public GameObject wallModulePrefab;
        public GameObject pillarPrefab;

        [Tooltip("끼운 문. 루트에 DoorPiece가 있어야 한다.")]
        public GameObject doorPrefab;

        [Tooltip("레이저 벽. 루트에 LaserSegment가 있어야 한다.")]
        public GameObject laserPrefab;

        [Tooltip("바닥·천장 판(1×1).")]
        public GameObject floorTilePrefab;
    }
}
