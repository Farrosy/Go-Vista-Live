using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Notif "Akun berganti" dengan satu tombol MULAI ULANG (tidak bisa ditutup), muncul saat akun berbeda masuk
/// di proses game yang sama (AccountSession). Dibangun saat runtime di canvas overlay paling atas, memakai
/// kit menu yang sama dengan dialog lain (FriendsUiKit / CustomizerUi).
/// </summary>
public static class AccountRestartPrompt {
    private const int SortingOrder = 32000;
    private const float Width = 760f;
    private const float Height = 380f;
    private const float Pad = 40f;
    private const float ButtonHeight = 84f;

    private static GameObject _shown;

    /// <param name="accountName">Nama akun baru, ditampilkan di keterangan.</param>
    public static void Show(string accountName) {
        if (_shown != null) return;
        ChatUiAssets a = FriendsMenu.SharedAssets;
        RectTransform root = CreateCanvas();
        _shown = root.gameObject;

        RectTransform overlay = ChatUiKit.Stretch(ChatUiKit.CreateRect("Restart Overlay", root), 0f, 0f, 0f, 0f);
        overlay.gameObject.AddComponent<Image>().color = FriendsTheme.Dim; // juga menahan klik ke UI di bawahnya
        RectTransform panel = CustomizerUi.CreatePanel(overlay, a, Width, Height);

        TextMeshProUGUI title = ChatUiKit.CreateText("Title", panel, a.font, "Akun berganti", 40, Color.white, FontStyles.Bold);
        ChatUiKit.Place(title.rectTransform, Pad, Pad, Width - Pad * 2f, 56f);
        TextMeshProUGUI body = ChatUiKit.CreateText("Body", panel, a.font, BodyText(accountName), FriendsTheme.BodySize,
                                                    FriendsTheme.Neutral, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.richText = true; // nama akun ditebalkan
        ChatUiKit.Place(body.rectTransform, Pad, Pad + 70f, Width - Pad * 2f, 140f);

        Button restart = FriendsUiKit.CreateSolidButton("Restart", panel, a, "MULAI ULANG", FriendsTheme.GreenTop,
                                                        FriendsTheme.GreenBottom, FriendsTheme.GreenLip);
        ChatUiKit.Place((RectTransform)restart.transform, Pad, Height - Pad - ButtonHeight, Width - Pad * 2f, ButtonHeight);
        restart.onClick.AddListener(() => {
            restart.interactable = false;
            AppRestarter.Restart();
        });
    }

    private static string BodyText(string accountName) {
        string name = string.IsNullOrEmpty(accountName) ? "akun baru" : "<b><color=#FFFFFF>" + PlayerNameRules.Sanitize(accountName) + "</color></b>";
        return AppRestarter.CanRelaunch
            ? $"Game perlu dimulai ulang supaya data akun sebelumnya bersih. Setelah itu kamu langsung masuk sebagai {name}."
            : $"Game perlu dimulai ulang supaya data akun sebelumnya bersih. Game akan ditutup; buka lagi untuk masuk sebagai {name}.";
    }

    private static RectTransform CreateCanvas() {
        var go = new GameObject("Account Restart Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(go);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }
}
