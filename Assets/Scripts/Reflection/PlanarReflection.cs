using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlimeColorMaker
{
    /// <summary>
    /// ① 평면 반사(<see cref="ReflectionSettings"/>). 코팅은 늘 블록 윗면(수평, 정수 높이)에 있으므로, 보이는 코팅 높이마다 반사 카메라가 지금 카메라(Camera.main)를
    /// 그 평면에 거울처럼 뒤집어 그린다(평면 아래는 잘라 냄, 그림자·후처리 없음). 셰이더(Lit Glass의 _PLANAR_REFLECTION, PlanarReflection.hlsl)가
    /// 그 그림을 화면 좌표로 읽어 윗면의 ②·③ 반사(실내 반사 탐침·하늘) 대신 쓴다. 그래서 주변 물체·캐릭터·하늘이 제자리에 비친다.
    /// Play 중에만 돈다(에디터 편집 화면은 ②·③). <see cref="SlipperyCoatingView"/>가 Play에서 붙이고 코팅 높이를 알려 준다.
    /// 카메라가 평면보다 아래거나 그 높이의 코팅이 화면 밖이면 그리지 않고, 한 번에 설정의 최대 수(카메라와 높이가 가까운 것부터)까지 그린다.
    /// </summary>
    [DefaultExecutionOrder(200)] // 카메라 위치(ThirdPersonCamera.LateUpdate) 뒤
    public class PlanarReflection : MonoBehaviour
    {
        public const int MaxPlanes = 4;
        // 렌더 파이프라인 애셋(PC·Mobile)의 렌더러 목록 1번 = 평면 반사 전용(PlanarReflection_Renderer, 화면 공간 효과 SSR·SSAO 없음).
        // 화면 공간 효과는 거울 속 그림에 필요 없고, 깊이 텍스처를 끈 반사 카메라에서 SSR이 오류를 낸다.
        const int RendererIndex = 1;
        const float ClipOffset = 0.01f; // 평면 바로 위까지 잘라 코팅·블록 윗면 자신은 비치지 않게

        static readonly int[] TextureIds =
        {
            Shader.PropertyToID("_PlanarReflectionTex0"), Shader.PropertyToID("_PlanarReflectionTex1"),
            Shader.PropertyToID("_PlanarReflectionTex2"), Shader.PropertyToID("_PlanarReflectionTex3"),
        };
        static readonly int HeightsId = Shader.PropertyToID("_PlanarReflectionHeights");
        static readonly int ValidId = Shader.PropertyToID("_PlanarReflectionValid");

        readonly List<(float height, Bounds bounds)> planes = new();
        readonly List<(float height, Bounds bounds)> order = new();
        readonly Camera[] cameras = new Camera[MaxPlanes];
        readonly RenderTexture[] textures = new RenderTexture[MaxPlanes];
        readonly Plane[] frustum = new Plane[6];

        /// <summary>코팅 높이(윗면 y)와 그 높이 코팅 전체의 범위.</summary>
        public void SetPlanes(IEnumerable<(float height, Bounds bounds)> coatingPlanes)
        {
            planes.Clear();
            planes.AddRange(coatingPlanes);
        }

        void LateUpdate()
        {
            var heights = Vector4.zero;
            var valid = Vector4.zero;
            var main = Camera.main;
            var settings = ReflectionSettings.Current;
            if (main != null && settings != null && settings.planar && planes.Count > 0)
            {
                GeometryUtility.CalculateFrustumPlanes(main, frustum);
                var eye = main.transform.position;
                order.Clear();
                order.AddRange(planes);
                order.Sort((a, b) => Mathf.Abs(eye.y - a.height).CompareTo(Mathf.Abs(eye.y - b.height)));
                int slot = 0;
                int limit = Mathf.Clamp(settings.planarMaxPlanes, 1, MaxPlanes);
                foreach (var (height, bounds) in order)
                {
                    if (slot >= limit) break;
                    if (eye.y <= height + ClipOffset || !GeometryUtility.TestPlanesAABB(frustum, bounds)) continue;
                    if (!RenderPlane(slot, main, height, settings.planarScale)) continue;
                    Shader.SetGlobalTexture(TextureIds[slot], textures[slot]);
                    heights[slot] = height;
                    valid[slot] = 1f;
                    slot++;
                }
            }
            Shader.SetGlobalVector(HeightsId, heights);
            Shader.SetGlobalVector(ValidId, valid);
        }

        bool RenderPlane(int slot, Camera main, float height, float scale)
        {
            var camera = cameras[slot] != null ? cameras[slot] : cameras[slot] = CreateCamera(slot);
            int width = Mathf.Max(16, Mathf.RoundToInt(main.pixelWidth * scale));
            int pixelHeight = Mathf.Max(16, Mathf.RoundToInt(main.pixelHeight * scale));
            var texture = textures[slot];
            if (texture == null || texture.width != width || texture.height != pixelHeight)
            {
                if (texture != null) Destroy(texture);
                texture = textures[slot] = new RenderTexture(width, pixelHeight, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = $"Planar Reflection {slot}", useMipMap = true, autoGenerateMips = true, hideFlags = HideFlags.HideAndDontSave,
                };
            }

            // 지금 카메라를 평면 y = height에 뒤집는다.
            camera.fieldOfView = main.fieldOfView;
            camera.nearClipPlane = main.nearClipPlane;
            camera.farClipPlane = main.farClipPlane;
            camera.aspect = main.aspect;
            camera.cullingMask = main.cullingMask;
            camera.clearFlags = main.clearFlags;
            camera.backgroundColor = main.backgroundColor;
            camera.targetTexture = texture;
            var mirror = Mirror(height);
            camera.worldToCameraMatrix = main.worldToCameraMatrix * mirror;
            var position = mirror.MultiplyPoint(main.transform.position);
            var forward = Vector3.Reflect(main.transform.forward, Vector3.up);
            var up = Vector3.Reflect(main.transform.up, Vector3.up);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            // 평면 아래(거울 속에서는 평면 너머)는 비스듬한 가까운 면으로 잘라 낸다.
            camera.projectionMatrix = main.CalculateObliqueMatrix(CameraSpacePlane(camera.worldToCameraMatrix, height + ClipOffset));
            camera.cullingMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return false;
            GL.invertCulling = true; // 뒤집힌 그림은 면의 앞뒤가 바뀐다
            RenderPipeline.SubmitRenderRequest(camera, request);
            GL.invertCulling = false;
            return true;
        }

        static Camera CreateCamera(int slot)
        {
            var go = new GameObject($"Planar Reflection Camera {slot}") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            camera.enabled = false; // 직접 그린다
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            data.SetRenderer(RendererIndex);
            return camera;
        }

        // 평면 y = height에 대한 거울 행렬.
        static Matrix4x4 Mirror(float height)
        {
            var m = Matrix4x4.identity;
            m.m11 = -1f;
            m.m13 = 2f * height;
            return m;
        }

        // 월드의 평면 y = height(위쪽이 남는 쪽)를 카메라 공간 평면으로.
        static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, float height)
        {
            var point = worldToCamera.MultiplyPoint(new Vector3(0f, height, 0f));
            var normal = worldToCamera.MultiplyVector(Vector3.up).normalized;
            return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(ValidId, Vector4.zero);
            for (int i = 0; i < MaxPlanes; i++)
            {
                if (cameras[i] != null) Destroy(cameras[i].gameObject);
                if (textures[i] != null) Destroy(textures[i]);
                cameras[i] = null;
                textures[i] = null;
            }
        }
    }
}
