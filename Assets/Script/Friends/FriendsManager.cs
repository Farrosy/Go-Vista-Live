using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using UnityEngine;

public enum FriendsState { Unavailable, Initializing, Ready, Failed }
public enum FriendRelation { None, Friend, Outgoing, Incoming, Blocked, Self }

/// Hasil aksi friendlist untuk ditampilkan ke pemain.
public readonly struct FriendResult {
    public readonly bool Success;
    public readonly string Message;
    public FriendResult(bool success, string message) { Success = success; Message = message; }
    public static FriendResult Ok(string message) => new FriendResult(true, message);
    public static FriendResult Fail(string message) => new FriendResult(false, message);
}

/// Pembungkus Unity Friends (com.unity.services.friends) untuk Go Vista:
///  - Hanya untuk akun TERDAFTAR (guest = Unavailable). Otomatis aktif setelah login, dibersihkan SDK saat logout.
///  - Player Name Authentication disinkronkan dengan nama karakter (ProgressService.playerName). Unity menambah #1234.
///  - Presence: Online + room yang sedang dimasuki (FriendActivity), supaya teman bisa menekan GABUNG.
/// Semua aksi async mengembalikan FriendResult berisi pesan berbahasa Indonesia, tidak melempar exception.
public static class FriendsManager {
    public static FriendsState State { get; private set; } = FriendsState.Unavailable;
    /// Nama lengkap pemain lokal di friendlist, mis. "Budi#4821". Kosong sebelum sinkron.
    public static string LocalFriendName { get; private set; } = "";
    /// Dipicu saat daftar teman/permintaan/presence/state berubah. Selalu di main thread.
    public static event Action Changed;

    private static string _currentRoom = "";
    private static int _roomPlayers;
    private static bool _presenceDirty;
    private static bool _presenceLoopRunning;
    /// Presence dikirim paling sering sekali per interval ini (Unity Friends membatasi frekuensi request).
    private const int PresenceMinIntervalMs = 3000;
    private static bool _eventsHooked;
    private static Task _initTask;

    public static bool IsReady => State == FriendsState.Ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        State = FriendsState.Unavailable;
        LocalFriendName = "";
        Changed = null;
        _currentRoom = "";
        _roomPlayers = 0;
        _presenceDirty = false;
        _presenceLoopRunning = false;
        _eventsHooked = false;
        _initTask = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        LoginUIController.AccountChanged -= OnAccountChanged;
        LoginUIController.AccountChanged += OnAccountChanged;
        ProgressService.PlayerNameChanged -= OnPlayerNameChanged;
        ProgressService.PlayerNameChanged += OnPlayerNameChanged;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    // ---------------------------------------------------------------- Lifecycle

    private static void OnAccountChanged() {
        bool eligible = AuthService.IsSignedIn && !AuthService.IsGuest;
        if (!eligible) {
            // SDK membersihkan datanya sendiri saat token berganti (logout); cukup reset state kita.
            _initTask = null;
            LocalFriendName = "";
            SetState(FriendsState.Unavailable);
            return;
        }
        if (_initTask == null || State == FriendsState.Failed || State == FriendsState.Unavailable)
            _initTask = InitializeAsync();
    }

    private static async Task InitializeAsync() {
        SetState(FriendsState.Initializing);
        try {
            var options = new InitializeOptions().WithEvents(true).WithMemberPresence(true).WithMemberProfile(true);
            await FriendsService.Instance.InitializeAsync(options);
            HookEvents();
            SetState(FriendsState.Ready);
            await SyncPlayerNameAsync(ProgressService.Current.playerName);
            RequestPresencePublish();
        } catch (Exception e) {
            Debug.LogWarning("[Friends] Gagal inisialisasi: " + e.Message);
            SetState(FriendsState.Failed);
        }
    }

    /// Coba lagi setelah gagal (mis. jaringan putus). Dipanggil tombol "Coba lagi" di menu teman.
    public static void Retry() {
        _initTask = null;
        OnAccountChanged();
    }

    private static void HookEvents() {
        if (_eventsHooked)
            return;
        _eventsHooked = true;
        IFriendsService service = FriendsService.Instance;
        service.RelationshipAdded += _ => NotifyChanged();
        service.RelationshipDeleted += _ => NotifyChanged();
        service.PresenceUpdated += _ => NotifyChanged();
    }

    private static void OnQuitting() {
        if (IsReady)
            _ = TrySetAvailabilityAsync(Availability.Offline);
    }

    private static void SetState(FriendsState state) {
        State = state;
        NotifyChanged();
    }

    private static void NotifyChanged() {
        try {
            Changed?.Invoke();
        } catch (Exception e) {
            Debug.LogException(e);
        }
    }

    // ---------------------------------------------------------------- Nama & presence

    private static void OnPlayerNameChanged(string playerName) {
        if (IsReady)
            _ = SyncPlayerNameAsync(playerName);
    }

    /// Samakan Player Name Authentication dengan nama karakter. Nama kosong = biarkan nama otomatis dari Unity.
    private static async Task SyncPlayerNameAsync(string playerName) {
        try {
            string clean = PlayerNameRules.Sanitize(playerName);
            string current = AuthenticationService.Instance.PlayerName ?? "";
            if (clean.Length > 0 && BaseName(current) != clean)
                current = await AuthenticationService.Instance.UpdatePlayerNameAsync(clean);
            else if (current.Length == 0)
                current = await AuthenticationService.Instance.GetPlayerNameAsync();
            LocalFriendName = current ?? "";
            NotifyChanged();
        } catch (Exception e) {
            Debug.LogWarning("[Friends] Gagal sinkron nama: " + e.Message);
        }
    }

    /// Room yang sedang dimasuki pemain lokal (format sama dengan room terakhir lobby), atau null saat keluar.
    public static void SetCurrentRoom(string room) {
        string next = room ?? "";
        if (next == _currentRoom)
            return;
        _currentRoom = next;
        if (next.Length == 0)
            _roomPlayers = 0;
        RequestPresencePublish();
    }

    /// Jumlah pemain di room sekarang (dipanggil tiap detik oleh daftar pemain ruang tunggu; hanya dikirim kalau berubah).
    public static void SetRoomPlayerCount(int players) {
        if (players == _roomPlayers)
            return;
        _roomPlayers = players;
        RequestPresencePublish();
    }

    /// Tandai presence perlu dikirim. Perubahan beruntun digabung dan dikirim paling sering tiap PresenceMinIntervalMs.
    private static void RequestPresencePublish() {
        _presenceDirty = true;
        if (IsReady && !_presenceLoopRunning)
            _ = PresenceLoopAsync();
    }

    private static async Task PresenceLoopAsync() {
        _presenceLoopRunning = true;
        try {
            while (_presenceDirty && IsReady) {
                _presenceDirty = false;
                var activity = new FriendActivity(_currentRoom, _currentRoom.Length > 0 ? _roomPlayers : 0);
                await FriendsService.Instance.SetPresenceAsync(Availability.Online, activity);
                await Task.Delay(PresenceMinIntervalMs);
            }
        } catch (Exception e) {
            Debug.LogWarning("[Friends] Gagal memperbarui status: " + e.Message);
        } finally {
            _presenceLoopRunning = false;
        }
    }

    private static async Task TrySetAvailabilityAsync(Availability availability) {
        try {
            await FriendsService.Instance.SetPresenceAvailabilityAsync(availability);
        } catch (Exception e) {
            Debug.LogWarning("[Friends] Gagal set status: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- Daftar

    public static IReadOnlyList<Relationship> Friends => IsReady ? FriendsService.Instance.Friends : Empty;
    public static IReadOnlyList<Relationship> Incoming => IsReady ? FriendsService.Instance.IncomingFriendRequests : Empty;
    public static IReadOnlyList<Relationship> Outgoing => IsReady ? FriendsService.Instance.OutgoingFriendRequests : Empty;
    private static readonly IReadOnlyList<Relationship> Empty = new List<Relationship>();

    /// Jumlah teman yang sedang online (untuk tile TEMAN di menu utama dan header menu teman).
    public static int OnlineFriendCount {
        get {
            int count = 0;
            foreach (Relationship r in Friends) {
                if (FriendStatus.From(r.Member).Online)
                    count++;
            }
            return count;
        }
    }

    /// Hubungan pemain lokal dengan akun lain (untuk tombol di daftar pemain room).
    public static FriendRelation RelationTo(string accountId) {
        if (string.IsNullOrEmpty(accountId) || !IsReady)
            return FriendRelation.None;
        if (accountId == AuthService.PlayerId)
            return FriendRelation.Self;
        foreach (Relationship r in FriendsService.Instance.Relationships) {
            if (r.Member == null || r.Member.Id != accountId)
                continue;
            if (r.Type == RelationshipType.Block) return FriendRelation.Blocked;
            if (r.Type == RelationshipType.Friend) return FriendRelation.Friend;
            if (r.Type == RelationshipType.FriendRequest)
                return r.Member.Role == MemberRole.Source ? FriendRelation.Incoming : FriendRelation.Outgoing;
        }
        return FriendRelation.None;
    }

    // ---------------------------------------------------------------- Aksi

    /// Tambah teman dari nama lengkap "Nama#1234" (Unity Friends tidak punya pencarian sebagian).
    public static Task<FriendResult> AddByNameAsync(string rawName) {
        string name = (rawName ?? "").Trim();
        string error = FriendNameRules.Validate(name, LocalFriendName);
        if (error != null)
            return Task.FromResult(FriendResult.Fail(error));
        return RunAsync(() => FriendsService.Instance.AddFriendByNameAsync(name),
                        "Permintaan teman terkirim ke " + name + ".");
    }

    /// Tambah teman dari PlayerId (tombol di daftar pemain room). Kalau dia sudah mengirim permintaan, langsung berteman.
    public static Task<FriendResult> AddByIdAsync(string accountId, string displayName) {
        return RunAsync(() => FriendsService.Instance.AddFriendAsync(accountId),
                        "Permintaan teman terkirim ke " + displayName + ".");
    }

    public static Task<FriendResult> AcceptAsync(string memberId, string name) =>
        RunAsync(() => FriendsService.Instance.AddFriendAsync(memberId), "Kamu sekarang berteman dengan " + name + ".");

    public static Task<FriendResult> DeclineAsync(string memberId) =>
        RunAsync(() => FriendsService.Instance.DeleteIncomingFriendRequestAsync(memberId), "Permintaan ditolak.");

    public static Task<FriendResult> CancelAsync(string memberId) =>
        RunAsync(() => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(memberId), "Permintaan dibatalkan.");

    public static Task<FriendResult> RemoveAsync(string memberId, string name) =>
        RunAsync(() => FriendsService.Instance.DeleteFriendAsync(memberId), name + " dihapus dari teman.");

    private static async Task<FriendResult> RunAsync(Func<Task> action, string successMessage) {
        if (!IsReady)
            return FriendResult.Fail(UnavailableMessage());
        try {
            await action();
            NotifyChanged();
            return FriendResult.Ok(successMessage);
        } catch (FriendsServiceException e) {
            Debug.LogWarning($"[Friends] {e.ErrorCode} ({e.StatusCode}): {e.Message}");
            return FriendResult.Fail(FriendlyMessage(e));
        } catch (RequestFailedException e) {
            Debug.LogWarning("[Friends] " + e.Message);
            return FriendResult.Fail("Tidak bisa terhubung ke server. Cek koneksi internet lalu coba lagi.");
        } catch (Exception e) {
            Debug.LogException(e);
            return FriendResult.Fail("Terjadi kesalahan. Coba lagi.");
        }
    }

    public static string UnavailableMessage() {
        switch (State) {
            case FriendsState.Initializing: return "Memuat daftar teman...";
            case FriendsState.Failed: return "Gagal memuat daftar teman. Tekan COBA LAGI.";
            default:
                return AuthService.IsSignedIn && AuthService.IsGuest
                    ? "Fitur teman hanya untuk akun terdaftar. Keluar akun lalu DAFTAR untuk memakainya."
                    : "Masuk ke akun dulu untuk memakai fitur teman.";
        }
    }

    private static string FriendlyMessage(FriendsServiceException e) {
        switch (e.ErrorCode) {
            case FriendsErrorCode.UserTargetingSelf: return "Kamu tidak bisa menambahkan dirimu sendiri.";
            case FriendsErrorCode.FriendshipAlreadyExists: return "Kalian sudah berteman.";
            case FriendsErrorCode.RelationshipAlreadyExists: return "Permintaan teman sudah pernah dikirim.";
            case FriendsErrorCode.FriendLimitReached: return "Daftar temanmu sudah penuh.";
            case FriendsErrorCode.FriendRequestLimitReached: return "Terlalu banyak permintaan teman yang menunggu.";
            case FriendsErrorCode.TargetsFriendLimitReached: return "Daftar teman pemain itu sudah penuh.";
            case FriendsErrorCode.ActionUnauthorizedWhenBlocked: return "Tidak bisa, salah satu dari kalian memblokir yang lain.";
            case FriendsErrorCode.ProjectNotEnabled: return "Layanan teman belum aktif untuk game ini.";
        }
        if (e.StatusCode == HttpStatusCode.NotFound)
            return "Nama tidak ditemukan. Pastikan nama lengkap dengan tag benar, contoh Budi#1234.";
        if (e.StatusCode == (HttpStatusCode)429)
            return "Terlalu sering. Tunggu sebentar lalu coba lagi.";
        return "Gagal: " + e.Message;
    }

    /// "Budi#4821" -> "Budi".
    public static string BaseName(string friendName) {
        if (string.IsNullOrEmpty(friendName))
            return "";
        int hash = friendName.LastIndexOf('#');
        return hash > 0 ? friendName.Substring(0, hash) : friendName;
    }
}
