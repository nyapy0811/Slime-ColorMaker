using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 플레이어 이동(2026-11-08, 1인칭을 없애고 정식 플레이어가 됨). WASD는 카메라가 보는 방향 기준이고 캐릭터는 가는 방향으로 돈다.
    /// 지면에서는 관성 시간 동안 일정하게 빨라져 출발하고, 키를 떼면 그때 속도에서 관성 시간 동안 일정하게 느려져 멈춘다(2026-11-14). 공중에서는 수평 속도를 유지한다. 게임플레이 입력이 잠기면 이동을 받지 않고(중력은 계속), 일시정지 중에는 멈춘다.
    /// 점프는 없다(2026-11-07): 위 칸으로는 계단으로만 간다. 피벗은 발밑이다. 시작 지점·체크포인트로 옮기기와 낙사는 맵 매니저가 맡는다.
    /// 컨베이어 벨트 위에 서 있으면 벨트가 흐르는 속도를 이동에 그대로 더한다(2026-11-12, 가속 없음, 내려오면 바로 멈춤).
    /// 중심점(발밑)이 벨트 구역(난간 빼고) 안에 있으면 민다(2026-11-14, <see cref="ConveyorBelt.TryPush"/>). 중심점이 벨트 구역을 벗어나는 순간
    /// 벨트가 밀던 속도로 그 방향을 따라 관성 시간 동안 점점 느려지며 간다(관성, <see cref="ConveyorInertia"/>, 조작과 따로 더함). 관성은 멈추거나 같은 직선 방향으로
    /// 미는 벨트에 올라서면 끝난다. 마주 보는 벨트의 경계에 닿으면 벨트 방향으로는 조작이 막히고(직각으로만) 거기서 벗어나도 관성이 없다.
    /// 턱 넘기(2026-11-13): 가는 쪽에 턱 넘기 높이 이하의 턱이 있으면 CharacterController가 올라가고, 바닥에 서 있다가 그 높이 이하로 내려가는 곳에서는
    /// 떨어지지 않고 바로 내려선다(아래로 구를 쏴 바닥이 있으면 붙임). 충돌은 한 번에 오르내리고, 몸 모델과 카메라는 바뀐 높이를 짧게 따라간다(<see cref="VisualOffset"/>).
    /// 수치는 <see cref="PlayerMoveSettings"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [SerializeField] PlayerMoveSettings settings;

        [Tooltip("이동 방향의 기준이 되는 카메라")]
        public ThirdPersonCamera playerCamera;

        [Tooltip("턱을 오르내릴 때 바뀐 높이를 부드럽게 따라갈 몸 모델(발밑이 원점)")]
        [SerializeField] Transform body;

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float visualOffset;
        float visualOffsetVelocity;
        Vector3 bodyPosition; // 몸 모델의 원래 위치(부모 기준)
        RaycastHit[] groundHits = new RaycastHit[8];
        ConveyorInertia inertia;
        float stopRate; // 키를 뗀 순간 정한 느려지는 정도(유닛/초²)
        float stopTime; // stopRate를 정한 관성 시간(밟은 바닥의 관성 시간이 바뀌면 그때 속도로 다시 정함)

        /// <summary>
        /// 캐릭터가 스스로 움직이는 중인가(직접 이동과 그 출발·멈춤 관성, 벨트를 벗어난 관성). 벨트에 실려 가기만 하는 이동은 빼고. 바닥 흔적이 쓴다.
        /// </summary>
        public bool IsSelfMoving => horizontalVelocity.sqrMagnitude > 0.0001f || inertia.Active;

        /// <summary>이동 속도(유닛/초). 출렁임이 쓴다.</summary>
        public float MoveSpeed => settings.moveSpeed;

        /// <summary>턱을 오르내린 직후 보이는 높이가 충돌보다 뒤처진 양(유닛, 오르면 음수). 몸 모델과 카메라가 더한다.</summary>
        public float VisualOffset => visualOffset;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (body != null) bodyPosition = body.localPosition;
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
            stopRate = 0f;
            verticalVelocity = 0f;
            inertia = default;
            visualOffset = visualOffsetVelocity = 0f;
            ApplyVisualOffset();
            if (playerCamera != null) playerCamera.LookAlong(rotation);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            bool locked = InputManager.Instance.IsGameplayLocked;
            Vector2 input = locked ? Vector2.zero : InputManager.Instance.ReadMove();

            // 카메라의 좌우 각도가 앞이다(위아래 각도와 상관없어 꼭대기에서 내려다봐도 정해진다).
            Vector3 forward = (playerCamera != null ? playerCamera.Heading : transform.rotation) * Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            cc.stepOffset = settings.stepHeight;
            bool wasGrounded = cc.isGrounded;
            // 벨트: 바닥에 서 있고 중심점이 벨트 구역 안이면 민다. 마주 보는 벨트의 경계면 그 축으로는 조작을 막는다.
            Vector3 push = Vector3.zero, lockedAxis = Vector3.zero;
            bool facing = false;
            bool onBelt = wasGrounded && ConveyorBelt.TryPush(Feet(), Time.deltaTime, out push, out facing, out lockedAxis);
            if (lockedAxis != Vector3.zero) move -= lockedAxis * Vector3.Dot(move, lockedAxis);
            if (wasGrounded)
            {
                // 출발·방향 바꾸기는 관성 시간에 이동 속도에 이르는 빠르기로, 키를 떼면 그때 속도에서 관성 시간 동안 멈춘다.
                // 중심점 아래가 미끄러운 코팅이면 그 종류의 관성 시간을 쓴다.
                float time = Mathf.Max(InertiaTime(), 0.0001f);
                bool moving = move.sqrMagnitude > 0.0001f;
                if (time != stopTime) stopRate = 0f;
                stopTime = time;
                if (moving) stopRate = 0f;
                else if (stopRate <= 0f) stopRate = horizontalVelocity.magnitude / time;
                float rate = moving ? settings.moveSpeed / time : stopRate;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, move * settings.moveSpeed, rate * Time.deltaTime);
                if (verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 땅에 붙어 있게 살짝 누른다.
            }
            if (lockedAxis != Vector3.zero) horizontalVelocity -= lockedAxis * Vector3.Dot(horizontalVelocity, lockedAxis);
            if (move.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(move), settings.turnSpeed * Time.deltaTime);

            // 관성: 같은 직선 방향으로 미는 벨트에 올라서면 그 벨트가 대신한다.
            var flatPush = new Vector2(push.x, push.z);
            if (inertia.Active && flatPush != Vector2.zero && Mathf.Abs(Vector2.Dot(flatPush.normalized, inertia.direction)) > 0.99f) inertia = default;
            var inertiaStep = inertia.Step(Time.deltaTime);

            verticalVelocity -= settings.gravity * Time.deltaTime;
            Vector3 velocity = horizontalVelocity + push + new Vector3(inertiaStep.x, 0f, inertiaStep.y) / Time.deltaTime;
            velocity.y = verticalVelocity;
            float yBefore = transform.position.y;
            cc.Move(velocity * Time.deltaTime);
            // 중심점이 벨트 구역을 벗어나면 그 순간 밀던 속도로 관성(마주 보는 벨트에서는 없음).
            if (onBelt && !facing && !ConveyorBelt.IsOnBelt(Feet())) inertia = ConveyorInertia.Start(flatPush, InertiaTime());
            // 바닥에 서 있다가 턱 높이 이하로 내려가는 곳이면 떨어지지 않고 바로 내려선다.
            if (wasGrounded && !cc.isGrounded && verticalVelocity <= 0f && HasGroundBelow(settings.stepHeight))
            {
                cc.Move(Vector3.down * (settings.stepHeight + cc.skinWidth));
                verticalVelocity = -2f;
            }
            // 땅에서 땅으로 턱 높이 이하만큼 오르내렸으면 보이는 높이는 뒤처졌다가 따라간다.
            float rise = transform.position.y - yBefore;
            if (wasGrounded && cc.isGrounded && Mathf.Abs(rise) > 0.02f && Mathf.Abs(rise) <= settings.stepHeight + 0.05f) visualOffset -= rise;
            visualOffset = settings.stepSmoothTime > 0f
                ? Mathf.SmoothDamp(visualOffset, 0f, ref visualOffsetVelocity, settings.stepSmoothTime)
                : 0f;
            ApplyVisualOffset();
        }

        void ApplyVisualOffset()
        {
            if (body != null) body.localPosition = bodyPosition + Vector3.up * visualOffset;
        }

        // 몸 구를 아래로 distance만큼 쏴 바닥(트리거·자기 자신 빼고)이 있는가.
        bool HasGroundBelow(float distance)
        {
            var center = transform.TransformPoint(cc.center);
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.SphereCastNonAlloc(center, cc.radius * 0.95f, Vector3.down, groundHits, distance + cc.skinWidth, ~0, QueryTriggerInteraction.Ignore)) == groundHits.Length)
                groundHits = new RaycastHit[groundHits.Length * 2];
            for (int i = 0; i < count; i++)
                if (groundHits[i].collider != cc) return true;
            return false;
        }

        // 발밑 가운데: 캡슐 바닥에서 피부 두께만큼 아래(바닥에 서 있으면 바닥 높이).
        // 지금 밟은 바닥의 관성 시간: 중심점 아래 블록 윗면 픽셀이 코팅이면 그 종류의 값, 아니면 이동 설정의 값.
        float InertiaTime() => SlipperyCoatingView.TryGetInertiaTime(Feet(), out float time) ? time : settings.inertiaTime;

        Vector3 Feet() => transform.TransformPoint(cc.center + Vector3.down * Mathf.Max(cc.height / 2f, cc.radius)) + Vector3.down * cc.skinWidth;
    }
}
