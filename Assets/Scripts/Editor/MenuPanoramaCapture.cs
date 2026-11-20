using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 메뉴 'Slime~ColorMaker~/메뉴 배경 찍기': 열린 맵 씬에서 씬 뷰 카메라 자리(Play 중이면 게임 카메라 자리)에서 360°를 찍어 메인 메뉴 배경 파노라마로 저장한다.
    /// 화면 위 UI(HUD 등 오버레이 캔버스)는 카메라가 그리지 않으므로 들어가지 않는다.
    /// 맵 카메라(씬의 Main Camera) 설정을 본뜬 임시 카메라로 큐브맵을 그리고 가로·세로 2:1 파노라마 PNG(<see cref="PanoramaPath"/>)로 펴서
    /// 큐브맵으로 가져온 뒤 설정 애셋(<see cref="MenuPanoramaSettings"/>)에 넣는다(찍은 씬·자리도 적음). 후처리는 들어가지 않는다.
    /// </summary>
    public static class MenuPanoramaCapture
    {
        public const string PanoramaPath = "Assets/Textures/MenuPanorama.png";

        [MenuItem("Slime~ColorMaker~/메뉴 배경 찍기")]
        static void Capture()
        {
            var settings = FindSettings();
            if (settings == null)
            {
                Debug.LogError("[메뉴 배경 찍기] 메뉴 배경 파노라마 설정 애셋(MenuPanoramaSettings)이 없다. Assets/Data/Camera에 만든다.");
                return;
            }
            // Play 중이면 게임 카메라 자리(Play 때만 켜지는 면 숨기기·방벽 모서리·발광 광원·캐릭터가 들어감), 아니면 씬 뷰 자리.
            var template = Camera.main;
            Vector3 position;
            UnityEngine.SceneManagement.Scene scene;
            if (Application.isPlaying && template != null)
            {
                position = template.transform.position;
                scene = template.gameObject.scene;
            }
            else
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    Debug.LogError("[메뉴 배경 찍기] 씬 뷰가 없다. 맵 씬을 열고 씬 뷰를 찍을 자리로 옮긴 뒤 다시 누른다(Play 중이면 게임 카메라 자리에서 찍음).");
                    return;
                }
                position = sceneView.camera.transform.position;
                scene = EditorSceneManager.GetActiveScene();
            }

            var pixels = Render(position, template, settings.captureWidth);
            Directory.CreateDirectory(Path.GetDirectoryName(PanoramaPath));
            File.WriteAllBytes(PanoramaPath, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(PanoramaPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PanoramaPath);
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Mathf.Max(1024, Mathf.NextPowerOfTwo(settings.captureWidth));
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Undo.RecordObject(settings, "메뉴 배경 찍기");
            settings.panorama = AssetDatabase.LoadAssetAtPath<Cubemap>(PanoramaPath);
            settings.sourceScene = scene.path;
            settings.capturePosition = position;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"[메뉴 배경 찍기] {scene.name}의 {position}에서 찍어 {PanoramaPath}에 저장했다.", settings);
        }

        /// <summary><paramref name="position"/>에서 360°를 찍어 가로 <paramref name="width"/> × 세로 절반의 파노라마(sRGB)로 돌려준다.</summary>
        public static Texture2D Render(Vector3 position, Camera template, int width)
        {
            width = Mathf.Max(256, width / 4 * 4);
            var holder = new GameObject("Panorama Capture") { hideFlags = HideFlags.HideAndDontSave };
            var cube = new RenderTexture(width / 4, width / 4, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { dimension = UnityEngine.Rendering.TextureDimension.Cube };
            var flat = new RenderTexture(width, width / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                var camera = holder.AddComponent<Camera>();
                if (template != null) camera.CopyFrom(template);
                camera.enabled = false;
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(position, Quaternion.identity);
                camera.RenderToCubemap(cube, 63);
                cube.ConvertToEquirect(flat, Camera.MonoOrStereoscopicEye.Mono);

                RenderTexture.active = flat;
                var pixels = new Texture2D(flat.width, flat.height, TextureFormat.RGBA32, false, false);
                pixels.ReadPixels(new Rect(0, 0, flat.width, flat.height), 0, 0);
                pixels.Apply();
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(holder);
                cube.Release();
                flat.Release();
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(flat);
            }
        }

        static MenuPanoramaSettings FindSettings()
        {
            var guids = AssetDatabase.FindAssets("t:MenuPanoramaSettings");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<MenuPanoramaSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
