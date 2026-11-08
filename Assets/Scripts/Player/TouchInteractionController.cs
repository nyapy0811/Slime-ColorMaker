using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 닿으면 상호작용(2026-11-08, 조준+좌클릭을 대신함). 캐릭터의 상자 콜라이더가 상호작용하는 기물(색 원천, 문, 목적지)에
    /// 새로 닿은 순간 한 번 상호작용한다(계속 닿아 있으면 되풀이하지 않고, 떨어졌다 다시 닿으면 또 함). 트리거(오브)도 닿음으로 친다.
    /// 상호작용 거리 안에서 값이 있는 기물 전부의 (윗면 가운데, 값)을 '가까운 값들 바뀜'으로 보낸다(목록이 달라질 때만).
    /// 입력이 잠겨 있거나 일시정지 중이면 상호작용하지 않고 값도 숨긴다(팝업·일시정지·클리어 화면).
    /// </summary>
    [RequireComponent(typeof(BoxCollider), typeof(PlayerColor))]
    public class TouchInteractionController : MonoBehaviour
    {
        [Tooltip("닿음을 찾을 때 상자를 둘레로 이만큼(유닛) 키운다. 막힌 기물(문·목적지)은 겉면에서 멈추므로 밀거나 위에 서도 닿게 한다.")]
        [SerializeField] float touchMargin = 0.05f;

        readonly HashSet<IInteractable> touching = new();
        readonly HashSet<IInteractable> touchingNow = new();
        BoxCollider box;
        PlayerContext context;
        List<NearbyValue> shown = new();

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            context = new PlayerContext(GetComponent<PlayerColor>());
        }

        // 이동(Update) 뒤에 잰다.
        void LateUpdate()
        {
            bool idle = InputManager.Instance.IsGameplayLocked || Time.timeScale == 0f;
            if (!idle) Touch();
            ReportValue(idle);
        }

        Vector3 Center => transform.TransformPoint(box.center);

        void Touch()
        {
            var halfExtents = Vector3.Scale(box.size, transform.lossyScale) / 2f + Vector3.one * touchMargin;
            touchingNow.Clear();
            foreach (var hit in Physics.OverlapBox(Center, halfExtents, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                if (hit.GetComponentInParent<IInteractable>() is { } interactable) touchingNow.Add(interactable);
            foreach (var interactable in touchingNow)
                if (!touching.Contains(interactable) && interactable.CanInteract) interactable.Interact(context);
            touching.Clear();
            touching.UnionWith(touchingNow);
        }

        // 흡수 직후처럼 기물의 값이 없어져도 여기서 잡힌다.
        void ReportValue(bool idle)
        {
            var found = idle ? new List<NearbyValue>() : FindValues();
            if (SameValues(found, shown)) return;
            shown = found;
            EventBus.Publish(new NearbyValuesChanged(found));
        }

        // 거리 안에 콜라이더가 하나라도 걸친 기물. 자리는 그 기물 콜라이더 전체의 경계 상자 윗면 가운데.
        List<NearbyValue> FindValues()
        {
            var found = new List<NearbyValue>();
            var seen = new HashSet<IValueProvider>();
            foreach (var hit in Physics.OverlapSphere(Center, MapManager.Current.Rules.interactDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (hit.GetComponentInParent<IValueProvider>() is not { } provider || !seen.Add(provider)) continue;
                if (!provider.TryGetDisplayValue(out var value)) continue;
                var bounds = hit.bounds;
                foreach (var part in ((Component)provider).GetComponentsInChildren<Collider>())
                    if (part.enabled) bounds.Encapsulate(part.bounds);
                found.Add(new NearbyValue(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), value));
            }
            return found;
        }

        // 물리 결과의 차례는 프레임마다 다를 수 있어 차례와 상관없이 비교한다.
        static bool SameValues(List<NearbyValue> a, List<NearbyValue> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var x in a)
                if (!b.Exists(y => y.Top == x.Top && y.Value == x.Value)) return false;
            return true;
        }
    }
}
