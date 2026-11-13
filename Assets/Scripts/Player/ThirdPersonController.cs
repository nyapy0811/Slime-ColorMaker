using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 플레이어 이동(2026-11-08, 1인칭을 없애고 정식 플레이어가 됨). WASD는 카메라가 보는 방향 기준이고 캐릭터는 가는 방향으로 돈다.
    /// 지면에서는 가속해 움직이고, 공중에서는 수평 속도를 유지한다. 게임플레이 입력이 잠기면 이동을 받지 않고(중력은 계속), 일시정지 중에는 멈춘다.
    /// 점프는 없다(2026-11-07): 위 칸으로는 계단으로만 간다. 피벗은 발밑이다. 시작 지점·체크포인트로 옮기기와 낙사는 맵 매니저가 맡는다.
    /// 컨베이어 벨트 위에 서 있으면 벨트가 흐르는 속도를 이동에 그대로 더한다(2026-11-12, 가속 없음, 내려오면 바로 멈춤).
    /// 발바닥(몸 반지름을 반 변으로 하는, 캐릭터와 함께 도는 정사각형)이 조금이라도 벨트에 걸치면 민다(2026-11-13). 방향은 <see cref="ConveyorBelt.PushVelocity"/>가 정한다.
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
        Collider[] floorHits = new Collider[8];
        RaycastHit[] groundHits = new RaycastHit[8];
        readonly List<ConveyorBelt> touchedBelts = new();

        /// <summary>플레이어가 직접 움직이는 중인가(입력으로 생긴 수평 속도, 벨트가 미는 속도는 빼고). 바닥 흔적이 쓴다.</summary>
        public bool IsWalking => horizontalVelocity.sqrMagnitude > 0.0001f;

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
            verticalVelocity = 0f;
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
            if (wasGrounded)
            {
                float accel = settings.moveSpeed / Mathf.Max(settings.accelerationTime, 0.0001f);
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, move * settings.moveSpeed, accel * Time.deltaTime);
                if (verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 땅에 붙어 있게 살짝 누른다.
            }
            if (move.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(move), settings.turnSpeed * Time.deltaTime);

            verticalVelocity -= settings.gravity * Time.deltaTime;
            Vector3 velocity = horizontalVelocity + ConveyorVelocity();
            velocity.y = verticalVelocity;
            float yBefore = transform.position.y;
            cc.Move(velocity * Time.deltaTime);
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

        // 발밑 바로 아래(피부 두께 + 0.005 ~ 0.105)에 발바닥 크기의 얇은 상자를 대고 걸친 벨트를 모아 미는 속도를 받는다. 공중이면 0.
        // 상자 윗면이 발 높이보다 아래라 옆에 벽처럼 선 벨트(바닥이 발 높이)는 잡지 않는다.
        Vector3 ConveyorVelocity()
        {
            if (!cc.isGrounded) return Vector3.zero;
            float half = cc.radius;
            var feet = transform.TransformPoint(cc.center + Vector3.down * Mathf.Max(cc.height / 2f, cc.radius));
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.OverlapBoxNonAlloc(feet + Vector3.down * (cc.skinWidth + 0.055f), new Vector3(half, 0.05f, half), floorHits, transform.rotation, ~0, QueryTriggerInteraction.Ignore)) == floorHits.Length)
                floorHits = new Collider[floorHits.Length * 2];
            touchedBelts.Clear();
            for (int i = 0; i < count; i++)
                if (floorHits[i].GetComponentInParent<ConveyorBelt>() is { } belt && !touchedBelts.Contains(belt)) touchedBelts.Add(belt);
            return ConveyorBelt.PushVelocity(feet, touchedBelts);
        }
    }
}
