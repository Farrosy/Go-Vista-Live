using Bozo.ModularCharacters;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Membuat/memperbarui Assets/Resources/CustomizerSettings.asset (referensi prefab dasar BoZo untuk
/// karakter custom). Aman dijalankan ulang.
/// </summary>
public static class CustomizerSettingsSetup {
    public const string AssetPath = "Assets/Resources/CustomizerSettings.asset";
    private const string BasePrefabPath =
        "Assets/Download_Assets/BoZo_StylizedModularCharacters/Prefabs/BSMC_CharacterBase.prefab";

    [MenuItem("Go Vista/Customization/Buat Customizer Settings")]
    public static void Run() {
        var basePrefab = AssetDatabase.LoadAssetAtPath<OutfitSystem>(BasePrefabPath);
        if (basePrefab == null) {
            Debug.LogError($"[Customizer] Prefab dasar tidak ditemukan: {BasePrefabPath}");
            return;
        }

        var settings = AssetDatabase.LoadAssetAtPath<CustomizerSettings>(AssetPath);
        if (settings == null) {
            settings = ScriptableObject.CreateInstance<CustomizerSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }
        settings.characterBase = basePrefab;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Customizer] {AssetPath} siap (prefab dasar: {basePrefab.name}).", settings);
    }
}
