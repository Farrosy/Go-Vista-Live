using Normal.Realtime;

/// Data "venue" avatar yang dikirim ke pemain lain (satu Normcore room, visibilitas per-venue).
/// Kode modelnya dibuat otomatis oleh source generator Normcore (partial + [RealtimeModel]),
/// tidak perlu klik "Compile Model" — sama seperti PlayerAnimSyncModel.
[RealtimeModel]
public partial class PlayerVenueModel
{
    // Reliable + change-callback: perpindahan venue penting dan tidak boleh hilang, dan
    // VenueVisibilityFilter perlu tahu persis kapan nilainya berubah. 0 = VenueId.World (default).
    [RealtimeProperty(1, true, true)] private int _venue;
}
