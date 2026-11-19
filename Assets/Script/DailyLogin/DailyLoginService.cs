using System;

/// <summary>
/// HADIAH LOGIN HARIAN untuk akun yang sedang login. Aturannya di DailyLoginRules, datanya di
/// PlayerProgress.dailyLogin (Cloud Save), gold masuk lewat PlayerCoins.
/// Dipakai DailyLoginPopup setiap pemain masuk room.
/// </summary>
public static class DailyLoginService {
    private static DailyLoginProgress Data => ProgressService.Current.dailyLogin ??= new DailyLoginProgress();

    /// <summary>Hasil RegisterToday terakhir (untuk banner "tabungan hangus").</summary>
    public static DailyLoginCheck LastCheck { get; private set; }

    public static DailyLoginProgress Progress => Data;

    /// <summary>Dipicu saat tabungan berubah (login hari baru tercatat / klaim). Dipakai badge tombol Hadiah.</summary>
    public static event Action Changed;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Changed = null;
        LastCheck = default;
    }

    /// <summary>Catat login hari ini (tanggal HP) dan simpan kalau ada perubahan.</summary>
    public static DailyLoginCheck RegisterToday() {
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(Data, DateTime.Now);
        LastCheck = check;
        if (check.Changed) {
            ProgressService.MarkDirty();
            _ = ProgressService.SaveAsync();
            Changed?.Invoke();
        }
        return check;
    }

    public static int SavedGold => DailyLoginRules.SavedGold(Data);

    /// <summary>Klaim semua tabungan: tambah gold ke saldo lalu simpan sekali. Mengembalikan gold yang didapat.</summary>
    public static int ClaimAll() {
        int gold = DailyLoginRules.ClaimAll(Data);
        if (gold <= 0) return 0;
        PlayerCoins.Add(gold, saveNow: false);
        ProgressService.MarkDirty();
        _ = ProgressService.SaveAsync();
        Changed?.Invoke();
        return gold;
    }
}
