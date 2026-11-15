using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeColorMaker
{
    /// <summary>
    /// Blockbench <c>.bbmodel</c>을 프리팹 겉모습으로 가져온다(약속은 Docs/v2 블록벤치 모델링 규칙).
    /// 부품 그룹(태그가 아닌 이름)은 자식 오브젝트(원점 = 그룹 기준점), 부위 태그 그룹(Gray·Color·Glass·GrayGlass·Light)은 그 안 큐브의 부위다.
    /// 텍스처는 휘도 Y(BT.601) 회색으로 바꿔 한 장(아틀라스)에 모으고, 부위마다 렌더러 하나로 나눈다.
    /// 렌더러 이름은 <see cref="VoxPaint"/>가 칠할 때 찾는 이름이다: Gray = 부품 오브젝트 자신, Color = ValueColor,
    /// Glass = GlassValueColor, GrayGlass = Glass, Light = Light. 큐브끼리 맞닿아 완전히 덮인 면은 버린다(반투명은 바깥 껍질만).
    /// 좌표는 Blockbench (X, Y, Z) → Unity (X, Y, −Z)이고 그룹·큐브 회전은 꼭짓점에 굽는다(자식 오브젝트는 회전 없음).
    /// </summary>
    [ScriptedImporter(1, "bbmodel")]
    public class BBModelImporter : ScriptedImporter
    {
        const float PixelSize = 1f / 16f;
        const float Epsilon = 1e-4f;

        const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

        // 반투명 재질의 셰이더: Lit 사본에 깊이 당기기(Offset)를 더해, 다른 면과 같은 평면에 붙어도 깊이 다툼 없이 앞에 그린다.
        const string GlassShaderPath = "Assets/Shaders/LitGlass.shader";

        enum Part { Gray, Color, Glass, GrayGlass, Light }

        static readonly Dictionary<string, Part> Tags = new()
        {
            ["gray"] = Part.Gray,
            ["color"] = Part.Color,
            ["glass"] = Part.Glass,
            ["grayglass"] = Part.GrayGlass,
            ["light"] = Part.Light,
        };

        static string RendererName(Part part) => part switch
        {
            Part.Color => VoxPaint.ValuePartName,
            Part.Glass => VoxPaint.GlassValuePartName,
            Part.GrayGlass => VoxPaint.GlassPartName,
            Part.Light => VoxPaint.LightPartName,
            _ => null, // Gray는 부품 오브젝트 자신
        };

        static bool IsGlass(Part part) => part is Part.Glass or Part.GrayGlass;

        public enum PivotMode { BottomCenter, BottomBackEdge, TopCenter, Origin }

        [Tooltip("기준점(규칙 5절). 모든 큐브를 합친 상자의 바닥면 가운데(블록·계단·칸 바닥에 서는 기물, 기본), " +
                 "바닥면 뒤쪽 가운데(블록 옆면에 붙는 기물, 뒤 = Blockbench 남쪽), 윗면 가운데, 또는 Blockbench 원점 그대로(.vox에서 변환한 모델).")]
        public PivotMode pivot = PivotMode.BottomCenter;

        [Tooltip("켜면 회전하지 않은 큐브마다 상자 콜라이더를 만든다(계단처럼 모델 모양 그대로 부딪히는 것).")]
        public bool boxColliders;

        class Quad
        {
            public BBModelFile.Cube cube;
            public Part part;
            public Transform owner;
            public readonly Vector3[] vertices = new Vector3[4]; // Unity 픽셀 좌표(기준점 전)
            public readonly Vector2[] uvs = new Vector2[4];
            public Vector3 normal;
            public bool axisAligned;
            public int axis, sign; // 축에 맞는 면: 법선 축(0·1·2)과 방향
            public float plane;
            public Vector2 rectMin, rectMax;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var file = BBModelFile.Parse(File.ReadAllText(ctx.assetPath));
            foreach (string warning in file.warnings) ctx.LogImportWarning($"[BBModelImporter] {warning}");

            ctx.DependsOnArtifact(LitShaderPath);
            ctx.DependsOnArtifact(GlassShaderPath);
            var lit = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            var glass = AssetDatabase.LoadAssetAtPath<Shader>(GlassShaderPath);

            var atlas = new Atlas(ctx, file);
            ctx.AddObjectToAsset("atlas", atlas.texture);
            var materials = new Dictionary<Part, Material[]>
            {
                [Part.Gray] = new[] { NewMaterial("Gray", lit, atlas.texture) },
                [Part.Color] = new[] { NewMaterial("Color", lit, atlas.texture) },
                [Part.Light] = new[] { NewMaterial("Light", lit, atlas.texture) },
                [Part.Glass] = new[]
                {
                    MakeTransparent(NewMaterial("GlassBack", glass, atlas.texture), backFaces: true),
                    MakeTransparent(NewMaterial("Glass", glass, atlas.texture), backFaces: false),
                },
                [Part.GrayGlass] = new[]
                {
                    MakeTransparent(NewMaterial("GrayGlassBack", glass, atlas.texture), backFaces: true),
                    MakeTransparent(NewMaterial("GrayGlass", glass, atlas.texture), backFaces: false),
                },
            };
            materials[Part.Light][0].SetTexture("_EmissionMap", atlas.texture);
            foreach (var material in materials.Values.SelectMany(m => m)) ctx.AddObjectToAsset("material:" + material.name, material);

            // 큐브 → 면(Unity 픽셀 좌표). 기준점은 내보내는 모든 큐브의 상자에서 정한다.
            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            var cubes = file.Cubes.Where(cube => cube.export && Exported(cube.parent)).ToList();
            var quads = new List<Quad>();
            foreach (var cube in cubes) quads.AddRange(CubeQuads(cube, atlas));
            var bounds = new Bounds();
            bool first = true;
            foreach (var v in quads.SelectMany(q => q.vertices))
            {
                if (first) { bounds = new Bounds(v, Vector3.zero); first = false; }
                else bounds.Encapsulate(v);
            }
            var pivotPoint = pivot switch
            {
                PivotMode.BottomBackEdge => new Vector3(bounds.center.x, bounds.min.y, bounds.min.z),
                PivotMode.TopCenter => new Vector3(bounds.center.x, bounds.max.y, bounds.center.z),
                PivotMode.Origin => Vector3.zero,
                _ => new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
            };

            // 부품 그룹 → 자식 오브젝트(원점 = 그룹 기준점, Unity 픽셀 좌표). 맨 위 오브젝트의 원점은 기준점.
            var owners = new Dictionary<BBModelFile.Group, Transform>();
            var ownerOrigin = new Dictionary<Transform, Vector3> { [root.transform] = pivotPoint };
            foreach (var node in file.roots) BuildOwners(node, root.transform);

            void BuildOwners(BBModelFile.Node node, Transform parent)
            {
                if (node is not BBModelFile.Group group || !group.export) return;
                var owner = parent;
                if (!Tags.ContainsKey(group.name.ToLowerInvariant()))
                {
                    var child = new GameObject(UniqueChildName(parent, group.name)).transform;
                    child.SetParent(parent, false);
                    var origin = ToUnity(group.origin);
                    child.localPosition = (origin - ownerOrigin[parent]) * PixelSize;
                    ownerOrigin[child] = origin;
                    owners[group] = owner = child;
                }
                foreach (var childNode in group.children) BuildOwners(childNode, owner);
            }

            Transform OwnerOf(BBModelFile.Node node)
            {
                for (var group = node.parent; group != null; group = group.parent)
                    if (owners.TryGetValue(group, out var owner)) return owner;
                return root.transform;
            }

            foreach (var quad in quads)
            {
                quad.owner = OwnerOf(quad.cube);
                quad.part = PartOf(quad.cube);
            }
            RemoveCovered(quads);

            // (부품 오브젝트, 부위)마다 렌더러 하나.
            foreach (var group in quads.GroupBy(q => (q.owner, q.part)))
            {
                var (owner, part) = group.Key;
                var target = owner.gameObject;
                string name = RendererName(part);
                if (name != null)
                {
                    target = new GameObject(name);
                    target.transform.SetParent(owner, false);
                }
                string id = $"{PathOf(owner, root.transform)}/{part}";
                var mesh = BuildMesh(id, group.ToList(), ownerOrigin[owner]);
                ctx.AddObjectToAsset("mesh:" + id, mesh);
                target.AddComponent<MeshFilter>().sharedMesh = mesh;
                target.AddComponent<MeshRenderer>().sharedMaterials = materials[part];
            }

            if (boxColliders)
                foreach (var cube in cubes.Where(c => !RotatedChain(c)))
                {
                    var owner = OwnerOf(cube);
                    Vector3 a = ToUnity(cube.from - Vector3.one * cube.inflate), b = ToUnity(cube.to + Vector3.one * cube.inflate);
                    var size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
                    if (size.x < Epsilon || size.y < Epsilon || size.z < Epsilon) continue;
                    // 한 오브젝트에 같은 컴포넌트가 여럿이면 다시 가져올 때 참조가 어긋날 수 있어 콜라이더마다 자식 하나.
                    var holder = new GameObject(UniqueChildName(owner, "Collider"));
                    holder.transform.SetParent(owner, false);
                    holder.transform.localPosition = ((a + b) / 2f - ownerOrigin[owner]) * PixelSize;
                    holder.AddComponent<BoxCollider>().size = size * PixelSize;
                }

            int rotated = cubes.Count(RotatedChain);
            if (rotated > 0) ctx.LogImportWarning($"[BBModelImporter] 회전한 큐브 {rotated}개: 지형 면 숨기기·면 덮기·상자 콜라이더에서 빠진다(규칙 5절).");
            if (atlas.untexturedFaces > 0) ctx.LogImportWarning($"[BBModelImporter] 텍스처가 없는 면 {atlas.untexturedFaces}개는 흰색(휘도 1)으로 그린다.");
            foreach (string missing in atlas.missing) ctx.LogImportWarning($"[BBModelImporter] 텍스처를 읽지 못했다: {missing} (흰색으로 그림). 텍스처를 파일 안에 넣어 저장하면 된다(규칙 1절).");

            root.AddComponent<VoxPaint>();
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
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

        /// <summary>
        /// Lit(반투명은 Lit 사본 `Slime~ColorMaker~/Lit Glass`)을 반투명(알파 섞기, 깊이 쓰기 없음)으로 바꾼다. 셰이더 GUI(BaseShaderGUI)가 하는 설정을 직접 한다:
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

        static bool Exported(BBModelFile.Group group)
        {
            for (; group != null; group = group.parent)
                if (!group.export) return false;
            return true;
        }

        static Part PartOf(BBModelFile.Node node)
        {
            for (var group = node.parent; group != null; group = group.parent)
                if (Tags.TryGetValue(group.name.ToLowerInvariant(), out var part)) return part;
            return Part.Gray;
        }

        static bool RotatedChain(BBModelFile.Node node)
        {
            if (node.rotation != Vector3.zero) return true;
            for (var group = node.parent; group != null; group = group.parent)
                if (group.rotation != Vector3.zero) return true;
            return false;
        }

        static string UniqueChildName(Transform parent, string name)
        {
            string result = name;
            for (int n = 2; parent.Find(result) != null; n++) result = $"{name} ({n})";
            return result;
        }

        static string PathOf(Transform target, Transform root) =>
            target == root ? root.name : $"{PathOf(target.parent, root)}/{target.name}";

        static Vector3 ToUnity(Vector3 blockbench) => new(blockbench.x, blockbench.y, -blockbench.z);

        // Blockbench 회전(도, 오일러 순서 ZYX = Rz·Ry·Rx, 오른손 좌표)을 점에 적용한다.
        static Vector3 Rotate(Vector3 v, Vector3 degrees)
        {
            if (degrees == Vector3.zero) return v;
            float ax = degrees.x * Mathf.Deg2Rad, ay = degrees.y * Mathf.Deg2Rad, az = degrees.z * Mathf.Deg2Rad;
            v = new Vector3(v.x, v.y * Mathf.Cos(ax) - v.z * Mathf.Sin(ax), v.y * Mathf.Sin(ax) + v.z * Mathf.Cos(ax));
            v = new Vector3(v.x * Mathf.Cos(ay) + v.z * Mathf.Sin(ay), v.y, -v.x * Mathf.Sin(ay) + v.z * Mathf.Cos(ay));
            v = new Vector3(v.x * Mathf.Cos(az) - v.y * Mathf.Sin(az), v.x * Mathf.Sin(az) + v.y * Mathf.Cos(az), v.z);
            return v;
        }

        // 큐브 자신, 그다음 감싼 그룹들의 회전을 기준점 둘레로 차례로 적용한다(Blockbench 좌표).
        static Vector3 Place(BBModelFile.Cube cube, Vector3 point, bool direction)
        {
            BBModelFile.Node node = cube;
            while (node != null)
            {
                point = direction ? Rotate(point, node.rotation) : Rotate(point - node.origin, node.rotation) + node.origin;
                node = node.parent;
            }
            return point;
        }

        static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back, // east, west, up, down, south(+Z), north(−Z)
        };

        static IEnumerable<Quad> CubeQuads(BBModelFile.Cube cube, Atlas atlas)
        {
            Vector3 f = cube.from - Vector3.one * cube.inflate, t = cube.to + Vector3.one * cube.inflate;
            // Blockbench 면마다 꼭짓점 0~3 = 왼쪽 위, 오른쪽 위, 왼쪽 아래, 오른쪽 아래(BufferGeometry.setShape와 같은 순서).
            var corners = new[]
            {
                new[] { new Vector3(t.x, t.y, t.z), new Vector3(t.x, t.y, f.z), new Vector3(t.x, f.y, t.z), new Vector3(t.x, f.y, f.z) }, // east
                new[] { new Vector3(f.x, t.y, f.z), new Vector3(f.x, t.y, t.z), new Vector3(f.x, f.y, f.z), new Vector3(f.x, f.y, t.z) }, // west
                new[] { new Vector3(f.x, t.y, f.z), new Vector3(t.x, t.y, f.z), new Vector3(f.x, t.y, t.z), new Vector3(t.x, t.y, t.z) }, // up
                new[] { new Vector3(f.x, f.y, t.z), new Vector3(t.x, f.y, t.z), new Vector3(f.x, f.y, f.z), new Vector3(t.x, f.y, f.z) }, // down
                new[] { new Vector3(f.x, t.y, t.z), new Vector3(t.x, t.y, t.z), new Vector3(f.x, f.y, t.z), new Vector3(t.x, f.y, t.z) }, // south
                new[] { new Vector3(t.x, t.y, f.z), new Vector3(f.x, t.y, f.z), new Vector3(t.x, f.y, f.z), new Vector3(f.x, f.y, f.z) }, // north
            };
            bool aligned = !RotatedChain(cube);
            for (int i = 0; i < 6; i++)
            {
                if (!cube.faces.TryGetValue(BBModelFile.FaceNames[i], out var face)) continue;
                var quad = new Quad { cube = cube, axisAligned = aligned };
                for (int k = 0; k < 4; k++) quad.vertices[k] = ToUnity(Place(cube, corners[i][k], false));
                quad.normal = ToUnity(Place(cube, FaceNormals[i], true)).normalized;

                // UV: 꼭짓점 0~3 = (u1, v1), (u2, v1), (u1, v2), (u2, v2), 면 회전 90°마다 한 칸씩 돌린다(Blockbench updateUV와 같음).
                var uv = face.uv;
                var corner = new[] { new Vector2(uv[0], uv[1]), new Vector2(uv[2], uv[1]), new Vector2(uv[0], uv[3]), new Vector2(uv[2], uv[3]) };
                for (int turn = face.rotation; turn > 0; turn -= 90)
                    (corner[0], corner[2], corner[3], corner[1]) = (corner[2], corner[3], corner[1], corner[0]);
                if (!atlas.HasTexture(face.texture)) atlas.untexturedFaces++;
                for (int k = 0; k < 4; k++) quad.uvs[k] = atlas.UV(face.texture, corner[k]);

                if (aligned)
                {
                    quad.axis = Mathf.Abs(quad.normal.x) > 0.5f ? 0 : Mathf.Abs(quad.normal.y) > 0.5f ? 1 : 2;
                    quad.sign = quad.normal[quad.axis] > 0 ? 1 : -1;
                    quad.plane = quad.vertices[0][quad.axis];
                    int a = (quad.axis + 1) % 3, b = (quad.axis + 2) % 3;
                    quad.rectMin = new Vector2(quad.vertices.Min(v => v[a]), quad.vertices.Min(v => v[b]));
                    quad.rectMax = new Vector2(quad.vertices.Max(v => v[a]), quad.vertices.Max(v => v[b]));
                }
                yield return quad;
            }
        }

        // 다른 큐브의 맞은편 면이 완전히 덮는 면을 버린다: 불투명 면은 불투명 면이, 반투명 면은 아무 면이 덮으면.
        static void RemoveCovered(List<Quad> quads)
        {
            var byPlane = quads.Where(q => q.axisAligned).ToLookup(q => (q.axis, q.sign, Mathf.RoundToInt(q.plane * 1000f)));
            var covered = new HashSet<Quad>();
            foreach (var quad in quads.Where(q => q.axisAligned))
                foreach (var other in byPlane[(quad.axis, -quad.sign, Mathf.RoundToInt(quad.plane * 1000f))])
                {
                    if (other.cube == quad.cube) continue;
                    if (!IsGlass(quad.part) && IsGlass(other.part)) continue;
                    if (other.rectMin.x <= quad.rectMin.x + Epsilon && other.rectMin.y <= quad.rectMin.y + Epsilon &&
                        other.rectMax.x >= quad.rectMax.x - Epsilon && other.rectMax.y >= quad.rectMax.y - Epsilon)
                    {
                        covered.Add(quad);
                        break;
                    }
                }
            quads.RemoveAll(covered.Contains);
        }

        static Mesh BuildMesh(string name, List<Quad> quads, Vector3 origin)
        {
            var vertices = new List<Vector3>(quads.Count * 4);
            var normals = new List<Vector3>(quads.Count * 4);
            var uvs = new List<Vector2>(quads.Count * 4);
            var triangles = new List<int>(quads.Count * 6);
            foreach (var quad in quads)
            {
                int start = vertices.Count;
                for (int k = 0; k < 4; k++)
                {
                    vertices.Add((quad.vertices[k] - origin) * PixelSize);
                    normals.Add(quad.normal);
                    uvs.Add(quad.uvs[k]);
                }
                // Unity는 시계 방향이 앞면이다. 법선 쪽에서 볼 때 앞면이 되도록 감는 순서를 고른다.
                var v = quad.vertices;
                bool keep = Vector3.Dot(Vector3.Cross(v[1] - v[0], v[2] - v[0]), quad.normal) > 0f;
                triangles.AddRange(keep
                    ? new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 }
                    : new[] { start, start + 2, start + 1, start + 2, start + 3, start + 1 });
            }
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>파일의 텍스처를 휘도 Y 회색으로 바꿔 위아래로 쌓은 한 장. 맨 아래 한 줄은 텍스처 없는 면의 흰색.</summary>
        class Atlas
        {
            public readonly Texture2D texture;
            public readonly List<string> missing = new();
            public int untexturedFaces;
            readonly int width, height;
            readonly List<(int top, int width, int frameHeight, BBModelFile.Texture source)> slots = new();
            readonly int whiteTop;

            public Atlas(AssetImportContext ctx, BBModelFile file)
            {
                var images = new List<Texture2D>();
                foreach (var source in file.textures)
                {
                    var image = BBModelFile.LoadImage(source, ctx.assetPath, out string external);
                    if (external != null)
                    {
                        string relative = ToProjectPath(external);
                        if (relative != null) ctx.DependsOnSourceAsset(relative);
                    }
                    if (image == null) missing.Add(source.name);
                    images.Add(image);
                }
                width = Mathf.Max(1, images.Where(i => i != null).Select(i => i.width).DefaultIfEmpty(1).Max());
                int top = 0;
                for (int i = 0; i < file.textures.Count; i++)
                {
                    var source = file.textures[i];
                    var image = images[i];
                    int w = image?.width ?? 1, h = image?.height ?? 1;
                    // 움직이는 텍스처(프레임 여러 장)는 첫 프레임을 쓴다. 프레임 높이 = 텍스처 폭 × (UV 높이 / UV 폭).
                    int frameHeight = source.uvWidth > 0 ? Mathf.Clamp(Mathf.RoundToInt(w * (float)source.uvHeight / source.uvWidth), 1, h) : h;
                    slots.Add((top, w, frameHeight, source));
                    top += h;
                }
                whiteTop = top;
                height = top + 1;

                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    if (image == null) continue;
                    var source = image.GetPixels32();
                    for (int y = 0; y < image.height; y++)
                        for (int x = 0; x < image.width; x++)
                        {
                            var c = source[y * image.width + x];
                            byte luma = (byte)Mathf.Clamp(Mathf.RoundToInt(0.299f * c.r + 0.587f * c.g + 0.114f * c.b), 0, 255);
                            // 그림의 y는 아래가 0. 아틀라스에서 이 텍스처는 위에서 slots[i].top부터.
                            int atlasRowFromTop = slots[i].top + (image.height - 1 - y);
                            pixels[(height - 1 - atlasRowFromTop) * width + x] = new Color32(luma, luma, luma, 255);
                        }
                    Object.DestroyImmediate(image);
                }
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Atlas",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(pixels);
                texture.Apply();
            }

            public bool HasTexture(int textureIndex) => textureIndex >= 0 && textureIndex < slots.Count;

            /// <summary>Blockbench 면 UV(텍스처 UV 크기 단위, 위가 0)를 아틀라스 UV(아래가 0)로.</summary>
            public Vector2 UV(int textureIndex, Vector2 uv)
            {
                if (!HasTexture(textureIndex)) return new Vector2(0.5f / width, 1f - (whiteTop + 0.5f) / height);
                var slot = slots[textureIndex];
                float u = slot.source.uvWidth > 0 ? uv.x / slot.source.uvWidth : 0f;
                float v = slot.source.uvHeight > 0 ? uv.y / slot.source.uvHeight : 0f;
                return new Vector2(u * slot.width / width, 1f - (slot.top + v * slot.frameHeight) / height);
            }

            static string ToProjectPath(string fullPath)
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
                string full = fullPath.Replace('\\', '/');
                return full.StartsWith(project) ? full[project.Length..] : null;
            }
        }
    }
}
