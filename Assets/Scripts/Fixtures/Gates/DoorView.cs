using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 끼운 문의 겉모습(그림 0): 가운데서 갈라진 문짝 둘이 좌우 벽 속으로 문짝 폭만큼 밀려 들어간다(양쪽 미닫이, 모든 문 공통).
    /// 문 루트가 문 폭으로 늘어나 있으므로 문짝 위치는 루트 기준(문 폭 1)이다.
    /// </summary>
    public class DoorView : OpenableView
    {
        [SerializeField] Transform leftLeaf;
        [SerializeField] Transform rightLeaf;

        [Tooltip("다 열리거나 닫히는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.5f;

        float closedX;
        float openness;
        float target;

        void Awake()
        {
            closedX = rightLeaf.localPosition.x;
            enabled = false;
        }

        public override void SetOpen(bool open)
        {
            target = open ? 1f : 0f;
            enabled = true;
        }

        /// <summary>미닫이 없이 바로 열리거나 닫힌 모양이 된다(체크포인트 복귀).</summary>
        public override void SetOpenImmediate(bool open)
        {
            openness = target = open ? 1f : 0f;
            Apply();
            enabled = false;
        }

        void Update()
        {
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / duration);
            Apply();
            if (openness == target) enabled = false;
        }

        void Apply()
        {
            float x = closedX + openness * rightLeaf.localScale.x;
            Place(leftLeaf, -x);
            Place(rightLeaf, x);
        }

        static void Place(Transform leaf, float x)
        {
            var position = leaf.localPosition;
            position.x = x;
            leaf.localPosition = position;
        }
    }
}
