using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 방벽(레이저 벽, <see cref="LaserWall"/>)의 겉모습: 칸의 한 면을 덮는 반투명 판. 지나갈 수 있는지와 상관없이 늘 보인다.
    /// 판은 조각으로 짓는다(모델 Models/Fixtures/BarrierMain·BarrierSide·BarrierCorner, 두께 1픽셀): 가운데 판 1 + 가장자리 띠 4(판의 로컬 +Z·−Z·+Y·−Y 끝)
    /// + 모서리 4. 같은 칸의 다른 면 방벽과 가장자리가 겹치면 띠·모서리는 한쪽만 켠다(우선순위: 위·아래 > 북·남 > 동·서, 모서리는 둘레 세 판 중 가장 높은 판).
    /// 같은 칸의 다른 방벽이 없어지면 다시 켠다. Play 중에만 바꾼다(편집 화면은 온전한 판).
    /// 이어진 방벽 묶음(<see cref="BarrierGroup"/>)의 바깥 테두리인 띠·모서리는 가장자리 투명도(<see cref="ModelLook.edgeTransparency"/>)로 칠한다:
    /// 묶음의 다른 판이 그 띠의 두 끝점을 모두 가지면(나란히 이어지거나 ㄱ자로 꺾임) 안쪽, 아니면 가장자리. 모서리는 맞닿은 띠 둘 중 하나라도 가장자리면 가장자리.
    /// Play 시작 때 한 번 정한다(Start: 모든 방벽이 묶음에 등록된 뒤, 묶음은 Play 중 바뀌지 않음).
    /// 모양(켜진 띠·모서리)이 바뀌면 알린다(큐브 격자가 맞닿은 면 숨기기를 다시 맞춤).
    /// </summary>
    public class BarrierView : MonoBehaviour, IUsesEdgeTransparency
    {
        [Tooltip("복셀 모델(방벽 판)")]
        [SerializeField] VoxPaint model;

        [Tooltip("붙은 면을 읽을 자리 컴포넌트")]
        [SerializeField] CellFaceMount mount;

        [Tooltip("가장자리 띠 4개(복셀 모델 아래): 판의 로컬 +Z, −Z, +Y, −Y 끝(서쪽 방벽이면 북, 남, 위, 아래)")]
        [SerializeField] GameObject[] sides = new GameObject[4];

        [Tooltip("모서리 4개(복셀 모델 아래): 로컬 (+Y, +Z), (+Y, −Z), (−Y, +Z), (−Y, −Z)")]
        [SerializeField] GameObject[] corners = new GameObject[4];

        // 띠·모서리 차례대로 판의 로컬 방향(서쪽 면 기준).
        static readonly Vector3[] SideAxes = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
        static readonly (Vector3 y, Vector3 z)[] CornerAxes =
            { (Vector3.up, Vector3.forward), (Vector3.up, Vector3.back), (Vector3.down, Vector3.forward), (Vector3.down, Vector3.back) };

        static readonly Dictionary<Vector3Int, List<BarrierView>> byCell = new();

        Vector3Int cell;

        /// <summary>모양(켜진 띠·모서리)이 바뀌었다.</summary>
        public static event Action<BarrierView> ShapeChanged;

        void Awake()
        {
            cell = CubeGridRules.CellAt(transform.position);
            if (!byCell.TryGetValue(cell, out var list)) byCell[cell] = list = new List<BarrierView>();
            list.Add(this);
            RefitCell(cell);
        }

        void OnDestroy()
        {
            if (!byCell.TryGetValue(cell, out var list)) return;
            list.Remove(this);
            if (list.Count == 0) byCell.Remove(cell);
            else RefitCell(cell);
        }

        void Start() => MarkEdges();

        static void RefitCell(Vector3Int cell)
        {
            if (byCell.TryGetValue(cell, out var list))
                foreach (var barrier in list) barrier.Refit();
        }

        // 띠는 그 끝이 닿는 면에 더 높은 방벽이 보이면 끄고, 모서리는 닿는 두 면 중 하나라도 더 높은 방벽이 보이면 끈다.
        void Refit()
        {
            if (mount == null) return;
            bool changed = false;
            for (int i = 0; i < sides.Length; i++)
                changed |= SetActive(sides[i], !Yields(FaceToward(SideAxes[i])));
            for (int i = 0; i < corners.Length; i++)
                changed |= SetActive(corners[i], !Yields(FaceToward(CornerAxes[i].y)) && !Yields(FaceToward(CornerAxes[i].z)));
            if (changed) ShapeChanged?.Invoke(this);
        }

        static bool SetActive(GameObject piece, bool active)
        {
            if (piece == null || piece.activeSelf == active) return false;
            piece.SetActive(active);
            return true;
        }

        // 그 면에 이 방벽보다 우선순위가 높은 방벽이 있는가.
        bool Yields(CellFace face)
        {
            foreach (var other in byCell[cell])
                if (other != this && other.mount != null && other.mount.Face == face && Priority(face) > Priority(mount.Face))
                    return true;
            return false;
        }

        // 묶음 테두리인 띠·모서리를 가장자리로 표시한다(모든 방벽이 등록된 뒤, Play 시작 때 한 번).
        void MarkEdges()
        {
            if (mount == null || !TryGetComponent(out LaserWall wall)) return;
            var rotation = CellFaceMount.Rotation(mount.Face);
            Vector3Int Axis(Vector3 local) => Vector3Int.RoundToInt(rotation * local);
            var center = BarrierGroup.FaceCenter(transform.position, mount.Face);
            var edge = new bool[sides.Length];
            for (int i = 0; i < sides.Length; i++)
            {
                var along = SideAxes[i].y != 0f ? Vector3.forward : Vector3.up; // 띠가 뻗은 방향
                var middle = center + Axis(SideAxes[i]);
                edge[i] = !SharedByGroup(wall, middle + Axis(along), middle - Axis(along));
                if (sides[i] != null) model.SetEdge(sides[i], edge[i]);
            }
            for (int i = 0; i < corners.Length; i++)
                if (corners[i] != null) model.SetEdge(corners[i], edge[SideIndex(CornerAxes[i].y)] || edge[SideIndex(CornerAxes[i].z)]);
        }

        static int SideIndex(Vector3 local) => Array.IndexOf(SideAxes, local);

        // 묶음의 다른 판이 두 점(판 꼭짓점, 두 배 좌표)을 모두 갖는가.
        static bool SharedByGroup(LaserWall wall, Vector3Int a, Vector3Int b)
        {
            foreach (var other in wall.Group.Members)
                if (other != wall && other.TryGetCorners(out var points) && Array.IndexOf(points, a) >= 0 && Array.IndexOf(points, b) >= 0)
                    return true;
            return false;
        }

        static int Priority(CellFace face) => face switch { CellFace.Up or CellFace.Down => 2, CellFace.North or CellFace.South => 1, _ => 0 };

        // 판의 로컬 방향(서쪽 면 기준)이 가리키는 칸의 면.
        CellFace FaceToward(Vector3 local)
        {
            var d = CellFaceMount.Rotation(mount.Face) * local;
            if (Mathf.Abs(d.x) > 0.5f) return d.x > 0f ? CellFace.East : CellFace.West;
            if (Mathf.Abs(d.y) > 0.5f) return d.y > 0f ? CellFace.Up : CellFace.Down;
            return d.z > 0f ? CellFace.North : CellFace.South;
        }
    }
}
