using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 벽 구간: 양 끝 노드 id, 종류, 재질 번호, 끼운 문(구간당 최대 1개). 벽은 y가 같은 노드끼리만 잇는다.
    /// JsonUtility는 null 객체를 담지 못하므로 문이 있는지는 <see cref="hasDoor"/>로 나타낸다.
    /// </summary>
    [Serializable]
    public sealed class WallData
    {
        public int id;
        public int startNodeId;
        public int endNodeId;
        public WallKind kind;
        public int materialIndex;
        public bool hasDoor;
        public DoorData door = new();
    }
}
