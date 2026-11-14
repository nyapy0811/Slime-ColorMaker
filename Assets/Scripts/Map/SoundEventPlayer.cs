using Framework.Core;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 소리 연결. 맵 기본 프리팹(MapRoot)에 두고, 이벤트를 받아 소리 연결 애셋의 효과음을 낸다.
    /// 기물은 소리를 직접 내지 않으므로 소리를 바꾸거나 더할 때 이곳과 애셋만 고친다.
    /// </summary>
    public class SoundEventPlayer : MonoBehaviour
    {
        [SerializeField] SoundEventConfig config;

        void OnEnable()
        {
            EventBus.Subscribe<ColorAbsorbed>(OnColorAbsorbed);
            EventBus.Subscribe<ColorRejected>(OnColorRejected);
            EventBus.Subscribe<DoorOpened>(OnDoorOpened);
            EventBus.Subscribe<LaserPassed>(OnLaserPassed);
            EventBus.Subscribe<CheckpointReached>(OnCheckpointReached);
            EventBus.Subscribe<MapCleared>(OnMapCleared);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ColorAbsorbed>(OnColorAbsorbed);
            EventBus.Unsubscribe<ColorRejected>(OnColorRejected);
            EventBus.Unsubscribe<DoorOpened>(OnDoorOpened);
            EventBus.Unsubscribe<LaserPassed>(OnLaserPassed);
            EventBus.Unsubscribe<CheckpointReached>(OnCheckpointReached);
            EventBus.Unsubscribe<MapCleared>(OnMapCleared);
        }

        void OnColorAbsorbed(ColorAbsorbed e) => Play(config.colorAbsorbed);
        void OnColorRejected(ColorRejected e) => Play(config.colorRejected);
        void OnDoorOpened(DoorOpened e) => Play(config.doorOpened);
        void OnLaserPassed(LaserPassed e) => Play(config.laserPassed);
        void OnCheckpointReached(CheckpointReached e) => Play(config.checkpointReached);
        void OnMapCleared(MapCleared e) => Play(config.mapCleared);

        static void Play(AudioClip clip)
        {
            if (clip != null) AudioManager.Instance.PlaySFX(clip);
        }
    }
}
