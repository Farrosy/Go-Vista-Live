using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Membersihkan override prefab yang tidak disengaja di scene yang sedang terbuka (mis. MainMultiplayerScene):
///  1. Override yatim: menunjuk ke objek/komponen yang sudah tidak ada di prefab (sisa UI lama).
///  2. Hanya di MainMultiplayerScene (bukan scene edit): override "noise" di canvas UI lobby yang sumber
///     kebenarannya prefab ("1 edit scene = 1 prefab"):
///     posisi/ukuran RectTransform (dulu ditulis SafeAreaFitter/ResponsivePanel di edit mode), nilai cache
///     TextMeshPro (m_TextStyleHashCode, m_fontColor32), dan m_AdditionalShaderChannelsFlag Canvas.
/// Override lain (nama, referensi, nilai komponen) tidak disentuh.
///
/// Berjalan OTOMATIS setiap scene disimpan (Ctrl+S) lewat hook sceneSaving, jadi file scene selalu bersih
/// tanpa langkah manual. Menu di bawah hanya untuk membersihkan scene terbuka tanpa menyimpan.
/// </summary>
[InitializeOnLoad]
public static class SceneOverrideCleaner {
    static SceneOverrideCleaner() {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path) {
        // Noise UI tidak dilaporkan: sebagian hanya nilai sementara di memori (canvas belum aktif saat scene
        // dimuat) yang memang tidak pernah tertulis ke file. Override yatim selalu perubahan nyata.
        (int orphans, _) = Clean(scene);
        if (orphans > 0)
            Debug.Log($"[SceneCleaner] {scene.name}: {orphans} override yatim (objek sudah tidak ada di prefab) dibuang saat simpan.");
    }

    /// Canvas UI yang layout-nya diedit di prefab/edit scene masing-masing, bukan di scene pemakai.
    private static readonly HashSet<string> UiCanvasPrefabs = new HashSet<string> {
        "Assets/Prefab/LoginCanvas.prefab",
        "Assets/Prefab/LobbyCanvas.prefab",
        "Assets/Prefab/LobbyReturnCanvas.prefab",
        "Assets/Prefab/RoomWaitingCanvas.prefab",
        "Assets/Prefab/GameHudCanvas.prefab",
        "Assets/Prefab/LobbyModalCanvas.prefab",
    };

    public static IEnumerable<string> UiCanvasPrefabPaths => UiCanvasPrefabs;

    /// Scene PEMAKAI canvas: hanya di sini override layout dianggap noise. Scene edit (LobyUIScene,
    /// LoginUIScene, RoomWaitingScene, LobbyModalScene) sengaja TIDAK termasuk: di sana override adalah
    /// editan layout-mu yang di-Apply ke prefab saat Ctrl+S (LobbyUIEditScenes / LoginCanvasBuilder).
    private static readonly HashSet<string> HostScenes = new HashSet<string> {
        "Assets/Scenes/MainMultiplayerScene.unity",
    };

    private static readonly string[] NoiseProperties = {
        "m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition", "m_SizeDelta", "m_Pivot",
        "m_LocalScale", "m_LocalPosition", "m_LocalRotation", "m_LocalEulerAnglesHint",
        "m_TextStyleHashCode", "m_fontColor32", "m_AdditionalShaderChannelsFlag",
    };

    [MenuItem("Go Vista/Scene/Bersihkan Override Prefab (scene terbuka)")]
    public static void CleanOpenScenesFromMenu() => Debug.Log("[SceneCleaner] " + CleanOpenScenes());

    public static string CleanOpenScenes() {
        var report = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            (int orphans, int noise) = Clean(scene);
            if (orphans + noise > 0) EditorSceneManager.MarkSceneDirty(scene);
            report.Add($"{scene.name}: {orphans} override yatim + {noise} override noise UI dihapus");
        }
        return string.Join("\n", report);
    }

    private static (int orphans, int noise) Clean(Scene scene) {
        int orphans = 0, noise = 0;
        bool hostScene = HostScenes.Contains(scene.path);
        foreach (GameObject root in OutermostInstances(scene)) {
            PropertyModification[] mods = PrefabUtility.GetPropertyModifications(root);
            if (mods == null) continue;
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            bool uiCanvas = hostScene && UiCanvasPrefabs.Contains(assetPath);

            var kept = new List<PropertyModification>(mods.Length);
            foreach (PropertyModification mod in mods) {
                if (mod.target == null) { orphans++; continue; }
                if (uiCanvas && IsNoise(mod)) { noise++; continue; }
                kept.Add(mod);
            }
            if (kept.Count == mods.Length) continue;
            PrefabUtility.SetPropertyModifications(root, kept.ToArray());
        }
        return (orphans, noise);
    }

    /// Properti driven (LayoutGroup, SafeAreaFitter, ResponsivePanel, AspectRatioFitter) dibiarkan: Unity
    /// mengelolanya sendiri dan menyimpannya sebagai placeholder tetap, jadi tidak membuat scene berubah.
    private static bool IsNoise(PropertyModification mod) {
        if (mod.target is RectTransform rect && rect.drivenByObject != null) return false;
        string path = mod.propertyPath;
        return NoiseProperties.Any(p => path == p || path.StartsWith(p + "."));
    }

    private static IEnumerable<GameObject> OutermostInstances(Scene scene) {
        var result = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects()) Collect(root.transform, result);
        return result;
    }

    private static void Collect(Transform node, List<GameObject> result) {
        if (PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject)) {
            result.Add(node.gameObject);
            return; // prefab bersarang ikut dibersihkan lewat modifikasi instance terluar
        }
        foreach (Transform child in node) Collect(child, result);
    }
}
