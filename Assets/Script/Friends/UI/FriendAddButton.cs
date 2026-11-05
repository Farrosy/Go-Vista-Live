using UnityEngine;
using UnityEngine.UI;

/// Tombol pertemanan di baris daftar pemain room (ruang tunggu & menu pause), lihat
/// Docs/Friends/preview-room-tambah-teman.png. Dibuat oleh LobyUISceneController.PopulatePlayerRows.
///   belum berteman → [ikon +] TEMAN  (hijau, kirim permintaan)
///   dia mengirim   → [✓] TERIMA      (hijau, langsung berteman)
///   sudah dikirim  → TERKIRIM        (outline abu-abu, tidak bisa ditekan)
///   sudah berteman → [ikon] TEMAN    (outline hijau, tidak bisa ditekan)
/// Tidak tampil untuk diri sendiri, guest, atau kalau friendlist belum siap. Memperbarui diri tiap 0,5 detik.
public sealed class FriendAddButton : MonoBehaviour {
    private const float RefreshInterval = 0.5f;
    private const float ErrorShowSeconds = 2.5f;
    public const float Width = 200f;
    private const float Height = 64f;

    private enum Look { Hidden, Add, Accept, Sent, Friend, Busy, Failed }

    private int _clientId;
    private RectTransform _slot;
    private Look _look = (Look)(-1);
    private float _nextRefresh;
    private float _errorUntil;
    private bool _busy;

    /// rightX = jarak dari kanan baris (negatif), mis. -330.
    public static void Create(RectTransform row, int clientId, float rightX) {
        RectTransform slot = ChatUiKit.CreateRect("Friend Button Slot", row);
        FriendsUiKit.PlaceInRow(slot, rightX, Width, Height);
        FriendAddButton component = slot.gameObject.AddComponent<FriendAddButton>();
        component._clientId = clientId;
        component._slot = slot;
        component.Refresh();
    }

    private void Update() {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    private void Refresh() {
        if (_busy) return;
        if (Time.unscaledTime < _errorUntil) {
            Apply(Look.Failed);
            return;
        }
        string accountId = AccountId();
        FriendRelation relation = FriendsManager.RelationTo(accountId);
        if (!FriendsManager.IsReady || string.IsNullOrEmpty(accountId) ||
            relation == FriendRelation.Self || relation == FriendRelation.Blocked) {
            Apply(Look.Hidden);
            return;
        }
        Apply(relation == FriendRelation.Friend ? Look.Friend
            : relation == FriendRelation.Outgoing ? Look.Sent
            : relation == FriendRelation.Incoming ? Look.Accept
            : Look.Add);
    }

    /// Bangun ulang isi slot hanya kalau tampilannya berubah.
    private void Apply(Look look) {
        if (look == _look) return;
        _look = look;
        ChatUiKit.DestroyChildren(_slot);
        if (look == Look.Hidden) return;

        ChatUiAssets a = FriendsMenu.SharedAssets;
        Button button;
        switch (look) {
            case Look.Add:
                button = Solid(a, "TEMAN", a.userPlusIcon);
                break;
            case Look.Accept:
                button = Solid(a, "TERIMA", a.checkIcon);
                break;
            case Look.Friend:
                button = FriendsUiKit.CreateOutlineButton("Button", _slot, a, "TEMAN", FriendsTheme.Teal,
                                                          FriendsTheme.TealFill, a.friendsIcon);
                break;
            case Look.Failed:
                button = FriendsUiKit.CreateOutlineButton("Button", _slot, a, "GAGAL", FriendsTheme.Danger,
                                                          FriendsTheme.DangerFill);
                break;
            default: // Sent, Busy
                button = FriendsUiKit.CreateOutlineButton("Button", _slot, a, look == Look.Busy ? "..." : "TERKIRIM",
                                                          FriendsTheme.Neutral, FriendsTheme.NeutralFill);
                break;
        }
        ChatUiKit.Stretch((RectTransform)button.transform, 0f, 0f, 0f, 0f);
        bool clickable = look == Look.Add || look == Look.Accept;
        // Status (TERKIRIM / TEMAN) bukan tombol: matikan komponen Button supaya warnanya tidak ikut diredupkan.
        button.enabled = clickable;
        if (clickable) button.onClick.AddListener(OnClick);
    }

    private Button Solid(ChatUiAssets a, string label, Sprite icon) =>
        FriendsUiKit.CreateSolidButton("Button", _slot, a, label, FriendsTheme.GreenTop, FriendsTheme.GreenBottom,
                                       FriendsTheme.GreenLip, icon);

    private async void OnClick() {
        string accountId = AccountId();
        if (_busy || string.IsNullOrEmpty(accountId)) return;
        bool accept = FriendsManager.RelationTo(accountId) == FriendRelation.Incoming;
        Apply(Look.Busy);
        _busy = true;
        string name = LobbyPresence.NameOf(_clientId);
        FriendResult result = accept
            ? await FriendsManager.AcceptAsync(accountId, name)
            : await FriendsManager.AddByIdAsync(accountId, name);
        if (this == null) return;
        _busy = false;
        if (!result.Success) {
            Debug.LogWarning("[Friends] " + result.Message);
            _errorUntil = Time.unscaledTime + ErrorShowSeconds;
        }
        _nextRefresh = 0f;
        Refresh();
    }

    private string AccountId() {
        LobbyPresence presence = LobbyPresence.Find(_clientId);
        return presence != null ? presence.AccountId : "";
    }
}
