using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Posisi stiker siap pakai (CustomizationRules.DecalPositions) → tulang + offset BoZo (DecalData).
/// Pengganti dropdown tulang + slider XYZ di creator BoZo. Tiap preset juga menentukan outfit mana yang
/// "membawa" decal-nya (decal BoZo menempel di tekstur outfit tertentu).
///
/// "Chest" diambil dari template 0_DefaultAnimeMale (logo Go Vista di hoodie). Preset lain adalah
/// perkiraan awal dan disetel ulang secara visual di Tahap 1d.
/// </summary>
public static class DecalPresets {
    public readonly struct Preset {
        public readonly string Bone;
        public readonly Vector3 Position;
        public readonly float Yaw;
        /// <summary>Tipe outfit pembawa, urut prioritas (mis. Top, lalu Body kalau tanpa atasan).</summary>
        public readonly string[] HostTypes;

        public Preset(string bone, Vector3 position, float yaw, params string[] hostTypes) {
            Bone = bone;
            Position = position;
            Yaw = yaw;
            HostTypes = hostTypes;
        }
    }

    private static readonly Dictionary<string, Preset> Presets = new Dictionary<string, Preset> {
        { "Chest", new Preset("spine_04", new Vector3(0.087f, 0.116f, -0.05f), 0f, "Top", "Overall", "Body") },
        { "Back", new Preset("spine_04", new Vector3(0.087f, -0.12f, -0.05f), 180f, "Top", "Overall", "Body") },
        { "Face", new Preset("head", new Vector3(0.08f, 0.1f, 0f), 0f, "Head") },
        { "LeftArm", new Preset("upperarm_l", new Vector3(0.12f, 0f, 0.05f), 90f, "Top", "Body") },
        { "RightArm", new Preset("upperarm_r", new Vector3(-0.12f, 0f, 0.05f), -90f, "Top", "Body") },
        { "Leg", new Preset("thigh_l", new Vector3(0.2f, 0.08f, 0f), 0f, "Bottom", "Overall", "Body") }
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
                             record.size, color);
    }
}
