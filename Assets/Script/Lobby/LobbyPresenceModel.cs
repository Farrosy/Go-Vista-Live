using Normal.Realtime;

/// Data LobbyPresence yang dikirim ke pemain lain. Kode modelnya dibuat otomatis oleh source
/// generator Normcore (karena partial + [RealtimeModel]).
[RealtimeModel]
public partial class LobbyPresenceModel {
    // Reliable: nama jarang berubah dan harus sampai.
    [RealtimeProperty(1, true, true)] private string _playerName;
    // PlayerId Unity Authentication pemilik, untuk tombol tambah teman di daftar pemain room.
    // Kosong untuk akun guest (friendlist hanya untuk akun terdaftar).
    [RealtimeProperty(2, true, true)] private string _accountId;
}
