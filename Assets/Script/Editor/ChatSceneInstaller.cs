using System.Reflection;
using Normal.Realtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Memasang sistem chat ke MainMultiplayerScene (sekali jalan, aman diulang):
///  - "Chat Room" : RealtimeView SCENE + ChatRoom (datastore chat milik room, tidak ikut avatar).
///  - "Chat UI"   : ChatPanel, aset (font, ikon) diisi otomatis.
/// Menu: Go Vista > Chat > Pasang Chat di MainMultiplayerScene.
public static class ChatSceneInstaller {
    private const string ScenePath = "Assets/Scenes/MainMultiplayerScene.unity";
    private const string ChatRoomObjectName = "Chat Room";
    private const string ChatUiObjectName = "Chat UI";
    private const string FontPath = "Assets/Fonts/Fredoka-SemiBold SDF.asset";
    private const string ChatIconPath = "Assets/Sprites/Chat/icon-chat-bubble@2x.png";
    private const string SendPath = "Assets/Sprites/Chat/btn-send@2x.png";
    private const string SendPressedPath = "Assets/Sprites/Chat/btn-send-pressed@2x.png";
    private const string RoundedPath = "Assets/Resources/UI/ui-rounded.png";
    private const string CirclePath = "Assets/Resources/UI/ui-circle.png";
    private const string ChevronPath = "Assets/Resources/UI/Icons/icon-chevron-right.png";
    private const string FriendsIconPath = "Assets/Resources/UI/Icons/icon-friends.png";
    private const string UserPlusIconPath = "Assets/Resources/UI/Icons/icon-user-plus.png";
    private const string CloseIconPath = "Assets/Resources/UI/Icons/icon-x.png";
    private const string CheckIconPath = "Assets/Resources/UI/Icons/icon-check.png";
    private const string RefreshIconPath = "Assets/Resources/UI/Icons/icon-refresh.png";
    private const string FriendsUiObjectName = "Friends UI";
    private const string ProfileUiObjectName = "Profile UI";

    [MenuItem("Go Vista/Chat/Pasang Chat di MainMultiplayerScene")]
    public static void Install() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) {
            Debug.LogError($"[Chat] Scene tidak ditemukan: {ScenePath}");
            return;
        }

        ChatRoom room = EnsureChatRoom(scene);
        EnsureChatUi(scene, room);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Chat] Chat terpasang di MainMultiplayerScene. Cek Inspector \"Chat Room\": daftar Components " +
                  "RealtimeView harus berisi ChatRoom.", room);
    }

    [MenuItem("Go Vista/Friends/Pasang Friends di MainMultiplayerScene")]
    public static void InstallFriends() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        // Tile TEMAN di menu utama (LobbyCanvas.prefab) dulu, baru object Friends UI di scene.
        FriendsMainMenuPatcher.EnsureFriendsTile();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) {
            Debug.LogError($"[Friends] Scene tidak ditemukan: {ScenePath}");
            return;
        }
        FriendsMenu menu = FindInScene<FriendsMenu>(scene);
        if (menu == null) {
            var uiObject = new GameObject(FriendsUiObjectName);
            SceneManager.MoveGameObjectToScene(uiObject, scene);
            Undo.RegisterCreatedObjectUndo(uiObject, "Pasang Friends UI");
            menu = uiObject.AddComponent<FriendsMenu>();
        }
        var so = new SerializedObject(menu);
        AssignUiAssets(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Friends] Friends UI terpasang di MainMultiplayerScene.", menu);
    }

    /// Menu PROFIL: rapikan menu utama (tile PROFIL, tanpa sapaan) lalu pasang object Profile UI.
    [MenuItem("Go Vista/Profile/Pasang Profile di MainMultiplayerScene")]
    public static void InstallProfile() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        FriendsMainMenuPatcher.PatchMainMenu();
        Debug.Log("[Profile] CharacterSelectCanvas.prefab: " + CharacterSelectBuilder.RemoveLegacyUiFromPrefab());
        AssignCharacterSelectAssets();
        // Fase C: info akun & keluar akun pindah ke PROFIL; panel lobby & pengaturan memakai desain 08a/08b.
        Debug.Log("[Profile] SettingsPanel.prefab: " + SettingsPanelRedesign.RemoveAccountRow());
        LobyUISceneController.RebuildLobbyPanelInPrefab();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) {
            Debug.LogError($"[Profile] Scene tidak ditemukan: {ScenePath}");
            return;
        }
        ProfileMenu menu = FindInScene<ProfileMenu>(scene);
        if (menu == null) {
            var uiObject = new GameObject(ProfileUiObjectName);
            SceneManager.MoveGameObjectToScene(uiObject, scene);
            Undo.RegisterCreatedObjectUndo(uiObject, "Pasang Profile UI");
            menu = uiObject.AddComponent<ProfileMenu>();
        }
        var so = new SerializedObject(menu);
        AssignUiAssets(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Profile] Profile UI terpasang di MainMultiplayerScene.", menu);
    }

    /// Font & ikon untuk menu GANTI KARAKTER, diisi langsung di CharacterSelectCanvas.prefab (dipakai 2 scene lobby).
    private static void AssignCharacterSelectAssets() {
        const string prefabPath = "Assets/Prefab/CharacterSelectCanvas.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            CharacterSelectUI ui = root.GetComponent<CharacterSelectUI>();
            if (ui == null)
                return;
            var so = new SerializedObject(ui);
            AssignUiAssets(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Go Vista/Chat/Setup Semua (Emoji + Scene)")]
    public static void SetupAll() {
        ChatAssetSetup.SetupEmojiFont();
        Install();
        InstallFriends();
        InstallProfile();
    }

    private static ChatRoom EnsureChatRoom(Scene scene) {
        ChatRoom room = FindInScene<ChatRoom>(scene);
        if (room == null) {
            var roomObject = new GameObject(ChatRoomObjectName);
            SceneManager.MoveGameObjectToScene(roomObject, scene);
            Undo.RegisterCreatedObjectUndo(roomObject, "Pasang Chat Room");
            // RequireComponent pada ChatRoom ikut menambahkan RealtimeView.
            room = roomObject.AddComponent<ChatRoom>();
        }
        RealtimeView view = room.GetComponent<RealtimeView>();
        RefreshRealtimeView(view);
        EnableDestroyWhenLastClientLeaves(view);
        return room;
    }

    /// Riwayat chat dihapus server saat pemain terakhir keluar room (tidak menumpuk/membebani server).
    private static void EnableDestroyWhenLastClientLeaves(RealtimeView view) {
        if (view == null)
            return;
        var so = new SerializedObject(view);
        SerializedProperty flag = so.FindProperty("_sceneViewDestroyWhenLastClientLeaves");
        if (flag == null) {
            Debug.LogWarning("[Chat] Opsi 'Destroy When Last Client Leaves' tidak ditemukan di RealtimeView. " +
                             "Centang manual di Inspector \"Chat Room\".", view);
            return;
        }
        flag.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// RealtimeView menyusun daftar komponennya (dan UUID scene view) di OnValidate. Panggil langsung
    /// supaya ChatRoom langsung terdaftar tanpa harus menyentuh Inspector.
    private static void RefreshRealtimeView(RealtimeView view) {
        if (view == null)
            return;
        MethodInfo onValidate = typeof(RealtimeView).GetMethod("OnValidate",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        onValidate?.Invoke(view, null);
        EditorUtility.SetDirty(view);
    }

    private static void EnsureChatUi(Scene scene, ChatRoom room) {
        ChatPanel panel = FindInScene<ChatPanel>(scene);
        if (panel == null) {
            var uiObject = new GameObject(ChatUiObjectName);
            SceneManager.MoveGameObjectToScene(uiObject, scene);
            Undo.RegisterCreatedObjectUndo(uiObject, "Pasang Chat UI");
            panel = uiObject.AddComponent<ChatPanel>();
        }

        var so = new SerializedObject(panel);
        so.FindProperty("chatRoom").objectReferenceValue = room;
        AssignUiAssets(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Isi field "assets" (ChatUiAssets) milik ChatPanel / FriendsMenu.
    private static void AssignUiAssets(SerializedObject so) {
        SetAsset(so, "assets.font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), FontPath);
        SetAsset(so, "assets.chatIcon", LoadSprite(ChatIconPath), ChatIconPath);
        SetAsset(so, "assets.sendButton", LoadSprite(SendPath), SendPath);
        SetAsset(so, "assets.sendButtonPressed", LoadSprite(SendPressedPath), SendPressedPath);
        SetAsset(so, "assets.rounded", LoadSprite(RoundedPath), RoundedPath);
        SetAsset(so, "assets.circle", LoadSprite(CirclePath), CirclePath);
        SetAsset(so, "assets.chevron", LoadSprite(ChevronPath), ChevronPath);
        SetAsset(so, "assets.friendsIcon", LoadSprite(FriendsIconPath), FriendsIconPath);
        SetAsset(so, "assets.userPlusIcon", LoadSprite(UserPlusIconPath), UserPlusIconPath);
        SetAsset(so, "assets.closeIcon", LoadSprite(CloseIconPath), CloseIconPath);
        SetAsset(so, "assets.checkIcon", LoadSprite(CheckIconPath), CheckIconPath);
        SetAsset(so, "assets.refreshIcon", LoadSprite(RefreshIconPath), RefreshIconPath);
    }

    private static void SetAsset(SerializedObject so, string property, Object value, string path) {
        if (value == null)
            Debug.LogWarning($"[Chat] Aset tidak ditemukan: {path}. Kalau ikon chat, klik kanan Assets/Sprites/Chat > Reimport.");
        so.FindProperty(property).objectReferenceValue = value;
    }

    private static Sprite LoadSprite(string path) {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static T FindInScene<T>(Scene scene) where T : Component {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }
}
