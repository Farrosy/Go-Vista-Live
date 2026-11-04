using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// Di mana chat boleh tampil. Diatur LobyUISceneController.
public enum ChatContext {
    /// Menu utama / lobby / belum masuk room: chat disembunyikan.
    Hidden,
    /// Ruang tunggu room (setelah join, sebelum MULAI). Panel mulai tertutup supaya tidak menutupi daftar pemain.
    WaitingRoom,
    /// Di dalam world/venue. Panel mulai terbuka.
    InGame,
}

/// Panel chat room (gaya Roblox + tampilan mockup Go Vista). UI dibuat lewat kode (ChatPanelBuilder) di
/// pojok kiri atas dan TIDAK bisa digeser. Perilaku:
///  - Tombol 💬 membuka/menutup panel. Saat tertutup, badge merah menghitung pesan belum dibaca.
///  - Pemain tetap bisa jalan selama panel terbuka; input baru dikunci saat kolom ketik fokus (keyboard).
///  - Panel memudar setelah beberapa detik tanpa aktivitas, dan kembali jelas saat ada pesan/disentuh.
/// Emoji diketik dari keyboard Android dan tampil lewat Emoji Fallback TMP (Go Vista > Chat > Setup Emoji Font).
public sealed class ChatPanel : MonoBehaviour {
    public static ChatPanel Instance { get; private set; }

    [SerializeField] private ChatRoom chatRoom;
    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();
    [Tooltip("Detik tanpa aktivitas sebelum panel memudar.")]
    [SerializeField] private float fadeDelay = 10f;
    [Range(0.1f, 1f)]
    [SerializeField] private float fadedAlpha = 0.4f;
    [SerializeField] private float fadeSpeed = 3f;

    private const float OnlineRefreshInterval = 1f;
    private const float NearBottomThreshold = 0.05f;
    private const int MaxUnreadShown = 99;

    private readonly List<KeyValuePair<ChatMessage, RectTransform>> _rows =
        new List<KeyValuePair<ChatMessage, RectTransform>>();
    private ChatPanelRefs _ui;
    private ChatContext _context = ChatContext.Hidden;
    private bool _suppressed;
    private bool _expanded;
    private bool _typing;
    private int _unread;
    private float _lastActivity;
    private float _nextOnlineRefresh;
    private bool _scrollToBottomPending;

    /// Ubah konteks tampil chat. Aman dipanggil walau ChatPanel belum dipasang di scene.
    public static void SetContext(ChatContext context) {
        if (Instance != null) Instance.ApplyContext(context);
    }

    /// Sembunyikan sementara (mis. menu pause terbuka) tanpa mengubah konteks.
    public static void SetSuppressed(bool suppressed) {
        if (Instance == null || Instance._suppressed == suppressed) return;
        Instance._suppressed = suppressed;
        Instance.RefreshVisibility();
    }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("[Chat] Ada lebih dari satu ChatPanel di scene; yang ini dimatikan.", this);
            enabled = false;
            return;
        }
        Instance = this;
        assets.ResolveFallbacks();
        _ui = ChatPanelBuilder.Build(transform, assets);
        WireUi();
        RefreshVisibility();
    }

    private void OnEnable() {
        if (chatRoom == null) chatRoom = ChatRoom.Instance != null ? ChatRoom.Instance : FindFirstObjectByType<ChatRoom>();
        if (chatRoom == null) {
            Debug.LogWarning("[Chat] ChatRoom tidak ditemukan. Jalankan menu Go Vista > Chat > Pasang Chat di MainMultiplayerScene.", this);
            return;
        }
        chatRoom.MessageAdded += OnMessageAdded;
        chatRoom.MessageRemoved += OnMessageRemoved;
        chatRoom.HistoryReset += RebuildRows;
        RebuildRows();
    }

    private void OnDisable() {
        SetTyping(false);
        if (chatRoom == null) return;
        chatRoom.MessageAdded -= OnMessageAdded;
        chatRoom.MessageRemoved -= OnMessageRemoved;
        chatRoom.HistoryReset -= RebuildRows;
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    private void Update() {
        if (_ui == null || !_ui.Canvas.enabled) return;
        if (Time.unscaledTime >= _nextOnlineRefresh) {
            _nextOnlineRefresh = Time.unscaledTime + OnlineRefreshInterval;
            _ui.OnlineText.text = LobbyPresence.All.Count + " online";
        }
        UpdateFade();
    }

    private void LateUpdate() {
        if (!_scrollToBottomPending || _ui == null) return;
        _scrollToBottomPending = false;
        Canvas.ForceUpdateCanvases();
        _ui.Scroll.verticalNormalizedPosition = 0f;
    }

    // ---------------------------------------------------------------- Setup & visibilitas

    private void WireUi() {
        _ui.ToggleButton.onClick.AddListener(Expand);
        _ui.CollapseButton.onClick.AddListener(Collapse);
        _ui.SendButton.onClick.AddListener(SendFromInput);
        _ui.Input.onSubmit.AddListener(_ => SendFromInput());
        _ui.Input.onSelect.AddListener(_ => SetTyping(true));
        _ui.Input.onDeselect.AddListener(_ => SetTyping(false));
        _ui.Input.onEndEdit.AddListener(_ => SetTyping(false));
        _ui.Input.onValueChanged.AddListener(_ => MarkActivity());
        _ui.Panel.gameObject.AddComponent<ChatPanelTouch>().Touched = MarkActivity;
    }

    private void ApplyContext(ChatContext context) {
        if (_context == context) return;
        _context = context;
        if (context == ChatContext.Hidden) {
            SetExpanded(false);
            SetUnread(0);
        } else {
            SetExpanded(context == ChatContext.InGame);
        }
        RefreshVisibility();
    }

    private void RefreshVisibility() {
        if (_ui == null) return;
        bool visible = _context != ChatContext.Hidden && !_suppressed;
        _ui.Canvas.enabled = visible;
        if (!visible) {
            if (_ui.Input.isFocused) _ui.Input.DeactivateInputField();
            SetTyping(false);
        }
    }

    private void Expand() => SetExpanded(true);
    private void Collapse() => SetExpanded(false);

    private void SetExpanded(bool expanded) {
        _expanded = expanded;
        if (!expanded && _ui.Input.isFocused) _ui.Input.DeactivateInputField();
        // Sembunyikan lewat CanvasGroup, bukan SetActive (lihat ChatPanelBuilder.BuildPanel).
        CanvasGroup group = _ui.PanelGroup;
        group.alpha = expanded ? 1f : 0f;
        group.interactable = expanded;
        group.blocksRaycasts = expanded;
        _ui.ToggleButton.gameObject.SetActive(!expanded);
        if (expanded) {
            SetUnread(0);
            MarkActivity();
            _scrollToBottomPending = true;
        } else {
            SetTyping(false);
        }
    }

    // ---------------------------------------------------------------- Kirim & input

    private void SendFromInput() {
        if (chatRoom == null) return;
        ChatSendResult result = chatRoom.TrySend(_ui.Input.text);
        MarkActivity();
        switch (result) {
            case ChatSendResult.Sent:
            case ChatSendResult.Empty:
                _ui.Input.text = "";
                break;
            case ChatSendResult.TooFast:
                // Teks dibiarkan supaya pemain cukup tekan kirim lagi sebentar kemudian.
                break;
            case ChatSendResult.NotConnected:
                Debug.LogWarning("[Chat] Belum terhubung ke room, pesan tidak dikirim.");
                break;
        }
    }

    /// Kunci gerak/kamera hanya selama mengetik, supaya joystick tidak ikut tersentuh di belakang keyboard.
    private void SetTyping(bool typing) {
        if (_typing == typing) return;
        _typing = typing;
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetChatTypingLocked(typing);
        if (typing) MarkActivity();
    }

    // ---------------------------------------------------------------- Pesan

    private void RebuildRows() {
        if (_ui == null) return;
        foreach (KeyValuePair<ChatMessage, RectTransform> row in _rows)
            if (row.Value != null) Destroy(row.Value.gameObject);
        _rows.Clear();
        SetUnread(0);
        if (chatRoom == null) return;

        IReadOnlyList<ChatMessage> messages = chatRoom.Messages;
        int start = Math.Max(0, messages.Count - ChatRules.MaxDisplayedMessages);
        for (int i = start; i < messages.Count; i++)
            AppendRow(messages[i]);
        _scrollToBottomPending = true;
    }

    private void OnMessageAdded(ChatMessage message) {
        IReadOnlyList<ChatMessage> messages = chatRoom.Messages;
        bool isNewest = messages.Count > 0 && ReferenceEquals(messages[messages.Count - 1], message);
        if (!isNewest) {
            // Pesan datang terlambat (urutan waktu lebih awal): susun ulang supaya urutan tetap benar.
            RebuildRows();
            return;
        }

        bool wasNearBottom = _ui.Scroll.verticalNormalizedPosition <= NearBottomThreshold;
        AppendRow(message);
        TrimRows();
        if (wasNearBottom || message.IsLocal) _scrollToBottomPending = true;
        // Badge hanya menghitung pesan pemain lain, bukan pesan sistem join/leave.
        if (!_expanded && !message.IsLocal && !message.IsSystem) SetUnread(_unread + 1);
        MarkActivity();
    }

    private void OnMessageRemoved(ChatMessage message) {
        for (int i = 0; i < _rows.Count; i++) {
            if (!ReferenceEquals(_rows[i].Key, message)) continue;
            if (_rows[i].Value != null) Destroy(_rows[i].Value.gameObject);
            _rows.RemoveAt(i);
            return;
        }
    }

    private void AppendRow(ChatMessage message) {
        RectTransform row = ChatMessageRow.Create(_ui.Content, message, assets);
        _rows.Add(new KeyValuePair<ChatMessage, RectTransform>(message, row));
    }

    private void TrimRows() {
        while (_rows.Count > ChatRules.MaxDisplayedMessages) {
            if (_rows[0].Value != null) Destroy(_rows[0].Value.gameObject);
            _rows.RemoveAt(0);
        }
    }

    private void SetUnread(int count) {
        _unread = Mathf.Max(0, count);
        if (_ui == null) return;
        _ui.UnreadBadge.SetActive(_unread > 0);
        _ui.UnreadText.text = _unread > MaxUnreadShown ? MaxUnreadShown + "+" : _unread.ToString();
    }

    // ---------------------------------------------------------------- Memudar

    private void MarkActivity() => _lastActivity = Time.unscaledTime;

    private void UpdateFade() {
        if (!_expanded) return;
        bool idle = !_typing && Time.unscaledTime - _lastActivity > fadeDelay;
        float target = idle ? fadedAlpha : 1f;
        CanvasGroup group = _ui.PanelGroup;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
    }
}

/// Sentuhan di area panel (termasuk saat scroll) membangunkan panel yang sedang memudar.
public sealed class ChatPanelTouch : MonoBehaviour, IPointerDownHandler {
    public Action Touched;
    public void OnPointerDown(PointerEventData eventData) => Touched?.Invoke();
}
