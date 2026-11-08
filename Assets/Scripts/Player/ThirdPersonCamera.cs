using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 3인칭 카메라(2026-11-08). 마우스로 캐릭터 둘레를 돌며 따라간다(좌우 = 둘레, 위아래 = 높이 각도).
    /// 거리는 <see cref="distance"/>(Play 중에 바꾸면 바로 보임), 처음 각도는 씬에 놓인 자리에서 가져온다(시작 지점으로 옮길 때 맵 매니저보다 먼저 정해지게 Awake에서). 캐릭터의 자식이어도 매 프레임 월드 자리를 직접 정하므로 캐릭터가 돌아도 따라 돌지 않는다.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Tooltip("따라갈 캐릭터")]
        public Transform target;

        [Tooltip("바라보는 점의 높이(캐릭터 발밑 기준, 유닛)")]
        public float lookHeight = 0.5f;

        [Tooltip("바라보는 점에서 카메라까지 거리(유닛)")]
        [Min(0.5f)] public float distance = 5.3f;

        [Tooltip("카메라 당기기 설정(Assets/Data/CameraCollisionSettings). 비우면 당기지 않는다.")]
        [SerializeField] CameraCollisionSettings collision;

        [Tooltip("마우스 감도. settings.json에 저장된 값이 있으면 그 값을 쓴다.")]
        public float mouseSensitivity = 0.02f;

        [Tooltip("위아래 각도 제한(도). 양수가 위에서 내려다봄")]
        public float minPitch = -10f;
        public float maxPitch = 70f;

        float yaw;
        float pitch;
        float currentDistance;
        RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            if (GameSettings.HasSave()) mouseSensitivity = GameSettings.Current.mouseSensitivity;
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minPitch, maxPitch);
            currentDistance = distance;
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
            Vector3 lookPoint = LookPoint();
            Vector3 back = rotation * Vector3.back;
            UpdateDistance(lookPoint, back);
            transform.SetPositionAndRotation(lookPoint + back * currentDistance, rotation);
        }

        // 카메라 당기기(2026-11-08): 바라보는 점에서 카메라 쪽으로 구를 쏴 지형·기물(므엥·트리거 제외)에 걸리면 그 앞까지 바로 당기고,
        // 걸린 것이 없어지면 원래 거리로 서서히 돌아간다.
        void UpdateDistance(Vector3 look, Vector3 back)
        {
            if (collision == null)
            {
                currentDistance = distance;
                return;
            }
            float wanted = Mathf.Max(Mathf.Min(distance, collision.minDistance), BlockedDistance(look, back));
            currentDistance = wanted < currentDistance ? wanted : Mathf.MoveTowards(currentDistance, wanted, collision.returnSpeed * Time.deltaTime);
        }

        float BlockedDistance(Vector3 look, Vector3 back)
        {
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.SphereCastNonAlloc(look, collision.collisionRadius, back, hits, distance, ~0, QueryTriggerInteraction.Ignore)) == hits.Length)
                hits = new RaycastHit[hits.Length * 2];
            float nearest = distance;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(target)) nearest = Mathf.Min(nearest, hits[i].distance - collision.collisionMargin);
            return Mathf.Max(collision.minDistance, nearest);
        }

        Vector3 LookPoint() => target.position + Vector3.up * lookHeight;
    }
}
