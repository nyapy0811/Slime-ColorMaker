using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵에 놓은 시험 모델(아트 모델을 맵에서 바로 보는 용도, 기물 아님). 칸 바닥 가운데에 놓이고 칸 하나를 차지하며,
    /// 충돌은 모델 모양과 상관없이 한 칸(16 × 16 × 16복셀 = 1유닛) 상자다. 모델은 이 오브젝트의 자식이다.
    /// 큐브 맵 편집 도구와 맵 검사가 이 컴포넌트로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class CubeTestModel : MonoBehaviour
    {
    }
}
