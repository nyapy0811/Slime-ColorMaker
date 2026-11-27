using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 게이트(문, <see cref="Door"/>)의 겉모습: 열리면 열리는 면(<see cref="GateFaces"/>)마다 판(모델 Gate)의 삼각형 네 조각이 조리개처럼
    /// 각자 바깥 변 쪽으로 미끄러져 테두리 속으로 들어간다. 테두리(14 × 14 칸)를 넘은 부분은 조각 모양을 그 칸 안으로 잘라 그린다
    /// (메시 꼭짓점을 칸 안으로 눌러 넣고 텍스처 좌표를 같은 비율로 맞춤, 축에 나란한 복셀 면이라 사각형끼리 겹침 계산으로 끝남).
    /// 다 열리면 판을 끄고 문에 알린다(그때부터 지나갈 수 있음). 조각의 바깥 방향은 모델의 꼭짓점에서 계산한다(조각 무게중심이 치우친 쪽).
    /// </summary>
    public class GateView : OpenableView
    {
        const float Half = 7f / 16f; // 판 칸의 반 폭(모델 기준, 유닛)

        [Tooltip("면 구성(열리는 면)")]
        [SerializeField] GateFaces faces;

        [Tooltip("다 열리는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.6f;

        // 조각 메시 하나: 모델 기준 원래 꼭짓점·텍스처 좌표, 바깥 방향, 메시 좌표 ↔ 모델 좌표 변환.
        sealed class Piece
        {
            public Mesh mesh;
            public Vector3[] vertices;   // 모델 기준
            public Vector2[] uvs;
            public Vector3 direction;    // 모델 기준 바깥 방향(±X 또는 ±Z)
            public Matrix4x4 toMesh;     // 모델 → 메시
            public Vector3[] moved;
            public Vector2[] movedUvs;
        }

        readonly List<Piece> pieces = new();
        readonly List<GameObject> models = new();
        float openness;
        float target;

        void Awake()
        {
            enabled = false;
            for (int i = 0; i < 6; i++)
            {
                var face = (CellFace)i;
                if (!faces.IsOpen(face) || faces.OpenModel(face) == null) continue;
                var model = faces.OpenModel(face).transform;
                models.Add(model.gameObject);
                foreach (Transform part in model) AddPiece(model, part);
            }
        }

        // 조각(모델 바로 아래 자식) 하나의 메시들. 바깥 방향은 조각 전체 무게중심이 치우친 축.
        void AddPiece(Transform model, Transform part)
        {
            var filters = part.GetComponentsInChildren<MeshFilter>(true);
            var center = Vector3.zero;
            int count = 0;
            var made = new List<Piece>();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                var toModel = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = filter.mesh; // 이 게이트만의 사본
                var source = mesh.vertices;
                var piece = new Piece
                {
                    mesh = mesh,
                    vertices = new Vector3[source.Length],
                    uvs = mesh.uv,
                    toMesh = toModel.inverse,
                    moved = new Vector3[source.Length],
                    movedUvs = new Vector2[source.Length],
                };
                for (int v = 0; v < source.Length; v++)
                {
                    piece.vertices[v] = toModel.MultiplyPoint3x4(source[v]);
                    center += piece.vertices[v];
                }
                count += source.Length;
                made.Add(piece);
            }
            if (count == 0) return;
            center /= count;
            var direction = Mathf.Abs(center.x) >= Mathf.Abs(center.z) ? new Vector3(Mathf.Sign(center.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(center.z));
            foreach (var piece in made)
            {
                piece.direction = direction;
                pieces.Add(piece);
            }
        }

        public override void SetOpen(bool open)
        {
            target = open ? 1f : 0f;
            Show(true);
            enabled = true;
        }

        public override void SetOpenImmediate(bool open)
        {
            openness = target = open ? 1f : 0f;
            Apply();
            enabled = false;
        }

        void Update()
        {
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / duration);
            Apply();
            if (openness != target) return;
            enabled = false;
            if (openness == 1f) NotifyFullyOpened();
        }

        void Apply()
        {
            Show(openness < 1f);
            float slide = Mathf.SmoothStep(0f, 1f, openness) * Half * 2f;
            foreach (var piece in pieces) Slide(piece, piece.direction * slide);
        }

        void Show(bool shown)
        {
            foreach (var model in models)
                if (model.activeSelf != shown) model.SetActive(shown);
        }

        // 옮긴 뒤 판 칸(±Half, 모델의 X·Z) 밖은 잘라 낸다. 메시는 사각형(꼭짓점 4개)마다 따로이고 꼭짓점 0→1, 0→2가 두 변이다.
        static void Slide(Piece piece, Vector3 offset)
        {
            var source = piece.vertices;
            for (int q = 0; q + 3 < source.Length; q += 4)
            {
                var origin = source[q] + offset;
                var u = source[q + 1] - source[q];
                var v = source[q + 2] - source[q];
                float uu = u.sqrMagnitude, vv = v.sqrMagnitude;
                for (int k = 0; k < 4; k++)
                {
                    var p = source[q + k] + offset;
                    p.x = Mathf.Clamp(p.x, -Half, Half);
                    p.z = Mathf.Clamp(p.z, -Half, Half);
                    var d = p - origin;
                    float s = uu > 0f ? Vector3.Dot(d, u) / uu : 0f;
                    float t = vv > 0f ? Vector3.Dot(d, v) / vv : 0f;
                    var uvs = piece.uvs;
                    piece.movedUvs[q + k] = uvs[q] + s * (uvs[q + 1] - uvs[q]) + t * (uvs[q + 2] - uvs[q]);
                    piece.moved[q + k] = piece.toMesh.MultiplyPoint3x4(p);
                }
            }
            piece.mesh.vertices = piece.moved;
            piece.mesh.uv = piece.movedUvs;
            piece.mesh.RecalculateBounds();
        }
    }
}
