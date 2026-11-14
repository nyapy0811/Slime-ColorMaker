using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 닿으면 상호작용. 캐릭터 몸(CharacterController 캡슐)을 조금 키운 모양이 상호작용하는 기물(색 원천, 문, 목적지)에
    /// 새로 닿은 순간 한 번 상호작용한다(계속 닿아 있으면 되풀이하지 않고, 떨어졌다 다시 닿으면 또 함). 트리거(오브)도 닿음으로 친다.
    /// 몸 모양을 쓰므로 막힌 기물 앞에서 멈춘 자리에서 방향과 상관없이 닿고, 캐릭터 크기를 바꿔도 닿는 범위가 따라간다.
    /// 상호작용 거리 안에서 값이 있는 기물 전부의 (윗면 가운데, 값)을 '가까운 값들 바뀜'으로 보낸다(목록이 달라질 때만).
    /// 입력이 잠겨 있거나 일시정지 중이면 상호작용하지 않고 값도 숨긴다(화면 페이드·일시정지·클리어 화면).
    /// 기물 레이어(<see cref="FixtureBase.LayerName"/>)만 찾아 블록은 결과에 들지 않는다. 매 프레임 도는 코드라 결과 배열·목록은 재사용한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerColor))]
    public class TouchInteractionController : MonoBehaviour
    {
        [Tooltip("닿음을 찾을 때 캐릭터 몸(반지름 + 피부 두께)을 둘레로 이만큼(유닛) 키운다. 막힌 기물(문·목적지)은 겉면에서 멈추므로 밀거나 위에 서도 닿게 한다.")]
        [SerializeField] float touchMargin = 0.05f;

        readonly HashSet<IInteractable> touching = new();
        readonly HashSet<IInteractable> touchingNow = new();
        readonly HashSet<IValueProvider> seen = new();
        readonly List<NearbyValue> found = new();
        readonly List<NearbyValue> shown = new();
        readonly List<Collider> parts = new();
        Collider[] hits = new Collider[16];
        int fixtureMask;
        CharacterController body;
        PlayerContext context;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            context = new PlayerContext(GetComponent<PlayerColor>());
            fixtureMask = LayerMask.GetMask(FixtureBase.LayerName);
            if (fixtureMask == 0) Debug.LogError($"[TouchInteractionController] '{FixtureBase.LayerName}' 레이어가 없다(Project Settings > Tags and Layers).", this);
        }

        // 이동(Update) 뒤에 잰다.
        void LateUpdate()
        {
            bool idle = InputManager.Instance.IsGameplayLocked || Time.timeScale == 0f;
            if (!idle) Touch();
            ReportValue(idle);
        }

        Vector3 Center => transform.TransformPoint(body.center);

        void Touch()
        {
            // 캡슐의 양 끝 구 중심. 높이가 지름보다 작으면 구 하나다(CharacterController와 같음).
            var half = transform.up * Mathf.Max(body.height / 2f - body.radius, 0f);
            float radius = body.radius + body.skinWidth + touchMargin;
            int count;
            // 결과가 배열을 꽉 채우면 빠진 것이 있을 수 있으므로 두 배로 늘려 다시 찾는다.
            while ((count = Physics.OverlapCapsuleNonAlloc(Center + half, Center - half, radius, hits, fixtureMask, QueryTriggerInteraction.Collide)) == hits.Length)
                hits = new Collider[hits.Length * 2];
            touchingNow.Clear();
            for (int i = 0; i < count; i++)
                if (hits[i].GetComponentInParent<IInteractable>() is { } interactable) touchingNow.Add(interactable);
            foreach (var interactable in touchingNow)
                if (!touching.Contains(interactable) && interactable.CanInteract) interactable.Interact(context);
            touching.Clear();
            touching.UnionWith(touchingNow);
        }

        // 흡수 직후처럼 기물의 값이 없어져도 여기서 잡힌다. 보내는 목록은 재사용하므로 받는 쪽이 복사해 둔다.
        void ReportValue(bool idle)
        {
            found.Clear();
            if (!idle) FindValues();
            if (SameValues(found, shown)) return;
            shown.Clear();
            shown.AddRange(found);
            EventBus.Publish(new NearbyValuesChanged(shown));
        }

        // 거리 안에 콜라이더가 하나라도 걸친 기물. 자리는 그 기물 콜라이더 전체의 경계 상자 윗면 가운데.
        void FindValues()
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(Center, MapManager.Current.Rules.interactDistance, hits, fixtureMask, QueryTriggerInteraction.Collide)) == hits.Length)
                hits = new Collider[hits.Length * 2];
            seen.Clear();
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.GetComponentInParent<IValueProvider>() is not { } provider || !seen.Add(provider)) continue;
                if (!provider.TryGetDisplayValue(out var value)) continue;
                var bounds = hit.bounds;
                ((Component)provider).GetComponentsInChildren(parts);
                foreach (var part in parts)
                    if (part.enabled) bounds.Encapsulate(part.bounds);
                found.Add(new NearbyValue(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), value));
            }
        }

        // 물리 결과의 차례는 프레임마다 다를 수 있어 차례와 상관없이 비교한다.
        static bool SameValues(List<NearbyValue> a, List<NearbyValue> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var x in a)
            {
                bool match = false;
                foreach (var y in b)
                    if (y.Top == x.Top && y.Value == x.Value) { match = true; break; }
                if (!match) return false;
            }
            return true;
        }
    }
}
