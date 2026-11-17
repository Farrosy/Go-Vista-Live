using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Posisi stiker siap pakai (CustomizationRules.DecalPositions) → tulang + offset BoZo (DecalData).
/// Pengganti dropdown tulang + slider XYZ di creator BoZo. Tiap preset juga menentukan outfit mana yang
/// "membawa" decal-nya (decal BoZo menempel di tekstur outfit tertentu).
///
/// "Chest" diambil dari template 0_DefaultAnimeMale (logo Go Vista di hoodie); preset lain dikalibrasi
/// visual di Tahap 1d.
/// </summary>
public static class DecalPresets {
    public readonly struct Preset {
        public readonly string Bone;
        public readonly Vector3 Position;
        public readonly float Yaw;
        /// <summary>Tipe outfit pembawa, urut prioritas (mis. Top, lalu Body kalau tanpa atasan).</summary>
        public readonly string[] HostTypes;
        /// <summary>
        /// true = dipasang ke SEMUA pembawa yang dipakai, bukan hanya yang pertama. Untuk lengan/kaki: atasan
        /// tanpa lengan tidak punya mesh di lengan, jadi stiker harus ikut ke kulit (Body) supaya terlihat.
        /// </summary>
        public readonly bool AllHosts;
        /// <summary>Pengali ukuran stiker (wajah jauh lebih kecil dari dada).</summary>
        public readonly float SizeScale;

        public Preset(string bone, Vector3 position, float yaw, bool allHosts, float sizeScale, params string[] hostTypes) {
            Bone = bone;
            Position = position;
            Yaw = yaw;
            AllHosts = allHosts;
            SizeScale = sizeScale;
            HostTypes = hostTypes;
        }
    }

    // Dikalibrasi visual di tahap 1d (karakter Default Cowok). Sumbu posisi = bingkai proyektor BoZo: X samping,
    // Y atas/bawah, Z kedalaman (negatif = ke depan). Yaw memutar proyektor mengelilingi tulang: 180 = punggung,
    // -90 = sisi luar lengan (BoZo mencerminkan sendiri tulang _r).
    // Wajah sengaja memakai neck_02, bukan "head": BoZo memproyeksikan decal tulang head ke arah DUNIA
    // (-Vector3.forward), jadi hasilnya bergantung arah hadap karakter saat bake (meja putar, pemain di world).
    // BoZo membake decal dalam pose T (lengan horizontal): offset lengan > 0.12 jatuh di luar mesh lengan.
    private static readonly Dictionary<string, Preset> Presets = new Dictionary<string, Preset> {
        { "Chest", new Preset("spine_04", new Vector3(0.087f, 0.116f, -0.05f), 0f, false, 1f, "Top", "Overall", "Body") },
        { "Back", new Preset("spine_04", new Vector3(0f, 0f, -0.05f), 180f, false, 1f, "Top", "Overall", "Body") },
        { "Face", new Preset("neck_02", new Vector3(0.045f, 0.025f, -0.07f), 0f, true, 0.35f, "Head") },
        { "LeftArm", new Preset("upperarm_l", new Vector3(0f, -0.12f, 0f), -90f, true, 0.8f, "Top", "Overall", "Body") },
        { "RightArm", new Preset("upperarm_r", new Vector3(0f, -0.12f, 0f), -90f, true, 0.8f, "Top", "Overall", "Body") },
        // UV celana BoZo dicerminkan kiri-kanan, jadi stiker Kaki muncul di kedua paha.
        { "Leg", new Preset("thigh_l", new Vector3(0f, -0.15f, 0f), 0f, true, 0.8f, "Bottom", "Overall", "Leggings", "Body") }
    };

    public static bool TryGet(string position, out Preset preset) => Presets.TryGetValue(position ?? "", out preset);

    /// <summary>Preset pertama yang memakai tulang ini (spine_04 → Chest), atau null.</summary>
    public static string PositionForBone(string bone) {
        foreach (string position in CustomizationRules.DecalPositions) {
            if (Presets.TryGetValue(position, out Preset preset) && preset.Bone == bone) return position;
        }
        return null;
    }

    /// <summary>DecalData BoZo untuk record ini (tekstur dimuat oleh BMAC_SaveSystem saat load).</summary>
    public static DecalData ToDecalData(DecalRecord record, Preset preset) {
        Color color = ColorUtility.TryParseHtmlString(record.color, out Color parsed) ? parsed : Color.white;
        return new DecalData(record.decal, preset.Bone, preset.Position, preset.Yaw, record.rotation, 0f,
                             record.size * preset.SizeScale, color);
    }
}
