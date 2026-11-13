using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 캐릭터 바닥 흔적(2026-11-13, 픽셀). 바닥 평면을 1/16유닛(복셀 한 칸) 격자로 나누고, 발바닥(몸 반지름을 반 변으로 하는,
    /// 캐릭터와 함께 도는 정사각형) 안에 가운데가 든 픽셀을 플레이어가 직접 움직이는 동안(<see cref="ThirdPersonController.IsWalking"/>,
    /// 벨트에 실려 가기만 하거나 가만히 있으면 안 칠함) 프레임마다 지금 색(몸 색과 같은 방식)으로 칠한다.
    /// 픽셀마다 바로 밑에 실제로 있는 면을 광선으로 찾아 그 높이에 칠하고(블록·계단 디딤판·벨트·방벽 판 등, 계단 충돌은 복셀 모델과 같음),
    /// 면이 없거나 너무 아래면(공중) 칠하지 않는다.
    /// 픽셀은 마지막으로 칠한 순간부터 점점 투명해져 유지 시간 뒤 없어진다. 공중이거나 지금 색이 (0,0,0)이면 칠하지 않는다.
    /// 벨트 위 픽셀은 벨트 무늬가 한 칸 흐를 때(<see cref="ConveyorBelt.TryGetStep"/>) 각자 미는 방향으로 한 칸 옮긴다
    /// (<see cref="ConveyorBelt.PointDirection"/>, 꺾이는 칸 규칙 포함). 옮긴 자리가 겹치면 더 최근에 칠한 픽셀이 남고,
    /// 벨트 밖으로 나가면 아래가 바닥이면 멈추고 비었으면 없앤다. 픽셀마다 따로라 서로 잇지 않는다.
    /// 그리기는 픽셀마다 바닥 위 작은 정사각형을 월드 좌표의 따로 둔 오브젝트 메시 하나에 매 프레임 다시 만든다(목록·메시는 재사용).
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerColor), typeof(ThirdPersonController))]
    public class SlimeTrail : MonoBehaviour
    {
        /// <summary>픽셀 한 칸(유닛).</summary>
        const float PixelSize = ConveyorBelt.StepPerFrame;

        /// <summary>바닥에서 띄우는 높이(유닛). 바닥과 겹쳐 깜박이지 않게 한다.</summary>
        const float Lift = 0.005f;

        /// <summary>면을 찾는 광선을 발 높이보다 이만큼 위에서 쏜다(유닛). 계단 오르막 쪽 픽셀(발바닥 반 너비만큼 높음)보다 위다.</summary>
        const float RayStart = 0.3f;

        struct Pixel
        {
            public Color color; // 선형 색
            public float time; // 마지막으로 칠한 시각
            public float height; // 칠한 바닥 높이(월드 y)
        }

        [SerializeField] SlimeTrailSettings settings;

        [Tooltip("흔적 재질(셰이더 Slime~ColorMaker~/Trail)")]
        [SerializeField] Material material;

        // 키 = (x 픽셀, 바닥 칸 높이, z 픽셀)
        Dictionary<Vector3Int, Pixel> pixels = new();
        Dictionary<Vector3Int, Pixel> moved = new();
        readonly List<Vector3Int> keys = new();
        readonly HashSet<Vector2Int> footprint = new(); // 이번 프레임 발바닥 픽셀 (x, z)
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();
        RaycastHit[] rayHits = new RaycastHit[8];
        CharacterController cc;
        PlayerColor playerColor;
        ThirdPersonController mover;
        Mesh mesh;
        GameObject view;
        int lastStep;
        bool hasStep;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            playerColor = GetComponent<PlayerColor>();
            mover = GetComponent<ThirdPersonController>();
            mesh = new Mesh { name = "SlimeTrail", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            view = new GameObject("SlimeTrail");
            view.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // 픽셀 가운데 (x, z) 바로 밑에 실제로 있는 면의 높이. 발 높이 + RayStart에서 아래로, 발 높이 − (발바닥 반 너비 + 0.05)까지 찾는다
        // (계단 내리막 쪽 픽셀까지). 캐릭터 자신과 트리거는 무시한다. 없으면 false(공중).
        bool TrySurface(Vector3 center, float feetY, out float height)
        {
            var origin = new Vector3(center.x, feetY + RayStart, center.z);
            float length = RayStart + cc.radius + 0.05f;
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.RaycastNonAlloc(origin, Vector3.down, rayHits, length, ~0, QueryTriggerInteraction.Ignore)) == rayHits.Length)
                rayHits = new RaycastHit[rayHits.Length * 2];
            int nearest = -1;
            for (int i = 0; i < count; i++)
                if (rayHits[i].collider != cc && (nearest < 0 || rayHits[i].distance < rayHits[nearest].distance)) nearest = i;
            if (nearest < 0)
            {
                height = 0f;
                return false;
            }
            height = rayHits[nearest].point.y;
            return true;
        }

        void OnDestroy()
        {
            if (view != null) Destroy(view);
            Destroy(mesh);
        }

        // 이동(Update) 뒤에 칠한다.
        void LateUpdate()
        {
            float now = Time.time;
            Expire(now);
            MoveOnBelts();
            Paint(now);
            Build(now);
        }

        void Expire(float now)
        {
            keys.Clear();
            foreach (var pair in pixels)
                if (now - pair.Value.time >= settings.lifetime) keys.Add(pair.Key);
            foreach (var key in keys) pixels.Remove(key);
        }

        // 지난 프레임 뒤로 무늬가 흐른 칸 수만큼 벨트 위 픽셀을 한 칸씩 옮긴다.
        void MoveOnBelts()
        {
            if (!ConveyorBelt.TryGetStep(out int step))
            {
                hasStep = false;
                return;
            }
            int steps = hasStep ? step - lastStep : 0;
            lastStep = step;
            hasStep = true;
            for (int i = 0; i < steps && pixels.Count > 0; i++) MoveOnce();
        }

        void MoveOnce()
        {
            moved.Clear();
            foreach (var pair in pixels)
            {
                var key = pair.Key;
                var pixel = pair.Value;
                var direction = ConveyorBelt.PointDirection(Center(key, pixel));
                if (direction != Vector2Int.zero)
                {
                    key += new Vector3Int(direction.x, 0, direction.y);
                    // 벨트 밖으로 나갔는데 아래가 비었으면(낭떠러지) 없앤다.
                    if (ConveyorBelt.PointDirection(Center(key, pixel)) == Vector2Int.zero && !HasFloor(Center(key, pixel))) continue;
                }
                if (!moved.TryGetValue(key, out var existing) || existing.time < pixel.time) moved[key] = pixel;
            }
            (pixels, moved) = (moved, pixels);
        }

        static Vector3 Center(Vector3Int key, Pixel pixel) => new((key.x + 0.5f) * PixelSize, pixel.height, (key.z + 0.5f) * PixelSize);

        static bool HasFloor(Vector3 point) => Physics.CheckSphere(point + Vector3.down * 0.05f, 0.01f, ~0, QueryTriggerInteraction.Ignore);

        // 플레이어가 직접 움직이는 동안 바닥에 서 있으면 발바닥 안 픽셀을 그 밑 면에 칠한다. 키의 높이는 면이 든 칸.
        void Paint(float now)
        {
            var value = playerColor.Current.Value;
            if (!cc.isGrounded || !mover.IsWalking || value == default) return;
            footprint.Clear();
            float feetY = Footprint(footprint);
            var color = ColorRules.ToColor(value).linear;
            foreach (var key in footprint)
            {
                var center = new Vector3((key.x + 0.5f) * PixelSize, feetY, (key.y + 0.5f) * PixelSize);
                if (!TrySurface(center, feetY, out float height)) continue;
                int level = CubeGridRules.CellAt(new Vector3(center.x, height - 0.01f, center.z)).y;
                pixels[new Vector3Int(key.x, level, key.y)] = new Pixel { color = color, time = now, height = height };
            }
        }

        // 발바닥 안에 가운데가 든 픽셀 (x, z)를 모으고 발 높이를 돌려준다.
        float Footprint(HashSet<Vector2Int> into)
        {
            var feet = transform.TransformPoint(cc.center + Vector3.down * Mathf.Max(cc.height / 2f, cc.radius));
            float half = cc.radius;
            var right = transform.right;
            var forward = Vector3.Cross(right, Vector3.up);
            float reach = half * (Mathf.Abs(right.x) + Mathf.Abs(right.z)); // 돌아간 정사각형을 덮는 축 정렬 반 너비
            int minX = Mathf.FloorToInt((feet.x - reach) / PixelSize), maxX = Mathf.FloorToInt((feet.x + reach) / PixelSize);
            int minZ = Mathf.FloorToInt((feet.z - reach) / PixelSize), maxZ = Mathf.FloorToInt((feet.z + reach) / PixelSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var offset = new Vector3((x + 0.5f) * PixelSize - feet.x, 0f, (z + 0.5f) * PixelSize - feet.z);
                    if (Mathf.Abs(Vector3.Dot(offset, right)) > half || Mathf.Abs(Vector3.Dot(offset, forward)) > half) continue;
                    into.Add(new Vector2Int(x, z));
                }
            }
            return feet.y;
        }

        void Build(float now)
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            foreach (var pair in pixels)
            {
                var key = pair.Key;
                var pixel = pair.Value;
                float x = key.x * PixelSize, z = key.z * PixelSize, y = pixel.height + Lift;
                var color = pixel.color;
                color.a = settings.opacity * Mathf.Clamp01(1f - (now - pixel.time) / settings.lifetime);
                int v = vertices.Count;
                vertices.Add(new Vector3(x, y, z));
                vertices.Add(new Vector3(x, y, z + PixelSize));
                vertices.Add(new Vector3(x + PixelSize, y, z + PixelSize));
                vertices.Add(new Vector3(x + PixelSize, y, z));
                for (int i = 0; i < 4; i++) colors.Add(color);
                triangles.Add(v);
                triangles.Add(v + 1);
                triangles.Add(v + 2);
                triangles.Add(v);
                triangles.Add(v + 2);
                triangles.Add(v + 3);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
