using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 카메라에서 므엥을 보는 시선 구멍(2026-11-08). 카메라에 붙인다.
    /// 맵을 시작할 때 지형(블록·계단)과 기물(오브·므엥 제외)의 재질을 같은 값의 점무늬 사본(<c>Slime ~ColorMaker~/Lit Dither</c>, 원래 재질마다 한 번)으로
    /// 한 번 바꿔 둔다. 뚫을지는 셰이더가 픽셀마다 정한다: 카메라에서 그 픽셀로 가는 시선이 므엥 깊이에서 므엥 둘레 반지름 안을 지나면
    /// (최대 투명도 반지름 안은 최대, 투명 시작 반지름까지 옅어짐) 그 시선 위의 면은 모두 같은 정도로 뚫린다 — 같은 시선 위 면이 같은 자리에서
    /// 함께 뚫려 덩어리 안쪽 면이 비치지 않는다. 앞 기준 평면(므엥 몸 가운데에서 카메라 쪽으로 앞 기준 거리, 시선 축에 수직)보다 카메라 반대쪽과
    /// 므엥 발 높이 이하의 픽셀은 뚫지 않는다.
    /// 흐림 진행은 하나다: 앞 기준 평면 ~ 카메라 사이 최대 투명도 반지름의 캡슐에 가리는 것이 있으면 흐림 시간 동안 1로, 없으면 0으로(전역 셰이더 값).
    /// 칠하기(<see cref="VoxPaint"/>)는 렌더러의 MaterialPropertyBlock이라 재질을 바꿔도 그대로다.
    /// </summary>
    [DefaultExecutionOrder(100)] // 카메라 위치(ThirdPersonCamera.LateUpdate) 뒤
    public class CameraOcclusionFader : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_OcclusionFade");
        static readonly int AxisStartId = Shader.PropertyToID("_OcclusionAxisStart");
        static readonly int AxisEndId = Shader.PropertyToID("_OcclusionAxisEnd");
        static readonly int ShapeId = Shader.PropertyToID("_OcclusionShape");
        static readonly int FeetId = Shader.PropertyToID("_OcclusionFeet");
        const float FeetMargin = 0.01f; // 발 높이와 같은 윗면(밟는 블록)도 빼도록 조금 위까지

        static readonly string[] Passes = { "DepthOnly", "DepthNormals", "ShadowCaster", "MotionVectors" };

        [SerializeField] CameraFadeSettings settings;

        [Tooltip("점무늬 셰이더(Assets/Shaders/LitDither.shader). 여기서 참조해 빌드에 들어가게 한다.")]
        [SerializeField] Shader ditherShader;

        [Tooltip("므엥(시선 축의 므엥 쪽 끝, 이 아래 렌더러·콜라이더는 빼고 찾음)")]
        [SerializeField] Transform target;

        [Tooltip("시선 축이 닿는 므엥 몸 가운데 높이(유닛)")]
        [SerializeField] float targetHeight = 0.5f;

        readonly Dictionary<Material, Material> ditherCopies = new();
        Collider[] hits = new Collider[32];
        float progress;

        void Start()
        {
            if (ditherShader == null || target == null) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var grid in root.GetComponentsInChildren<CubeGrid>(true)) UseDither(grid.gameObject);
                foreach (var fixture in root.GetComponentsInChildren<FixtureBase>(true))
                    if (fixture is not ColorSource) UseDither(fixture.gameObject);
            }
        }

        void UseDither(GameObject owner)
        {
            foreach (var renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(target)) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = DitherCopy(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        void LateUpdate()
        {
            if (settings == null || target == null) return;

            Vector3 start = target.position + Vector3.up * targetHeight;
            Vector3 camera = transform.position;
            float feet = target.position.y + FeetMargin;
            float startRadius = settings.startRadius;
            float fullRadius = Mathf.Min(settings.fullRadius, startRadius);
            Shader.SetGlobalVector(AxisStartId, start);
            Shader.SetGlobalVector(AxisEndId, camera);
            Shader.SetGlobalVector(ShapeId, new Vector4(fullRadius, startRadius, settings.fadeAmount, settings.frontOffset));
            Shader.SetGlobalFloat(FeetId, feet);

            float step = Time.deltaTime / settings.fadeTime;
            progress = Mathf.Clamp01(progress + (Blocked(start, camera, feet, fullRadius) ? step : -step));
            Shader.SetGlobalFloat(FadeId, progress);
        }

        // 앞 기준 평면 ~ 카메라 사이, 최대 투명도 반지름의 캡슐에 므엥 발 높이보다 위로 올라온 콜라이더(므엥·오브 제외)가 있는가.
        bool Blocked(Vector3 start, Vector3 camera, float feet, float radius)
        {
            Vector3 toCamera = camera - start;
            float length = toCamera.magnitude;
            if (length <= settings.frontOffset + radius) return false;
            Vector3 front = start + toCamera / length * (settings.frontOffset + radius);
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.OverlapCapsuleNonAlloc(front, camera, radius, hits, ~0, QueryTriggerInteraction.Collide)) == hits.Length)
                hits = new Collider[hits.Length * 2];
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(target) || hit.bounds.max.y <= feet || hit.GetComponentInParent<ColorSource>() != null) continue;
                return true;
            }
            return false;
        }

        void OnDisable() => Shader.SetGlobalFloat(FadeId, 0f);

        void OnDestroy()
        {
            foreach (var copy in ditherCopies.Values) Destroy(copy);
        }

        // 원래 재질의 값·키워드·그리기 순서·단계 켜짐을 그대로 옮긴 점무늬 사본.
        Material DitherCopy(Material original)
        {
            if (original == null) return null;
            if (ditherCopies.TryGetValue(original, out var copy)) return copy;
            copy = new Material(ditherShader) { name = original.name + " (Dither)" };
            copy.CopyPropertiesFromMaterial(original);
            copy.shaderKeywords = original.shaderKeywords;
            copy.renderQueue = original.renderQueue;
            copy.SetOverrideTag("RenderType", original.GetTag("RenderType", false));
            foreach (var pass in Passes) copy.SetShaderPassEnabled(pass, original.GetShaderPassEnabled(pass));
            copy.enableInstancing = original.enableInstancing;
            ditherCopies[original] = copy;
            return copy;
        }
    }
}
