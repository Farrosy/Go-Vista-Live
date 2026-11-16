using System.Collections.Generic;

public enum EditorCategoryKind { Skin, Face, Outfits, Stickers }

/// <summary>Satu sub-tab di panel kanan editor: label + tipe outfit BoZo (nama folder Resources).</summary>
public readonly struct EditorSubTab {
    public readonly string Label;
    public readonly string OutfitType;

    public EditorSubTab(string label, string outfitType) {
        Label = label;
        OutfitType = outfitType;
    }
}

/// <summary>Satu tab di rail kiri editor (mockup v2 layar 2–4).</summary>
public sealed class EditorCategory {
    public readonly string Label;
    /// <summary>Nama ikon di Resources/UI/Icons (tanpa ekstensi).</summary>
    public readonly string Icon;
    public readonly EditorCategoryKind Kind;
    public readonly IReadOnlyList<EditorSubTab> SubTabs;

    public EditorCategory(string label, string icon, EditorCategoryKind kind, params EditorSubTab[] subTabs) {
        Label = label;
        Icon = icon;
        Kind = kind;
        SubTabs = subTabs;
    }
}

/// <summary>
/// Daftar tab editor: Kulit · Wajah · Rambut · Atasan · Bawahan · Kaki · Aksesori · Dalaman · Stiker.
/// Tab Outfits memetakan sub-tab ke tipe outfit BoZo; Kulit, Wajah, dan Stiker punya panel sendiri (1d).
/// </summary>
public static class EditorCategories {
    public static readonly IReadOnlyList<EditorCategory> All = new[] {
        new EditorCategory("Kulit", "icon-skin", EditorCategoryKind.Skin),
        new EditorCategory("Wajah", "icon-face", EditorCategoryKind.Face),
        new EditorCategory("Rambut", "icon-hair", EditorCategoryKind.Outfits,
                           new EditorSubTab("Depan", "HairFront"), new EditorSubTab("Belakang", "HairBack")),
        new EditorCategory("Atasan", "icon-shirt", EditorCategoryKind.Outfits,
                           new EditorSubTab("Atasan", "Top"), new EditorSubTab("Overall", "Overall")),
        new EditorCategory("Bawahan", "icon-pants", EditorCategoryKind.Outfits,
                           new EditorSubTab("Bawahan", "Bottom"), new EditorSubTab("Legging", "Leggings")),
        new EditorCategory("Kaki", "icon-shoe", EditorCategoryKind.Outfits,
                           new EditorSubTab("Sepatu", "Feet"), new EditorSubTab("Kaus Kaki", "Socks")),
        new EditorCategory("Aksesori", "icon-hat", EditorCategoryKind.Outfits,
                           new EditorSubTab("Topi", "Hat"), new EditorSubTab("Kacamata", "UpperFace"),
                           new EditorSubTab("Jenggot", "LowerFace"), new EditorSubTab("Sarung Tangan", "Gloves"),
                           new EditorSubTab("Kepala", "HeadAcc")),
        new EditorCategory("Dalaman", "icon-under", EditorCategoryKind.Outfits,
                           new EditorSubTab("Atas", "UnderUpper"), new EditorSubTab("Bawah", "UnderLower")),
        new EditorCategory("Stiker", "icon-star", EditorCategoryKind.Stickers),
    };

    /// <returns>Kategori dengan label ini, atau null.</returns>
    public static EditorCategory Get(string label) {
        foreach (EditorCategory category in All) {
            if (category.Label == label) return category;
        }
        return null;
    }
}
