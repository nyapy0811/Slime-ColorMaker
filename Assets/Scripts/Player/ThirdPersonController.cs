using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 플레이어 이동(2026-11-08, 1인칭을 없애고 정식 플레이어가 됨). WASD는 카메라가 보는 방향 기준이고 캐릭터는 가는 방향으로 돈다.
    /// 지면에서는 가속해 움직이고, 공중에서는 수평 속도를 유지한다. 게임플레이 입력이 잠기면 이동을 받지 않고(중력은 계속), 일시정지 중에는 멈춘다.
    /// 점프는 없다(2026-11-07): 위 칸으로는 계단으로만 간다. 피벗은 발밑이다. 시작 지점·체크포인트로 옮기기와 낙사는 맵 매니저가 맡는다.
    /// 같은 오브젝트의 상자 콜라이더(닿음 상호작용용)와 CharacterController는 서로 부딪히지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Tooltip("이동 속도(유닛/초)")]
        public float moveSpeed = 3f;

        [Tooltip("정지 상태에서 최대 속도까지 걸리는 시간(초, 지면 전용)")]
        public float accelerationTime = 0.2f;

        [Tooltip("가는 방향으로 도는 빠르기(도/초)")]
        public float turnSpeed = 720f;

        [Tooltip("중력 가속도(유닛/초²). 점프를 없앨 때 그 전의 낙하 느낌(점프 높이 1.05, 체공 0.8초)에서 역산한 값.")]
        public float gravity = 13.125f;

        [Tooltip("이동 방향의 기준이 되는 카메라")]
        public ThirdPersonCamera playerCamera;

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            // 모델 크기의 상자 콜라이더가 같은 오브젝트에 있다. 이동하는 CharacterController가 그 상자에 걸리지 않게 둘 사이 충돌을 끈다.
            if (TryGetComponent<BoxCollider>(out var box)) Physics.IgnoreCollision(cc, box);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>위치와 방향을 바로 옮긴다(시작 지점, 체크포인트). 속도는 초기화하고, 카메라는 그 방향 뒤에서 보게 돌린다(2차 Q450).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            cc.enabled = false; // 켜진 CharacterController는 transform 직접 이동을 덮어쓴다.
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            if (playerCamera != null) playerCamera.LookAlong(rotation);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            bool locked = InputManager.Instance.IsGameplayLocked;
            Vector2 input = locked ? Vector2.zero : InputManager.Instance.ReadMove();

            // 카메라가 보는 방향을 바닥에 눕힌 것이 앞이다.
            var view = playerCamera != null ? playerCamera.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (cc.isGrounded)
            {
                float accel = moveSpeed / Mathf.Max(accelerationTime, 0.0001f);
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, move * moveSpeed, accel * Time.deltaTime);
                if (verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 땅에 붙어 있게 살짝 누른다.
            }
            if (move.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(move), turnSpeed * Time.deltaTime);

            verticalVelocity -= gravity * Time.deltaTime;
            Vector3 velocity = horizontalVelocity;
            velocity.y = verticalVelocity;
            cc.Move(velocity * Time.deltaTime);
        }
    }
}
