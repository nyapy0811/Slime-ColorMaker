using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// v2 기물의 기반. 맵 안 id(맵 안에서 1부터 늘어나는 정수, 에디터가 발급)를 들고, 켜질 때 맵 매니저에 등록한다.
    /// 상호작용하는 기물(<see cref="IInteractable"/>)은 상호작용할 수 있는 동안 강조를 켠다.
    /// 상태 저장 기본 구현은 체크포인트 단계에서 더한다.
    /// </summary>
    public abstract class FixtureBase : MonoBehaviour
    {
        /// <summary>
        /// 상호작용하거나 값을 보이는 기물의 콜라이더가 있을 레이어. 캐릭터의 닿음·가까운 값 찾기가 이 레이어만 보므로 블록이 결과에 들지 않는다
        /// (맵 검사가 알림, 2026-11-08).
        /// </summary>
        public const string LayerName = "Interactable";

        [Tooltip("맵 안 id. 메뉴 Slime~ColorMaker~/맵 안 id 고치기로 발급한다.")]
        [SerializeField] int fixtureId;

        [Tooltip("상호작용 강조의 겉모습. 상호작용하는 기물에만 단다.")]
        [SerializeField] InteractHighlightView highlight;

        public int FixtureId => fixtureId;

        protected virtual void Start() => RefreshHighlight();

        /// <summary>'상호작용할 수 있는가'가 바뀌면 부른다. 강조를 켜고 끈다.</summary>
        protected void RefreshHighlight()
        {
            if (highlight != null) highlight.SetOn(this is IInteractable { CanInteract: true });
        }

        protected virtual void OnEnable()
        {
            if (MapManager.Current != null) MapManager.Current.Register(this);
        }

        protected virtual void OnDisable()
        {
            if (MapManager.Current != null) MapManager.Current.Unregister(this);
        }
    }
}
