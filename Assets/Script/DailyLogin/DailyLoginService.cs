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

    /// <summary>Catat login hari ini (tanggal HP) dan simpan kalau ada perubahan.</summary>
    public static DailyLoginCheck RegisterToday() {
        DailyLoginCheck check = DailyLoginRules.RegisterLogin(Data, DateTime.Now);
        LastCheck = check;
        if (check.Changed) {
            ProgressService.MarkDirty();
            _ = ProgressService.SaveAsync();
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
        return gold;
    }
}
