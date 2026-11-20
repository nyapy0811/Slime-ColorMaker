using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 메인 메뉴 배경 파노라마(메인 메뉴 카메라에 붙임). 설정 애셋(<see cref="MenuPanoramaSettings"/>)의 그림을 하늘로 그리고
    /// 카메라를 제자리에서 천천히 옆으로 돌린다. 그림이 없으면 카메라의 단색 배경 그대로다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MenuPanorama : MonoBehaviour
    {
        [SerializeField] MenuPanoramaSettings settings;

        [Tooltip("하늘 셰이더(Skybox/Cubemap). 빌드에 들어가게 여기서 참조한다.")]
        [SerializeField] Shader skyboxShader;

        Camera view;
        float yaw;

        void Awake()
        {
            view = GetComponent<Camera>();
            if (settings == null || settings.panorama == null || skyboxShader == null) return;
            var material = new Material(skyboxShader);
            material.SetTexture("_Tex", settings.panorama);
            if (!TryGetComponent(out Skybox sky)) sky = gameObject.AddComponent<Skybox>();
            sky.material = material;
            view.clearFlags = CameraClearFlags.Skybox;
            yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (settings == null || settings.panorama == null) return;
            yaw = Mathf.Repeat(yaw + settings.rotateSpeed * Time.unscaledDeltaTime, 360f);
            transform.rotation = Quaternion.Euler(settings.pitch, yaw, 0f);
            view.fieldOfView = settings.fieldOfView;
        }
    }
}
