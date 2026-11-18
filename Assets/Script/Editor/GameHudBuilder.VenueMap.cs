using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Bagian HUD untuk peta venue KPOP Arena / KPOP Community (memory: venue-map-decisions):
///  - label "BALKON" di dalam radar (GoVistaMinimap.levelBadge),
///  - lapisan area di peta penuh (VenueMapOverlay): nama area, sorotan, garis putus-putus balkon, tag BALKON.
public static partial class GameHudBuilder {
    private static readonly Color VenueGold = new Color32(255, 196, 61, 255);
    private static readonly Color VenueGoldText = new Color32(26, 20, 5, 255);
    private static readonly Color VenueHighlight = new Color32(54, 196, 186, 230);
    private static readonly Color VenueLabelGlass = new Color32(10, 16, 30, 205);

    /// Label emas di bagian bawah lingkaran radar. Anak radar, jadi ikut tersembunyi saat radar tidak tampil.
    private static GameObject BuildRadarLevelBadge(RectTransform radar, Fonts fonts) {
        RectTransform chip = BuildChip("Level Badge", radar, fonts, "BALKON", 17f, VenueGold, VenueGoldText, 6f,
            new RectOffset(12, 12, 3, 5));
        chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0f);
        chip.pivot = new Vector2(0.5f, 0f);
        chip.anchoredPosition = new Vector2(0f, 14f); // di bawah panah pemain (tengah radar)
        chip.gameObject.SetActive(false);
        return chip.gameObject;
    }

    private static VenueMapOverlay BuildVenueOverlay(RectTransform overlay, Fonts fonts) {
        RectTransform layer = Stretch(NewRect("Venue Areas", overlay));

        Image outline = MapSliced("Upper Outline Template", layer, "ui_dashed_outline.png", VenueGold);
        outline.type = Image.Type.Tiled;
        outline.pixelsPerUnitMultiplier = 0.6f; // garis lebih tebal dan putus-putus lebih panjang
        Image outlineFill = NewRect("Fill", outline.rectTransform).gameObject.AddComponent<Image>();
        outlineFill.color = new Color(VenueGold.r, VenueGold.g, VenueGold.b, 0.05f);
        outlineFill.raycastTarget = false;
        Stretch(outlineFill.rectTransform);
        Center(outline.rectTransform, 0f, 0f, 100f, 100f);

        Image highlight = NewRect("Highlight", layer).gameObject.AddComponent<Image>();
        highlight.sprite = MapSprite("ui_circle.png");
        highlight.color = new Color(VenueHighlight.r, VenueHighlight.g, VenueHighlight.b, 0.28f);
        highlight.raycastTarget = false;
        Center(highlight.rectTransform, 0f, 0f, 100f, 100f);
        Image ring = NewRect("Ring", highlight.rectTransform).gameObject.AddComponent<Image>();
        ring.sprite = MapSprite("ui_ring.png");
        ring.color = VenueHighlight;
        ring.raycastTarget = false;
        Stretch(ring.rectTransform);

        RectTransform label = BuildChip("Area Label Template", layer, fonts, "Area", 24f, VenueLabelGlass, Color.white, 4f);
        Image dot = NewRect("Dot", label).gameObject.AddComponent<Image>();
        dot.sprite = MapSprite("ui_circle.png");
        dot.raycastTarget = false;
        dot.transform.SetAsFirstSibling();
        var dotLayout = dot.gameObject.AddComponent<LayoutElement>();
        dotLayout.preferredWidth = dotLayout.preferredHeight = 16f;
        Center(label, 0f, 0f, 160f, 44f);

        RectTransform tag = BuildChip("Level Tag", layer, fonts, "BALKON", 22f, VenueGold, VenueGoldText, 8f);
        Center(tag, 0f, 0f, 120f, 40f);

        var view = layer.gameObject.AddComponent<VenueMapOverlay>();
        Bind(view, ("labelTemplate", label), ("outlineTemplate", outline.rectTransform), ("highlight", highlight),
            ("levelTag", tag));
        return view;
    }

    /// Chip bulat yang lebarnya mengikuti teks ("Label").
    private static RectTransform BuildChip(string name, Transform parent, Fonts fonts, string text, float size,
        Color background, Color textColor, float spacing, RectOffset padding = null) {
        Image chip = MapRounded(name, parent, background);
        chip.pixelsPerUnitMultiplier = 1.6f;
        var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = padding ?? new RectOffset(18, 18, 6, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI label = Text("Label", chip.rectTransform, fonts.Bold, text, size, textColor, TextAlignmentOptions.Center);
        label.characterSpacing = spacing;
        label.raycastTarget = false;
        return chip.rectTransform;
    }
}
