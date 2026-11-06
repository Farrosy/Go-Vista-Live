using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Panel "GO VISTA LOBBY" (dibuka dari MAIN), desain Docs/Profil/08b-panel-lobby.png:
///  - kiri: CARI ROOM CEPAT, ROOM PRIVAT (nama + MASUK), BUAT ROOM, KODE ROOM (kode + GABUNG);
///  - kanan: ROOM TERAKHIR (nama, "Room privat · 2 jam lalu" / "Kode room · kemarin", MASUK);
///  - bawah: status. Chip akun & "Keluar akun" pindah ke menu PROFIL.
/// Nama objek ("Lobby ... Button/Input/Status") dipertahankan karena dicari lewat FindChild.
/// </summary>
public sealed partial class LobyUISceneController {
    private const string RecentRoomTimesKey = "LobyUIScene.RecentRoomTimes";
    private const float LobbyPanelWidth = 1680f;
    private const float LobbyPanelHeight = 800f;
    private const float LobbyPad = 54f;
    private const float LobbyLeftWidth = 900f;
    private const float LobbyRightX = 1006f;
    private const float LobbyRightWidth = LobbyPanelWidth - LobbyRightX - LobbyPad;
    private const float LobbyInputWidth = 704f;
    private const float LobbyActionX = LobbyPad + LobbyInputWidth + 16f;
    private const float LobbyActionWidth = LobbyLeftWidth - LobbyInputWidth - 16f;
    private const float RecentCardHeight = 86f;
    private const float RecentCardSpacing = 12f;

    private static readonly Color LobbyGreenTop = new Color32(52, 211, 153, 255);
    private static readonly Color LobbyGreenBottom = new Color32(16, 185, 129, 255);
    private static readonly Color LobbyGreenLip = new Color32(4, 108, 78, 255);
    private static readonly Color LobbyNeutralFill = new Color32(27, 36, 64, 255);
    private static readonly Color LobbyNeutralText = new Color32(205, 213, 232, 255);

    private static RectTransform BuildLobbyPanel(RectTransform safeArea) {
        RectTransform panel = CreateResponsivePanel("Lobby Panel", safeArea, new Vector2(LobbyPanelWidth, LobbyPanelHeight));
        AddShape(panel.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(panel, LineColor, cornerScale: 1.8f);
        Image accent = Place(CreateRect("Lobby Accent Line", panel, Vector2.zero), 80f, 0f, LobbyPanelWidth - 160f, 3f)
            .gameObject.AddComponent<Image>();
        accent.color = new Color(PlayTopColor.r, PlayTopColor.g, PlayTopColor.b, 0.8f);
        accent.raycastTarget = false;

        BuildLobbyHeader(panel);
        BuildLobbyLeftColumn(panel);
        BuildLobbyRightColumn(panel);
        BuildLobbyFooter(panel);
        return panel;
    }

    private static void BuildLobbyHeader(RectTransform panel) {
        RectTransform iconBox = Place(CreateRect("Lobby Title Icon", panel, Vector2.zero), LobbyPad, 46f, 68f, 68f);
        AddGradientShape(iconBox.gameObject, PlayTopColor, PlayBottomColor, cornerScale: 1.1f).raycastTarget = false;
        AddIcon("Lobby Title Icon Image", iconBox, "icon-play", Color.white, 18f, 16f, 36f);
        Place(CreateText("Lobby Title", panel, "GO VISTA LOBBY", 52, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 140f, 36f, 1000f, 64f);
        Place(CreateText("Lobby Subtitle", panel, "Cari room cepat, masuk room privat, atau gabung dengan kode.",
                         SmallFontSize, FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor)
                  .rectTransform, 140f, 98f, 1000f, 32f);
        Button close = CreateButton("Lobby Close Button", panel, "×", LobbyNeutralFill, 44);
        AddOutline((RectTransform)close.transform, LineColor);
        PlaceTopRight(close.GetComponent<RectTransform>(), LobbyPad, 48f, 64f, 64f);
    }

    private static void BuildLobbyLeftColumn(RectTransform panel) {
        Button quick = LobbyGradientButton("Lobby Quickmatch Button", panel, "CARI ROOM CEPAT", PlayTopColor,
                                           PlayBottomColor, PlayLipColor, 36);
        Place((RectTransform)quick.transform, LobbyPad, 152f, LobbyLeftWidth, 112f);

        SectionLabel(panel, "Room Name Label", "ROOM PRIVAT", 296f);
        Place(CreateInputField("Lobby Room Input", panel, "Nama room privat").GetComponent<RectTransform>(),
              LobbyPad, 330f, LobbyInputWidth, 72f);
        Button join = LobbyGradientButton("Lobby Join Button", panel, "MASUK", PlayTopColor, PlayBottomColor,
                                          PlayLipColor, 28);
        Place((RectTransform)join.transform, LobbyActionX, 328f, LobbyActionWidth, 78f);

        Button create = CreateButton("Lobby Create Button", panel, "BUAT ROOM", LobbyNeutralFill, 26);
        create.GetComponentInChildren<Text>().color = LobbyNeutralText;
        AddOutline((RectTransform)create.transform, LineColor);
        Place(create.GetComponent<RectTransform>(), LobbyPad, 424f, LobbyLeftWidth, 72f);

        SectionLabel(panel, "Code Room Label", "KODE ROOM", 526f);
        Place(CreateInputField("Lobby Code Input", panel, "Masukkan kode room").GetComponent<RectTransform>(),
              LobbyPad, 560f, LobbyInputWidth, 72f);
        Button code = LobbyGradientButton("Lobby Join Code Button", panel, "GABUNG", LobbyGreenTop, LobbyGreenBottom,
                                          LobbyGreenLip, 28);
        Place((RectTransform)code.transform, LobbyActionX, 558f, LobbyActionWidth, 78f);
    }

    private static void BuildLobbyRightColumn(RectTransform panel) {
        SectionLabel(panel, "Recent Rooms Label", "ROOM TERAKHIR", 152f, LobbyRightX, LobbyRightWidth);
        float listHeight = MaxRecentRooms * RecentCardHeight + (MaxRecentRooms - 1) * RecentCardSpacing;
        RectTransform recent = Place(CreateRect("Lobby Recent Rooms", panel, Vector2.zero), LobbyRightX, 188f,
                                     LobbyRightWidth, listHeight);
        recent.gameObject.AddComponent<RectMask2D>();
        ScrollRect recentScroll = recent.gameObject.AddComponent<ScrollRect>();
        recentScroll.horizontal = false;
        recentScroll.movementType = ScrollRect.MovementType.Clamped;
        recentScroll.scrollSensitivity = 28f;
        recentScroll.viewport = recent;
        RectTransform recentContent = CreateRect("Lobby Recent Rooms Content", recent, new Vector2(0f, listHeight));
        recentContent.anchorMin = new Vector2(0f, 1f);
        recentContent.anchorMax = new Vector2(1f, 1f);
        recentScroll.content = recentContent;
        Place(CreateText("Global Rooms Note", panel, "Room publik bisa ditemukan lewat CARI ROOM CEPAT.", SmallFontSize,
                         FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              LobbyRightX, 188f + listHeight + 14f, LobbyRightWidth, 36f);
    }

    private static void BuildLobbyFooter(RectTransform panel) {
        AddLine(panel, "Lobby Footer Line", LobbyPad, 708f, LobbyPanelWidth - LobbyPad * 2f);
        RectTransform dot = Place(CreateRect("Lobby Status Dot", panel, Vector2.zero), LobbyPad, 738f, 18f, 18f);
        AddShape(dot.gameObject, SuccessTextColor, "ui-circle");
        Place(CreateText("Lobby Status", panel, ReadyStatusMessage, BodyFontSize - 2, FontStyle.Normal,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              LobbyPad + 32f, 724f, LobbyPanelWidth - LobbyPad * 2f - 32f, 46f);
    }

    private static void SectionLabel(RectTransform panel, string name, string text, float y,
                                     float x = LobbyPad, float width = LobbyLeftWidth) {
        Text label = CreateText(name, panel, text, LabelFontSize - 4, FontStyle.Bold, TextAnchor.MiddleLeft,
                                Vector2.zero, Vector2.zero, MutedColor);
        Place(label.rectTransform, x, y, width, 30f);
    }

    /// Tombol bergradasi + lip (gaya MAIN). Label = "{name} Label" di tengah.
    private static Button LobbyGradientButton(string name, Transform parent, string label, Color top, Color bottom,
                                              Color lip, int fontSize) {
        Button button = CreateMenuButton(name, parent, top, bottom, lip, new Color(1f, 1f, 1f, 0.16f), 1.2f,
                                         out RectTransform face);
        Stretch(CreateText(name + " Label", face, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter,
                           Vector2.zero, Vector2.zero, Color.white).rectTransform, 12f);
        return button;
    }

    // ---------------------------------------------------------------- Room terakhir

    private void RefreshRecentRooms() {
        if (_recentList == null || _recentScroll == null)
            return;

        ClearChildren(_recentList);
        List<string> rooms = GetRecentRooms();
        int visibleCount = Mathf.Min(rooms.Count, MaxRecentRooms);
        var contentRect = (RectTransform)_recentList;
        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(1, visibleCount) * RecentCardHeight +
                                                Mathf.Max(0, visibleCount - 1) * RecentCardSpacing);

        if (visibleCount == 0) {
            RectTransform empty = CreateListRow(_recentList, "Recent Rooms Empty", 0, RecentCardHeight, 0f, 0f);
            AddShape(empty.gameObject, new Color(1f, 1f, 1f, 0.18f), "ui-dashed");
            Stretch(CreateText("Label", empty, "Belum ada. Room yang kamu masuki muncul di sini.", SmallFontSize,
                               FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor)
                        .rectTransform, 24f);
        }
        for (int i = 0; i < visibleCount; i++)
            BuildRecentRoomCard(rooms[i], i);
        _recentScroll.verticalNormalizedPosition = 1f;
    }

    private void BuildRecentRoomCard(string room, int index) {
        bool isQuickmatch = room.StartsWith("qm:");
        string label = isQuickmatch ? room.Substring(3) : room;
        RectTransform card = CreateListRow(_recentList, "Recent Room " + index, index, RecentCardHeight,
                                           RecentCardSpacing, 0f);
        AddShape(card.gameObject, CardColor, cornerScale: 1.4f);
        AddOutline(card, LineColor, cornerScale: 1.4f);

        Text name = CreateText("Name", card, label, BodyFontSize - 2, FontStyle.Bold, TextAnchor.MiddleLeft,
                               Vector2.zero, Vector2.zero, Color.white);
        PlaceInRow(name.rectTransform, 22f, LobbyRightWidth - 200f, 38f).anchoredPosition = new Vector2(22f, 14f);
        Text meta = CreateText("Meta", card, RecentRoomSubtitle(room), SmallFontSize - 2, FontStyle.Normal,
                               TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor);
        PlaceInRow(meta.rectTransform, 22f, LobbyRightWidth - 200f, 30f).anchoredPosition = new Vector2(22f, -18f);

        Button join = CreateButton("Join", card, "MASUK", LobbyNeutralFill, 22);
        join.GetComponentInChildren<Text>().color = LobbyNeutralText;
        AddOutline((RectTransform)join.transform, LineColor);
        PlaceInRow(join.GetComponent<RectTransform>(), -14f, 130f, 58f);
        join.onClick.AddListener(() => JoinRecentRoom(room));
    }

    /// "Room privat · 2 jam lalu" / "Kode room · kemarin" (tanpa waktu untuk riwayat lama yang belum tercatat).
    private static string RecentRoomSubtitle(string room) {
        string kind = room.StartsWith("qm:") ? "Kode room" : "Room privat";
        long joinedAt = GetRecentRoomTime(room);
        return joinedAt > 0 ? kind + " · " + RelativeTime(joinedAt) : kind;
    }

    public static string RelativeTime(long unixSeconds) {
        TimeSpan ago = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (ago.TotalMinutes < 1) return "baru saja";
        if (ago.TotalHours < 1) return (int)ago.TotalMinutes + " menit lalu";
        if (ago.TotalDays < 1) return (int)ago.TotalHours + " jam lalu";
        if (ago.TotalDays < 2) return "kemarin";
        return (int)ago.TotalDays + " hari lalu";
    }

    // Waktu masuk disimpan terpisah ("room=detik|...") supaya format daftar room lama tetap terbaca.
    private static Dictionary<string, long> GetRecentRoomTimes() {
        var times = new Dictionary<string, long>();
        foreach (string entry in PlayerPrefs.GetString(RecentRoomTimesKey, "").Split('|')) {
            int separator = entry.LastIndexOf('=');
            if (separator <= 0 || !long.TryParse(entry.Substring(separator + 1), out long seconds))
                continue;
            times[entry.Substring(0, separator)] = seconds;
        }
        return times;
    }

    private static long GetRecentRoomTime(string room) =>
        GetRecentRoomTimes().TryGetValue(room, out long seconds) ? seconds : 0;

    /// Catat waktu masuk; hanya room yang masih ada di daftar room terakhir yang disimpan.
    private static void SaveRecentRoomTime(string room, bool remove) {
        Dictionary<string, long> times = GetRecentRoomTimes();
        if (remove) times.Remove(room);
        else times[room] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        List<string> keep = GetRecentRooms();
        var entries = new List<string>();
        foreach (KeyValuePair<string, long> pair in times) {
            if (keep.Contains(pair.Key))
                entries.Add(pair.Key + "=" + pair.Value);
        }
        PlayerPrefs.SetString(RecentRoomTimesKey, string.Join("|", entries));
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    /// Ganti "Lobby Panel" di LobbyCanvas.prefab dengan desain baru (bagian lain prefab tidak berubah).
    public static void RebuildLobbyPanelInPrefab() {
        if (LoadCanvasPrefab(LobbyCanvasPrefabPath) == null) {
            Debug.LogWarning($"[Lobby UI] {LobbyCanvasPrefabPath} tidak ditemukan.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(LobbyCanvasPrefabPath);
        try {
            Transform oldPanel = FindChild(root.transform, "Lobby Panel");
            RectTransform safeArea = oldPanel != null
                ? oldPanel.parent as RectTransform
                : FindChild(root.transform, "Lobby Safe Area") as RectTransform;
            if (safeArea == null) {
                Debug.LogWarning("[Lobby UI] \"Lobby Panel\" / \"Lobby Safe Area\" tidak ditemukan di prefab.");
                return;
            }
            int siblingIndex = oldPanel != null ? oldPanel.GetSiblingIndex() : 0;
            bool wasActive = oldPanel == null || oldPanel.gameObject.activeSelf;
            if (oldPanel != null)
                UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

            RectTransform panel = BuildLobbyPanel(safeArea);
            panel.SetSiblingIndex(Mathf.Min(siblingIndex, safeArea.childCount - 1));
            panel.gameObject.SetActive(wasActive);
            PrefabUtility.SaveAsPrefabAsset(root, LobbyCanvasPrefabPath);
            Debug.Log("[Lobby UI] Panel lobby di LobbyCanvas.prefab diganti ke desain baru (08b).");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
#endif
}
