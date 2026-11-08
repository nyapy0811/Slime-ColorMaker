using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 3인칭 카메라 당기기 수치(2026-11-08, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ThirdPersonCamera"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ColorMaze/카메라 당기기 설정", fileName = "CameraCollisionSettings")]
    public class CameraCollisionSettings : ScriptableObject
    {
        [Tooltip("바라보는 점에서 카메라 쪽으로 쏘는 구의 반지름(유닛). 클수록 벽에서 일찍, 멀리 떨어진다.")]
        [Range(0.01f, 1f)] public float collisionRadius = 0.2f;

        [Tooltip("지형·기물에 걸린 지점에서 므엥 쪽으로 더 당기는 여유(유닛)")]
        [Range(0f, 1f)] public float collisionMargin = 0.1f;

        [Tooltip("최소 거리(유닛). 당겨도 바라보는 점에서 이보다 가까이는 오지 않는다.")]
        [Range(0.1f, 5f)] public float minDistance = 0.5f;

        [Tooltip("걸린 것이 없어졌을 때 원래 거리로 돌아가는 속도(유닛/초). 당길 때는 바로 당긴다.")]
        [Range(0.1f, 30f)] public float returnSpeed = 4f;
    }
}
