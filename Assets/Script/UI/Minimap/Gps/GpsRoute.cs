using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using UnityEngine;

/// Rute GPS gaya GTA: garis di radar dari pemain ke tujuan GpsService, mengikuti jalan.
/// Pencarian rute (A*) dan garis di radar memakai "Minimap Routes" dari Easy Minimap System.
/// Jaringan jalannya dibangun otomatis dari spline jalan (RoadSection) lewat menu
/// "Go Vista/Minimap/Bangun jaringan jalan GPS (WorldScene)", yang membuat prefab GpsRoadNetwork di WorldScene.
/// Peta layar penuh (GoVistaFullMap) membaca Points untuk menggambar rute yang sama.
[DefaultExecutionOrder(-10)] // sebelum MinimapRoutes.Update, supaya titik awal/tujuan sudah benar di frame yang sama
public sealed class GpsRoute : MonoBehaviour {
    public static GpsRoute Active { get; private set; }

    [SerializeField] private MinimapRoutes routes;
    [Tooltip("Objek kosong yang dipindah ke posisi tujuan (Minimap Routes butuh Transform).")]
    [SerializeField] private Transform destination;
    [Tooltip("Ikon tujuan buatan pemain di radar.")]
    [SerializeField] private MinimapBlip waypointBlip;

    [Header("Tampilan")]
    [SerializeField] private Color questColor = new Color32(255, 204, 64, 255);
    [SerializeField] private Color customColor = new Color32(214, 64, 224, 255);
    [Tooltip("Tebal garis rute di radar (px pada referensi 1080p). Tetap sama walau radar zoom.")]
    [SerializeField] private float radarLinePixels = 7f;
    [Tooltip("Tujuan buatan pemain otomatis hilang saat pemain sedekat ini (meter).")]
    [SerializeField] private float arrivalRadius = 8f;

    private readonly List<Vector3> _points = new List<Vector3>();
    private float _tailMeters;
    private LineRenderer _line;

    /// Titik rute dalam koordinat dunia: [0] = pemain, terakhir = tujuan. Kosong kalau tidak ada rute.
    public IReadOnlyList<Vector3> Points => _points;
    public bool IsShowing { get; private set; }
    public GpsDestinationKind Kind { get; private set; }
    public Color CurrentColor => Kind == GpsDestinationKind.Custom ? customColor : questColor;
    public Color QuestColor => questColor;
    public Color CustomColor => customColor;

    /// Panjang rute dari posisi pemain sekarang (meter), atau -1 kalau belum ada rute.
    public float LengthMeters { get; private set; } = -1f;

    private void Awake() {
        Active = this;
        routes.showRoutesOnStart = false;
        routes.onUpdateRoutesRenderization.AddListener(CopyRoute);
    }

    private void OnDestroy() {
        if (Active == this) Active = null;
        if (routes != null) routes.onUpdateRoutesRenderization.RemoveListener(CopyRoute);
    }

    private void Update() {
        PlayerController player = PlayerController.Local;
        if (player != null && GpsService.HasCustomWaypoint &&
            FlatDistance(player.transform.position, GpsService.CustomWaypoint) < arrivalRadius)
            GpsService.ClearCustomWaypoint();

        if (waypointBlip != null) {
            waypointBlip.Item.enabled = GpsService.HasCustomWaypoint;
            if (GpsService.HasCustomWaypoint) waypointBlip.transform.position = GpsService.CustomWaypoint;
        }

        if (player == null || !GpsService.TryGetDestination(out Vector3 target, out GpsDestinationKind kind)) {
            Hide();
            return;
        }

        Kind = kind;
        destination.position = target;
        routes.startingPoint = player.transform;
        routes.destinationPoint = destination;
        routes.lineRenderColor = CurrentColor;
        GoVistaMinimap radar = GoVistaMinimap.Current;
        if (radar != null) routes.lineWidthSize = Mathf.Max(0.5f, radarLinePixels / radar.PixelsPerMeter);
        if (!routes.isCurrentlyShowingRoutes()) routes.StartCalculatingAndShowRotesToDestination();
        IsShowing = true;
    }

    // Rute dihitung ulang beberapa kali per detik; di antaranya ujung garis ditempel ke posisi pemain
    // supaya garis tidak tertinggal di belakang pemain yang sedang berjalan.
    private void LateUpdate() {
        PlayerController player = PlayerController.Local;
        if (!IsShowing || player == null || _points.Count == 0) return;
        Vector3 here = player.transform.position;
        _points[0] = here;
        LengthMeters = _tailMeters + (_points.Count > 1 ? FlatDistance(here, _points[1]) : 0f);

        if (_line == null) _line = routes.GetGeneratedLineRendererAtRunTime();
        if (_line != null && _line.positionCount > 0) {
            Vector3 first = _line.GetPosition(0);
            _line.SetPosition(0, new Vector3(here.x, first.y, here.z));
        }
    }

    private void Hide() {
        if (routes.isCurrentlyShowingRoutes()) routes.StopCalculatingAndHideRotesToDestination();
        IsShowing = false;
        Kind = GpsDestinationKind.None;
        _points.Clear();
        _tailMeters = 0f;
        LengthMeters = -1f;
    }

    private void CopyRoute() {
        if (!routes.isCurrentlyShowingRoutes()) return;
        MinimapRoutes.CurrentDisplayingRoute route = routes.GetCurrentGeneratedRouteIfIsCalculingAndShowingRoutes();
        _points.Clear();
        _points.AddRange(route.positionsThatMakeUpThisRoute);
        _tailMeters = 0f;
        for (int i = 2; i < _points.Count; i++) _tailMeters += FlatDistance(_points[i - 1], _points[i]);
    }

    private static float FlatDistance(Vector3 a, Vector3 b) {
        float x = a.x - b.x, z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }
}
