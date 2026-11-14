using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 방벽의 겉모습(모델 Models/Fixtures/Barrier): 열리면 반투명 판이 흐려지며 사라진다(복셀 모델의 반투명 정도를 0까지 내리고 끔).
    /// </summary>
    public class BarrierView : OpenableView
    {
        [Tooltip("흐려질 복셀 모델(방벽 판)")]
        [SerializeField] VoxPaint model;

        [Tooltip("다 사라지는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.4f;

        float closedOpacity;
        float openness;
        float target;

        void Awake()
        {
            closedOpacity = model.Opacity;
            enabled = false;
        }

        public override void SetOpen(bool open)
        {
            target = open ? 1f : 0f;
            model.gameObject.SetActive(true);
            enabled = true;
        }

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
            model.Opacity = closedOpacity * (1f - openness);
            model.gameObject.SetActive(openness < 1f);
        }
    }
}
