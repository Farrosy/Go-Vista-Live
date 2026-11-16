using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pemilih warna HSV (mockup v2 layar 2): kotak Saturasi/Value, bar Hue, input HEX, dan swatch.
///  - Changed  : setiap gerakan saat digeser (untuk preview langsung).
///  - Committed: saat jari dilepas / HEX dikirim / swatch ditekan (satu langkah URUNGKAN).
/// Dibangun lewat <see cref="Build"/>; tekstur SV dibuat ulang hanya saat hue berubah.
/// </summary>
public sealed class HsvColorPicker : MonoBehaviour {
    private const int SvTextureSize = 32;
    private const int HueTextureWidth = 128;
    private const float SvSize = 170f;
    private const float Gap = 16f;
    private const float HueHeight = 30f;
    private const float HexHeight = 48f;
    private const float SwatchSize = 38f;
    private const float SwatchGap = 8f;
    private const float HandleSize = 30f;

    private static readonly Color[] Swatches = {
        new Color32(0x15, 0x46, 0xD8, 0xFF), new Color32(0xE1, 0x1D, 0x48, 0xFF), new Color32(0x10, 0xB9, 0x81, 0xFF),
        new Color32(0xFB, 0xBF, 0x24, 0xFF), new Color32(0x7B, 0x4D, 0xFF, 0xFF), new Color32(0xF4, 0x72, 0xB6, 0xFF),
        new Color32(0x2A, 0x2D, 0x36, 0xFF), new Color32(0xF2, 0xF4, 0xF8, 0xFF),
    };

    public event Action<Color> Changed;
    public event Action<Color> Committed;

    private RawImage _sv;
    private RectTransform _svHandle;
    private RectTransform _hueHandle;
    private TMP_InputField _hex;
    private Texture2D _svTexture;
    private float _h, _s, _v;
    private float _textureHue = -1f;

    public Color Color => Color.HSVToRGB(_h, _s, _v);

    public static HsvColorPicker Build(RectTransform parent, ChatUiAssets a, float width) {
        RectTransform root = ChatUiKit.CreateRect("HSV Picker", parent);
        ChatUiKit.Place(root, 0f, 0f, width, SvSize);
        HsvColorPicker picker = root.gameObject.AddComponent<HsvColorPicker>();
        picker.BuildContent(root, a, width);
        return picker;
    }

    private void BuildContent(RectTransform root, ChatUiAssets a, float width) {
        RectTransform svRect = ChatUiKit.CreateRect("SV", root);
        ChatUiKit.Place(svRect, 0f, 0f, SvSize, SvSize);
        _svTexture = new Texture2D(SvTextureSize, SvTextureSize, TextureFormat.RGB24, false) {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HSV SV"
        };
        _sv = svRect.gameObject.AddComponent<RawImage>();
        _sv.texture = _svTexture;
        svRect.gameObject.AddComponent<PickerDragArea>().Init(OnSvDrag, Commit);
        _svHandle = Handle(svRect, a, "SV Handle");

        float rightX = SvSize + Gap;
        float rightWidth = width - rightX;
        RectTransform hueRect = ChatUiKit.CreateRect("Hue", root);
        ChatUiKit.Place(hueRect, rightX, 0f, rightWidth, HueHeight);
        hueRect.gameObject.AddComponent<RawImage>().texture = BuildHueTexture();
        hueRect.gameObject.AddComponent<PickerDragArea>().Init(OnHueDrag, Commit);
        _hueHandle = Handle(hueRect, a, "Hue Handle");

        _hex = CreateHexInput(root, a, rightX, HueHeight + 14f, rightWidth);
        BuildSwatches(root, a, rightX, HueHeight + 14f + HexHeight + 14f, rightWidth);
    }

    void OnDestroy() {
        if (_svTexture) Destroy(_svTexture);
    }

    /// <summary>Tampilkan warna tanpa memicu event (mis. saat memilih bagian warna lain).</summary>
    public void SetColor(Color color) {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        // Hitam/abu tidak punya hue: pertahankan hue lama supaya kotak SV tidak melompat ke merah.
        if (s > 0.001f && v > 0.001f) _h = h;
        _s = s;
        _v = v;
        Refresh();
    }

    private void OnSvDrag(Vector2 normalized) {
        _s = normalized.x;
        _v = normalized.y;
        Refresh();
        Changed?.Invoke(Color);
    }

    private void OnHueDrag(Vector2 normalized) {
        _h = Mathf.Clamp(normalized.x, 0f, 0.999f);
        Refresh();
        Changed?.Invoke(Color);
    }

    private void Commit() => Committed?.Invoke(Color);

    private void Refresh() {
        if (!Mathf.Approximately(_textureHue, _h)) RebuildSvTexture();
        MoveHandle(_svHandle, _s, _v);
        MoveHandle(_hueHandle, _h, 0.5f);
        if (_hex != null && !_hex.isFocused) _hex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(Color));
    }

    private static void MoveHandle(RectTransform handle, float x, float y) {
        handle.anchorMin = handle.anchorMax = new Vector2(x, y);
        handle.anchoredPosition = Vector2.zero;
    }

    private void RebuildSvTexture() {
        _textureHue = _h;
        var pixels = new Color[SvTextureSize * SvTextureSize];
        float max = SvTextureSize - 1;
        for (int y = 0; y < SvTextureSize; y++) {
            for (int x = 0; x < SvTextureSize; x++) pixels[y * SvTextureSize + x] = Color.HSVToRGB(_h, x / max, y / max);
        }
        _svTexture.SetPixels(pixels);
        _svTexture.Apply(false);
    }

    private static Texture2D BuildHueTexture() {
        var texture = new Texture2D(HueTextureWidth, 1, TextureFormat.RGB24, false) {
            wrapMode = TextureWrapMode.Clamp, name = "HSV Hue"
        };
        for (int x = 0; x < HueTextureWidth; x++) texture.SetPixel(x, 0, Color.HSVToRGB(x / (HueTextureWidth - 1f), 1f, 1f));
        texture.Apply(false);
        return texture;
    }

    private static RectTransform Handle(RectTransform parent, ChatUiAssets a, string name) {
        RectTransform handle = ChatUiKit.CreateRect(name, parent);
        handle.sizeDelta = new Vector2(HandleSize, HandleSize);
        handle.pivot = new Vector2(0.5f, 0.5f);
        ChatUiKit.AddShape(handle.gameObject, a.circle, Color.white).raycastTarget = false;
        RectTransform inner = ChatUiKit.Stretch(ChatUiKit.CreateRect("Inner", handle), 5f, 5f, 5f, 5f);
        ChatUiKit.AddShape(inner.gameObject, a.circle, FriendsTheme.Panel).raycastTarget = false;
        return handle;
    }

    private TMP_InputField CreateHexInput(RectTransform root, ChatUiAssets a, float x, float y, float width) {
        RectTransform box = ChatUiKit.CreateRect("Hex", root);
        ChatUiKit.Place(box, x, y, width, HexHeight);
        Image background = ChatUiKit.AddShape(box.gameObject, a.rounded, FriendsTheme.Card, 1.4f);
        FriendsUiKit.AddBorder(box, FriendsTheme.CardBorder);
        TextMeshProUGUI prefix = ChatUiKit.CreateText("Prefix", box, a.font, "HEX #", FriendsTheme.SmallSize - 2,
                                                      FriendsTheme.Muted, FontStyles.Bold, TextAlignmentOptions.Left);
        ChatUiKit.Stretch(prefix.rectTransform, 16f, 0f, width - 96f, 0f);

        RectTransform viewport = ChatUiKit.Stretch(ChatUiKit.CreateRect("Text Area", box), 96f, 6f, 12f, 6f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", viewport, a.font, "", FriendsTheme.SmallSize, Color.white,
                                                    FontStyles.Bold, TextAlignmentOptions.Left);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

        TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = text;
        input.fontAsset = a.font;
        input.pointSize = FriendsTheme.SmallSize;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 6;
        input.richText = false;
        input.shouldHideMobileInput = false;
        input.onValidateInput = (_, _, c) => Uri.IsHexDigit(c) ? char.ToUpperInvariant(c) : '\0';
        input.onEndEdit.AddListener(OnHexSubmitted);
        return input;
    }

    private void OnHexSubmitted(string value) {
        if (value.Length != 6 || !ColorUtility.TryParseHtmlString("#" + value, out Color color)) {
            Refresh(); // kembalikan teks ke warna yang sedang dipakai
            return;
        }
        SetColor(color);
        Changed?.Invoke(Color);
        Commit();
    }

    private void BuildSwatches(RectTransform root, ChatUiAssets a, float x, float y, float width) {
        int perRow = Mathf.Max(1, Mathf.FloorToInt((width + SwatchGap) / (SwatchSize + SwatchGap)));
        for (int i = 0; i < Swatches.Length; i++) {
            Color swatch = Swatches[i];
            RectTransform rect = ChatUiKit.CreateRect("Swatch " + i, root);
            ChatUiKit.Place(rect, x + i % perRow * (SwatchSize + SwatchGap), y + i / perRow * (SwatchSize + SwatchGap),
                            SwatchSize, SwatchSize);
            Image image = ChatUiKit.AddShape(rect.gameObject, a.circle, swatch);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => {
                SetColor(swatch);
                Changed?.Invoke(Color);
                Commit();
            });
        }
    }
}

/// <summary>Area yang bisa ditekan & digeser; melaporkan posisi ternormalisasi 0..1 di dalam rect.</summary>
public sealed class PickerDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    private Action<Vector2> _onDrag;
    private Action _onRelease;

    public void Init(Action<Vector2> onDrag, Action onRelease) {
        _onDrag = onDrag;
        _onRelease = onRelease;
    }

    public void OnPointerDown(PointerEventData eventData) => Report(eventData);
    public void OnDrag(PointerEventData eventData) => Report(eventData);
    public void OnPointerUp(PointerEventData eventData) => _onRelease?.Invoke();

    private void Report(PointerEventData eventData) {
        var rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                                                                     eventData.pressEventCamera, out Vector2 local))
            return;
        Rect r = rect.rect;
        _onDrag?.Invoke(new Vector2(Mathf.Clamp01((local.x - r.x) / r.width), Mathf.Clamp01((local.y - r.y) / r.height)));
    }
}
