using System;
using System.Collections.Generic;

/// <summary>
/// Aturan customizer yang diputuskan bersama user (memory: character-customizer-revamp). Logika murni,
/// tanpa UnityEngine, supaya bisa dites EditMode dan dipakai ulang oleh validator + UI.
/// </summary>
public static class CustomizationRules {
    public const float ShapeMin = 0f;
    public const float ShapeMax = 100f;
    /// <summary>Iris dan kelopak mata dibatasi 50% supaya wajah tidak aneh.</summary>
    public const float CappedShapeMax = 50f;

    public const int MaxDecals = 5;
    public const float MinDecalSize = 0.05f;
    public const float MaxDecalSize = 0.4f;
    public const float MaxDecalRotation = 180f;
    public const int MaxColorChannels = 9;
    public const int PatternColorCount = 3;
    public const float MinPatternScale = 0.5f;
    public const float MaxPatternScale = 20f;
    /// <summary>Motif BoZo dimuat dari Resources "Pattern/&lt;nama&gt;".</summary>
    public const string PatternFolder = "Pattern/";

    /// <summary>Pilihan cepat warna kulit (tab Kulit), dari terang ke gelap. Warna lain lewat HSV picker.</summary>
    public static readonly IReadOnlyList<string> SkinTones = new[] {
        "#FCE3D3", "#F7D2B8", "#F1C09E", "#E5A882", "#D49770", "#C08160",
        "#A86B4A", "#8D5638", "#6E4129", "#4F2D1C"
    };

    /// <summary>Posisi stiker siap pakai (pengganti dropdown tulang + slider XYZ BoZo).</summary>
    public static readonly IReadOnlyList<string> DecalPositions = new[] {
        "Chest", "Back", "Face", "LeftArm", "RightArm", "Leg"
    };

    private static readonly HashSet<string> CappedShapes = new HashSet<string> { "IrisSize", "EyeLidHeight" };
    private static readonly HashSet<string> MaleOnlyShapes = new HashSet<string> { "Stern" };

    /// Blendshape badan yang juga muncul di kepala. Bentuk badan dikunci template, jadi disembunyikan.
    private static readonly HashSet<string> BodyShapesOnHead = new HashSet<string> {
        "BodyType", "Weight", "Muscle", "NeckThickness"
    };

    /// Bagian dasar yang tidak boleh kosong. UnderUpper diatur terpisah (cowok boleh kosong).
    private static readonly HashSet<string> RequiredTypes = new HashSet<string> {
        "Head", "Body", "Eyes", "UnderLower"
    };
    private const string UpperUnderwearType = "UnderUpper";

    public static bool IsFaceShapeVisible(string key, bool isMale) {
        if (string.IsNullOrEmpty(key) || BodyShapesOnHead.Contains(key)) return false;
        return isMale || !MaleOnlyShapes.Contains(key);
    }

    public static float ClampFaceShape(string key, float value) {
        float max = CappedShapes.Contains(key ?? "") ? CappedShapeMax : ShapeMax;
        return Math.Max(ShapeMin, Math.Min(max, value));
    }

    public static float MaxFor(string key) => CappedShapes.Contains(key ?? "") ? CappedShapeMax : ShapeMax;

    /// <summary>Apakah slot tipe outfit ini boleh "Kosong" di editor.</summary>
    public static bool CanBeEmpty(string outfitType, bool isMale) {
        if (outfitType == UpperUnderwearType) return isMale;
        return !RequiredTypes.Contains(outfitType ?? "");
    }

    /// <summary>Label bagian warna. Bagian tanpa nama di data BoZo jadi "Warna N".</summary>
    public static string ColorChannelLabel(string channelName, int index) {
        return string.IsNullOrWhiteSpace(channelName) ? $"Warna {index + 1}" : channelName;
    }

    public static bool IsDecalPosition(string position) {
        foreach (string known in DecalPositions) {
            if (known == position) return true;
        }
        return false;
    }

    /// <summary>Path motif kosong (tanpa motif) atau "Pattern/Nama" tanpa folder lain.</summary>
    public static bool IsPatternPath(string path) {
        if (string.IsNullOrEmpty(path)) return true;
        if (!path.StartsWith(PatternFolder, StringComparison.Ordinal) || path.Length == PatternFolder.Length) return false;
        string name = path.Substring(PatternFolder.Length);
        return name.IndexOf('/') < 0 && name.IndexOf('\\') < 0 && name.IndexOf("..", StringComparison.Ordinal) < 0;
    }

    public static float ClampPatternScale(float scale) {
        if (float.IsNaN(scale)) return 1f;
        return Math.Max(MinPatternScale, Math.Min(MaxPatternScale, scale));
    }

    public static float ClampDecalSize(float size) => Math.Max(MinDecalSize, Math.Min(MaxDecalSize, size));

    public static float ClampDecalRotation(float degrees) {
        return Math.Max(-MaxDecalRotation, Math.Min(MaxDecalRotation, degrees));
    }
}
