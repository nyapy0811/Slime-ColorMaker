using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 미끄러운 코팅 설정(2026-11-14, 애셋은 Assets/Data). 종류(기름·얼음)마다 그 위에서 쓰는 관성 시간과 겉모습 재질.
    /// 재질의 색·반투명도·매끄러움·무늬(16 × 16, 칸마다 반복)는 재질 애셋에서 고친다.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/미끄러운 코팅 설정", fileName = "SlipperyCoatingSettings")]
    public class SlipperyCoatingSettings : ScriptableObject
    {
        [Serializable]
        public class Kind
        {
            [Tooltip("캐릭터 중심점이 이 코팅 위에 있을 때의 관성 시간(초). 출발·멈춤·방향 바꾸기와 벨트 관성이 모두 이 시간에 걸쳐 일어난다(보통 바닥은 이동 설정의 관성 시간).")]
            [Min(0.0001f)] public float inertiaTime = 1f;

            [Tooltip("코팅 층 재질(Slime~ColorMaker~/Lit Glass, 반투명, 렌더 큐 2998 = 바닥 흔적보다 먼저). 색·반투명도(알파)·매끄러움·무늬를 여기서 고친다.")]
            public Material material;
        }

        [Tooltip("기름 코팅")]
        public Kind oil = new() { inertiaTime = 1f };

        [Tooltip("얼음 코팅")]
        public Kind ice = new() { inertiaTime = 1.5f };

        public Kind For(CoatingKind kind) => kind switch
        {
            CoatingKind.Oil => oil,
            CoatingKind.Ice => ice,
            _ => null,
        };
    }
}
