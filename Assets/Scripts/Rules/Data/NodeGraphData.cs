using System;
using System.Collections.Generic;

namespace SlimeColorMaker
{
    /// <summary>
    /// 벽 노드 시스템의 데이터: 노드 목록, 벽 목록, 다음 id. 유니티 에디터 맵의 노드 그래프와
    /// UGC 맵 파일이 같은 클래스를 쓴다. id는 노드·벽·끼운 문이 함께 쓰는 번호로, 1부터 늘어나고 지운 번호는 다시 쓰지 않는다.
    /// </summary>
    [Serializable]
    public sealed class NodeGraphData
    {
        public List<NodeData> nodes = new();
        public List<WallData> walls = new();
        public int nextId = 1;
    }
}
