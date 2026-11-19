using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mode ATUR KONTROL: pemain menggeser tombol mobile dan mengubah ukurannya.
/// Pasang di objek "Control Layout Editor" (child terakhir MobileUI, full-screen, Image transparan
/// dengan Raycast Target ON). Selama mode ini terbuka, semua sentuhan ditangkap objek ini, jadi
/// joystick/tombol tidak ikut tertekan.
///
/// Dua tab: Karakter (joystick, lompat, lari, interaksi) dan Kendaraan (HUD menyetir: setir, gas, rem,
/// lihat VehicleDriveHud). Saat kontrol Setir dipilih, muncul pilihan Setir / Panah (tipe kontrol belok,
/// GameSettings.DriveControl); pilihan itu ikut disimpan dengan SIMPAN dan dibatalkan dengan BATAL.
///
/// Dibuka dari Pengaturan > Atur kontrol, di lobby (latar hitam) dan di menu pause (LobyUISceneController).
/// Semua tampilan (warna, teks, posisi toolbar) bisa diedit langsung di prefab MobileUI
/// (dibangun Go Vista > Desain Baru > Atur Kontrol).
/// </summary>
public class ControlLayoutEditor : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    public static bool IsOpen { get; private set; }

    [Tooltip("Kontrol karakter yang bisa diatur. Kosong = semua CustomizableControl di canvas ini. " +
             "Kontrol kendaraan (VehicleDriveHud) selalu ditambahkan otomatis.")]
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

    [Header("Tab Karakter / Kendaraan")]
    [SerializeField] private Button characterTab;
    [SerializeField] private Button vehicleTab;
    [Tooltip("Garis tepi cyan tab yang aktif.")]
    [SerializeField] private GameObject characterTabOn;
    [SerializeField] private GameObject vehicleTabOn;
    [Tooltip("Ikon + teks tiap tab, diwarnai cyan saat aktif dan abu terang saat tidak.")]
    [SerializeField] private Graphic[] characterTabContent;
    [SerializeField] private Graphic[] vehicleTabContent;
    [SerializeField] private Color tabOnContentColor = new Color32(103, 232, 249, 255);
    [SerializeField] private Color tabOffContentColor = new Color32(207, 216, 240, 255);

    [Header("Pilihan Setir / Panah (muncul saat kontrol Setir dipilih)")]
    [SerializeField] private GameObject steerModeToggle;
    [SerializeField] private Button steerWheelButton;
    [SerializeField] private Button steerArrowButton;
    [SerializeField] private GameObject steerWheelOn;
    [SerializeField] private GameObject steerArrowOn;
    [SerializeField] private Graphic[] steerWheelContent;
    [SerializeField] private Graphic[] steerArrowContent;
    [SerializeField] private Color steerOnContentColor = new Color32(4, 34, 43, 255);
    [SerializeField] private Color steerOffContentColor = new Color32(207, 216, 240, 255);

    [Tooltip("Opsional: tombol Interact biasanya tersembunyi, jadi ditampilkan selama mode ini.")]
    [SerializeField] private InteractButtonUI interactButton;

    private readonly List<CustomizableControl> _all = new List<CustomizableControl>();
    // Kontrol tab lain yang disembunyikan selama mode ini (diaktifkan lagi saat pindah tab / ditutup).
    private readonly List<CustomizableControl> _hiddenByEditor = new List<CustomizableControl>();
    private ControlCategory _category;
    private DriveControlMode _pendingDriveMode;
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
        if (characterTab) characterTab.onClick.AddListener(() => ShowCategory(ControlCategory.Character));
        if (vehicleTab) vehicleTab.onClick.AddListener(() => ShowCategory(ControlCategory.Vehicle));
        if (steerWheelButton) steerWheelButton.onClick.AddListener(() => SetDriveMode(DriveControlMode.SteeringWheel));
        if (steerArrowButton) steerArrowButton.onClick.AddListener(() => SetDriveMode(DriveControlMode.Buttons));
    }

    void OnDisable() {
        // Misalnya keluar room saat mode ini masih terbuka.
        if (!IsOpen) return;
        IsOpen = false;
        RestoreControls();
    }

    /// <param name="closed">Dipanggil setelah Simpan atau Batal.</param>
    /// <param name="opaqueBackground">True = latar hitam pekat (lobby), false = gelap tipis (in-game).</param>
    public void Open(Action closed, bool opaqueBackground = false) {
        onClosed = closed;
        // HUD menyetir dibangun dulu, supaya ikut dikumpulkan ControlEditRings saat objek ini aktif.
        VehicleDriveHud.EnsureBuilt();
        gameObject.SetActive(true);   // Awake pertama kali jalan di sini
        CollectControls();
        transform.SetAsLastSibling();
        if (TryGetComponent(out EditModeBackdrop backdrop)) backdrop.SetOpaque(opaqueBackground);
        IsOpen = true;
        dragging = false;
        _pendingDriveMode = GameSettings.DriveControl;
        if (interactButton) interactButton.SetPreview(true);
        MobileControls.SetEditing(true);
        foreach (CustomizableControl c in _all) c.ApplySaved();
        // Sedang menyetir: langsung buka tab Kendaraan.
        ShowCategory(VehicleDriveHud.IsDriving ? ControlCategory.Vehicle : ControlCategory.Character);
    }

    /// Tutup tanpa menyimpan (tombol BATAL / tombol Back HP).
    public void Cancel() {
        foreach (CustomizableControl c in _all) c.ApplySaved();
        Close();
    }

    private void SaveLayout() {
        foreach (CustomizableControl c in _all) c.SaveCurrent();
        ControlLayoutSettings.Save();
        GameSettings.DriveControl = _pendingDriveMode;
        GameSettings.Save();
        Close();
    }

    private void ResetLayout() {
        foreach (CustomizableControl c in _all) c.ResetToDefault();
        Select(selected);
    }

    private void Close() {
        IsOpen = false;
        dragging = false;
        RestoreControls();
        gameObject.SetActive(false);
        Action closed = onClosed;
        onClosed = null;
        closed?.Invoke();
    }

    // Kontrol karakter dari prefab + kontrol kendaraan yang dibuat VehicleDriveHud dari kode.
    private void CollectControls() {
        _all.Clear();
        foreach (CustomizableControl c in controls)
            if (c && !_all.Contains(c)) _all.Add(c);
        foreach (CustomizableControl c in VehicleDriveHud.EditableControls)
            if (c && !_all.Contains(c)) _all.Add(c);
    }

    private void RestoreControls() {
        foreach (CustomizableControl c in _hiddenByEditor)
            if (c) c.gameObject.SetActive(true);
        _hiddenByEditor.Clear();
        VehicleDriveHud.EndEditPreview();
        MobileControls.SetEditing(false);
        if (interactButton) interactButton.SetPreview(false);
    }

    private void ShowCategory(ControlCategory category) {
        _category = category;
        dragging = false;

        // HUD menyetir dulu, karena aktif/tidaknya kontrol kendaraan ikut HUD itu.
        if (category == ControlCategory.Vehicle) VehicleDriveHud.BeginEditPreview(_pendingDriveMode);
        else VehicleDriveHud.EndEditPreview();

        foreach (CustomizableControl c in _all) {
            if (!c) continue;
            if (c.Category == category) {
                if (_hiddenByEditor.Remove(c)) c.gameObject.SetActive(true);
            } else if (c.gameObject.activeSelf) {
                c.gameObject.SetActive(false);
                _hiddenByEditor.Add(c);
            }
        }

        SetTabVisual(characterTabOn, characterTabContent, category == ControlCategory.Character,
                     tabOnContentColor, tabOffContentColor);
        SetTabVisual(vehicleTabOn, vehicleTabContent, category == ControlCategory.Vehicle,
                     tabOnContentColor, tabOffContentColor);
        Select(FirstVisible());
    }

    private CustomizableControl FirstVisible() {
        foreach (CustomizableControl c in _all)
            if (IsSelectable(c)) return c;
        return null;
    }

    private bool IsSelectable(CustomizableControl c) =>
        c && c.Category == _category && c.gameObject.activeInHierarchy;

    private void SetDriveMode(DriveControlMode mode) {
        _pendingDriveMode = mode;
        VehicleDriveHud.SetPreviewMode(mode);
        RefreshSelection();
    }

    private static void SetTabVisual(GameObject on, Graphic[] content, bool active, Color onColor, Color offColor) {
        if (on) on.SetActive(active);
        if (content == null) return;
        foreach (Graphic g in content)
            if (g) g.color = active ? onColor : offColor;
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
        for (int i = _all.Count - 1; i >= 0; i--) {
            CustomizableControl c = _all[i];
            if (IsSelectable(c) && RectTransformUtility.RectangleContainsScreenPoint(c.Rect, screenPosition, cam))
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
        RefreshSteerToggle();

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

    private void RefreshSteerToggle() {
        bool show = selected && selected == VehicleDriveHud.SteerControl;
        if (steerModeToggle && steerModeToggle.activeSelf != show) steerModeToggle.SetActive(show);
        if (!show) return;
        bool wheel = _pendingDriveMode == DriveControlMode.SteeringWheel;
        SetTabVisual(steerWheelOn, steerWheelContent, wheel, steerOnContentColor, steerOffContentColor);
        SetTabVisual(steerArrowOn, steerArrowContent, !wheel, steerOnContentColor, steerOffContentColor);
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
