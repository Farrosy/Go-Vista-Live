using Normal.Realtime;

/// Data animasi avatar yang dikirim ke pemain lain. Kode modelnya dibuat otomatis oleh source
/// generator Normcore (karena partial + [RealtimeModel]), tidak perlu klik "Compile Model".
[RealtimeModel]
public partial class PlayerAnimSyncModel {
    // Unreliable: nilai terbaru saja yang penting, paket yang hilang tidak perlu dikirim ulang.
    [RealtimeProperty(1, false, false)] private float _speed;
    [RealtimeProperty(2, true, false)] private bool _grounded = true;
    // Naik 1 tiap lompat. Pemain lain memicu animasi Jump saat nilainya berubah.
    [RealtimeProperty(3, true, true)] private int _jumpCount;
    // Naik 1 tiap tertabrak mobil. Pemain lain memicu animasi Hit Knockback saat nilainya berubah.
    [RealtimeProperty(4, true, true)] private int _knockdownCount;
    // True selama rebahan karena tertabrak sampai mati (tidak bangkit, masuk ke tanah) sampai pemain bangkit lagi.
    [RealtimeProperty(5, true, true)] private bool _knockedDownDead;
}
