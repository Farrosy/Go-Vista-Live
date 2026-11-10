using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Menu "Go Vista > UI > Terapkan Teks Fleksibel": mengaktifkan auto-fit (UiTextFit) pada teks di semua prefab UI
/// game dan canvas di scene yang sedang terbuka, supaya teks mengecil sendiri kalau tidak muat di layar kecil.
/// Aset pihak ketiga (Download_Assets, Synty, Joystick Pack, TextMesh Pro) tidak disentuh. Aman diulang.
public static class UiTextFitTool {
    private static readonly string[] PrefabFolders = { "Assets/Prefab", "Assets/MobilePrefabs", "Assets/Resources/UI" };

    [MenuItem("Go Vista/UI/Terapkan Teks Fleksibel (Semua Canvas)")]
    public static void ApplyEverywhere() {
        int prefabTexts = ApplyToPrefabs(out List<string> touched);
        int sceneTexts = ApplyToOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[UI] Teks fleksibel diterapkan: {prefabTexts} teks di {touched.Count} prefab " +
                  $"({string.Join(", ", touched)}), {sceneTexts} teks di scene terbuka.");
    }

    private static int ApplyToPrefabs(out List<string> touched) {
        touched = new List<string>();
        int total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders)) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !HasText(asset))
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                int changed = UiTextFit.ApplyAll(root.transform);
                if (changed == 0)
                    continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                total += changed;
                touched.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return total;
    }

    /// Hanya teks yang BUKAN bagian prefab (prefab sudah diurus di atas, supaya tidak jadi override di scene).
    private static int ApplyToOpenScenes() {
        int total = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) {
                    if (PrefabUtility.IsPartOfPrefabInstance(canvas.gameObject))
                        continue;
                    changed += ApplyNonPrefab(canvas.transform);
                }
            }
            if (changed > 0) {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                total += changed;
            }
        }
        return total;
    }

    private static int ApplyNonPrefab(Transform canvasRoot) {
        int changed = 0;
        foreach (Text text in canvasRoot.GetComponentsInChildren<Text>(true)) {
            if (!PrefabUtility.IsPartOfPrefabInstance(text) && UiTextFit.Apply(text)) {
                EditorUtility.SetDirty(text);
                changed++;
            }
        }
        foreach (TMPro.TextMeshProUGUI text in canvasRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) {
            if (!PrefabUtility.IsPartOfPrefabInstance(text) && UiTextFit.Apply(text)) {
                EditorUtility.SetDirty(text);
                changed++;
            }
        }
        return changed;
    }

    /// Prefab UI saja: punya Canvas, atau root-nya RectTransform (mis. SettingsPanel). Prefab 3D (bangunan) dilewati.
    private static bool HasText(GameObject asset) {
        bool isUi = asset.GetComponentInChildren<Canvas>(true) != null || asset.transform is RectTransform;
        return isUi && (asset.GetComponentInChildren<Text>(true) != null ||
                        asset.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) != null);
    }
}
