using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 큐브 맵의 지형 데이터: 블록이 있는 칸 목록, 계단 목록, 장식(기둥·천장 조명·상자·난간·계기판) 목록, 블록 윗면 코팅 목록(규칙은 <see cref="CubeGridRules"/>). 블록은 아직 한 종류다.
    /// 씬의 큐브 격자와 나중의 UGC 맵 파일이 같은 클래스를 쓴다(JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class CubeGridData
    {
        public List<Vector3Int> blocks = new();
        public List<CubeStair> stairs = new();
        public List<CubePillar> pillars = new();
        public List<CubeLight> lights = new();

        /// <summary>상자(반 칸 8³)의 반 칸 좌표(칸 × 2 + 0·1, <see cref="CubeGridRules.CrateCell"/>).</summary>
        public List<Vector3Int> crates = new();

        public List<CubeSidePiece> railings = new();
        public List<CubeSidePiece> panels = new();
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
    /// 장식 기둥 하나(H빔 한 칸): 한 칸을 차지하고, 세울지 눕힐지와 방향을 든다. 쌓아서 높이를 나타낸다.
    /// 세운 기둥은 <see cref="axis"/>가 홈(파인 면)이 보는 쪽이고, 눕힌 기둥(가로 들보)은 <see cref="axis"/>가 들보 방향이다.
    /// 눕힌 기둥의 방향은 옆 칸으로 정하고 정하지 못할 때만 이 값을 쓴다(<see cref="CubeGridRules.PillarLayouts"/>).
    /// </summary>
    [Serializable]
    public struct CubePillar
    {
        public Vector3Int cell;

        /// <summary>눕혔는가(가로 들보).</summary>
        public bool lying;

        /// <summary>세운 기둥은 홈이 보는 쪽, 눕힌 기둥은 옆 칸으로 정하지 못할 때의 들보 방향.</summary>
        public CellAxis axis;

        public CubePillar(Vector3Int cell, bool lying, CellAxis axis)
        {
            this.cell = cell;
            this.lying = lying;
            this.axis = axis;
        }
    }

    /// <summary>
    /// 천장 조명 하나(장식): 위 칸 지형의 아랫면에 붙는 막대(2 × 1 × 8픽셀). 놓는 곳은 프리팹의 설치 규칙으로 정한다
    /// (처음 값: 위 칸 지형의 칸 경계 아랫면이 꽉 참, <see cref="CubeGridRules.Supported"/>).
    /// 칸을 차지하지 않아 같은 칸에 기물을 둘 수 있다. 천장에서 떨어지면 편집 도구가 지운다.
    /// </summary>
    [Serializable]
    public struct CubeLight
    {
        public Vector3Int cell;

        /// <summary>막대 길이 방향.</summary>
        public CellAxis axis;

        /// <summary>빛 색(발광 부분과 광원의 색).</summary>
        public Color color;

        public CubeLight(Vector3Int cell, CellAxis axis, Color color)
        {
            this.cell = cell;
            this.axis = axis;
            this.color = color;
        }
    }

    /// <summary>
    /// 칸의 한 옆면에 붙는 장식 하나(철망 난간, 계기판): 칸 + 옆면(0 = 북, 1 = 동, 2 = 남, 3 = 서, CellFace 차례).
    /// 모델은 남쪽 면 기준으로 만들고 옆면대로 돌린다(<see cref="CubeGridRules.SideRotation"/>). 놓는 곳은 프리팹의 설치 규칙으로 정한다(처음 값: 난간은 아래, 계기판은 뒤 칸 지형의 칸 경계 면이 꽉 참).
    /// </summary>
    [Serializable]
    public struct CubeSidePiece
    {
        public Vector3Int cell;
        public int side;

        public CubeSidePiece(Vector3Int cell, int side)
        {
            this.cell = cell;
            this.side = side;
        }
    }

    /// <summary>칸 방향: 남북(±z) 또는 동서(±x). 기둥과 천장 조명이 쓴다.</summary>
    public enum CellAxis { NorthSouth, EastWest }

    /// <summary>기둥 한 칸의 놓인 모양: 세움(홈이 남북·동서)·눕힘(들보가 남북·동서).</summary>
    public enum PillarPose { UprightNorthSouth, UprightEastWest, LyingNorthSouth, LyingEastWest }

    /// <summary>기둥 칸 옆면의 커넥터: 없음, 1 = 세운 기둥의 평평한 면, 2 = 세운 기둥의 홈, 3 = 눕힌 기둥의 옆 홈.</summary>
    public enum PillarConnector { None, Flat, Groove, Beam }

    /// <summary>
    /// 기둥 한 칸의 겉모습 값(<see cref="CubeGridRules.PillarLayouts"/>가 계산): 놓인 모양과 동서남북 옆면마다 커넥터.
    /// 커넥터는 옆 칸의 들보 끝이 이 칸에 닿는 면에 놓여, 기둥 면(칸 가운데에서 4픽셀)과 칸 경계 사이 4픽셀 틈을 메운다.
    /// </summary>
    [Serializable]
    public struct PillarLayout
    {
        public PillarPose pose;
        public PillarConnector north, east, south, west;

        /// <summary>옆면의 커넥터. 옆면 번호는 0 = 북(+z), 1 = 동(+x), 2 = 남(−z), 3 = 서(−x)(CellFace 차례).</summary>
        public PillarConnector this[int side]
        {
            readonly get => side switch { 0 => north, 1 => east, 2 => south, _ => west };
            set
            {
                switch (side)
                {
                    case 0: north = value; break;
                    case 1: east = value; break;
                    case 2: south = value; break;
                    default: west = value; break;
                }
            }
        }
    }

    /// <summary>덮인 빈 칸 덩어리(<see cref="CubeGridRules.CoveredRegions"/>): 칸 범위, 가운데에 가장 가까운 칸, 칸 수.</summary>
    public readonly struct CoveredRegion
    {
        public readonly Vector3Int min;
        public readonly Vector3Int max;
        public readonly Vector3Int center;
        public readonly int cellCount;

        public CoveredRegion(Vector3Int min, Vector3Int max, Vector3Int center, int cellCount)
        {
            this.min = min;
            this.max = max;
            this.center = center;
            this.cellCount = cellCount;
        }
    }

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
