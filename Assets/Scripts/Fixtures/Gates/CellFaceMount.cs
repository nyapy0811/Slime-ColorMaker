using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>칸의 면 여섯(2026-11-11): 북 +Z, 동 +X, 남 −Z, 서 −X, 위 +Y(칸 윗면), 아래 −Y(칸 바닥면).</summary>
    public enum CellFace { North, East, South, West, Up, Down }

    /// <summary>
    /// 칸의 한 면에 붙는 기물의 자리(2026-11-11, 방벽). 피벗(칸 가운데)을 돌려, 서쪽(−X) 면에 만든 모델과 막는 상자를 고른 면으로 보낸다.
    /// 기물 루트는 칸 바닥 가운데에 돌리지 않고 두고, 면은 이 값으로 정한다(큐브 맵 편집의 방벽 붓이 정함).
    /// </summary>
    public class CellFaceMount : MonoBehaviour
    {
        [Tooltip("칸 가운데에 있는 피벗. 모델과 막는 상자가 이 아래에 서쪽(−X) 면 기준으로 있다.")]
        [SerializeField] Transform pivot;

        [Tooltip("붙을 면: 북 +Z, 동 +X, 남 −Z, 서 −X, 위(칸 윗면), 아래(칸 바닥면)")]
        [SerializeField] CellFace face = CellFace.West;

        public Transform Pivot => pivot;

        public CellFace Face
        {
            get => face;
            set
            {
                face = value;
                Apply();
            }
        }

        void Awake() => Apply();

        void OnValidate() => Apply();

        void Apply()
        {
            if (pivot != null) pivot.localRotation = Rotation(face);
        }

        /// <summary>서쪽(−X) 면을 그 면으로 보내는 회전.</summary>
        public static Quaternion Rotation(CellFace face) => face switch
        {
            CellFace.North => Quaternion.Euler(0f, 90f, 0f),
            CellFace.East => Quaternion.Euler(0f, 180f, 0f),
            CellFace.South => Quaternion.Euler(0f, -90f, 0f),
            CellFace.Up => Quaternion.Euler(0f, 0f, -90f),
            CellFace.Down => Quaternion.Euler(0f, 0f, 90f),
            _ => Quaternion.identity,
        };
    }
}
