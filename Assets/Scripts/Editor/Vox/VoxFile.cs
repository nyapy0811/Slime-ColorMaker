using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// MagicaVoxel <c>.vox</c> 파일(버전 200)을 읽는다. 모델(SIZE·XYZI), 팔레트(RGBA), 씬 그래프(nTRN·nGRP·nSHP)만 읽고
    /// 회전·재질·레이어·카메라는 읽지 않는다(Docs/v2 복셀 모델링 규칙 5절). 좌표는 MagicaVoxel 축 그대로(Z가 위)다.
    /// </summary>
    public class VoxFile
    {
        public class Model
        {
            public Vector3Int size;

            /// <summary>모델 안 칸 좌표와 색 번호(1~255).</summary>
            public readonly List<(Vector3Int position, byte color)> voxels = new();
        }

        /// <summary>씬에 놓인 오브젝트 하나. 위치(<c>_t</c>)는 모델 가운데 칸(크기/2 내림)이 놓이는 칸이다(규칙 7절).</summary>
        public class VoxObject
        {
            public string name;
            public Model model;
            public Vector3Int translation;

            /// <summary>모델 크기 상자의 가장 작은 모서리(씬 칸 좌표).</summary>
            public Vector3Int Min => translation - new Vector3Int(model.size.x / 2, model.size.y / 2, model.size.z / 2);

            public Vector3Int Max => Min + model.size;
        }

        public readonly List<Model> models = new();

        /// <summary>색 번호 i의 색이 i번째 칸이다(RGBA 청크의 i − 1번째 항목, 0번은 빈칸이라 쓰지 않음).</summary>
        public readonly Color32[] palette = new Color32[256];

        public readonly List<VoxObject> objects = new();

        /// <summary>회전(<c>_r</c>)이 있는 변환 노드가 있다. 회전은 읽지 않으므로 가져오기 코드가 경고한다.</summary>
        public bool hasRotation;

        /// <summary>모든 오브젝트의 모델 크기 상자를 합친 상자(씬 칸 좌표, max는 끝 칸 바로 다음).</summary>
        public (Vector3Int min, Vector3Int max) Bounds()
        {
            var min = objects[0].Min;
            var max = objects[0].Max;
            foreach (var obj in objects)
            {
                min = Vector3Int.Min(min, obj.Min);
                max = Vector3Int.Max(max, obj.Max);
            }
            return (min, max);
        }

        public static VoxFile Read(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (ReadId(reader) != "VOX ") throw new InvalidDataException(".vox 파일이 아니다.");
            reader.ReadInt32(); // 버전
            if (ReadId(reader) != "MAIN") throw new InvalidDataException("MAIN 청크가 없다.");
            reader.ReadInt32();
            reader.ReadInt32();

            var file = new VoxFile();
            var transforms = new Dictionary<int, (string name, int child, Vector3Int translation)>();
            var groups = new Dictionary<int, int[]>();
            var shapes = new Dictionary<int, int>();
            // MAIN 아래 청크는 모두 자식이 없어 차례로 읽으면 된다.
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string id = ReadId(reader);
                int contentSize = reader.ReadInt32();
                reader.ReadInt32();
                long end = reader.BaseStream.Position + contentSize;
                switch (id)
                {
                    case "SIZE":
                        file.models.Add(new Model { size = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()) });
                        break;
                    case "XYZI":
                        var voxels = file.models[^1].voxels;
                        int count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            var position = new Vector3Int(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                            voxels.Add((position, reader.ReadByte()));
                        }
                        break;
                    case "RGBA":
                        for (int i = 1; i < 256; i++) file.palette[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                        break;
                    case "nTRN":
                    {
                        int nodeId = reader.ReadInt32();
                        ReadDict(reader).TryGetValue("_name", out string name);
                        int child = reader.ReadInt32();
                        reader.ReadInt32(); // 예약
                        reader.ReadInt32(); // 레이어
                        int frames = reader.ReadInt32();
                        var translation = Vector3Int.zero;
                        // 첫 프레임만 쓴다(애니메이션 없음).
                        var frame = frames > 0 ? ReadDict(reader) : new Dictionary<string, string>();
                        if (frame.TryGetValue("_t", out string t))
                        {
                            var parts = t.Split(' ');
                            translation = new Vector3Int(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                        }
                        if (frame.ContainsKey("_r")) file.hasRotation = true;
                        transforms[nodeId] = (name, child, translation);
                        break;
                    }
                    case "nGRP":
                    {
                        int nodeId = reader.ReadInt32();
                        ReadDict(reader);
                        var children = new int[reader.ReadInt32()];
                        for (int i = 0; i < children.Length; i++) children[i] = reader.ReadInt32();
                        groups[nodeId] = children;
                        break;
                    }
                    case "nSHP":
                    {
                        int nodeId = reader.ReadInt32();
                        ReadDict(reader);
                        reader.ReadInt32(); // 모델 수(늘 1)
                        shapes[nodeId] = reader.ReadInt32();
                        break;
                    }
                }
                reader.BaseStream.Position = end;
            }

            // 씬 그래프는 0번 변환 노드에서 시작한다. 그룹을 거치며 위치를 더한다(회전은 쓰지 않음).
            void Visit(int nodeId, Vector3Int offset)
            {
                var (name, child, translation) = transforms[nodeId];
                offset += translation;
                if (groups.TryGetValue(child, out var children))
                {
                    foreach (int next in children) Visit(next, offset);
                }
                else if (shapes.TryGetValue(child, out int modelIndex))
                {
                    file.objects.Add(new VoxObject { name = name, model = file.models[modelIndex], translation = offset });
                }
            }
            Visit(0, Vector3Int.zero);
            return file;
        }

        static string ReadId(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));

        static string ReadString(BinaryReader reader) => Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));

        static Dictionary<string, string> ReadDict(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var dict = new Dictionary<string, string>(count, StringComparer.Ordinal);
            for (int i = 0; i < count; i++) dict[ReadString(reader)] = ReadString(reader);
            return dict;
        }
    }
}
