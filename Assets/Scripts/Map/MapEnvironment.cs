using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorMaze
{
    /// <summary>
    /// 맵 환경(2026-11-08). MapRoot에 붙어 맵 환경 설정(<see cref="MapEnvironmentSettings"/>)을 그 맵에 적용한다: 하늘·환경 조명·반사·안개
    /// (씬의 조명 설정)와 태양. 에디터 편집 화면에서도 돈다(씬을 열 때, 애셋을 고칠 때). 맵 씬은 하나만 열고 그 씬이 활성 씬이라
    /// 씬의 조명 설정(RenderSettings)이 곧 이 맵의 것이다.
    /// 태양은 씬에 저장하지 않는 숨긴 방향광으로 만든다: 값이 애셋에만 있어 맵마다 프리팹 오버라이드가 생기지 않는다(맵에 다른 방향광은 두지 않음, 맵 검사가 알림).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MapEnvironment : MonoBehaviour
    {
        [Tooltip("맵 환경 설정(Assets/Data/MapEnvironmentSettings). 이 맵만 다르게 하려면 다른 애셋을 넣는다.")]
        [SerializeField] MapEnvironmentSettings settings;

        Light sun;

        void OnEnable()
        {
            MapEnvironmentSettings.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            MapEnvironmentSettings.Changed -= Apply;
            if (sun == null) return;
            if (Application.isPlaying) Destroy(sun.gameObject);
            else DestroyImmediate(sun.gameObject);
            sun = null;
        }

        void Apply()
        {
            if (settings == null || this == null) return;
#if UNITY_EDITOR
            // 프리팹 편집 화면(MapRoot를 열었을 때)과 프리팹 애셋에서는 하지 않는다.
            if (!gameObject.scene.IsValid() || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
#endif
            RenderSettings.skybox = settings.skybox;
            RenderSettings.ambientMode = settings.ambientMode;
            RenderSettings.ambientSkyColor = settings.ambientSkyColor;
            RenderSettings.ambientEquatorColor = settings.ambientEquatorColor;
            RenderSettings.ambientGroundColor = settings.ambientGroundColor;
            RenderSettings.ambientIntensity = settings.ambientIntensity;
            RenderSettings.defaultReflectionMode = settings.reflectionMode;
            RenderSettings.customReflectionTexture = settings.customReflection;
            RenderSettings.reflectionIntensity = settings.reflectionIntensity;
            RenderSettings.fog = settings.fog;
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogMode = settings.fogMode;
            RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.fogStartDistance = settings.fogStart;
            RenderSettings.fogEndDistance = settings.fogEnd;

            if (sun == null) sun = CreateSun();
            sun.color = settings.sunColor;
            sun.intensity = settings.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(settings.sunAngles);
            sun.shadows = settings.sunShadows;
            sun.shadowStrength = settings.sunShadowStrength;
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        Light CreateSun()
        {
            var go = new GameObject("Sun (맵 환경)") { hideFlags = HideFlags.HideAndDontSave };
            if (gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.bounceIntensity = 0f;
#if UNITY_EDITOR
            light.lightmapBakeType = LightmapBakeType.Realtime;
#endif
            return light;
        }
    }
}
