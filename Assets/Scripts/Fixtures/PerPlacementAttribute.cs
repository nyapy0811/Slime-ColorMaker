using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 놓을 때마다(맵 위의 하나하나가) 정하는 값이라는 표시: 색, 방향·모양, 맵 안 id 등.
    /// 프리팹 설정 창은 이 표시가 붙은 필드를 보이지 않는다(프리팹 공통 설정만 보임).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class PerPlacementAttribute : Attribute
    {
    }
}
