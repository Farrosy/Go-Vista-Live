using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Satu baris di Daftar Quest: judul + tombol pin. Ketuk baris untuk membuka detail (deskripsi + tujuan).
public sealed class QuestRowView : MonoBehaviour {
    private const string PinnedTag = "  <size=60%><color=#FF9F1C>DIPIN</color></size>";
    private const string DoneColor = "#7FD9A0";
    private const string LockedColor = "#8A97AD";

    [SerializeField] private Image background;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite pinnedSprite;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button pinButton;
    [SerializeField] private Image pinIcon;
    [SerializeField] private Sprite pinOn;
    [SerializeField] private Sprite pinOff;
    [SerializeField] private GameObject details;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI objectives;
    [SerializeField] private VerticalLayoutGroup layout;
    [SerializeField] private float expandedBottomPadding = 24f;

    private readonly StringBuilder _builder = new StringBuilder();
    private string _questId;
    private Action<string> _onToggle;
    private Action<string> _onPin;

    private void Awake() {
        toggleButton.onClick.AddListener(() => _onToggle?.Invoke(_questId));
        pinButton.onClick.AddListener(() => _onPin?.Invoke(_questId));
    }

    public void Bind(QuestService.Entry entry, bool pinned, bool expanded, Action<string> onToggle, Action<string> onPin) {
        QuestData quest = entry.Data;
        _questId = quest.Id;
        _onToggle = onToggle;
        _onPin = onPin;

        background.sprite = pinned ? pinnedSprite : normalSprite;
        title.text = pinned ? quest.title + PinnedTag : quest.title;
        pinIcon.sprite = pinned ? pinOn : pinOff;

        details.SetActive(expanded);
        RectOffset padding = layout.padding;
        padding.bottom = expanded ? Mathf.RoundToInt(expandedBottomPadding) : 0;
        layout.padding = padding;
        if (!expanded) return;

        description.gameObject.SetActive(!string.IsNullOrWhiteSpace(quest.description));
        description.text = quest.description;
        objectives.text = BuildObjectives(entry);
    }

    private string BuildObjectives(QuestService.Entry entry) {
        QuestData quest = entry.Data;
        _builder.Clear();
        for (int i = 0; i < quest.objectives.Length; i++) {
            if (i > 0) _builder.Append('\n');
            string text = quest.sequentialObjectives ? $"{i + 1}. {quest.objectives[i].description}" : "- " + quest.objectives[i].description;
            if (QuestService.IsObjectiveDone(quest, entry.Progress, i))
                _builder.Append("<color=").Append(DoneColor).Append("><s>").Append(text).Append("</s></color>");
            else if (QuestService.IsObjectiveAvailable(quest, entry.Progress, i))
                _builder.Append(text);
            else
                _builder.Append("<color=").Append(LockedColor).Append('>').Append(text).Append("</color>");
        }
        return _builder.ToString();
    }
}
