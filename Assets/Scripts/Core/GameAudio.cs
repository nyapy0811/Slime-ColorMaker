using System.Collections;
using Framework.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace SlimeColorMaker
{
    /// <summary>
    /// 이 게임의 소리 설정. 부팅 씬에 두고 BGM 클립과 믹서를 연결한다.
    /// BGM을 틀고, Framework.Core AudioManager의 소스를 믹서 그룹(BGM/SFX)에 잇고, settings.json의 음량을 적용한다.
    /// 효과음은 소리 연결(이벤트 → 클립, 코어 루프 8단계)이 맡는다.
    /// </summary>
    public class GameAudio : MonoSingleton<GameAudio>
    {
        const string BgmParam = "BGMVolume";
        const string SfxParam = "SFXVolume";
        const float MinLinearVolume = 0.0001f; // 0이면 log10(0)이 -무한대라 막는다.

        [SerializeField] AudioClip bgm;

        [Header("믹서 (음량 조절용)")]
        [SerializeField] AudioMixer audioMixer;
        [SerializeField] AudioMixerGroup bgmGroup;
        [SerializeField] AudioMixerGroup sfxGroup;

        protected override System.Type[] Dependencies => new[] { typeof(AudioManager) };

        protected override void OnAwake()
        {
            AudioManager.Instance.PlayBGM(bgm);
            RouteAudioSources();
            StartCoroutine(ApplySavedVolumeNextFrame());
        }

        // 믹서 값은 Awake에서 바꾸면 반영되지 않아 한 프레임 뒤에 적용한다.
        IEnumerator ApplySavedVolumeNextFrame()
        {
            yield return null;
            SetMixerVolume(BgmParam, GameSettings.Current.bgmVolume);
            SetMixerVolume(SfxParam, GameSettings.Current.sfxVolume);
        }

        // AudioManager는 첫 소스를 BGM, 나머지를 효과음에 쓴다.
        void RouteAudioSources()
        {
            var sources = AudioManager.Instance.GetComponents<AudioSource>();
            if (sources.Length == 0) return;
            sources[0].outputAudioMixerGroup = bgmGroup;
            for (int i = 1; i < sources.Length; i++) sources[i].outputAudioMixerGroup = sfxGroup;
        }

        void SetMixerVolume(string param, float linear)
        {
            if (!audioMixer) return;
            audioMixer.SetFloat(param, 20f * Mathf.Log10(Mathf.Max(linear, MinLinearVolume)));
        }
    }
}
