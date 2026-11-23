using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 이어진 방벽 묶음: 판이 맞닿고(가장자리 하나 이상을 함께 가짐) 요구 색이 같은 닫힌 문을 이어 따라간 전부.
    /// 나란히 이어진 판, 같은 칸이나 옆 칸과 ㄱ자로 만나는 판, 등을 맞댄 판이 모두 이어진다. 색이 다른 방벽에서는 끊긴다.
    /// 문 하나를 열면 묶음 전체가 열리고, 가까운 값은 묶음에 하나만 보인다.
    /// 켜진 문을 모두 알고, 문이 켜지거나 꺼지거나 열림이 바뀌면 묶음을 다시 만든다(필요할 때 한 번).
    /// </summary>
    public sealed class DoorGroup
    {
        static readonly List<Door> doors = new();
        static int version;

        /// <summary>묶음의 문(닫힌 것만, 자기 포함).</summary>
        public readonly Door[] Members;

        /// <summary>문마다 막는 상자의 경계 상자(값 상자를 띄울 자리를 찾는 데 씀).</summary>
        public readonly Bounds[] Boxes;

        readonly int builtAt;

        DoorGroup(Door[] members)
        {
            Members = members;
            Boxes = new Bounds[members.Length];
            for (int i = 0; i < members.Length; i++) Boxes[i] = members[i].BlockerBox;
            builtAt = version;
        }

        bool IsCurrent => builtAt == version;

        public static void Register(Door door)
        {
            doors.Add(door);
            version++;
        }

        public static void Unregister(Door door)
        {
            doors.Remove(door);
            version++;
        }

        /// <summary>문의 열림이 바뀌었다(묶음을 다시 만든다).</summary>
        public static void Changed() => version++;

        /// <summary>문이 든 묶음. 지난 묶음이 아직 맞으면 그대로 쓴다.</summary>
        public static DoorGroup Of(Door door, DoorGroup cached)
        {
            if (cached != null && cached.IsCurrent) return cached;
            var members = new List<Door> { door };
            if (!door.IsOpen && door.TryGetCorners(out _))
            {
                // 판이 맞닿은(꼭짓점 둘 이상을 함께 가진) 같은 색 닫힌 문을 넓이 우선으로 따라간다.
                for (int i = 0; i < members.Count; i++)
                {
                    members[i].TryGetCorners(out var corners);
                    foreach (var other in doors)
                        if (!other.IsOpen && !members.Contains(other) && other.RequiredColor == door.RequiredColor
                            && other.TryGetCorners(out var otherCorners) && SharedCount(corners, otherCorners) >= 2)
                            members.Add(other);
                }
            }
            var group = new DoorGroup(members.ToArray());
            foreach (var member in group.Members) member.Group = group;
            return group;
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

        static int SharedCount(Vector3Int[] a, Vector3Int[] b)
        {
            int count = 0;
            foreach (var x in a)
                foreach (var y in b)
                    if (x == y) count++;
            return count;
        }

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
