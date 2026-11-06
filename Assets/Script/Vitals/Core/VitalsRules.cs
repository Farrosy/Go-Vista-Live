using System;

/// Angka aturan HP dan O₂. Satu tempat supaya mudah disetel dan dites.
public static class VitalsRules {
    public const int MaxHealth = 100;
    public const float OxygenDurationSeconds = 10f;
    public const float OxygenRefillSeconds = 2f;
    public const float OxygenWarningFraction = 0.5f;
    public const float OxygenDangerFraction = 0.2f;
    public const float HealthCriticalFraction = 0.2f;
    public const int OxygenSegmentCount = 10;
    public const float SafeFallMetres = 3f;
    public const int FallDamagePerMetre = 10;

    /// Toleransi pembulatan float supaya batas seperti "tepat 10 detik" tidak meleset satu frame.
    internal const float Epsilon = 0.0001f;

    public static int FallDamage(float fallMetres) {
        float excess = fallMetres - SafeFallMetres;
        return excess <= Epsilon ? 0 : (int)Math.Ceiling(excess * FallDamagePerMetre - Epsilon);
    }
}
