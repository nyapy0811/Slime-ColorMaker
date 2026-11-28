using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>설치 규칙의 검사.</summary>
    public enum PlacementCheck
    {
        [InspectorName("완전한 면")] FullFace,
        [InspectorName("같은 종류에 연결")] SameKind,
    }

    /// <summary>
    /// 설치 규칙의 방향. 아랫방향·윗방향·옆방향(북·동·남·서 중 아무 쪽)은 세계 기준이라 놓인 방향과 상관없고,
    /// 뒷면·앞면·왼쪽면·오른쪽면·윗면·아랫면은 기물 기준이라 놓인 회전 전체를 따라 돈다(모델의 앞 = +z, 위 = +y, 옆면 장식은 칸 바깥이 뒷면, 기둥은 놓인 모양).
    /// 값(번호)은 저장된 규칙과 맞도록 고정이다.
    /// </summary>
    public enum PlacementDirection
    {
        [InspectorName("아랫방향 (세계)")] Down = 0,
        [InspectorName("윗방향 (세계)")] Up = 1,
        [InspectorName("옆방향 (세계)")] AnySide = 6,
        [InspectorName("뒷면 (기물)")] Back = 2,
        [InspectorName("앞면 (기물)")] Front = 3,
        [InspectorName("왼쪽면 (기물)")] Left = 4,
        [InspectorName("오른쪽면 (기물)")] Right = 5,
        [InspectorName("윗면 (기물)")] TopFace = 7,
        [InspectorName("아랫면 (기물)")] BottomFace = 8,
    }

    /// <summary>
    /// 설치 규칙 하나(<see cref="CubeGridRules.Supported"/>).
    /// 완전한 면은 그 방향 옆 칸 지형(블록·계단·기둥)의 칸 경계 면(16 × 16 픽셀 한 겹)이 꽉 찼을 때 맞는다.
    /// 같은 종류에 연결은 그 방향 옆(상자는 반 칸)에 같은 종류가 있을 때 맞는다.
    /// </summary>
    [Serializable]
    public struct PlacementRule
    {
        [Tooltip("완전한 면: 그 방향 옆 칸 지형(블록·계단·기둥)의 칸 경계 면 16 × 16 픽셀이 꽉 찼다. 같은 종류에 연결: 그 방향 옆(상자는 반 칸)에 같은 종류가 있다.")]
        public PlacementCheck check;

        [Tooltip("세계 기준(아랫방향·윗방향·옆방향 = 북·동·남·서 중 하나라도)이나 기물 기준(뒷면·앞면·왼쪽면·오른쪽면·윗면·아랫면, 놓인 회전을 따라 돎).")]
        public PlacementDirection direction;

        public PlacementRule(PlacementCheck check, PlacementDirection direction)
        {
            this.check = check;
            this.direction = direction;
        }
    }
}
