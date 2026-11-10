using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 새 맵 만들기(메뉴 Slime ~ColorMaker~/새 맵 만들기, 2026-11-08). 맵 id·화면 이름·씬 이름을 받아 한 번에 만든다:
    /// 빈 씬에 맵 기본 프리팹(MapRoot, 원점의 시작 지점·맵 환경 포함)과 큐브 격자를 넣고, 시작 칸 아래 (0,−1,0)에 블록 하나를 깔고,
    /// Scenes/Maps에 저장해 맵 목록(맨 뒤, 버전 1)과 Build Settings에 넣은 뒤 큐브 맵 편집 창을 연다. 목적지는 직접 놓는다(맵 검사가 알림).
    /// </summary>
    public class NewMapWindow : EditorWindow
    {
        const string MapsFolder = "Assets/Scenes/Maps";

        string mapId = "";
        string displayName = "";
        string sceneName = "";

        [MenuItem("Slime ~ColorMaker~/새 맵 만들기")]
        static void Open()
        {
            var window = GetWindow<NewMapWindow>(true, "새 맵 만들기");
            window.minSize = new Vector2(360f, 170f);
        }

        void OnGUI()
        {
            mapId = EditorGUILayout.TextField(new GUIContent("맵 id", "저장 파일이 맵을 찾는 이름(예: map_01). 맵 목록 안에서 겹치지 않아야 한다."), mapId).Trim();
            displayName = EditorGUILayout.TextField(new GUIContent("화면 이름", "맵 선택 화면에 보이는 이름"), displayName);
            sceneName = EditorGUILayout.TextField(new GUIContent("씬 이름", "Scenes/Maps에 저장할 씬 파일 이름(예: Map01)"), sceneName).Trim();

            string problem = Problem();
            EditorGUILayout.HelpBox(problem ?? $"{MapsFolder}/{sceneName}.unity를 만들고 맵 목록 맨 뒤와 Build Settings에 넣는다. 시작 지점은 원점, 바닥은 시작 칸 하나.",
                problem == null ? MessageType.None : MessageType.Warning);
            using (new EditorGUI.DisabledScope(problem != null))
                if (GUILayout.Button("만들기")) Create();
        }

        string Problem()
        {
            if (mapId.Length == 0 || sceneName.Length == 0 || displayName.Trim().Length == 0) return "세 칸을 모두 채운다.";
            if (sceneName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "씬 이름에 파일 이름에 못 쓰는 글자가 있다.";
            var list = FindMapList();
            if (list == null) return "맵 목록 애셋(MapList)이 없다.";
            if (list.FindById(mapId) != null) return $"맵 id '{mapId}'는 이미 맵 목록에 있다.";
            if (list.FindByScene(sceneName) != null || File.Exists($"{MapsFolder}/{sceneName}.unity")) return $"씬 '{sceneName}'이 이미 있다.";
            var config = CubeGridEditing.FindConfig();
            if (config == null || config.mapRootPrefab == null || config.blockPrefab == null) return "큐브 맵 설정에 MapRoot 프리팹이나 블록 프리팹이 없다.";
            return null;
        }

        void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var config = CubeGridEditing.FindConfig();
            string path = $"{MapsFolder}/{sceneName}.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(config.mapRootPrefab, scene);
            var grid = CubeGridEditing.CreateGrid();
            CubeGridRules.AddBlock(grid.Data, new Vector3Int(0, -1, 0));
            CubeGridBuilder.Build(grid, config);
            EditorSceneManager.SaveScene(scene, path);

            var list = FindMapList();
            list.maps.Add(new MapListEntry { mapId = mapId, sceneName = sceneName, displayName = displayName.Trim(), version = 1 });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();

            Debug.Log($"[새 맵] {path}를 만들어 맵 목록과 Build Settings에 넣었다. 목적지를 놓고 Slime ~ColorMaker~/맵 검사로 확인한다.", AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            GetWindow<CubeGridWindow>("큐브 맵 편집");
            Close();
        }

        static MapList FindMapList()
        {
            var guids = AssetDatabase.FindAssets("t:MapList");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<MapList>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
