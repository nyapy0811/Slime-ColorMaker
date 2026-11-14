using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>맵을 클리어함. 보내는 곳: 맵 매니저. 받는 곳: 클리어 화면, 소리 연결.</summary>
    public readonly struct MapCleared : IEvent
    {
        public readonly string MapId;

        /// <summary>클리어 시간(초). 일시정지 중에는 멈춘다.</summary>
        public readonly float ClearTime;

        public MapCleared(string mapId, float clearTime)
        {
            MapId = mapId;
            ClearTime = clearTime;
        }
    }
}
