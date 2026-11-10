using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 소리 연결(10.2, 코드 흐름 Q502): 이벤트마다 낼 효과음. 비워 둔 칸은 소리를 내지 않는다.
    /// 지금은 있는 소리로 임시 연결했고, 효과음은 모두 SFX 믹서 그룹으로 나간다(5종 나누기는 설정 화면을 설계할 때).
    /// </summary>
    [CreateAssetMenu(menuName = "Slime ~ColorMaker~/소리 연결", fileName = "SoundEventConfig")]
    public class SoundEventConfig : ScriptableObject
    {
        public AudioClip colorAbsorbed;
        [Tooltip("문·목적지가 지금 색을 받아들이지 않음")]
        public AudioClip colorRejected;
        public AudioClip doorOpened;
        public AudioClip laserPassed;
        public AudioClip checkpointReached;
        public AudioClip mapCleared;
    }
}
