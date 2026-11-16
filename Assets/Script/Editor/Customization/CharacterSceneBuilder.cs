using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Membuat scene KARAKTER (CharacterSceneLoader.ScenePath) SEKALI, lalu mendaftarkannya ke Build Profiles.
/// Isi scene:
///  - "Character Scene": CharacterSceneUI (pengendali, font & ikon terisi) + Canvas full-screen (sorting 60).
///  - "Preview Stage": kamera + lampu + meja putar di y -2000 (panggung PROFIL di y -1000, jadi tidak bentrok).
/// Tanpa EventSystem dan tanpa kamera utama: scene dimuat additive di atas lobby.
///
/// Setelah dibuat, scene ini aset biasa: atur framing kamera/lampu langsung di scene. Menjalankan menu
/// lagi hanya melengkapi yang kurang (Build Profiles, font/ikon), tidak menimpa scene.
/// </summary>
public static class CharacterSceneBuilder {
    private const int CanvasSortingOrder = 60; // di atas LobbyCanvas (50) & chat (52), di bawah login (100)
    private static readonly Vector3 StagePosition = new Vector3(0f, -2000f, 0f);
    private static readonly Vector2Int TextureSize = new Vector2Int(1024, 1280); // = CustomizerPickView.PreviewAspect

    [MenuItem("Go Vista/Customization/Buat CharacterScene")]
    public static void Run() {
        string path = CharacterSceneLoader.ScenePath;
        string result = File.Exists(path) ? UpdateAssets(path) : Create(path);
        AddToBuildSettings(path);
        Debug.Log($"[CharacterScene] {path}: {result}. Terdaftar di Build Profiles.");
    }

    private static string Create(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try {
            var root = new GameObject("Character Scene", typeof(CharacterSceneUI));
            SceneManager.MoveGameObjectToScene(root, scene);
            BuildCanvas(root.transform);
            CharacterSelectBuilder.BuildStage(null, StagePosition, TextureSize, out CharacterPreviewStage stage);
            SceneManager.MoveGameObjectToScene(stage.gameObject, scene);

            var so = new SerializedObject(root.GetComponent<CharacterSceneUI>());
            so.FindProperty("stage").objectReferenceValue = stage;
            ChatSceneInstaller.AssignUiAssets(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, path);
            return "dibuat";
        } finally {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildCanvas(Transform parent) {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// Scene sudah ada: hanya isi ulang font & ikon pengendali (tanpa mengubah tata letak scene).
    private static string UpdateAssets(string path) {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try {
            CharacterSceneUI ui = scene.GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<CharacterSceneUI>(true)).FirstOrDefault(c => c != null);
            if (ui == null) return "CharacterSceneUI tidak ditemukan (hapus scene lalu jalankan lagi untuk membuat ulang)";
            var so = new SerializedObject(ui);
            ChatSceneInstaller.AssignUiAssets(so);
            var stage = so.FindProperty("stage").objectReferenceValue as CharacterPreviewStage;
            if (stage != null) CharacterSelectBuilder.EnsureFloor(stage.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "sudah ada, font, ikon & alas diperbarui";
        } finally {
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AddToBuildSettings(string path) {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
