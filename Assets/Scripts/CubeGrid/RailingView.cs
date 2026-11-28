using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 철망 난간 하나(프리팹 `Railing`, 칸의 한 옆면)의 겉모습: 동쪽 끝 기둥(모델 부품 `PostEast`)을 켜고 끈다.
    /// 같은 면으로 이어진 옆 칸 난간이나 같은 칸 안쪽 모서리의 난간과 기둥이 맞닿으면 끈다(서쪽 기둥은 늘 켬).
    /// 값은 큐브 격자 빌더가 지을 때 정한다(<see cref="CubeGridRules.RailingEastPosts"/>).
    /// </summary>
    public class RailingView : MonoBehaviour
    {
        [Tooltip("동쪽 끝 기둥(모델 부품 PostEast)")]
        [SerializeField] GameObject eastPost;

        [Tooltip("동쪽 끝 기둥을 켤지. 큐브 격자 빌더가 정한다.")]
        [SerializeField, PerPlacement] bool eastPostShown = true;

        public GameObject EastPost => eastPost;

        public bool EastPostShown
        {
            get => eastPostShown;
            set
            {
                eastPostShown = value;
                Apply();
            }
        }

        public void Apply()
        {
            if (eastPost != null && eastPost.activeSelf != eastPostShown) eastPost.SetActive(eastPostShown);
        }
    }
}
