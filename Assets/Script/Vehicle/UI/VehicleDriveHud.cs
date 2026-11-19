using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD menyetir. Dibangun dari kode saat pertama dipakai, sebagai child GameplayHUD di canvas MobileUI
/// (MobileControls.ControlsRoot), jadi ikut tersembunyi bersama kontrol lain saat menu/dialog/layar mati.
///
/// Kiri: roda setir ATAU tombol belok kiri/kanan (ATUR KONTROL > Kendaraan, pilihan Setir / Panah).
/// Kanan: pedal GAS dan REM (rem saat berhenti = mundur). Tengah bawah: kecepatan.
///
/// Setir (roda + panah sebagai satu grup), gas, dan rem adalah CustomizableControl kategori Kendaraan,
/// jadi posisi & ukurannya bisa diatur di ATUR KONTROL. Selama mode itu HUD ditampilkan sebagai preview
/// (BeginEditPreview / EndEditPreview) walau pemain tidak sedang menyetir.
/// </summary>
public class VehicleDriveHud : MonoBehaviour {
    private const string IconFolder = "UI/Icons/";
    private static readonly Color PanelColor = new Color32(13, 18, 34, 200);
    private static readonly Color GasColor = new Color32(52, 211, 153, 255);
    private static readonly Color BrakeColor = new Color32(248, 113, 113, 255);
    private static readonly Color SteerColor = new Color32(207, 216, 240, 255);

    private static VehicleDriveHud instance;

    private CarDriveController car;
    private GameObject wheelGroup;
    private GameObject buttonGroup;
    private Text speedText;
    private HoldButton left, right;
    private int shownSpeed = -1;
    private CustomizableControl steerControl, gasControl, brakeControl;
    private bool previewing;
    private DriveControlMode previewMode;

    /// Kontrol "Setir" (roda setir / panah). Saat dipilih di ATUR KONTROL, muncul pilihan Setir / Panah.
    public static CustomizableControl SteerControl => instance ? instance.steerControl : null;

    /// Kontrol yang bisa diatur di tab Kendaraan (kosong kalau canvas MobileUI belum ada).
    public static CustomizableControl[] EditableControls {
        get {
            EnsureBuilt();
            return instance ? new[] { instance.steerControl, instance.gasControl, instance.brakeControl }
                            : new CustomizableControl[0];
        }
    }

    /// True kalau HUD sedang tampil karena pemain menyetir (bukan preview ATUR KONTROL).
    public static bool IsDriving => instance && instance.car;

    /// Bangun HUD (tersembunyi) kalau belum ada, supaya kontrolnya bisa diatur sebelum pernah menyetir.
    public static void EnsureBuilt() {
        if (!instance) instance = Build();
    }

    /// ATUR KONTROL tab Kendaraan: tampilkan HUD walau tidak menyetir. mode = pilihan Setir / Panah yang dilihat.
    public static void BeginEditPreview(DriveControlMode mode) {
        EnsureBuilt();
        if (!instance) return;
        instance.previewing = true;
        instance.previewMode = mode;
        instance.gameObject.SetActive(true);
        instance.ApplyMode();
    }

    /// Ganti pilihan Setir / Panah yang dilihat selama preview (belum disimpan).
    public static void SetPreviewMode(DriveControlMode mode) {
        if (!instance || !instance.previewing) return;
        instance.previewMode = mode;
        instance.ApplyMode();
    }

    /// Selesai preview: kembali ke pilihan tersimpan, dan sembunyikan lagi kalau pemain tidak sedang menyetir.
    public static void EndEditPreview() {
        if (!instance || !instance.previewing) return;
        instance.previewing = false;
        instance.ApplyMode();
        if (!instance.car) instance.gameObject.SetActive(false);
    }

    public static void Show(CarDriveController drive) {
        EnsureBuilt();
        if (!instance) return;
        instance.car = drive;
        instance.gameObject.SetActive(true);
        instance.ApplyMode();
    }

    public static void Hide() {
        DriveInput.Reset();
        if (!instance) return;
        instance.car = null;
        if (!instance.previewing) instance.gameObject.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    void OnEnable() => GameSettings.DriveControlChanged += ApplyMode;

    void OnDisable() {
        GameSettings.DriveControlChanged -= ApplyMode;
        DriveInput.Reset();
    }

    void Update() {
        if (!car) return;
        int kmh = Mathf.RoundToInt(car.SpeedKmh);
        if (kmh == shownSpeed) return;
        shownSpeed = kmh;
        speedText.text = kmh + " <size=26>km/j</size>";
    }

    private void ApplyMode() {
        DriveControlMode mode = previewing ? previewMode : GameSettings.DriveControl;
        bool wheel = mode == DriveControlMode.SteeringWheel;
        DriveInput.UiSteer = 0f;
        wheelGroup.SetActive(wheel);
        buttonGroup.SetActive(!wheel);
    }

    private void UpdateButtonSteer(bool _) {
        DriveInput.UiSteer = (right.IsHeld ? 1f : 0f) - (left.IsHeld ? 1f : 0f);
    }

    // ---------------------------------------------------------------- Build

    private static VehicleDriveHud Build() {
        RectTransform parent = MobileControls.ControlsRoot;
        if (!parent) {
            Debug.LogWarning("[VehicleDriveHud] Canvas MobileUI belum ada, HUD menyetir tidak bisa ditampilkan.");
            return null;
        }

        RectTransform root = NewRect("Driving HUD", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        // Di atas TouchPanel (geser kamera) tapi di bawah tombol lain, termasuk Interact (Keluar).
        root.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));
        var hud = root.gameObject.AddComponent<VehicleDriveHud>();

        // ---- Kiri: setir (roda setir dan tombol belok dalam satu kotak, diatur bersama di ATUR KONTROL)
        RectTransform steer = Place(NewRect("Vehicle Steer", root), Vector2.zero, new Vector2(330f, 270f), new Vector2(440f, 380f));
        hud.wheelGroup = NewRect("Steering Wheel Group", steer).gameObject;
        Stretch((RectTransform)hud.wheelGroup.transform);
        RectTransform wheel = Place(NewRect("Steering Wheel", hud.wheelGroup.transform), Center, Vector2.zero, new Vector2(380f, 380f));
        Image wheelImage = wheel.gameObject.AddComponent<Image>();
        wheelImage.sprite = LoadIcon("icon-steering", "ui-circle");
        wheelImage.preserveAspect = true;
        wheelImage.color = SteerColor;
        wheel.gameObject.AddComponent<SteeringWheelControl>();

        // ---- Kiri: tombol belok
        hud.buttonGroup = NewRect("Steer Buttons Group", steer).gameObject;
        Stretch((RectTransform)hud.buttonGroup.transform);
        hud.left = SteerButton("Steer Left Button", hud.buttonGroup.transform, new Vector2(-130f, -40f), flip: true);
        hud.right = SteerButton("Steer Right Button", hud.buttonGroup.transform, new Vector2(110f, -40f), flip: false);
        hud.left.Changed += hud.UpdateButtonSteer;
        hud.right.Changed += hud.UpdateButtonSteer;

        // ---- Kanan: pedal
        HoldButton gas = Pedal("Gas Pedal", root, new Vector2(-160f, 230f), new Vector2(170f, 260f), GasColor, "GAS");
        gas.Changed += held => DriveInput.UiGas = held;
        HoldButton brake = Pedal("Brake Pedal", root, new Vector2(-360f, 155f), new Vector2(160f, 170f), BrakeColor, "REM");
        brake.Changed += held => DriveInput.UiBrake = held;

        // Ditambahkan paling akhir: CustomizableControl mencatat ukuran semua child sebagai ukuran default.
        hud.steerControl = steer.gameObject.AddComponent<CustomizableControl>();
        hud.steerControl.Init("VehicleSteer", "Setir", ControlCategory.Vehicle);
        hud.gasControl = gas.gameObject.AddComponent<CustomizableControl>();
        hud.gasControl.Init("VehicleGas", "Gas", ControlCategory.Vehicle);
        hud.brakeControl = brake.gameObject.AddComponent<CustomizableControl>();
        hud.brakeControl.Init("VehicleBrake", "Rem", ControlCategory.Vehicle);

        // ---- Tengah bawah: kecepatan
        RectTransform speedPanel = Place(NewRect("Speed Panel", root), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(240f, 84f));
        Image panel = speedPanel.gameObject.AddComponent<Image>();
        panel.sprite = Resources.Load<Sprite>("UI/ui-rounded");
        panel.type = Image.Type.Sliced;
        panel.color = PanelColor;
        panel.raycastTarget = false;
        hud.speedText = NewText("Speed", speedPanel, "0 <size=26>km/j</size>", 44, Color.white);
        hud.speedText.supportRichText = true;

        root.gameObject.SetActive(false);
        return hud;
    }

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private static HoldButton SteerButton(string name, Transform parent, Vector2 position, bool flip) {
        RectTransform rect = Place(NewRect(name, parent), Center, position, new Vector2(190f, 190f));
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UI/ui-circle");
        bg.preserveAspect = true;
        bg.color = PanelColor;

        RectTransform arrow = NewRect(name + " Arrow", rect);
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.sizeDelta = new Vector2(96f, 96f);
        arrow.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        Image icon = arrow.gameObject.AddComponent<Image>();
        icon.sprite = LoadIcon("icon-arrow-right", "icon-chevron-right");
        icon.preserveAspect = true;
        icon.color = SteerColor;
        icon.raycastTarget = false;
        return rect.gameObject.AddComponent<HoldButton>();
    }

    private static HoldButton Pedal(string name, Transform parent, Vector2 position, Vector2 size, Color color, string label) {
        RectTransform rect = Place(NewRect(name, parent), new Vector2(1f, 0f), position, size);
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UI/ui-rounded");
        bg.type = Image.Type.Sliced;
        bg.color = PanelColor;

        RectTransform grip = NewRect(name + " Grip", rect);
        grip.anchorMin = new Vector2(0.18f, 0.28f);
        grip.anchorMax = new Vector2(0.82f, 0.92f);
        grip.offsetMin = grip.offsetMax = Vector2.zero;
        Image gripImage = grip.gameObject.AddComponent<Image>();
        gripImage.sprite = LoadIcon("icon-pedal", "ui-rounded");
        gripImage.color = color;
        gripImage.raycastTarget = false;

        RectTransform text = NewText(name + " Label", rect, label, 28, color).rectTransform;
        text.anchorMin = new Vector2(0f, 0f);
        text.anchorMax = new Vector2(1f, 0.28f);
        text.offsetMin = text.offsetMax = Vector2.zero;
        return rect.gameObject.AddComponent<HoldButton>();
    }

    private static Sprite LoadIcon(string name, string fallback) {
        Sprite sprite = Resources.Load<Sprite>(IconFolder + name);
        if (sprite) return sprite;
        sprite = Resources.Load<Sprite>(IconFolder + fallback);
        return sprite ? sprite : Resources.Load<Sprite>("UI/" + fallback);
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text NewText(string name, RectTransform parent, string content, int size, Color color) {
        RectTransform rect = NewRect(name, parent);
        Stretch(rect);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }
}
