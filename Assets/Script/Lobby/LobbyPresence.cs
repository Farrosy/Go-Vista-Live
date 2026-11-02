using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

/// <summary>
/// Penanda "pemain ini ada di room". Setiap client membuat satu objek ini saat terhubung
/// (LobyUISceneController.SpawnPresence), dan Normcore otomatis menghapusnya saat pemiliknya keluar.
/// Jadi All = semua pemain yang sedang terhubung, termasuk yang belum masuk world.
/// Normcore tidak punya API untuk daftar client di room, jadi cara ini yang dipakai.
///
/// Membawa juga nama pemain (PlayerProgress.playerName, diisi di menu KARAKTER). Pemilik objek
/// menulis namanya ke model, dan pemain lain membacanya lewat NameOf() untuk daftar pemain.
///
/// Prefab: Assets/Resources/Lobby Presence.prefab (harus di folder Resources supaya bisa Realtime.Instantiate).
/// Komponen ini harus terdaftar di daftar Components milik RealtimeView prefab tersebut.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public sealed class LobbyPresence : RealtimeComponent<LobbyPresenceModel> {
    public const string PrefabName = "Lobby Presence";

    private static readonly List<LobbyPresence> _all = new List<LobbyPresence>();
    public static IReadOnlyList<LobbyPresence> All => _all;

    private RealtimeView _view;

    public int ClientID => _view != null ? _view.ownerIDSelf : -1;
    public bool IsLocal => _view != null && _view.isOwnedLocallySelf;

    /// <summary>Nama mentah dari model (bisa kosong). Untuk tampilan pakai NameOf().</summary>
    public string PlayerName => model != null ? model.playerName : "";

    public static bool HasLocal() {
        foreach (LobbyPresence presence in _all) {
            if (presence.IsLocal)
                return true;
        }
        return false;
    }

    /// <summary>Nama tampilan client ini: nama yang diisi pemain, atau "Player N" kalau kosong/belum ada.</summary>
    public static string NameOf(int clientId) {
        foreach (LobbyPresence presence in _all) {
            if (presence.ClientID == clientId)
                return PlayerNameRules.DisplayName(presence.PlayerName, clientId);
        }
        return PlayerNameRules.DisplayName("", clientId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        _all.Clear();
    }

    private void Awake() {
        _view = GetComponent<RealtimeView>();
    }

    private void OnEnable() {
        _all.Add(this);
        ProgressService.PlayerNameChanged += OnLocalPlayerNameChanged;
    }

    private void OnDisable() {
        _all.Remove(this);
        ProgressService.PlayerNameChanged -= OnLocalPlayerNameChanged;
    }

    protected override void OnRealtimeModelReplaced(LobbyPresenceModel previousModel, LobbyPresenceModel currentModel) {
        if (currentModel != null && IsLocal)
            PublishName(ProgressService.Current.playerName);
    }

    private void OnLocalPlayerNameChanged(string playerName) {
        if (IsLocal)
            PublishName(playerName);
    }

    private void PublishName(string playerName) {
        if (model == null)
            return;
        // Rapikan lagi di sisi pengirim; penerima juga merapikan lewat PlayerNameRules.DisplayName.
        string clean = PlayerNameRules.Sanitize(playerName);
        if (model.playerName != clean)
            model.playerName = clean;
    }
}
