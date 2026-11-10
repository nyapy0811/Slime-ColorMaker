using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 노드 시스템 기물의 기반: 노드 데이터의 id를 기억하고 데이터로 모양을 만든다. 다시 만들 때 벽 빌더가 이 id로
    /// 지금 오브젝트를 찾아 남기므로, 손으로 넣는 값(요구 색 등)은 이 루트 오브젝트의 컴포넌트에 둔다.
    /// 루트 아래 '만든 부분'은 다시 만들 때 지워진다. 열림·막음 같은 동작은 같은 오브젝트에 붙는 문 계열 컴포넌트가 맡는다.
    /// </summary>
    public abstract class NodeObjectBase : MonoBehaviour
    {
        /// <summary>다시 만들 때 지우고 새로 채우는 자식의 이름.</summary>
        public const string GeneratedPartsName = "만든 부분";

        [Tooltip("노드 데이터의 id(벽 또는 끼운 문). 다시 만들 때 이 id로 오브젝트를 찾는다.")]
        [SerializeField] int dataId;

        public int DataId => dataId;

        public void SetDataId(int id)
        {
            NodeBuildOps.Record(this);
            dataId = id;
        }

        /// <summary>
        /// 구간을 따라 놓는다: <paramref name="position"/>에 두고 로컬 +X가 시작에서 끝을 향하게 돌린다.
        /// 위치는 노드 그래프 기준이다(벽 루트는 노드 그래프의 자식).
        /// </summary>
        protected void PlaceAlong(Vector3 position, Vector3 start, Vector3 end)
        {
            NodeBuildOps.Record(transform);
            transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(end - start) * Quaternion.Euler(0f, -90f, 0f));
        }
    }
}
