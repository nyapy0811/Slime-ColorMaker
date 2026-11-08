using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 1인칭 이동(v1을 참고해 새로 씀). 몸통은 좌우로, 카메라는 위아래로 돈다.
    /// 지면에서는 가속해 움직이고, 공중에서는 땅을 떠난 순간의 수평 속도를 유지한다(이동키를 떼면 감속).
    /// 게임플레이 입력이 잠기면 이동·시점을 받지 않고(중력은 계속), 일시정지 중에는 멈춘다.
    /// 점프는 없다(2026-11-07): 위 칸으로는 계단으로만 간다(계단의 경사 콜라이더를 걸어 오름).
    /// 피벗은 발밑이다(맵 매니저가 시작 지점 위치로 바로 옮김).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Tooltip("이동 속도(유닛/초)")]
        public float moveSpeed = 3f;

        [Tooltip("마우스 감도. settings.json에 저장된 값이 있으면 그 값을 쓴다.")]
        public float mouseSensitivity = 0.02f;

        [Tooltip("위/아래 시점 제한(도)")]
        public float pitchLimit = 85f;

        [Tooltip("정지 상태에서 최대 이동 속도까지 걸리는 시간(초, 지면 전용)")]
        public float accelerationTime = 0.2f;

        [Tooltip("공중에서 이동키를 뗐을 때 최대 속도에서 0까지 감속되는 시간(초)")]
        public float airDecelerationTime = 0.5f;

        [Tooltip("중력 가속도(유닛/초²). 점프를 없앨 때 그 전의 낙하 느낌(점프 높이 1.05, 체공 0.8초)에서 역산한 값.")]
        public float gravity = 13.125f;

        [Tooltip("눈높이 카메라")]
        public Transform cameraPivot;

        CharacterController cc;
        float pitch;
        float verticalVelocity;
        Vector3 horizontalVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (GameSettings.HasSave()) mouseSensitivity = GameSettings.Current.mouseSensitivity;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>위치와 방향을 바로 옮긴다(시작 지점, 나중에 체크포인트). 속도와 위아래 시점은 초기화한다.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = cc.enabled; // 3인칭 캐릭터 시험 중에는 꺼져 있다(켜지 않고 그대로 둠).
            cc.enabled = false; // 켜진 CharacterController는 transform 직접 이동을 덮어쓴다.
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = wasEnabled;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            bool locked = InputManager.Instance.IsGameplayLocked;
            if (!locked) Look();
            Move(locked);
        }

        void Look()
        {
            Vector2 look = InputManager.Instance.ReadLook() * mouseSensitivity;
            transform.Rotate(Vector3.up, look.x, Space.Self);
            if (cameraPivot != null)
            {
                pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        void Move(bool locked)
        {
            bool grounded = cc.isGrounded;
            Vector2 input = locked ? Vector2.zero : InputManager.Instance.ReadMove();

            if (grounded)
            {
                float accel = moveSpeed / Mathf.Max(accelerationTime, 0.0001f);
                Vector3 move = transform.forward * input.y + transform.right * input.x;
                if (move.sqrMagnitude > 1f) move.Normalize();
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, move * moveSpeed, accel * Time.deltaTime);
            }
            else if (input.sqrMagnitude == 0f)
            {
                float decel = moveSpeed / Mathf.Max(airDecelerationTime, 0.0001f);
                float speed = Mathf.MoveTowards(horizontalVelocity.magnitude, 0f, decel * Time.deltaTime);
                horizontalVelocity = horizontalVelocity.normalized * speed;
            }

            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 땅에 붙어 있게 살짝 누른다.
            verticalVelocity -= gravity * Time.deltaTime;

            Vector3 velocity = horizontalVelocity;
            velocity.y = verticalVelocity;
            cc.Move(velocity * Time.deltaTime);
        }
    }
}
