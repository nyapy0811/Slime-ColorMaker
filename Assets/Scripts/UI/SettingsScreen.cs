using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 설정 화면(UIScene). 메인 메뉴와 일시정지 메뉴의 '설정' 버튼이 연다(<see cref="Current"/>).
    /// 줄은 BGM·효과음(0~100, 1 단위, 저장은 0~1)과 마우스 감도(0.005~0.1, 0.005 단위)이고, 슬라이더를 움직이면 바로 반영하고(음량은 믹서, 감도는 카메라가 매 프레임 읽음)
    /// 닫을 때 settings.json에 저장한다. 되돌리기는 없다. 열 때 뒤로 가기 스택에 '닫기'를 넣어 ESC로 닫힌다(일시정지에서 열었으면 일시정지로 돌아감).
    /// 일시정지 메뉴보다 위, 확인 창보다 아래에 그린다. 시간을 쓰지 않으므로 일시정지 중에도 동작한다.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        const float SensitivityStep = 0.005f; // 감도 슬라이더 1칸

        [SerializeField] GameObject panel;
        [SerializeField] Slider bgmSlider;
        [SerializeField] TMP_Text bgmValue;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text sfxValue;
        [SerializeField] Slider sensitivitySlider;
        [SerializeField] TMP_Text sensitivityValue;
        [SerializeField] Button closeButton;

        [Tooltip("마우스 감도 기본값을 읽을 3인칭 카메라 설정(Assets/Data/Camera/ThirdPersonCameraSettings). 설정 화면에서 정한 적이 없으면 이 값을 보인다.")]
        [SerializeField] ThirdPersonCameraSettings cameraSettings;

        /// <summary>지금 UI 씬의 설정 화면(없으면 null).</summary>
        public static SettingsScreen Current { get; private set; }

        void Awake()
        {
            panel.SetActive(false);
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            closeButton.onClick.AddListener(Close);
        }

        void OnEnable() => Current = this;

        void OnDisable()
        {
            BackStack.Remove(Close);
            if (Current == this) Current = null;
        }

        public void Open()
        {
            if (panel.activeSelf) return;
            var settings = GameSettings.Current;
            bgmSlider.SetValueWithoutNotify(Mathf.Round(settings.bgmVolume * 100f));
            sfxSlider.SetValueWithoutNotify(Mathf.Round(settings.sfxVolume * 100f));
            sensitivitySlider.SetValueWithoutNotify(Mathf.Round(GameSettings.MouseSensitivity(cameraSettings.mouseSensitivity) / SensitivityStep));
            ShowValues();
            panel.SetActive(true);
            BackStack.Push(Close);
        }

        void Close()
        {
            BackStack.Remove(Close);
            panel.SetActive(false);
            GameSettings.Save();
        }

        void OnBgmChanged(float value)
        {
            GameSettings.Current.bgmVolume = value / 100f;
            ApplyVolumes();
        }

        void OnSfxChanged(float value)
        {
            GameSettings.Current.sfxVolume = value / 100f;
            ApplyVolumes();
        }

        void OnSensitivityChanged(float value)
        {
            GameSettings.Current.mouseSensitivity = value * SensitivityStep;
            ShowValues();
        }

        void ApplyVolumes()
        {
            GameAudio.Instance.ApplyVolumes();
            ShowValues();
        }

        void ShowValues()
        {
            bgmValue.text = bgmSlider.value.ToString("0");
            sfxValue.text = sfxSlider.value.ToString("0");
            sensitivityValue.text = (sensitivitySlider.value * SensitivityStep).ToString("0.000");
        }
    }
}
