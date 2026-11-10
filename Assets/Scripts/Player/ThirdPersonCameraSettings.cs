using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 카메라 수치(2026-11-08, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ThirdPersonCamera"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime ~ColorMaker~/3인칭 카메라 설정", fileName = "ThirdPersonCameraSettings")]
    public class ThirdPersonCameraSettings : ScriptableObject
    {
        [Tooltip("바라보는 점의 높이(캐릭터 발밑 기준, 유닛). 0.25면 므엥 몸 가운데")]
        public float lookHeight = 0.25f;

        [Tooltip("처음 거리: 맵을 시작할 때 바라보는 점에서 카메라까지 거리(유닛). 휠로 최소~최대 거리 사이에서 바꾼다. 가리는 것이 있으면 카메라 당기기 설정대로 더 가까이 온다.")]
        [Min(0.5f)] public float distance = 3f;

        [Tooltip("휠로 당길 수 있는 가장 가까운 거리(유닛)")]
        [Min(0.5f)] public float minDistance = 1f;

        [Tooltip("휠로 밀 수 있는 가장 먼 거리(유닛). 최소 거리보다 작으면 최소 거리로 본다.")]
        [Min(0.5f)] public float maxDistance = 8f;

        [Tooltip("변환 속도: 휠 한 칸에 바뀌는 거리(유닛). 휠을 위로 굴리면 가까워진다.")]
        [Range(0.05f, 5f)] public float zoomStep = 0.5f;

        [Tooltip("휠로 바꾼 거리로 다가가는 속도(유닛/초). 클수록 빨리 따라간다.")]
        [Range(0.5f, 50f)] public float zoomSmoothSpeed = 10f;

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
