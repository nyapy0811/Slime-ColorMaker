using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 조명 굽기. 태양·환경 조명의 빛 번짐을 맵마다 프로브 볼륨(APV, MapRoot의 'Probe Volume')에 굽는다.
    /// 블록 모델에 라이트맵 UV가 없고 Play 때 블록 메시를 바꿔 끼우므로(<see cref="CubeFaceCuller"/>) 라이트맵 대신 탐침을 쓴다.
    /// - 맵 씬을 저장할 때: 조명 지문(굽기에 들어가는 것: static 렌더러, 구울 광원, 환경 조명, 맵 환경 설정, 굽기 설정)이 구운 때와 다르면
    ///   백그라운드에서 굽는다. 굽는 중에 바꿔 저장하면 처음부터 다시 굽는다.
    /// - 빌드 전: 맵 목록의 맵 가운데 지난 맵을 열어 굽고 저장한다.
    /// - 메뉴: 조명 굽기(지금 맵), 모든 맵 조명 굽기(지난 맵만).
    /// 굽기 전에 ② 실내 반사 탐침(<see cref="IndoorReflectionProbes"/>)을 맞추고 함께 굽는다.
    /// 굽기 결과(굽기 묶음, 탐침 데이터, LightingData)와 지문 파일은 씬 옆 씬 이름 폴더에 있다. 굽기 설정은 모든 맵이 <see cref="LightingSettingsPath"/> 하나를 쓴다.
    /// </summary>
    [InitializeOnLoad]
    public static class MapLightBaker
    {
        const string LightingSettingsPath = "Assets/Data/Map/MapLighting.lighting";
        const string FingerprintFile = "LightingFingerprint.txt";

        static bool batch; // 모든 맵 굽기(빌드 전 포함) 중: 저장할 때 굽기를 하지 않는다.
        static readonly Dictionary<string, string> baking = new(); // 굽는 중인 맵 씬 경로 → 굽기를 시작할 때의 지문

        static MapLightBaker()
        {
            EditorSceneManager.sceneSaving += (scene, _) =>
            {
                if (MapValidator.IsMapScene(scene)) Prepare(scene);
            };
            EditorSceneManager.sceneSaved += OnSceneSaved;
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }

        [MenuItem("Slime~ColorMaker~/조명 굽기")]
        static void BakeActiveMap() => BakeMap(SceneManager.GetActiveScene());

        /// <summary>맵을 지문과 상관없이 백그라운드로 굽는다(굽는 중이면 처음부터). 활성 맵 씬만.</summary>
        public static void BakeMap(Scene scene)
        {
            if (Application.isPlaying || scene != SceneManager.GetActiveScene() || !MapValidator.IsMapScene(scene))
            {
                Debug.LogWarning("[조명 굽기] Play 모드가 아닐 때 맵 씬을 열고 굽는다.");
                return;
            }
            Prepare(scene);
            StartBake();
        }

        [MenuItem("Slime~ColorMaker~/모든 맵 조명 굽기 (바뀐 맵만)")]
        static void BakeAllMaps()
        {
            if (Application.isPlaying) return;
            int failed = BakeStaleMaps();
            if (failed == 0) Debug.Log("[조명 굽기] 모든 맵의 조명이 최신이다.");
        }

        /// <summary>
        /// 활성 씬(맵)의 조명이 지난 것인가: 구운 적이 없거나, 구운 뒤 지문이 바뀌었다. 활성 씬만 볼 수 있다(환경 조명·LightingData가 활성 씬 것).
        /// </summary>
        public static bool IsStale(Scene scene)
        {
            if (scene != SceneManager.GetActiveScene() || Lightmapping.lightingDataAsset == null) return true;
            var set = FindBakingSet(scene);
            if (set == null || !set.HasBakedData()) return true;
            var path = Path.Combine(DataFolder(scene), FingerprintFile);
            return !File.Exists(path) || File.ReadAllText(path) != Fingerprint(scene);
        }

        /// <summary>맵 목록의 맵 가운데 조명이 지난 맵을 열어 굽고 저장한다. 굽지 못한 맵 수를 돌려준다. 끝나면 열려 있던 씬으로 돌아간다.</summary>
        public static int BakeStaleMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogError("[조명 굽기] 저장하지 않은 씬이 있어 모든 맵 굽기를 하지 않았다.");
                return 1;
            }
            if (Lightmapping.isRunning) Lightmapping.Cancel();

            var setup = EditorSceneManager.GetSceneManagerSetup().Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
            int failed = 0;
            batch = true;
            try
            {
                foreach (var path in MapScenePaths())
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (!MapValidator.IsMapScene(scene)) continue;
                    Prepare(scene);
                    if (IsStale(scene))
                    {
                        EditorSceneManager.SaveScene(scene); // 굽기 전에 저장: 열 때 새로 만든 태양이 씬에 있어야 굽기가 태양의 굽기 기록을 남긴다.
                        if (!Lightmapping.Bake())
                        {
                            Debug.LogError($"[조명 굽기] {scene.name}: 굽기에 실패했다.");
                            failed++;
                            continue;
                        }
                    }
                    if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                batch = false;
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            return failed;
        }

        static void OnSceneSaved(Scene scene)
        {
            if (batch || Application.isPlaying || scene != SceneManager.GetActiveScene() || !MapValidator.IsMapScene(scene)) return;
            if (!IsStale(scene)) return;
            if (Lightmapping.isRunning && baking.TryGetValue(scene.path, out var fingerprint) && fingerprint == Fingerprint(scene)) return; // 같은 것을 굽는 중
            StartBake();
        }

        // 백그라운드 굽기. 굽는 중이면 멈추고 다음 틱에 처음부터 다시 굽는다(여러 번 불러도 한 번).
        // delayCall이 아니라 update를 쓴다: delayCall은 에디터 창이 뒤에 있으면 돌지 않는다.
        static void StartBake()
        {
            if (Lightmapping.isRunning) Lightmapping.Cancel();
            EditorApplication.update -= BakeNextTick;
            EditorApplication.update += BakeNextTick;
        }

        static void BakeNextTick()
        {
            EditorApplication.update -= BakeNextTick;
            if (!Application.isPlaying && !Lightmapping.isRunning) Lightmapping.BakeAsync();
        }

        static void OnBakeStarted()
        {
            baking.Clear();
            var scene = SceneManager.GetActiveScene();
            if (MapValidator.IsMapScene(scene)) baking[scene.path] = Fingerprint(scene);
        }

        // 다 구웠을 때만 온다(멈추면 오지 않음): 시작할 때의 지문을 남긴다.
        static void OnBakeCompleted()
        {
            foreach (var (scenePath, fingerprint) in baking)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid()) continue;
                File.WriteAllText(Path.Combine(DataFolder(scene, true), FingerprintFile), fingerprint);
                Debug.Log($"[조명 굽기] {scene.name}: 다 구웠다.");
            }
            baking.Clear();
            AssetDatabase.Refresh();
        }

        // 맵을 굽기 전에 맞춘다: 공용 굽기 설정, 이 맵의 굽기 묶음(탐침 간격은 맵 환경 설정 값).
        static void Prepare(Scene scene)
        {
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (lighting == null) Debug.LogError($"[조명 굽기] 굽기 설정({LightingSettingsPath})이 없다.");
            else if (Lightmapping.GetLightingSettingsForScene(scene) != lighting) Lightmapping.SetLightingSettingsForScene(scene, lighting);

            var set = FindBakingSet(scene);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
                // 조명 창이 새 굽기 묶음을 만들 때와 같이 기본값을 넣는다(공개 API가 없음).
                typeof(ProbeVolumeBakingSet).GetMethod("SetDefaults", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(set, null);
                set.name = scene.name + " Baking Set";
                AssetDatabase.CreateAsset(set, $"{DataFolder(scene, true)}/{set.name}.asset");
                set.TryAddScene(AssetDatabase.AssetPathToGUID(scene.path));
            }
            var settings = EnvironmentSettings(scene);
            float spacing = settings != null ? settings.probeSpacing : 1f;
            if (set.minDistanceBetweenProbes != spacing)
            {
                set.minDistanceBetweenProbes = spacing;
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssetIfDirty(set);

            IndoorReflectionProbes.Update(scene, settings != null ? settings.reflection : null);
        }

        // 조명 지문: 굽기 결과를 바꾸는 것만 모은다. 순서에 상관없게 줄을 정렬한다.
        static string Fingerprint(Scene scene)
        {
            var lines = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || !GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;
                    var mesh = renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                    lines.Add($"renderer {MatrixKey(renderer.localToWorldMatrix)} {AssetKey(mesh)} {string.Join(",", renderer.sharedMaterials.Select(AssetKey))}");
                }
                foreach (var light in root.GetComponentsInChildren<Light>())
                {
                    if (!light.enabled || light.lightmapBakeType == LightmapBakeType.Realtime) continue;
                    lines.Add($"light {light.type} {light.color} {light.intensity} {light.bounceIntensity} {light.range} {light.spotAngle} {MatrixKey(light.transform.localToWorldMatrix)}");
                }
            }
            lines.Add($"environment {AssetKey(EnvironmentSettings(scene))} {AssetKey(RenderSettings.skybox)}");
            lines.Add($"lighting {AssetKey(Lightmapping.GetLightingSettingsForScene(scene))}");
            var environment = EnvironmentSettings(scene);
            lines.AddRange(IndoorReflectionProbes.FingerprintLines(scene, environment != null ? environment.reflection : null));
            lines.Sort(StringComparer.Ordinal);
            return Hash128.Compute(string.Join("\n", lines)).ToString();
        }

        static string MatrixKey(Matrix4x4 m) => string.Join(",", Enumerable.Range(0, 16).Select(i => m[i].ToString("F4")));

        // 애셋: 경로 GUID, 애셋 안 id, 내용 해시(애셋을 고치면 바뀜).
        static string AssetKey(Object asset)
        {
            if (asset == null) return "-";
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)) return asset.name;
            return $"{guid}:{localId}:{AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(guid))}";
        }

        static MapEnvironmentSettings EnvironmentSettings(Scene scene)
        {
            var environment = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<MapEnvironment>(true)).FirstOrDefault(e => e != null);
            return environment != null ? new SerializedObject(environment).FindProperty("settings").objectReferenceValue as MapEnvironmentSettings : null;
        }

        static ProbeVolumeBakingSet FindBakingSet(Scene scene)
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            return AssetDatabase.FindAssets("t:" + nameof(ProbeVolumeBakingSet))
                .Select(guid => AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(set => set != null && set.sceneGUIDs.Contains(sceneGuid));
        }

        // 씬 옆 씬 이름 폴더(굽기 결과가 들어가는 곳, 조명 창과 같음). create면 없을 때 만든다.
        static string DataFolder(Scene scene, bool create = false)
        {
            var parent = Path.GetDirectoryName(scene.path)!.Replace(Path.DirectorySeparatorChar, '/');
            var folder = $"{parent}/{scene.name}";
            if (create && !AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, scene.name);
            return folder;
        }

        // 맵 목록에 있고 Build Settings에서 켜진 맵 씬.
        static IEnumerable<string> MapScenePaths()
        {
            var names = AssetDatabase.FindAssets("t:" + nameof(MapList))
                .Select(guid => AssetDatabase.LoadAssetAtPath<MapList>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(list => list != null)
                .SelectMany(list => list.maps.Select(entry => entry.sceneName))
                .ToHashSet();
            return EditorBuildSettings.scenes.Where(s => s.enabled && names.Contains(Path.GetFileNameWithoutExtension(s.path))).Select(s => s.path);
        }
    }

    /// <summary>빌드 전에 조명이 지난 맵을 굽는다. 굽지 못한 맵이 있으면 빌드를 멈춘다.</summary>
    class MapLightBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (MapLightBaker.BakeStaleMaps() > 0) throw new BuildFailedException("[조명 굽기] 조명을 굽지 못한 맵이 있어 빌드를 멈췄다(콘솔 참고).");
        }
    }
}
