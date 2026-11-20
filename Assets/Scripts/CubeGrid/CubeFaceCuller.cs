using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 지형(블록·계단)과 방벽 판의 맞닿은 면에서 이웃이 덮는 부분을 그리지 않는다(Play 시작할 때 <see cref="CubeGrid"/>가 부름).
    /// 축에 맞춘 면을 (방향, 평면 위치: 칸 안 1/16 간격 17곳)마다 16 × 16 칸 그림(칸마다 그 자리 원래 면의 UV 대응식, 복셀 1칸 = 1/16)으로 바꾸고,
    /// 맞은편 면이 덮는 칸을 지운 뒤 남은 칸을 같은 대응식끼리 큰 사각형으로 다시 합친다(새 꼭짓점의 UV는 대응식으로 계산: 팔레트 모델은 상수, 텍스처 모델은 면을 따라 변함).
    /// 맞은편 면은 이웃 칸 모델의 칸 경계 면(칸 경계 면끼리)과, 같은 칸의 다른 모델이 같은 평면에서 반대쪽을 보는 면(한 칸의 방벽 여럿이 맞닿는 면)이다.
    /// 덮는 면이 없는 면(계단 안쪽 단, 이웃이 없는 방향)은 그대로다.
    /// 좌표는 칸 가운데를 원점으로 한 월드 축이라 돌려 놓인 계단·방벽도 그대로 맞대어 본다.
    /// 불투명 면은 불투명 이웃에만 지워지고(반투명 뒤에서는 보여야 함), 반투명 면은 불투명·반투명 이웃 모두에 지워진다(모델 가져오기 규칙과 같음).
    /// 방벽이 열려 사라지거나 다시 닫히면 <see cref="Refresh"/>로 그 칸과 이웃 칸을 다시 맞춘다(꺼진 렌더러는 덮지 않음).
    /// 같은 (모델, 놓인 방향, 이웃 모양) 조합은 메시를 한 번만 만들어 나눠 쓴다. 블록 오브젝트·콜라이더·데이터·씬은 그대로다.
    /// </summary>
    public sealed class CubeFaceCuller
    {
        const int Resolution = 16;
        const int Planes = Resolution + 1; // 방향마다 면 평면 위치 0~16(칸 기준 -0.5 ~ 0.5)
        const float Epsilon = 0.001f;

        static readonly Vector3[] Directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3Int[] Steps = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };

        // 한 모델 메시가 한 방향으로 놓인 모양: 칸 기준 꼭짓점과 면 평면 그림.
        sealed class Shape
        {
            public string key;
            public bool opaque, glass;
            public Matrix4x4 toCell;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uvs;
            public int[] triangles;
            public int[] triangleFace;                              // 삼각형이 놓인 면 평면(PlaneKey, 없으면 -1)
            public readonly Dictionary<int, int[]> faces = new();   // 면 평면마다 칸별 UV 대응식 번호(없으면 -1)
        }

        // 면 평면 번호: 방향 d와 그 축의 평면 위치 k(0~16).
        static int PlaneKey(int d, int k) => d * Planes + k;

        // 방향 d의 칸 경계 면 평면(+ 방향은 0.5, − 방향은 -0.5).
        static int BoundaryKey(int d) => PlaneKey(d, d % 2 == 0 ? Resolution : 0);

        sealed class Part
        {
            public MeshFilter filter;
            public Mesh original;
            public Shape shape;
        }

        readonly Dictionary<Vector3Int, List<Part>> cells = new();
        readonly Dictionary<Mesh, int> meshIds = new();
        readonly Dictionary<string, Shape> shapes = new();
        readonly Dictionary<string, Mesh> variants = new();
        readonly List<UvMap> uvTable = new();

        // 면 평면 좌표(a, b) → UV. 한 사각형(삼각형 둘)은 같은 대응식이다.
        readonly struct UvMap
        {
            public readonly Vector2 origin, da, db;

            public UvMap(Vector2 origin, Vector2 da, Vector2 db)
            {
                this.origin = origin;
                this.da = da;
                this.db = db;
            }

            public Vector2 At(float a, float b) => origin + da * a + db * b;

            public bool Same(UvMap other) =>
                (origin - other.origin).sqrMagnitude < 1e-10f && (da - other.da).sqrMagnitude < 1e-10f && (db - other.db).sqrMagnitude < 1e-10f;
        }
        readonly List<Mesh> created = new();

        /// <summary>격자 아래 만든 부분의 블록·계단과 방벽 판 메시를 덮인 칸을 뺀 메시로 바꾼다.</summary>
        public void Apply(CubeGrid grid, IEnumerable<Transform> barriers)
        {
            var generated = grid.transform.Find(CubeGridBuilder.GeneratedPartsName);
            if (generated != null)
                foreach (Transform kind in generated)
                    foreach (Transform part in kind)
                        Collect(part, includeInactive: false);
            foreach (var barrier in barriers) Collect(barrier, includeInactive: true);

            foreach (var cell in cells.Keys) Fit(cell);
        }

        /// <summary>칸과 여섯 이웃 칸의 메시를 지금 켜진 이웃 기준으로 다시 맞춘다(방벽이 사라지거나 다시 생길 때).</summary>
        public void Refresh(Vector3Int cell)
        {
            Fit(cell);
            foreach (var step in Steps) Fit(cell + step);
        }

        void Fit(Vector3Int cell)
        {
            if (!cells.TryGetValue(cell, out var parts)) return;
            foreach (var part in parts)
                if (part.shape.opaque || part.shape.glass)
                    part.filter.sharedMesh = Variant(cell, part, parts) ?? part.original;
        }

        /// <summary>만든 메시를 지운다.</summary>
        public void Dispose()
        {
            foreach (var mesh in created) Object.Destroy(mesh);
            created.Clear();
        }

        void Collect(Transform part, bool includeInactive)
        {
            var cell = CubeGridRules.CellAt(part.position);
            var center = CubeGridRules.CellCenter(cell);
            if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<Part>();
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(includeInactive))
            {
                if (filter.sharedMesh == null) continue;
                var toCell = Matrix4x4.Translate(-center) * filter.transform.localToWorldMatrix;
                var renderer = filter.GetComponent<Renderer>();
                list.Add(new Part { filter = filter, original = filter.sharedMesh, shape = ShapeOf(filter.sharedMesh, toCell, renderer) });
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
            bool usable = mesh.isReadable && mesh.subMeshCount == 1 && renderer != null && renderer.sharedMaterial != null;
            shape.opaque = usable && renderer.sharedMaterial.renderQueue < 2500;
            shape.glass = usable && !shape.opaque;
            if (!usable) return shape;

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
            for (int t = 0; t < shape.triangleFace.Length; t++)
            {
                shape.triangleFace[t] = FacePlane(shape, t);
                if (shape.triangleFace[t] >= 0) Rasterize(shape, t, shape.triangleFace[t]);
            }
            return shape;
        }

        // 삼각형이 축에 맞춘 면 평면(칸 안 1/16 간격)에 놓이면 그 평면 번호(보는 방향 + 위치), 아니면 -1.
        static int FacePlane(Shape shape, int t)
        {
            var a = shape.vertices[shape.triangles[t * 3]];
            var b = shape.vertices[shape.triangles[t * 3 + 1]];
            var c = shape.vertices[shape.triangles[t * 3 + 2]];
            var normal = Vector3.Cross(b - a, c - a).normalized;
            for (int d = 0; d < 6; d++)
            {
                if (Vector3.Dot(normal, Directions[d]) < 0.99f) continue;
                int axis = d / 2;
                float position = a[axis];
                if (Mathf.Abs(b[axis] - position) >= Epsilon || Mathf.Abs(c[axis] - position) >= Epsilon) return -1;
                int k = Mathf.RoundToInt((position + 0.5f) * Resolution);
                if (k < 0 || k > Resolution || Mathf.Abs(position - Edge(k)) >= Epsilon) return -1;
                return PlaneKey(d, k);
            }
            return -1;
        }

        // 면 칸 가운데가 삼각형 안이면 그 칸에 삼각형의 UV 대응식을 둔다.
        void Rasterize(Shape shape, int t, int plane)
        {
            var (ua, ub) = Axes(plane / Planes);
            var p0 = Plane(shape.vertices[shape.triangles[t * 3]], ua, ub);
            var p1 = Plane(shape.vertices[shape.triangles[t * 3 + 1]], ua, ub);
            var p2 = Plane(shape.vertices[shape.triangles[t * 3 + 2]], ua, ub);
            Vector2 UvOf(int k) => shape.uvs.Length > 0 ? shape.uvs[shape.triangles[t * 3 + k]] : Vector2.zero;
            int uv = UvIndex(Map(p0, p1, p2, UvOf(0), UvOf(1), UvOf(2)));
            if (!shape.faces.TryGetValue(plane, out var face))
            {
                shape.faces[plane] = face = new int[Resolution * Resolution];
                System.Array.Fill(face, -1);
            }
            for (int j = 0; j < Resolution; j++)
                for (int i = 0; i < Resolution; i++)
                    if (Inside(new Vector2(CellMid(i), CellMid(j)), p0, p1, p2)) face[i + j * Resolution] = uv;
        }

        int UvIndex(UvMap map)
        {
            for (int i = 0; i < uvTable.Count; i++)
                if (uvTable[i].Same(map)) return i;
            uvTable.Add(map);
            return uvTable.Count - 1;
        }

        // 삼각형의 세 점(평면 좌표)과 UV로 평면 좌표 → UV 대응식을 푼다. 넓이가 없으면 첫 점의 UV(상수).
        static UvMap Map(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            Vector2 e1 = p1 - p0, e2 = p2 - p0, f1 = uv1 - uv0, f2 = uv2 - uv0;
            float det = e1.x * e2.y - e1.y * e2.x;
            if (Mathf.Abs(det) < 1e-12f) return new UvMap(uv0, Vector2.zero, Vector2.zero);
            var da = (f1 * e2.y - f2 * e1.y) / det;
            var db = (f2 * e1.x - f1 * e2.x) / det;
            return new UvMap(uv0 - da * p0.x - db * p0.y, da, db);
        }

        // 이 칸 이 부분의 메시: 덮인 면이 없으면 null(원래 메시 그대로).
        Mesh Variant(Vector3Int cell, Part part, List<Part> sameCell)
        {
            var shape = part.shape;
            var covered = new Dictionary<int, bool[]>();
            string key = shape.key;

            // 이웃 칸: 칸 경계 면끼리.
            for (int d = 0; d < 6; d++)
            {
                if (!cells.TryGetValue(cell + Steps[d], out var neighbors)) continue;
                foreach (var neighbor in neighbors)
                    if (Covers(neighbor, shape) && Cover(shape, BoundaryKey(d), neighbor.shape, BoundaryKey(d ^ 1), covered))
                        key += $"|{d}:{neighbor.shape.key}";
            }
            // 같은 칸: 같은 평면에서 반대쪽을 보는 면끼리(한 칸의 방벽 여럿이 맞닿는 면).
            foreach (var other in sameCell)
            {
                if (other == part || !Covers(other, shape)) continue;
                bool touched = false;
                foreach (var plane in shape.faces.Keys)
                    touched |= Cover(shape, plane, other.shape, PlaneKey((plane / Planes) ^ 1, plane % Planes), covered);
                if (touched) key += $"|s:{other.shape.key}";
            }

            if (covered.Count == 0) return null;
            if (variants.TryGetValue(key, out var cached)) return cached;
            var mesh = Build(shape, covered);
            variants[key] = mesh;
            created.Add(mesh);
            return mesh;
        }

        // 불투명 이웃은 모두를, 반투명 이웃은 반투명 면만 덮는다. 꺼진(사라진 방벽) 이웃은 덮지 않는다.
        static bool Covers(Part other, Shape shape) =>
            (other.shape.opaque || other.shape.glass && shape.glass) && other.filter.gameObject.activeInHierarchy;

        // shape의 평면 mine에서 other의 평면 theirs가 덮는 칸을 covered에 표시한다. 덮은 칸이 있으면 true.
        static bool Cover(Shape shape, int mine, Shape other, int theirs, Dictionary<int, bool[]> covered)
        {
            if (!shape.faces.TryGetValue(mine, out var face) || !other.faces.TryGetValue(theirs, out var otherFace)) return false;
            bool any = false;
            for (int c = 0; c < face.Length; c++)
            {
                if (face[c] < 0 || otherFace[c] < 0) continue;
                if (!covered.TryGetValue(mine, out var marks)) covered[mine] = marks = new bool[face.Length];
                marks[c] = true;
                any = true;
            }
            return any;
        }

        Mesh Build(Shape shape, Dictionary<int, bool[]> covered)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var remap = new Dictionary<int, int>();

            // 덮인 평면의 면은 빼고(아래에서 남은 칸만 다시 만듦), 나머지 삼각형은 그대로 옮긴다.
            for (int t = 0; t < shape.triangleFace.Length; t++)
            {
                int plane = shape.triangleFace[t];
                if (plane >= 0 && covered.ContainsKey(plane)) continue;
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

            foreach (var (plane, marks) in covered)
            {
                var cellsLeft = (int[])shape.faces[plane].Clone();
                for (int c = 0; c < cellsLeft.Length; c++)
                    if (marks[c]) cellsLeft[c] = -1;
                Merge(cellsLeft, plane, vertices, normals, uvs, triangles);
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

        // 남은 칸을 같은 UV 대응식끼리 큰 사각형으로 합친다(면 합치기).
        void Merge(int[] face, int plane, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int d = plane / Planes;
            var (ua, ub) = Axes(d);
            var direction = Directions[d];
            float position = Edge(plane % Planes);
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
                    var v0 = Point(d / 2, position, ua, ub, a0, b0);
                    var v1 = Point(d / 2, position, ua, ub, a1, b0);
                    var v2 = Point(d / 2, position, ua, ub, a1, b1);
                    var v3 = Point(d / 2, position, ua, ub, a0, b1);
                    if (Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), direction) < 0f) (v1, v3) = (v3, v1);
                    int start = vertices.Count;
                    vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
                    foreach (var v in new[] { v0, v1, v2, v3 })
                    {
                        normals.Add(direction);
                        uvs.Add(uvTable[uv].At(v[ua], v[ub]));
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

        static Vector3 Point(int axis, float position, int ua, int ub, float a, float b)
        {
            var point = Vector3.zero;
            point[axis] = position;
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
