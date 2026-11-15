using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 장식 기둥 한 칸의 겉모습(프리팹 `Pillar` 하나). 모양(<see cref="Shape"/>: 몸통만·받침 + 몸통·몸통 + 머리·받침 + 몸통 + 머리)마다
    /// 모델 하나(`PillarShaft`·`PillarBase`·`PillarTop`·`Pillar`)를 자식으로 두고, 지금 모양의 모델만 켠다(꺼진 모델은 콜라이더도 꺼짐).
    /// 모양은 큐브 격자 빌더가 그룹으로 계산해 정한다(<see cref="CubeGridRules.PillarShapes"/>). 에디터에서 값을 바꾸면 바로 보인다.
    /// </summary>
    public class PillarView : MonoBehaviour
    {
        [Tooltip("몸통만(그룹의 가운데 칸, 또는 머리·받침이 없는 설정)")]
        [SerializeField] GameObject shaftModel;

        [Tooltip("받침 + 몸통(받침 설정 그룹의 맨 아래 칸)")]
        [SerializeField] GameObject baseModel;

        [Tooltip("몸통 + 머리(머리 설정 그룹의 맨 위 칸)")]
        [SerializeField] GameObject topModel;

        [Tooltip("받침 + 몸통 + 머리(머리·받침 설정의 한 칸 그룹)")]
        [SerializeField] GameObject fullModel;

        [Tooltip("모양. 큐브 격자 빌더가 지을 때 정한다.")]
        [SerializeField] PillarShape shape = PillarShape.Full;

        /// <summary>모양. 바꾸면 그 모양의 모델만 켠다(에디터에서는 빌더가 프리팹 오버라이드로 기록한다).</summary>
        public PillarShape Shape
        {
            get => shape;
            set
            {
                shape = value;
                ShowShape();
            }
        }

        /// <summary>모양마다의 모델. 순서는 <see cref="PillarShape"/>와 같다.</summary>
        public GameObject[] Models => new[] { shaftModel, baseModel, topModel, fullModel };

        /// <summary>지금 모양의 모델만 켜고 나머지는 끈다.</summary>
        public void ShowShape()
        {
            var models = Models;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(i == (int)shape);
        }

        /// <summary>켜진 모델이 지금 모양과 다른가.</summary>
        public bool ShapeMismatch()
        {
            var models = Models;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null && models[i].activeSelf != (i == (int)shape)) return true;
            return false;
        }

#if UNITY_EDITOR
        // 인스펙터에서 모양을 바꾸면 한 틱 뒤 모델을 맞춘다(OnValidate 안에서는 다른 오브젝트를 켜고 끌 수 없음).
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !ShapeMismatch()) return;
                foreach (var model in Models) if (model != null) UnityEditor.Undo.RecordObject(model, "기둥 모양");
                ShowShape();
                foreach (var model in Models) if (model != null) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            };
        }
#endif
    }
}
