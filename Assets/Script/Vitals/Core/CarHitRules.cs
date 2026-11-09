using System;

/// Ukuran badan mobil dihitung dari titik pusatnya, dalam meter.
public readonly struct CarExtents {
    public float HalfWidth { get; }
    public float Rear { get; }
    public float Front { get; }
    public float Bottom { get; }
    public float Top { get; }

    public CarExtents(float halfWidth, float rear, float front, float bottom, float top) {
        HalfWidth = halfWidth;
        Rear = rear;
        Front = front;
        Bottom = bottom;
        Top = top;
    }
}

/// Aturan tertabrak mobil. Satu tempat supaya mudah disetel dan dites.
public static class CarHitRules {
    public const int Damage = 20;
    public const float MinSpeed = 2f;
    public const float InvulnerableSeconds = 2f;
    public const float ShoveMetres = 2f;
    public const float ShoveSeconds = 0.2f;

    // Karakter berhenti tepat di tepi collider mobil, jadi sentuhan perlu sedikit kelonggaran.
    private const float ContactTolerance = 0.15f;
    // Kaki yang sudah setinggi ini dianggap berada di atap, bukan tertabrak.
    private const float RoofTolerance = 0.25f;

    public static bool CanHurt(float speed, float now, float vulnerableFrom) {
        return speed >= MinSpeed && now >= vulnerableFrom;
    }

    /// Posisi kaki pemain (x, feetY, z) dalam ruang lokal mobil. Berlaku untuk semua sisi, kecuali di atap.
    public static bool Touches(CarExtents car, float x, float feetY, float z, float radius, float height) {
        if (feetY >= car.Top - RoofTolerance || feetY + height <= car.Bottom) return false;
        float outsideX = Math.Max(0f, Math.Abs(x) - car.HalfWidth);
        float outsideZ = Math.Max(0f, Math.Max(z - car.Front, -car.Rear - z));
        float reach = radius + ContactTolerance;
        return outsideX * outsideX + outsideZ * outsideZ <= reach * reach;
    }
}
