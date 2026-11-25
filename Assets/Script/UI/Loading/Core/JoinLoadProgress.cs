/// <summary>
/// Progres gabungan 0..1 untuk bar di tombol MULAI, dari tahap-tahap masuk world berurutan:
/// karakter di-bake (0 → CharacterShare), lalu kerumunan NPC (CharacterShare → 1).
/// Dipisah per tahap supaya bar tidak pernah mundur ke 0 saat ganti tahap.
/// </summary>
public static class JoinLoadProgress {
    /// Porsi bar untuk tahap bake karakter; sisanya untuk kerumunan NPC.
    public const float CharacterShare = 0.3f;

    /// Rasio done/total dibatasi 0..1. Total ≤ 0 berarti tahap itu tidak ada pekerjaan = selesai.
    public static float Fraction(int done, int total) {
        if (total <= 0) return 1f;
        if (done <= 0) return 0f;
        return done >= total ? 1f : (float)done / total;
    }

    /// Selama bake karakter: hanya mengisi porsi karakter.
    public static float DuringCharacters(int bakesDone, int bakesTotal) {
        return Fraction(bakesDone, bakesTotal) * CharacterShare;
    }

    /// Selama kerumunan NPC: porsi karakter sudah penuh, sisanya mengikuti jumlah NPC.
    public static float DuringCrowd(int npcPresent, int npcTarget) {
        return CharacterShare + Fraction(npcPresent, npcTarget) * (1f - CharacterShare);
    }

    /// Persen bulat untuk label (0..100).
    public static int Percent(float progress) {
        if (progress <= 0f) return 0;
        if (progress >= 1f) return 100;
        return (int)(progress * 100f);
    }
}
