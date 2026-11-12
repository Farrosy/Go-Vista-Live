using System;
using System.Globalization;

/// Aturan koin G (Go Vista Coin). Saldo disimpan permanen di Cloud Save (lihat PlayerCoins).
public static class CoinRules {
    /// Saldo akun baru, dan saldo awal pemain lama yang save-nya belum punya koin.
    public const int StartingBalance = 100;
    public const int DeathPenaltyAmount = 20;

    /// Jumlah yang benar-benar terpotong saat mati. Saldo tidak pernah minus.
    public static int DeathPenalty(int balance) => Math.Min(Math.Max(0, balance), DeathPenaltyAmount);

    /// Saldo setelah menerima hadiah. Jumlah negatif diabaikan, dan saldo tidak pernah overflow.
    public static int Add(int balance, int amount) {
        if (amount <= 0) return Sanitize(balance);
        long sum = (long)Sanitize(balance) + amount;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    /// Rapikan saldo dari save (mis. diedit atau rusak) supaya tidak pernah minus.
    public static int Sanitize(int balance) => Math.Max(0, balance);

    /// Format Indonesia (12.450) tanpa bergantung pada data culture perangkat.
    public static string FormatBalance(int balance) =>
        balance.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
}
