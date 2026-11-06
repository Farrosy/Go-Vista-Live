using Normal.Realtime;

/// Satu pesan chat. Kode model dibuat otomatis oleh source generator Normcore (partial + [RealtimeModel]).
///
/// createMetaModel = model punya OWNER yang dijaga server Normcore. Pengirim menjadikan dirinya owner
/// sebelum pesan dimasukkan ke room (ChatRoom.TrySend). Server menolak owner selain clientID pengirim
/// yang asli, jadi penerima memakai ownerIDSelf (bukan data yang ditulis pengirim) untuk tahu siapa
/// pengirimnya, lalu mengambil namanya dari LobbyPresence milik client itu. Pesan juga tidak bisa
/// diubah/dihapus client lain karena preventOwnershipTakeover.
///
/// Semua properti reliable dan diisi SEBELUM model dimasukkan ke ChatRoomModel.messages.
[RealtimeModel(createMetaModel: true)]
public partial class ChatMessageModel {
    // Cadangan untuk pesan yang pengirimnya sudah keluar room (owner sudah tidak ada). Tidak dipakai
    // selama pengirim masih di room; nama asli diambil dari LobbyPresence.
    [RealtimeProperty(1, true)] private string _senderName;
    [RealtimeProperty(2, true)] private int _senderId;
    [RealtimeProperty(3, true)] private string _text;
    // Waktu server Normcore (detik sejak epoch Unix), sama untuk semua client, dipakai untuk urutan.
    [RealtimeProperty(4, true)] private double _sentAt;
    // Hash akun pengirim (ChatIdentity.LocalKey). Tetap sama walau pengirim rejoin dengan clientID baru,
    // jadi pesan lamanya tetap dikenali sebagai miliknya sendiri.
    [RealtimeProperty(5, true)] private string _senderAccount;
}
