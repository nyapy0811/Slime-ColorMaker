using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 선택 화면(MainMenu 씬). 왼쪽은 맵 목록 애셋 순서의 맵 버튼(해금 없음),
    /// 오른쪽은 고른 맵의 미리보기(썸네일, 이름, 클리어 표시와 최고 시간)와 '시작'·'이어하기'다.
    /// '이어하기'는 그 맵에 체크포인트가 저장돼 있을 때만 보인다. '시작'은 경고가 필요하면 확인 창을 거친다.
    /// 열 때 뒤로 가기 스택에 '뒤로'를 넣어 ESC로 메인 메뉴로 돌아간다.
    /// </summary>
    public class MapSelectScreen : MonoBehaviour
    {
        [SerializeField] MapList mapList;
        [SerializeField] GameObject panel;

        [Tooltip("맵 버튼 본보기(꺼 둠). 맵마다 복제해 같은 부모 아래에 둔다.")]
        [SerializeField] MapSelectItem itemTemplate;

        [SerializeField] Image thumbnail;
        [SerializeField] GameObject noThumbnailLabel;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text recordText;
        [SerializeField] Button startButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button backButton;

        readonly List<MapSelectItem> items = new();
        MapListEntry selected;
        Action onBack;

        void Awake()
        {
            panel.SetActive(false);
            itemTemplate.gameObject.SetActive(false);
            startButton.onClick.AddListener(() => SceneFlow.AskAndStartMap(selected));
            continueButton.onClick.AddListener(() => SceneFlow.StartMap(selected, true));
            backButton.onClick.AddListener(Close);
        }

        void OnDisable() => BackStack.Remove(Close);

        /// <summary>화면을 연다. '뒤로'나 ESC로 닫히면 <paramref name="onClosed"/>를 부른다.</summary>
        public void Open(Action onClosed)
        {
            onBack = onClosed;
            if (items.Count == 0) BuildItems();
            panel.SetActive(true);
            if (mapList.maps.Count > 0) Select(selected ?? mapList.maps[0]);
            BackStack.Push(Close);
        }

        void Close()
        {
            BackStack.Remove(Close);
            panel.SetActive(false);
            var closed = onBack;
            onBack = null;
            closed?.Invoke();
        }

        void BuildItems()
        {
            var progress = ProgressStore.Current;
            foreach (var entry in mapList.maps)
            {
                var item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.gameObject.SetActive(true);
                var record = progress.FindMap(entry.mapId);
                string status = record is { cleared: true } ? $"클리어  {TimeFormat.ClearTime(record.bestTime)}" : "";
                if (HasCheckpoint(entry)) status = status.Length > 0 ? status + "\n체크포인트" : "체크포인트";
                item.Show(entry.displayName, status, () => Select(entry));
                items.Add(item);
            }
        }

        void Select(MapListEntry entry)
        {
            selected = entry;
            for (int i = 0; i < items.Count; i++) items[i].SetSelected(mapList.maps[i] == entry);

            thumbnail.sprite = entry.thumbnail;
            thumbnail.color = entry.thumbnail != null ? Color.white : new Color(0.22f, 0.22f, 0.25f);
            noThumbnailLabel.SetActive(entry.thumbnail == null);
            nameText.text = entry.displayName;

            var record = ProgressStore.Current.FindMap(entry.mapId);
            string text = record is { cleared: true } ? $"클리어함 · 최고 기록 {TimeFormat.ClearTime(record.bestTime)}" : "아직 클리어하지 않았습니다";
            if (HasCheckpoint(entry)) text += "\n저장된 체크포인트가 있습니다";
            recordText.text = text;
            continueButton.gameObject.SetActive(HasCheckpoint(entry));
        }

        static bool HasCheckpoint(MapListEntry entry)
        {
            var progress = ProgressStore.Current;
            return progress.hasCheckpoint && progress.checkpoint.mapId == entry.mapId;
        }
    }
}
