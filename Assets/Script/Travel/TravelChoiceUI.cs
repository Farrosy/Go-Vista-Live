using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Panel pilihan tujuan yang bisa dipakai ulang (judul + 2–3 tombol), gaya gelap seperti panel
/// lobby/dialog. API: Open(judul, opsi[]) / Close(). Dibangun sendiri saat runtime (overlay canvas),
/// jadi tidak perlu merakit UI di scene — cukup pasang komponen ini di object persisten
/// MainMultiplayerScene. InteractButtonUI yang single-verb tidak cukup untuk multi-pilihan.
public class TravelChoiceUI : MonoBehaviour
{
    public static TravelChoiceUI Instance { get; private set; }

    [Tooltip("Sorting order canvas. Di atas HUD gameplay, di bawah fade transisi (30000).")]
    [SerializeField] private int sortingOrder = 25000;

    private CanvasGroup group;
    private TMP_Text titleLabel;
    private RectTransform optionsContainer;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private Action onCancel;
    private bool built;

    public bool IsOpen => group != null && group.blocksRaycasts;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Build();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// Buka panel dengan judul + daftar opsi. onCancel opsional dipanggil kalau pemain menekan Batal.
    public void Open(string title, (string label, Action onPick)[] options, Action onCancel = null)
    {
        Build();
        this.onCancel = onCancel;
        if (titleLabel != null) titleLabel.text = title;
        BuildOptionButtons(options);
        SetVisible(true);

        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPauseLocked(true);
        if (InteractButtonUI.Instance != null) InteractButtonUI.Instance.SetSuppressed(true);
    }

    /// Tutup panel tanpa memicu onCancel (dipakai setelah memilih).
    public void Close()
    {
        onCancel = null;
        SetVisible(false);
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPauseLocked(false);
        if (InteractButtonUI.Instance != null) InteractButtonUI.Instance.SetSuppressed(false);
    }

    private void Pick(Action onPick)
    {
        Close();          // tutup dulu supaya tidak dobel-tap; lalu jalankan aksi (mis. TravelTo).
        onPick?.Invoke();
    }

    private void Cancel()
    {
        Action cb = onCancel;
        Close();
        cb?.Invoke();
    }

    private void BuildOptionButtons((string label, Action onPick)[] options)
    {
        foreach (var go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();

        if (options == null) return;
        foreach (var opt in options)
        {
            Action pick = opt.onPick;
            GameObject button = CreateButton(optionsContainer, opt.label,
                new Color(0.20f, 0.45f, 0.85f, 1f), () => Pick(pick));
            spawnedButtons.Add(button);
        }
    }

    private void SetVisible(bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    // ---- Pembangunan UI prosedural ----

    private void Build()
    {
        if (built && group != null) return;
        built = true;

        var canvasGo = new GameObject("TravelChoiceCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        group = canvasGo.AddComponent<CanvasGroup>();

        // Backdrop gelap full-screen yang menangkap tap (biar gameplay di belakang tidak ketekan).
        var backdrop = new GameObject("Backdrop").AddComponent<Image>();
        backdrop.transform.SetParent(canvasGo.transform, false);
        backdrop.color = new Color(0f, 0f, 0f, 0.6f);
        backdrop.raycastTarget = true;
        Stretch(backdrop.rectTransform);

        // Panel tengah.
        var panel = new GameObject("Panel").AddComponent<Image>();
        panel.transform.SetParent(canvasGo.transform, false);
        panel.color = new Color(0.11f, 0.12f, 0.16f, 0.98f);
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(620f, 100f); // tinggi diatur ContentSizeFitter

        var panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(40, 40, 36, 36);
        panelLayout.spacing = 20f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;

        var panelFitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Judul.
        titleLabel = CreateText(panel.transform, "Title", "", 40, FontStyles.Bold, Color.white);
        var titleLe = titleLabel.gameObject.AddComponent<LayoutElement>();
        titleLe.minHeight = 56f;

        // Container tombol opsi.
        var optionsGo = new GameObject("Options", typeof(RectTransform));
        optionsGo.transform.SetParent(panel.transform, false);
        optionsContainer = optionsGo.GetComponent<RectTransform>();
        var optionsLayout = optionsGo.AddComponent<VerticalLayoutGroup>();
        optionsLayout.spacing = 14f;
        optionsLayout.childForceExpandWidth = true;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.childControlWidth = true;
        optionsLayout.childControlHeight = true;
        optionsGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Tombol Batal.
        CreateButton(panel.transform, "Batal", new Color(0.24f, 0.25f, 0.30f, 1f), Cancel);
    }

    private GameObject CreateButton(Transform parent, string label, Color bg, Action onClick)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = bg;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick?.Invoke());

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 92f;
        le.preferredHeight = 92f;

        var text = CreateText(go.transform, "Label", label, 34, FontStyles.Bold, Color.white);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;

        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string content, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }
}
