using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 거부 효과(임시): 이 오브젝트를 자기 오른쪽 방향으로 짧게 흔들고 제자리로 돌려놓는다.
    /// 문·목적지가 지금 색을 받아들이지 않을 때 쓴다. 연출이 정해지면 이 컴포넌트만 바꾼다.
    /// </summary>
    public class ShakeView : MonoBehaviour
    {
        [SerializeField] float distance = 0.03f;
        [SerializeField] float duration = 0.3f;

        Vector3 restPosition;
        float timer;

        void Awake() => enabled = false;

        public void Shake()
        {
            if (!enabled) restPosition = transform.localPosition;
            timer = 0f;
            enabled = true;
        }

        void Update()
        {
            float progress = Mathf.Clamp01(timer / duration);
            timer += Time.deltaTime;
            transform.localPosition = restPosition;
            if (progress >= 1f)
            {
                enabled = false;
                return;
            }
            transform.position += transform.right * (Mathf.Sin(progress * 40f) * distance * (1f - progress));
        }
    }
}
