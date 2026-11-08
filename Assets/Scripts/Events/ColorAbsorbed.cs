using Framework.Core;
using UnityEngine;

namespace ColorMaze
{
    /// <summary>색 원천에서 색을 흡수함. 보내는 곳: 색 원천. 받는 곳: 소리 연결.</summary>
    public readonly struct ColorAbsorbed : IEvent
    {
        /// <summary>흡수한 색 원천의 위치(연출이 날아오는 곳).</summary>
        public readonly Vector3 Position;
        public readonly ColorValue Value;

        public ColorAbsorbed(Vector3 position, ColorValue value)
        {
            Position = position;
            Value = value;
        }
    }
}
