using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 지형 데이터: 블록이 있는 칸 목록, 계단 목록, 장식 기둥 목록, 블록 윗면 코팅 목록(규칙은 <see cref="CubeGridRules"/>). 블록은 아직 한 종류다.
    /// 씬의 큐브 격자와 나중의 UGC 맵 파일이 같은 클래스를 쓴다(JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class CubeGridData
    {
        public List<Vector3Int> blocks = new();
        public List<CubeStair> stairs = new();
        public List<CubePillar> pillars = new();
        public List<CubeCoating> coatings = new();
    }

    /// <summary>
    /// 계단 하나: 한 칸을 차지하고 그 칸 안에서 한 칸 높이를 오른다(점프가 없어 위 칸으로 가는 길).
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

    /// <summary>
    /// 장식 기둥 하나: 한 칸을 차지하고, 놓을 때 정한 설정(그룹에 머리·받침을 둘지)을 든다. 칸별 모양은 저장하지 않고
    /// <see cref="CubeGridRules.PillarShapes"/>가 위아래로 붙은 같은 설정의 기둥(그룹)을 보고 정한다.
    /// </summary>
    [Serializable]
    public struct CubePillar
    {
        public Vector3Int cell;

        /// <summary>그룹 맨 위 칸에 머리를 둘지.</summary>
        public bool hasTop;

        /// <summary>그룹 맨 아래 칸에 받침을 둘지.</summary>
        public bool hasBase;

        public CubePillar(Vector3Int cell, bool hasTop, bool hasBase)
        {
            this.cell = cell;
            this.hasTop = hasTop;
            this.hasBase = hasBase;
        }
    }

    /// <summary>기둥 한 칸의 모양: 몸통만, 받침 + 몸통, 몸통 + 머리, 받침 + 몸통 + 머리.</summary>
    public enum PillarShape { Shaft, Base, Top, Full }

    /// <summary>블록 윗면 코팅의 종류(미끄러운 바닥). 없음, 기름, 얼음.</summary>
    public enum CoatingKind : byte { None, Oil, Ice }

    /// <summary>
    /// 블록 하나의 윗면 코팅: 윗면 16 × 16 픽셀(1/16칸, 바닥 흔적과 같은 크기)마다 <see cref="CoatingKind"/>.
    /// <see cref="pixels"/>의 번호는 z × 16 + x(x·z는 칸의 −x·−z 모서리에서 셈)다. 블록이 없어지면 함께 지운다.
    /// </summary>
    [Serializable]
    public struct CubeCoating
    {
        public Vector3Int cell;
        public byte[] pixels;
    }
}
