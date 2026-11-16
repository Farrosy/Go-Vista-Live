using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Referensi aset customizer yang tidak ada di Resources (prefab dasar BoZo ada di folder vendor).
/// Asset: Assets/Resources/CustomizerSettings.asset, dibuat lewat menu
/// Go Vista > Customization > Buat Customizer Settings.
/// </summary>
[CreateAssetMenu(menuName = "Go Vista/Customizer Settings", fileName = "CustomizerSettings")]
public class CustomizerSettings : ScriptableObject {
    public const string ResourcePath = "CustomizerSettings";

    [Tooltip("BSMC_CharacterBase (OutfitSystem) untuk merakit karakter custom saat runtime.")]
    public OutfitSystem characterBase;

    private static CustomizerSettings _instance;

    public static CustomizerSettings Instance {
        get {
            if (_instance == null) _instance = Resources.Load<CustomizerSettings>(ResourcePath);
            return _instance;
        }
    }
}
