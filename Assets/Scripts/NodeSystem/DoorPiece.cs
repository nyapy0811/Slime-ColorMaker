using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 일반 벽 구간에 끼운 문의 모양. 문 프리팹의 루트에 있고, 일반 벽 구간이 자식으로 만들어 문 자리에 놓는다.
    /// 끼운 문의 id로 남으므로 열림을 맡는 문 컴포넌트(코어 루프 6단계)를 같은 오브젝트에 붙인다.
    /// 임시 프리팹은 폭·높이·두께가 1인 상자 문이고(피벗은 문 아래 가운데) 기준 치수로 늘린다.
    /// </summary>
    public class DoorPiece : NodeObjectBase
    {
        /// <summary>벽 구간 루트 기준으로 문 자리에 놓는다. <paramref name="center"/>는 구간 시작 노드에서 문 가운데까지의 거리다.</summary>
        public void Build(float center, float width, NodeBuildConfig config)
        {
            NodeBuildOps.Record(transform);
            transform.SetLocalPositionAndRotation(new Vector3(center, 0f, 0f), Quaternion.identity);
            transform.localScale = new Vector3(width, config.doorHeight, config.wallThickness);
        }
    }
}
