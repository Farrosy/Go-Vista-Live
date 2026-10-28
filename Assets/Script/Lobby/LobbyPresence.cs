using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

/// <summary>
/// Penanda "pemain ini ada di room". Setiap client membuat satu objek ini saat terhubung
/// (LobyUISceneController.SpawnPresence), dan Normcore otomatis menghapusnya saat pemiliknya keluar.
/// Jadi All = semua pemain yang sedang terhubung, termasuk yang belum masuk world.
/// Normcore tidak punya API untuk daftar client di room, jadi cara ini yang dipakai.
///
/// Prefab: Assets/Resources/Lobby Presence.prefab (harus di folder Resources supaya bisa Realtime.Instantiate).
///
/// TODO (next develop): tambahkan RealtimeComponent + model berisi username di prefab ini,
/// supaya daftar pemain menampilkan nama akun, bukan "Player N".
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public sealed class LobbyPresence : MonoBehaviour {
    public const string PrefabName = "Lobby Presence";

    private static readonly List<LobbyPresence> _all = new List<LobbyPresence>();
    public static IReadOnlyList<LobbyPresence> All => _all;

    private RealtimeView _view;

    public int ClientID => _view != null ? _view.ownerIDSelf : -1;
    public bool IsLocal => _view != null && _view.isOwnedLocallySelf;

    public static bool HasLocal() {
        foreach (LobbyPresence presence in _all) {
            if (presence.IsLocal)
                return true;
        }
        return false;
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
    }

    private void OnDisable() {
        _all.Remove(this);
    }
}
