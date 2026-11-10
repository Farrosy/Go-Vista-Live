using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Merapikan menu utama di LobbyCanvas.prefab yang SUDAH ADA (tanpa reset layout lain):
///  - tile KARAKTER menjadi PROFIL (Docs/Profil/01-main-menu.png),
///  - tile TEMAN ditambahkan (salinan tile PROFIL: gaya, lip, animasi tekan ikut; teks/ikon/warna diganti),
///  - header sapaan "Selamat datang, nama" dihapus dan isi kartu dinaikkan,
///  - PROFIL · TEMAN · PENGATURAN ditata tiga kolom.
/// Aman dijalankan berulang: bagian yang sudah beres dilewati, tata letak dirapikan ulang.
public static class FriendsMainMenuPatcher {
    private const string LobbyPrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string FriendsIconPath = "Assets/Resources/UI/Icons/icon-friends.png";

    private static readonly string[] HeaderObjects = {
        "Main Menu Avatar Ring", "Main Menu Avatar", "Main Menu Greeting", "Main Menu Name",
    };

    [MenuItem("Go Vista/Profile/Rapikan Menu Utama (PROFIL · TEMAN · PENGATURAN)")]
    public static bool PatchMainMenu() => EnsureFriendsTile();

    [MenuItem("Go Vista/Friends/Tambah Tile TEMAN di Menu Utama")]
    public static bool EnsureFriendsTile() {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath) == null) {
            Debug.LogError($"[Friends] Prefab tidak ditemukan: {LobbyPrefabPath}");
            return false;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(LobbyPrefabPath);
        try {
            Transform profile = FindDeep(root.transform, MainMenuTiles.ProfileTile) ?? ConvertToProfile(root.transform);
            Transform settings = FindDeep(root.transform, MainMenuTiles.SettingsTile);
            if (profile == null || settings == null) {
                Debug.LogError("[Friends] Tile KARAKTER/PROFIL atau PENGATURAN tidak ditemukan di LobbyCanvas.prefab. " +
                               "Jangan ganti nama objek \"Main Menu ... Button\".");
                return false;
            }
            Transform friends = FindDeep(root.transform, MainMenuTiles.FriendsTile) ?? CloneAsFriends(profile);
            RemoveHeader(root.transform);
            MainMenuTiles.Relayout((RectTransform)profile, 0);
            MainMenuTiles.Relayout((RectTransform)friends, 1);
            MainMenuTiles.Relayout((RectTransform)settings, 2);
            PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            Debug.Log("[Friends] Menu utama siap di LobbyCanvas.prefab (PROFIL · TEMAN · PENGATURAN, tanpa sapaan).");
            return true;
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform CloneAsFriends(Transform character) {
        GameObject clone = Object.Instantiate(character.gameObject, character.parent);
        clone.transform.SetSiblingIndex(character.GetSiblingIndex() + 1);
        RenameTree(clone.transform, MainMenuTiles.ProfileTile, MainMenuTiles.FriendsTile);
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

    /// Tile KARAKTER lama → PROFIL: ganti nama objek (listener mencari nama baru), label, dan subtitle.
    private static Transform ConvertToProfile(Transform root) {
        Transform character = FindDeep(root, MainMenuTiles.CharacterTile);
        if (character == null)
            return null;
        RenameTree(character, MainMenuTiles.CharacterTile, MainMenuTiles.ProfileTile);
        Transform face = character.Find(MainMenuTiles.ProfileTile + " Face");
        SetText(face, MainMenuTiles.ProfileTile + " Label", "PROFIL");
        SetText(face, MainMenuTiles.ProfileTile + " Subtitle", "Pemain"); // diisi nama pemain saat runtime
        return character;
    }

    /// Hapus header sapaan, lalu naikkan MAIN / tile / KELUAR GAME dan perpendek kartu (sekali saja).
    private static void RemoveHeader(Transform root) {
        Transform card = FindDeep(root, "Main Menu Card");
        if (card == null)
            return;
        bool removed = false;
        foreach (string objectName in HeaderObjects) {
            Transform header = card.Find(objectName);
            if (header == null)
                continue;
            Object.DestroyImmediate(header.gameObject);
            removed = true;
        }
        if (!removed)
            return;
        var cardRect = (RectTransform)card;
        cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, cardRect.sizeDelta.y - MainMenuTiles.HeaderRemoved);
        foreach (Transform child in card) {
            if (!(child is RectTransform rect) || rect.anchorMin != rect.anchorMax)
                continue; // hanya objek yang diposisikan dari atas (Place), bukan outline yang stretch
            rect.anchoredPosition += new Vector2(0f, MainMenuTiles.HeaderRemoved);
        }
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
