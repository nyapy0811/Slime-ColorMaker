using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 요구 색을 가진 기물의 기반(문, 레이저 벽, 목적지). 요구 색의 렌더러와 복셀 모델(<see cref="VoxPaint"/>, 목적지의 므엥킹)을
    /// 요구 색(가장 큰 채널을 최대 밝기로 한 비율)으로 칠하고(에디터에서도 보임),
    /// 가까이 가면 요구 색을 보인다. 요구 색을 어떻게 쓰는지는 종류마다 다르다(문·목적지는 정확히 같아야 하고, 레이저 벽은 뺀다).
    /// </summary>
    public abstract class RequiredColorBase : FixtureBase, IValueProvider
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("요구 색. 문·목적지는 모든 채널 1 이상·세 채널이 모두 같지 않음·한도 안, 레이저 벽(빼는 색)은 (0,0,0)과 세 채널이 같은 값이 아니고 한도 안(맵 검사가 알림).")]
        [SerializeField] ColorValue requiredColor = new(6, 3, 3);

        [Tooltip("요구 색으로 칠할 렌더러")]
        [SerializeField] Renderer[] requiredColorRenderers;

        [Tooltip("요구 색으로 칠할 복셀 모델(없으면 비워 둠). 복셀 모델은 렌더러를 직접 칠하지 않고 VoxPaint로 칠한다.")]
        [SerializeField] VoxPaint requiredColorModel;

        MaterialPropertyBlock block;

        public ColorValue RequiredColor => requiredColor;

        protected virtual void Awake() => Paint();

        void OnValidate() => Paint();

        public virtual bool TryGetDisplayValue(out ColorValue value)
        {
            value = requiredColor;
            return true;
        }

        void Paint()
        {
            if (requiredColorModel != null) requiredColorModel.Color = ColorRules.ToColor(requiredColor);
            if (requiredColorRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var target in requiredColorRenderers)
            {
                if (target == null) continue;
                target.GetPropertyBlock(block);
                block.SetColor(BaseColorId, ColorRules.ToColor(requiredColor));
                target.SetPropertyBlock(block);
            }
        }
    }
}
