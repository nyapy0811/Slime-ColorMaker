using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 문 계열의 기반(6.5, 색 시스템 개편): 요구 색과 길을 막는 콜라이더. 막음이 바뀌면 콜라이더만 켜고 끄고,
    /// 겉모습과 언제 막는지는 종류마다 다르다(문은 열면 계속 열림, 레이저 벽은 지금 색에 따라). 루트와 스크립트는 늘 켜 둔다(2차 Q277).
    /// </summary>
    public abstract class GateBase : RequiredColorBase
    {
        [Tooltip("막을 때 켜는 콜라이더")]
        [SerializeField] BoxCollider[] blockers;

        /// <summary>지금 길을 막는가. 처음에는 막는다.</summary>
        public bool IsBlocking { get; private set; } = true;

        protected IReadOnlyList<BoxCollider> Blockers => blockers;

        protected void SetBlocking(bool blocking)
        {
            IsBlocking = blocking;
            foreach (var blocker in blockers) blocker.enabled = blocking;
        }
    }
}
