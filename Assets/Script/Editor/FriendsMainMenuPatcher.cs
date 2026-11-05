using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Menambahkan tile TEMAN ke LobbyCanvas.prefab yang SUDAH ADA (tanpa reset layout lain), lalu menata ulang
/// KARAKTER · TEMAN · PENGATURAN menjadi tiga kolom (Docs/Friends/preview-main-menu-teman.png).
/// Tile TEMAN = salinan tile KARAKTER (gaya, lip, animasi tekan ikut), lalu teks/ikon/warna diganti.
/// Aman dijalankan berulang: kalau tile TEMAN sudah ada, hanya tata letaknya yang dirapikan.
public static class FriendsMainMenuPatcher {
    private const string LobbyPrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string FriendsIconPath = "Assets/Resources/UI/Icons/icon-friends.png";

    [MenuItem("Go Vista/Friends/Tambah Tile TEMAN di Menu Utama")]
    public static bool EnsureFriendsTile() {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath) == null) {
            Debug.LogError($"[Friends] Prefab tidak ditemukan: {LobbyPrefabPath}");
            return false;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(LobbyPrefabPath);
        try {
            Transform character = FindDeep(root.transform, MainMenuTiles.CharacterTile);
            Transform settings = FindDeep(root.transform, MainMenuTiles.SettingsTile);
            if (character == null || settings == null) {
                Debug.LogError("[Friends] Tile KARAKTER / PENGATURAN tidak ditemukan di LobbyCanvas.prefab. " +
                               "Jangan ganti nama objek \"Main Menu ... Button\".");
                return false;
            }
            Transform friends = FindDeep(root.transform, MainMenuTiles.FriendsTile) ?? CloneAsFriends(character);
            MainMenuTiles.Relayout((RectTransform)character, 0);
            MainMenuTiles.Relayout((RectTransform)friends, 1);
            MainMenuTiles.Relayout((RectTransform)settings, 2);
            PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            Debug.Log("[Friends] Tile TEMAN siap di LobbyCanvas.prefab (KARAKTER · TEMAN · PENGATURAN).");
            return true;
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform CloneAsFriends(Transform character) {
        GameObject clone = Object.Instantiate(character.gameObject, character.parent);
        clone.transform.SetSiblingIndex(character.GetSiblingIndex() + 1);
        RenameTree(clone.transform, MainMenuTiles.CharacterTile, MainMenuTiles.FriendsTile);
        clone.name = MainMenuTiles.FriendsTile;

        string name = MainMenuTiles.FriendsTile;
        Transform face = clone.transform.Find(name + " Face");
        SetText(face, name + " Label", "TEMAN");
        SetText(face, name + " Subtitle", "Cari & undang");

        Transform iconBox = face != null ? face.Find(name + " Icon Box") : null;
        UIVerticalGradient gradient = iconBox != null ? iconBox.GetComponent<UIVerticalGradient>() : null;
        if (gradient != null)
            gradient.SetColors(MainMenuTiles.GreenTop, MainMenuTiles.GreenBottom);
        Image icon = iconBox != null ? iconBox.Find(name + " Icon")?.GetComponent<Image>() : null;
        if (icon != null)
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FriendsIconPath);
        MainMenuTiles.AddBadge(iconBox);

        // Listener KARAKTER (kalau ada yang tersimpan di prefab) jangan ikut tersalin.
        Button button = clone.GetComponent<Button>();
        if (button != null)
            button.onClick = new Button.ButtonClickedEvent();
        return clone.transform;
    }

    private static void RenameTree(Transform node, string from, string to) {
        if (node.name.StartsWith(from))
            node.name = to + node.name.Substring(from.Length);
        foreach (Transform child in node)
            RenameTree(child, from, to);
    }

    private static void SetText(Transform face, string objectName, string value) {
        Text text = face != null ? face.Find(objectName)?.GetComponent<Text>() : null;
        if (text != null)
            text.text = value;
    }

    private static Transform FindDeep(Transform root, string name) {
        if (root.name == name)
            return root;
        foreach (Transform child in root) {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
