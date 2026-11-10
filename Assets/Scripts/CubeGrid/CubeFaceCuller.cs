using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 지형(블록·계단)의 맞닿은 면에서 이웃이 덮는 부분을 그리지 않는다(2026-11-08, Play 시작할 때 <see cref="CubeGrid"/>가 부름).
    /// 칸 경계에 놓인 면을 방향마다 16 × 16 칸 그림(칸마다 팔레트 UV, 복셀 1칸 = 1/16)으로 바꾸고, 이웃의 맞은편 면이 덮는 칸을 지운 뒤
    /// 남은 칸을 같은 UV끼리 큰 사각형으로 다시 합친다. 칸 경계에 있지 않은 면(계단 안쪽 단)과 이웃이 없는 방향의 면은 그대로다.
    /// 좌표는 칸 가운데를 원점으로 한 월드 축이라 돌려 놓인 계단도 그대로 맞대어 본다. 불투명 렌더러만 덮는 쪽·지우는 쪽이 된다.
    /// 같은 (모델, 놓인 방향, 이웃 모양) 조합은 메시를 한 번만 만들어 나눠 쓴다. 블록 오브젝트·콜라이더·데이터·씬은 그대로다.
    /// </summary>
    public sealed class CubeFaceCuller
    {
        const int Resolution = 16;
        const float Epsilon = 0.001f;

        static readonly Vector3[] Directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3Int[] Steps = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };

        // 한 모델 메시가 한 방향으로 놓인 모양: 칸 기준 꼭짓점과 칸 경계 면 그림.
        sealed class Shape
        {
            public string key;
            public bool opaque;
            public Matrix4x4 toCell;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uvs;
            public int[] triangles;
            public int[] triangleFace;                      // 삼각형이 놓인 칸 경계 방향(없으면 -1)
            public readonly int[][] faces = new int[6][];   // 방향마다 칸별 UV 번호(없으면 -1)
        }

        sealed class Part
        {
            public MeshFilter filter;
            public Shape shape;
        }

        readonly Dictionary<Vector3Int, List<Part>> cells = new();
        readonly Dictionary<Mesh, int> meshIds = new();
        readonly Dictionary<string, Shape> shapes = new();
        readonly Dictionary<string, Mesh> variants = new();
        readonly List<Vector2> uvTable = new();
        readonly List<Mesh> created = new();

        /// <summary>격자 아래 만든 부분의 블록·계단 메시를 덮인 칸을 뺀 메시로 바꾼다.</summary>
        public void Apply(CubeGrid grid)
        {
            var generated = grid.transform.Find(CubeGridBuilder.GeneratedPartsName);
            if (generated == null) return;
            foreach (Transform kind in generated)
                foreach (Transform part in kind)
                    Collect(part);

            foreach (var (cell, parts) in cells)
                foreach (var part in parts)
                    if (part.shape.opaque && Variant(cell, part.shape) is { } mesh)
                        part.filter.sharedMesh = mesh;
        }

        /// <summary>만든 메시를 지운다.</summary>
        public void Dispose()
        {
            foreach (var mesh in created) Object.Destroy(mesh);
            created.Clear();
        }

        void Collect(Transform part)
        {
            var cell = CubeGridRules.CellAt(part.position);
            var center = CubeGridRules.CellCenter(cell);
            if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<Part>();
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var toCell = Matrix4x4.Translate(-center) * filter.transform.localToWorldMatrix;
                var renderer = filter.GetComponent<Renderer>();
                list.Add(new Part { filter = filter, shape = ShapeOf(filter.sharedMesh, toCell, renderer) });
            }
        }

        // 같은 메시·같은 놓임(칸 기준 변환)이면 같은 모양으로 본다.
        Shape ShapeOf(Mesh mesh, Matrix4x4 toCell, Renderer renderer)
        {
            if (!meshIds.TryGetValue(mesh, out int id)) meshIds[mesh] = id = meshIds.Count;
            var key = id.ToString();
            for (int i = 0; i < 12; i++) key += "," + Mathf.RoundToInt(toCell[i % 3, i / 3] * 1000f);
            if (shapes.TryGetValue(key, out var shape)) return shape;

            shape = new Shape { key = key, toCell = toCell };
            shapes[key] = shape;
            shape.opaque = mesh.isReadable && mesh.subMeshCount == 1 && renderer != null && renderer.sharedMaterial != null
                && renderer.sharedMaterial.renderQueue < 2500;
            if (!shape.opaque) return shape;

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = toCell.MultiplyPoint3x4(vertices[i]);
                if (i < normals.Length) normals[i] = toCell.MultiplyVector(normals[i]).normalized;
            }
            shape.vertices = vertices;
            shape.normals = normals;
            shape.uvs = mesh.uv;
            shape.triangles = mesh.triangles;
            shape.triangleFace = new int[shape.triangles.Length / 3];
            for (int d = 0; d < 6; d++)
            {
                shape.faces[d] = new int[Resolution * Resolution];
                System.Array.Fill(shape.faces[d], -1);
            }
            for (int t = 0; t < shape.triangleFace.Length; t++)
            {
                shape.triangleFace[t] = BoundaryFace(shape, t);
                if (shape.triangleFace[t] >= 0) Rasterize(shape, t, shape.triangleFace[t]);
            }
            return shape;
        }

        // 삼각형이 칸 경계 평면(방향 쪽 0.5)에 놓이고 바깥을 보면 그 방향, 아니면 -1.
        static int BoundaryFace(Shape shape, int t)
        {
            var a = shape.vertices[shape.triangles[t * 3]];
            var b = shape.vertices[shape.triangles[t * 3 + 1]];
            var c = shape.vertices[shape.triangles[t * 3 + 2]];
            var normal = Vector3.Cross(b - a, c - a).normalized;
            for (int d = 0; d < 6; d++)
            {
                var direction = Directions[d];
                if (Vector3.Dot(normal, direction) < 0.99f) continue;
                if (Mathf.Abs(Vector3.Dot(a, direction) - 0.5f) < Epsilon && Mathf.Abs(Vector3.Dot(b, direction) - 0.5f) < Epsilon
                    && Mathf.Abs(Vector3.Dot(c, direction) - 0.5f) < Epsilon) return d;
            }
            return -1;
        }

        // 면 칸 가운데가 삼각형 안이면 그 칸에 삼각형의 UV를 둔다.
        void Rasterize(Shape shape, int t, int d)
        {
            var (ua, ub) = Axes(d);
            var p0 = Plane(shape.vertices[shape.triangles[t * 3]], ua, ub);
            var p1 = Plane(shape.vertices[shape.triangles[t * 3 + 1]], ua, ub);
            var p2 = Plane(shape.vertices[shape.triangles[t * 3 + 2]], ua, ub);
            int uv = UvIndex(shape.uvs.Length > 0 ? shape.uvs[shape.triangles[t * 3]] : Vector2.zero);
            var face = shape.faces[d];
            for (int j = 0; j < Resolution; j++)
                for (int i = 0; i < Resolution; i++)
                    if (Inside(new Vector2(CellMid(i), CellMid(j)), p0, p1, p2)) face[i + j * Resolution] = uv;
        }

        int UvIndex(Vector2 uv)
        {
            for (int i = 0; i < uvTable.Count; i++)
                if ((uvTable[i] - uv).sqrMagnitude < 1e-10f) return i;
            uvTable.Add(uv);
            return uvTable.Count - 1;
        }

        // 이 칸 이 모양의 메시: 이웃이 덮는 방향이 없으면 null(원래 메시 그대로).
        Mesh Variant(Vector3Int cell, Shape shape)
        {
            var covered = new bool[6][];

            string key = shape.key;
            bool any = false;
            for (int d = 0; d < 6; d++)
            {
                if (!cells.TryGetValue(cell + Steps[d], out var neighbors)) continue;
                int opposite = d ^ 1;
                foreach (var neighbor in neighbors)
                {
                    if (!neighbor.shape.opaque) continue;
                    var face = neighbor.shape.faces[opposite];
                    for (int c = 0; c < face.Length; c++)
                    {
                        if (face[c] < 0 || shape.faces[d][c] < 0) continue;
                        covered[d] ??= new bool[face.Length];
                        covered[d][c] = true;
                        any = true;
                    }
                    key += $"|{d}:{neighbor.shape.key}";
                }
            }
            if (!any) return null;
            if (variants.TryGetValue(key, out var cached)) return cached;
            var mesh = Build(shape, covered);
            variants[key] = mesh;
            created.Add(mesh);
            return mesh;
        }

        Mesh Build(Shape shape, bool[][] covered)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var remap = new Dictionary<int, int>();

            // 덮인 방향의 경계 면은 빼고(아래에서 남은 칸만 다시 만듦), 나머지 삼각형은 그대로 옮긴다.
            for (int t = 0; t < shape.triangleFace.Length; t++)
            {
                int d = shape.triangleFace[t];
                if (d >= 0 && covered[d] != null) continue;
                for (int k = 0; k < 3; k++)
                {
                    int index = shape.triangles[t * 3 + k];
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        mapped = vertices.Count;
                        remap[index] = mapped;
                        vertices.Add(shape.vertices[index]);
                        normals.Add(index < shape.normals.Length ? shape.normals[index] : Vector3.up);
                        uvs.Add(index < shape.uvs.Length ? shape.uvs[index] : Vector2.zero);
                    }
                    triangles.Add(mapped);
                }
            }

            for (int d = 0; d < 6; d++)
            {
                if (covered[d] == null) continue;
                var cellsLeft = (int[])shape.faces[d].Clone();
                for (int c = 0; c < cellsLeft.Length; c++)
                    if (covered[d][c]) cellsLeft[c] = -1;
                Merge(cellsLeft, d, vertices, normals, uvs, triangles);
            }

            var fromCell = shape.toCell.inverse;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = fromCell.MultiplyPoint3x4(vertices[i]);
                normals[i] = fromCell.MultiplyVector(normals[i]).normalized;
            }
            var mesh = new Mesh { name = "Culled" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 남은 칸을 같은 UV끼리 큰 사각형으로 합친다(면 합치기).
        void Merge(int[] face, int d, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            var (ua, ub) = Axes(d);
            var direction = Directions[d];
            for (int j = 0; j < Resolution; j++)
            {
                for (int i = 0; i < Resolution; i++)
                {
                    int uv = face[i + j * Resolution];
                    if (uv < 0) continue;
                    int width = 1;
                    while (i + width < Resolution && face[i + width + j * Resolution] == uv) width++;
                    int height = 1;
                    while (j + height < Resolution && RowMatches(face, i, j + height, width, uv)) height++;
                    for (int y = j; y < j + height; y++)
                        for (int x = i; x < i + width; x++)
                            face[x + y * Resolution] = -1;

                    float a0 = Edge(i), a1 = Edge(i + width), b0 = Edge(j), b1 = Edge(j + height);
                    var v0 = Point(direction, ua, ub, a0, b0);
                    var v1 = Point(direction, ua, ub, a1, b0);
                    var v2 = Point(direction, ua, ub, a1, b1);
                    var v3 = Point(direction, ua, ub, a0, b1);
                    if (Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), direction) < 0f) (v1, v3) = (v3, v1);
                    int start = vertices.Count;
                    vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
                    for (int k = 0; k < 4; k++)
                    {
                        normals.Add(direction);
                        uvs.Add(uvTable[uv]);
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }
        }

        static bool RowMatches(int[] face, int i, int j, int width, int uv)
        {
            for (int x = i; x < i + width; x++)
                if (face[x + j * Resolution] != uv) return false;
            return true;
        }

        // 방향 d의 면 평면에서 쓰는 두 축(나머지 두 축).
        static (int, int) Axes(int d)
        {
            int axis = d / 2;
            return ((axis + 1) % 3, (axis + 2) % 3);
        }

        static Vector2 Plane(Vector3 point, int ua, int ub) => new(point[ua], point[ub]);

        static Vector3 Point(Vector3 direction, int ua, int ub, float a, float b)
        {
            var point = direction * 0.5f;
            point[ua] = a;
            point[ub] = b;
            return point;
        }

        static float Edge(int index) => -0.5f + (float)index / Resolution;
        static float CellMid(int index) => -0.5f + (index + 0.5f) / Resolution;

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (a.x - p.x) * (b.y - p.y) - (a.y - p.y) * (b.x - p.x);
    }
}
