using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 환경. MapRoot에 붙어 맵 환경 설정(<see cref="MapEnvironmentSettings"/>)을 그 맵에 적용한다: 하늘·환경 조명·하늘 반사(<see cref="ReflectionSettings"/>, 지금 맵의 반사 설정으로도 알림)·안개
    /// (씬의 조명 설정)와 태양. 에디터 편집 화면에서도 돈다(씬을 열 때, 애셋을 고칠 때). 맵 씬은 하나만 열고 그 씬이 활성 씬이라
    /// 씬의 조명 설정(RenderSettings)이 곧 이 맵의 것이다.
    /// 태양은 맵 씬에 한 번 만들어 저장해 두는 숨긴 방향광(<see cref="SunName"/>, 고칠 수 없음)이고 값은 열 때마다 애셋에서 넣는다:
    /// 맵마다 프리팹 오버라이드가 생기지 않는다(맵에 다른 방향광은 두지 않음, 맵 검사가 알림).
    /// 태양은 Mixed(Baked Indirect)라 조명 굽기(<c>MapLightBaker</c>)에서 빛 번짐만 프로브 볼륨에 굽고 직접 빛·그림자는 실시간이다.
    /// 씬에 저장해 두어야 굽기가 태양의 굽기 기록을 남길 수 있다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MapEnvironment : MonoBehaviour
    {
        /// <summary>맵 씬에 저장되는 태양 오브젝트 이름(씬 맨 위, 숨김).</summary>
        public const string SunName = "Sun (맵 환경)";

        [Tooltip("맵 환경 설정(Assets/Data/Map/MapEnvironmentSettings). 이 맵만 다르게 하려면 다른 애셋을 넣는다.")]
        [SerializeField] MapEnvironmentSettings settings;

        Light sun;

        void OnEnable()
        {
            MapEnvironmentSettings.Changed += Apply;
            ReflectionSettings.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            MapEnvironmentSettings.Changed -= Apply;
            ReflectionSettings.Changed -= Apply;
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
            var reflection = settings.reflection;
            ReflectionSettings.Current = reflection;
            if (reflection != null)
            {
                RenderSettings.defaultReflectionMode = reflection.skyMode;
                RenderSettings.customReflectionTexture = reflection.skyCubemap;
                RenderSettings.reflectionIntensity = reflection.skyIntensity;
            }
            RenderSettings.fog = settings.fog;
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogMode = settings.fogMode;
            RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.fogStartDistance = settings.fogStart;
            RenderSettings.fogEndDistance = settings.fogEnd;

            if (sun == null) sun = FindOrCreateSun();
            sun.color = settings.sunColor;
            sun.intensity = settings.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(settings.sunAngles);
            sun.shadows = settings.sunShadows;
            sun.shadowStrength = settings.sunShadowStrength;
            sun.bounceIntensity = settings.sunBounce;
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        // 씬에 저장된 태양을 찾고, 없으면 만든다(에디터에서 만들면 씬을 바뀐 것으로 표시해 다음 저장에 들어가게 함, 여는 중에는 표시할 수 없어 한 틱 뒤).
        Light FindOrCreateSun()
        {
            // 씬을 여는 중(OnEnable)에는 씬의 맨 위 오브젝트 목록을 못 읽으므로 모든 광원에서 찾는다.
            var scene = gameObject.scene;
            foreach (var found in Resources.FindObjectsOfTypeAll<Light>())
                if (found.gameObject.scene == scene && found.name == SunName && found.transform.parent == null) return found;

            var go = new GameObject(SunName) { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
#if UNITY_EDITOR
            light.lightmapBakeType = LightmapBakeType.Mixed;
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (scene.IsValid() && scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                };
#endif
            return light;
        }
    }
}
