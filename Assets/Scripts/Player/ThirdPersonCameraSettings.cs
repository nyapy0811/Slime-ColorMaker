using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 3인칭 카메라 수치(2026-11-08, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ThirdPersonCamera"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ColorMaze/3인칭 카메라 설정", fileName = "ThirdPersonCameraSettings")]
    public class ThirdPersonCameraSettings : ScriptableObject
    {
        [Tooltip("바라보는 점의 높이(캐릭터 발밑 기준, 유닛)")]
        public float lookHeight = 0.5f;

        [Tooltip("바라보는 점에서 카메라까지 거리(유닛). 가리는 것이 있으면 카메라 당기기 설정대로 더 가까이 온다.")]
        [Min(0.5f)] public float distance = 7f;

        [Tooltip("시야각(도, 세로 기준). 클수록 넓게 보인다.")]
        [Range(30f, 120f)] public float fieldOfView = 90f;

        [Tooltip("마우스 감도. settings.json에 저장된 값이 있으면 그 값을 쓴다(맵을 시작할 때 한 번 정함).")]
        public float mouseSensitivity = 0.02f;

        [Tooltip("위아래 각도 아래 한도(도). 양수가 위에서 내려다봄")]
        public float minPitch = 10f;

        [Tooltip("위아래 각도 위 한도(도). 90이면 바로 위에서 내려다봄")]
        public float maxPitch = 90f;
    }
}
