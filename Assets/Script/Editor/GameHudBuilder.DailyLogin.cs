using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Tombol "Hadiah" (HADIAH LOGIN HARIAN) di GameHudCanvas: ukuran & gaya sama dengan tombol Quest, ikon kalender,
/// tepat di bawah tombol Quest dengan jarak 16 px. Membuka lagi popup daily login (DailyLoginHudButton).
public static partial class GameHudBuilder {
    private const string DailyLoginButtonName = "Daily Login Button";
    private const float DailyLoginButtonGap = 16f;
    private const float DailyLoginButtonTop = QuestButtonTop + QuestButtonHeight + DailyLoginButtonGap;

    /// Pasang tombol Hadiah dan turunkan tombol Quest di GameHudCanvas.prefab yang sudah ada, tanpa membangun
    /// ulang seluruh HUD (bagian lain prefab tidak berubah). Aman diulang.
    [MenuItem("Go Vista/HUD/Pasang Tombol Hadiah Login")]
    public static void InstallDailyLoginButton() {
        Fonts fonts = EnsureFonts();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            RectTransform safeArea = FindRect(root.transform, "HUD Safe Area");
            if (safeArea == null) {
                Debug.LogWarning($"[HUD] \"HUD Safe Area\" tidak ada di {PrefabPath}. Jalankan Go Vista > HUD > Buat ulang GameHudCanvas.");
                return;
            }
            RectTransform quest = FindRect(safeArea, "Quest Button");
            if (quest != null) quest.anchoredPosition = new Vector2(-QuestButtonRight, -QuestButtonTop);

            Transform old = root.transform.Find(DailyLoginButtonName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform oldButton = safeArea.Find(DailyLoginButtonName);
            if (oldButton != null) Object.DestroyImmediate(oldButton.gameObject);

            BuildDailyLoginButton(root.transform, safeArea, fonts);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HUD] Tombol Hadiah Login dipasang, tombol Quest diturunkan.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildDailyLoginButton(Transform root, RectTransform safeArea, Fonts fonts) {
        RectTransform rect = NewRect(DailyLoginButtonName, safeArea);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-QuestButtonRight, -DailyLoginButtonTop);
        rect.sizeDelta = new Vector2(QuestButtonWidth, QuestButtonHeight);
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = HudSortingOrder;
        rect.gameObject.AddComponent<GraphicRaycaster>();

        Image background = SlicedImage("Background", rect, QuestFolder + "quest_button_bg.png");
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        Image icon = PlainImage("Icon", rect, "HadiahLogin/icon_login_calendar.png");
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(24f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
        icon.preserveAspect = true;

        TextMeshProUGUI label = Text("Label", rect, fonts.Bold, "Hadiah", 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform, 80f, 0f, 12f, 4f);

        Image badge = PlainImage("New Badge", rect, QuestFolder + "quest_badge.png");
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(2f, -2f);
        badge.rectTransform.sizeDelta = new Vector2(QuestBadgeSize, QuestBadgeSize);
        TextMeshProUGUI mark = Text("Mark", badge.rectTransform, fonts.Bold, "!", 28f, Color.white, TextAlignmentOptions.Midline);
        Stretch(mark.rectTransform, 0f, 0f, 0f, 2f);

        var view = rect.gameObject.AddComponent<DailyLoginHudButton>();
        Bind(view, ("buttonCanvas", canvas), ("openButton", button), ("badge", badge.gameObject));
    }

    private static RectTransform FindRect(Transform parent, string name) {
        foreach (RectTransform t in parent.GetComponentsInChildren<RectTransform>(true))
            if (t.name == name) return t;
        return null;
    }
}
