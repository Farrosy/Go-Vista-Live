using UnityEngine;

/// <summary>
/// Dibuat otomatis oleh ProgressService (tidak perlu dipasang di scene). Tugasnya: menyimpan progres
/// ke cloud tiap beberapa menit dan saat app di-pause/ditutup.
/// </summary>
public sealed class ProgressAutoSave : MonoBehaviour {
    // Kirim ke Cloud Save. Jangan terlalu sering: kuota gratis 1 juta write per bulan untuk SEMUA pemain.
    // Dengan 120 detik, 1 pemain yang main 1 jam = maksimal 30 write.
    private const float SaveInterval = 120f;

    private float _nextSave;

    public static void Create() {
        if (FindFirstObjectByType<ProgressAutoSave>() != null)
            return;
        var runner = new GameObject("[ProgressAutoSave]");
        runner.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(runner);
        runner.AddComponent<ProgressAutoSave>();
    }

    private void Update() {
        float now = Time.unscaledTime;
        if (now >= _nextSave) {
            _nextSave = now + SaveInterval;
            if (ProgressService.IsDirty)
                _ = ProgressService.SaveAsync();
        }
    }

    // Di HP, ini dipanggil saat pemain pindah ke app lain atau mengunci layar. Ini kesempatan
    // paling aman untuk save, karena OnApplicationQuit sering tidak dipanggil di Android/iOS.
    private void OnApplicationPause(bool paused) {
        if (paused)
            _ = ProgressService.SaveAsync();
    }

    // Best effort: save async bisa terpotong kalau app benar-benar tertutup.
    private void OnApplicationQuit() {
        _ = ProgressService.SaveAsync();
    }
}
