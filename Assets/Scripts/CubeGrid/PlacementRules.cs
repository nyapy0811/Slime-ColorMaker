using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 프리팹의 설치 규칙(장식·기물 프리팹 맨 위에 붙임). 큐브 맵 편집 도구가 놓을 때 보고, 규칙 중 하나라도 맞으면 놓는다(없으면 어디든).
    /// 편집마다 규칙에 맞지 않게 된 것(받침이 사라짐)은 지우고, 그것이 받침이던 것도 이어서 지운다.
    /// </summary>
    public class PlacementRules : MonoBehaviour
    {
        [Tooltip("하나라도 맞으면 놓인다. 비우면 어디든 놓인다.")]
        [SerializeField] List<PlacementRule> rules = new();

        public IReadOnlyList<PlacementRule> Rules => rules;

        public void Set(List<PlacementRule> value) => rules = value;
    }
}
