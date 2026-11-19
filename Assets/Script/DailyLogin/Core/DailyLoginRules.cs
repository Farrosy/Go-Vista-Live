using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Data HADIAH LOGIN HARIAN satu akun (disimpan di PlayerProgress.dailyLogin, Cloud Save).
/// Selalu ubah lewat DailyLoginRules supaya datanya tetap valid.
/// </summary>
[Serializable]
public class DailyLoginProgress {
    /// <summary>Tanggal Hari 1 siklus ini (yyyy-MM-dd, tanggal HP). Kosong = belum pernah login.</summary>
    public string cycleStart = "";
    /// <summary>Hari ke- (1..7) di siklus ini saat pemain login (masuk room). Hadiahnya masuk tabungan.</summary>
    public List<int> loggedDays = new List<int>();
    /// <summary>Hari ke- (1..7) yang hadiahnya sudah diklaim. Selalu bagian dari loggedDays.</summary>
    public List<int> claimedDays = new List<int>();
}

/// <summary>Keadaan satu hari di popup.</summary>
public enum DailyDayState {
    /// <summary>Belum sampai harinya.</summary>
    Future,
    /// <summary>Harinya sudah lewat tapi pemain tidak login: hadiahnya tidak masuk tabungan.</summary>
    Missed,
    /// <summary>Pemain login di hari itu, hadiahnya ada di tabungan dan bisa diklaim.</summary>
    Saved,
    /// <summary>Hadiahnya sudah diklaim.</summary>
    Claimed,
}

/// <summary>Hasil mencatat login hari ini.</summary>
public readonly struct DailyLoginCheck {
    /// <summary>Hari ke- (1..7) hari ini di siklus.</summary>
    public readonly int Today;
    /// <summary>True kalau data berubah (perlu disimpan).</summary>
    public readonly bool Changed;
    /// <summary>True kalau siklus lama sudah lewat 7 hari dan siklus baru dimulai hari ini.</summary>
    public readonly bool CycleReset;
    /// <summary>Gold tabungan siklus lama yang hangus karena belum diklaim.</summary>
    public readonly int LostGold;

    public DailyLoginCheck(int today, bool changed, bool cycleReset, int lostGold) {
        Today = today;
        Changed = changed;
        CycleReset = cycleReset;
        LostGold = lostGold;
    }
}

/// <summary>
/// Aturan HADIAH LOGIN HARIAN ("tabung daily login"):
///  - Siklus 7 hari, Hari 1 = hari pertama login. Hari dihitung dari tanggal HP.
///  - Tiap hari pemain login, hadiah hari itu masuk tabungan. Hari yang tidak login: hadiahnya hangus.
///  - Semua tabungan diklaim sekaligus dengan satu tombol.
///  - Di Hari 8 siklus mulai lagi dari Hari 1, dan tabungan yang belum diklaim hangus.
///  - Untuk sekarang hadiahnya gold saja (item menyusul).
/// ⚠ Tanggal HP bisa diubah pemain (sama seperti koin yang disimpan dari client, lihat PlayerProgress).
/// </summary>
public static class DailyLoginRules {
    public const int CycleDays = 7;
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly int[] GoldRewards = { 50, 75, 100, 125, 150, 200, 500 };

    /// <summary>Gold hadiah hari ke- (1..7). Di luar itu 0.</summary>
    public static int Reward(int day) => day >= 1 && day <= CycleDays ? GoldRewards[day - 1] : 0;

    public static string FormatDate(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Hari ke- tanggal ini di siklus. 0 kalau belum ada siklus; bisa &lt; 1 atau &gt; 7 kalau di luar siklus.</summary>
    public static int DayIndex(DailyLoginProgress progress, DateTime today) {
        if (progress == null || !DateTime.TryParseExact(progress.cycleStart, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime start))
            return 0;
        return (today.Date - start.Date).Days + 1;
    }

    /// <summary>
    /// Catat login hari ini: mulai siklus baru kalau perlu, lalu masukkan hadiah hari ini ke tabungan.
    /// Jam HP mundur ke sebelum Hari 1 juga dianggap siklus baru.
    /// </summary>
    public static DailyLoginCheck RegisterLogin(DailyLoginProgress progress, DateTime today) {
        bool changed = Sanitize(progress);
        int day = DayIndex(progress, today);
        bool reset = false;
        int lost = 0;
        if (day < 1 || day > CycleDays) {
            reset = !string.IsNullOrEmpty(progress.cycleStart);
            lost = SavedGold(progress);
            progress.cycleStart = FormatDate(today);
            progress.loggedDays.Clear();
            progress.claimedDays.Clear();
            day = 1;
            changed = true;
        }
        if (!progress.loggedDays.Contains(day)) {
            progress.loggedDays.Add(day);
            changed = true;
        }
        return new DailyLoginCheck(day, changed, reset, lost);
    }

    public static DailyDayState StateOf(DailyLoginProgress progress, int day, int today) {
        if (progress.claimedDays.Contains(day)) return DailyDayState.Claimed;
        if (progress.loggedDays.Contains(day)) return DailyDayState.Saved;
        return day < today ? DailyDayState.Missed : DailyDayState.Future;
    }

    /// <summary>Hari yang hadiahnya ada di tabungan (login tapi belum diklaim), urut naik.</summary>
    public static List<int> SavedDays(DailyLoginProgress progress) {
        var days = new List<int>();
        foreach (int day in progress.loggedDays)
            if (!progress.claimedDays.Contains(day)) days.Add(day);
        days.Sort();
        return days;
    }

    /// <summary>Total gold di tabungan.</summary>
    public static int SavedGold(DailyLoginProgress progress) {
        int total = 0;
        foreach (int day in SavedDays(progress)) total += Reward(day);
        return total;
    }

    /// <summary>Klaim semua tabungan. Mengembalikan total gold yang harus ditambahkan ke saldo.</summary>
    public static int ClaimAll(DailyLoginProgress progress) {
        Sanitize(progress);
        int total = 0;
        foreach (int day in SavedDays(progress)) {
            progress.claimedDays.Add(day);
            total += Reward(day);
        }
        return total;
    }

    /// <summary>Rapikan data dari save (null, hari di luar 1..7, dobel, klaim tanpa login). True = ada yang diubah.</summary>
    public static bool Sanitize(DailyLoginProgress progress) {
        bool changed = false;
        if (progress.cycleStart == null) { progress.cycleStart = ""; changed = true; }
        if (progress.loggedDays == null) { progress.loggedDays = new List<int>(); changed = true; }
        if (progress.claimedDays == null) { progress.claimedDays = new List<int>(); changed = true; }
        changed |= CleanDays(progress.loggedDays, null);
        changed |= CleanDays(progress.claimedDays, progress.loggedDays);
        return changed;
    }

    private static bool CleanDays(List<int> days, List<int> allowed) {
        var seen = new HashSet<int>();
        int removed = days.RemoveAll(d => d < 1 || d > CycleDays || !seen.Add(d) || (allowed != null && !allowed.Contains(d)));
        return removed > 0;
    }
}
