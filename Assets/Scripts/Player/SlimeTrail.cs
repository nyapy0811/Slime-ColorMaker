using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 캐릭터 바닥 흔적(2026-11-13, 픽셀). 바닥 평면을 1/16유닛(복셀 한 칸) 격자로 나누고, 발바닥(몸 반지름을 반 변으로 하는,
    /// 캐릭터와 함께 도는 정사각형) 안에 가운데가 든 픽셀을 캐릭터가 스스로 움직이는 동안(<see cref="ThirdPersonController.IsSelfMoving"/>, 직접 이동과
    /// 출발·멈춤 관성, 벨트를 벗어난 관성 포함, 벨트에 실려 가기만 하거나 가만히 있으면 안 칠함) 프레임마다 지금 색(몸 색과 같은 방식)으로 칠한다.
    /// 픽셀마다 바로 밑에 실제로 있는 면을 광선으로 찾아 그 높이에 칠하고(블록·계단 디딤판·벨트·방벽 판 등, 계단 충돌은 복셀 모델과 같음),
    /// 면이 없거나 너무 아래면(공중) 칠하지 않는다. 공중이거나 지금 색이 (0,0,0)이면 칠하지 않는다.
    /// 픽셀은 마지막으로 칠한 순간부터 점점 투명해져 유지 시간 뒤 없어진다.
    /// 픽셀은 각자 자기 위치를 갖고(2026-11-14) 한 자리에 여러 픽셀이 있을 수 있다. 칠하면 같은 자리에 있던 픽셀을 새 픽셀 하나로 바꾼다.
    /// 자리는 보통 격자 칸이고, 꺾임 벨트 칸의 벨트 구역(난간 빼고)에서는 벨트 자리(<see cref="ConveyorRules"/>, 진행 × 반지름 16 × 16 중 반지름 1~14번)다.
    /// 꺾임 칸에서는 발바닥 안에 가운데가 든 벨트 자리와 발바닥 안 격자 칸의 가운데가 든 벨트 자리를 칠한다.
    /// 벨트 위 픽셀은 벨트 무늬가 한 칸 흐를 때(<see cref="ConveyorBelt.TryGetStep"/>) 각자 벨트를 따라 1/16만큼 옮긴다(<see cref="ConveyorBelt.TryAdvance"/>,
    /// 꺾임은 돌림판). 겹쳐도 지우지 않아, 꺾임을 지나 나오면 모양이 그대로다. 벨트 밖으로 나가면 아래가 바닥이면 멈추고 비었으면 없앤다.
    /// 난간(직선 양 끝 한 줄, 꺾임 안쪽 모서리·바깥 호) 위 픽셀은 움직이지 않고, 움직이던 픽셀도 난간에 닿으면 멈춘다(2026-11-14).
    /// 그리기는 격자 칸마다 가장 진한 픽셀 하나만 그린다. 꺾임 칸의 격자 칸은 그 칸 가운데가 든 벨트 자리의 픽셀도 함께 따져 바깥쪽에 빈 칸이 보이지 않게 하고,
    /// 꺾임 벨트 자리 픽셀은 난간 격자 칸에 그리지 않는다.
    /// 바닥 위 작은 정사각형을 월드 좌표의 따로 둔 오브젝트 메시 하나에 매 프레임 다시 만든다(목록·메시는 재사용).
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerColor), typeof(ThirdPersonController))]
    public class SlimeTrail : MonoBehaviour
    {
        /// <summary>픽셀 한 칸(유닛).</summary>
        const float PixelSize = ConveyorBelt.StepPerFrame;

        /// <summary>한 칸의 픽셀 수.</summary>
        const int PixelsPerCell = ConveyorRules.Slots;

        /// <summary>바닥에서 띄우는 높이(유닛). 바닥과 겹쳐 깜박이지 않게 한다.</summary>
        const float Lift = 0.005f;

        /// <summary>면을 찾는 광선을 발 높이보다 이만큼 위에서 쏜다(유닛). 계단 오르막 쪽 픽셀(발바닥 반 너비만큼 높음)보다 위다.</summary>
        const float RayStart = 0.3f;

        static readonly Vector2Int GridSlot = new(-1, -1); // 자리 키에서 '격자 칸'을 뜻하는 벨트 자리 값

        struct Pixel
        {
            public Vector3 position; // 가운데(y = 칠한 바닥 높이)
            public Color color; // 선형 색
            public float time; // 마지막으로 칠한 시각
        }

        // 그릴 격자 칸 하나: 가장 진한 픽셀의 색(알파 = 진하기)과 바닥 높이.
        struct Shown
        {
            public Color color;
            public float height;
        }

        [SerializeField] SlimeTrailSettings settings;

        [Tooltip("흔적 재질(셰이더 Slime~ColorMaker~/Trail)")]
        [SerializeField] Material material;

        readonly List<Pixel> pixels = new();
        readonly HashSet<Vector2Int> footprint = new(); // 이번 프레임 발바닥 픽셀 (x, z)
        // 자리 키 = (칸, 벨트 자리). 격자 칸이면 (x 픽셀, 칸 높이, z 픽셀)과 GridSlot, 꺾임 칸 벨트 자리면 (벨트 칸, 자리).
        readonly Dictionary<(Vector3Int, Vector2Int), Pixel> painted = new();
        readonly Dictionary<(Vector3Int, Vector2Int), Shown> slotShown = new();
        readonly Dictionary<Vector3Int, Shown> shown = new();
        readonly HashSet<Vector3Int> curveCells = new();
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();
        RaycastHit[] rayHits = new RaycastHit[8];
        Predicate<Pixel> isRepainted;
        CharacterController cc;
        PlayerColor playerColor;
        ThirdPersonController mover;
        Mesh mesh;
        GameObject view;
        int lastStep;
        bool hasStep;
        Vector3 feet, footRight, footForward; // 이번 프레임 발바닥
        float footHalf;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            playerColor = GetComponent<PlayerColor>();
            mover = GetComponent<ThirdPersonController>();
            isRepainted = pixel => painted.ContainsKey(SlotOf(pixel.position));
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
            for (int i = pixels.Count - 1; i >= 0; i--)
                if (now - pixels[i].time >= settings.lifetime) RemoveAt(i);
        }

        // 순서는 상관없으므로 마지막 픽셀을 그 자리로 옮겨 지운다.
        void RemoveAt(int index)
        {
            pixels[index] = pixels[^1];
            pixels.RemoveAt(pixels.Count - 1);
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
            for (int i = pixels.Count - 1; i >= 0; i--)
            {
                var pixel = pixels[i];
                if (!ConveyorBelt.TryAdvance(pixel.position, PixelSize, out var moved)) continue;
                // 벨트 밖으로 나갔는데 아래가 비었으면(낭떠러지) 없앤다.
                if (!ConveyorBelt.IsBeltBelow(moved) && !HasFloor(moved))
                {
                    RemoveAt(i);
                    continue;
                }
                pixel.position = moved;
                pixels[i] = pixel;
            }
        }

        static bool HasFloor(Vector3 point) => Physics.CheckSphere(point + Vector3.down * 0.05f, 0.01f, ~0, QueryTriggerInteraction.Ignore);

        static Vector3Int GridKey(Vector3 position) =>
            new(Mathf.FloorToInt(position.x / PixelSize), ConveyorBelt.CellBelow(position).y, Mathf.FloorToInt(position.z / PixelSize));

        // 바닥 위 점의 자리: 꺾임 벨트 칸의 벨트 구역이면 벨트 자리, 아니면(난간 포함) 격자 칸.
        static (Vector3Int, Vector2Int) SlotOf(Vector3 position)
        {
            var cell = ConveyorBelt.CellBelow(position);
            return ConveyorBelt.TryCurveSlot(cell, position, out var slot) ? (cell, slot) : (GridKey(position), GridSlot);
        }

        // 캐릭터가 스스로 움직이는 동안 바닥에 서 있으면 발바닥 안 자리를 그 밑 면에 칠하고, 그 자리에 있던 픽셀을 새 픽셀로 바꾼다.
        void Paint(float now)
        {
            var value = playerColor.Current.Value;
            if (!cc.isGrounded || !mover.IsSelfMoving || value == default) return;
            footprint.Clear();
            painted.Clear();
            curveCells.Clear();
            Footprint(footprint);
            var color = ColorRules.ToColor(value).linear;
            foreach (var key in footprint)
            {
                var center = new Vector3((key.x + 0.5f) * PixelSize, feet.y, (key.y + 0.5f) * PixelSize);
                if (!TrySurface(center, feet.y, out float height)) continue;
                center.y = height;
                var slot = SlotOf(center);
                if (slot.Item2 != GridSlot)
                {
                    // 꺾임 칸: 격자 칸 가운데가 든 벨트 자리를 칠한다.
                    curveCells.Add(slot.Item1);
                    center = ConveyorBelt.CurveSlotCenter(slot.Item1, slot.Item2, height);
                }
                painted[slot] = new Pixel { position = center, color = color, time = now };
            }
            // 꺾임 칸: 발바닥 안에 가운데가 든 벨트 자리도 칠한다(안쪽의 작은 자리까지).
            foreach (var cell in curveCells)
            {
                for (int progress = 0; progress < PixelsPerCell; progress++)
                {
                    for (int radius = 0; radius < PixelsPerCell; radius++)
                    {
                        var slot = new Vector2Int(progress, radius);
                        var center = ConveyorBelt.CurveSlotCenter(cell, slot, feet.y);
                        if (!InFootprint(center) || !TrySurface(center, feet.y, out float height)) continue;
                        center.y = height;
                        if (SlotOf(center) != (cell, slot)) continue; // 벨트 위가 아닌 면(방벽 판 등)
                        painted[(cell, slot)] = new Pixel { position = center, color = color, time = now };
                    }
                }
            }
            pixels.RemoveAll(isRepainted);
            pixels.AddRange(painted.Values);
        }

        // 발바닥 안에 가운데가 든 픽셀 (x, z)를 모으고 이번 프레임 발바닥을 기억한다.
        void Footprint(HashSet<Vector2Int> into)
        {
            feet = transform.TransformPoint(cc.center + Vector3.down * Mathf.Max(cc.height / 2f, cc.radius));
            footHalf = cc.radius;
            footRight = transform.right;
            footForward = Vector3.Cross(footRight, Vector3.up);
            float reach = footHalf * (Mathf.Abs(footRight.x) + Mathf.Abs(footRight.z)); // 돌아간 정사각형을 덮는 축 정렬 반 너비
            int minX = Mathf.FloorToInt((feet.x - reach) / PixelSize), maxX = Mathf.FloorToInt((feet.x + reach) / PixelSize);
            int minZ = Mathf.FloorToInt((feet.z - reach) / PixelSize), maxZ = Mathf.FloorToInt((feet.z + reach) / PixelSize);
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    if (InFootprint(new Vector3((x + 0.5f) * PixelSize, 0f, (z + 0.5f) * PixelSize))) into.Add(new Vector2Int(x, z));
        }

        bool InFootprint(Vector3 point)
        {
            var offset = new Vector3(point.x - feet.x, 0f, point.z - feet.z);
            return Mathf.Abs(Vector3.Dot(offset, footRight)) <= footHalf && Mathf.Abs(Vector3.Dot(offset, footForward)) <= footHalf;
        }

        // 격자 칸마다 가장 진한 픽셀 하나를 그린다.
        void Build(float now)
        {
            shown.Clear();
            slotShown.Clear();
            curveCells.Clear();
            foreach (var pixel in pixels)
            {
                var color = pixel.color;
                color.a = settings.opacity * Mathf.Clamp01(1f - (now - pixel.time) / settings.lifetime);
                var entry = new Shown { color = color, height = pixel.position.y };
                var grid = GridKey(pixel.position);
                var slot = SlotOf(pixel.position);
                if (slot.Item2 == GridSlot)
                {
                    Keep(shown, grid, entry);
                    continue;
                }
                // 꺾임 벨트 자리 픽셀은 난간 격자 칸에는 그리지 않는다.
                if (ConveyorBelt.TryCurveSlot(slot.Item1, new Vector3((grid.x + 0.5f) * PixelSize, 0f, (grid.z + 0.5f) * PixelSize), out _)) Keep(shown, grid, entry);
                Keep(slotShown, slot, entry);
                curveCells.Add(slot.Item1);
            }
            // 꺾임 칸의 격자 칸은 그 칸 가운데가 든 벨트 자리의 픽셀도 따진다(바깥쪽 자리는 격자 칸보다 커서 빈 칸이 생기지 않게).
            foreach (var cell in curveCells)
            {
                int baseX = cell.x * PixelsPerCell - PixelsPerCell / 2, baseZ = cell.z * PixelsPerCell - PixelsPerCell / 2;
                for (int x = baseX; x < baseX + PixelsPerCell; x++)
                {
                    for (int z = baseZ; z < baseZ + PixelsPerCell; z++)
                    {
                        var center = new Vector3((x + 0.5f) * PixelSize, 0f, (z + 0.5f) * PixelSize);
                        if (ConveyorBelt.TryCurveSlot(cell, center, out var slot) && slotShown.TryGetValue((cell, slot), out var entry))
                            Keep(shown, new Vector3Int(x, cell.y, z), entry);
                    }
                }
            }

            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            foreach (var pair in shown)
            {
                var key = pair.Key;
                float x = key.x * PixelSize, z = key.z * PixelSize, y = pair.Value.height + Lift;
                int v = vertices.Count;
                vertices.Add(new Vector3(x, y, z));
                vertices.Add(new Vector3(x, y, z + PixelSize));
                vertices.Add(new Vector3(x + PixelSize, y, z + PixelSize));
                vertices.Add(new Vector3(x + PixelSize, y, z));
                for (int i = 0; i < 4; i++) colors.Add(pair.Value.color);
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

        static void Keep<TKey>(Dictionary<TKey, Shown> into, TKey key, Shown entry)
        {
            if (!into.TryGetValue(key, out var existing) || existing.color.a < entry.color.a) into[key] = entry;
        }
    }
}
