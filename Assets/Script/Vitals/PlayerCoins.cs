using System;
using UnityEngine;

/// Saldo koin pemain lokal. Disimpan permanen di PlayerProgress (Cloud Save) lewat ProgressService.
/// Akun baru dan save lama yang belum punya koin mulai dari CoinRules.StartingBalance.
///
/// ⚠ Disimpan dari client, jadi bisa dicurangi. Lihat catatan KEAMANAN di PlayerProgress sebelum
/// koin bisa ditukar dengan sesuatu yang bernilai.
public static class PlayerCoins {
    public static int Balance => ProgressService.Current.coins;

    /// Dipicu setiap saldo berubah, dengan selisihnya (negatif = berkurang).
    public static event Action<int> Changed;

    private static int _lastBalance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Changed = null;
        ProgressService.Loaded -= OnProgressLoaded;
        _lastBalance = CoinRules.StartingBalance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() {
        ProgressService.Loaded += OnProgressLoaded;
        _lastBalance = Balance;
    }

    // Login atau ganti akun: saldo berubah tanpa lewat Add/Penalty, jadi HUD perlu diberi tahu.
    private static void OnProgressLoaded() => Notify();

    /// Tambah koin (hadiah). Jumlah ≤ 0 diabaikan. 'saveNow' = false kalau pemanggil langsung menyimpan
    /// progres sendiri, supaya koin dan perubahan lain (mis. quest selesai) masuk ke cloud dalam satu save.
    public static void Add(int amount, bool saveNow = true) {
        if (amount <= 0) return;
        ProgressService.Current.coins = CoinRules.Add(Balance, amount);
        ProgressService.MarkDirty();
        if (saveNow) _ = ProgressService.SaveAsync();
        Notify();
    }

    /// Memotong penalti mati dan mengembalikan jumlah yang benar-benar terpotong.
    public static int ApplyDeathPenalty() {
        int lost = CoinRules.DeathPenalty(Balance);
        if (lost == 0) return 0;
        ProgressService.Current.coins = Balance - lost;
        Save();
        return lost;
    }

    private static void Save() {
        ProgressService.MarkDirty();
        _ = ProgressService.SaveAsync();
        Notify();
    }

    private static void Notify() {
        int delta = Balance - _lastBalance;
        _lastBalance = Balance;
        if (delta != 0) Changed?.Invoke(delta);
    }
}
