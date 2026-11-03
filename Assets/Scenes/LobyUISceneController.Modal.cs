using Normal.Realtime;
using UnityEngine;

/// <summary>
/// Modal pemblokir input selama proses koneksi (cari / buat / gabung / masuk room) dan alert hasilnya.
/// Prefab LobbyModalCanvas dipasang di scene dan disambungkan ke field "modal" oleh LobbyModalBuilder.
/// Tanpa modal (mis. di LobyUIScene) semuanya tetap jalan: hanya teks status yang berubah, dan _busy
/// tetap mencegah koneksi ganda.
/// </summary>
public sealed partial class LobyUISceneController {
    private const string NoRoomAlertTitle = "Belum ada room";
    private const string CreateRoomFailedTitle = "Gagal membuat room";
    private const string ConnectCancelledMessage = "Dibatalkan.";
    private const string RoomNotFoundTitle = "Room tidak ditemukan";
    private const string RoomExpiredMessage = "Room sudah kedaluwarsa dan dihapus dari daftar.";
    private const string ConnectionLostTitle = "Koneksi terputus";
    private const string ConnectionLostMessage =
        "Koneksi ke server terputus. Masuk lagi lewat ROOM TERAKHIR atau kode room.";
    private const string ConnectionLostStatus = "Koneksi ke room terputus.";
    private const string JoinFailedTitle = "Gagal masuk room";
    private const string JoinFailedFallback = "Tidak bisa terhubung ke server. Coba lagi.";

    [SerializeField] private LobbyModal modal;

    // BATAL ditekan: abaikan semua event koneksi dari percobaan itu sampai Connect() berikutnya.
    private bool _connectCancelled;

    private void ResolveModal() {
        if (modal == null)
            modal = FindAnyObjectByType<LobbyModal>(FindObjectsInactive.Include);
    }

    private void ShowConnectingModal(string message) {
        if (modal != null)
            modal.ShowLoading(message, CancelConnect);
    }

    /// <summary>Tutup modal loading saja. Alert dibiarkan sampai pemain menekan OK.</summary>
    private void HideModal() {
        if (modal != null)
            modal.HideLoading();
    }

    private void ShowAlertModal(string title, string message) {
        if (modal != null)
            modal.ShowAlert(title, message);
    }

    private void CancelConnect() {
        if (!_busy)
            return;
        _connectCancelled = true;
        StopQuickmatchRoutines();
        _quickmatchIntent = QuickmatchIntent.None;
        _quickmatchDisconnecting = false;
        _afterQuickmatchDisconnect = null;
        _busy = false;
        HideModal();
        SetStatus(ConnectCancelledMessage);
        if (realtime != null)
            realtime.Disconnect();
    }

    /// <summary>True = event koneksi ini milik percobaan yang sudah dibatalkan, jadi diabaikan.</summary>
    private bool IgnoreCancelledConnect(Realtime connectedRealtime) {
        if (!_connectCancelled)
            return false;
        // Koneksi sempat berhasil setelah BATAL: langsung putuskan lagi.
        if (connectedRealtime != null && connectedRealtime.connected)
            connectedRealtime.Disconnect();
        return true;
    }
}
