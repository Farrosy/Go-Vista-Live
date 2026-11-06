using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teks UI yang fleksibel: ukuran font boleh MENGECIL otomatis (sampai MinRatio dari ukuran desain) kalau
/// teks tidak muat di kotaknya, misalnya label tile "PENGATURAN" di HP berlayar kecil. Ukuran desain tetap
/// jadi ukuran maksimal, jadi di layar besar tampilannya tidak berubah.
///
/// Legacy Text: Best Fit hanya mengecilkan kalau tingginya dibatasi, jadi overflow diset Wrap + Truncate.
/// Teks yang ukurannya diatur layout group / ContentSizeFitter, dan teks di dalam kolom input, dilewati.
/// Dipakai oleh menu "Go Vista > UI > Terapkan Teks Fleksibel" (prefab & scene) dan pembuat UI lewat kode.
/// </summary>
public static class UiTextFit {
    public const float MinRatio = 0.6f;
    private const int SmallestFontSize = 12;

    /// <summary>Terapkan ke satu Text. Mengembalikan true kalau ada yang diubah.</summary>
    public static bool Apply(Text text) {
        if (text == null || !ShouldFit(text.rectTransform, text.GetComponentInParent<InputField>(true) != null))
            return false;
        int max = text.resizeTextForBestFit ? Mathf.Max(text.resizeTextMaxSize, text.fontSize) : text.fontSize;
        int min = Mathf.Max(SmallestFontSize, Mathf.RoundToInt(max * MinRatio));
        bool changed = !text.resizeTextForBestFit || text.resizeTextMaxSize != max || text.resizeTextMinSize != min ||
                       text.horizontalOverflow != HorizontalWrapMode.Wrap || text.verticalOverflow != VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = max;
        text.resizeTextMinSize = min;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return changed;
    }

    /// <summary>Terapkan ke satu teks TextMeshPro (Auto Size). Mengembalikan true kalau ada yang diubah.</summary>
    public static bool Apply(TMP_Text text) {
        if (text == null || !ShouldFit(text.rectTransform, text.GetComponentInParent<TMP_InputField>(true) != null))
            return false;
        float max = text.enableAutoSizing ? Mathf.Max(text.fontSizeMax, text.fontSize) : text.fontSize;
        float min = Mathf.Max(SmallestFontSize, Mathf.Round(max * MinRatio));
        bool changed = !text.enableAutoSizing || !Mathf.Approximately(text.fontSizeMax, max) ||
                       !Mathf.Approximately(text.fontSizeMin, min);
        text.enableAutoSizing = true;
        text.fontSizeMax = max;
        text.fontSizeMin = min;
        return changed;
    }

    /// <summary>Terapkan ke semua teks di bawah root (termasuk yang nonaktif). Mengembalikan jumlah teks yang diubah.</summary>
    public static int ApplyAll(Transform root) {
        int changed = 0;
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            if (Apply(text)) changed++;
        // Hanya TMP UI (TextMeshProUGUI); TextMeshPro 3D (papan nama di world) tidak disentuh.
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (Apply(text)) changed++;
        return changed;
    }

    /// <summary>
    /// Lewati teks yang ukurannya bukan dari RectTransform-nya sendiri: diatur layout group (lebar mengikuti
    /// teks, mis. label KELUAR GAME), ContentSizeFitter, atau teks/placeholder kolom input.
    /// </summary>
    private static bool ShouldFit(RectTransform rect, bool insideInput) {
        if (insideInput || rect.GetComponent<ContentSizeFitter>() != null || rect.GetComponent<LayoutElement>() != null)
            return false;
        Transform parent = rect.parent;
        if (parent != null) {
            var group = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group != null && (group.childControlWidth || group.childControlHeight))
                return false;
            if (parent.GetComponent<GridLayoutGroup>() != null)
                return false;
        }
        return true;
    }
}
