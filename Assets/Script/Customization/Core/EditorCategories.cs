using System.Collections.Generic;

public enum EditorCategoryKind { Skin, Face, Outfits, Stickers }

/// <summary>Isi panel kanan untuk satu sub-tab.</summary>
public enum EditorPanelKind {
    /// <summary>Grid item outfit + semua bagian warna (+ motif kalau outfit mendukung).</summary>
    Outfits,
    /// <summary>Pilihan warna kulit + bagian warna outfit Body.</summary>
    Skin,
    /// <summary>Slider bentuk wajah (blendshape kepala).</summary>
    FaceShapes,
    /// <summary>Grid ekspresi wajah bernomor (FaceExpressions), tanpa bagian warna.</summary>
    Expressions,
    /// <summary>Stiker (decal BoZo): pilih, posisi preset, ukuran, putar, warna.</summary>
    Stickers
}

/// <summary>
/// Satu sub-tab di panel kanan editor. Sub-tab outfit bisa berisi beberapa tipe outfit BoZo (nama folder
/// Resources), mis. "Alis &amp; Bulu Mata" = EyeBrows + EyeLashes; grid-nya dibagi per tipe.
/// </summary>
public readonly struct EditorSubTab {
    public readonly string Label;
    public readonly EditorPanelKind Panel;
    public readonly IReadOnlyList<string> OutfitTypes;

    public EditorSubTab(string label, EditorPanelKind panel, params string[] outfitTypes) {
        Label = label;
        Panel = panel;
        OutfitTypes = outfitTypes ?? new string[0];
    }

    /// <summary>Sub-tab outfit biasa (satu atau beberapa tipe).</summary>
    public EditorSubTab(string label, params string[] outfitTypes) : this(label, EditorPanelKind.Outfits, outfitTypes) { }

    /// <summary>Tipe pertama, atau kosong untuk sub-tab tanpa outfit (Bentuk, Stiker).</summary>
    public string OutfitType => OutfitTypes.Count > 0 ? OutfitTypes[0] : "";
}

/// <summary>Satu tab di rail kiri editor (mockup v2 layar 2–4).</summary>
public sealed class EditorCategory {
    public readonly string Label;
    /// <summary>Nama ikon di Resources/UI/Icons (tanpa ekstensi).</summary>
    public readonly string Icon;
    public readonly EditorCategoryKind Kind;
    public readonly IReadOnlyList<EditorSubTab> SubTabs;
    /// <summary>Kamera otomatis zoom ke wajah saat tab ini dibuka (Wajah, Rambut).</summary>
    public readonly bool FocusFace;

    public EditorCategory(string label, string icon, EditorCategoryKind kind, bool focusFace, params EditorSubTab[] subTabs) {
        Label = label;
        Icon = icon;
        Kind = kind;
        FocusFace = focusFace;
        SubTabs = subTabs;
    }
}

/// <summary>
/// Daftar tab editor: Kulit · Wajah · Rambut · Atasan · Bawahan · Kaki · Aksesori · Dalaman · Stiker.
/// </summary>
public static class EditorCategories {
    public static readonly IReadOnlyList<EditorCategory> All = new[] {
        new EditorCategory("Kulit", "icon-skin", EditorCategoryKind.Skin, false,
                           new EditorSubTab("Warna Kulit", EditorPanelKind.Skin, "Body")),
        new EditorCategory("Wajah", "icon-face", EditorCategoryKind.Face, true,
                           new EditorSubTab("Bentuk", EditorPanelKind.FaceShapes),
                           new EditorSubTab("Ekspresi", EditorPanelKind.Expressions),
                           new EditorSubTab("Kepala", "Head"),
                           new EditorSubTab("Mata", "Eyes", "Pupil", "EyeShine"),
                           new EditorSubTab("Alis & Bulu Mata", "EyeBrows", "EyeLashes"),
                           new EditorSubTab("Make-up", "MakeUpLips", "MakeUpCheeks", "MakeUpEyes", "FaceDetails")),
        new EditorCategory("Rambut", "icon-hair", EditorCategoryKind.Outfits, true,
                           new EditorSubTab("Depan", "HairFront"), new EditorSubTab("Belakang", "HairBack")),
        new EditorCategory("Atasan", "icon-shirt", EditorCategoryKind.Outfits, false,
                           new EditorSubTab("Atasan", "Top"), new EditorSubTab("Overall", "Overall")),
        new EditorCategory("Bawahan", "icon-pants", EditorCategoryKind.Outfits, false,
                           new EditorSubTab("Bawahan", "Bottom"), new EditorSubTab("Legging", "Leggings")),
        new EditorCategory("Kaki", "icon-shoe", EditorCategoryKind.Outfits, false,
                           new EditorSubTab("Sepatu", "Feet"), new EditorSubTab("Kaus Kaki", "Socks")),
        new EditorCategory("Aksesori", "icon-hat", EditorCategoryKind.Outfits, false,
                           new EditorSubTab("Topi", "Hat"), new EditorSubTab("Kacamata", "UpperFace"),
                           new EditorSubTab("Jenggot", "LowerFace"), new EditorSubTab("Sarung Tangan", "Gloves"),
                           new EditorSubTab("Kepala", "HeadAcc")),
        new EditorCategory("Dalaman", "icon-under", EditorCategoryKind.Outfits, false,
                           new EditorSubTab("Atas", "UnderUpper"), new EditorSubTab("Bawah", "UnderLower")),
        new EditorCategory("Stiker", "icon-star", EditorCategoryKind.Stickers, false,
                           new EditorSubTab("Stiker", EditorPanelKind.Stickers)),
    };

    /// <returns>Kategori dengan label ini, atau null.</returns>
    public static EditorCategory Get(string label) {
        foreach (EditorCategory category in All) {
            if (category.Label == label) return category;
        }
        return null;
    }

    /// <summary>Label untuk tipe outfit ("Atasan · Overall", "Mata"), atau null kalau tipe ini tidak punya tab.</summary>
    public static string LabelOf(string outfitType) {
        foreach (EditorCategory category in All) {
            foreach (EditorSubTab tab in category.SubTabs) {
                if (!Contains(tab.OutfitTypes, outfitType)) continue;
                return category.Label == tab.Label || category.SubTabs.Count == 1 ? category.Label
                    : category.Label + " · " + tab.Label;
            }
        }
        return null;
    }

    private static bool Contains(IReadOnlyList<string> list, string value) {
        foreach (string item in list) {
            if (item == value) return true;
        }
        return false;
    }
}
