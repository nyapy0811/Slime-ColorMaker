using System;

namespace SlimeColorMaker
{
    /// <summary>
    /// 일반 벽 구간에 끼운 문. 위치는 구간의 첫 벽 모듈부터 센 0.25유닛 모듈 수(문의 시작 쪽 가장자리)라
    /// 문 양옆이 늘 모듈 경계와 맞는다. 문 설정은 문 계열을 만들 때(코어 루프 6단계) 더한다.
    /// </summary>
    [Serializable]
    public sealed class DoorData
    {
        public int id;
        public int offsetModules;
    }
}
