using System.Collections.Generic;
using UnityEngine;

/// Data awal peta venue dari mockup yang disetujui 2026-11-18 (memory: venue-map-decisions).
/// Posisi = meter dunia (x, z), diukur dari render top-down kedua scene. Setelah dibake, data ini
/// tinggal di komponen VenueMapData dan boleh diedit di Inspector; baker tidak menimpanya lagi.
public static class VenueMapDefaults {
    public sealed class Venue {
        public string ScenePath, DisplayName;
        public float FloorY, UpperLift;
        public System.Func<List<VenueMapData.Area>> Areas;
        public System.Func<List<Rect>> UpperOutline;
    }

    private const string SceneFolder = "Assets/Download_Assets/PolygonNightclubs/Scenes/";

    public static readonly Venue[] All = {
        new Venue {
            ScenePath = SceneFolder + VenueMap.DanceClubSceneName + ".unity",
            DisplayName = "KPOP Arena",
            FloorY = -15f,
            UpperLift = 2f, // balkon di Y −11,9; bordes tangga Y −13,5 dan booth di bawah balkon Y −14,2 tetap lantai bawah
            Areas = () => new List<VenueMapData.Area> {
                A("Pintu teleport", -11.35f, 14.03f, 2.2f, 94, 240, 255),
                A("Bar Utama", 0f, 11.83f, 6.0f, 255, 196, 61),
                A("Panggung DJ", 0f, 3.33f, 3.0f, 159, 179, 255),
                A("Lantai Dansa", 0f, -8.67f, 6.9f, 255, 216, 77),
                A("Lounge Ungu", -14.83f, 7.17f, 5.6f, 200, 107, 255),
                A("Booth VIP Oranye", -16.5f, -9.83f, 5.6f, 255, 154, 77),
                A("Booth VIP Biru", 16.83f, -9.83f, 5.6f, 63, 182, 255),
                A("Area Meja Kanan", 10f, -5.83f, 4.3f, 142, 163, 194),
                A("Tangga ke balkon", 15.83f, 6.83f, 3.9f, 109, 124, 255),
                A("Ruang lantai catur", 13.67f, 17.5f, 4.3f, 231, 231, 231),
                A("Balkon VIP", 16.17f, -1.83f, 5.2f, 255, 77, 109, upper: true),
            },
            UpperOutline = () => new List<Rect> {
                Rect.MinMaxRect(12.43f, -20f, 20f, 5.1f),   // balkon sisi kanan
                Rect.MinMaxRect(5.5f, -9.33f, 12.43f, -8.17f), // jembatan dari tangga lantai dansa
            },
        },
        new Venue {
            ScenePath = SceneFolder + VenueMap.RooftopBarSceneName + ".unity",
            DisplayName = "KPOP Community",
            FloorY = 0f,
            UpperLift = 0f,
            Areas = () => new List<VenueMapData.Area> {
                A("Pintu teleport", 11.15f, 3.92f, 2.1f, 94, 240, 255),
                A("Bar Tengah", 1.17f, -2.5f, 5.2f, 255, 196, 61),
                A("Karpet Merah", 11.17f, -1.17f, 3.0f, 216, 69, 79),
                A("Area Makan", 11.17f, -13.17f, 5.2f, 215, 155, 107),
                A("Lounge Sofa", 1.17f, -14.83f, 5.2f, 255, 154, 77),
                A("Lounge Karpet", -13.83f, -14.83f, 5.2f, 196, 107, 60),
                A("Meja Kiri", -11.17f, -2.5f, 4.3f, 159, 179, 255),
                A("Toilet", -11.83f, 11.83f, 4.3f, 231, 231, 231),
                A("Teras (meja pinggir)", 18.3f, -6f, 4.5f, 142, 163, 194), // di bawah Karpet Merah supaya label tidak menumpuk
            },
            UpperOutline = () => new List<Rect>(),
        },
    };

    private static VenueMapData.Area A(string name, float x, float z, float radius, byte r, byte g, byte b, bool upper = false) =>
        new VenueMapData.Area { name = name, center = new Vector2(x, z), radius = radius, color = new Color32(r, g, b, 255), upper = upper };
}
