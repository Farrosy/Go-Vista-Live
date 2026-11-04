using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Membuat satu baris pesan di panel chat (gaya mockup Docs/Chat/chat-panel-mockup.png):
///  - Pesan orang lain : avatar inisial di kiri + bubble gelap berisi nama berwarna, jam, dan teks.
///  - Pesan sendiri    : bubble gradasi biru→ungu rata kanan, teks + jam di pojok kanan bawah.
/// Ukuran bubble dihitung manual dari ukuran teks (tanpa nested layout group) supaya ringan & stabil.
public static class ChatMessageRow {
    private const float AvatarSize = 48f;
    private const float AvatarGap = 12f;
    private const float PadX = 16f;
    private const float PadY = 10f;
    private const float HeaderHeight = 28f;
    private const float HeaderGap = 10f;
    private const float OwnTimeGap = 12f;
    private const float MaxOtherTextWidth = 380f;
    private const float MaxOwnTextWidth = 420f;
    private const float BubbleCorner = 1.4f;

    public static RectTransform Create(Transform content, ChatMessage message, ChatUiAssets assets) {
        RectTransform row = ChatUiKit.CreateRect("Chat Row", content);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        float height = message.IsSystem ? BuildSystem(row, message, assets)
                     : message.IsLocal ? BuildOwn(row, message, assets)
                     : BuildOther(row, message, assets);
        layout.preferredHeight = height;
        layout.minHeight = height;
        return row;
    }

    private static float BuildOther(RectTransform row, ChatMessage message, ChatUiAssets assets) {
        Color nameColor = ChatRules.NameColor(message.ColorSeed);
        RectTransform bubble = ChatUiKit.CreateRect("Bubble", row);
        ChatUiKit.AddShape(bubble.gameObject, assets.rounded, ChatTheme.BubbleOther, BubbleCorner).raycastTarget = false;

        TextMeshProUGUI name = ChatUiKit.CreateText("Name", bubble, assets.font, message.SenderName,
                                                    ChatTheme.NameSize, nameColor, FontStyles.Bold);
        TextMeshProUGUI time = ChatUiKit.CreateText("Time", bubble, assets.font, ChatRules.FormatTime(message.SentAt),
                                                    ChatTheme.TimeSize, ChatTheme.Muted);
        TextMeshProUGUI body = CreateBody(bubble, message.Text, assets);

        Vector2 nameSize = name.GetPreferredValues(message.SenderName);
        float nameWidth = Mathf.Min(nameSize.x, MaxOtherTextWidth - 70f);
        float timeWidth = time.GetPreferredValues(time.text).x;
        Vector2 textSize = MeasureBody(body, MaxOtherTextWidth);

        float innerWidth = Mathf.Max(textSize.x, nameWidth + HeaderGap + timeWidth);
        float bubbleWidth = innerWidth + PadX * 2f;
        float bubbleHeight = PadY + HeaderHeight + textSize.y + PadY;

        ChatUiKit.Place(name.rectTransform, PadX, PadY, nameWidth, HeaderHeight);
        ChatUiKit.Place(time.rectTransform, PadX + nameWidth + HeaderGap, PadY, timeWidth, HeaderHeight);
        ChatUiKit.Place(body.rectTransform, PadX, PadY + HeaderHeight, textSize.x, textSize.y);
        ChatUiKit.Place(bubble, AvatarSize + AvatarGap, 0f, bubbleWidth, bubbleHeight);

        BuildAvatar(row, message, nameColor, assets, bubbleHeight);
        return bubbleHeight;
    }

    private static float BuildOwn(RectTransform row, ChatMessage message, ChatUiAssets assets) {
        RectTransform bubble = ChatUiKit.CreateRect("Bubble", row);
        Image shape = ChatUiKit.AddShape(bubble.gameObject, assets.rounded, Color.white, BubbleCorner);
        shape.raycastTarget = false;
        bubble.gameObject.AddComponent<UIVerticalGradient>().SetColors(ChatTheme.BubbleOwnTop, ChatTheme.BubbleOwnBottom);

        TextMeshProUGUI body = CreateBody(bubble, message.Text, assets);
        TextMeshProUGUI time = ChatUiKit.CreateText("Time", bubble, assets.font, ChatRules.FormatTime(message.SentAt),
                                                    ChatTheme.TimeSize, new Color(1f, 1f, 1f, 0.75f));
        float timeWidth = time.GetPreferredValues(time.text).x;
        Vector2 textSize = MeasureBody(body, MaxOwnTextWidth);

        float bubbleWidth = PadX + textSize.x + OwnTimeGap + timeWidth + PadX;
        float bubbleHeight = PadY + textSize.y + PadY;

        ChatUiKit.Place(body.rectTransform, PadX, PadY, textSize.x, textSize.y);
        // Jam di pojok kanan bawah bubble, sejajar baris teks terakhir.
        ChatUiKit.Place(time.rectTransform, PadX + textSize.x + OwnTimeGap,
                        bubbleHeight - PadY - HeaderHeight, timeWidth, HeaderHeight);
        ChatUiKit.PlaceTopRight(bubble, 0f, 0f, bubbleWidth, bubbleHeight);
        return bubbleHeight;
    }

    /// Pesan sistem ("Budi bergabung ke room"): teks kecil abu-abu miring di tengah, tanpa bubble.
    private static float BuildSystem(RectTransform row, ChatMessage message, ChatUiAssets assets) {
        string content = "• " + message.Text + "  " + ChatRules.FormatTime(message.SentAt);
        TextMeshProUGUI text = ChatUiKit.CreateText("System", row, assets.font, content, ChatTheme.NameSize,
                                                    ChatTheme.Muted, FontStyles.Italic, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        float height = Mathf.Ceil(text.GetPreferredValues(content, MaxOwnTextWidth + 100f, 0f).y);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return Mathf.Max(height, HeaderHeight);
    }

    private static TextMeshProUGUI CreateBody(Transform bubble, string text, ChatUiAssets assets) {
        TextMeshProUGUI body = ChatUiKit.CreateText("Body", bubble, assets.font, text, ChatTheme.BodySize,
                                                    Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        return body;
    }

    /// Lebar = baris terpanjang (maks maxWidth), tinggi = setelah dibungkus ke lebar itu.
    private static Vector2 MeasureBody(TextMeshProUGUI body, float maxWidth) {
        float width = Mathf.Min(body.GetPreferredValues(body.text).x, maxWidth);
        width = Mathf.Max(width, 1f);
        float height = body.GetPreferredValues(body.text, width, 0f).y;
        return new Vector2(Mathf.Ceil(width) + 1f, Mathf.Ceil(height));
    }

    private static void BuildAvatar(RectTransform row, ChatMessage message, Color color, ChatUiAssets assets,
                                    float bubbleHeight) {
        RectTransform avatar = ChatUiKit.CreateRect("Avatar", row);
        ChatUiKit.AddShape(avatar.gameObject, assets.circle, color).raycastTarget = false;
        // Avatar sejajar bagian bawah bubble, seperti di mockup.
        ChatUiKit.Place(avatar, 0f, Mathf.Max(0f, bubbleHeight - AvatarSize), AvatarSize, AvatarSize);
        TextMeshProUGUI initial = ChatUiKit.CreateText("Initial", avatar, assets.font, ChatRules.Initial(message.SenderName),
                                                       ChatTheme.NameSize, Color.white, FontStyles.Bold,
                                                       TextAlignmentOptions.Center);
        ChatUiKit.Stretch(initial.rectTransform, 0f, 0f, 0f, 0f);
    }
}
