using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// Hasil simpan nama di menu PROFIL.
public readonly struct ProfileNameResult {
    public readonly bool Success;
    public readonly string Message;
    /// Nama lengkap baru di friendlist (mis. "budi#11872"); kosong kalau gagal.
    public readonly string FriendName;
    /// True kalau Unity memberi tag angka baru (perlu diberi tahu ke pemain).
    public readonly bool TagChanged;

    public ProfileNameResult(bool success, string message, string friendName, bool tagChanged) {
        Success = success;
        Message = message;
        FriendName = friendName;
        TagChanged = tagChanged;
    }

    public static ProfileNameResult Fail(string message) => new ProfileNameResult(false, message, "", false);
}

/// Data & aksi menu PROFIL (Docs/Profil/*.png):
///  - Akun guest otomatis bernama "Tamu_XXXX" (4 angka tetap per akun, dari PlayerId) dan tidak bisa mengganti nama.
///  - Akun terdaftar mengganti nama lewat SaveNameAsync: Player Name Authentication diperbarui dulu (server memberi
///    tag angka), baru nama disimpan ke progres. Kalau server gagal, progres tidak diubah.
public static class ProfileService {
    private const string GuestPrefix = "Tamu_";
    private const int GuestDigits = 10000;

    /// Dipicu saat nama / akun berubah.
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Changed = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        LoginUIController.AccountChanged -= OnAccountChanged;
        LoginUIController.AccountChanged += OnAccountChanged;
        ProgressService.PlayerNameChanged -= OnPlayerNameChanged;
        ProgressService.PlayerNameChanged += OnPlayerNameChanged;
    }

    public static bool IsSignedIn => AuthService.IsSignedIn;
    public static bool IsGuest => AuthService.IsSignedIn && AuthService.IsGuest;

    /// Nama pemain tanpa tag (yang tampil di game).
    public static string PlayerName => ProgressService.Current.playerName ?? "";

    /// Username untuk masuk (hanya ditampilkan ke pemain itu sendiri).
    public static string LoginUsername => AuthService.LastUsername;

    /// Nama lengkap di friendlist "Nama#tag" (akun terdaftar), atau kosong.
    public static string FriendName {
        get {
            if (!IsSignedIn || IsGuest) return "";
            string fromAuth = AuthenticationService.Instance.PlayerName;
            return !string.IsNullOrEmpty(fromAuth) ? fromAuth : FriendsManager.LocalFriendName;
        }
    }

    /// Tag dari nama friendlist ("budi#11872" → "#11872"), atau kosong.
    public static string TagOf(string friendName) {
        if (string.IsNullOrEmpty(friendName)) return "";
        int hash = friendName.LastIndexOf('#');
        return hash >= 0 ? friendName.Substring(hash) : "";
    }

    /// Nama tamu tetap untuk PlayerId ini, mis. "Tamu_4821".
    public static string GuestNameFor(string playerId) {
        int number = ChatRules.StableHash("govista-guest:" + (playerId ?? "")) % GuestDigits;
        return GuestPrefix + number.ToString("D4");
    }

    private static void OnAccountChanged() {
        // Guest tidak bisa memilih nama: pastikan namanya Tamu_XXXX (juga mengganti nama custom lama).
        if (IsGuest) {
            string guestName = GuestNameFor(AuthService.PlayerId);
            if (ProgressService.IsLoaded && PlayerName != guestName)
                ProgressService.SetPlayerName(guestName);
        }
        Notify();
    }

    private static void OnPlayerNameChanged(string _) => Notify();

    /// Simpan nama baru (akun terdaftar). Tidak melempar exception; pesan error siap ditampilkan.
    public static async Task<ProfileNameResult> SaveNameAsync(string rawName) {
        if (!IsSignedIn || IsGuest)
            return ProfileNameResult.Fail("Nama tamu tidak bisa diubah.");
        string name = PlayerNameRules.Sanitize(rawName);
        if (!PlayerNameRules.IsValid(name))
            return ProfileNameResult.Fail(PlayerNameRules.Hint + ".");

        string oldTag = TagOf(FriendName);
        string friendName;
        try {
            friendName = await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
        } catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.InvalidParameters) {
            Debug.LogWarning("[Profile] Nama ditolak server: " + e.Message);
            return ProfileNameResult.Fail("Nama ini tidak bisa dipakai. Coba nama lain.");
        } catch (RequestFailedException e) {
            Debug.LogWarning("[Profile] Gagal menyimpan nama: " + e.Message);
            return ProfileNameResult.Fail("Gagal menyimpan nama. Periksa koneksi, lalu coba lagi.");
        } catch (Exception e) {
            Debug.LogException(e);
            return ProfileNameResult.Fail("Gagal menyimpan nama. Coba lagi.");
        }

        FriendsManager.SetLocalFriendName(friendName);
        ProgressService.SetPlayerName(name);
        bool saved = await ProgressService.SaveAsync();
        if (!saved)
            Debug.LogWarning("[Profile] Nama tersimpan di server, progres akan disimpan ulang otomatis.");
        string newTag = TagOf(friendName);
        return new ProfileNameResult(true, "Nama disimpan.", friendName, oldTag.Length > 0 && newTag != oldTag);
    }

    private static void Notify() {
        try {
            Changed?.Invoke();
        } catch (Exception e) {
            Debug.LogException(e);
        }
    }
}
