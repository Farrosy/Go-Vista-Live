/// <summary>
/// Aturan mencicil spawn NPC per frame berdasarkan waktu, bukan jumlah tetap. Satu NPC kerumunan
/// (22 SkinnedMeshRenderer, ~700 transform) butuh ~7–18 ms untuk di-instantiate di Editor, lebih lama di HP,
/// jadi "4 NPC per frame" membuat frame melonjak saat pemain baru masuk World.
///
/// Per frame: NPC pertama selalu boleh (roster tetap terisi walau satu instantiate melebihi budget),
/// berikutnya hanya kalau waktu yang terpakai di frame ini masih di bawah budget dan belum mencapai batas maksimal.
/// </summary>
public static class SpawnBudget {
    public static bool CanSpawnMore(int spawnedThisFrame, double elapsedMs, float budgetMs, int maxPerFrame) {
        if (spawnedThisFrame <= 0) return true;
        if (spawnedThisFrame >= System.Math.Max(1, maxPerFrame)) return false;
        return elapsedMs < budgetMs;
    }
}
