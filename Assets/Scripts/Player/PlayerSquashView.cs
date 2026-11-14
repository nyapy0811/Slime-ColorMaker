using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 므엥의 말랑말랑한 출렁임(푸딩·슬라임 느낌). 몸 모델(<see cref="body"/>) 메시의 꼭짓점만 바꾸므로
    /// 콜라이더·이동·닿음·카메라는 그대로다. 출렁임은 스프링 둘이다:
    /// 위아래(늘이고 누름, 옆으로는 부피가 같게 반대로)와 윗부분 흔들림(감쇠를 낮게 해 여러 번 흔들림).
    /// 바닥면은 늘 평평하고 밀리지 않는다: 기울기는 높이만큼 수평으로 밀고(윗면만 쏠림), 옆으로 퍼지거나 좁아지는 양은
    /// 바닥에서 설정의 바닥 퍼짐 비율, 꼭대기에서 100%로 높이에 따라 이어진다(바닥면은 넓이만 바뀜).
    /// 매 프레임 상태를 공중·멈춤·이동 중으로 보고, 바뀌는 순간 그 반응을 한 번 준다:
    /// 출발(멈춤 → 이동 중) = 위로 늘고 윗부분이 뒤로 처짐, 도착(이동 중 → 멈춤) = 눌리고 윗부분이 앞으로 넘침,
    /// 착지(공중 → 땅) = 낙하 속도에 비례해 철퍽(출발·도착 반응은 주지 않음). 이동 중에는 걸은 거리에 맞춰 통통 튀고,
    /// 땅에서는 가속도만큼 윗부분이 반대로 쏠린다(돌면 바깥으로). 수치는 설정 애셋에서 매 프레임 읽으므로 Play 모드에서 고치며 볼 수 있다.
    /// 일시정지 중에는 멈춘다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(ThirdPersonController))]
    public class PlayerSquashView : MonoBehaviour
    {
        enum Motion { Air, Idle, Moving }

        const float StepTime = 1f / 120f; // 프레임이 느려도 스프링이 튀지 않게 잘게 나눠 계산한다.
        const float MaxAccel = 100f;       // 이보다 큰 속도 변화(순간이동 등)는 가속도로 보지 않는다.

        [SerializeField] PlayerSquashSettings settings;

        [Tooltip("변형할 몸 모델(발밑이 원점, 아래 메시를 모두 변형)")]
        [SerializeField] Transform body;

        // 변형하는 메시 하나: 원래 꼭짓점(몸 기준)과 몸 → 메시 변환.
        sealed class Part
        {
            public Mesh mesh;
            public Vector3[] source;
            public Vector3[] deformed;
            public Matrix4x4 bodyToMesh;
        }

        CharacterController cc;
        ThirdPersonController movement;
        Part[] parts;
        float modelHeight = 1f;
        Motion motion = Motion.Idle;
        Vector3 lastVelocity;
        Vector3 lastMoveDirection = Vector3.forward;
        float fallSpeed;
        float phase;

        float stretch;
        float stretchVelocity;
        Vector3 lean;          // 윗부분이 쏠린 방향과 각도(도, 수평 벡터)
        Vector3 leanVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            movement = GetComponent<ThirdPersonController>();
            if (body != null) SetUpParts();
        }

        // 모델 메시를 복사해 쓴다(가져온 원본 메시는 그대로). 꼭짓점은 몸 기준으로 바꿔 두고, 모델 높이는 가장 높은 꼭짓점.
        void SetUpParts()
        {
            var filters = body.GetComponentsInChildren<MeshFilter>(true);
            parts = new Part[filters.Length];
            float top = 0f;
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var mesh = Instantiate(filter.sharedMesh);
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
                var meshToBody = body.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var source = mesh.vertices;
                for (int v = 0; v < source.Length; v++)
                {
                    source[v] = meshToBody.MultiplyPoint3x4(source[v]);
                    top = Mathf.Max(top, source[v].y);
                }
                parts[i] = new Part { mesh = mesh, source = source, deformed = new Vector3[source.Length], bodyToMesh = meshToBody.inverse };
            }
            if (top > 0f) modelHeight = top;
        }

        void OnDestroy()
        {
            if (parts == null) return;
            foreach (var part in parts) Destroy(part.mesh);
        }

        // 이동(Update) 뒤에 잰다.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || settings == null || parts == null) return;

            Vector3 velocity = cc.velocity;
            Vector3 horizontal = new(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            var next = !cc.isGrounded ? Motion.Air : speed > settings.moveThreshold ? Motion.Moving : Motion.Idle;

            if (next == Motion.Air) fallSpeed = Mathf.Max(0f, -velocity.y);
            if (next != motion) OnMotionChanged(motion, next);
            motion = next;
            if (motion == Motion.Moving) lastMoveDirection = horizontal / speed;

            // 땅에서는 가속도의 반대로 윗부분이 쏠린다(공중에서는 수평 속도가 그대로라 가라앉기만 함).
            Vector3 accel = (horizontal - lastVelocity) / dt;
            lastVelocity = horizontal;
            Vector3 leanTarget = motion != Motion.Air && accel.magnitude < MaxAccel
                ? Vector3.ClampMagnitude(-accel * settings.leanPerAccel, settings.maxLean)
                : Vector3.zero;

            float stretchTarget = 0f;
            if (motion == Motion.Moving)
            {
                phase = Mathf.Repeat(phase + speed * dt * settings.bouncesPerUnit, 1f);
                float ratio = Mathf.Clamp01(speed / Mathf.Max(movement.MoveSpeed, 0.01f));
                stretchTarget = settings.walkBounce * ratio * Mathf.Sin(phase * 2f * Mathf.PI);
            }
            else phase = 0f;

            for (float left = dt; left > 0f; left -= StepTime)
            {
                float step = Mathf.Min(left, StepTime);
                StepStretch(stretchTarget, step);
                StepLean(leanTarget, step);
            }
            Apply();
        }

        void OnMotionChanged(Motion from, Motion to)
        {
            if (from == Motion.Air)
            {
                if (fallSpeed > settings.landSpeedThreshold) stretchVelocity -= settings.landKick * fallSpeed; // 착지
            }
            else if (from == Motion.Idle && to == Motion.Moving) // 출발: 가는 반대쪽으로 처짐
            {
                stretchVelocity += settings.startStretch;
                Vector3 direction = new Vector3(cc.velocity.x, 0f, cc.velocity.z).normalized;
                leanVelocity -= direction * settings.startLean;
            }
            else if (from == Motion.Moving && to == Motion.Idle) // 도착: 가던 쪽으로 넘침
            {
                stretchVelocity -= settings.stopSquash;
                leanVelocity += lastMoveDirection * settings.stopLean;
            }
        }

        // 감쇠 스프링: 목표 쪽으로 당기고 속도에 비례해 늦춘다. 최대 출렁임을 넘으면 그 자리에서 멈춘다.
        void StepStretch(float target, float dt)
        {
            float k = settings.stiffness;
            stretchVelocity += (k * (target - stretch) - 2f * settings.damping * Mathf.Sqrt(k) * stretchVelocity) * dt;
            stretch += stretchVelocity * dt;
            float max = settings.maxStretch;
            if (Mathf.Abs(stretch) <= max) return;
            stretch = Mathf.Sign(stretch) * max;
            stretchVelocity = 0f;
        }

        // 기울기도 같은 스프링을 수평 두 축에 쓴다. 최대 기울기를 넘으면 바깥으로 가는 속도만 없앤다.
        void StepLean(Vector3 target, float dt)
        {
            float k = settings.leanStiffness;
            leanVelocity += (k * (target - lean) - 2f * settings.leanDamping * Mathf.Sqrt(k) * leanVelocity) * dt;
            lean += leanVelocity * dt;
            float max = settings.maxLean;
            if (lean.magnitude <= max) return;
            lean = lean.normalized * max;
            float outward = Vector3.Dot(leanVelocity, lean.normalized);
            if (outward > 0f) leanVelocity -= lean.normalized * outward;
        }

        // 몸 기준 꼭짓점을 높이 비율(바닥 0 ~ 꼭대기 1)에 따라 변형한다: 높이는 (1 + stretch)배, 옆은 1/√(1 + stretch)배를
        // 바닥에서 바닥 퍼짐 비율만큼 ~ 꼭대기에서 전부 쓰고, 늘인 뒤 높이 × tan(기울기)만큼 기울기 쪽으로 민다. 바닥은 높이 0 그대로다.
        void Apply()
        {
            float height = 1f + stretch;
            float side = 1f / Mathf.Sqrt(height);
            float angle = lean.magnitude;
            Vector3 shear = angle > 0.0001f
                ? body.InverseTransformDirection(lean / angle) * Mathf.Tan(angle * Mathf.Deg2Rad)
                : Vector3.zero;
            shear.y = 0f;

            foreach (var part in parts)
            {
                for (int v = 0; v < part.source.Length; v++)
                {
                    var p = part.source[v];
                    float spread = 1f + (side - 1f) * Mathf.Lerp(settings.bottomSpread, 1f, Mathf.Clamp01(p.y / modelHeight));
                    float y = p.y * height;
                    var moved = new Vector3(p.x * spread, y, p.z * spread) + shear * y;
                    part.deformed[v] = part.bodyToMesh.MultiplyPoint3x4(moved);
                }
                part.mesh.vertices = part.deformed;
                part.mesh.RecalculateBounds();
            }
        }
    }
}
