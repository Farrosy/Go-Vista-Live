using System;
using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Peta kota layar penuh, seperti menu peta GTA San Andreas:
///  - Utara selalu di atas. Seret = geser, cubit / scroll = zoom, tombol + / − juga ada.
///  - Ketuk peta = pasang tujuan sendiri (GpsService); ketuk tujuan itu lagi atau "Hapus Tujuan" = lepas.
///  - Menampilkan semua blip radar (pemain, tempat penting, tujuan sendiri), tujuan quest, dan rute GPS.
/// Gambar peta = hasil scan Minimap Scanner di WorldScene (sama dengan radar), jadi tidak ada kamera tambahan.
/// Dibuka dengan mengetuk radar atau tombol M; ditutup dengan tombol X, Esc / Back, atau M lagi.
/// Komponen ini ada di "Viewport" peta (area yang menerima sentuhan). Dipasang oleh GameHudBuilder.FullMap.cs.
public sealed class GoVistaFullMap : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IScrollHandler {
    private const float SlowTickSeconds = 0.5f;

    public static GoVistaFullMap Current { get; private set; }
    public static bool IsOpen => Current != null && Current._open;

    [Header("Susunan HUD")]
    [SerializeField] private Canvas panelCanvas;
    [SerializeField] private RawImage mapImage;
    [SerializeField] private UiPolyline routeLine;
    [SerializeField] private RectTransform iconLayer;
    [Tooltip("Contoh ikon (non-aktif). Digandakan untuk tiap blip radar.")]
    [SerializeField] private Image iconTemplate;
    [SerializeField] private RectTransform questIcon;
    [SerializeField] private TextMeshProUGUI infoLabel;

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

    private RectTransform _viewport;
    private bool _open;
    private float _zoom;
    private Vector2 _center;          // posisi dunia (x, z) di tengah viewport
    private Vector2 _mapCorner;       // posisi dunia (x, z) sudut kiri bawah hasil scan
    private float _mapSize;           // lebar = tinggi hasil scan (meter)
    private bool _gestureHadPinch;
    private float _pinchDistance;
    private float _nextSlowTick;
    private bool _available;
    private readonly List<Image> _icons = new List<Image>();
    private readonly List<MinimapBlip> _sortedBlips = new List<MinimapBlip>();
    private readonly List<Vector2> _routeLocal = new List<Vector2>();

    private static readonly Comparison<MinimapBlip> ByOrder = (a, b) => a.Item.orderInLayer.CompareTo(b.Item.orderInLayer);

    private void Awake() {
        Current = this;
        _viewport = (RectTransform)transform;
        panelCanvas.enabled = false;
        iconTemplate.gameObject.SetActive(false);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        clearButton.onClick.AddListener(GpsService.ClearCustomWaypoint);
        centerButton.onClick.AddListener(CenterOnPlayer);
        zoomInButton.onClick.AddListener(() => ZoomAroundLocal(Vector2.zero, buttonZoomStep));
        zoomOutButton.onClick.AddListener(() => ZoomAroundLocal(Vector2.zero, 1f / buttonZoomStep));
    }

    private void OnDestroy() {
        if (Current == this) Current = null;
        if (_open) Close();
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
        openButtonCanvas.enabled = _available && !PlayerInputHub.IsDeathLocked;

        if (_open) {
            if (!_available || blocked || PlayerController.Local == null || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(toggleKey)) {
                Close();
                return;
            }
            HandlePinch();
            Layout();
        } else if (Input.GetKeyDown(toggleKey) && (hub == null || !hub.InputLocked)) {
            // InputLocked juga menutup jalan saat sedang mengetik chat atau panel lain terbuka.
            Open();
        }
    }

    // ------------------------------------------------------------ Buka / tutup

    public void Open() {
        if (_open || !_available || PlayerController.Local == null) return;
        PlayerInputHub hub = PlayerInputHub.Instance;
        if (hub != null && hub.OverlayActive) return;
        if (!BindScan()) return;

        _open = true;
        panelCanvas.enabled = true;
        if (hub != null) hub.SetPanelLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _zoom = Mathf.Clamp(openZoom, MinZoom, maxZoom);
        CenterOnPlayer();
        Layout();
    }

    public void Close() {
        if (!_open) return;
        _open = false;
        panelCanvas.enabled = false;
        routeLine.Clear();
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(false);
    }

    private bool BindScan() {
        MinimapScanner scanner = FindAnyObjectByType<MinimapScanner>();
        if (scanner == null || scanner.textureResultOfScan == null) return false;
        mapImage.texture = scanner.textureResultOfScan;
        Vector3 corner = scanner.transform.position;
        _mapCorner = new Vector2(corner.x, corner.z);
        _mapSize = ScanAreaMeters(scanner.scanArea);
        return true;
    }

    /// Nama enum ScanArea = "Units<meter>", mis. Units500.
    private static float ScanAreaMeters(MinimapScanner.ScanArea area) =>
        float.TryParse(area.ToString().Replace("Units", ""), out float meters) ? meters : 500f;

    private void CenterOnPlayer() {
        PlayerController player = PlayerController.Local;
        if (player != null) _center = Flat(player.transform.position);
        ClampCenter();
    }

    // ------------------------------------------------------------ Input

    public void OnPointerClick(PointerEventData eventData) {
        if (!_open || eventData.dragging || _gestureHadPinch || eventData.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;

        if (GpsService.HasCustomWaypoint &&
            (WorldToLocal(GpsService.CustomWaypoint) - local).magnitude <= tapRemoveRadius) {
            GpsService.ClearCustomWaypoint();
            return;
        }
        Vector2 world = LocalToWorld(local);
        if (!InsideMap(world)) return;
        // Tinggi tidak dipakai untuk jarak (GPS memakai x,z); pakai tinggi pemain supaya pencarian titik jalan terdekat
        // di Minimap Routes tidak tersaring batas beda tinggi.
        float y = PlayerController.Local != null ? PlayerController.Local.transform.position.y : 0f;
        GpsService.SetCustomWaypoint(new Vector3(world.x, y, world.y));
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData) {
        if (!_open || Input.touchCount >= 2) return;
        _center -= eventData.delta / (CanvasScale * _zoom);
        ClampCenter();
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
        _zoom = Mathf.Clamp(_zoom * factor, MinZoom, maxZoom);
        _center = world - local / _zoom;
        ClampCenter();
    }

    // ------------------------------------------------------------ Gambar

    private void Layout() {
        RectTransform map = mapImage.rectTransform;
        map.sizeDelta = Vector2.one * (_mapSize * _zoom);
        map.anchoredPosition = (_mapCorner + Vector2.one * (_mapSize * 0.5f) - _center) * _zoom;

        DrawIcons();
        DrawRoute();
        DrawInfo();
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

        Transform quest = QuestWaypoint.Target;
        questIcon.gameObject.SetActive(quest != null);
        if (quest != null) {
            questIcon.anchoredPosition = WorldToLocal(quest.position);
            questIcon.SetAsLastSibling();
        }
    }

    private void DrawRoute() {
        GpsRoute route = GpsRoute.Active;
        if (route == null || !route.IsShowing || route.Points.Count < 2) {
            routeLine.Clear();
            return;
        }
        _routeLocal.Clear();
        foreach (Vector3 point in route.Points) _routeLocal.Add(WorldToLocal(point));
        routeLine.color = route.CurrentColor;
        routeLine.Thickness = routePixels;
        routeLine.SetPoints(_routeLocal);
    }

    private void DrawInfo() {
        bool custom = GpsService.HasCustomWaypoint;
        clearButton.interactable = custom;
        GpsRoute route = GpsRoute.Active;
        string distance = route != null && route.LengthMeters > 0f ? FormatDistance(route.LengthMeters) : "";
        string text;
        if (custom) text = "Tujuanmu" + (distance.Length > 0 ? ": " + distance : "");
        else if (QuestWaypoint.Target != null) text = "Tujuan quest" + (distance.Length > 0 ? ": " + distance : "");
        else text = "Ketuk peta untuk memasang tujuan";
        if (infoLabel.text != text) infoLabel.text = text;
    }

    private static string FormatDistance(float meters) =>
        meters >= 1000f ? (meters / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(meters) + " m";

    // ------------------------------------------------------------ Koordinat

    private float MinZoom {
        get {
            Vector2 size = _viewport.rect.size;
            float fit = Mathf.Min(size.x, size.y) / Mathf.Max(1f, _mapSize);
            return Mathf.Min(fit, maxZoom);
        }
    }

    private float CanvasScale => panelCanvas.rootCanvas != null ? panelCanvas.rootCanvas.scaleFactor : 1f;

    /// Posisi dunia → posisi lokal di viewport (px dari tengah viewport, utara di atas).
    private Vector2 WorldToLocal(Vector3 world) => (Flat(world) - _center) * _zoom;

    private Vector2 LocalToWorld(Vector2 local) => _center + local / _zoom;

    private bool InsideMap(Vector2 world) =>
        world.x >= _mapCorner.x && world.y >= _mapCorner.y &&
        world.x <= _mapCorner.x + _mapSize && world.y <= _mapCorner.y + _mapSize;

    private void ClampCenter() {
        _center.x = Mathf.Clamp(_center.x, _mapCorner.x, _mapCorner.x + _mapSize);
        _center.y = Mathf.Clamp(_center.y, _mapCorner.y, _mapCorner.y + _mapSize);
    }

    private static Vector2 Flat(Vector3 world) => new Vector2(world.x, world.z);
}
