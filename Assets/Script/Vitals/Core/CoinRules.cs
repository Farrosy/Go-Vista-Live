using System;
using System.Globalization;

/// Aturan koin G. Koin belum punya sumber: saldo selalu mulai dari StartingBalance tiap masuk room.
public static class CoinRules {
    public const int StartingBalance = 100;
    public const int DeathPenaltyAmount = 50;

    /// Jumlah yang benar-benar terpotong saat mati. Saldo tidak pernah minus.
    public static int DeathPenalty(int balance) => Math.Min(Math.Max(0, balance), DeathPenaltyAmount);

    /// Format Indonesia (12.450) tanpa bergantung pada data culture perangkat.
    public static string FormatBalance(int balance) =>
        balance.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
}
