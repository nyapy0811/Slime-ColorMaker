using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// UI 캔버스 배율을 픽셀아트가 고르게 보이는 정수배로 정한다(Docs/v2 UI 픽셀아트 규칙 1절). UI 그림 1픽셀은 기준 화면(1920 × 1080)에서 3픽셀이고
    /// 화면 전체는 그림 640 × 360픽셀 판이므로, 화면에 판이 들어가는 가장 큰 정수 n(가로 ÷ 640, 세로 ÷ 360 중 작은 쪽, 최소 1)을 골라
    /// 그림 1픽셀 = 화면 n픽셀이 되게 한다(캔버스 배율 = n ÷ 3). 남는 화면은 여백이 된다(배치는 앵커를 따름).
    /// 1080p는 n = 3(배율 1), 720p는 2, 1440p는 4, 4K는 6이라 지금의 '화면 크기 따라 늘이기(1920 × 1080)'와 같고, 그 사이 해상도에서만 작아진다.
    /// CanvasScaler를 대신해 캔버스에 붙인다(인스펙터의 배율 방식 값은 쓰지 않음).
    /// </summary>
    [AddComponentMenu("Layout/Pixel Canvas Scaler (Slime~ColorMaker~)")]
    public class PixelCanvasScaler : CanvasScaler
    {
        /// <summary>그림 판 크기(그림 픽셀) = 기준 화면 ÷ 3.</summary>
        public static readonly Vector2Int Board = new(640, 360);

        /// <summary>그림 1픽셀이 기준 화면에서 차지하는 픽셀(PxoImporter와 같음).</summary>
        public const int PixelScale = 3;

        protected override void Handle()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas) return;
            Vector2 screen = canvas.renderingDisplaySize; // CanvasScaler의 '화면 크기 따라 늘이기'와 같은 화면 크기
            int n = Mathf.Max(1, Mathf.Min(Mathf.FloorToInt(screen.x / Board.x), Mathf.FloorToInt(screen.y / Board.y)));
            SetScaleFactor((float)n / PixelScale);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }
    }
}
