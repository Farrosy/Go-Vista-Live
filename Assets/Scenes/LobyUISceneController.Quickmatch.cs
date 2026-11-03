using System.Collections;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

/// <summary>
/// CARI ROOM CEPAT dan BUAT ROOM (publik), keduanya di atas Quickmatch Normcore.
///
/// Normcore tidak punya API "cari saja": ConnectToNextAvailableQuickmatchRoom selalu membuat room baru
/// kalau tidak ada yang tersisa. Jadi setelah tersambung kita cek siapa saja yang ada di room:
///  - CARI ROOM CEPAT: kalau hanya kita sendiri (room baru), langsung keluar dan beri tahu pemain.
///  - BUAT ROOM: kalau ternyata masuk room orang lain, keluar dan ulangi dengan mengecualikan kode room itu,
///    sampai dapat room yang benar-benar baru.
/// Room privat (nama room + MASUK) tidak lewat sini dan tidak pernah muncul di CARI ROOM CEPAT.
/// </summary>
public sealed partial class LobyUISceneController {
    private const string QuickmatchRoomGroup = "go-vista";
    private const int MaxCreateRoomAttempts = 5;
    // Presence pemain lain biasanya sudah ada saat didConnectToRoom; jeda kecil ini hanya untuk jaga-jaga.
    private const float OtherPlayersGraceSeconds = 0.75f;
    private const string NoRoomAvailableMessage = "Belum ada room tersedia. Buat room sendiri atau coba lagi.";
    private const string CreateRoomFailedMessage = "Gagal membuat room baru. Coba lagi.";
    private const string ReadyStatusMessage = "Siap. Cari room cepat atau buat room.";

    private enum QuickmatchIntent { None, Search, CreatePublic }

    private QuickmatchIntent _quickmatchIntent;
    private readonly List<string> _excludedRoomCodes = new List<string>();
    private int _createRoomAttempts;
    private bool _quickmatchDisconnecting;
    private System.Action _afterQuickmatchDisconnect;
    private Coroutine _quickmatchVerifyRoutine;
    private Coroutine _quickmatchRetryRoutine;

    private void SearchQuickRoom() {
        if (_busy)
            return;
        _quickmatchIntent = QuickmatchIntent.Search;
        Connect(() => realtime.ConnectToNextAvailableQuickmatchRoom(QuickmatchRoomGroup, MaxRoomPlayers),
                "Mencari room…");
    }

    private void CreatePublicRoom() {
        if (_busy)
            return;
        _quickmatchIntent = QuickmatchIntent.CreatePublic;
        _excludedRoomCodes.Clear();
        _createRoomAttempts = 0;
        ConnectNewPublicRoom();
    }

    private void ConnectNewPublicRoom() {
        _createRoomAttempts++;
        var options = new Room.QuickmatchOptions { excludeRoomCodes = _excludedRoomCodes.ToArray() };
        Connect(() => realtime.ConnectToNextAvailableQuickmatchRoom(QuickmatchRoomGroup, MaxRoomPlayers,
                                                                    quickmatchOptions: options),
                "Membuat room…");
    }

    /// <summary>Dipanggil paling awal di DidConnectToRoom. True = koneksi ini masih dicek, tahan UI dulu.</summary>
    private bool InterceptQuickmatchConnect(Realtime connectedRealtime) {
        if (_quickmatchIntent == QuickmatchIntent.None)
            return false;
        StopQuickmatchRoutines();
        _quickmatchVerifyRoutine = StartCoroutine(VerifyQuickmatchRoom(connectedRealtime));
        return true;
    }

    private IEnumerator VerifyQuickmatchRoom(Realtime connectedRealtime) {
        // Pembuat room langsung terdaftar, supaya pencari yang masuk bersamaan melihat room ini berpenghuni.
        if (_quickmatchIntent == QuickmatchIntent.CreatePublic)
            SpawnPresence(connectedRealtime);

        float deadline = Time.unscaledTime + OtherPlayersGraceSeconds;
        bool hasOthers = HasOtherPlayers(connectedRealtime);
        while (!hasOthers && connectedRealtime.connected && Time.unscaledTime < deadline) {
            yield return null;
            hasOthers = HasOtherPlayers(connectedRealtime);
        }
        _quickmatchVerifyRoutine = null;
        if (!connectedRealtime.connected)
            yield break; // Handler disconnect yang membereskan state.

        if (_quickmatchIntent == QuickmatchIntent.Search)
            ResolveSearch(connectedRealtime, hasOthers);
        else
            ResolveCreate(connectedRealtime, hasOthers);
    }

    private void ResolveSearch(Realtime connectedRealtime, bool hasOthers) {
        if (hasOthers)
            AcceptQuickmatchRoom(connectedRealtime);
        else
            LeaveQuickmatchRoom(connectedRealtime, () => FailQuickmatch(NoRoomAlertTitle, NoRoomAvailableMessage));
    }

    private void ResolveCreate(Realtime connectedRealtime, bool hasOthers) {
        if (!hasOthers) {
            AcceptQuickmatchRoom(connectedRealtime);
            return;
        }
        string joinedCode = connectedRealtime.room != null ? connectedRealtime.room.quickmatchRoomCode : null;
        if (!string.IsNullOrEmpty(joinedCode))
            _excludedRoomCodes.Add(joinedCode);
        if (_createRoomAttempts >= MaxCreateRoomAttempts)
            LeaveQuickmatchRoom(connectedRealtime, () => FailQuickmatch(CreateRoomFailedTitle, CreateRoomFailedMessage));
        else
            LeaveQuickmatchRoom(connectedRealtime, ConnectNewPublicRoom);
    }

    private void AcceptQuickmatchRoom(Realtime connectedRealtime) {
        _quickmatchIntent = QuickmatchIntent.None;
        DidConnectToRoom(connectedRealtime);
    }

    private void FailQuickmatch(string title, string message) {
        _quickmatchIntent = QuickmatchIntent.None;
        SetStatus(message, StatusKind.Error);
        HideModal();
        ShowAlertModal(title, message);
    }

    private void LeaveQuickmatchRoom(Realtime connectedRealtime, System.Action afterDisconnect) {
        _quickmatchDisconnecting = true;
        _afterQuickmatchDisconnect = afterDisconnect;
        connectedRealtime.Disconnect();
    }

    /// <summary>
    /// Dipanggil paling awal di kedua handler disconnect. True = disconnect ini sengaja (bagian dari cek
    /// Quickmatch), jadi alur "terputus dari room" yang biasa dilewati.
    /// </summary>
    private bool HandleQuickmatchDisconnect() {
        if (!_quickmatchDisconnecting) {
            // Gagal konek / terputus di tengah pengecekan: kembali ke alur normal.
            _quickmatchIntent = QuickmatchIntent.None;
            return false;
        }
        if (_afterQuickmatchDisconnect != null) {
            System.Action next = _afterQuickmatchDisconnect;
            _afterQuickmatchDisconnect = null;
            // Tunggu satu frame supaya kedua event disconnect Normcore selesai sebelum konek lagi.
            _quickmatchRetryRoutine = StartCoroutine(RunAfterQuickmatchDisconnect(next));
        }
        return true;
    }

    private IEnumerator RunAfterQuickmatchDisconnect(System.Action next) {
        yield return null;
        _quickmatchRetryRoutine = null;
        _quickmatchDisconnecting = false;
        _busy = false;
        next();
    }

    private void StopQuickmatchRoutines() {
        if (_quickmatchVerifyRoutine != null)
            StopCoroutine(_quickmatchVerifyRoutine);
        if (_quickmatchRetryRoutine != null)
            StopCoroutine(_quickmatchRetryRoutine);
        _quickmatchVerifyRoutine = null;
        _quickmatchRetryRoutine = null;
    }

    private static bool HasOtherPlayers(Realtime connectedRealtime) {
        foreach (LobbyPresence presence in LobbyPresence.All) {
            if (presence != null && presence.ClientID >= 0 && presence.ClientID != connectedRealtime.clientID)
                return true;
        }
        return false;
    }
}
