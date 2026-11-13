using TMPro;
using UnityEngine;

/// Marker tujuan quest di minimap. Tujuan dekat: titik di posisinya.
/// Tujuan jauh: panah di tepi minimap yang menunjuk ke arahnya. Jarak dalam meter tampil di bawah minimap
/// (panjang rute GPS kalau ada, garis lurus kalau belum).
/// Kalau radar (GoVistaMinimap) aktif, marker ikut putaran dan zoom radar; tanpa radar, north-up.
public sealed class QuestWaypointMarker : MonoBehaviour {
    [SerializeField] private RectTransform marker;
    [Tooltip("Panah di dalam marker, menunjuk ke atas di sprite aslinya.")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private GameObject dot;
    [SerializeField] private TextMeshProUGUI distanceLabel;
    [Tooltip("Jarak dunia (meter) dari tengah ke tepi minimap.")]
    [SerializeField] private float worldRadius = 50f;
    [Tooltip("Jarak (px) dari tengah ke tepi area marker.")]
    [SerializeField] private float edgeRadius = 110f;

    private int _shownMeters = -1;

    private void Update() {
        Transform target = QuestWaypoint.Target;
        PlayerController player = PlayerController.Local;
        bool show = target != null && player != null;
        marker.gameObject.SetActive(show);
        distanceLabel.gameObject.SetActive(show);
        if (!show) return;

        Vector3 delta = target.position - player.transform.position;
        var flat = new Vector2(delta.x, delta.z);
        float meters = flat.magnitude;
        GoVistaMinimap radar = GoVistaMinimap.Current;
        Vector2 onRadar = radar != null ? radar.WorldOffsetToRadar(flat) : flat / worldRadius * edgeRadius;
        float pixels = onRadar.magnitude;
        bool inside = pixels < edgeRadius;

        Vector2 direction = pixels > 0.01f ? onRadar / pixels : Vector2.up;
        marker.anchoredPosition = direction * Mathf.Min(pixels, edgeRadius);
        arrow.gameObject.SetActive(!inside);
        dot.SetActive(inside);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
        // Selama GPS memandu ke tujuan quest ini, jarak = panjang rute di jalan (seperti GPS GTA), bukan garis lurus.
        GpsRoute route = GpsRoute.Active;
        if (route != null && route.Kind == GpsDestinationKind.Quest && route.LengthMeters > 0f) meters = route.LengthMeters;
        ShowDistance(Mathf.RoundToInt(meters));
    }

    private void ShowDistance(int meters) {
        if (meters == _shownMeters) return;
        _shownMeters = meters;
        distanceLabel.text = meters + " m";
    }
}
