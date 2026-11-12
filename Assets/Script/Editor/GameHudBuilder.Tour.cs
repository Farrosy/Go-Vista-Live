using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Bagian HUD untuk penunjuk arah quest dan notifikasi quest:
///  - Marker tujuan quest di minimap (titik kalau dekat, panah di tepi kalau jauh) + jarak dalam meter.
///  - Toast "Quest baru!" / "Quest selesai!" (+ hadiah koin) di tengah atas.
public static partial class GameHudBuilder {
    private const float WaypointEdgeRadius = 104f;
    private const float WaypointWorldRadius = 50f;
    private const float WaypointArrowSize = 34f;
    private const float WaypointDotSize = 22f;
    private const float ToastWidth = 560f;
    private const float ToastTop = 96f;

    private static readonly Color WaypointGold = new Color32(255, 204, 64, 255);

    private static void BuildQuestWaypoint(RectTransform minimap, Fonts fonts) {
        RectTransform root = Stretch(NewRect("Quest Waypoint", minimap));

        RectTransform marker = Center(NewRect("Marker", root), 0f, 0f, WaypointArrowSize, WaypointArrowSize);
        Image arrow = PlainImage("Arrow", marker, "Icons/icon_arrow_up.png");
        Stretch(arrow.rectTransform);
        arrow.color = WaypointGold;
        Image dot = NewRect("Dot", marker).gameObject.AddComponent<Image>();
        dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        dot.color = WaypointGold;
        dot.raycastTarget = false;
        Center(dot.rectTransform, 0f, 0f, WaypointDotSize, WaypointDotSize);
        dot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        TextMeshProUGUI distance = Text("Distance", root, fonts.Bold, "0 m", 22f, WaypointGold, TextAlignmentOptions.Midline);
        distance.fontSharedMaterial = fonts.BoldUnderlay;
        distance.rectTransform.anchorMin = distance.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        distance.rectTransform.pivot = new Vector2(0.5f, 0f);
        distance.rectTransform.anchoredPosition = new Vector2(0f, 26f);
        distance.rectTransform.sizeDelta = new Vector2(140f, 30f);

        var view = root.gameObject.AddComponent<QuestWaypointMarker>();
        Bind(view, ("marker", marker), ("arrow", arrow.rectTransform), ("dot", dot.gameObject), ("distanceLabel", distance));
        SetFloat(view, "worldRadius", WaypointWorldRadius);
        SetFloat(view, "edgeRadius", WaypointEdgeRadius);
    }

    private static void BuildQuestToast(RectTransform safeArea, Fonts fonts) {
        Image pill = SlicedImage("Quest Toast", safeArea, "Panels/pill_toast.png");
        RectTransform rect = pill.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -ToastTop);
        rect.sizeDelta = new Vector2(ToastWidth, 130f);
        var group = pill.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        TextMeshProUGUI title = Text("Title", rect, fonts.Bold, "Quest selesai!", 30f, WaypointGold, TextAlignmentOptions.Midline);
        title.fontSharedMaterial = fonts.BoldUnderlay;
        TopLeft(title.rectTransform, 20f, 14f, ToastWidth - 40f, 40f);
        TextMeshProUGUI detail = Text("Detail", rect, fonts.Medium, "Tur Kota Go Vista", 24f, Color.white, TextAlignmentOptions.Top);
        detail.textWrappingMode = TextWrappingModes.Normal;
        TopLeft(detail.rectTransform, 20f, 56f, ToastWidth - 40f, 64f);

        var view = pill.gameObject.AddComponent<QuestToastView>();
        Bind(view, ("toast", group), ("titleLabel", title), ("detailLabel", detail));
    }
}
