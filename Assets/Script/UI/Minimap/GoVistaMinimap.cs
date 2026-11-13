using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Radar gaya GTA San Andreas di slot minimap HUD, dibangun di atas Easy Minimap System (MT Assets).
///  - Peta = hasil scan "Minimap Scanner" di WorldScene (dibake di Editor, murah saat runtime).
///  - Peta berputar mengikuti kamera: bagian atas radar = arah pandang kamera.
///  - Panah pemain lokal di tengah menunjukkan arah hadap avatar.
///  - Pemain lain = kotak kecil berwarna per pemain, menempel di tepi kalau jauh.
///  - Tempat penting = MinimapBlip di WorldScene (lihat MinimapScanTool: "Pasang ikon tempat penting").
///  - Radar sedikit zoom out saat sprint (di GTA SA radar zoom out mengikuti kecepatan).
///  - Tidak ada scan di scene aktif (venue Dance Club / Rooftop Bar) = peta disembunyikan dan
///    placeholder HUD tampil, seperti radar di dalam interior GTA SA.
/// Dipasang oleh GameHudBuilder di "Minimap/Radar" pada GameHudCanvas.
[DefaultExecutionOrder(-50)] // sebelum MinimapRenderer, supaya kameranya sudah ada saat Update pertama
[RequireComponent(typeof(MinimapRenderer))]
public sealed class GoVistaMinimap : MonoBehaviour {
    // Nama yang dicari semua komponen Easy Minimap System lewat GameObject.Find.
    private const string DataHolderName = "Minimap Data Holder";
    private const float SlowTickSeconds = 0.5f;
    private const int LocalArrowOrder = 5; // urutan tertinggi yang diizinkan MinimapItem
    private const int RemotePlayerOrder = 4;

    public static GoVistaMinimap Current { get; private set; }

    [Header("Susunan HUD")]
    [SerializeField] private CanvasGroup radarGroup;
    [Tooltip("Gambar pengganti saat tidak ada peta (lobi, venue).")]
    [SerializeField] private GameObject placeholder;
    [Tooltip("Jarak (px) dari tengah ke tepi peta yang terlihat.")]
    [SerializeField] private float edgeRadiusPixels = 100f;

    [Header("Zoom (meter dari tengah ke tepi radar)")]
    [SerializeField] private float walkRadius = 60f;
    [SerializeField] private float sprintRadius = 72f;
    [Tooltip("Kecepatan perubahan zoom. Makin besar makin cepat.")]
    [SerializeField] private float zoomSharpness = 2f;
    [Tooltip("Kehalusan gerak dan putaran peta (Movements Smoothing di Easy Minimap System).")]
    [SerializeField] private float followSmoothing = 20f;
    [SerializeField] private MinimapCamera.CaptureResolutionSquare resolution = MinimapCamera.CaptureResolutionSquare.pixels512x512;

    [Header("Blip pemain")]
    [SerializeField] private Sprite localArrowSprite;
    [SerializeField] private float localArrowPixels = 24f;
    [SerializeField] private Sprite remotePlayerSprite;
    [SerializeField] private float remotePlayerPixels = 14f;
    [Tooltip("Warna pemain lain, dipilih dari clientID Normcore, jadi semua pemain melihat warna yang sama untuk orang yang sama. " +
             "Hindari emas (marker quest) dan putih (panah pemain lokal).")]
    [SerializeField] private Color[] remotePlayerPalette = {
        new Color32(230, 57, 70, 255),   // merah
        new Color32(58, 134, 255, 255),  // biru
        new Color32(255, 140, 66, 255),  // oranye
        new Color32(155, 93, 229, 255),  // ungu
        new Color32(6, 214, 160, 255),   // hijau mint
        new Color32(241, 91, 181, 255),  // merah muda
        new Color32(0, 187, 249, 255),   // biru muda
        new Color32(160, 110, 70, 255),  // cokelat
    };

    private MinimapRenderer _renderer;
    private MinimapCamera _minimapCamera;
    private Transform _rig;
    private Transform _dataHolder;
    private float _radius;
    private float _nextSlowTick;
    private bool _hasMap;
    private bool? _showing;
    private PlayerController _arrowOwner;
    private readonly List<Transform> _orphans = new List<Transform>();

    /// Jari-jari radar dalam meter (dari tengah ke tepi), sudah termasuk zoom saat sprint.
    public float WorldRadius => _radius;
    public float PixelsPerMeter => edgeRadiusPixels / Mathf.Max(1f, _radius);
    public bool IsShowing => _showing == true;

    /// Arah atas radar dalam derajat (yaw dunia), sama dengan putaran peta yang sedang tampil.
    public float Heading {
        get {
            Camera cam = _minimapCamera != null ? _minimapCamera.GetGeneratedCameraAtRunTime() : null;
            if (cam == null) return _rig != null ? _rig.eulerAngles.y : 0f;
            // Kamera radar menghadap ke bawah, jadi "atas" layarnya = transform.up.
            Vector3 up = cam.transform.up;
            return Mathf.Atan2(up.x, up.z) * Mathf.Rad2Deg;
        }
    }

    /// Selisih posisi dunia (x,z) → posisi di radar (px dari tengah, atas = arah kamera).
    public Vector2 WorldOffsetToRadar(Vector2 flatOffset) {
        float radians = Heading * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        var rotated = new Vector2(flatOffset.x * cos - flatOffset.y * sin, flatOffset.x * sin + flatOffset.y * cos);
        return rotated * PixelsPerMeter;
    }

    private void Awake() {
        Current = this;
        _renderer = GetComponent<MinimapRenderer>();
        _radius = walkRadius;
        _dataHolder = EnsureDataHolder();

        // Kamera radar ada di objek biasa (bukan UI) yang mengikuti pemain. Dibuat non-aktif dulu supaya
        // Awake MinimapCamera langsung memakai pengaturan di bawah, bukan bawaannya.
        var rig = new GameObject("Minimap Rig");
        SceneManager.MoveGameObjectToScene(rig, gameObject.scene);
        rig.SetActive(false);
        _minimapCamera = rig.AddComponent<MinimapCamera>();
        _minimapCamera.captureShape = MinimapCamera.CaptureShape.Square;
        _minimapCamera.captureResolutionSquare = resolution;
        _minimapCamera.captureMode = MinimapCamera.CaptureMode.TopView2D;
        _minimapCamera.fieldOfView = _radius;
        _minimapCamera.followRotationOf = MinimapCamera.FollowRotationOf.ThisGameObject;
        _minimapCamera.movementsSmoothing = followSmoothing;
        rig.SetActive(true);
        _rig = rig.transform;

        _renderer.minimapCameraToShow = _minimapCamera;
    }

    private void OnDestroy() {
        if (Current == this) Current = null;
        if (_rig != null) Destroy(_rig.gameObject);
    }

    private void LateUpdate() {
        if (Time.unscaledTime >= _nextSlowTick) {
            _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
            _hasMap = FindAnyObjectByType<MinimapScanner>() != null;
            SyncPlayerBlips();
            SweepOrphans();
        }

        PlayerController player = PlayerController.Local;
        Camera view = Camera.main;
        if (player != null) _rig.position = player.transform.position;
        if (view != null) _rig.rotation = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f);
        SetShowing(_hasMap && player != null);

        float sprint = player != null ? Mathf.InverseLerp(0.5f, 1f, player.Speed01) : 0f;
        float targetRadius = Mathf.Lerp(walkRadius, sprintRadius, sprint);
        _radius = Mathf.Lerp(_radius, targetRadius, 1f - Mathf.Exp(-zoomSharpness * Time.deltaTime));
        _minimapCamera.fieldOfView = _radius;
        _minimapCamera.movementsSmoothing = followSmoothing;

        float metersPerPixel = _radius / edgeRadiusPixels;
        IReadOnlyList<MinimapBlip> blips = MinimapBlip.All;
        for (int i = 0; i < blips.Count; i++)
            blips[i].Apply(metersPerPixel, _rig, followSmoothing, _renderer);
    }

    private void SetShowing(bool show) {
        if (_showing == show) return;
        _showing = show;
        if (radarGroup != null) radarGroup.alpha = show ? 1f : 0f;
        if (placeholder != null) placeholder.SetActive(!show);
        // Kamera radar tidak perlu merender saat tidak tampil.
        Camera cam = _minimapCamera.GetGeneratedCameraAtRunTime();
        if (cam != null) cam.enabled = show;
    }

    // ------------------------------------------------------------ Blip pemain

    private void SyncPlayerBlips() {
        PlayerController local = PlayerController.Local;
        if (local != null && local != _arrowOwner) {
            _arrowOwner = local;
            MinimapBlip arrow = EnsureBlip(local.gameObject, localArrowSprite, Color.white, LocalArrowOrder);
            arrow.Configure(localArrowPixels, upright: false, edge: false);
        }

        foreach (PlayerVenueSync sync in PlayerVenueSync.All) {
            if (sync == null || sync.IsLocalAvatar) continue;
            if (local != null && sync.gameObject == local.gameObject) continue;
            MinimapBlip blip = sync.GetComponent<MinimapBlip>();
            if (blip == null) {
                blip = EnsureBlip(sync.gameObject, remotePlayerSprite, Color.white, RemotePlayerOrder);
                blip.Configure(remotePlayerPixels, upright: true, edge: true);
            }
            // Diperbarui terus: clientID baru ada setelah model Normcore siap (sebelumnya -1).
            blip.Item.spriteColor = ColorForClient(sync.OwnerClientId);
            // Sama dengan aturan VenueVisibilityFilter: pemain di venue lain atau sedang disembunyikan tidak tampil.
            blip.Item.enabled = sync.Venue == VenueTravelManager.LocalVenue && !sync.IsHidden;
        }
    }

    private Color ColorForClient(int clientId) {
        if (remotePlayerPalette == null || remotePlayerPalette.Length == 0) return Color.white;
        int index = clientId < 0 ? 0 : clientId % remotePlayerPalette.Length;
        return remotePlayerPalette[index];
    }

    private static MinimapBlip EnsureBlip(GameObject owner, Sprite sprite, Color color, int order) {
        MinimapBlip blip = owner.GetComponent<MinimapBlip>();
        if (blip == null) blip = owner.AddComponent<MinimapBlip>(); // RequireComponent ikut menambah MinimapItem
        MinimapItem item = blip.Item;
        item.itemSprite = sprite;
        item.spriteColor = color;
        item.orderInLayer = order;
        item.raycastTarget = false;
        return blip;
    }

    // ------------------------------------------------------------ Easy Minimap System

    /// Buat "Minimap Data Holder" di scene HUD (MainMultiplayerScene, tidak pernah di-unload).
    /// Kalau dibuat oleh komponen di WorldScene, holder ikut hilang saat pindah venue dan radar rusak.
    private Transform EnsureDataHolder() {
        GameObject holder = GameObject.Find(DataHolderName);
        if (holder == null) {
            holder = new GameObject(DataHolderName);
            holder.AddComponent<MinimapDataHolder>();
        }
        if (holder.transform.parent == null && holder.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(holder, gameObject.scene);
        return holder.transform;
    }

    /// Easy Minimap System hanya menonaktifkan sprite/kamera milik komponen yang sudah hancur
    /// (mis. scan WorldScene setelah pindah venue, atau pemain yang keluar). Hapus supaya tidak menumpuk.
    private void SweepOrphans() {
        if (_dataHolder == null) return;
        _orphans.Clear();
        foreach (Transform group in _dataHolder)
            foreach (Transform child in group)
                if (child.TryGetComponent(out ActivityMonitor monitor) && monitor.responsibleScriptComponentForThis == null)
                    _orphans.Add(child);
        foreach (Transform orphan in _orphans) Destroy(orphan.gameObject);
    }
}
