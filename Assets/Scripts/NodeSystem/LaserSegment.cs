using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 레이저 벽 구간의 모양. 레이저 프리팹의 루트에 있다. 노드 사이 구간 하나가 레이저 벽 하나이고,
    /// 레이저 면 하나를 구간 길이에 맞게 늘리며(겉모습과 콜라이더 모두 하나) 발사 장치는 늘리지 않고 구간 가운데 천장에 붙인다.
    /// 막음은 같은 오브젝트에 붙는 레이저 벽 컴포넌트(코어 루프 6단계, 색 시스템 개편)가 맡는다.
    /// </summary>
    public class LaserSegment : NodeObjectBase
    {
        [Tooltip("레이저 면. 두께는 프리팹 값을 쓰고 길이와 높이만 맞춘다.")]
        [SerializeField] Transform beam;

        [Tooltip("천장에 붙는 발사 장치")]
        [SerializeField] Transform emitter;

        public void Build(Vector3 start, Vector3 end, NodeBuildConfig config)
        {
            PlaceAlong((start + end) / 2f, start, end);
            float length = Vector3.Distance(start, end);
            int height = config.wallHeight;

            NodeBuildOps.Record(beam);
            beam.localPosition = new Vector3(0f, height / 2f, 0f);
            beam.localScale = new Vector3(length, height, beam.localScale.z);

            NodeBuildOps.Record(emitter);
            emitter.localPosition = new Vector3(0f, height - config.plateThickness - emitter.localScale.y / 2f, 0f);
        }
    }
}
