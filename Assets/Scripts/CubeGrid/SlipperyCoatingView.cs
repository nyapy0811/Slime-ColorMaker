using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 블록 윗면 코팅의 겉모습과 찾기(2026-11-14). 큐브 격자 빌더가 '만든 부분/코팅'에 두고, 켜질 때와 데이터가 바뀔 때 다시 만든다.
    /// 칠한 픽셀마다 블록 맨 위 1/16 층 자리에 면을 그린다: 윗면(블록 윗면과 같은 평면), 가장자리 픽셀은 바깥 옆면 띠(높이 1/16, 옆 칸이 블록이면 안 그림).
    /// 같은 줄의 이어진 같은 종류 픽셀은 한 면으로 합친다. 무늬 UV는 월드 좌표라 칸마다 그림 한 장이 반복된다.
    /// 면은 저장하지 않는 숨긴 오브젝트(종류마다 하나)로 만들어 씬 파일에 메시가 남지 않는다. 재질은 깊이를 당기는 Lit Glass라 블록 면과 겹쳐도 깨지지 않는다.
    /// 종류에 재질감(<see cref="SurfaceFinish"/>)이 있으면 재질의 저장하지 않는 사본에 입혀 쓰고, 재질감 값이 바뀌면 다시 만든다.
    /// 캐릭터는 <see cref="TryGetInertiaTime"/>으로 중심점 아래 픽셀의 관성 시간을 묻는다.
    /// </summary>
    [ExecuteAlways]
    public class SlipperyCoatingView : MonoBehaviour
    {
        const float Pixel = 1f / CubeGridRules.CoatingPixels;

        [SerializeField] SlipperyCoatingSettings settings;

        static SlipperyCoatingView active;

        readonly Dictionary<Vector3Int, byte[]> byCell = new();
        readonly List<GameObject> shown = new();
        readonly List<Material> finished = new(); // 재질감을 입힌 재질 사본(저장하지 않음)

        public SlipperyCoatingSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>점(캐릭터 중심점의 발 높이) 아래 블록 윗면 픽셀이 코팅이면 그 종류의 관성 시간.</summary>
        public static bool TryGetInertiaTime(Vector3 point, out float time)
        {
            time = 0f;
            if (active == null || active.settings == null) return false;
            CubeGridRules.CoatingPixelAt(point, out var cell, out int x, out int z);
            if (!active.byCell.TryGetValue(cell, out var pixels)) return false;
            var kind = active.settings.For((CoatingKind)pixels[z * CubeGridRules.CoatingPixels + x]);
            if (kind == null) return false;
            time = kind.inertiaTime;
            return true;
        }

        void OnEnable()
        {
            active = this;
            SurfaceFinish.Changed += OnFinishChanged;
            SlipperyCoatingSettings.Changed += RebuildSoon;
#if UNITY_EDITOR
            Undo.undoRedoPerformed += Rebuild;
#endif
            Rebuild();
        }

        void OnDisable()
        {
            SurfaceFinish.Changed -= OnFinishChanged;
            SlipperyCoatingSettings.Changed -= RebuildSoon;
#if UNITY_EDITOR
            Undo.undoRedoPerformed -= Rebuild;
#endif
            Clear();
            if (active == this) active = null;
        }

        void OnFinishChanged(SurfaceFinish finish)
        {
            if (settings != null && (settings.oil.finish == finish || settings.ice.finish == finish)) RebuildSoon();
        }

        // 설정·재질감의 인스펙터 변경(OnValidate) 중에는 오브젝트를 만들 수 없어 한 번 미룬다.
        void RebuildSoon()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Rebuild();
            };
#else
            Rebuild();
#endif
        }

        /// <summary>격자 데이터에서 찾기 표와 면을 다시 만든다.</summary>
        public void Rebuild()
        {
            Clear();
            var grid = GetComponentInParent<CubeGrid>();
            if (grid == null) return;
            var data = grid.Data;
            foreach (var coating in data.coatings)
                if (coating.pixels != null && coating.pixels.Length == CubeGridRules.CoatingPixels * CubeGridRules.CoatingPixels)
                    byCell[coating.cell] = coating.pixels;
            if (settings == null) return;
            foreach (var kind in new[] { CoatingKind.Oil, CoatingKind.Ice })
            {
                var entry = settings.For(kind);
                var material = entry.material;
                if (material != null && entry.finish != null)
                {
                    material = new Material(material) { name = $"{material.name} ({entry.finish.name})", hideFlags = HideFlags.HideAndDontSave };
                    entry.finish.ApplyTo(material);
                    finished.Add(material);
                }
                var mesh = BuildMesh(data, kind);
                if (mesh == null) continue;
                var go = new GameObject($"Coating {kind}") { hideFlags = HideFlags.HideAndDontSave };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                shown.Add(go);
            }
        }

        void Clear()
        {
            byCell.Clear();
            foreach (var go in shown)
            {
                if (go == null) continue;
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                DestroyNow(mesh);
                DestroyNow(go);
            }
            shown.Clear();
            foreach (var material in finished) DestroyNow(material);
            finished.Clear();
        }

        static void DestroyNow(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        Mesh BuildMesh(CubeGridData data, CoatingKind kind)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            const int n = CubeGridRules.CoatingPixels;
            byte code = (byte)kind;

            foreach (var (cell, pixels) in byCell)
            {
                float left = cell.x - 0.5f, back = cell.z - 0.5f, top = cell.y + 1f;
                // 윗면: x 방향으로 이어진 같은 종류 픽셀을 한 면으로.
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        if (pixels[z * n + x] != code) continue;
                        int end = x;
                        while (end + 1 < n && pixels[z * n + end + 1] == code) end++;
                        float x0 = left + x * Pixel, x1 = left + (end + 1) * Pixel, z0 = back + z * Pixel, z1 = back + (z + 1) * Pixel;
                        AddQuad(new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), Vector3.up,
                            new Vector2(x0 + 0.5f, z0 + 0.5f), new Vector2(x0 + 0.5f, z1 + 0.5f), new Vector2(x1 + 0.5f, z1 + 0.5f), new Vector2(x1 + 0.5f, z0 + 0.5f));
                        x = end;
                    }
                // 가장자리 픽셀의 바깥 옆면 띠(옆 칸이 블록이면 가려지므로 안 그림).
                float bottom = top - Pixel;
                if (!CubeGridRules.HasBlock(data, cell + Vector3Int.left)) EdgeRun(pixels, i => i * n, left, Vector3.left, false, top, bottom, back);
                if (!CubeGridRules.HasBlock(data, cell + Vector3Int.right)) EdgeRun(pixels, i => i * n + n - 1, left + 1f, Vector3.right, false, top, bottom, back);
                if (!CubeGridRules.HasBlock(data, cell + new Vector3Int(0, 0, -1))) EdgeRun(pixels, i => i, back, Vector3.back, true, top, bottom, left);
                if (!CubeGridRules.HasBlock(data, cell + new Vector3Int(0, 0, 1))) EdgeRun(pixels, i => (n - 1) * n + i, back + 1f, Vector3.forward, true, top, bottom, left);
            }
            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = $"Coating {kind}", hideFlags = HideFlags.HideAndDontSave };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;

            // 한 가장자리(픽셀 줄)의 이어진 같은 종류 픽셀을 바깥 옆면 띠로. alongX면 띠가 x 방향으로 뻗는다(앞·뒤 면).
            void EdgeRun(byte[] row, System.Func<int, int> indexOf, float plane, Vector3 normal, bool alongX, float high, float low, float start)
            {
                for (int i = 0; i < n; i++)
                {
                    if (row[indexOf(i)] != code) continue;
                    int end = i;
                    while (end + 1 < n && row[indexOf(end + 1)] == code) end++;
                    float a = start + i * Pixel, b = start + (end + 1) * Pixel;
                    Vector3 Point(float along, float y) => alongX ? new Vector3(along, y, plane) : new Vector3(plane, y, along);
                    // 바깥에서 볼 때 시계 방향이 되게 감는 순서를 고른다.
                    bool flip = alongX ? normal.z > 0f : normal.x < 0f;
                    var v0 = Point(a, low); var v1 = Point(a, high); var v2 = Point(b, high); var v3 = Point(b, low);
                    // 무늬는 그림의 맨 위 한 줄(v 1 − 1/16 ~ 1)을 쓴다.
                    var u0 = new Vector2(a + 0.5f, 1f - Pixel); var u1 = new Vector2(a + 0.5f, 1f); var u2 = new Vector2(b + 0.5f, 1f); var u3 = new Vector2(b + 0.5f, 1f - Pixel);
                    if (flip) AddQuad(v3, v2, v1, v0, normal, u3, u2, u1, u0);
                    else AddQuad(v0, v1, v2, v3, normal, u0, u1, u2, u3);
                    i = end;
                }
            }

            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
    }
}
