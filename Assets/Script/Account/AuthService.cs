using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Hasil operasi akun/progres. UI cukup cek Success lalu tampilkan Message.
/// </summary>
public readonly struct ServiceResult {
    public readonly bool Success;
    public readonly string Message;

    private ServiceResult(bool success, string message) {
        Success = success;
        Message = message;
    }

    public static ServiceResult Ok() => new ServiceResult(true, "");
    public static ServiceResult Fail(string message) => new ServiceResult(false, message);
}

/// <summary>
/// Pembungkus Unity Authentication (UGS): inisialisasi, register, login, auto-login, dan logout.
/// Tidak berisi kode UI sama sekali, jadi bisa dipanggil dari scene mana pun.
/// </summary>
public static class AuthService {
    public const string UsernameRules = "Username 3–20 karakter: huruf, angka, atau . - @ _";
    public const string PasswordRules =
        "Password 8–30 karakter, wajib ada huruf besar, huruf kecil, angka, dan simbol.";

    private const string LastUsernameKey = "Auth.LastUsername";
    private static readonly Regex UsernamePattern = new Regex(@"^[A-Za-z0-9.\-@_]{3,20}$");

    private static Task _initTask;

    public static bool IsSignedIn =>
        UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn;

    /// <summary>ID unik pemain dari UGS. Pakai ini sebagai kunci data, bukan username.</summary>
    public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : "";

    /// <summary>Username terakhir yang berhasil login di perangkat ini (untuk ditampilkan).</summary>
    public static string LastUsername => PlayerPrefs.GetString(LastUsernameKey, "");

    // Reset saat Play Mode dimulai, supaya tetap benar walau Domain Reload dimatikan.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        _initTask = null;
    }

    /// <summary>
    /// Login otomatis memakai sesi yang tersimpan dari login sebelumnya.
    /// Mengembalikan false kalau belum pernah login atau sesinya sudah tidak berlaku.
    /// </summary>
    public static async Task<bool> TryRestoreSessionAsync() {
        if (!(await EnsureInitializedAsync()).Success)
            return false;

        IAuthenticationService auth = AuthenticationService.Instance;
        if (auth.IsSignedIn)
            return true;
        if (!auth.SessionTokenExists)
            return false;

        try {
            // Dengan session token yang ada, method ini memulihkan akun yang terakhir login
            // (bukan membuat akun tamu baru).
            await auth.SignInAnonymouslyAsync();
            return true;
        } catch (Exception e) {
            Debug.LogWarning($"[Auth] Sesi lama tidak bisa dipulihkan: {e.Message}");
            auth.ClearSessionToken();
            return false;
        }
    }

    public static Task<ServiceResult> RegisterAsync(string username, string password) {
        username = (username ?? "").Trim();
        string error = ValidateUsername(username) ?? ValidatePassword(password);
        if (error != null)
            return Task.FromResult(ServiceResult.Fail(error));

        return RunSignInAsync(
            () => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password),
            username, isRegister: true);
    }

    public static Task<ServiceResult> SignInAsync(string username, string password) {
        username = (username ?? "").Trim();
        if (username.Length == 0 || string.IsNullOrEmpty(password))
            return Task.FromResult(ServiceResult.Fail("Username dan password harus diisi."));

        return RunSignInAsync(
            () => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password),
            username, isRegister: false);
    }

    /// <summary>Keluar dan hapus sesi tersimpan, jadi tidak auto-login lagi.</summary>
    public static void SignOut() {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            return;
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(clearCredentials: true);
    }

    public static string ValidateUsername(string username) {
        return UsernamePattern.IsMatch(username ?? "") ? null : UsernameRules;
    }

    public static string ValidatePassword(string password) {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 30)
            return PasswordRules;

        bool upper = false, lower = false, digit = false, symbol = false;
        foreach (char c in password) {
            if (char.IsUpper(c)) upper = true;
            else if (char.IsLower(c)) lower = true;
            else if (char.IsDigit(c)) digit = true;
            else symbol = true;
        }
        return upper && lower && digit && symbol ? null : PasswordRules;
    }

    private static async Task<ServiceResult> EnsureInitializedAsync() {
        if (UnityServices.State == ServicesInitializationState.Initialized)
            return ServiceResult.Ok();

        try {
            _initTask ??= UnityServices.InitializeAsync();
            await _initTask;
            return ServiceResult.Ok();
        } catch (Exception e) {
            _initTask = null; // izinkan coba lagi
            Debug.LogException(e);
            return ServiceResult.Fail("Gagal terhubung ke Unity Services. Cek internet, dan pastikan project " +
                                   "sudah di-link di Project Settings > Services.");
        }
    }

    private static async Task<ServiceResult> RunSignInAsync(Func<Task> signIn, string username, bool isRegister) {
        ServiceResult init = await EnsureInitializedAsync();
        if (!init.Success)
            return init;

        // Pastikan tidak ada sesi lain yang masih aktif sebelum ganti akun.
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(clearCredentials: true);

        try {
            await signIn();
            PlayerPrefs.SetString(LastUsernameKey, username);
            PlayerPrefs.Save();
            return ServiceResult.Ok();
        } catch (RequestFailedException e) {
            Debug.LogWarning($"[Auth] {(isRegister ? "Register" : "Login")} gagal ({e.ErrorCode}): {e.Message}");
            return ServiceResult.Fail(ToFriendlyMessage(e, isRegister));
        } catch (Exception e) {
            Debug.LogException(e);
            return ServiceResult.Fail("Terjadi kesalahan tak terduga. Coba lagi.");
        }
    }

    private static string ToFriendlyMessage(RequestFailedException e, bool isRegister) {
        if (e.ErrorCode == CommonErrorCodes.TransportError || e.ErrorCode == CommonErrorCodes.Timeout ||
            e.ErrorCode == CommonErrorCodes.ServiceUnavailable)
            return "Tidak bisa terhubung ke server. Cek koneksi internet kamu.";
        if (e.ErrorCode == CommonErrorCodes.TooManyRequests)
            return "Terlalu banyak percobaan. Tunggu sebentar lalu coba lagi.";

        string message = e.Message ?? "";
        if (message.IndexOf("banned", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Akun ini diblokir.";
        if (isRegister && message.IndexOf("exist", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Username sudah dipakai. Coba username lain.";

        // Format username/password sudah dicek lokal, jadi kegagalan login hampir selalu
        // berarti kombinasi username/password salah.
        return isRegister ? "Pendaftaran gagal. Coba lagi beberapa saat." : "Username atau password salah.";
    }
}
