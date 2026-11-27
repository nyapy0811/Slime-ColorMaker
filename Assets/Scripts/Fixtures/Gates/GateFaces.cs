using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>칸 면 여럿(비트 묶음). 차례는 <see cref="CellFace"/>와 같다.</summary>
    [Flags]
    public enum CellFaces
    {
        None = 0,
        North = 1 << (int)CellFace.North,
        East = 1 << (int)CellFace.East,
        South = 1 << (int)CellFace.South,
        West = 1 << (int)CellFace.West,
        Up = 1 << (int)CellFace.Up,
        Down = 1 << (int)CellFace.Down,
    }

    /// <summary>
    /// 게이트(한 칸 큐브 문)의 면 구성. 여섯 면 중 열리는 면(놓을 때 정함, 여럿 가능)은 열리는 판(모델 Gate, 막는 판은 기물 레이어라 닿으면 문이 받음),
    /// 나머지는 안 열리는 판(모델 GateWall, 막는 판은 지형 레이어라 닿아도 아무 일 없음)을 켠다. 모서리 띠(GateSide)·꼭짓점(GateEdge)은 늘 켜져 있다.
    /// 면은 칸 가운데 피벗 아래에 바닥 면 기준으로 만든 것을 면마다 돌려 둔다(<see cref="Rotation"/>). 문(<see cref="Door"/>)이 열 때 끄는 막는 콜라이더는 열리는 면의 판뿐이다.
    /// </summary>
    public class GateFaces : MonoBehaviour
    {
        [Tooltip("열리는 면(여럿 가능): 북 +Z, 동 +X, 남 −Z, 서 −X, 위(칸 윗면), 아래(칸 바닥면). 나머지 면은 안 열리는 판이다.")]
        [SerializeField, PerPlacement] CellFaces open = CellFaces.North | CellFaces.South;

        [Tooltip("면마다 열리는 판 모델(Gate), 차례는 북·동·남·서·위·아래")]
        [SerializeField] GameObject[] openModels = new GameObject[6];

        [Tooltip("면마다 안 열리는 판 모델(GateWall), 차례는 북·동·남·서·위·아래")]
        [SerializeField] GameObject[] wallModels = new GameObject[6];

        [Tooltip("면마다 열리는 판의 막는 상자(기물 레이어, 문이 열 때 끔), 차례는 북·동·남·서·위·아래")]
        [SerializeField] GameObject[] openBlockers = new GameObject[6];

        [Tooltip("면마다 안 열리는 판의 막는 상자(지형 레이어, 늘 막음), 차례는 북·동·남·서·위·아래")]
        [SerializeField] GameObject[] wallBlockers = new GameObject[6];

        /// <summary>열리는 면.</summary>
        public CellFaces Open
        {
            get => open;
            set
            {
                open = value;
                Apply();
            }
        }

        /// <summary>면이 열리는 면인가.</summary>
        public bool IsOpen(CellFace face) => (open & (CellFaces)(1 << (int)face)) != 0;

        /// <summary>면의 열리는 판 모델.</summary>
        public GameObject OpenModel(CellFace face) => openModels[(int)face];

        /// <summary>안 열리는 판의 막는 상자인가(지형 레이어라 기물 레이어 검사에서 뺌).</summary>
        public bool IsWallBlocker(GameObject target) => System.Array.IndexOf(wallBlockers, target) >= 0;

        void Awake() => Apply();

#if UNITY_EDITOR
        // 켜고 끄기는 OnValidate 안에서 할 수 없어 바로 뒤로 미룬다.
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Apply();
        };
#endif

        /// <summary>면마다 열리는 판 또는 안 열리는 판(모델과 막는 상자)을 켠다.</summary>
        public void Apply()
        {
            for (int i = 0; i < 6; i++)
            {
                bool isOpen = IsOpen((CellFace)i);
                SetActive(openModels[i], isOpen);
                SetActive(openBlockers[i], isOpen);
                SetActive(wallModels[i], !isOpen);
                SetActive(wallBlockers[i], !isOpen);
            }
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>바닥 면(−Y)을 그 면으로 보내는 회전(칸 가운데 기준).</summary>
        public static Quaternion Rotation(CellFace face) => face switch
        {
            CellFace.North => Quaternion.Euler(-90f, 0f, 0f),
            CellFace.East => Quaternion.Euler(0f, 0f, 90f),
            CellFace.South => Quaternion.Euler(90f, 0f, 0f),
            CellFace.West => Quaternion.Euler(0f, 0f, -90f),
            CellFace.Up => Quaternion.Euler(180f, 0f, 0f),
            _ => Quaternion.identity,
        };
    }
}
