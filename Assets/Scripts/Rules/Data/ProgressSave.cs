using System;
using System.Collections.Generic;

namespace SlimeColorMaker
{
    /// <summary>
    /// v2 저장 데이터(colormaze_save.json, 결정 로그 3절): 맵별 기록과 지금 플레이 중인 맵의 마지막 체크포인트 하나.
    /// 형식 버전은 시맨틱 버전이고, 저장 시각은 나중의 클라우드 세이브에 맞추려고 둔다(2차 Q319).
    /// JsonUtility는 null 객체를 쓰지 못하므로 체크포인트가 있는지는 따로 든다.
    /// </summary>
    [Serializable]
    public sealed class ProgressSave
    {
        public const string CurrentVersion = "2.0.0";

        public string version = CurrentVersion;
        /// <summary>마지막으로 저장한 시각(UTC, ISO 8601).</summary>
        public string savedAt;
        public List<MapRecord> maps = new();
        public bool hasCheckpoint;
        public MapSnapshot checkpoint = new();

        public MapRecord FindMap(string mapId) => maps.Find(record => record.mapId == mapId);

        /// <summary>클리어를 기록한다. 최고 시간은 더 짧을 때만 바꾼다.</summary>
        public void RecordClear(string mapId, float clearTime)
        {
            var record = FindMap(mapId);
            if (record == null)
            {
                maps.Add(new MapRecord { mapId = mapId, cleared = true, bestTime = clearTime });
                return;
            }
            if (!record.cleared || clearTime < record.bestTime) record.bestTime = clearTime;
            record.cleared = true;
        }

        /// <summary>체크포인트를 저장한다. 다른 맵의 것이 있었어도 덮어쓴다(2차 Q449).</summary>
        public void SetCheckpoint(MapSnapshot snapshot)
        {
            hasCheckpoint = true;
            checkpoint = snapshot;
        }

        /// <summary>그 맵의 체크포인트를 지운다. 다른 맵의 것이면 그대로 둔다. 지웠으면 true.</summary>
        public bool ClearCheckpoint(string mapId)
        {
            if (!hasCheckpoint || checkpoint.mapId != mapId) return false;
            hasCheckpoint = false;
            checkpoint = new MapSnapshot();
            return true;
        }

        /// <summary>
        /// 그 맵의 체크포인트를 꺼낸다. 맵 버전이 지금 버전과 다르면 기록을 버리고 false를 돌려준다(3차 Q488).
        /// <paramref name="discarded"/>는 버렸는지(저장 파일에 써야 하는지)다.
        /// </summary>
        public bool TryGetCheckpoint(string mapId, int mapVersion, out MapSnapshot snapshot, out bool discarded)
        {
            snapshot = null;
            discarded = false;
            if (!hasCheckpoint || checkpoint.mapId != mapId) return false;
            if (checkpoint.mapVersion != mapVersion)
            {
                discarded = ClearCheckpoint(mapId);
                return false;
            }
            snapshot = checkpoint;
            return true;
        }

        /// <summary>
        /// 저장된 체크포인트의 맵이 맵 목록에 없거나(<paramref name="currentVersion"/>이 null) 맵 버전이 다르면 버린다(3차 Q488).
        /// 메뉴가 '이어하기'를 보일지 정하기 전에 부른다. 버렸으면 true.
        /// </summary>
        public bool DiscardStaleCheckpoint(int? currentVersion)
        {
            if (!hasCheckpoint || checkpoint.mapVersion == currentVersion) return false;
            return ClearCheckpoint(checkpoint.mapId);
        }

        /// <summary>그 맵을 처음부터 시작하기 전에 띄울 경고(2차 Q449, Q456).</summary>
        public MapStartWarning StartWarning(string mapId)
        {
            if (!hasCheckpoint) return MapStartWarning.None;
            return checkpoint.mapId == mapId ? MapStartWarning.ClearsThisCheckpoint : MapStartWarning.OverwritesOtherCheckpoint;
        }
    }

    /// <summary>맵을 처음부터 시작하기 전에 띄울 경고.</summary>
    public enum MapStartWarning
    {
        None,
        /// <summary>이 맵에 체크포인트가 저장돼 있다: 시작하면 지운다(2차 Q456).</summary>
        ClearsThisCheckpoint,
        /// <summary>다른 맵의 체크포인트가 저장돼 있다: 이 맵의 체크포인트에 닿으면 덮어쓴다(2차 Q449).</summary>
        OverwritesOtherCheckpoint,
    }

    /// <summary>맵 하나의 기록. 맵은 맵 목록의 고정 문자열 맵 id로 찾는다(3차 Q487).</summary>
    [Serializable]
    public sealed class MapRecord
    {
        public string mapId;
        public bool cleared;
        /// <summary>최고 클리어 시간(초). 클리어하지 않았으면 의미 없음.</summary>
        public float bestTime;
    }
}
