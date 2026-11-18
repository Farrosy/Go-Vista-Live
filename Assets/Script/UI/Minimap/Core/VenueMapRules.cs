using System;
using System.Collections.Generic;

/// <summary>Satu area bernama di peta venue (posisi dunia x,z dalam meter).</summary>
public readonly struct VenueArea {
    public readonly string Name;
    public readonly float X, Z, Radius;
    /// <summary>Area di lantai atas (mis. balkon KPOP Arena).</summary>
    public readonly bool Upper;

    public VenueArea(string name, float x, float z, float radius, bool upper) {
        Name = name; X = x; Z = z; Radius = radius; Upper = upper;
    }
}

/// <summary>
/// Aturan peta venue KPOP Arena / KPOP Community (memory: venue-map-decisions).
/// Peta hanya satu lantai; pemain di balkon tetap digambar di posisi x,z yang sama dengan label BALKON.
/// Logika murni tanpa UnityEngine supaya bisa dites EditMode.
/// </summary>
public static class VenueMapRules {
    /// <summary>Pemain sedikit di luar lingkaran area masih dihitung "di" area itu (jarak / jari-jari).</summary>
    public const float AreaReach = 1.6f;

    /// <summary>
    /// Pemain di lantai atas kalau kakinya lebih dari <paramref name="upperLift"/> meter di atas lantai venue.
    /// Bordes tangga (1,5 m) dan booth di bawah balkon (0,8 m) masih lantai bawah.
    /// <paramref name="upperLift"/> ≤ 0 = venue tanpa lantai atas.
    /// </summary>
    public static bool IsUpperLevel(float playerY, float floorY, float upperLift) =>
        upperLift > 0f && playerY - floorY > upperLift;

    /// <summary>
    /// Indeks area tempat pemain berada, atau -1. Hanya area di lantai yang sama yang dihitung.
    /// Di lantai atas area terdekat selalu dipakai (lantai atas hanya berisi balkon dan jalan ke sana).
    /// </summary>
    public static int AreaIndexAt(IReadOnlyList<VenueArea> areas, float x, float z, bool upper) {
        if (areas == null) return -1;
        int best = -1;
        float bestRatio = float.MaxValue;
        for (int i = 0; i < areas.Count; i++) {
            VenueArea area = areas[i];
            if (area.Upper != upper || area.Radius <= 0f) continue;
            float dx = area.X - x, dz = area.Z - z;
            float ratio = (float)Math.Sqrt(dx * dx + dz * dz) / area.Radius;
            if (ratio < bestRatio) {
                bestRatio = ratio;
                best = i;
            }
        }
        if (best < 0) return -1;
        return upper || bestRatio <= AreaReach ? best : -1;
    }

    /// <summary>Jangkauan radar venue saat sprint: rasio zoom-out sama dengan radar pulau.</summary>
    public static float SprintRadius(float venueWalk, float islandWalk, float islandSprint) =>
        islandWalk > 0f && islandSprint > 0f ? venueWalk * (islandSprint / islandWalk) : venueWalk;
}
