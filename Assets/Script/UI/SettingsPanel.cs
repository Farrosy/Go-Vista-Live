using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dialog PENGATURAN yang dipakai bersama oleh menu utama lobby dan menu pause di dalam game.
/// Satu prefab: Assets/Resources/UI/SettingsPanel.prefab (dibuat lewat Go Vista > Desain Baru > Panel Pengaturan),
/// dipasang sebagai nested prefab di LobbyCanvas ("Settings Overlay") dan RoomWaitingCanvas (menu pause).
/// Ubah desainnya sekali di prefab itu, kedua tempat ikut berubah.
///
/// Panel ini mengurus semua isi dialog sendiri (sensitivitas kamera, info akun, simpan saat ditutup).
/// Pemakai hanya memanggil Open() dengan dua callback untuk hal yang memang beda per tempat:
///  - controlsRequested : tombol "Atur kontrol" (lobby menyembunyikan menu, pause menyembunyikan overlay pause)
///  - closed            : SELESAI / tombol × (lobby menutup overlay, pause kembali ke menu pause)
/// </summary>
public sealed class SettingsPanel : MonoBehaviour {
    /// <summary>Path Resources untuk Resources.Load / pembuat UI dari kode.</summary>
    public const string ResourcePath = "UI/SettingsPanel";

    [Header("Kamera")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Text sensitivityValue;
    [SerializeField] private string sensitivityFormat = "0.0";

    [Header("Akun")]
    [Tooltip("Opsional. Diisi \"Masuk sebagai <nama akun>\".")]
    [SerializeField] private Text accountLabel;
    [SerializeField] private string accountPrefix = "Masuk sebagai  ";

    [Header("Mobil")]
    [Tooltip("Pilihan kontrol belok: roda setir.")]
    [SerializeField] private Button driveWheelButton;
    [Tooltip("Pilihan kontrol belok: tombol kiri/kanan.")]
    [SerializeField] private Button driveButtonsButton;

    // Sama dengan RedesignUiKit.BlueColor / GhostColor (Editor-only, jadi disalin di sini).
    private static readonly Color SelectedColor = new Color32(79, 140, 255, 255);
    private static readonly Color UnselectedColor = new Color32(28, 37, 64, 255);

    [Header("Tombol")]
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button doneButton;
    [Tooltip("Opsional. Tombol × di pojok.")]
    [SerializeField] private Button closeButton;

    private Action _controlsRequested;
    private Action _closed;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>Dipakai pembuat prefab (Editor) untuk mengisi referensi.</summary>
    public void Init(Slider slider, Text value, Text account, Button controls, Button done, Button close) {
        sensitivitySlider = slider;
        sensitivityValue = value;
        accountLabel = account;
        controlsButton = controls;
        doneButton = done;
        closeButton = close;
    }

    /// <summary>Dipakai pembuat prefab (Editor) untuk baris "Kontrol mobil".</summary>
    public void InitDriveControls(Button wheel, Button buttons) {
        driveWheelButton = wheel;
        driveButtonsButton = buttons;
    }

    private void Awake() {
        if (driveWheelButton != null)
            driveWheelButton.onClick.AddListener(() => SetDriveControl(DriveControlMode.SteeringWheel));
        if (driveButtonsButton != null)
            driveButtonsButton.onClick.AddListener(() => SetDriveControl(DriveControlMode.Buttons));
        if (sensitivitySlider != null) {
            sensitivitySlider.minValue = GameSettings.MinCameraSensitivity;
            sensitivitySlider.maxValue = GameSettings.MaxCameraSensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        if (controlsButton != null) controlsButton.onClick.AddListener(RequestControls);
        if (doneButton != null) doneButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable() {
        LoginUIController.AccountChanged += RefreshAccount;
        Refresh();
    }

    private void OnDisable() {
        LoginUIController.AccountChanged -= RefreshAccount;
    }

    /// <summary>Tampilkan panel dengan nilai terbaru.</summary>
    /// <param name="controlsRequested">Dipanggil saat "Atur kontrol" ditekan (pengaturan sudah disimpan).</param>
    /// <param name="closed">Dipanggil setelah SELESAI / × (pengaturan sudah disimpan, panel sudah disembunyikan).</param>
    public void Open(Action controlsRequested, Action closed) {
        _controlsRequested = controlsRequested;
        _closed = closed;
        gameObject.SetActive(true);
        Refresh();
    }

    /// <summary>Simpan lalu sembunyikan (SELESAI / × / tombol Back HP).</summary>
    public void Close() {
        GameSettings.Save();
        gameObject.SetActive(false);
        Action closed = _closed;
        _closed = null;
        _controlsRequested = null;
        closed?.Invoke();
    }

    /// <summary>Sembunyikan tanpa callback, misalnya saat menu pause ditutup paksa (keluar room).</summary>
    public void Hide() {
        GameSettings.Save();
        _closed = null;
        _controlsRequested = null;
        gameObject.SetActive(false);
    }

    public void Refresh() {
        // SetValueWithoutNotify supaya membuka panel tidak dianggap mengubah pengaturan.
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(GameSettings.CameraSensitivity);
        UpdateSensitivityLabel();
        RefreshDriveControl();
        RefreshAccount();
    }

    private void SetDriveControl(DriveControlMode mode) {
        GameSettings.DriveControl = mode;
        RefreshDriveControl();
    }

    private void RefreshDriveControl() {
        bool wheel = GameSettings.DriveControl == DriveControlMode.SteeringWheel;
        Highlight(driveWheelButton, wheel);
        Highlight(driveButtonsButton, !wheel);
    }

    private static void Highlight(Button button, bool selected) {
        if (button == null || button.targetGraphic == null)
            return;
        button.targetGraphic.color = selected ? SelectedColor : UnselectedColor;
    }

    private void RequestControls() {
        GameSettings.Save();
        _controlsRequested?.Invoke();
    }

    private void OnSensitivityChanged(float value) {
        GameSettings.CameraSensitivity = value;
        UpdateSensitivityLabel();
    }

    private void UpdateSensitivityLabel() {
        if (sensitivityValue != null)
            sensitivityValue.text = GameSettings.CameraSensitivity.ToString(sensitivityFormat) + "x";
    }

    private void RefreshAccount() {
        if (accountLabel == null)
            return;
        if (!AuthService.IsSignedIn) {
            accountLabel.text = "";
            return;
        }
        // Username hanya huruf/angka/. - @ _ (AuthService), jadi aman dipakai di dalam rich text.
        accountLabel.text = accountPrefix + "<b><color=#FFFFFF>" + AuthService.DisplayName + "</color></b>";
    }
}
