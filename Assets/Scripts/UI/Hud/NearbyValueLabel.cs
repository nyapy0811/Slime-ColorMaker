using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 가까운 값 표시(UIScene HUD, 2차 Q461 ← 2026-11-08 조준을 없앰). '가까운 값들 바뀜'을 받아 범위 안 기물마다 그 기물 위에
    /// 'R G B' 상자를 하나씩 보인다. 상자는 화면에 그리고(거리와 상관없이 크기가 같고 벽에 가려지지 않음), 매 프레임 기물 윗면에서
    /// 조금 위의 점을 지금 카메라(<see cref="Camera.main"/>)로 화면 자리로 바꿔 맞춘다. 카메라 뒤쪽 기물의 상자는 숨긴다.
    /// 팝업이 열리면 입력이 잠겨 값이 비므로 따로 듣지 않아도 숨는다.
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
            for (int i = 0; i < shown.Count; i++)
            {
                Vector3 screen = view != null ? view.WorldToScreenPoint(shown[i].Top + Vector3.up * heightAbove) : Vector3.back;
                bool visible = screen.z > 0f;
                labels[i].gameObject.SetActive(visible);
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local))
                    ((RectTransform)labels[i].transform).anchoredPosition = local;
            }
        }
    }
}
