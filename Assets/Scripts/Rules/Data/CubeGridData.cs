using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 지형 데이터: 블록이 있는 칸 목록과 계단 목록(v1 칸 규칙, <see cref="CubeGridRules"/>). 블록은 아직 한 종류다.
    /// 씬의 큐브 격자와 나중의 UGC 맵 파일이 같은 클래스를 쓴다(JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class CubeGridData
    {
        public List<Vector3Int> blocks = new();
        public List<CubeStair> stairs = new();
    }

    /// <summary>
    /// 계단 하나: 한 칸을 차지하고 그 칸 안에서 한 칸 높이를 오른다(점프가 없어 위 칸으로 가는 길, 2026-11-07).
    /// <see cref="turn"/>은 오르는 방향을 Y축으로 90°씩 돌린 횟수다: 0 = +z, 1 = +x, 2 = −z, 3 = −x.
    /// </summary>
    [Serializable]
    public struct CubeStair
    {
        public Vector3Int cell;
        public int turn;

        public CubeStair(Vector3Int cell, int turn)
        {
            this.cell = cell;
            this.turn = turn;
        }
    }
}
