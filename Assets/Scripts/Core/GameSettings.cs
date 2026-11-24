using Framework.Core;

namespace SlimeColorMaker
{
    /// <summary>
    /// 설정값(음량, 마우스 감도)을 settings.json 하나로 관리한다. SaveManager의 범용 JSON API 위에서 동작한다.
    /// 값들이 한 파일에 같이 저장되므로 하나만 바뀌어도 늘 전체를 다시 저장한다.
    /// </summary>
    public static class GameSettings
    {
        const string FileName = "settings.json";

        static SettingsData current;

        public static SettingsData Current
        {
            get
            {
                if (current == null) current = SaveManager.Instance.LoadJson<SettingsData>(FileName);
                return current;
            }
        }

        /// <summary>마우스 감도: 설정 화면에서 정한 값, 정하지 않았으면 <paramref name="fallback"/>(카메라 설정 애셋의 기본값).</summary>
        public static float MouseSensitivity(float fallback) => Current.mouseSensitivity > 0f ? Current.mouseSensitivity : fallback;

        public static void Save() => SaveManager.Instance.SaveJson(FileName, Current);
    }
}
