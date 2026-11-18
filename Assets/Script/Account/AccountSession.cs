using UnityEngine;

/// <summary>
/// Mengingat akun pertama yang masuk sejak game dibuka. Banyak sistem (teman, chat, voice, karakter, quest)
/// menyimpan data akun di memori, jadi masuk ke akun LAIN tanpa membuka ulang game bisa membawa sisa akun lama.
/// Seperti Mobile Legends: kalau akun yang masuk berbeda, game dimulai ulang (AccountRestartPrompt).
/// Masuk lagi ke akun yang sama tidak perlu restart.
/// </summary>
public static class AccountSession {
    private static string _sessionPlayerId = "";

    // Reset saat Play Mode dimulai, supaya tetap benar walau Domain Reload dimatikan.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _sessionPlayerId = "";

    /// <summary>true kalau akun lain sudah pernah masuk di proses game ini.</summary>
    public static bool IsDifferentAccount(string playerId) {
        return !string.IsNullOrEmpty(_sessionPlayerId) && !string.IsNullOrEmpty(playerId) && playerId != _sessionPlayerId;
    }

    /// <summary>Catat akun ini sebagai akun sesi (hanya akun pertama yang dicatat).</summary>
    public static void Remember(string playerId) {
        if (string.IsNullOrEmpty(_sessionPlayerId) && !string.IsNullOrEmpty(playerId)) _sessionPlayerId = playerId;
    }
}
