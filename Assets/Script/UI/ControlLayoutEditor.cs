using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mode ATUR KONTROL: pemain menggeser tombol mobile dan mengubah ukurannya.
/// Pasang di objek "Control Layout Editor" (child terakhir MobileUI, full-screen, Image transparan
/// dengan Raycast Target ON). Selama mode ini terbuka, semua sentuhan ditangkap objek ini, jadi
/// joystick/tombol tidak ikut tertekan.
///
/// Dibuka dari Pause > Pengaturan > ATUR KONTROL (LobyUISceneController). Semua tampilan (warna,
/// teks, posisi toolbar) bisa diedit langsung di prefab MobileUI.
/// </summary>
public class ControlLayoutEditor : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    public static bool IsOpen { get; private set; }

    [Tooltip("Kontrol yang bisa diatur. Kosong = semua CustomizableControl di canvas ini.")]
    [SerializeField] private CustomizableControl[] controls;

    [Header("UI (child objek ini)")]
    [SerializeField] private RectTransform selectionFrame;
    [Tooltip("Panel tombol Simpan/Batal/Reset. Otomatis pindah ke bawah kalau kontrol yang dipilih ada di atas layar.")]
    [SerializeField] private RectTransform toolbar;
    [SerializeField] private float toolbarMargin = 24f;
    [SerializeField] private Text selectedLabel;
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private Text sizeValue;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;

    [Tooltip("Opsional: tombol Interact biasanya tersembunyi, jadi ditampilkan selama mode ini.")]
    [SerializeField] private InteractButtonUI interactButton;

    private CustomizableControl selected;
    private bool dragging;
    private Canvas canvas;
    private Action onClosed;
    private readonly Vector3[] corners = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    void Awake() {
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        if (controls == null || controls.Length == 0)
            controls = canvas.GetComponentsInChildren<CustomizableControl>(true);
        if (!interactButton) interactButton = canvas.GetComponentInChildren<InteractButtonUI>(true);

        if (sizeSlider) {
            sizeSlider.minValue = ControlLayoutSettings.MinScale;
            sizeSlider.maxValue = ControlLayoutSettings.MaxScale;
            sizeSlider.onValueChanged.AddListener(OnSizeChanged);
        }
        if (resetButton) resetButton.onClick.AddListener(ResetLayout);
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);
        if (saveButton) saveButton.onClick.AddListener(SaveLayout);
    }

    void OnDisable() {
        // Misalnya keluar room saat mode ini masih terbuka.
        if (!IsOpen) return;
        IsOpen = false;
        if (interactButton) interactButton.SetPreview(false);
    }

    /// <param name="closed">Dipanggil setelah Simpan atau Batal.</param>
    public void Open(Action closed) {
        onClosed = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        IsOpen = true;
        dragging = false;
        if (interactButton) interactButton.SetPreview(true);
        foreach (CustomizableControl c in controls) c.ApplySaved();
        Select(controls.Length > 0 ? controls[0] : null);
    }

    /// Tutup tanpa menyimpan (tombol BATAL / tombol Back HP).
    public void Cancel() {
        foreach (CustomizableControl c in controls) c.ApplySaved();
        Close();
    }

    private void SaveLayout() {
        foreach (CustomizableControl c in controls) c.SaveCurrent();
        ControlLayoutSettings.Save();
        Close();
    }

    private void ResetLayout() {
        foreach (CustomizableControl c in controls) c.ResetToDefault();
        Select(selected);
    }

    private void Close() {
        IsOpen = false;
        dragging = false;
        if (interactButton) interactButton.SetPreview(false);
        gameObject.SetActive(false);
        Action closed = onClosed;
        onClosed = null;
        closed?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData) {
        // Sentuhan di toolbar ikut "naik" ke sini, abaikan supaya slider/tombol toolbar tetap jalan.
        if (eventData.pointerCurrentRaycast.gameObject != gameObject) return;
        CustomizableControl hit = FindControlAt(eventData.position);
        if (!hit) return;
        Select(hit);
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData) {
        if (!dragging || !selected) return;
        selected.MoveBy(eventData.delta / canvas.scaleFactor);
        RefreshSelection();
    }

    public void OnPointerUp(PointerEventData eventData) => dragging = false;

    private CustomizableControl FindControlAt(Vector2 screenPosition) {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        // Dari belakang: kontrol yang digambar paling atas dipilih duluan.
        for (int i = controls.Length - 1; i >= 0; i--) {
            CustomizableControl c = controls[i];
            if (c && c.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(c.Rect, screenPosition, cam))
                return c;
        }
        return null;
    }

    private void Select(CustomizableControl control) {
        selected = control;
        if (sizeSlider) {
            sizeSlider.interactable = selected;
            if (selected) sizeSlider.SetValueWithoutNotify(selected.Scale);
        }
        RefreshSelection();
    }

    private void OnSizeChanged(float value) {
        if (!selected) return;
        selected.SetScale(value);
        RefreshSelection();
    }

    private void RefreshSelection() {
        if (selectedLabel) selectedLabel.text = selected ? selected.DisplayName : "Ketuk tombol untuk memilih";
        if (sizeValue) sizeValue.text = selected ? Mathf.RoundToInt(selected.Scale * 100f) + "%" : "-";

        if (!selectionFrame) return;
        selectionFrame.gameObject.SetActive(selected);
        if (!selected) return;

        // Bingkai mengikuti kotak kontrol yang dipilih (dalam koordinat parent bingkai).
        selected.Rect.GetWorldCorners(corners);
        var parent = (RectTransform)selectionFrame.parent;
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        selectionFrame.anchorMin = selectionFrame.anchorMax = new Vector2(0.5f, 0.5f);
        selectionFrame.pivot = new Vector2(0.5f, 0.5f);
        selectionFrame.sizeDelta = max - min + new Vector2(16f, 16f);
        selectionFrame.localPosition = (min + max) * 0.5f;

        PlaceToolbar((corners[0].y + corners[2].y) * 0.5f);
    }

    // Toolbar di atas layar, kecuali kontrol yang dipilih juga di atas, supaya tidak tertutup.
    private void PlaceToolbar(float selectedWorldY) {
        if (!toolbar) return;
        var root = (RectTransform)canvas.transform;
        float localY = root.InverseTransformPoint(new Vector3(0f, selectedWorldY, 0f)).y;
        bool atBottom = localY > root.rect.center.y + root.rect.height * 0.1f;
        float y = atBottom ? 0f : 1f;
        toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0.5f, y);
        toolbar.anchoredPosition = new Vector2(0f, atBottom ? toolbarMargin : -toolbarMargin);
    }
}
