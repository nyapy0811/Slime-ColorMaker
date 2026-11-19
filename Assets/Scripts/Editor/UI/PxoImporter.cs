using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// Pixelorama <c>.pxo</c>를 UI 스프라이트로 가져온다(약속은 Docs/v2 UI 픽셀아트 규칙).
    /// 보이는 레이어를 Pixelorama 화면처럼 위 레이어부터 겹치고, 각 픽셀은 그 자리 맨 위 레이어의 태그(레이어나 감싼 그룹의 이름, 대소문자 무시:
    /// Gray·Color, 없으면 Gray)를 따른다. 텍스처 한 장에 프레임을 가로로 이어 붙이고 프레임마다 스프라이트(Normal·Hover·Pressed·Disabled 순)를 만든다.
    /// 텍스처: RGB = 휘도 Y(BT.601) 회색, 알파 = 방식(<see cref="PaintImage"/>의 UI Paint 셰이더가 읽음: Gray 255, Color 254, 투명 0). 필터 없음·압축 없음.
    /// 스프라이트의 픽셀/유닛은 100 ÷ 3이라 캔버스 기준(1920 × 1080, 기준 픽셀/유닛 100)에서 그림 1픽셀 = 3픽셀이다.
    /// </summary>
    [ScriptedImporter(1, "pxo")]
    public class PxoImporter : ScriptedImporter
    {
        /// <summary>UI 그림 1픽셀이 기준 화면(1920 × 1080)에서 차지하는 픽셀.</summary>
        public const int PixelScale = 3;

        /// <summary>방식마다 텍스처 알파 값(UI Paint 셰이더와 같아야 함).</summary>
        public const byte GrayAlpha = 255, ColorAlpha = 254;

        static readonly Dictionary<string, byte> Tags = new()
        {
            ["gray"] = GrayAlpha,
            ["color"] = ColorAlpha,
        };

        static readonly string[] StateNames = { "Normal", "Hover", "Pressed", "Disabled" };

        [Tooltip("9-슬라이스 테두리(그림 픽셀): 왼쪽, 아래, 오른쪽, 위. 늘어나는 버튼·창에만 쓴다(규칙 5절 표에 적는다). 0이면 늘이지 않는 그림.")]
        public Vector4 border;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var file = PxoFile.Read(ctx.assetPath);
            foreach (var warning in file.warnings) ctx.LogImportWarning($"[PxoImporter] {warning}");
            if (file.width <= 0 || file.height <= 0 || file.frames.Count == 0)
            {
                ctx.LogImportError("[PxoImporter] 판 크기나 프레임이 없다.");
                return;
            }
            Warn(ctx, file);

            int frameCount = file.frames.Count;
            var texture = new Texture2D(file.width * frameCount, file.height, TextureFormat.RGBA32, false)
            {
                name = "Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[texture.width * texture.height];
            for (int f = 0; f < frameCount; f++) Compose(file, f, pixels, texture.width, f * file.width);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ctx.AddObjectToAsset("texture", texture);
            ctx.SetMainObject(texture);

            for (int f = 0; f < frameCount; f++)
            {
                var sprite = Sprite.Create(texture, new Rect(f * file.width, 0, file.width, file.height), new Vector2(0.5f, 0.5f),
                    100f / PixelScale, 0, SpriteMeshType.FullRect, border);
                sprite.name = f < StateNames.Length ? StateNames[f] : $"Frame {f + 1}";
                ctx.AddObjectToAsset("sprite " + f, sprite);
            }
        }

        // 프레임 하나를 겹쳐 texture의 x = left 자리에 쓴다(텍스처는 아래쪽 줄부터, .pxo는 위쪽 줄부터).
        static void Compose(PxoFile file, int frame, Color32[] target, int stride, int left)
        {
            var cels = file.frames[frame];
            for (int layer = 0; layer < file.layers.Count; layer++) // 아래 레이어부터 덮어쓰면 맨 위 레이어가 남는다
            {
                if (file.layers[layer].type != 0 || !file.IsVisible(layer)) continue; // 픽셀 레이어만(그룹 등 제외)
                var cel = layer < cels.Length ? cels[layer] : null;
                if (cel?.pixels == null) continue;
                byte mode = ModeOf(file, layer);
                for (int y = 0; y < cel.height; y++)
                {
                    int canvasY = cel.offsetY + y;
                    if (canvasY < 0 || canvasY >= file.height) continue;
                    for (int x = 0; x < cel.width; x++)
                    {
                        int canvasX = cel.offsetX + x;
                        if (canvasX < 0 || canvasX >= file.width) continue;
                        int i = (y * cel.width + x) * 4;
                        if (cel.pixels[i + 3] < 128) continue; // 규칙 3절: 투명은 0, 불투명은 255만(반투명은 반으로 나눔)
                        byte luma = Luma(cel.pixels[i], cel.pixels[i + 1], cel.pixels[i + 2]);
                        target[(file.height - 1 - canvasY) * stride + left + canvasX] = new Color32(luma, luma, luma, mode);
                    }
                }
            }
        }

        // 휘도 Y = 0.299 R + 0.587 G + 0.114 B (sRGB 값 그대로, 블록벤치 규칙과 같음).
        static byte Luma(byte r, byte g, byte b) => (byte)Mathf.Clamp(Mathf.RoundToInt(0.299f * r + 0.587f * g + 0.114f * b), 0, 255);

        // 레이어 자신부터 감싼 그룹으로 올라가며 처음 만나는 태그. 없으면 Gray.
        static byte ModeOf(PxoFile file, int layer)
        {
            for (int i = layer; i >= 0 && i < file.layers.Count; i = file.layers[i].parent)
                if (Tags.TryGetValue(file.layers[i].name.Trim().ToLowerInvariant(), out byte mode)) return mode;
            return GrayAlpha;
        }

        // 규칙 3절(레이어 불투명도 100%, 섞기 Normal, 픽셀 투명도 0·255만)을 어기면 알린다.
        static void Warn(AssetImportContext ctx, PxoFile file)
        {
            for (int layer = 0; layer < file.layers.Count; layer++)
            {
                var info = file.layers[layer];
                if (info.type != 0 || !file.IsVisible(layer)) continue;
                if (info.opacity < 0.999f) ctx.LogImportWarning($"[PxoImporter] 레이어 '{info.name}'의 불투명도가 100%가 아니다(무시하고 불투명으로 가져옴).");
                if (info.blendMode != 0) ctx.LogImportWarning($"[PxoImporter] 레이어 '{info.name}'의 섞기 방식이 Normal이 아니다(무시함).");
                bool partial = false;
                foreach (var cels in file.frames)
                    if (layer < cels.Length && cels[layer]?.pixels is { } pixels)
                        for (int i = 3; i < pixels.Length && !partial; i += 4)
                            partial = pixels[i] != 0 && pixels[i] != 255;
                if (partial) ctx.LogImportWarning($"[PxoImporter] 레이어 '{info.name}'에 반투명 픽셀이 있다(알파 128 이상은 불투명, 아래는 투명으로 가져옴).");
            }
        }
    }
}
