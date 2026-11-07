using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ColorMaze
{
    public class VoxTests
    {
        // 아트 세션이 만든 가져오기 시험 모델(Docs/v2 복셀 모델링 규칙 8절).
        const string TestFile = "Assets/Models/Tests/VoxImportTest.vox";

        static VoxFile ReadTestFile() => VoxFile.Read(File.ReadAllBytes(TestFile));

        static int Area(VoxMesher.Quad quad) => (quad.du.x + quad.du.y + quad.du.z) * (quad.dv.x + quad.dv.y + quad.dv.z);

        [Test]
        public void Read_TestFile_PlacesObjectsByCenterCell()
        {
            var file = ReadTestFile();
            Assert.AreEqual(new[] { "Base", "Lamp" }, file.objects.Select(obj => obj.name).ToArray());
            Assert.AreEqual(new Vector3Int(14, 14, 12), file.objects[0].model.size);
            // 가운데 칸은 크기/2 내림: Base 14·14·12 → 7·7·6, Lamp 5·5·7 → 2·2·3.
            Assert.AreEqual(new Vector3Int(-7, -7, 0), file.objects[0].Min);
            Assert.AreEqual(new Vector3Int(0, -2, 12), file.objects[1].Min);
            Assert.AreEqual((new Vector3Int(-7, -7, 0), new Vector3Int(7, 7, 19)), file.Bounds());
        }

        [Test]
        public void Read_TestFile_PaletteIndexIsColorNumber()
        {
            // 값 색 칸 33(가장 밝음)과 40(가장 어두움)은 Base의 윗면 테두리에만 있다(규칙 3절, 8절).
            var file = ReadTestFile();
            Assert.AreEqual(new Color32(255, 0, 0, 255), file.palette[VoxImporter.FirstValueColor]);
            Assert.AreEqual(new Color32(0, 0, 0, 255), file.palette[VoxImporter.LastValueColor]);
            Assert.AreEqual(44, file.objects[0].model.voxels.Count(voxel => voxel.color == VoxImporter.FirstValueColor));
            Assert.AreEqual(44, file.objects[0].model.voxels.Count(voxel => voxel.color == VoxImporter.LastValueColor));
            Assert.AreEqual(0, file.objects[1].model.voxels.Count(voxel => VoxImporter.IsValueColor(voxel.color)));
            Assert.IsFalse(file.hasRotation);
        }

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
