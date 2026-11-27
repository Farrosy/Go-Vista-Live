/// <summary>
/// Aturan murni untuk pengaturan "Efek grafik" (post-processing kota: tonemapping, warna, bloom, vignette).
/// Dipisah dari GameSettings supaya bisa dites tanpa PlayerPrefs / SystemInfo.
/// </summary>
public static class GraphicsQualityRules {
    /// <summary>
    /// Di bawah RAM ini (MB) HP dianggap low-end, jadi efek grafik defaultnya mati supaya FPS aman.
    /// Pemain tetap bisa menyalakannya di PENGATURAN.
    /// </summary>
    public const int MinMemoryMbForEffects = 3000;

    /// <summary>
    /// Nilai default "Efek grafik" sebelum pemain pernah mengubahnya.
    /// RAM tidak diketahui (0 / negatif) dianggap cukup, karena SystemInfo hanya gagal di platform non-HP.
    /// </summary>
    public static bool DefaultEffectsOn(int systemMemoryMb) =>
        systemMemoryMb <= 0 || systemMemoryMb >= MinMemoryMbForEffects;
}
