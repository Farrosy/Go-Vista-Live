using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Membuat baris-baris daftar di menu TEMAN (Docs/Friends/menu-teman-1-daftar.png & -2-permintaan.png).
/// Setiap baris tinggi tetap (LayoutElement) di dalam VerticalLayoutGroup.
public static class FriendRows {
    private const float RowHeight = 100f;
    private const float SectionHeight = 48f;
    private const float AvatarSize = 64f;
    private const float ButtonHeight = 66f;
    private const float RightPad = 22f;
    private const float ButtonGap = 12f;
    private const float OfflineAlpha = 0.6f;
    private const float ConfirmSeconds = 3f;

    public static void Section(Transform content, ChatUiAssets assets, string title) {
        RectTransform row = CreateRow(content, "Section", SectionHeight, false, assets);
        TextMeshProUGUI text = ChatUiKit.CreateText("Title", row, assets.font, title, FriendsTheme.SmallSize,
                                                    FriendsTheme.Muted, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        text.characterSpacing = 8f;
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 6f);
    }

    public static void Friend(Transform content, ChatUiAssets assets, string name, FriendStatus status,
                              Action join, Action remove) {
        RectTransform row = CreatePersonRow(content, assets, name, status.Label, status.LabelColor, status.DotColor);
        float x = -RightPad;
        x = AddConfirmButton(row, assets, "HAPUS", FriendsTheme.Danger, FriendsTheme.DangerFill, x, 150f, remove);
        if (status.Place == FriendPlace.InRoom) {
            if (status.CanJoin)
                AddSolidButton(row, assets, "GABUNG", FriendsTheme.BlueTop, FriendsTheme.BlueBottom, FriendsTheme.BlueLip,
                               x, 170f, join);
            else
                AddOutlineButton(row, assets, "PENUH", FriendsTheme.Neutral, FriendsTheme.NeutralFill, x, 130f, null)
                    .interactable = false;
        }
        if (!status.Online)
            row.gameObject.AddComponent<CanvasGroup>().alpha = OfflineAlpha;
    }

    public static void Incoming(Transform content, ChatUiAssets assets, string name, Action accept, Action decline) {
        RectTransform row = CreatePersonRow(content, assets, name, "Ingin berteman denganmu", FriendsTheme.Muted, null);
        float x = AddOutlineButton(row, assets, "TOLAK", FriendsTheme.Danger, FriendsTheme.DangerFill, -RightPad, 140f,
                                   decline, out _);
        AddSolidButton(row, assets, "TERIMA", FriendsTheme.GreenTop, FriendsTheme.GreenBottom, FriendsTheme.GreenLip,
                       x, 160f, accept);
    }

    public static void Outgoing(Transform content, ChatUiAssets assets, string name, Action cancel) {
        RectTransform row = CreatePersonRow(content, assets, name, "Menunggu jawaban", FriendsTheme.Muted, null);
        AddOutlineButton(row, assets, "BATAL", FriendsTheme.Neutral, FriendsTheme.NeutralFill, -RightPad, 140f, cancel);
    }

    // ---------------------------------------------------------------- Helper

    private static RectTransform CreatePersonRow(Transform content, ChatUiAssets assets, string name, string subtitle,
                                                 Color subtitleColor, Color? dot) {
        RectTransform row = CreateRow(content, "Person " + name, RowHeight, true, assets);
        RectTransform avatar = FriendsUiKit.CreateAvatar(row, assets, FriendsManager.BaseName(name), AvatarSize, dot);
        FriendsUiKit.PlaceInRow(avatar, 22f, AvatarSize, AvatarSize);

        TextMeshProUGUI nameText = ChatUiKit.CreateText("Name", row, assets.font, FriendsManager.BaseName(name),
                                                        FriendsTheme.NameSize, Color.white, FontStyles.Bold);
        FriendsUiKit.PlaceInRow(nameText.rectTransform, 106f, 560f, 40f, 16f);
        TextMeshProUGUI sub = ChatUiKit.CreateText("Subtitle", row, assets.font, subtitle, FriendsTheme.BodySize,
                                                   subtitleColor);
        FriendsUiKit.PlaceInRow(sub.rectTransform, 106f, 600f, 34f, -20f);
        UiTextFit.Apply(nameText);
        UiTextFit.Apply(sub);
        return row;
    }

    private static RectTransform CreateRow(Transform content, string name, float height, bool card, ChatUiAssets assets) {
        RectTransform row = ChatUiKit.CreateRect(name, content);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = layout.minHeight = height;
        if (card) {
            ChatUiKit.AddShape(row.gameObject, assets.rounded, FriendsTheme.Card, 1.4f).raycastTarget = false;
            FriendsUiKit.AddBorder(row, FriendsTheme.CardBorder);
        }
        return row;
    }

    /// Menaruh tombol dari kanan ke kiri; mengembalikan x untuk tombol berikutnya.
    private static float AddSolidButton(RectTransform row, ChatUiAssets assets, string label, Color top, Color bottom,
                                        Color lip, float rightX, float width, Action onClick) {
        Button button = FriendsUiKit.CreateSolidButton(label + " Button", row, assets, label, top, bottom, lip);
        FriendsUiKit.PlaceInRow((RectTransform)button.transform, rightX, width, ButtonHeight);
        WireOnce(button, onClick);
        return rightX - width - ButtonGap;
    }

    private static Button AddOutlineButton(RectTransform row, ChatUiAssets assets, string label, Color color, Color fill,
                                           float rightX, float width, Action onClick) {
        AddOutlineButton(row, assets, label, color, fill, rightX, width, onClick, out Button button);
        return button;
    }

    private static float AddOutlineButton(RectTransform row, ChatUiAssets assets, string label, Color color, Color fill,
                                          float rightX, float width, Action onClick, out Button button) {
        button = FriendsUiKit.CreateOutlineButton(label + " Button", row, assets, label, color, fill);
        FriendsUiKit.PlaceInRow((RectTransform)button.transform, rightX, width, ButtonHeight);
        if (onClick != null) WireOnce(button, onClick);
        return rightX - width - ButtonGap;
    }

    private static void WireOnce(Button button, Action onClick) {
        button.onClick.AddListener(() => {
            button.interactable = false; // cegah tap dobel; daftar dibangun ulang setelah aksi selesai.
            onClick?.Invoke();
        });
    }

    /// Tombol yang harus ditekan dua kali (tap pertama = "YAKIN?") supaya teman tidak terhapus tidak sengaja.
    private static float AddConfirmButton(RectTransform row, ChatUiAssets assets, string label, Color color, Color fill,
                                          float rightX, float width, Action onConfirm) {
        Button button = FriendsUiKit.CreateOutlineButton(label + " Button", row, assets, label, color, fill);
        FriendsUiKit.PlaceInRow((RectTransform)button.transform, rightX, width, ButtonHeight);
        TextMeshProUGUI text = FriendsUiKit.LabelOf(button);
        ConfirmLabel confirm = button.gameObject.AddComponent<ConfirmLabel>();
        confirm.Init(text, label);
        button.onClick.AddListener(() => {
            if (!confirm.Armed) {
                confirm.Arm(ConfirmSeconds);
                return;
            }
            button.interactable = false;
            onConfirm?.Invoke();
        });
        return rightX - width - ButtonGap;
    }
}

/// Label tombol konfirmasi: "YAKIN?" selama beberapa detik, lalu kembali ke label semula.
public sealed class ConfirmLabel : MonoBehaviour {
    private TextMeshProUGUI _text;
    private string _idle;
    private float _armedUntil;

    public bool Armed => Time.unscaledTime < _armedUntil;

    public void Init(TextMeshProUGUI text, string idleLabel) {
        _text = text;
        _idle = idleLabel;
    }

    public void Arm(float seconds) {
        _armedUntil = Time.unscaledTime + seconds;
        _text.text = "YAKIN?";
    }

    private void Update() {
        if (_armedUntil > 0f && !Armed) {
            _armedUntil = 0f;
            _text.text = _idle;
        }
    }
}
