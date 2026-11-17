using Normal.Realtime;

/// Tampilan karakter custom pemilik avatar (Tahap 3). Kode model dibuat otomatis oleh source generator
/// Normcore (partial + [RealtimeModel]), sama seperti PlayerAnimSyncModel.
[RealtimeModel]
public partial class PlayerAppearanceModel {
    // Reliable + change-callback: dikirim sekali saat spawn (customizer hanya di lobby, jadi tidak berubah di
    // dalam room). JSON CustomCharacterRecord, kosong = template. Pemain yang masuk belakangan langsung dapat nilainya.
    [RealtimeProperty(1, true, true)] private string _record;
}
