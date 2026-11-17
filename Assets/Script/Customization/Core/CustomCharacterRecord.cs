using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Satu karakter custom milik pemain, disimpan di PlayerProgress (Cloud Save, per akun).
/// Bentuknya sengaja sederhana dan JSON-friendly: warna sebagai string hex "#RRGGBB", tanpa
/// Color/Vector (lihat aturan di PlayerProgress). Dikonversi ke CharacterData BoZo oleh
/// CharacterRecordConverter. Selalu lewatkan data dari luar ke CustomCharacterValidator dulu.
/// </summary>
[Serializable]
public class CustomCharacterRecord {
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    /// <summary>Id template asal (CharacterTemplates), sekaligus id CharacterDatabase untuk spawn.</summary>
    public string templateId = "";
    public List<OutfitRecord> outfits = new List<OutfitRecord>();
    public List<DecalRecord> decals = new List<DecalRecord>();
    public List<ShapeRecord> faceShapes = new List<ShapeRecord>();

    /// <summary>Salinan dalam (deep copy): mengubah salinan tidak mengubah record ini.</summary>
    public CustomCharacterRecord Clone() {
        return new CustomCharacterRecord {
            version = version,
            templateId = templateId,
            outfits = (outfits ?? new List<OutfitRecord>()).Select(o => o?.Clone()).ToList(),
            decals = (decals ?? new List<DecalRecord>()).Select(d => d?.Clone()).ToList(),
            faceShapes = (faceShapes ?? new List<ShapeRecord>()).Select(f => f?.Clone()).ToList()
        };
    }
}

[Serializable]
public class OutfitRecord {
    /// <summary>Path Resources BoZo, mis. "Top/BSMC_Top_Hoodie".</summary>
    public string path = "";
    /// <summary>Warna per bagian (ColorChannels), urut dari bagian 1.</summary>
    public List<string> colors = new List<string>();
    /// <summary>Path tekstur pattern di Resources, kosong = tanpa motif.</summary>
    public string pattern = "";
    public List<string> patternColors = new List<string>();
    public float patternScaleX = 1f;
    public float patternScaleY = 1f;

    public OutfitRecord Clone() {
        return new OutfitRecord {
            path = path,
            colors = new List<string>(colors ?? new List<string>()),
            pattern = pattern,
            patternColors = new List<string>(patternColors ?? new List<string>()),
            patternScaleX = patternScaleX,
            patternScaleY = patternScaleY
        };
    }
}

[Serializable]
public class DecalRecord {
    /// <summary>Nama tekstur di Resources/Decal, mis. "Decal_GoVistaG".</summary>
    public string decal = "";
    /// <summary>Salah satu CustomizationRules.DecalPositions.</summary>
    public string position = "Chest";
    public float size = 0.15f;
    /// <summary>Derajat, -180..180.</summary>
    public float rotation;
    public string color = HexColor.White;
    /// <summary>
    /// Geser halus (meter) dari titik preset posisi, dalam bingkai proyektor BoZo: X kiri–kanan, Y atas–bawah.
    /// Kedalaman (Z) selalu dari preset. Lihat CustomizationRules.ClampDecalOffset.
    /// </summary>
    public float offsetX;
    public float offsetY;

    public DecalRecord Clone() {
        return new DecalRecord {
            decal = decal, position = position, size = size, rotation = rotation, color = color,
            offsetX = offsetX, offsetY = offsetY
        };
    }
}

[Serializable]
public class ShapeRecord {
    public string key = "";
    /// <summary>0..100 (lihat CustomizationRules.ClampFaceShape).</summary>
    public float value;

    public ShapeRecord Clone() => new ShapeRecord { key = key, value = value };
}
