using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel DEALER MOBIL. Prefab: Assets/Resources/UI/CarDealerPanel.prefab
/// (dibangun Go Vista > Dealer Mobil > 2. Bangun Panel Dealer).
///
/// Kiri: grid kartu mobil (dibuat dari CarCatalog, satu kartu per entry, dari Card Template). Kanan: preview 3D
/// berputar, nama, bar Speed, Price, dan satu tombol aksi:
///  - Buy (hijau) kalau gold cukup, abu-abu dan tidak bisa ditekan kalau kurang;
///  - Bought! (kuning) + bunyi kaching sesaat setelah dibeli;
///  - Spawn (biru) untuk mobil yang sudah dimiliki: mobil muncul di parkiran depan dealer selagi panel masih menutup
///    layar, lalu panel tertutup sendiri.
/// Selama terbuka, gerak pemain dikunci (PlayerInputHub.SetPanelLocked), sama seperti popup Hadiah Login.
/// </summary>
public sealed class CarDealerPanel : MonoBehaviour {
    public const string ResourcePath = "UI/CarDealerPanel";

    [Serializable]
    public sealed class CardView {
        public Button button;
        public Image background;
        public Image thumbnail;
        public TMP_Text label;
    }

    [Header("Grid")]
    [Tooltip("Kartu contoh (nonaktif). Diklon untuk tiap mobil di katalog.")]
    [SerializeField] private RectTransform cardTemplate;
    [Tooltip("Posisi kartu ke-1..6 (kiri-atas ke kanan-bawah), relatif terhadap parent kartu.")]
    [SerializeField] private Vector2[] cardSlots = Array.Empty<Vector2>();
    [SerializeField] private Sprite cardNormal;
    [SerializeField] private Sprite cardSelected;

    [Header("Detail")]
    [SerializeField] private CarPreviewStage preview;
    [SerializeField] private RectTransform previewArea;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private RectTransform speedArea;
    [SerializeField] private Image[] speedSegments = Array.Empty<Image>();
    [SerializeField] private Color speedOnColor = Color.white;
    [SerializeField] private Color speedOffColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    [SerializeField] private RectTransform priceArea;
    [SerializeField] private TMP_Text priceLabel;

    [Header("Tombol aksi")]
    [SerializeField] private Button actionButton;
    [SerializeField] private Image actionImage;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Sprite buySprite;
    [SerializeField] private Sprite disabledSprite;
    [SerializeField] private Sprite boughtSprite;
    [SerializeField] private Sprite spawnSprite;
    [SerializeField] private Color buyTextColor = Color.white;
    [SerializeField] private Color disabledTextColor = new Color32(120, 130, 150, 255);
    [SerializeField] private Color boughtTextColor = new Color32(84, 52, 0, 255);
    [SerializeField] private Color spawnTextColor = new Color32(10, 50, 70, 255);
    [Tooltip("Lama tombol menampilkan 'Bought!' sebelum berubah jadi Spawn (detik).")]
    [SerializeField] private float boughtSeconds = 1.4f;

    [Header("Lainnya")]
    [SerializeField] private Button closeButton;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip kachingClip;
    [SerializeField] private GameObject toast;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastSeconds = 2.5f;
    [Tooltip("Jeda setelah mobil di-spawn sebelum panel tertutup (mobil muncul di balik panel).")]
    [SerializeField] private float closeAfterSpawnSeconds = 0.35f;
    [SerializeField] private CarDealerTutorial tutorial;

    private static CarDealerPanel _instance;

    private CarCatalog _catalog;
    private readonly List<CardView> _cards = new List<CardView>();
    private readonly List<CarCatalog.Entry> _cars = new List<CarCatalog.Entry>();
    private int _selected;
    private string _justBought;
    private bool _open;
    private bool _busy;
    private Coroutine _toastRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    public static bool IsOpen => _instance != null && _instance._open;

    /// <summary>Buka panel. withTutorial = tampilkan tutorial sekali (alur beli sampai spawn) di atas panel.</summary>
    public static void Show(CarCatalog catalog, bool withTutorial) {
        if (catalog == null || IsOpen || !EnsureInstance()) return;
        _instance.Open(catalog, withTutorial);
    }

    public static void HideIfOpen() {
        if (IsOpen) _instance.Close();
    }

    private static bool EnsureInstance() {
        if (_instance != null) return true;
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null) {
            Debug.LogWarning($"[CarDealer] Resources/{ResourcePath}.prefab tidak ada. " +
                             "Jalankan Go Vista > Dealer Mobil > 2. Bangun Panel Dealer.");
            return false;
        }
        _instance = Instantiate(prefab).GetComponent<CarDealerPanel>();
        _instance.name = prefab.name;
        DontDestroyOnLoad(_instance.gameObject);
        return true;
    }

    // ---- Dipakai CarDealerTutorial untuk menyorot bagian panel.
    public RectTransform FirstCard => _cards.Count > 0 ? (RectTransform)_cards[0].button.transform : null;
    public RectTransform PreviewArea => previewArea;
    public RectTransform SpeedArea => speedArea;
    public RectTransform PriceArea => priceArea;
    public RectTransform ActionArea => actionButton != null ? (RectTransform)actionButton.transform : null;

    private void Awake() {
        if (actionButton != null) actionButton.onClick.AddListener(OnAction);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
    }

    private void Open(CarCatalog catalog, bool withTutorial) {
        StopAllCoroutines();
        _open = true;
        _busy = false;
        _justBought = null;
        gameObject.SetActive(true);
        if (toast != null) toast.SetActive(false);
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(true);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(true);
        if (_catalog != catalog) {
            _catalog = catalog;
            BuildCards();
        }
        Select(Mathf.Clamp(_selected, 0, Mathf.Max(0, _cards.Count - 1)));
        if (withTutorial && tutorial != null) tutorial.Play(this, OnTutorialFinished);
    }

    private void OnTutorialFinished() {
        CarDealerService.MarkTutorialDone();
        Refresh();
    }

    private void Close() {
        StopAllCoroutines();
        if (tutorial != null) tutorial.Stop();
        _open = false;
        if (preview != null) preview.Hide();
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(false);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(false);
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Grid

    private void BuildCards() {
        foreach (CardView card in _cards)
            if (card.button != null) Destroy(card.button.gameObject);
        _cards.Clear();
        _cars.Clear();
        if (cardTemplate == null || _catalog == null) return;

        for (int i = 0; i < _catalog.cars.Length && i < cardSlots.Length; i++) {
            CarCatalog.Entry car = _catalog.cars[i];
            if (car == null) continue;
            RectTransform rect = Instantiate(cardTemplate, cardTemplate.parent);
            rect.name = "Card " + car.displayName;
            rect.anchoredPosition = cardSlots[i];
            rect.gameObject.SetActive(true);
            var view = new CardView {
                button = rect.GetComponent<Button>(),
                background = rect.GetComponent<Image>(),
                thumbnail = rect.Find("Preview/Thumbnail")?.GetComponent<Image>(),
                label = rect.Find("Name")?.GetComponent<TMP_Text>(),
            };
            if (view.label != null) view.label.text = car.displayName;
            if (view.thumbnail != null) {
                view.thumbnail.sprite = car.thumbnail;
                view.thumbnail.enabled = car.thumbnail != null;
            }
            int index = _cards.Count;
            view.button.onClick.AddListener(() => Select(index));
            _cards.Add(view);
            _cars.Add(car);
        }
    }

    private CarCatalog.Entry SelectedCar =>
        _selected >= 0 && _selected < _cars.Count ? _cars[_selected] : null;

    private void Select(int index) {
        if (_busy) return;
        _selected = index;
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i].background != null) _cards[i].background.sprite = i == index ? cardSelected : cardNormal;
        CarCatalog.Entry car = SelectedCar;
        if (preview != null) {
            if (car != null) preview.Show(car.previewModel); else preview.Hide();
        }
        Refresh();
    }

    // ---------------------------------------------------------------- Detail

    private void Refresh() {
        CarCatalog.Entry car = SelectedCar;
        if (nameLabel != null) nameLabel.text = car != null ? car.displayName : "";
        int fill = car != null ? CarDealerRules.SpeedFill(car.maxSpeedKmh) : 0;
        for (int i = 0; i < speedSegments.Length; i++)
            if (speedSegments[i] != null) speedSegments[i].color = i < fill ? speedOnColor : speedOffColor;
        if (priceLabel != null) priceLabel.text = car != null ? CarDealerRules.PriceLabel(car.price) : "";

        if (car == null) {
            ApplyButton(DealerButtonState.BuyDisabled);
            return;
        }
        bool owned = CarDealerService.IsOwned(car.id);
        ApplyButton(CarDealerRules.StateFor(owned, _justBought == car.id, car.price, CarDealerService.Balance));
    }

    /// <summary>Tampilkan tombol dalam keadaan tertentu (juga dipakai tutorial untuk memperagakan Buy/Spawn).</summary>
    public void ApplyButton(DealerButtonState state) {
        if (actionImage != null) {
            actionImage.sprite = state switch {
                DealerButtonState.Buy => buySprite,
                DealerButtonState.Bought => boughtSprite,
                DealerButtonState.Spawn => spawnSprite,
                _ => disabledSprite,
            };
        }
        if (actionLabel != null) {
            actionLabel.text = CarDealerRules.ButtonLabel(state);
            actionLabel.color = state switch {
                DealerButtonState.Buy => buyTextColor,
                DealerButtonState.Bought => boughtTextColor,
                DealerButtonState.Spawn => spawnTextColor,
                _ => disabledTextColor,
            };
        }
        // Buy abu-abu tidak bisa ditekan; Bought! juga tidak (sedang animasi).
        if (actionButton != null)
            actionButton.interactable = state == DealerButtonState.Buy || state == DealerButtonState.Spawn;
    }

    /// <summary>Kembalikan tombol ke keadaan sebenarnya (setelah tutorial memperagakan).</summary>
    public void RestoreButton() => Refresh();

    // ---------------------------------------------------------------- Aksi

    private void OnAction() {
        if (_busy || (tutorial != null && tutorial.IsPlaying)) return;
        CarCatalog.Entry car = SelectedCar;
        if (car == null) return;
        if (CarDealerService.IsOwned(car.id)) Spawn(car);
        else Buy(car);
    }

    private void Buy(CarCatalog.Entry car) {
        if (!CarDealerService.TryBuy(car)) {
            Refresh();
            return;
        }
        if (sfxSource != null && kachingClip != null) sfxSource.PlayOneShot(kachingClip);
        _justBought = car.id;
        Refresh();
        StartCoroutine(EndBoughtLater(car.id));
    }

    private IEnumerator EndBoughtLater(string carId) {
        yield return new WaitForSecondsRealtime(boughtSeconds);
        if (_justBought == carId) _justBought = null;
        Refresh();
    }

    private void Spawn(CarCatalog.Entry car) {
        VehicleDirector director = VehicleDirector.Instance;
        CarSpawnResult result = director != null ? director.SpawnLocalCar(car.drivablePrefab) : CarSpawnResult.Offline;
        if (result != CarSpawnResult.Spawned) {
            ShowToast(MessageFor(result));
            return;
        }
        _busy = true;
        if (actionButton != null) actionButton.interactable = false;
        StartCoroutine(CloseLater());
    }

    private IEnumerator CloseLater() {
        yield return new WaitForSecondsRealtime(closeAfterSpawnSeconds);
        Close();
    }

    private static string MessageFor(CarSpawnResult result) => result switch {
        CarSpawnResult.Offline => "Belum terhubung ke server. Coba lagi sebentar.",
        CarSpawnResult.NotInWorld => "Mobil hanya bisa dimunculkan di kota.",
        CarSpawnResult.NoParking => "Parkiran belum siap. Coba lagi sebentar.",
        CarSpawnResult.OldCarInUse => "Mobilmu yang lama sedang dinaiki. Tunggu semua penumpang turun dulu ya.",
        CarSpawnResult.ParkingBlocked => "Parkiran depan dealer sedang terhalang. Coba lagi sebentar.",
        _ => "Mobil gagal dimunculkan. Coba lagi.",
    };

    private void ShowToast(string message) {
        if (toast == null) return;
        if (toastText != null) toastText.text = message;
        toast.SetActive(true);
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(HideToastLater());
    }

    private IEnumerator HideToastLater() {
        yield return new WaitForSecondsRealtime(toastSeconds);
        if (toast != null) toast.SetActive(false);
        _toastRoutine = null;
    }
}
