using System.Collections.Generic;

/// <summary>
/// Ekspresi wajah siap pakai (sub-tab Wajah · Ekspresi), diberi nomor "Ekspresi 1..8". Bobotnya disalin dari
/// klip BoZo Animations/Expressions/BSMC_*.anim (semuanya pose statis satu keyframe), urutan sama dengan blend tree
/// "Expression" di CharacterCreatorCTRL. Diterapkan lewat kode ke blendshape "Expression_*" kepala, bukan lewat
/// Animator: controller pemain tidak punya layer wajah, dan kepala Anime tidak cocok dengan path klip BoZo.
/// Blendshape yang tidak dimiliki kepala ini (mis. Mouth_Oh di kepala Anime) dilewati saja.
/// </summary>
public static class FaceExpressions {
    private const string Prefix = "Expression_";

    private static readonly IReadOnlyDictionary<string, float>[] Presets = {
        // 1 · Neutral
        new Dictionary<string, float>(),
        // 2 · Smile
        new Dictionary<string, float> {
            ["Mouth_Happy_R"] = 52f, ["Mouth_Happy_L"] = 52.6f, ["Mouth_Wide_R"] = 80.1f, ["Mouth_Wide_L"] = 80.9f,
            ["Mouth_Oh_R"] = 63f, ["Mouth_Oh_L"] = 62.5f, ["Brows_Raised_R"] = 29.3f, ["Brows_Raised_L"] = 28.8f,
            ["Brows_Sad_L"] = 5.9f, ["Brows_Sad_R"] = 6.2f, ["Eyes_LowerClosed_R"] = 5.9f, ["Eyes_LowerClosed_L"] = 6.2f
        },
        // 3 · Angry
        new Dictionary<string, float> {
            ["Brow_Angry_L"] = 51.6f, ["Brow_Angry_R"] = 48.7f, ["Brows_Lowered_R"] = 100f, ["Brows_Lowered_L"] = 100f,
            ["Mouth_Sad_R"] = 41.4f, ["Mouth_Sad_L"] = 41.8f, ["Eyes_LowerClosed_L"] = 9.7f, ["Eyes_LowerClosed_R"] = 8.9f
        },
        // 4 · Sad
        new Dictionary<string, float> {
            ["Brows_Sad_L"] = 99.2f, ["Brows_Sad_R"] = 99.8f, ["Mouth_Sad_R"] = 43.8f, ["Mouth_Sad_L"] = 40.4f,
            ["Brows_Lowered_L"] = 100f, ["Brows_Lowered_R"] = 100f, ["Eyes_UpperClosed_R"] = 12.8f,
            ["Eyes_UpperClosed_L"] = 12.6f, ["Eyes_LowerClosed_R"] = 9.8f, ["Eyes_LowerClosed_L"] = 9.9f,
            ["Mouth_Oh_R"] = 32.2f, ["Mouth_Oh_L"] = 34.3f, ["Mouth_Press_R"] = 32.4f, ["Mouth_Press_L"] = 33.5f
        },
        // 5 · Surprised
        new Dictionary<string, float> {
            ["Brows_Sad_R"] = 34.1f, ["Brows_Sad_L"] = 33.7f, ["Brows_Raised_R"] = 100f, ["Brows_Raised_L"] = 100f,
            ["Eyes_UpperOpen_R"] = 100f, ["Eyes_UpperOpen_L"] = 100f, ["Eyes_LowerOpen_R"] = 94.6f,
            ["Eyes_LowerOpen_L"] = 100f, ["Jaw_Open"] = 45.7f, ["Mouth_Oh_L"] = 68.7f, ["Mouth_Oh_R"] = 68.9f
        },
        // 6 · Devious
        new Dictionary<string, float> {
            ["Eyes_LowerClosed_L"] = 19.7f, ["Eyes_LowerClosed_R"] = 17.5f, ["Mouth_Wide_R"] = 79.4f, ["Mouth_Wide_L"] = 100f,
            ["Mouth_Oh_R"] = 43f, ["Mouth_Oh_L"] = 43.3f, ["Mouth_Happy_R"] = 46.4f, ["Mouth_Happy_L"] = 71.3f,
            ["Brows_Sad_R"] = 34.3f, ["Brow_Angry_L"] = 67.7f, ["Brow_Angry_R"] = 74.4f,
            ["Mouth_Press_R"] = 33f, ["Mouth_Press_L"] = 27.3f
        },
        // 7 · Happy
        new Dictionary<string, float> {
            ["Mouth_Happy_R"] = 99.3f, ["Mouth_Happy_L"] = 100f, ["Jaw_Open"] = 40.3f, ["Mouth_Wide_R"] = 63.4f,
            ["Mouth_Wide_L"] = 63.2f, ["Eyes_UpperOpen_R"] = 34.6f, ["Eyes_UpperOpen_L"] = 33.9f,
            ["Eyes_LowerOpen_R"] = 15.7f, ["Eyes_LowerOpen_L"] = 16.7f, ["Brows_Raised_L"] = 88.4f, ["Brows_Raised_R"] = 100f
        },
        // 8 · Evil
        new Dictionary<string, float> {
            ["Brow_Angry_L"] = 100f, ["Brow_Angry_R"] = 100f, ["Mouth_Wide_L"] = 100f, ["Mouth_Wide_R"] = 100f,
            ["Mouth_Grimace_R"] = 22.2f, ["Mouth_Grimace_L"] = 21.4f, ["Mouth_Happy_L"] = 81.1f, ["Mouth_Happy_R"] = 82.3f,
            ["Mouth_Oh_R"] = 45f, ["Mouth_Oh_L"] = 43.8f, ["Brows_Lowered_L"] = 94.6f
        }
    };

    public static int Count => Presets.Length;

    /// <returns>Indeks valid, atau 0 (netral) kalau di luar jangkauan (data lama / data jaringan aneh).</returns>
    public static int Clamp(int index) => index >= 0 && index < Presets.Length ? index : 0;

    /// <summary>Nama di grid, bernomor mulai 1 (keputusan user: tanpa nama emosi).</summary>
    public static string Label(int index) => "Ekspresi " + (Clamp(index) + 1);

    /// <summary>Bobot blendshape (kunci tanpa awalan "Expression_"). Yang tidak tercantum = 0.</summary>
    public static IReadOnlyDictionary<string, float> Weights(int index) => Presets[Clamp(index)];

    /// <returns>Kunci ekspresi dari nama blendshape mesh ("BS_Head.Expression_Jaw_Open" → "Jaw_Open"), atau null.</returns>
    public static string KeyOf(string blendShapeName) {
        if (string.IsNullOrEmpty(blendShapeName)) return null;
        int dot = blendShapeName.IndexOf('.');
        string name = dot >= 0 ? blendShapeName.Substring(dot + 1) : blendShapeName;
        return name.StartsWith(Prefix, System.StringComparison.Ordinal) ? name.Substring(Prefix.Length) : null;
    }
}
