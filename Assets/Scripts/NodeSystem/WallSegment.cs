using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 일반 벽 구간. 루트는 시작 노드에 서서 로컬 +X가 끝 노드를 향한다. '만든 부분' 아래에 0.25 벽 모듈을 반복하고
    /// (남는 길이는 양 끝 기둥이 가림), 콜라이더 상자를 구간마다 하나 둔다(문이 있으면 문 양옆 둘과 문 위 벽 하나).
    /// 끼운 문은 '만든 부분' 밖의 자식이라 문 id가 같으면 다시 만들어도 남는다.
    /// </summary>
    public class WallSegment : NodeObjectBase
    {
        public void Build(WallData wall, Vector3 start, Vector3 end, NodeBuildConfig config)
        {
            PlaceAlong(start, start, end);
            float length = Vector3.Distance(start, end);
            int height = config.wallHeight;
            int doorModules = NodeGraphRules.DoorModules(config.doorWidth);
            float doorWidth = doorModules * NodeGraphRules.ModuleLength;
            float doorStart = NodeGraphRules.DoorStart(length, wall.door.offsetModules);

            var parts = NodeBuildOps.ReplaceChild(transform, GeneratedPartsName);
            float first = NodeGraphRules.FirstModuleStart(length);
            int count = NodeGraphRules.ModuleCount(length);
            for (int i = 0; i < count; i++)
            {
                // 문 자리의 모듈은 문 위 벽이 된다.
                bool aboveDoor = wall.hasDoor && i >= wall.door.offsetModules && i < wall.door.offsetModules + doorModules;
                float bottom = aboveDoor ? config.doorHeight : 0f;
                var module = NodeBuildOps.Spawn(config.wallModulePrefab, parts).transform;
                module.localPosition = new Vector3(first + (i + 0.5f) * NodeGraphRules.ModuleLength, (bottom + height) / 2f, 0f);
                module.localScale = new Vector3(NodeGraphRules.ModuleLength, height - bottom, config.wallThickness);
            }

            foreach (var (from, to) in NodeGraphRules.SolidSpans(length, wall.hasDoor, config.doorWidth, wall.door.offsetModules))
                AddBox(parts, from, to, 0f, height, config.wallThickness);
            if (wall.hasDoor) AddBox(parts, doorStart, doorStart + doorWidth, config.doorHeight, height, config.wallThickness);
            NodeBuildOps.RegisterCreated(parts.gameObject);

            BuildDoor(wall, doorStart + doorWidth / 2f, doorWidth, config);
        }

        static void AddBox(Transform parts, float from, float to, float bottom, float top, float thickness)
        {
            var box = parts.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3((from + to) / 2f, (bottom + top) / 2f, 0f);
            box.size = new Vector3(to - from, top - bottom, thickness);
        }

        // 문 id가 같은 끼운 문은 남겨 자리만 옮기고, 다른 것은 지운다.
        void BuildDoor(WallData wall, float center, float width, NodeBuildConfig config)
        {
            DoorPiece door = null;
            foreach (var piece in GetComponentsInChildren<DoorPiece>(true))
            {
                if (wall.hasDoor && door == null && piece.DataId == wall.door.id) door = piece;
                else NodeBuildOps.Destroy(piece.gameObject);
            }
            if (!wall.hasDoor) return;

            if (door == null)
            {
                door = NodeBuildOps.Spawn(config.doorPrefab, transform).GetComponent<DoorPiece>();
                door.name = $"Door {wall.door.id}";
                door.SetDataId(wall.door.id);
                NodeBuildOps.RegisterCreated(door.gameObject);
            }
            door.Build(center, width, config);
        }
    }
}
