using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 체크포인트에 저장하는 맵 상태. 지금은 자리만 두고 체크포인트 단계에서 채운다.
    /// JsonUtility로 저장하므로 공개 필드로 둔다.
    /// </summary>
    [Serializable]
    public sealed class MapSnapshot
    {
        public string mapId;
        public int mapVersion;

        public Vector3 playerPosition;
        /// <summary>몸통의 좌우 회전(도).</summary>
        public float playerYaw;
        /// <summary>카메라의 위아래 회전(도).</summary>
        public float playerPitch;

        /// <summary>지금 색.</summary>
        public ColorValue currentColor;

        public List<FixtureStateEntry> fixtures = new();
        public float playTime;
        public List<int> activatedCheckpointIds = new();
    }

    /// <summary>기물 하나의 상태: 맵 안 id와, 기물이 자기 상태를 내보낸 JSON 문자열.</summary>
    [Serializable]
    public sealed class FixtureStateEntry
    {
        public int fixtureId;
        public string state;
    }
}
