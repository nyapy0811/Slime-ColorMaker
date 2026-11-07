using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorMaze
{
    /// <summary>
    /// MagicaVoxel <c>.vox</c>를 프리팹 겉모습으로 가져온다(약속은 Docs/v2 복셀 모델링 규칙 1~5절). 오브젝트 하나가 자식 하나이고,
    /// 값 색 칸(33~40, 밝기 8단계)은 자식 아래 별도 렌더러 하나(ValueColor)로 떼어낸다. 그 렌더러의 칸은 자기 색의 가장 큰 채널을
    /// 밝기로 남긴 회색이라, 기물 컴포넌트가 렌더러의 _BaseColor를 바꾸면 그 색과 곱해진다(값 색 안의 음영).
    /// 반투명 칸(규칙 2절, 2026-11-07)도 같은 방식으로 떼어낸다: 반투명 값 색 41~48은 GlassValueColor(칠함), 반투명 회색 49~56은 Glass.
    /// 칠할 색과 반투명 정도는 맨 위 오브젝트에 붙이는 <see cref="VoxPaint"/>가 렌더러마다 준다(놓인 모델마다 바꿀 수 있음). 반투명 부분은 바깥 껍질만 만들고,
    /// 렌더러 하나에 재질 둘을 걸어 뒷면(먼 쪽 벽의 안쪽)을 먼저, 앞면을 나중에 그린다(유리 상자처럼 반대편 벽이 비침).
    /// 나머지 칸은 파일의 팔레트를 256 × 1 텍스처로 만들어 면마다 자기 색 칸을 가리키게 한다(URP Lit은 버텍스 색을 읽지 않음).
    /// </summary>
    [ScriptedImporter(6, "vox")]
    public class VoxImporter : ScriptedImporter
    {
        public enum PivotMode { BottomBackEdge, BottomCenter, TopCenter }

        /// <summary>한 칸의 길이(유닛). 1유닛 = 16칸(블록 한 칸이 16³, 2026-11-07).</summary>
        public const float VoxelSize = 0.0625f;

        /// <summary>값 색 칸: 33(가장 밝음) ~ 40(가장 어두움).</summary>
        public const byte FirstValueColor = 33;
        public const byte LastValueColor = 40;

        /// <summary>반투명 값 색 칸 41~48(밝기 8단계, 칠함)과 반투명 회색 칸 49~56(칠하지 않음).</summary>
        public const byte FirstGlassValueColor = 41;
        public const byte LastGlassValueColor = 48;
        public const byte FirstGlassGray = 49;
        public const byte LastGlassGray = 56;

        const string ValuePartName = VoxPaint.ValuePartName;
        const string GlassPartName = VoxPaint.GlassPartName;
        const string GlassValuePartName = VoxPaint.GlassValuePartName;

        const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

        [Tooltip("기준점. 모든 오브젝트의 모델 크기 상자를 합친 상자에서 고른다(규칙 5절): 바닥면 뒤쪽 가장자리의 가운데(기물 기준, 기본), " +
                 "바닥면 가운데(기물·블록·계단 기본, 칸 바닥 가운데에 놓음), 바닥면 뒤쪽 가장자리의 가운데(블록 옆면에 붙는 기물), " +
                 "윗면 가운데(위에 붙는 것). 자식은 자기 상자에서 같은 쪽을 기준점으로 쓴다.")]
        public PivotMode pivot = PivotMode.BottomCenter;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var file = VoxFile.Read(File.ReadAllBytes(ctx.assetPath));
            Warn(ctx, file);

            var palette = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = "Palette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            palette.SetPixels32(file.palette);
            palette.Apply();
            ctx.AddObjectToAsset("palette", palette);
            // 셰이더를 의존으로 알려 셰이더가 먼저 가져와지게 한다(전체를 다시 가져올 때 Shader.Find가 null을 주던 원인).
            ctx.DependsOnArtifact(LitShaderPath);
            var lit = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            var valuePalette = ValuePalette(file.palette);
            ctx.AddObjectToAsset("valuePalette", valuePalette);
            var glassPalette = GlassPalette(file.palette);
            ctx.AddObjectToAsset("glassPalette", glassPalette);
            var voxelMaterial = NewMaterial("Voxel", lit, palette);
            var paintMaterial = NewMaterial("Paint", lit, valuePalette);
            var glassMaterials = new[]
            {
                MakeTransparent(NewMaterial("GlassBack", lit, glassPalette), backFaces: true),
                MakeTransparent(NewMaterial("Glass", lit, glassPalette), backFaces: false),
            };
            var glassPaintMaterials = new[]
            {
                MakeTransparent(NewMaterial("GlassPaintBack", lit, glassPalette), backFaces: true),
                MakeTransparent(NewMaterial("GlassPaint", lit, glassPalette), backFaces: false),
            };
            ctx.AddObjectToAsset("material:Voxel", voxelMaterial);
            ctx.AddObjectToAsset("material:Paint", paintMaterial);
            foreach (var material in glassMaterials.Concat(glassPaintMaterials)) ctx.AddObjectToAsset("material:" + material.name, material);
            var parts = new (string name, Func<byte, bool> include, Material[] materials)[]
            {
                (ValuePartName, IsValueColor, new[] { paintMaterial }),
                (GlassPartName, IsGlassGray, glassMaterials),
                (GlassValuePartName, IsGlassValueColor, glassPaintMaterials),
            };

            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            var (min, max) = file.Bounds();
            var rootPivot = Pivot(min, max);
            var names = new HashSet<string>();
            for (int i = 0; i < file.objects.Count; i++)
            {
                var obj = file.objects[i];
                // 다시 가져와도 프리팹의 참조가 이어지게 이름(= 하위 애셋 id)이 겹치지 않게 한다.
                string name = obj.name ?? $"Object {i + 1}";
                for (int n = 2; !names.Add(name); n++) name = $"{obj.name ?? $"Object {i + 1}"} ({n})";

                var childPivot = Pivot(obj.Min, obj.Max);
                var child = new GameObject(name);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = (childPivot - rootPivot) * VoxelSize;

                var grid = VoxMesher.Grid(obj.model);
                var offset = ToUnity(obj.Min) - childPivot;
                var plain = VoxMesher.Quads(grid, color => !IsValueColor(color) && !IsGlass(color), IsGlass);
                if (plain.Count > 0) Attach(ctx, child, name, BuildMesh(name, plain, offset), voxelMaterial);
                foreach (var (partName, include, materials) in parts)
                {
                    var quads = VoxMesher.Quads(grid, include, IsGlass);
                    if (quads.Count == 0) continue;
                    var part = new GameObject(partName);
                    part.transform.SetParent(child.transform, false);
                    Attach(ctx, part, $"{name}/{partName}", BuildMesh(partName, quads, offset), materials);
                }
            }
            root.AddComponent<VoxPaint>();
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }

        /// <summary>상자(MagicaVoxel 칸 좌표)의 기준점을 Unity 축 칸 좌표로 준다. 뒤쪽은 MagicaVoxel −Y(Unity −Z, 앞면의 반대쪽)다.</summary>
        Vector3 Pivot(Vector3Int min, Vector3Int max)
        {
            float x = (min.x + max.x) / 2f;
            float depth = (min.y + max.y) / 2f;
            return pivot switch
            {
                PivotMode.BottomBackEdge => new Vector3(x, min.z, min.y),
                PivotMode.TopCenter => new Vector3(x, max.z, depth),
                _ => new Vector3(x, min.z, depth),
            };
        }

        static Vector3 ToUnity(Vector3Int position) => new(position.x, position.z, position.y);

        public static bool IsValueColor(byte color) => color >= FirstValueColor && color <= LastValueColor;

        public static bool IsGlassValueColor(byte color) => color >= FirstGlassValueColor && color <= LastGlassValueColor;

        public static bool IsGlassGray(byte color) => color >= FirstGlassGray && color <= LastGlassGray;

        public static bool IsGlass(byte color) => IsGlassValueColor(color) || IsGlassGray(color);

        /// <summary>
        /// 반투명 렌더러 둘이 같이 쓰는 텍스처: 반투명 값 색 칸은 가장 큰 채널을 밝기로 남긴 회색, 반투명 회색 칸은 그 색 그대로다.
        /// 불투명도는 텍스처가 아니라 _BaseColor의 알파(재질 기본값 <see cref="VoxPaint.DefaultOpacity"/>, 모델마다 VoxPaint)다.
        /// </summary>
        static Texture2D GlassPalette(Color32[] colors)
        {
            var pixels = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                var c = colors[i];
                byte level = IsGlassValueColor((byte)i) ? Max(c) : (byte)255;
                pixels[i] = IsGlassGray((byte)i) ? new Color32(c.r, c.g, c.b, 255) : new Color32(level, level, level, 255);
            }
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = "GlassPalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// URP Lit을 반투명(알파 섞기, 깊이 쓰기 없음)으로 바꾼다. 셰이더 GUI(BaseShaderGUI)가 하는 설정을 직접 한다:
        /// URP의 알파 섞기는 셰이더에서 알파를 미리 곱하고(_ALPHAPREMULTIPLY_ON) One / OneMinusSrcAlpha로 섞는다.
        /// <paramref name="backFaces"/>면 뒷면만 그리고 한 칸 먼저 그린다(큐 2999 = 반투명 − 1), 아니면 앞면만(큐 3000).
        /// </summary>
        static Material MakeTransparent(Material material, bool backFaces)
        {
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, VoxPaint.DefaultOpacity));
            material.SetFloat("_Cull", (float)(backFaces ? CullMode.Front : CullMode.Back));
            material.SetFloat("_QueueOffset", backFaces ? -1f : 0f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent + (backFaces ? -1 : 0);
            return material;
        }

        /// <summary>값 색 렌더러의 텍스처: 값 색 칸마다 그 칸 색의 가장 큰 채널을 밝기로 남긴 회색(나머지 칸은 쓰지 않아 흰색).</summary>
        static Texture2D ValuePalette(Color32[] colors)
        {
            var pixels = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                byte level = IsValueColor((byte)i) ? Max(colors[i]) : (byte)255;
                pixels[i] = new Color32(level, level, level, 255);
            }
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = "ValuePalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static byte Max(Color32 color) => (byte)Mathf.Max(color.r, Mathf.Max(color.g, color.b));

        static void Attach(AssetImportContext ctx, GameObject target, string id, Mesh mesh, Material material) =>
            Attach(ctx, target, id, mesh, new[] { material });

        // 재질이 둘 이상이면 메시(서브메시 하나)를 재질마다 한 번씩 그린다(반투명의 뒷면 → 앞면).
        static void Attach(AssetImportContext ctx, GameObject target, string id, Mesh mesh, Material[] materials)
        {
            ctx.AddObjectToAsset("mesh:" + id, mesh);
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            target.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        /// <summary>사각형을 메시로 만든다. offset(칸)만큼 옮겨 칸 크기를 곱하고, UV는 팔레트 텍스처의 자기 색 칸 가운데를 가리킨다.</summary>
        static Mesh BuildMesh(string name, List<VoxMesher.Quad> quads, Vector3 offset)
        {
            var vertices = new List<Vector3>(quads.Count * 4);
            var normals = new List<Vector3>(quads.Count * 4);
            var uvs = new List<Vector2>(quads.Count * 4);
            var triangles = new List<int>(quads.Count * 6);
            foreach (var quad in quads)
            {
                int start = vertices.Count;
                Vector3 origin = quad.origin + offset;
                vertices.Add(origin * VoxelSize);
                vertices.Add((origin + quad.du) * VoxelSize);
                vertices.Add((origin + quad.du + quad.dv) * VoxelSize);
                vertices.Add((origin + quad.dv) * VoxelSize);
                var uv = new Vector2((quad.color + 0.5f) / 256f, 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(quad.normal);
                    uvs.Add(uv);
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>
        /// 파일 안 모든 렌더러가 같이 쓰는 재질. 칠하기와 상호작용 강조는 기물 컴포넌트가 MaterialPropertyBlock으로 렌더러마다 바꾸므로
        /// 발광을 켜 두고(검정) 나머지는 임시 회색 재질(Temp)과 맞춘다.
        /// </summary>
        static Material NewMaterial(string name, Shader shader, Texture2D baseMap)
        {
            var material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", baseMap);
            material.SetFloat("_Smoothness", 0.2f);
            material.SetColor("_EmissionColor", Color.black);
            // 발광 색이 검정이어도 키워드가 남게 한다(EmissiveIsBlack이면 URP가 _EMISSION을 끈다).
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
            return material;
        }

        /// <summary>규칙 2·7절: 값 색 칸(33~40, 반투명 41~48)이 아닌 칸은 회색(R = G = B)만 쓴다. 규칙 5절: 오브젝트 회전은 쓰지 않는다.</summary>
        static void Warn(AssetImportContext ctx, VoxFile file)
        {
            if (file.hasRotation) ctx.LogImportWarning("[VoxImporter] 오브젝트 회전(_r)이 있지만 읽지 않는다(회전 없이 놓임).");
            var used = file.models.SelectMany(model => model.voxels).Select(voxel => voxel.color).Distinct().OrderBy(color => color).ToList();
            var colored = used.Where(color => !IsValueColor(color) && !IsGlassValueColor(color) && !IsGray(file.palette[color])).ToList();
            if (colored.Count > 0) ctx.LogImportWarning($"[VoxImporter] 값 색 칸(33~48)이 아닌데 회색이 아닌 칸을 썼다: 색 번호 {string.Join(", ", colored)}");
        }

        static bool IsGray(Color32 color) => color.r == color.g && color.g == color.b;
    }
}
