using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Kotak dialog (NewDialougeRoot di MobileUI): warna kotak per maskot dan margin teks.
public static partial class CityTourBuilder {
    private const string DialoguePrefabPath = "Assets/MobilePrefabs/NewDialougeRoot.prefab";
    private const string DialogueSpriteFolder = "Assets/DialougeAssets/";
    // Teks dialog: jarak dari tepi area teks. Margin kanan dulu −1097 px sehingga kalimat panjang tidak pernah
    // turun baris dan keluar dari kotak.
    private static readonly Vector4 DialogueTextMargin = new Vector4(22f, 10f, 22f, 10f);

    private static void ConfigureDialogueBox() {
        GameObject root = PrefabUtility.LoadPrefabContents(DialoguePrefabPath);
        try {
            var ui = root.GetComponentInChildren<DialogueUI>(true);
            var serialized = new SerializedObject(ui);
            var text = serialized.FindProperty("dialogueText").objectReferenceValue as TMPro.TMP_Text;
            if (text != null) text.margin = DialogueTextMargin;

            Transform box = text != null ? text.transform.parent : null;
            serialized.FindProperty("boxImage").objectReferenceValue = box != null ? box.GetComponent<Image>() : null;
            SerializedProperty styles = serialized.FindProperty("speakerStyles");
            styles.arraySize = 2;
            SetStyle(styles.GetArrayElementAtIndex(0), CityTourContent.Vio, "dialoguebox_light_blue");
            SetStyle(styles.GetArrayElementAtIndex(1), CityTourContent.Vira, "dialoguebox_light_pink");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetStyle(SerializedProperty style, string speaker, string sprite) {
        style.FindPropertyRelative("speaker").stringValue = speaker;
        var asset = AssetDatabase.LoadAssetAtPath<Sprite>(DialogueSpriteFolder + sprite + ".png");
        if (asset == null) Debug.LogWarning($"[Tour] Sprite kotak dialog '{sprite}' tidak ditemukan.");
        style.FindPropertyRelative("box").objectReferenceValue = asset;
    }
}
