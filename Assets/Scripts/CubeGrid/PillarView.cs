using System.Linq;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 장식 기둥 한 칸(H빔)의 겉모습(프리팹 `Pillar` 하나). 놓인 모양(<see cref="PillarPose"/>: 세움 홈 남북·동서, 눕힘 남북·동서)마다
    /// 같은 모델 `Pillar`를 돌려 둔 자식 하나와, 동서남북 옆면마다 커넥터 1·2·3(`PillarConnector1~3`)을 둔 자식을 갖고,
    /// 지금 값(<see cref="Layout"/>)의 모델만 켠다(꺼진 모델은 콜라이더도 꺼짐). 값은 큐브 격자 빌더가 지을 때 계산해 정한다
    /// (<see cref="CubeGridRules.PillarLayouts"/>). 에디터에서 값을 바꾸면 바로 보인다.
    /// </summary>
    public class PillarView : MonoBehaviour
    {
        [Tooltip("놓인 모양마다 기둥 모델: 세움(홈 남북)·세움(홈 동서)·눕힘(남북)·눕힘(동서)")]
        [SerializeField] GameObject[] poseModels = new GameObject[4];

        [Tooltip("옆면마다 커넥터 1·2·3 모델(북 1·2·3, 동 1·2·3, 남 1·2·3, 서 1·2·3)")]
        [SerializeField] GameObject[] connectorModels = new GameObject[12];

        [Tooltip("놓인 모양과 옆면 커넥터. 큐브 격자 빌더가 지을 때 정한다.")]
        [SerializeField, PerPlacement] PillarLayout layout;

        /// <summary>겉모습 값. 바꾸면 그 값의 모델만 켠다(에디터에서는 빌더가 프리팹 오버라이드로 기록한다).</summary>
        public PillarLayout Layout
        {
            get => layout;
            set
            {
                layout = value;
                ShowLayout();
            }
        }

        /// <summary>켜고 끄는 모델 전부.</summary>
        public GameObject[] Models => poseModels.Concat(connectorModels).ToArray();

        /// <summary>지금 값의 모델만 켜고 나머지는 끈다.</summary>
        public void ShowLayout()
        {
            for (int i = 0; i < poseModels.Length; i++) SetActive(poseModels[i], i == (int)layout.pose);
            for (int i = 0; i < connectorModels.Length; i++) SetActive(connectorModels[i], Wanted(i));
        }

        /// <summary>켜진 모델이 지금 값과 다른가.</summary>
        public bool LayoutMismatch()
        {
            for (int i = 0; i < poseModels.Length; i++)
                if (poseModels[i] != null && poseModels[i].activeSelf != (i == (int)layout.pose)) return true;
            for (int i = 0; i < connectorModels.Length; i++)
                if (connectorModels[i] != null && connectorModels[i].activeSelf != Wanted(i)) return true;
            return false;
        }

        // 커넥터 모델 i(옆면 i / 3, 종류 i % 3 + 1)를 켤지.
        bool Wanted(int i) => (int)layout[i / 3] == i % 3 + 1;

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

#if UNITY_EDITOR
        // 인스펙터에서 값을 바꾸면 한 틱 뒤 모델을 맞춘다(OnValidate 안에서는 다른 오브젝트를 켜고 끌 수 없음).
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !LayoutMismatch()) return;
                foreach (var model in Models) if (model != null) UnityEditor.Undo.RecordObject(model, "기둥 모양");
                ShowLayout();
                foreach (var model in Models) if (model != null) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            };
        }
#endif
    }
}
