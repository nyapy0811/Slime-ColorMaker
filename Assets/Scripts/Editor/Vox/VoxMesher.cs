using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 복셀 칸에서 겉면 사각형을 만든다. 칸 배열은 Unity 축(y가 위)이고 0은 빈칸이다. 다른 칸과 맞닿은 면은 버리고,
    /// 같은 방향·같은 층·같은 색의 면은 큰 사각형 하나로 합친다(면 합치기, greedy meshing).
    /// 반투명 칸이 있으면: 불투명 칸은 반투명 칸과 맞닿은 면을 남기고(반투명 너머로 보임), 반투명 칸은 어느 칸과 맞닿든 면을 버린다
    /// (반투명 부분은 바깥 껍질만 그려 반투명 면끼리 겹치지 않음).
    /// </summary>
    public static class VoxMesher
    {
        /// <summary>네 꼭짓점은 origin, +du, +du+dv, +dv 차례이고 du × dv가 normal 쪽이다(Unity 앞면).</summary>
        public struct Quad
        {
            public Vector3Int origin;
            public Vector3Int du;
            public Vector3Int dv;
            public Vector3Int normal;
            public byte color;
        }

        /// <summary>MagicaVoxel 모델을 Unity 축 칸 배열로 바꾼다: (x, y, z) → (x, z, y).</summary>
        public static byte[,,] Grid(VoxFile.Model model)
        {
            var grid = new byte[model.size.x, model.size.z, model.size.y];
            foreach (var (position, color) in model.voxels) grid[position.x, position.z, position.y] = color;
            return grid;
        }

        /// <summary>
        /// include가 받는 색의 칸에서만 면을 만든다. 맞닿음은 모든 칸으로 본다(칠하는 부분과 회색 사이 면도 버림).
        /// 다만 isClear가 받는 색(반투명)의 칸은 불투명 칸의 면을 가리지 않는다.
        /// </summary>
        public static List<Quad> Quads(byte[,,] grid, Func<byte, bool> include, Func<byte, bool> isClear = null)
        {
            var quads = new List<Quad>();
            var size = new[] { grid.GetLength(0), grid.GetLength(1), grid.GetLength(2) };
            var cell = new int[3];
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3, v = (d + 2) % 3;
                var mask = new byte[size[u], size[v]];
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var normal = Vector3Int.zero;
                    normal[d] = sign;
                    for (int s = 0; s < size[d]; s++)
                    {
                        // 이 층에서 바깥을 보는 면의 색을 모은다.
                        for (int a = 0; a < size[u]; a++)
                        {
                            for (int b = 0; b < size[v]; b++)
                            {
                                cell[d] = s;
                                cell[u] = a;
                                cell[v] = b;
                                byte color = grid[cell[0], cell[1], cell[2]];
                                cell[d] = s + sign;
                                byte neighbor = cell[d] < 0 || cell[d] >= size[d] ? (byte)0 : grid[cell[0], cell[1], cell[2]];
                                bool open = neighbor == 0 || (isClear != null && isClear(neighbor) && !isClear(color));
                                mask[a, b] = color != 0 && open && include(color) ? color : (byte)0;
                            }
                        }

                        // 같은 색끼리 u 쪽으로 넓히고 v 쪽으로 늘려 사각형 하나로 합친다.
                        for (int a = 0; a < size[u]; a++)
                        {
                            for (int b = 0; b < size[v]; b++)
                            {
                                byte color = mask[a, b];
                                if (color == 0) continue;
                                int w = 1;
                                while (a + w < size[u] && mask[a + w, b] == color) w++;
                                int h = 1;
                                while (b + h < size[v] && RowMatches(mask, a, w, b + h, color)) h++;
                                for (int i = 0; i < w; i++)
                                {
                                    for (int j = 0; j < h; j++) mask[a + i, b + j] = 0;
                                }

                                var origin = Vector3Int.zero;
                                origin[d] = sign > 0 ? s + 1 : s;
                                origin[u] = a;
                                origin[v] = b;
                                var du = Vector3Int.zero;
                                du[u] = w;
                                var dv = Vector3Int.zero;
                                dv[v] = h;
                                if (Vector3.Dot(Vector3.Cross(du, dv), normal) < 0) (du, dv) = (dv, du);
                                quads.Add(new Quad { origin = origin, du = du, dv = dv, normal = normal, color = color });
                            }
                        }
                    }
                }
            }
            return quads;
        }

        static bool RowMatches(byte[,] mask, int start, int width, int row, byte color)
        {
            for (int i = 0; i < width; i++)
            {
                if (mask[start + i, row] != color) return false;
            }
            return true;
        }
    }
}
