using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 3인칭 카메라(2026-11-08). 마우스로 캐릭터 둘레를 돌며 따라간다(좌우 = 둘레, 위아래 = 높이 각도).
    /// 처음 거리와 각도는 씬에 놓인 자리에서 가져온다(시작 지점으로 옮길 때 맵 매니저보다 먼저 정해지게 Awake에서). 캐릭터의 자식이어도 매 프레임 월드 자리를 직접 정하므로 캐릭터가 돌아도 따라 돌지 않는다.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Tooltip("따라갈 캐릭터")]
        public Transform target;

        [Tooltip("바라보는 점의 높이(캐릭터 발밑 기준, 유닛)")]
        public float lookHeight = 0.5f;

        [Tooltip("마우스 감도. settings.json에 저장된 값이 있으면 그 값을 쓴다.")]
        public float mouseSensitivity = 0.02f;

        [Tooltip("위아래 각도 제한(도). 양수가 위에서 내려다봄")]
        public float minPitch = -10f;
        public float maxPitch = 70f;

        float yaw;
        float pitch;
        float distance;

        void Awake()
        {
            if (GameSettings.HasSave()) mouseSensitivity = GameSettings.Current.mouseSensitivity;
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minPitch, maxPitch);
            distance = target != null ? Vector3.Distance(transform.position, LookPoint()) : 5f;
        }

        /// <summary>이 방향을 뒤에서 보게 좌우 각도를 맞춘다(위아래 각도는 그대로).</summary>
        public void LookAlong(Quaternion rotation) => yaw = rotation.eulerAngles.y;

        void LateUpdate()
        {
            if (target == null) return;
            if (Time.timeScale > 0f && !InputManager.Instance.IsGameplayLocked)
            {
                Vector2 look = InputManager.Instance.ReadLook() * mouseSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            }
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(LookPoint() - rotation * Vector3.forward * distance, rotation);
        }

        Vector3 LookPoint() => target.position + Vector3.up * lookHeight;
    }
}
