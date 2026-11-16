using System;
using System.Collections.Generic;

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
}

[Serializable]
public class ShapeRecord {
    public string key = "";
    /// <summary>0..100 (lihat CustomizationRules.ClampFaceShape).</summary>
    public float value;
}
