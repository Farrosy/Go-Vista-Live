using System;
using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Peta kota layar penuh: peta di kiri, panel samping di kanan (tujuan + daftar tempat penting).
///  - Utara selalu di atas. Seret = geser, cubit / scroll = zoom, tombol + / − juga ada.
///  - Ketuk peta = pasang tujuan sendiri (GpsService); ketuk tujuan itu lagi atau "Hapus" = lepas.
///  - Ketuk tempat di daftar = pasang tujuan ke sana dan geser peta ke tempat itu.
///  - Menampilkan semua blip radar (pemain, tempat penting, tujuan sendiri), tujuan quest, dan rute GPS.
///  - Laut: di luar hasil scan diisi warna laut yang sama, dan ombak animasi mengikuti garis pantai
///    (MinimapShoreData, di-bake dari hasil scan; lihat MinimapShoreBaker).
/// Gambar peta = hasil scan Minimap Scanner di scene aktif (sama dengan radar), jadi tidak ada kamera tambahan.
/// Mode venue (KPOP Arena / Community, ada VenueMapData): tanpa tujuan / GPS; daftar berisi area di dalam ruangan,
/// ketuk area = sorot + geser ke sana; kartu atas = "Kamu di"; label BALKON di lantai atas (VenueMapOverlay).
/// Dibuka dengan mengetuk radar (ada lencana "perbesar" di bawah radar) atau tombol M;
/// ditutup dengan tombol X, Esc / Back, atau M lagi.
/// Komponen ini ada di "Viewport" peta (area yang menerima sentuhan). Dipasang oleh GameHudBuilder.FullMap.cs.
public sealed class GoVistaFullMap : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IScrollHandler {
    private const float SlowTickSeconds = 0.5f;

    public static GoVistaFullMap Current { get; private set; }
    public static bool IsOpen => Current != null && Current._open;

    [Header("Susunan HUD")]
    [SerializeField] private Canvas panelCanvas;
    [Tooltip("CanvasGroup di akar panel, untuk animasi buka / tutup.")]
    [SerializeField] private CanvasGroup panelGroup;
    [Tooltip("Latar viewport = warna laut di luar hasil scan.")]
    [SerializeField] private Image seaBackground;
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RawImage wavesImage;
    [SerializeField] private UiPolyline routeLine;
    [SerializeField] private RectTransform iconLayer;
    [Tooltip("Contoh ikon (non-aktif). Digandakan untuk tiap blip radar.")]
    [SerializeField] private Image iconTemplate;
    [SerializeField] private RectTransform questIcon;
    [Tooltip("Panel kanan. Peta memusatkan diri di area yang tidak tertutup panel ini.")]
    [SerializeField] private RectTransform sidePanel;
    [Tooltip("Tombol zoom / posisiku di tepi kanan peta (ikut bergeser saat animasi buka).")]
    [SerializeField] private RectTransform mapControls;

    [Header("Kartu tujuan")]
    [SerializeField] private Image destinationIconBackground;
    [SerializeField] private Image destinationIcon;
    [SerializeField] private TextMeshProUGUI destinationCaption;
    [SerializeField] private TextMeshProUGUI destinationTitle;
    [Tooltip("Baris bawah kartu: jarak rute, atau petunjuk kalau belum ada rute.")]
    [SerializeField] private TextMeshProUGUI destinationHint;
    [SerializeField] private Sprite pinSprite;
    [SerializeField] private Sprite flagSprite;

    [Header("Tempat penting")]
    [SerializeField] private RectTransform legendContent;
    [Tooltip("Contoh baris (non-aktif): Button dengan anak \"Icon\" (Image) dan \"Label\" (TMP).")]
    [SerializeField] private Button legendRowTemplate;

    [Header("Tombol")]
    [Tooltip("Area transparan di atas radar HUD; ketuk untuk membuka peta.")]
    [SerializeField] private Canvas openButtonCanvas;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button centerButton;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    [Header("Venue (KPOP Arena / Community)")]
    [SerializeField] private VenueMapOverlay venueOverlay;
    [Tooltip("Judul panel (\"Peta Kota\"), diganti nama venue.")]
    [SerializeField] private TextMeshProUGUI panelTitle;
    [Tooltip("Judul daftar (\"TEMPAT PENTING\"), diganti \"AREA DI DALAM\".")]
    [SerializeField] private TextMeshProUGUI sectionLabel;
    [SerializeField] private TextMeshProUGUI gestureHint;
    [Tooltip("Ikon baris area: lingkaran yang diwarnai warna area.")]
    [SerializeField] private Sprite areaSprite;
    [SerializeField] private Sprite hereSprite;
    [SerializeField] private Color hereColor = new Color32(54, 196, 186, 255);
    [Tooltip("Zoom maksimal di venue (ruangan kecil, jadi boleh jauh lebih dekat dari peta kota).")]
    [SerializeField] private float venueMaxZoom = 40f;
    [Tooltip("Zoom saat dibuka = zoom pas-layar x angka ini (scan 60 m, ruangan ±43 m).")]
    [SerializeField] private float venueOpenZoomFactor = 1.3f;

    [Header("Laut dan ombak")]
    [Tooltip("Hasil bake garis pantai. Kosong = tanpa ombak, laut memakai warna latar bawaan.")]
    [SerializeField] private MinimapShoreData shore;

    [Header("Zoom (piksel per meter, referensi 1080p)")]
    [SerializeField] private float openZoom = 3f;
    [SerializeField] private float maxZoom = 8f;
    [SerializeField] private float buttonZoomStep = 1.5f;
    [SerializeField] private float scrollZoomStep = 1.15f;

    [Header("Tampilan")]
    [Tooltip("Ikon di peta penuh = ukuran blip di radar x angka ini.")]
    [SerializeField] private float iconScale = 1.25f;
    [SerializeField] private float routePixels = 9f;
    [Tooltip("Ketukan sejauh ini (px) dari tujuan sendiri = hapus tujuan itu.")]
    [SerializeField] private float tapRemoveRadius = 44f;
    [SerializeField] private float openSeconds = 0.22f;
    [SerializeField] private float closeSeconds = 0.14f;
    [Tooltip("Jarak geser panel samping (px) saat animasi buka / tutup.")]
    [SerializeField] private float slideDistance = 72f;
    [SerializeField] private Color customColor = new Color32(240, 84, 96, 255);
    [SerializeField] private Color questColor = new Color32(255, 196, 61, 255);

    [Header("Pemberi quest utama")]
    [Tooltip("Pin quest baru di peta penuh (warna penuh, tidak di-tint). Ujung bawah pin = posisi pemberi. " +
             "Ketuk pin / baris quest di daftar = jadikan tujuan (GPS).")]
    [SerializeField] private Sprite giverMapSprite;
    [SerializeField] private float giverMapSize = 40f;
    [Tooltip("Titik pivot pin = ujung bawahnya (y dari bawah, 0–1).")]
    [SerializeField] private float giverPivotY = 0.14f;
    [SerializeField] private Color giverColor = new Color32(255, 160, 58, 255);
    [SerializeField] private Color idleColor = new Color32(120, 136, 160, 255);
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color rowSelectedColor = new Color32(54, 196, 186, 90);

    private RectTransform _viewport;
    private RectTransform _giverIcon;
    private bool _open;
    private float _anim;               // 0 = tertutup, 1 = terbuka penuh
    private float _zoom;
    private Vector2 _focus;            // posisi dunia (x, z) yang tampil di titik fokus (tengah area peta yang terlihat)
    private Vector2? _panTarget;       // geser halus ke tempat yang dipilih dari daftar
    private Vector2 _mapCorner;        // posisi dunia (x, z) sudut kiri bawah hasil scan
    private float _mapSize;            // lebar = tinggi hasil scan (meter)
    private bool _wavesReady;
    private Material _wavesMaterial;
    private Vector2 _sidePanelRest;
    private Vector2 _controlsRest;
    private bool _gestureHadPinch;
    private float _pinchDistance;
    private float _nextSlowTick;
    private bool _available;
    private readonly List<Image> _icons = new List<Image>();
    private readonly List<MinimapBlip> _sortedBlips = new List<MinimapBlip>();
    private readonly List<Vector2> _routeLocal = new List<Vector2>();
    private readonly List<LegendRow> _rows = new List<LegendRow>();
    private readonly Vector3[] _corners = new Vector3[4];
    private VenueMapData _venue;
    private Color _seaColor;
    private string _cityTitle, _citySection, _cityHint;
    private Func<Vector2, Vector2> _worldToLocal; // disimpan sekali: method group baru tiap frame = alokasi

    private struct LegendRow {
        public Button Button;
        public Image Background;
        public MinimapBlip Blip;
        /// Indeks area venue (-1 = baris tempat penting pulau).
        public int Area;
        /// Baris pemberi quest utama (paling atas daftar).
        public bool Giver;
    }

    private static readonly Comparison<MinimapBlip> ByOrder = (a, b) => a.Item.orderInLayer.CompareTo(b.Item.orderInLayer);

    private void Awake() {
        Current = this;
        _viewport = (RectTransform)transform;
        _worldToLocal = WorldToLocal2;
        _sidePanelRest = sidePanel.anchoredPosition;
        _controlsRest = mapControls.anchoredPosition;
        panelCanvas.enabled = false;
        iconTemplate.gameObject.SetActive(false);
        _giverIcon = CreateGiverIcon();
        legendRowTemplate.gameObject.SetActive(false);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        clearButton.onClick.AddListener(GpsService.ClearCustomWaypoint);
        centerButton.onClick.AddListener(() => PanTo(PlayerPosition()));
        zoomInButton.onClick.AddListener(() => ZoomAroundLocal(FocusLocal, buttonZoomStep));
        zoomOutButton.onClick.AddListener(() => ZoomAroundLocal(FocusLocal, 1f / buttonZoomStep));

        if (shore != null) seaBackground.color = shore.waterColor;
        _seaColor = seaBackground.color;
        _cityTitle = panelTitle != null ? panelTitle.text : "";
        _citySection = sectionLabel != null ? sectionLabel.text : "";
        _cityHint = gestureHint != null ? gestureHint.text : "";
        // Salinan material per peta, supaya mengatur parameter ombak tidak mengubah aset material.
        if (wavesImage.material != null && wavesImage.material != Canvas.GetDefaultCanvasMaterial())
            wavesImage.material = _wavesMaterial = new Material(wavesImage.material);
    }

    private void OnDestroy() {
        if (Current == this) Current = null;
        if (_open) Close();
        if (_wavesMaterial != null) Destroy(_wavesMaterial);
    }

    private void Update() {
        if (Time.unscaledTime >= _nextSlowTick) {
            _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
            // Peta penuh hanya ada kalau radar menampilkan peta (WorldScene; bukan lobi atau venue).
            _available = GoVistaMinimap.Current != null && GoVistaMinimap.Current.IsShowing &&
                         FindAnyObjectByType<MinimapScanner>() != null;
        }
        PlayerInputHub hub = PlayerInputHub.Instance;
        bool blocked = hub != null && hub.OverlayActive;
        openButtonCanvas.enabled = _available && !PlayerInputHub.IsDeathLocked && !GameplayHud.Hidden;

        if (_open) {
            if (!_available || blocked || PlayerController.Local == null || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(toggleKey)) {
                Close();
            } else {
                HandlePinch();
                UpdatePan();
            }
        } else if (Input.GetKeyDown(toggleKey) && (hub == null || !hub.InputLocked)) {
            // InputLocked juga menutup jalan saat sedang mengetik chat atau panel lain terbuka.
            Open();
        }

        Animate();
        if (panelCanvas.enabled) Layout();
    }

    // ------------------------------------------------------------ Buka / tutup

    public void Open() {
        if (_open || !_available || PlayerController.Local == null) return;
        PlayerInputHub hub = PlayerInputHub.Instance;
        if (hub != null && hub.OverlayActive) return;
        if (!BindScan()) return;

        _open = true;
        panelCanvas.enabled = true;
        panelGroup.blocksRaycasts = true;
        if (hub != null) hub.SetPanelLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RebuildLegend();
        _panTarget = null;
        if (_venue != null) {
            // Ruangan kecil: buka dengan seluruh ruangan terlihat, dipusatkan di tengah ruangan.
            _zoom = Mathf.Clamp(MinZoom * venueOpenZoomFactor, MinZoom, MaxZoom);
            _focus = _mapCorner + Vector2.one * (_mapSize * 0.5f);
        } else {
            _zoom = Mathf.Clamp(openZoom, MinZoom, MaxZoom);
            _focus = PlayerPosition();
        }
        ClampFocus();
    }

    public void Close() {
        if (!_open) return;
        _open = false;
        _panTarget = null;
        panelGroup.blocksRaycasts = false; // canvas tetap tampil sampai animasi tutup selesai
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(false);
    }

    private void Animate() {
        float goal = _open ? 1f : 0f;
        if (Mathf.Approximately(_anim, goal)) {
            if (!_open && panelCanvas.enabled) {
                panelCanvas.enabled = false;
                routeLine.Clear();
            }
            return;
        }
        float seconds = _open ? openSeconds : closeSeconds;
        _anim = Mathf.MoveTowards(_anim, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        float eased = 1f - Mathf.Pow(1f - _anim, 3f); // ease-out cubic
        panelGroup.alpha = eased;
        float slide = (1f - eased) * slideDistance;
        sidePanel.anchoredPosition = _sidePanelRest + new Vector2(slide, 0f);
        mapControls.anchoredPosition = _controlsRest + new Vector2(slide, 0f);
        float scale = Mathf.Lerp(1.04f, 1f, eased);
        mapImage.rectTransform.parent.localScale = new Vector3(scale, scale, 1f); // isi viewport sedikit "mendarat"
    }

    private bool BindScan() {
        // Di venue, scanner ada di objek yang sama dengan VenueMapData (gambar dan data pasti cocok).
        VenueMapData venue = VenueMapData.Current;
        MinimapScanner scanner = venue != null && venue.TryGetComponent(out MinimapScanner venueScanner)
            ? venueScanner
            : FindAnyObjectByType<MinimapScanner>();
        if (scanner == null || scanner.textureResultOfScan == null) return false;
        mapImage.texture = scanner.textureResultOfScan;
        Vector3 corner = scanner.transform.position;
        _mapCorner = new Vector2(corner.x, corner.z);
        _mapSize = ScanAreaMeters(scanner.scanArea);
        BindVenue(venue);
        BindWaves(scanner.textureResultOfScan);
        return true;
    }

    /// Pulau ↔ venue: warna latar, judul, petunjuk gestur, dan lapisan area.
    private void BindVenue(VenueMapData venue) {
        _venue = venue;
        seaBackground.color = venue != null ? venue.BackgroundColor : _seaColor;
        if (panelTitle != null) panelTitle.text = venue != null ? venue.DisplayName : _cityTitle;
        if (sectionLabel != null) sectionLabel.text = venue != null ? "AREA DI DALAM" : _citySection;
        if (gestureHint != null) gestureHint.text = venue != null ? "Seret: geser     Cubit / scroll: zoom" : _cityHint;
        if (venueOverlay != null) venueOverlay.Bind(venue);
    }

    private void BindWaves(Texture scan) {
        if (_venue != null) {
            // Di dalam ruangan tidak ada laut.
            _wavesReady = false;
            wavesImage.enabled = false;
            return;
        }
        _wavesReady = shore != null && shore.distanceField != null && _wavesMaterial != null;
        if (_wavesReady && shore.sourceScan != scan) {
            // Garis pantai dari scan lama tidak cocok lagi dengan gambar peta: lebih baik tanpa ombak.
            Debug.LogWarning("[Peta] Data garis pantai usang. Jalankan \"Go Vista/Minimap/Bake garis pantai peta penuh\".");
            _wavesReady = false;
        }
        wavesImage.enabled = _wavesReady;
        if (!_wavesReady) return;
        wavesImage.texture = shore.distanceField;
        _wavesMaterial.SetFloat("_MaxDistance", shore.maxDistance);
        _wavesMaterial.SetFloat("_WorldSize", shore.worldSize);
    }

    /// Nama enum ScanArea = "Units<meter>", mis. Units500.
    private static float ScanAreaMeters(MinimapScanner.ScanArea area) =>
        float.TryParse(area.ToString().Replace("Units", ""), out float meters) ? meters : 500f;

    private static Vector2 PlayerPosition() {
        PlayerController player = PlayerController.Local;
        return player != null ? Flat(player.transform.position) : Vector2.zero;
    }

    // ------------------------------------------------------------ Daftar tempat penting

    private void RebuildLegend() {
        if (_venue != null) {
            RebuildVenueLegend();
            return;
        }
        var blips = new List<MinimapBlip>();
        foreach (MinimapBlip blip in MinimapBlip.All)
            if (blip != null && !string.IsNullOrEmpty(blip.MapLabel) && blip.Item.itemSprite != null) blips.Add(blip);
        blips.Sort((a, b) => string.Compare(a.MapLabel, b.MapLabel, StringComparison.OrdinalIgnoreCase));

        // Quest utama yang belum diterima tampil paling atas, dengan pin quest-nya.
        QuestData mainQuest = giverMapSprite ? QuestWaypoint.MainGiverQuest : null;
        int offset = mainQuest != null ? 1 : 0;
        EnsureRows(blips.Count + offset);
        for (int i = 0; i < _rows.Count; i++) {
            LegendRow row = _rows[i];
            bool used = i < blips.Count + offset;
            row.Button.gameObject.SetActive(used);
            if (!used) continue;
            row.Area = -1;
            row.Giver = i < offset;
            row.Blip = row.Giver ? null : blips[i - offset];
            if (row.Giver) SetRow(row, "Row Quest " + mainQuest.title, giverMapSprite, Color.white, mainQuest.title);
            else SetRow(row, "Row " + row.Blip.MapLabel, row.Blip.Item.itemSprite, Color.white, row.Blip.MapLabel);
            _rows[i] = row;
        }
    }

    private void RebuildVenueLegend() {
        IReadOnlyList<VenueMapData.Area> areas = _venue.Areas;
        EnsureRows(areas.Count);
        for (int i = 0; i < _rows.Count; i++) {
            LegendRow row = _rows[i];
            bool used = i < areas.Count;
            row.Button.gameObject.SetActive(used);
            if (!used) continue;
            row.Blip = null;
            row.Giver = false;
            row.Area = i;
            SetRow(row, "Row " + areas[i].name, areaSprite, areas[i].color, areas[i].name);
            _rows[i] = row;
        }
    }

    private void EnsureRows(int count) {
        while (_rows.Count < count) {
            Button button = Instantiate(legendRowTemplate, legendContent);
            int index = _rows.Count;
            button.onClick.AddListener(() => SelectLandmark(index));
            _rows.Add(new LegendRow { Button = button, Background = button.GetComponent<Image>(), Area = -1 });
        }
    }

    private static void SetRow(LegendRow row, string name, Sprite icon, Color iconColor, string label) {
        row.Button.name = name;
        Image image = row.Button.transform.Find("Icon").GetComponent<Image>();
        image.sprite = icon;
        image.color = iconColor;
        row.Button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = label;
    }

    private void SelectLandmark(int index) {
        if (!_open || index >= _rows.Count) return;
        if (_venue != null) {
            int area = _rows[index].Area;
            if (area < 0 || area >= _venue.Areas.Count || venueOverlay == null) return;
            if (venueOverlay.ToggleSelect(area)) PanTo(_venue.Areas[area].center);
            return;
        }
        if (_rows[index].Giver) {
            SelectMainGiver();
            return;
        }
        if (_rows[index].Blip == null) return;
        Vector3 position = _rows[index].Blip.transform.position;
        GpsService.SetCustomWaypoint(position);
        PanTo(Flat(position));
    }

    /// Pemberi quest utama dijadikan tujuan sendiri: GPS memandu ke sana sampai pemain tiba atau menghapusnya.
    private void SelectMainGiver() {
        Transform giver = QuestWaypoint.MainGiver;
        if (giver == null) return;
        GpsService.SetCustomWaypoint(giver.position);
        PanTo(Flat(giver.position));
    }

    private MinimapBlip SelectedLandmark() {
        if (_venue != null || !GpsService.HasCustomWaypoint) return null;
        Vector2 waypoint = Flat(GpsService.CustomWaypoint);
        foreach (LegendRow row in _rows)
            if (row.Blip != null && row.Button.gameObject.activeSelf &&
                (Flat(row.Blip.transform.position) - waypoint).sqrMagnitude < 1f)
                return row.Blip;
        return null;
    }

    // ------------------------------------------------------------ Input

    public void OnPointerClick(PointerEventData eventData) {
        if (!_open || eventData.dragging || _gestureHadPinch || eventData.button != PointerEventData.InputButton.Left) return;
        if (_venue != null) return; // venue: tidak ada tujuan / GPS (ruangan kecil)
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;

        if (GpsService.HasCustomWaypoint &&
            (WorldToLocal(GpsService.CustomWaypoint) - local).magnitude <= tapRemoveRadius) {
            GpsService.ClearCustomWaypoint();
            return;
        }
        if (_giverIcon != null && _giverIcon.gameObject.activeSelf && QuestWaypoint.MainGiver != null &&
            (GiverIconCenter() - local).magnitude <= tapRemoveRadius) {
            SelectMainGiver();
            return;
        }
        Vector2 world = LocalToWorld(local);
        if (!InsideMap(world)) return;
        // Tinggi tidak dipakai untuk jarak (GPS memakai x,z); pakai tinggi pemain supaya pencarian titik jalan terdekat
        // di Minimap Routes tidak tersaring batas beda tinggi.
        float y = PlayerController.Local != null ? PlayerController.Local.transform.position.y : 0f;
        GpsService.SetCustomWaypoint(new Vector3(world.x, y, world.y));
    }

    /// Tengah badan pin pemberi quest (pivot-nya di ujung bawah), untuk deteksi ketukan.
    private Vector2 GiverIconCenter() =>
        _giverIcon.anchoredPosition + new Vector2(0f, giverMapSize * (0.5f - giverPivotY));

    public void OnBeginDrag(PointerEventData eventData) => _panTarget = null;

    public void OnDrag(PointerEventData eventData) {
        if (!_open || Input.touchCount >= 2) return;
        _focus -= eventData.delta / (CanvasScale * _zoom);
        ClampFocus();
    }

    public void OnScroll(PointerEventData eventData) {
        if (!_open || Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.enterEventCamera, out Vector2 local)) return;
        ZoomAroundLocal(local, Mathf.Pow(scrollZoomStep, Mathf.Sign(eventData.scrollDelta.y)));
    }

    private void HandlePinch() {
        if (Input.touchCount == 0) _gestureHadPinch = false;
        if (Input.touchCount < 2) return;
        _gestureHadPinch = true;
        _panTarget = null;
        Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
        float distance = Vector2.Distance(a.position, b.position);
        if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began || _pinchDistance <= 0f) {
            _pinchDistance = distance;
            return;
        }
        if (distance > 1f &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, (a.position + b.position) * 0.5f, null,
                out Vector2 local))
            ZoomAroundLocal(local, distance / _pinchDistance);
        _pinchDistance = distance;
    }

    private void ZoomAroundLocal(Vector2 local, float factor) {
        Vector2 world = LocalToWorld(local);
        _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _focus = world - (local - FocusLocal) / _zoom;
        ClampFocus();
    }

    private void PanTo(Vector2 world) => _panTarget = world;

    private void UpdatePan() {
        if (_panTarget == null) return;
        Vector2 target = _panTarget.Value;
        _focus = Vector2.Lerp(_focus, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        ClampFocus();
        if ((_focus - target).sqrMagnitude < 0.01f) _panTarget = null;
    }

    // ------------------------------------------------------------ Gambar

    private void Layout() {
        RectTransform map = mapImage.rectTransform;
        map.sizeDelta = Vector2.one * (_mapSize * _zoom);
        map.anchoredPosition = WorldToLocal2(_mapCorner + Vector2.one * (_mapSize * 0.5f));

        if (_wavesReady) {
            RectTransform waves = wavesImage.rectTransform;
            waves.sizeDelta = Vector2.one * (shore.worldSize * _zoom);
            waves.anchoredPosition = WorldToLocal2(shore.worldMin + Vector2.one * (shore.worldSize * 0.5f));
        }

        DrawIcons();
        DrawRoute();
        if (_venue != null) {
            PlayerController player = PlayerController.Local;
            if (venueOverlay != null)
                venueOverlay.Draw(_worldToLocal, _zoom, player != null ? player.transform.position : (Vector3?)null);
            DrawHere(player);
        } else {
            DrawDestination();
        }
    }

    private void DrawIcons() {
        _sortedBlips.Clear();
        foreach (MinimapBlip blip in MinimapBlip.All)
            if (blip != null && blip.Item.enabled && blip.Item.itemSprite != null) _sortedBlips.Add(blip);
        _sortedBlips.Sort(ByOrder);

        while (_icons.Count < _sortedBlips.Count) {
            Image icon = Instantiate(iconTemplate, iconLayer);
            icon.name = "Icon " + _icons.Count;
            _icons.Add(icon);
        }
        for (int i = 0; i < _icons.Count; i++) {
            Image icon = _icons[i];
            bool used = i < _sortedBlips.Count;
            if (icon.gameObject.activeSelf != used) icon.gameObject.SetActive(used);
            if (!used) continue;
            MinimapBlip blip = _sortedBlips[i];
            icon.sprite = blip.Item.itemSprite;
            icon.color = blip.Item.spriteColor;
            RectTransform rect = icon.rectTransform;
            rect.sizeDelta = Vector2.one * (blip.PixelSize * iconScale);
            rect.anchoredPosition = WorldToLocal(blip.transform.position);
            // Panah pemain menunjukkan arah hadap; peta penuh selalu utara di atas.
            rect.localRotation = blip.KeepUpright
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, -blip.transform.eulerAngles.y);
        }

        Transform quest = _venue == null ? QuestWaypoint.Target : null;
        questIcon.gameObject.SetActive(quest != null);
        if (quest != null) {
            questIcon.anchoredPosition = WorldToLocal(quest.position);
            questIcon.SetAsLastSibling();
        }

        Transform giver = _venue == null && _giverIcon != null ? QuestWaypoint.MainGiver : null;
        if (_giverIcon == null) return;
        _giverIcon.gameObject.SetActive(giver != null);
        if (giver == null) return;
        _giverIcon.anchoredPosition = WorldToLocal(giver.position);
        _giverIcon.SetAsLastSibling();
    }

    /// Pin pemberi quest utama di peta penuh, dibuat saat runtime di lapisan ikon (prefab cukup menyimpan sprite-nya).
    private RectTransform CreateGiverIcon() {
        if (!giverMapSprite) return null;
        var icon = new GameObject("Main Quest Giver", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        icon.SetParent(questIcon.parent, false);
        icon.anchorMin = questIcon.anchorMin;
        icon.anchorMax = questIcon.anchorMax;
        icon.pivot = new Vector2(0.5f, giverPivotY);
        icon.sizeDelta = new Vector2(giverMapSize, giverMapSize);
        var image = icon.GetComponent<Image>();
        image.sprite = giverMapSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        icon.gameObject.SetActive(false);
        return icon;
    }

    /// True kalau tujuan sendiri sedang dipasang di pemberi quest utama (dipilih dari pin / daftar).
    private static bool CustomIsMainGiver(out QuestData quest) {
        quest = QuestWaypoint.MainGiverQuest;
        return GpsService.CustomIsMainGiver;
    }

    private void DrawRoute() {
        GpsRoute route = GpsRoute.Active;
        if (_venue != null || route == null || !route.IsShowing || route.Points.Count < 2) {
            routeLine.Clear();
            return;
        }
        _routeLocal.Clear();
        foreach (Vector3 point in route.Points) _routeLocal.Add(WorldToLocal(point));
        routeLine.color = route.CurrentColor;
        routeLine.Thickness = routePixels;
        routeLine.SetPoints(_routeLocal);
    }

    private void DrawDestination() {
        bool custom = GpsService.HasCustomWaypoint;
        bool quest = !custom && QuestWaypoint.Target != null;
        MinimapBlip landmark = SelectedLandmark();

        GpsRoute route = GpsRoute.Active;
        string distance = (custom || quest) && route != null && route.LengthMeters > 0f
            ? FormatDistance(route.LengthMeters)
            : "";

        string caption, title, hint;
        Color accent;
        if (custom && CustomIsMainGiver(out QuestData mainQuest)) {
            caption = "QUEST UTAMA";
            title = mainQuest.title;
            hint = "Temui pemberi quest untuk memulai";
            accent = giverColor;
        } else if (custom) {
            caption = "TUJUANMU";
            title = landmark != null ? landmark.MapLabel : "Titik di peta";
            hint = "Ketuk penanda di peta untuk menghapus";
            accent = customColor;
        } else if (quest) {
            caption = "TUJUAN QUEST";
            title = "Ikuti garis GPS";
            hint = "Ketuk peta untuk memilih tujuan sendiri";
            accent = questColor;
        } else {
            caption = "TUJUAN";
            title = "Belum ada tujuan";
            hint = "Ketuk peta atau pilih tempat di daftar";
            accent = idleColor;
        }
        // Ada rute: jarak besar + keterangan kecil menggantikan petunjuk.
        if (distance.Length > 0) hint = "<b><size=36><color=#FFFFFF>" + distance + "</color></size></b>   lewat jalan";

        SetText(destinationCaption, caption);
        SetText(destinationTitle, title);
        SetText(destinationHint, hint);
        destinationCaption.color = accent;
        destinationIconBackground.color = new Color(accent.r, accent.g, accent.b, 0.22f);
        destinationIcon.color = accent;
        destinationIcon.sprite = quest || (custom && CustomIsMainGiver(out _)) ? flagSprite : pinSprite;
        if (clearButton.gameObject.activeSelf != custom) {
            clearButton.gameObject.SetActive(custom);
            // Tombol Hapus di kanan baris bawah: sempitkan teks supaya tidak tertimpa.
            float clearSpace = ((RectTransform)clearButton.transform).sizeDelta.x + 12f;
            RectTransform hintRect = destinationHint.rectTransform;
            hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x + (custom ? -clearSpace : clearSpace), hintRect.sizeDelta.y);
        }

        foreach (LegendRow row in _rows)
            if (row.Giver) row.Background.color = custom && CustomIsMainGiver(out _) ? rowSelectedColor : rowColor;
            else if (row.Blip != null) row.Background.color = row.Blip == landmark ? rowSelectedColor : rowColor;
    }

    /// Kartu atas di venue: area tempat pemain berada (tanpa tujuan / rute).
    private void DrawHere(PlayerController player) {
        int here = player != null ? _venue.AreaIndexAt(player.transform.position) : -1;
        bool upper = player != null && _venue.IsUpper(player.transform.position);
        SetText(destinationCaption, upper ? "KAMU DI · BALKON" : "KAMU DI");
        SetText(destinationTitle, here >= 0 ? _venue.Areas[here].name : "Lorong");
        SetText(destinationHint, "Ketuk area di daftar untuk menyorotnya");
        destinationCaption.color = hereColor;
        destinationIconBackground.color = new Color(hereColor.r, hereColor.g, hereColor.b, 0.22f);
        destinationIcon.color = hereColor;
        destinationIcon.sprite = hereSprite != null ? hereSprite : pinSprite;
        if (clearButton.gameObject.activeSelf) {
            clearButton.gameObject.SetActive(false);
            float clearSpace = ((RectTransform)clearButton.transform).sizeDelta.x + 12f;
            RectTransform hintRect = destinationHint.rectTransform;
            hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x + clearSpace, hintRect.sizeDelta.y);
        }
        int selected = venueOverlay != null ? venueOverlay.Selected : -1;
        foreach (LegendRow row in _rows)
            if (row.Area >= 0) row.Background.color = row.Area == selected ? rowSelectedColor : rowColor;
    }

    private static void SetText(TextMeshProUGUI label, string text) {
        if (label.text != text) label.text = text;
    }

    private static string FormatDistance(float meters) =>
        meters >= 1000f ? (meters / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(meters) + " m";

    // ------------------------------------------------------------ Koordinat

    /// Batas kanan area peta yang terlihat (lokal viewport) = tepi kiri panel samping saat diam.
    private float VisibleRight {
        get {
            sidePanel.GetWorldCorners(_corners);
            float left = _viewport.InverseTransformPoint(_corners[0]).x;
            // Saat animasi panel bergeser; hitung dari posisi diamnya supaya peta tidak ikut bergoyang.
            float offset = (sidePanel.anchoredPosition - _sidePanelRest).x;
            return Mathf.Min(left - offset, _viewport.rect.xMax);
        }
    }

    /// Titik fokus (lokal viewport): tengah area peta yang tidak tertutup panel samping.
    private Vector2 FocusLocal => new Vector2((_viewport.rect.xMin + VisibleRight) * 0.5f, _viewport.rect.center.y);

    private float MinZoom {
        get {
            float width = VisibleRight - _viewport.rect.xMin;
            float fit = Mathf.Min(width, _viewport.rect.height) / Mathf.Max(1f, _mapSize);
            return Mathf.Min(fit, MaxZoom);
        }
    }

    private float MaxZoom => _venue != null ? venueMaxZoom : maxZoom;

    private float CanvasScale => panelCanvas.rootCanvas != null ? panelCanvas.rootCanvas.scaleFactor : 1f;

    /// Posisi dunia → posisi lokal di viewport (px dari tengah viewport, utara di atas).
    private Vector2 WorldToLocal(Vector3 world) => WorldToLocal2(Flat(world));

    private Vector2 WorldToLocal2(Vector2 world) => FocusLocal + (world - _focus) * _zoom;

    private Vector2 LocalToWorld(Vector2 local) => _focus + (local - FocusLocal) / _zoom;

    private bool InsideMap(Vector2 world) =>
        world.x >= _mapCorner.x && world.y >= _mapCorner.y &&
        world.x <= _mapCorner.x + _mapSize && world.y <= _mapCorner.y + _mapSize;

    private void ClampFocus() {
        _focus.x = Mathf.Clamp(_focus.x, _mapCorner.x, _mapCorner.x + _mapSize);
        _focus.y = Mathf.Clamp(_focus.y, _mapCorner.y, _mapCorner.y + _mapSize);
    }

    private static Vector2 Flat(Vector3 world) => new Vector2(world.x, world.z);
}
