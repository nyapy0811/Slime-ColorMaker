using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 3인칭 캐릭터 이동 시험(2026-11-08, 사용자: 캐릭터만 따로 시험). 1인칭 플레이어와 별개로 움직이며, 이 캐릭터가 켜져 있는 동안
    /// 1인칭 플레이어의 이동·몸 충돌·카메라(손 뷰모델 포함)를 꺼 둔다(끄면 다시 켬). 1인칭 플레이어의 지금 색은 살려 두어
    /// 이 캐릭터의 닿음 상호작용(<see cref="TouchInteractionController"/>)이 함께 쓴다. 맵 매니저의 시작·낙사·체크포인트와는
    /// 연결하지 않고, 시작 지점에서 시작해 떨어지면 시작 지점으로 돌아오기만 한다. WASD는 카메라가 보는 방향 기준이고 캐릭터는 가는 방향으로 돈다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonTestController : MonoBehaviour
    {
        [Tooltip("이동 속도(유닛/초). 1인칭 플레이어와 같은 값에서 시작")]
        public float moveSpeed = 3f;

        [Tooltip("정지 상태에서 최대 속도까지 걸리는 시간(초, 지면 전용)")]
        public float accelerationTime = 0.2f;

        [Tooltip("가는 방향으로 도는 빠르기(도/초)")]
        public float turnSpeed = 720f;

        [Tooltip("중력 가속도(유닛/초²). 1인칭 플레이어와 같음")]
        public float gravity = 13.125f;

        [Tooltip("이 높이보다 아래로 떨어지면 시작 지점으로 돌아온다")]
        public float fallHeight = -20f;

        [Tooltip("이동 방향의 기준이 되는 카메라")]
        public Transform cameraTransform;

        [Tooltip("이 캐릭터가 켜져 있는 동안 이동·몸 충돌·카메라를 꺼 둘 1인칭 플레이어")]
        public GameObject firstPersonPlayer;

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        Vector3 startPosition;
        Quaternion startRotation;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            // 모델 크기의 상자 콜라이더가 같은 오브젝트에 있다. 이동하는 CharacterController가 그 상자에 걸리지 않게 둘 사이 충돌을 끈다.
            if (TryGetComponent<BoxCollider>(out var box)) Physics.IgnoreCollision(cc, box);
        }

        void OnEnable() => SetFirstPerson(false);

        void OnDisable() => SetFirstPerson(true);

        // 지금 색(PlayerColor)은 켜 둔 채 1인칭의 이동·몸 충돌·카메라만 켜고 끈다.
        void SetFirstPerson(bool on)
        {
            if (firstPersonPlayer == null) return;
            firstPersonPlayer.GetComponent<FirstPersonController>().enabled = on;
            firstPersonPlayer.GetComponent<CharacterController>().enabled = on;
            firstPersonPlayer.GetComponentInChildren<Camera>(true).gameObject.SetActive(on);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            var start = FindAnyObjectByType<StartPoint>();
            startPosition = start != null ? start.transform.position : transform.position;
            startRotation = start != null ? start.SpawnRotation : transform.rotation;
            Teleport(startPosition, startRotation);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            bool locked = InputManager.Instance.IsGameplayLocked;
            Vector2 input = locked ? Vector2.zero : InputManager.Instance.ReadMove();

            // 카메라가 보는 방향을 바닥에 눕힌 것이 앞이다.
            var view = cameraTransform != null ? cameraTransform : transform;
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

            if (transform.position.y < fallHeight) Teleport(startPosition, startRotation);
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            cc.enabled = false; // 켜진 CharacterController는 transform 직접 이동을 덮어쓴다.
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
