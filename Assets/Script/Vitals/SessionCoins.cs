using System;

/// Saldo koin selama berada di satu room. Sengaja tidak disimpan: masuk room lagi berarti kembali ke saldo awal.
public static class SessionCoins {
    public static int Balance { get; private set; } = CoinRules.StartingBalance;

    /// Dipicu setiap saldo berubah, dengan selisihnya (negatif = berkurang).
    public static event Action<int> Changed;

    public static void ResetForNewRoom() {
        int delta = CoinRules.StartingBalance - Balance;
        Balance = CoinRules.StartingBalance;
        if (delta != 0) Changed?.Invoke(delta);
    }

    /// Memotong penalti mati dan mengembalikan jumlah yang benar-benar terpotong.
    public static int ApplyDeathPenalty() {
        int lost = CoinRules.DeathPenalty(Balance);
        if (lost == 0) return 0;
        Balance -= lost;
        Changed?.Invoke(-lost);
        return lost;
    }
}
