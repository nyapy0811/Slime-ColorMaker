using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 화면 위 TextMeshPro 글자를 화면 픽셀 격자에 맞춘다(Docs/v2 UI 픽셀아트 규칙). 비트맵 폰트(Point 필터)는 글자 기준점이 화면 픽셀 사이(예: 가운데 정렬로 반 픽셀)에
    /// 놓이면 글자 픽셀이 2·4픽셀로 갈려 깨져 보이므로, 캔버스가 배치와 글자 모양을 다 만든 직후(<see cref="Canvas.willRenderCanvases"/>) 글자마다
    /// 첫 보이는 글자의 기준점(왼쪽·밑줄)이 정수 화면 픽셀에 오게 글자 그림 전체를 1픽셀 미만만큼 옮긴다. 글자끼리의 간격은 폰트 픽셀의 정수배라 하나만 맞추면 모두 맞는다.
    /// 글자가 바뀌면(TextMeshPro가 모양을 다시 만듦) 옮긴 값을 잊고 다시 맞추고, 창이 움직이거나 해상도가 바뀌어도 매 프레임 확인해 맞춘다.
    /// 컴포넌트를 붙이지 않고 Play 때 저절로 모든 글자에 적용된다(UI 캔버스의 글자만, 3D 글자는 뺌).
    /// </summary>
    public static class PixelSnapText
    {
        static readonly Dictionary<TextMeshProUGUI, Vector3> applied = new(); // 글자마다 지금 옮겨 둔 값(글자 로컬 좌표)
        static readonly List<TextMeshProUGUI> removed = new();
        static readonly List<(TextMeshProUGUI text, Vector3 offset)> moved = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            applied.Clear();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            // UI 배치·그리기 갱신(CanvasUpdateRegistry)이 먼저 등록돼야 그 뒤에 맞춘다.
            _ = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases -= Snap;
            Canvas.willRenderCanvases += Snap;
        }

        // 모양을 다시 만들었으면 옮긴 값이 없어졌다.
        static void OnTextChanged(Object changed)
        {
            if (changed is TextMeshProUGUI text) applied[text] = Vector3.zero;
        }

        static void Snap()
        {
            removed.Clear();
            moved.Clear();
            foreach (var (text, current) in applied)
            {
                if (text == null) { removed.Add(text); continue; }
                if (!text.isActiveAndEnabled || !TryGetOffset(text, out var offset)) continue;
                var delta = offset - current;
                if (delta.sqrMagnitude < 1e-8f) continue;
                Move(text, delta);
                moved.Add((text, offset));
            }
            // 사전을 도는 중에는 고칠 수 없어 끝난 뒤에 고친다.
            foreach (var text in removed) applied.Remove(text);
            foreach (var (text, offset) in moved) applied[text] = offset;
        }

        // 원래(옮기기 전) 첫 글자 기준점을 정수 화면 픽셀로 보내는 이동(글자 로컬 좌표).
        static bool TryGetOffset(TextMeshProUGUI text, out Vector3 offset)
        {
            offset = Vector3.zero;
            var info = text.textInfo;
            var canvas = text.canvas;
            if (info == null || canvas == null) return false;
            int i = 0;
            while (i < info.characterCount && !info.characterInfo[i].isVisible) i++;
            if (i >= info.characterCount) return false;
            var character = info.characterInfo[i];
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var world = text.transform.TransformPoint(new Vector3(character.origin, character.baseLine, 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            var target = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y));
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(text.rectTransform, target, camera, out var targetWorld)) return false;
            offset = text.transform.InverseTransformPoint(targetWorld) - text.transform.InverseTransformPoint(world);
            offset.z = 0f;
            return true;
        }

        static void Move(TextMeshProUGUI text, Vector3 delta)
        {
            var info = text.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var vertices = info.meshInfo[m].vertices;
                if (vertices == null) continue;
                int count = info.meshInfo[m].vertexCount;
                for (int v = 0; v < count; v++) vertices[v] += delta;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
