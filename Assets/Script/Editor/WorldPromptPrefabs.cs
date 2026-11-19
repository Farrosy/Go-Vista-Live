using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Membangun ulang prefab InteractPrompt (Assets/MobilePrefabs) bergaya HUD Go Vista (kaca navy + emas).
/// Dipakai SEMUA Interactable (NPC, kursi, mobil, pintu). Root, GUID, dan komponen InteractPromptWorld
/// dipertahankan; isi anak digambar ulang.
internal static class WorldPromptPrefabs
{
    public const string PromptPath = "Assets/MobilePrefabs/InteractPrompt.prefab";
    private const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee"; // LiberationSans SDF (sama dengan prompt lama)

    private static readonly Color Glass = new Color32(13, 22, 46, 248);
    private static readonly Color GlassLine = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color Gold = new Color32(255, 196, 61, 255);
    private static readonly Color GoldInk = new Color32(26, 20, 5, 255);
    private static readonly Color White = Color.white;

    // ------------------------------------------------------------------ InteractPrompt

    public static void RebuildInteractPrompt()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PromptPath);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(440f, 160f);
            rootRect.pivot = new Vector2(0.5f, 0f);   // ujung segitiga = titik objek
            rootRect.localScale = Vector3.one * 0.0075f;

            Pointer(rootRect, Glass, 18f);
            RectTransform card = Card(rootRect, "Card", Glass, 18f, 26f, 14, 30, 18);
            Outline(card, GlassLine, 26f);

            RectTransform badge = Badge(card, "Badge", Gold, 74f);
            Image icon = Icon(badge, "Icon", GoldInk, 46f, null);

            RectTransform column = Column(card, "Texts");
            TMP_Text nameLabel = Label(column, "NameLabel", "Name", 34f, FontStyles.Bold, White);
            TMP_Text verbLabel = Label(column, "VerbLabel", "Action", 26f, FontStyles.Bold, Gold);

            var prompt = root.GetComponent<InteractPromptWorld>();
            var so = new SerializedObject(prompt);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("verbLabel").objectReferenceValue = verbLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PromptPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ bagian

    /// Kartu yang melebar mengikuti isi (HorizontalLayoutGroup + ContentSizeFitter), berdiri di atas y = bottom.
    private static RectTransform Card(RectTransform parent, string name, Color color, float bottom, float radius,
                                      int padY, int padRight, int spacing)
    {
        RectTransform card = NewRect(name, parent);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = new Vector2(0f, bottom - parent.sizeDelta.y * 0.5f);
        Rounded(card.gameObject, color, radius);

        var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padY, padRight, padY, padY);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return card;
    }

    private static void Outline(RectTransform card, Color color, float radius)
    {
        RectTransform outline = NewRect("Outline", card);
        outline.anchorMin = Vector2.zero;
        outline.anchorMax = Vector2.one;
        outline.offsetMin = outline.offsetMax = Vector2.zero;
        outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Image image = Rounded(outline.gameObject, color, radius, "ui-outline");
        image.raycastTarget = false;
    }

    /// Segitiga kecil (kotak diputar 45°) di bawah kartu, menunjuk ke objek.
    private static void Pointer(RectTransform parent, Color color, float y)
    {
        RectTransform tail = NewRect("Pointer", parent);
        tail.anchorMin = tail.anchorMax = tail.pivot = new Vector2(0.5f, 0.5f);
        tail.sizeDelta = new Vector2(26f, 26f);
        tail.anchoredPosition = new Vector2(0f, y + 2f - parent.sizeDelta.y * 0.5f);
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Rounded(tail.gameObject, color, 6f);
    }

    private static RectTransform Badge(RectTransform parent, string name, Color color, float size)
    {
        RectTransform badge = NewRect(name, parent);
        var element = badge.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.preferredHeight = element.minWidth = element.minHeight = size;
        Image image = badge.gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/ui-circle");
        image.color = color;
        image.raycastTarget = false;
        return badge;
    }

    private static Image Icon(RectTransform parent, string name, Color color, float size, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Column(RectTransform parent, string name)
    {
        RectTransform column = NewRect(name, parent);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = -4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return column;
    }

    private static TMP_Text Label(RectTransform parent, string name, string text, float size, FontStyles style,
                                  Color color)
    {
        RectTransform rect = NewRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static Image Rounded(GameObject target, Color color, float radius, string sprite = "ui-rounded")
    {
        Image image = target.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/" + sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 20f / Mathf.Max(1f, radius);   // sprite border = 20 px
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }
}
