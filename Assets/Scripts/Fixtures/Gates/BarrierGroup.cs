using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 이어진 방벽 묶음: 판이 맞닿고(가장자리 하나 이상을 함께 가짐) 요구 색(빼는 색)이 같은 방벽(<see cref="LaserWall"/>)을 이어 따라간 전부.
    /// 나란히 이어진 판, 같은 칸이나 옆 칸과 ㄱ자로 만나는 판, 등을 맞댄 판이 모두 이어진다. 색이 다른 방벽에서는 끊긴다.
    /// 묶음은 레이저 벽 하나처럼 동작한다: 지나가면 색을 한 번만 빼고(위아래로 쌓인 판을 같이 지나도 한 번), 캐릭터가 묶음 안에 있는 동안은 다시 막지 않는다.
    /// 가까운 값은 묶음에 하나만 보이고, 바깥 테두리는 가장자리 투명도로 칠한다.
    /// 켜진 방벽을 모두 알고, 방벽이 켜지거나 꺼지면 묶음을 다시 만든다(필요할 때 한 번).
    /// </summary>
    public sealed class BarrierGroup
    {
        static readonly List<LaserWall> walls = new();
        static int version;

        /// <summary>묶음의 방벽(자기 포함).</summary>
        public readonly LaserWall[] Members;

        /// <summary>방벽마다 막는 상자의 경계 상자(값 상자를 띄울 자리를 찾는 데 씀).</summary>
        public readonly Bounds[] Boxes;

        readonly int builtAt;
        int inside;          // 캐릭터가 걸친 방벽 트리거 수
        Vector3 entryPoint;  // 묶음에 들어온 자리

        BarrierGroup(LaserWall[] members)
        {
            Members = members;
            Boxes = new Bounds[members.Length];
            for (int i = 0; i < members.Length; i++) Boxes[i] = members[i].BlockerBox;
            builtAt = version;
        }

        bool IsCurrent => builtAt == version;

        /// <summary>캐릭터가 묶음 안(방벽 트리거 하나 이상)에 있는가.</summary>
        public bool IsInside => inside > 0;

        public static void Register(LaserWall wall)
        {
            walls.Add(wall);
            version++;
        }

        public static void Unregister(LaserWall wall)
        {
            walls.Remove(wall);
            version++;
        }

        /// <summary>방벽이 든 묶음. 지난 묶음이 아직 맞으면 그대로 쓴다.</summary>
        public static BarrierGroup Of(LaserWall wall, BarrierGroup cached)
        {
            if (cached != null && cached.IsCurrent) return cached;
            var members = new List<LaserWall> { wall };
            if (wall.TryGetCorners(out _))
            {
                // 판이 맞닿은(꼭짓점 둘 이상을 함께 가진) 같은 색 방벽을 넓이 우선으로 따라간다.
                for (int i = 0; i < members.Count; i++)
                {
                    members[i].TryGetCorners(out var corners);
                    foreach (var other in walls)
                        if (!members.Contains(other) && other.RequiredColor == wall.RequiredColor
                            && other.TryGetCorners(out var otherCorners) && SharedCount(corners, otherCorners) >= 2)
                            members.Add(other);
                }
            }
            var group = new BarrierGroup(members.ToArray());
            foreach (var member in group.Members) member.Group = group;
            return group;
        }

        /// <summary>캐릭터가 방벽 하나의 트리거에 들어왔다. 묶음에 처음 들어온 자리를 기억한다.</summary>
        public void Enter(Vector3 position)
        {
            if (inside++ == 0) entryPoint = position;
        }

        /// <summary>
        /// 캐릭터가 방벽 하나(<paramref name="exited"/>)의 트리거에서 나갔다. 묶음을 다 벗어났고 들어온 자리와 반대쪽이면 지나간 것이다(true).
        /// </summary>
        public bool Exit(LaserWall exited, Vector3 position)
        {
            if (inside == 0) return false;
            if (--inside > 0) return false;
            return exited.Side(entryPoint) != exited.Side(position);
        }

        /// <summary>안에 있음을 잊는다(체크포인트 복귀: 순간이동은 트리거 나감을 보내지 않음).</summary>
        public void ResetInside() => inside = 0;

        static int SharedCount(Vector3Int[] a, Vector3Int[] b)
        {
            int count = 0;
            foreach (var x in a)
                foreach (var y in b)
                    if (x == y) count++;
            return count;
        }

        /// <summary>면의 바깥 방향.</summary>
        public static Vector3Int Normal(CellFace face) => face switch
        {
            CellFace.North => Vector3Int.forward,
            CellFace.East => Vector3Int.right,
            CellFace.South => Vector3Int.back,
            CellFace.West => Vector3Int.left,
            CellFace.Up => Vector3Int.up,
            _ => Vector3Int.down,
        };

        /// <summary>칸(바닥 가운데)의 면 가운데, 두 배 좌표: 칸 가운데 = 2 × 바닥 가운데 + (0,1,0), 면 가운데 = 그 + 법선.</summary>
        public static Vector3Int FaceCenter(Vector3 cell, CellFace face) => Vector3Int.RoundToInt(cell * 2f) + Vector3Int.up + Normal(face);

        /// <summary>
        /// 칸(<paramref name="cell"/>, 칸 바닥 가운데)의 <paramref name="face"/> 면 네 꼭짓점. 좌표는 두 배 해 정수로 둔다(칸 꼭짓점은 0.5 단위).
        /// </summary>
        public static Vector3Int[] FaceCorners(Vector3 cell, CellFace face)
        {
            var normal = Normal(face);
            var center = FaceCenter(cell, face);
            Vector3Int u = normal.x != 0 ? Vector3Int.up : Vector3Int.right;
            Vector3Int v = normal.z != 0 ? Vector3Int.up : new Vector3Int(0, 0, 1);
            return new[] { center + u + v, center + u - v, center - u + v, center - u - v };
        }
    }
}
