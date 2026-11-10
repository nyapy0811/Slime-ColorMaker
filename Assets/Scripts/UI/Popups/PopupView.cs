using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 팝업 화면의 기반. 세션 종류마다 하나이고 팝업 호스트 아래에 두어 등록한다(6.8 '새 팝업').
    /// 판정과 상태는 세션에 있고 화면은 그리기만 한다. 켜고 끄는 것은 호스트가 한다.
    /// </summary>
    public abstract class PopupView : MonoBehaviour
    {
        /// <summary>이 세션을 그리는 화면인가.</summary>
        public abstract bool Accepts(object session);

        public abstract void Show(object session);

        /// <summary>닫히기 직전에 부른다.</summary>
        public virtual void Hide() { }

        /// <summary>줄의 칸을 모두 지운다. Destroy는 프레임 끝에 일어나므로 먼저 꺼서 줄 배치에서 뺀다.</summary>
        protected static void Clear(RectTransform row)
        {
            foreach (Transform child in row)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }
}
