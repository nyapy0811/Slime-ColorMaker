using System;

namespace SlimeColorMaker
{
    /// <summary>settings.json으로 저장되는 설정값. v1과 같은 형식이라 v1 설정이 그대로 이어진다.</summary>
    [Serializable]
    public class SettingsData
    {
        public float bgmVolume = 0.3f; // 저장된 설정이 없을 때의 기본값. 1(0 dB)은 너무 크다.
        public float sfxVolume = 1f;
        public float mouseSensitivity = 0.1f;
    }
}
