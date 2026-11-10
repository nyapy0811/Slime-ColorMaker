using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>노드: id와 정수 좌표(월드 1유닛). 맵은 한 층이라 y는 늘 0이다.</summary>
    [Serializable]
    public sealed class NodeData
    {
        public int id;
        public Vector3Int position;
    }
}
