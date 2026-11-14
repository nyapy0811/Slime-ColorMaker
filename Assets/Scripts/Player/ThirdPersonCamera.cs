using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 카메라(2026-11-08). 마우스로 캐릭터 둘레를 돌며 따라간다(좌우 = 둘레, 위아래 = 높이 각도).
    /// 높이·거리(처음 거리, 휠로 최소~최대 사이에서 바꿈, 2026-11-09)·시야각·각도 제한·감도는 설정 애셋(<see cref="ThirdPersonCameraSettings"/>, Play 중에 바꾸면 바로 보임, 감도만 시작할 때 한 번), 처음 각도는 씬에 놓인 자리에서 가져온다(시작 지점으로 옮길 때 맵 매니저보다 먼저 정해지게 Awake에서). 캐릭터의 자식이어도 매 프레임 월드 자리를 직접 정하므로 캐릭터가 돌아도 따라 돌지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Tooltip("따라갈 캐릭터")]
        public Transform target;

        [Tooltip("3인칭 카메라 설정(Assets/Data/Camera/ThirdPersonCameraSettings)")]
        [SerializeField] ThirdPersonCameraSettings settings;

        [Tooltip("카메라 당기기 설정(Assets/Data/Camera/CameraCollisionSettings). 비우면 당기지 않는다.")]
        [SerializeField] CameraCollisionSettings collision;

        Camera view;
        float mouseSensitivity;
        float yaw;
        float pitch;
        float currentDistance;
        float zoomTarget;
        float zoomDistance;
        RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            view = GetComponent<Camera>();
            mouseSensitivity = GameSettings.HasSave() ? GameSettings.Current.mouseSensitivity : settings.mouseSensitivity;
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), settings.minPitch, settings.maxPitch);
            zoomTarget = zoomDistance = currentDistance = ClampZoom(settings.distance);
        }

        /// <summary>이 방향을 뒤에서 보게 좌우 각도를 맞춘다(위아래 각도는 그대로).</summary>
        public void LookAlong(Quaternion rotation) => yaw = rotation.eulerAngles.y;

        /// <summary>카메라의 좌우 각도만 담은 방향. 이동의 앞은 이것으로 정한다(보는 방향을 바닥에 눕히면 꼭대기에서 볼 때 0이 됨).</summary>
        public Quaternion Heading => Quaternion.Euler(0f, yaw, 0f);

        void LateUpdate()
        {
            if (target == null) return;
            if (Time.timeScale > 0f && !InputManager.Instance.IsGameplayLocked)
            {
                Vector2 look = InputManager.Instance.ReadLook() * mouseSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, settings.minPitch, settings.maxPitch);
                zoomTarget -= InputManager.Instance.ReadZoom() * settings.zoomStep;
            }
            // 휠 거리(2026-11-09): 목표는 최소~최대 거리 안, 실제 거리는 목표로 부드럽게 다가간다.
            float previousZoom = zoomDistance;
            zoomTarget = ClampZoom(zoomTarget);
            zoomDistance = Mathf.MoveTowards(zoomDistance, zoomTarget, settings.zoomSmoothSpeed * Time.deltaTime);
            view.fieldOfView = settings.fieldOfView;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 lookPoint = LookPoint();
            Vector3 back = rotation * Vector3.back;
            UpdateDistance(lookPoint, back, previousZoom);
            transform.SetPositionAndRotation(lookPoint + back * currentDistance, rotation);
        }

        // 카메라 당기기(2026-11-08): 바라보는 점에서 카메라 쪽으로 구를 쏴 지형·기물(므엥·트리거 제외)에 걸리면 그 앞까지 바로 당기고,
        // 걸린 것이 없어지면 원래 거리로 서서히 돌아간다. 원래 거리는 휠 거리다. 당겨져 있지 않던 카메라는 휠 거리를 그대로 따라간다
        // (휠로 멀어질 때 되돌아가는 속도에 묶이지 않게).
        void UpdateDistance(Vector3 look, Vector3 back, float previousZoom)
        {
            float distance = zoomDistance;
            if (collision == null)
            {
                currentDistance = distance;
                return;
            }
            float wanted = Mathf.Max(Mathf.Min(distance, collision.minDistance), BlockedDistance(look, back, distance));
            bool wasFree = currentDistance >= previousZoom - 0.001f;
            currentDistance = wanted < currentDistance || wasFree ? wanted : Mathf.MoveTowards(currentDistance, wanted, collision.returnSpeed * Time.deltaTime);
        }

        float BlockedDistance(Vector3 look, Vector3 back, float distance)
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

        float ClampZoom(float value) => Mathf.Clamp(value, settings.minDistance, Mathf.Max(settings.minDistance, settings.maxDistance));

        // 턱을 오르내린 직후에는 몸 모델처럼 바뀐 높이를 부드럽게 따라간다(2026-11-13).
        Vector3 LookPoint() => target.position + Vector3.up * (settings.lookHeight + (target.TryGetComponent<ThirdPersonController>(out var mover) ? mover.VisualOffset : 0f));
    }
}
