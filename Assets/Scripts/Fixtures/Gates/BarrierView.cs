using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 방벽의 겉모습(모델 Models/Fixtures/Barrier): 열리면 반투명 판이 흐려지며 사라진다(복셀 모델의 흐려짐을 0까지 내리고 끔).
    /// 모서리 겹침 피하기: 같은 칸에 북·남 방벽이 있으면 동·서 방벽의 판이 그 끝만큼(2픽셀) 짧은 모양으로 바뀐다
    /// (모델 Barrier·BarrierOneCorner·BarrierNoCorner, 반대쪽 끝은 OneCorner를 판 축으로 뒤집은 것). 북·남 방벽이 사라지면 원래 길이로 돌아간다.
    /// 바닥·천장 방벽과의 겹침은 아직 다루지 않는다. Play 중에만 바꾼다(편집 화면은 온전한 판).
    /// 판이 사라지거나 다시 생기거나 모양이 바뀌면 알린다(큐브 격자가 맞닿은 면 숨기기를 다시 맞춤).
    /// </summary>
    public class BarrierView : OpenableView
    {
        [Tooltip("흐려질 복셀 모델(방벽 판)")]
        [SerializeField] VoxPaint model;

        [Tooltip("다 사라지는 데 걸리는 시간(초)")]
        [SerializeField] float duration = 0.4f;

        [Tooltip("붙은 면을 읽을 자리 컴포넌트")]
        [SerializeField] CellFaceMount mount;

        [Tooltip("판 모양 4개(복셀 모델 아래, 하나만 켬): 0 온전, 1 +Z 끝 자름(서쪽 방벽이면 북쪽 끝), 2 −Z 끝 자름, 3 두 끝 자름")]
        [SerializeField] GameObject[] shapes = new GameObject[4];

        static readonly Dictionary<Vector3Int, List<BarrierView>> byCell = new();

        float openness;
        float target;
        Vector3Int cell;

        /// <summary>판이 사라지거나(끔) 다시 생겼거나(켬) 모양이 바뀌었다.</summary>
        public static event Action<BarrierView> ShownChanged;

        bool IsShown => model.gameObject.activeSelf;

        void Awake()
        {
            enabled = false;
            cell = CubeGridRules.CellAt(transform.position);
            if (!byCell.TryGetValue(cell, out var list)) byCell[cell] = list = new List<BarrierView>();
            list.Add(this);
            RefitCell(cell);
        }

        void OnDestroy()
        {
            if (!byCell.TryGetValue(cell, out var list)) return;
            list.Remove(this);
            if (list.Count == 0) byCell.Remove(cell);
            else RefitCell(cell);
        }

        public override void SetOpen(bool open)
        {
            target = open ? 1f : 0f;
            Show(true);
            enabled = true;
        }

        public override void SetOpenImmediate(bool open)
        {
            openness = target = open ? 1f : 0f;
            Apply();
            enabled = false;
        }

        void Update()
        {
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / duration);
            Apply();
            if (openness == target) enabled = false;
        }

        void Apply()
        {
            model.Fade = 1f - openness;
            Show(openness < 1f);
        }

        void Show(bool shown)
        {
            if (IsShown == shown) return;
            model.gameObject.SetActive(shown);
            ShownChanged?.Invoke(this);
            RefitCell(cell);
        }

        static void RefitCell(Vector3Int cell)
        {
            if (byCell.TryGetValue(cell, out var list))
                foreach (var barrier in list) barrier.Refit();
        }

        // 같은 칸의 보이는 북·남 방벽 쪽 끝을 자른 모양을 고른다(동·서 방벽만).
        void Refit()
        {
            if (mount == null || shapes == null || shapes.Length < 4) return;
            bool plus = false, minus = false;
            if (mount.Face is CellFace.West or CellFace.East)
                foreach (var other in byCell[cell])
                {
                    if (other == this || !other.IsShown || other.mount == null || other.mount.Face is not (CellFace.North or CellFace.South)) continue;
                    // 서쪽 방벽은 모델의 +Z 끝이 북쪽, 동쪽 방벽(180도 돌림)은 남쪽이다.
                    bool atPlus = (other.mount.Face == CellFace.North) == (mount.Face == CellFace.West);
                    if (atPlus) plus = true;
                    else minus = true;
                }
            int index = plus && minus ? 3 : plus ? 1 : minus ? 2 : 0;
            if (shapes[index] == null || shapes[index].activeSelf) return;
            for (int i = 0; i < shapes.Length; i++)
                if (shapes[i] != null) shapes[i].SetActive(i == index);
            ShownChanged?.Invoke(this);
        }
    }
}
