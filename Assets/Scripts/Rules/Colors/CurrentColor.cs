using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 플레이어가 든 지금 색(코어 규칙). (0,0,0)에서 시작해 흡수하면 더하고(채널마다 한도에서 자름),
    /// 문·목적지에 정확히 같은 값을 쓰면 비우고, 레이저 벽을 지나면 뺀다. 값이 바뀔 때만 <see cref="Changed"/>를 부른다.
    /// </summary>
    public sealed class CurrentColor
    {
        public CurrentColor(ColorValue limit) => Limit = limit;

        public ColorValue Value { get; private set; }
        public ColorValue Limit { get; }

        public event Action Changed;

        /// <summary>색 원천의 값을 더한다. 넘친 값은 사라진다.</summary>
        public void Absorb(ColorValue value) => Set(ColorRules.Clamp(Value + value, Limit));

        /// <summary>요구 색과 정확히 같으면 (0,0,0)으로 비우고 true(문, 목적지). 다르면 그대로 두고 false.</summary>
        public bool TrySpend(ColorValue required)
        {
            if (Value != required) return false;
            Set(ColorValue.Zero);
            return true;
        }

        /// <summary>레이저 벽을 지나 빼는 색만큼 뺀다. 모자란 채널은 0에서 멈춘다.</summary>
        public void Pass(ColorValue cost) => Set(ColorRules.Clamp(Value - cost, Limit));

        /// <summary>체크포인트에 저장했던 값으로 되돌린다.</summary>
        public void Restore(ColorValue value) => Set(value);

        void Set(ColorValue value)
        {
            if (value == Value) return;
            Value = value;
            Changed?.Invoke();
        }
    }
}
