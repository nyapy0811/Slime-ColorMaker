using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 메인 메뉴 배경 파노라마 설정(애셋은 Assets/Data/Camera). 그림은 에디터 메뉴 'Slime~ColorMaker~/메뉴 배경 찍기'가
    /// 맵의 씬 뷰 자리에서 360°로 찍어 넣는다. 쓰는 곳: <see cref="MenuPanorama"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/메뉴 배경 파노라마 설정", fileName = "MenuPanoramaSettings")]
    public class MenuPanoramaSettings : ScriptableObject
    {
        [Tooltip("배경 그림(큐브맵). 'Slime~ColorMaker~/메뉴 배경 찍기'가 찍어 넣는다. 비면 메인 메뉴는 단색 배경이다.")]
        public Cubemap panorama;

        [Tooltip("옆으로 도는 속도(도/초). 0이면 멈춰 있다.")]
        public float rotateSpeed = 3f;

        [Tooltip("위아래 각도(도). 0은 수평, +는 아래를 본다.")]
        [Range(-89f, 89f)] public float pitch = 0f;

        [Tooltip("메인 메뉴 카메라의 세로 시야각(도).")]
        [Range(10f, 120f)] public float fieldOfView = 60f;

        [Tooltip("찍을 때 파노라마 그림의 가로 픽셀(세로는 절반). 큐브맵 한 면은 가로 ÷ 4.")]
        public int captureWidth = 4096;

        [Header("찍은 곳 (찍기 메뉴가 적음)")]
        [Tooltip("찍은 맵 씬.")]
        public string sourceScene;

        [Tooltip("찍은 자리(월드 좌표).")]
        public Vector3 capturePosition;
    }
}
