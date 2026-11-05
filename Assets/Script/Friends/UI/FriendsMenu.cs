using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;

/// Menu TEMAN (Unity Friends). Pasang lewat "Go Vista > Friends > Pasang Friends di MainMultiplayerScene".
///  - Dibuka dari tile TEMAN di menu utama ("Main Menu Friends Button" di LobbyCanvas.prefab). Tile menampilkan
///    jumlah teman online dan badge jumlah permintaan masuk.
///  - Tab TEMAN (online / offline, GABUNG / PENUH / HAPUS), PERMINTAAN (masuk: TERIMA / TOLAK, terkirim: BATAL),
///    dan TAMBAH (nama lengkap "Nama#1234").
///  - Hanya akun terdaftar; guest, memuat, dan gagal ditampilkan di halaman keadaan.
public sealed class FriendsMenu : MonoBehaviour {
    public static FriendsMenu Instance { get; private set; }

    public const string TileName = "Main Menu Friends Button";

    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();

    private static ChatUiAssets _fallbackAssets;
    private readonly FriendsMenuRefs _ui = new FriendsMenuRefs();
    private FriendsTab _tab = FriendsTab.Friends;
    private LobyUISceneController _lobby;
    private Text _tileSubtitle;
    private GameObject _tileBadge;
    private Text _tileBadgeText;
    private bool _tileBound;
    private bool _rebuildPending;
    private bool _busy;

    public bool IsOpen => _ui.Canvas != null && _ui.Canvas.gameObject.activeSelf;

    /// Aset untuk UI friendlist di luar menu ini (tombol di daftar pemain room).
    public static ChatUiAssets SharedAssets {
        get {
            if (Instance != null) return Instance.assets;
            if (_fallbackAssets == null) {
                _fallbackAssets = new ChatUiAssets();
                _fallbackAssets.ResolveFallbacks();
            }
            return _fallbackAssets;
        }
    }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("[Friends] Ada lebih dari satu FriendsMenu di scene; yang ini dimatikan.", this);
            enabled = false;
            return;
        }
        Instance = this;
        assets.ResolveFallbacks();
        FriendsMenuBuilder.Build(transform, assets, _ui);
        // Teks statis menu (judul, keadaan, hasil, petunjuk) mengecil sendiri di layar kecil.
        UiTextFit.ApplyAll(_ui.Canvas.transform);
        WireUi();
    }

    private void WireUi() {
        _ui.CloseButton.onClick.AddListener(Close);
        _ui.CopyButton.onClick.AddListener(CopyOwnName);
        for (int i = 0; i < _ui.Tabs.Length; i++) {
            FriendsTab tab = (FriendsTab)i;
            _ui.Tabs[i].Button.onClick.AddListener(() => ShowTab(tab));
        }
        _ui.SendButton.onClick.AddListener(() => _ = SendRequestAsync());
        // Parameter lambda bernama "text" (bukan "_"), supaya "_ =" di dalamnya benar-benar discard.
        _ui.SearchInput.onSubmit.AddListener(text => _ = SendRequestAsync());
        _ui.StateButton.onClick.AddListener(OnStateButton);
    }

    private void OnEnable() => FriendsManager.Changed += OnFriendsChanged;
    private void OnDisable() => FriendsManager.Changed -= OnFriendsChanged;

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    private void Update() {
        if (!_tileBound) TryBindTile();
        UpdateCopyFeedback();
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close(); // tombol Back Android
    }

    private void LateUpdate() {
        if (!_rebuildPending) return;
        _rebuildPending = false;
        Refresh();
    }

    // ---------------------------------------------------------------- Tile TEMAN di menu utama

    /// Tile ada di LobbyCanvas.prefab (dibuat installer). Dicari tiap frame sampai Main Menu siap.
    private void TryBindTile() {
        if (_lobby == null) _lobby = FindAnyObjectByType<LobyUISceneController>();
        Transform mainMenu = _lobby != null ? _lobby.MainMenuRoot : null;
        if (mainMenu == null) return;
        _tileBound = true;
        Transform tile = FindDeep(mainMenu, TileName);
        if (tile == null) {
            Debug.LogWarning("[Friends] Tile TEMAN tidak ada di Main Menu. Jalankan Go Vista > Friends > " +
                             "Pasang Friends di MainMultiplayerScene.");
            return;
        }
        tile.GetComponent<Button>()?.onClick.AddListener(Open);
        _tileSubtitle = FindDeep(tile, TileName + " Subtitle")?.GetComponent<Text>();
        _tileBadge = FindDeep(tile, TileName + " Badge")?.gameObject;
        _tileBadgeText = _tileBadge != null ? _tileBadge.GetComponentInChildren<Text>(true) : null;
        RefreshTile();
    }

    private void RefreshTile() {
        int incoming = FriendsManager.Incoming.Count;
        if (_tileBadge != null) _tileBadge.SetActive(incoming > 0);
        if (_tileBadgeText != null) _tileBadgeText.text = incoming > 9 ? "9+" : incoming.ToString();
        if (_tileSubtitle != null)
            _tileSubtitle.text = FriendsManager.IsReady ? FriendsManager.OnlineFriendCount + " online" : "Cari & undang";
    }

    private static Transform FindDeep(Transform root, string name) {
        if (root.name == name) return root;
        foreach (Transform child in root) {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ---------------------------------------------------------------- Buka / tutup / tab

    public void Open() {
        _ui.Canvas.gameObject.SetActive(true);
        _ui.ResultBox.SetActive(false);
        ShowTab(FriendsManager.Incoming.Count > 0 ? FriendsTab.Requests : FriendsTab.Friends);
    }

    public void Close() {
        if (_ui.SearchInput.isFocused) _ui.SearchInput.DeactivateInputField();
        _ui.Canvas.gameObject.SetActive(false);
    }

    private void ShowTab(FriendsTab tab) {
        _tab = tab;
        _ui.Scroll.verticalNormalizedPosition = 1f;
        Refresh();
    }

    private void OnFriendsChanged() {
        RefreshTile();
        if (IsOpen) _rebuildPending = true; // digabung per frame; presence bisa datang beruntun.
    }

    private void Refresh() {
        RefreshTile();
        RefreshHeaderAndTabs();
        if (!FriendsManager.IsReady) {
            ShowState(FriendsManager.State == FriendsState.Failed ? StateKind.Failed : StateKind.Unavailable);
            return;
        }
        switch (_tab) {
            case FriendsTab.Add:
                ShowPage(_ui.AddPage);
                break;
            case FriendsTab.Requests:
                ShowList(BuildRequests);
                break;
            default:
                if (FriendsManager.Friends.Count == 0) ShowState(StateKind.NoFriends);
                else ShowList(BuildFriends);
                break;
        }
    }

    private void RefreshHeaderAndTabs() {
        bool ready = FriendsManager.IsReady;
        string ownName = FriendsManager.LocalFriendName;
        _ui.OwnNameGroup.SetActive(ready && !string.IsNullOrEmpty(ownName));
        _ui.OwnNameText.text = ownName;
        _ui.OnlinePill.gameObject.SetActive(ready);
        if (ready) SetPillText(_ui.OnlinePill, _ui.OnlineText, "● " + FriendsManager.OnlineFriendCount + " online");

        for (int i = 0; i < _ui.Tabs.Length; i++) {
            FriendsTabRefs tab = _ui.Tabs[i];
            bool selected = (int)_tab == i;
            tab.Background.color = selected ? FriendsTheme.TabSelected : Color.clear;
            tab.Border.SetActive(selected);
            Color textColor = selected ? Color.white : FriendsTheme.Muted;
            tab.Label.color = textColor;
            if (tab.CounterText == null) {
                tab.Counter.GetComponent<Image>().color = textColor; // ikon TAMBAH
                continue;
            }
            int count = i == (int)FriendsTab.Friends ? FriendsManager.Friends.Count : FriendsManager.Incoming.Count;
            bool show = ready && (i == (int)FriendsTab.Friends || count > 0);
            tab.Counter.SetActive(show);
            if (show) SetCounter(tab, count);
        }
    }

    private static void SetCounter(FriendsTabRefs tab, int count) {
        tab.CounterText.text = count > 99 ? "99+" : count.ToString();
        LayoutElement size = tab.Counter.GetComponent<LayoutElement>();
        size.preferredWidth = Mathf.Max(34f, tab.CounterText.GetPreferredValues(tab.CounterText.text).x + 20f);
    }

    private static void SetPillText(RectTransform pill, TextMeshProUGUI text, string value) {
        text.text = value;
        pill.sizeDelta = new Vector2(text.GetPreferredValues(value).x + 36f, pill.sizeDelta.y);
    }

    private void ShowPage(GameObject page) {
        _ui.ListPage.SetActive(page == _ui.ListPage);
        _ui.AddPage.SetActive(page == _ui.AddPage);
        _ui.StatePage.SetActive(page == _ui.StatePage);
    }

    // ---------------------------------------------------------------- Daftar

    private void ShowList(Action build) {
        ShowPage(_ui.ListPage);
        ChatUiKit.DestroyChildren(_ui.ListContent);
        build();
    }

    private void BuildFriends() {
        var online = new List<Relationship>();
        var offline = new List<Relationship>();
        foreach (Relationship r in FriendsManager.Friends)
            (FriendStatus.From(r.Member).Online ? online : offline).Add(r);
        online.Sort(CompareByName);
        offline.Sort(CompareByName);

        if (online.Count > 0) FriendRows.Section(_ui.ListContent, assets, "ONLINE · " + online.Count);
        foreach (Relationship r in online) AddFriendRow(r);
        if (offline.Count > 0) FriendRows.Section(_ui.ListContent, assets, "OFFLINE · " + offline.Count);
        foreach (Relationship r in offline) AddFriendRow(r);
    }

    private void AddFriendRow(Relationship r) {
        string id = r.Member.Id, name = NameOf(r);
        FriendStatus status = FriendStatus.From(r.Member);
        string room = status.Activity.room;
        FriendRows.Friend(_ui.ListContent, assets, name, status, () => JoinFriend(room),
                          () => Run(FriendsManager.RemoveAsync(id, FriendsManager.BaseName(name))));
    }

    private void BuildRequests() {
        IReadOnlyList<Relationship> incoming = FriendsManager.Incoming;
        IReadOnlyList<Relationship> outgoing = FriendsManager.Outgoing;
        if (incoming.Count == 0 && outgoing.Count == 0) {
            ShowState(StateKind.NoRequests);
            return;
        }
        if (incoming.Count > 0) FriendRows.Section(_ui.ListContent, assets, "PERMINTAAN MASUK · " + incoming.Count);
        foreach (Relationship r in incoming) {
            string id = r.Member.Id, name = FriendsManager.BaseName(NameOf(r));
            FriendRows.Incoming(_ui.ListContent, assets, NameOf(r), () => Run(FriendsManager.AcceptAsync(id, name)),
                                () => Run(FriendsManager.DeclineAsync(id)));
        }
        if (outgoing.Count > 0) FriendRows.Section(_ui.ListContent, assets, "TERKIRIM · " + outgoing.Count);
        foreach (Relationship r in outgoing) {
            string id = r.Member.Id;
            FriendRows.Outgoing(_ui.ListContent, assets, NameOf(r), () => Run(FriendsManager.CancelAsync(id)));
        }
    }

    private static int CompareByName(Relationship a, Relationship b) =>
        string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase);

    private static string NameOf(Relationship r) {
        string name = r.Member?.Profile?.Name;
        return string.IsNullOrEmpty(name) ? "Pemain" : name;
    }

    // ---------------------------------------------------------------- Halaman keadaan

    private enum StateKind { NoFriends, NoRequests, Failed, Unavailable }
    private StateKind _state;

    private void ShowState(StateKind kind) {
        _state = kind;
        ShowPage(_ui.StatePage);
        bool failed = kind == StateKind.Failed;
        _ui.StateIcon.sprite = failed ? assets.refreshIcon : assets.friendsIcon;
        _ui.StateIcon.color = failed ? FriendsTheme.Danger : FriendsTheme.Muted;

        switch (kind) {
            case StateKind.NoFriends:
                SetState("Belum ada teman",
                         "Tambahkan teman dengan nama lengkapnya, atau ketuk TEMAN di daftar pemain room.",
                         "TAMBAH TEMAN");
                break;
            case StateKind.NoRequests:
                SetState("Belum ada permintaan", "Permintaan teman yang masuk dan yang kamu kirim muncul di sini.",
                         "TAMBAH TEMAN");
                break;
            case StateKind.Failed:
                SetState("Gagal memuat daftar teman", "Periksa koneksi internetmu, lalu coba lagi.", "COBA LAGI");
                break;
            default:
                bool loading = FriendsManager.State == FriendsState.Initializing;
                SetState(loading ? "Memuat daftar teman..." : "Fitur teman belum tersedia",
                         FriendsManager.UnavailableMessage(), null);
                break;
        }
    }

    private void SetState(string title, string body, string action) {
        _ui.StateTitle.text = title;
        _ui.StateBody.text = body;
        _ui.StateButton.gameObject.SetActive(action != null);
        if (action == null) return;
        _ui.StateButtonLabel.text = action;
        // COBA LAGI tanpa ikon tambah teman.
        Transform icon = _ui.StateButton.transform.Find("Face/Content/Icon");
        if (icon != null) icon.gameObject.SetActive(_state != StateKind.Failed);
    }

    private void OnStateButton() {
        if (_state == StateKind.Failed) FriendsManager.Retry();
        else ShowTab(FriendsTab.Add);
    }

    // ---------------------------------------------------------------- Aksi

    private async Task SendRequestAsync() {
        if (_busy) return;
        _busy = true;
        _ui.SendButton.interactable = false;
        FriendResult result = await FriendsManager.AddByNameAsync(_ui.SearchInput.text);
        if (this == null) return;
        _busy = false;
        _ui.SendButton.interactable = true;
        ShowResult(result);
        if (result.Success) _ui.SearchInput.text = "";
    }

    private async void Run(Task<FriendResult> action) {
        FriendResult result = await action;
        if (this == null) return;
        if (!result.Success) Debug.LogWarning("[Friends] " + result.Message);
        _rebuildPending = true;
    }

    private void JoinFriend(string room) {
        string error = _lobby != null ? _lobby.JoinFriendRoom(room) : "Lobby tidak ditemukan.";
        if (error != null) {
            Debug.LogWarning("[Friends] " + error);
            _rebuildPending = true;
            return;
        }
        Close(); // Lobby menampilkan dialog "menghubungkan" sendiri.
    }

    private const float CopiedFeedbackSeconds = 1.5f;
    private float _copiedUntil;

    /// Salin nama lengkap "Nama#1234" supaya mudah dibagikan ke teman (ditempel di kolom TAMBAH mereka).
    private void CopyOwnName() {
        string ownName = FriendsManager.LocalFriendName;
        if (string.IsNullOrEmpty(ownName)) return;
        GUIUtility.systemCopyBuffer = ownName;
        _copiedUntil = Time.unscaledTime + CopiedFeedbackSeconds;
        _ui.CopyLabel.text = "TERSALIN";
    }

    private void UpdateCopyFeedback() {
        if (_copiedUntil <= 0f || Time.unscaledTime < _copiedUntil) return;
        _copiedUntil = 0f;
        _ui.CopyLabel.text = "SALIN";
    }

    /// Kotak hasil di tab TAMBAH: hijau + centang kalau berhasil, merah + silang kalau gagal.
    private void ShowResult(FriendResult result) {
        _ui.ResultBox.SetActive(true);
        Color color = result.Success ? ChatTheme.Online : FriendsTheme.Danger;
        _ui.ResultBackground.color = result.Success ? FriendsTheme.TealFill : FriendsTheme.DangerFill;
        _ui.ResultIcon.sprite = result.Success ? assets.checkIcon : assets.closeIcon;
        _ui.ResultIcon.color = color;
        _ui.ResultText.color = color;
        _ui.ResultText.text = result.Message;
    }
}
