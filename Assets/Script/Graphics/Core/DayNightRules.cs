using System;

/// <summary>
/// Aturan murni siklus siang-malam kota: jam game dari jam UTC, tinggi matahari, dan seberapa "malam".
/// Dipisah dari DayNightCycle supaya bisa dites tanpa scene.
///
/// Jam game dihitung dari waktu UTC (bukan disinkron lewat Normcore), jadi semua pemain otomatis melihat
/// jam yang sama dan pemain yang baru masuk langsung ikut tanpa beban jaringan.
/// </summary>
public static class DayNightRules {
    /// <summary>Satu siklus 24 jam game = 24 menit nyata (1 jam game = 1 menit).</summary>
    public const double DefaultCycleSeconds = 24 * 60;

    public const float SunriseHour = 6f;
    public const float SunsetHour = 18f;

    /// <summary>Lampu jalan mulai menyala saat matahari di bawah sudut ini (derajat, ~17:40).</summary>
    public const float LightsStartElevation = 6f;
    /// <summary>Lampu jalan menyala penuh saat matahari di bawah sudut ini (derajat, ~18:10).</summary>
    public const float LightsFullElevation = -2f;

    /// <summary>
    /// Jam game 0..24 dari detik Unix. <paramref name="hourOffset"/> menggeser jam (mis. supaya jam game
    /// tidak kebetulan selalu malam di jam main ramai).
    /// </summary>
    public static float HourFromUnixSeconds(double unixSeconds, double cycleSeconds, float hourOffset) {
        if (cycleSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(cycleSeconds));
        double phase = unixSeconds % cycleSeconds;
        if (phase < 0) phase += cycleSeconds;
        return WrapHour((float)(phase / cycleSeconds * 24.0) + hourOffset);
    }

    public static float WrapHour(float hour) {
        float h = hour % 24f;
        return h < 0f ? h + 24f : h;
    }

    /// <summary>
    /// Sudut matahari di atas cakrawala (derajat). 0 saat terbit (06:00) dan terbenam (18:00),
    /// <paramref name="maxElevation"/> di jam 12, dan -maxElevation di tengah malam.
    /// </summary>
    public static float SunElevation(float hour, float maxElevation) {
        double t = (WrapHour(hour) - SunriseHour) / (SunsetHour - SunriseHour); // 0..1 siang
        return (float)(Math.Sin(t * Math.PI) * maxElevation);
    }

    /// <summary>
    /// Pergeseran arah (azimut) matahari dari arah siangnya, derajat: -90 saat terbit, 0 jam 12, +90 saat
    /// terbenam, lalu terus berputar di bawah cakrawala sepanjang malam.
    /// </summary>
    public static float SunAzimuthOffset(float hour) =>
        ((WrapHour(hour) - SunriseHour) / (SunsetHour - SunriseHour) - 0.5f) * 180f;

    /// <summary>Kecerahan lampu jalan 0 (mati) .. 1 (penuh) dari tinggi matahari.</summary>
    public static float StreetLightLevel(float sunElevation) =>
        SmoothStep(LightsStartElevation, LightsFullElevation, sunElevation);

    /// <summary>0 = siang penuh, 1 = malam penuh. Transisi di sekitar cakrawala (+10° .. -8°).</summary>
    public static float NightFactor(float sunElevation) => SmoothStep(10f, -8f, sunElevation);

    /// <summary>
    /// Seberapa "senja/subuh" (0..1): puncaknya saat matahari rendah (sekitar cakrawala),
    /// nol saat siang tinggi atau malam pekat. Dipakai untuk warna oranye.
    /// </summary>
    public static float GoldenFactor(float sunElevation) {
        float rise = SmoothStep(-10f, 2f, sunElevation);
        float fall = SmoothStep(25f, 5f, sunElevation);
        return rise * fall;
    }

    /// <summary>Smoothstep yang juga menerima edge0 &gt; edge1 (turun).</summary>
    public static float SmoothStep(float edge0, float edge1, float x) {
        if (edge0 == edge1) return x < edge0 ? 0f : 1f;
        float t = (x - edge0) / (edge1 - edge0);
        t = t < 0f ? 0f : t > 1f ? 1f : t;
        return t * t * (3f - 2f * t);
    }
}
