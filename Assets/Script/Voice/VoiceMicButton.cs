using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// Tombol mic in-game di bawah minimap, di kanan tombol chat (lihat HudLayout). UI dibuat lewat kode, tampil selama
/// ada avatar lokal, panel chat tertutup (panel chat menutupi pojok yang sama), dan pemain tidak sedang di layar mati.
/// Cincin berubah hijau saat pemain terdeteksi bicara.
///
/// Normcore meminta izin mikrofon saat avatar spawn dan tidak mencoba lagi kalau ditolak, jadi pemain yang
/// menolak harus mengizinkannya di Pengaturan HP lalu masuk room lagi. Tombol menjelaskan hal itu.
public sealed class VoiceMicButton : MonoBehaviour {
    private const int CanvasSortingOrder = 26; // Di atas HUD (25), di bawah menu pause (51) dan chat (52).
    private const float ButtonSize = HudLayout.MicButtonSize;
    private const float RingThickness = 4f;
    private const float IconInset = 24f;
    private const float HintGap = 12f;
    private const float HintWidth = 640f;
    private const float HintHeight = 72f;
    private const float HintSeconds = 4f;
    private const string PermissionHint = "Izinkan mikrofon di Pengaturan HP, lalu masuk room lagi";

    [SerializeField] private TMP_FontAsset font;

    private Canvas _canvas;
    private Image _ring;
    private Image _icon;
    private GameObject _hint;
    private Sprite _micSprite;
    private Sprite _micOffSprite;
    private bool _micAllowed;
    private bool _deniedForThisAvatar;
    private ProximityVoice _avatar;
    private float _hintUntil;

    /// Izin yang baru diberikan setelah ditolak tidak berguna untuk avatar ini, karena Normcore tidak mencoba lagi.
    private bool MicUsable => _micAllowed && !_deniedForThisAvatar;

    private void Awake() {
        if (font == null) font = TMP_Settings.defaultFontAsset;
        _micSprite = VoiceIcons.Mic;
        _micOffSprite = VoiceIcons.MicOff;
        RefreshPermission();
        BuildUi();
    }

    private void OnApplicationFocus(bool hasFocus) {
        // Dialog izin Android menutup fokus aplikasi; saat kembali, jawabannya sudah bisa dibaca.
        if (hasFocus) RefreshPermission();
    }

    private void Update() {
        ProximityVoice local = ProximityVoice.Local;
        if (local != _avatar) {
            _avatar = local;
            _deniedForThisAvatar = false;
        }
        _canvas.enabled = local != null && !ChatPanel.IsPanelOpen && !PlayerInputHub.IsDeathLocked;
        if (!_canvas.enabled) {
            _hintUntil = 0f;
            return;
        }

        bool micOn = VoiceSession.MicEnabled && MicUsable;
        bool speaking = micOn && VoiceRules.IsSpeaking(local.VoiceVolume);
        _icon.sprite = micOn ? _micSprite : _micOffSprite;
        _icon.color = micOn ? Color.white : FriendsTheme.Danger;
        _ring.color = speaking ? ChatTheme.Online : micOn ? FriendsTheme.Neutral : FriendsTheme.Danger;
        _hint.SetActive(Time.unscaledTime < _hintUntil);
    }

    private void OnClick() {
        RefreshPermission();
        if (MicUsable)
            VoiceSession.ToggleMic();
        else
            _hintUntil = Time.unscaledTime + HintSeconds;
    }

    private void RefreshPermission() {
#if UNITY_ANDROID && !UNITY_EDITOR
        _micAllowed = Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        _micAllowed = true;
#endif
        if (!_micAllowed && ProximityVoice.Local != null)
            _deniedForThisAvatar = true;
    }

    private void BuildUi() {
        _canvas = CreateCanvas();
        RectTransform safeArea = ChatUiKit.Stretch(ChatUiKit.CreateRect("Voice Safe Area", _canvas.transform), 0f, 0f, 0f, 0f);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        Sprite circle = VoiceIcons.Circle;
        Button button = ChatUiKit.CreateButton("Mic Button", safeArea, circle, FriendsTheme.Neutral);
        ChatUiKit.Place((RectTransform)button.transform, HudLayout.MicButtonLeft, HudLayout.MicButtonTop, ButtonSize, ButtonSize);
        button.onClick.AddListener(OnClick);
        _ring = (Image)button.targetGraphic;

        RectTransform fill = ChatUiKit.Stretch(ChatUiKit.CreateRect("Fill", button.transform),
                                               RingThickness, RingThickness, RingThickness, RingThickness);
        ChatUiKit.AddShape(fill.gameObject, circle, ChatTheme.Panel).raycastTarget = false;

        _icon = ChatUiKit.AddIcon("Icon", button.transform, _micOffSprite, FriendsTheme.Danger);
        ChatUiKit.Stretch(_icon.rectTransform, IconInset, IconInset, IconInset, IconInset);

        _hint = BuildHint(safeArea);
    }

    private Canvas CreateCanvas() {
        var canvasObject = new GameObject("Voice Canvas", typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvas.enabled = false;
        return canvas;
    }

    private GameObject BuildHint(RectTransform parent) {
        RectTransform hint = ChatUiKit.CreateRect("Permission Hint", parent);
        ChatUiKit.Place(hint, HudLayout.MicButtonLeft + ButtonSize + HintGap,
                        HudLayout.MicButtonTop + (ButtonSize - HintHeight) / 2f, HintWidth, HintHeight);
        ChatUiKit.AddShape(hint.gameObject, VoiceIcons.Rounded, ChatTheme.Panel, 1.6f).raycastTarget = false;
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", hint, font, PermissionHint, ChatTheme.NameSize, Color.white,
                                                    FontStyles.Normal, TextAlignmentOptions.Center);
        ChatUiKit.Stretch(text.rectTransform, 16f, 0f, 16f, 0f);
        hint.gameObject.SetActive(false);
        return hint.gameObject;
    }
}
