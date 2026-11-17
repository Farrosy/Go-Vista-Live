using UnityEditor;
using UnityEngine;

/// <summary>
/// Tahap 3 customizer: memasang PlayerAppearanceSync di root semua prefab avatar pemain (CharacterDatabase),
/// supaya karakter custom tersinkron ke pemain lain. Aman dijalankan ulang (yang sudah terpasang dilewati).
/// RealtimeView Normcore mendaftarkan komponen baru saat prefab disimpan; hasilnya dicatat di Console.
/// </summary>
public static class PlayerAppearanceInstaller {
    private const string MenuPath = "Go Vista/Customization/Pasang PlayerAppearanceSync di Prefab Pemain";

    [MenuItem(MenuPath)]
    public static void Install() {
        CharacterDatabase database = CharacterDatabase.Instance;
        if (!database) {
            Debug.LogError("[Appearance] CharacterDatabase tidak ada di Resources.");
            return;
        }
        int added = 0;
        for (int i = 0; i < database.Count; i++) {
            CharacterDatabase.Entry entry = database[i];
            if (entry == null || !entry.avatarPrefab) continue;
            string path = AssetDatabase.GetAssetPath(entry.avatarPrefab);
            if (string.IsNullOrEmpty(path)) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                if (root.GetComponent<PlayerAppearanceSync>()) continue;
                root.AddComponent<PlayerAppearanceSync>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                added++;
                Debug.Log($"[Appearance] PlayerAppearanceSync dipasang di {path}");
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log($"[Appearance] Selesai: {added} prefab diperbarui.");
    }
}
