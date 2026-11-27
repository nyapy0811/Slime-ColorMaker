using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 모델의 큐브 상자들(이 오브젝트 = 모델 맨 위 오브젝트 기준, 유닛). .bbmodel 가져오기 코드가 붙인다.
    /// 큐브 맵이 칸의 픽셀 모양(겹침·맞닿음)을 셀 때 쓴다. 회전한 큐브는 돌린 꼭짓점을 감싼 상자다(90°씩 돌린 것은 정확함).
    /// </summary>
    public class VoxelBoxes : MonoBehaviour
    {
        [SerializeField] List<Bounds> boxes = new();

        public IReadOnlyList<Bounds> Boxes => boxes;

        public void Set(List<Bounds> value) => boxes = value;
    }
}
