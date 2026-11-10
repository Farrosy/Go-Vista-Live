/// Aturan posisi tubuh terhadap permukaan air.
public static class WaterRules {
    /// Kedalaman (dari kaki) saat karakter mulai berjalan lambat: kira-kira setinggi lutut.
    public const float WadingDepthMetres = 0.4f;

    public static bool IsWading(float feetDepth) => feetDepth >= WadingDepthMetres;

    public static bool IsHeadUnderWater(float headY, float waterLevel) => headY < waterLevel;
}
