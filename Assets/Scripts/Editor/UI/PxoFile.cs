using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SlimeColorMaker
{
    /// <summary>
    /// Pixelorama 프로젝트 파일(.pxo, Pixelorama v1.2.4·pxo 형식 8 확인)을 읽은 결과. .pxo는 ZIP이고
    /// <c>data.json</c>(판 크기, 레이어, 프레임·셀 정보)과 셀마다의 원시 픽셀 <c>image_data/frames/&lt;프레임&gt;/layer_&lt;레이어&gt;</c>(번호는 1부터)이 들어 있다.
    /// 셀은 판 전체가 아니라 그린 범위만 저장된다(<c>image_size</c>, <c>offset</c>). 픽셀은 RGBA8(color_mode 5), 위쪽 줄부터.
    /// 레이어 목록은 맨 아래 레이어가 0번이다(Docs/v2 UI 픽셀아트 규칙).
    /// </summary>
    public class PxoFile
    {
        const int Rgba8 = 5; // Godot Image.FORMAT_RGBA8

        public class Layer
        {
            public string name;
            public bool visible = true;
            /// <summary>부모 레이어(그룹) 번호, 없으면 -1.</summary>
            public int parent = -1;
            public int type;
            public float opacity = 1f;
            public int blendMode;
        }

        public class Cel
        {
            public int width, height, offsetX, offsetY;
            public float opacity = 1f;
            /// <summary>RGBA8, 위쪽 줄부터. 비었으면 null.</summary>
            public byte[] pixels;
        }

        public int width, height;
        public readonly List<Layer> layers = new();
        /// <summary>프레임마다 레이어 순서대로의 셀.</summary>
        public readonly List<Cel[]> frames = new();
        public readonly List<string> warnings = new();

        public static PxoFile Read(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var dataEntry = zip.GetEntry("data.json") ?? throw new InvalidDataException("data.json이 없다(.pxo가 아님).");
            JObject data;
            using (var reader = new StreamReader(dataEntry.Open())) data = JObject.Parse(reader.ReadToEnd());

            var file = new PxoFile
            {
                width = data.Value<int?>("size_x") ?? 0,
                height = data.Value<int?>("size_y") ?? 0,
            };
            int colorMode = data.Value<int?>("color_mode") ?? Rgba8;
            if (colorMode != Rgba8) file.warnings.Add($"이미지 형식(color_mode {colorMode})이 RGBA8이 아니다. RGBA8로 보고 읽는다.");

            foreach (var layer in data["layers"] as JArray ?? new JArray())
                file.layers.Add(new Layer
                {
                    name = layer.Value<string>("name") ?? "",
                    visible = layer.Value<bool?>("visible") ?? true,
                    parent = layer.Value<int?>("parent") ?? -1,
                    type = layer.Value<int?>("type") ?? 0,
                    opacity = layer.Value<float?>("opacity") ?? 1f,
                    blendMode = layer.Value<int?>("blend_mode") ?? 0,
                });

            int frameNumber = 0;
            foreach (var frame in data["frames"] as JArray ?? new JArray())
            {
                frameNumber++;
                var cels = new Cel[file.layers.Count];
                var celData = frame["cels"] as JArray ?? new JArray();
                for (int i = 0; i < cels.Length && i < celData.Count; i++)
                {
                    var size = Numbers(celData[i].Value<string>("image_size"));
                    var offset = Numbers(celData[i].Value<string>("offset"));
                    var cel = new Cel
                    {
                        width = size.Length >= 2 ? size[0] : file.width,
                        height = size.Length >= 2 ? size[1] : file.height,
                        offsetX = offset.Length >= 2 ? offset[0] : 0,
                        offsetY = offset.Length >= 2 ? offset[1] : 0,
                        opacity = celData[i].Value<float?>("opacity") ?? 1f,
                    };
                    var entry = zip.GetEntry($"image_data/frames/{frameNumber}/layer_{i + 1}");
                    if (entry != null && cel.width > 0 && cel.height > 0)
                    {
                        using var stream = entry.Open();
                        using var memory = new MemoryStream();
                        stream.CopyTo(memory);
                        var bytes = memory.ToArray();
                        if (bytes.Length == cel.width * cel.height * 4) cel.pixels = bytes;
                        else file.warnings.Add($"프레임 {frameNumber} 레이어 '{file.layers[i].name}'의 픽셀 크기가 맞지 않아 읽지 않는다({bytes.Length}바이트, {cel.width} × {cel.height}).");
                    }
                    cels[i] = cel;
                }
                file.frames.Add(cels);
            }
            return file;
        }

        // "Vector2i(33, 21)", "(13, 21)" → [33, 21] / [13, 21]. 괄호 안의 수만 읽는다(Vector2i의 2를 빼려고).
        static int[] Numbers(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
            int open = text.IndexOf('(');
            var inside = open >= 0 ? text[(open + 1)..] : text;
            return Regex.Matches(inside, @"-?\d+(\.\d+)?").Select(m => (int)Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        }

        /// <summary>레이어가 보이는가(자신과 모든 부모 그룹이 보임).</summary>
        public bool IsVisible(int layer)
        {
            for (int i = layer; i >= 0 && i < layers.Count; i = layers[i].parent)
                if (!layers[i].visible) return false;
            return true;
        }
    }
}
