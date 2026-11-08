using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ColorMaze
{
    public class VoxTests
    {
        static int Area(VoxMesher.Quad quad) => (quad.du.x + quad.du.y + quad.du.z) * (quad.dv.x + quad.dv.y + quad.dv.z);

        [Test]
        public void Grid_SwapsYAndZ()
        {
            var model = new VoxFile.Model { size = new Vector3Int(1, 2, 3) };
            model.voxels.Add((new Vector3Int(0, 1, 2), 5));
            var grid = VoxMesher.Grid(model);
            Assert.AreEqual(new[] { 1, 3, 2 }, new[] { grid.GetLength(0), grid.GetLength(1), grid.GetLength(2) });
            Assert.AreEqual(5, grid[0, 2, 1]);
        }

        [Test]
        public void Quads_SingleVoxel_SixFaces()
        {
            var grid = new byte[1, 1, 1];
            grid[0, 0, 0] = 1;
            Assert.AreEqual(6, VoxMesher.Quads(grid, _ => true).Count);
        }

        [Test]
        public void Quads_SameColorRow_MergesIntoSixFaces()
        {
            var grid = new byte[3, 1, 1];
            for (int x = 0; x < 3; x++) grid[x, 0, 0] = 1;
            var quads = VoxMesher.Quads(grid, _ => true);
            Assert.AreEqual(6, quads.Count);
            Assert.AreEqual(14, quads.Sum(Area));
        }

        [Test]
        public void Quads_DifferentColors_DoNotMergeAndHideSharedFace()
        {
            var grid = new byte[2, 1, 1];
            grid[0, 0, 0] = 1;
            grid[1, 0, 0] = 2;
            Assert.AreEqual(10, VoxMesher.Quads(grid, _ => true).Count);
        }

        [Test]
        public void Quads_OnlyIncludedColor_StillHidesTouchingFace()
        {
            var grid = new byte[2, 1, 1];
            grid[0, 0, 0] = 1;
            grid[1, 0, 0] = 2;
            var quads = VoxMesher.Quads(grid, color => color == 2);
            Assert.AreEqual(5, quads.Count);
            Assert.IsTrue(quads.All(quad => quad.color == 2));
        }

        [Test]
        public void Quads_ClearNeighbor_KeepsOpaqueFaceButClearHidesItsOwn()
        {
            // 0: 불투명(1), 1: 반투명(2), 2: 반투명(3). 반투명은 바깥 껍질만, 불투명은 반투명 너머로 보이게 맞닿은 면을 남긴다.
            var grid = new byte[3, 1, 1];
            grid[0, 0, 0] = 1;
            grid[1, 0, 0] = 2;
            grid[2, 0, 0] = 3;
            System.Func<byte, bool> clear = color => color >= 2;
            var opaque = VoxMesher.Quads(grid, color => color == 1, clear);
            Assert.AreEqual(6, opaque.Count);
            var glass = VoxMesher.Quads(grid, clear, clear);
            Assert.AreEqual(4 + 5, glass.Count); // 가운데 칸: 옆 넷(양쪽 맞닿은 면 없음), 끝 칸: 다섯
        }

        [Test]
        public void Quads_FrontFacesPointOutward()
        {
            var grid = new byte[1, 1, 1];
            grid[0, 0, 0] = 1;
            var center = new Vector3(0.5f, 0.5f, 0.5f);
            foreach (var quad in VoxMesher.Quads(grid, _ => true))
            {
                // Unity 앞면: 꼭짓점 차례로 (b − a) × (c − a)가 법선 쪽이다.
                Assert.AreEqual((Vector3)quad.normal, Vector3.Cross(quad.du, quad.dv).normalized);
                Vector3 faceCenter = quad.origin + (Vector3)(quad.du + quad.dv) / 2f;
                Assert.Greater(Vector3.Dot(faceCenter - center, quad.normal), 0f);
            }
        }
    }
}
