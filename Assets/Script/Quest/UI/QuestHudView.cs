using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Tombol Quest di HUD (dengan tanda ! untuk quest baru) dan panel modal Daftar Quest.
/// Panel menggelapkan layar dan mengunci kontrol; ketuk di luar panel atau tombol × untuk menutup.
public sealed class QuestHudView : MonoBehaviour {
    private const float BadgePopSeconds = 0.35f;

    [Header("Tombol HUD")]
    [SerializeField] private Canvas buttonCanvas;
    [SerializeField] private Button openButton;
    [SerializeField] private RectTransform badge;

    [Header("Panel")]
    [SerializeField] private Canvas panelCanvas;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private QuestRowView rowTemplate;
    [SerializeField] private GameObject emptyLabel;
    [SerializeField] private float headerHeight = 110f;
    [SerializeField] private float bottomPadding = 28f;
    [SerializeField] private float emptyHeight = 140f;
    [SerializeField] private float maxCardHeight = 820f;

    private readonly List<QuestRowView> _rows = new List<QuestRowView>();
    private readonly HashSet<string> _expanded = new HashSet<string>();
    private bool _open;
    private bool _badgeShown;
    private float _badgeShownAt;
    private bool _dirty;

    private void Awake() {
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        backdropButton.onClick.AddListener(Close);
        rowTemplate.gameObject.SetActive(false);
        panelCanvas.enabled = false;
        badge.gameObject.SetActive(false);
    }

    private void OnEnable() => QuestService.Changed += OnQuestsChanged;

    private void OnDisable() {
        QuestService.Changed -= OnQuestsChanged;
        if (_open) Close();
    }

    private void OnQuestsChanged() => _dirty = true;

    private void Update() {
        bool hasPlayer = PlayerController.Local != null;
        // Disembunyikan selama layar mati (semua penyebab), muncul lagi setelah bangkit.
        buttonCanvas.enabled = hasPlayer && !PlayerInputHub.IsDeathLocked;
        if (_open && (!hasPlayer || OverlayActive())) Close();
        UpdateBadge();
        if (_open && _dirty) Rebuild();
    }

    private static bool OverlayActive() => PlayerInputHub.Instance != null && PlayerInputHub.Instance.OverlayActive;

    private void UpdateBadge() {
        bool show = QuestService.HasNewQuest;
        if (show && !_badgeShown) _badgeShownAt = Time.unscaledTime;
        _badgeShown = show;
        badge.gameObject.SetActive(show);
        if (show) badge.localScale = Vector3.one * HudAnimation.Pop((Time.unscaledTime - _badgeShownAt) / BadgePopSeconds);
    }

    public void Open() {
        if (_open || PlayerController.Local == null || OverlayActive()) return;
        _open = true;
        panelCanvas.enabled = true;
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(true);
        // Kapan tanda ! hilang: saat panel dibuka.
        QuestService.MarkQuestsSeen();
        Rebuild();
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Close() {
        if (!_open) return;
        _open = false;
        panelCanvas.enabled = false;
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(false);
    }

    private void Rebuild() {
        _dirty = false;
        List<QuestService.Entry> quests = QuestService.GetActiveQuests();
        string pinned = QuestService.PinnedQuestId;

        while (_rows.Count < quests.Count) {
            QuestRowView row = Instantiate(rowTemplate, listContent);
            row.name = "Quest Row " + _rows.Count;
            _rows.Add(row);
        }
        for (int i = 0; i < _rows.Count; i++) {
            bool used = i < quests.Count;
            _rows[i].gameObject.SetActive(used);
            if (!used) continue;
            string id = quests[i].Data.Id;
            _rows[i].Bind(quests[i], id == pinned, _expanded.Contains(id), ToggleExpanded, TogglePin);
        }
        // Detail quest yang sudah selesai tidak perlu diingat lagi.
        _expanded.RemoveWhere(id => !QuestService.IsActive(id));
        emptyLabel.SetActive(quests.Count == 0);
        FitCard(quests.Count == 0);
    }

    /// Tinggi panel mengikuti isi daftar, dengan batas maksimum (selebihnya bisa di-scroll).
    private void FitCard(bool empty) {
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        float listHeight = empty ? emptyHeight : listContent.rect.height;
        float height = Mathf.Min(headerHeight + listHeight + bottomPadding, maxCardHeight);
        card.sizeDelta = new Vector2(card.sizeDelta.x, height);
        scroll.vertical = headerHeight + listHeight + bottomPadding > maxCardHeight;
    }

    private void ToggleExpanded(string questId) {
        if (!_expanded.Remove(questId)) _expanded.Add(questId);
        Rebuild();
    }

    private void TogglePin(string questId) {
        QuestService.TogglePin(questId);
        Rebuild();
        // Quest yang baru dipin naik ke atas, jadi tampilkan bagian atas daftar.
        if (QuestService.PinnedQuestId == questId) scroll.verticalNormalizedPosition = 1f;
    }
}
