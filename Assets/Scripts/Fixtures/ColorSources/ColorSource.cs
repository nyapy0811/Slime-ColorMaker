using System;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 색 원천. 프리팹(지금은 오브 하나)에 붙인다. 복셀 모델을 값의 색(가장 큰 채널을 최대 밝기로 한 비율 × 오브젝트 밝기)으로
    /// 칠하고(<see cref="VoxPaint"/>로, 에디터에서도 보임), 닿으면 값을 지금 색에 바로 더한다(채널마다 한도에서 자름). 한 번만 흡수할 수 있고,
    /// 흡수하면 흑백이 되고 강조를 끄고 '색 흡수'를 보낸다. 가까이 가면 흡수 전에만 값을 보인다. 체크포인트에는 흡수했는지를 저장한다.
    /// </summary>
    public class ColorSource : FixtureBase, IInteractable, IValueProvider, ISnapshotable
    {
        [Serializable]
        struct State
        {
            public bool absorbed;
        }

        [Tooltip("흡수되는 값. (0,0,0)과 세 채널이 같은 값은 쓰지 않는다(맵 검사가 알림).")]
        [SerializeField] ColorValue value = new(3, 0, 0);

        [Tooltip("값의 색으로 칠할 복셀 모델")]
        [SerializeField] VoxPaint paint;

        [Tooltip("오브젝트 밝기(아트용, 값과 무관)")]
        [SerializeField, Range(0f, 1f)] float brightness = 1f;

        bool absorbed;

        public ColorValue Value => value;
        public bool CanInteract => !absorbed;

        public bool TryGetDisplayValue(out ColorValue shown)
        {
            shown = value;
            return !absorbed;
        }

        void Awake() => Paint();

        void OnValidate() => Paint();

        public void Interact(PlayerContext player)
        {
            if (absorbed) return;
            player.Color.Absorb(value);
            absorbed = true;
            Paint();
            RefreshHighlight();
            EventBus.Publish(new ColorAbsorbed(Center, value));
        }

        public string CaptureState() => JsonUtility.ToJson(new State { absorbed = absorbed });

        public void RestoreState(string state)
        {
            absorbed = JsonUtility.FromJson<State>(state).absorbed;
            Paint();
            RefreshHighlight();
        }

        Vector3 Center => paint != null ? paint.GetComponentInChildren<Renderer>().bounds.center : transform.position;

        // 흡수 전은 값의 색, 흡수한 뒤는 그 색의 흑백.
        void Paint()
        {
            if (paint == null) return;
            var color = ColorRules.ToColor(value) * brightness;
            color.a = 1f;
            if (absorbed) color = ColorRules.ToGrayscale(color);
            paint.Color = color;
        }
    }
}
