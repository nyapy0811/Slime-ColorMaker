using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 가까운 값 표시(UIScene HUD). '가까운 값들 바뀜'을 받아 범위 안 기물마다(이어진 방벽은 묶음마다) 그 기물 위에
    /// 'R G B' 상자를 하나씩 보인다(묶음은 판들 위에서 캐릭터와 가장 가까운 점, <see cref="NearbyValue.TopNear"/>). 상자는 화면에 그리고(거리와 상관없이 크기가 같고 벽에 가려지지 않음), 매 프레임 기물 윗면에서
    /// 조금 위의 점을 지금 카메라(<see cref="Camera.main"/>)로 화면 자리로 바꿔 맞춘다. 카메라 뒤쪽 기물의 상자는 숨긴다.
    /// 캐릭터와 기물의 거리에 따라 보이기 시작하는 거리에서 '제 크기 거리'까지 상자가 시작 크기에서 제 크기로 커지고 투명에서 불투명해진다
    /// (게임 규칙 <see cref="GameRulesConfig"/>, 투명도는 상자의 CanvasGroup).
    /// 입력이 잠기면 값이 비므로 따로 듣지 않아도 숨는다.
    /// </summary>
    public class NearbyValueLabel : MonoBehaviour
    {
        [Tooltip("본이 되는 값 상자(이 오브젝트의 자식). 필요한 만큼 복제해 쓰고 남는 것은 숨긴다. 기준점은 상자 아래 가운데.")]
        [SerializeField] ColorValueLabel label;

        [Tooltip("기물 콜라이더 윗면에서 상자 아래까지의 높이(유닛)")]
        [SerializeField] float heightAbove = 0.2f;

        readonly List<ColorValueLabel> labels = new();
        readonly List<NearbyValue> shown = new();

        void Awake()
        {
            label.Hide();
            labels.Add(label);
        }

        void OnEnable() => EventBus.Subscribe<NearbyValuesChanged>(OnNearbyValuesChanged);
        void OnDisable() => EventBus.Unsubscribe<NearbyValuesChanged>(OnNearbyValuesChanged);

        void OnNearbyValuesChanged(NearbyValuesChanged e)
        {
            shown.Clear();
            shown.AddRange(e.Values); // 보내는 쪽이 목록을 재사용하므로 복사해 둔다.
            while (labels.Count < shown.Count) labels.Add(Instantiate(label, label.transform.parent));
            for (int i = 0; i < labels.Count; i++)
            {
                if (i < shown.Count) labels[i].Show(shown[i].Value);
                else labels[i].Hide();
            }
            LateUpdate();
        }

        // 화면 위 오버레이 캔버스라 화면 좌표를 이 오브젝트(화면 가운데, 크기 0) 기준 자리로 바꾸면 된다.
        void LateUpdate()
        {
            var view = Camera.main;
            var area = (RectTransform)transform;
            var player = TouchInteractionController.Current;
            for (int i = 0; i < shown.Count; i++)
            {
                var top = shown[i].TopNear(player != null ? player.Center : shown[i].Box.center);
                Vector3 screen = view != null ? view.WorldToScreenPoint(top + Vector3.up * heightAbove) : Vector3.back;
                bool visible = screen.z > 0f;
                labels[i].gameObject.SetActive(visible);
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local))
                {
                    var rect = (RectTransform)labels[i].transform;
                    float near = Nearness(shown[i]);
                    rect.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, near);
                    labels[i].GetComponent<CanvasGroup>().alpha = near;
                    rect.anchoredPosition = SnapToPixels(area, rect, local);
                }
            }
        }

        static float StartScale => MapManager.Current != null ? MapManager.Current.Rules.valueStartScale : 1f;

        // 가까운 정도: 보이기 시작하는 거리에서 0, 제 크기 거리 안에서 1.
        static float Nearness(NearbyValue value)
        {
            var player = TouchInteractionController.Current;
            if (player == null || MapManager.Current == null) return 1f;
            var rules = MapManager.Current.Rules;
            return Mathf.InverseLerp(rules.interactDistance, rules.valueFullDistance, value.DistanceFrom(player.Center));
        }

        // 화면 픽셀 격자에 맞춘다: 상자 왼쪽 아래 모서리가 화면의 정수 픽셀에 오게 반올림한다. 그림 1픽셀은 늘 화면 n픽셀(PixelCanvasScaler)이라
        // 모서리만 정수 픽셀이면 테두리 굵기가 흔들리지 않고, 움직임은 화면 1픽셀씩이라 부드럽다(그림 픽셀 단위로 맞추면 n픽셀씩 끊겨 떨림).
        static Vector2 SnapToPixels(RectTransform area, RectTransform rect, Vector2 local)
        {
            var canvas = area.GetComponentInParent<Canvas>().rootCanvas;
            var root = (RectTransform)canvas.transform;
            Vector2 origin = (Vector2)root.InverseTransformPoint(area.position) - root.rect.min;
            Vector2 pivot = Vector2.Scale(rect.pivot, rect.rect.size) * rect.localScale.x;
            Vector2 corner = (origin + local - pivot) * canvas.scaleFactor; // 화면 픽셀
            corner = new Vector2(Mathf.Round(corner.x), Mathf.Round(corner.y)) / canvas.scaleFactor;
            return corner - origin + pivot;
        }
    }
}
