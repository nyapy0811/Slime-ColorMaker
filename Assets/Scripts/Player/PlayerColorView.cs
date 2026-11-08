using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 지금 색을 캐릭터 몸 색으로 보인다(2026-11-08, 1인칭 뷰모델의 구를 대신함). '지금 색 바뀜'을 받아 캐릭터 모델의
    /// <see cref="VoxPaint"/> 색을 바꾼다(값 색·반투명 값 색 칸만, 회색 칸은 그대로). 색은 HUD 색 견본과 같이 값 ÷ 15 밝기이고
    /// (0,0,0)은 검정이다.
    /// </summary>
    [RequireComponent(typeof(VoxPaint))]
    public class PlayerColorView : MonoBehaviour
    {
        VoxPaint paint;

        void Awake() => paint = GetComponent<VoxPaint>();

        void OnEnable() => EventBus.Subscribe<CurrentColorChanged>(OnCurrentColorChanged);
        void OnDisable() => EventBus.Unsubscribe<CurrentColorChanged>(OnCurrentColorChanged);

        void OnCurrentColorChanged(CurrentColorChanged e) => paint.Color = ColorRules.ToColor(e.Value);
    }
}
