using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵마다 하나 있는 벽 노드 그래프. 노드·벽 데이터를 들고, 벽 빌더(<see cref="NodeGraphBuilder"/>)가 이 오브젝트 아래에
    /// 벽·기둥·바닥·천장을 만든다. 노드 좌표는 이 오브젝트 기준이다. 만든 벽이 프리팹 오버라이드가 되지 않게
    /// 맵 기본 프리팹(MapRoot) 밖에 따로 둔다. 데이터는 메뉴 Slime~ColorMaker~/노드 편집에서 고친다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NodeGraph : MonoBehaviour
    {
        [SerializeField] NodeGraphData data = new();

        public NodeGraphData Data => data;
    }
}
