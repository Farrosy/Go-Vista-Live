using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Penanda quest di minimap. Ikon tidak pernah berganti dan tidak hilang karena jarak: tujuan dekat tampil di
/// posisinya, tujuan jauh menempel di bingkai minimap ke arah tujuan itu (tanpa panah); pusat ikon tepat di bingkai,
/// sama seperti ikon tempat penting (mis. Dermaga).
///  - Tujuan quest aktif (QuestWaypoint.Target): titik emas, dipandu GPS otomatis. Jarak dalam meter tampil di bawah
///    minimap (panjang rute GPS kalau ada, garis lurus kalau belum).
///  - Pemberi quest utama yang belum diterima (QuestWaypoint.MainGiver): pin "!" oranye, ujung bawah pin di posisi
///    pemberi. Bukan tujuan GPS otomatis; pemain memilihnya sendiri di peta penuh.
/// Kalau radar (GoVistaMinimap) aktif, penanda ikut putaran dan zoom radar; tanpa radar, north-up.
public sealed class QuestWaypointMarker : MonoBehaviour {
    [SerializeField] private RectTransform marker;
    [Tooltip("Tidak dipakai lagi (penanda jauh menempel di tepi, bukan panah); dibiarkan supaya prefab lama tetap valid.")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private GameObject dot;
    [SerializeField] private TextMeshProUGUI distanceLabel;
    [Tooltip("Jarak dunia (meter) dari tengah ke tepi minimap.")]
    [SerializeField] private float worldRadius = 50f;
    [Tooltip("Jarak (px) dari tengah ke bingkai, dipakai kalau radar (GoVistaMinimap) tidak ada. Dengan radar, " +
             "bingkai = setengah lebar radar.")]
    [SerializeField] private float edgeRadius = 110f;

    [Header("Pemberi quest utama")]
    [Tooltip("Pin quest baru (warna penuh, tidak di-tint).")]
    [SerializeField] private Sprite giverSprite;
    [Tooltip("Ukuran pin di minimap (px).")]
    [SerializeField] private float giverSize = 34f;
    [Tooltip("Titik pivot pin = ujung bawahnya (y dari bawah, 0–1).")]
    [SerializeField] private float giverPivotY = 0.14f;

    private int _shownMeters = -1;
    private RectTransform _giverPin;

    private void Awake() {
        if (arrow) arrow.gameObject.SetActive(false);
        if (dot) dot.SetActive(true);
        _giverPin = CreateGiverPin();
    }

    /// Pin pemberi quest dibuat saat runtime di sebelah penanda tujuan, jadi prefab HUD cukup menyimpan sprite-nya.
    private RectTransform CreateGiverPin() {
        if (!giverSprite || !marker) return null;
        var pin = new GameObject("Main Quest Giver", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        pin.SetParent(marker.parent, false);
        pin.SetSiblingIndex(marker.GetSiblingIndex()); // di bawah titik tujuan aktif kalau bertumpuk
        pin.anchorMin = pin.anchorMax = new Vector2(0.5f, 0.5f);
        pin.pivot = new Vector2(0.5f, giverPivotY);
        pin.sizeDelta = new Vector2(giverSize, giverSize);
        var image = pin.GetComponent<Image>();
        image.sprite = giverSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        pin.gameObject.SetActive(false);
        return pin;
    }

    private void Update() {
        PlayerController player = PlayerController.Local;
        Transform target = player != null ? QuestWaypoint.Target : null;
        Transform giver = player != null ? QuestWaypoint.MainGiver : null;

        marker.gameObject.SetActive(target != null);
        distanceLabel.gameObject.SetActive(target != null);
        if (target != null) {
            float meters = Place(marker, target.position, player, Vector2.zero);
            // Selama GPS memandu ke tujuan quest ini, jarak = panjang rute di jalan (seperti GPS GTA), bukan garis lurus.
            GpsRoute route = GpsRoute.Active;
            if (route != null && route.Kind == GpsDestinationKind.Quest && route.LengthMeters > 0f) meters = route.LengthMeters;
            ShowDistance(Mathf.RoundToInt(meters));
        }

        if (_giverPin == null) return;
        _giverPin.gameObject.SetActive(giver != null);
        if (giver == null) return;
        // Yang ditahan di bingkai adalah tengah badan pin, bukan ujungnya, sama seperti ikon tempat penting di radar.
        var toCenter = new Vector2(0f, giverSize * (0.5f - giverPivotY));
        Place(_giverPin, giver.position, player, toCenter);
    }

    /// Taruh penanda di posisi tujuan pada radar; di luar jangkauan, tengah ikon ditahan tepat di bingkai.
    /// Mengembalikan jarak (m). <paramref name="toCenter"/> = geser dari pivot ke tengah ikon.
    private float Place(RectTransform icon, Vector3 world, PlayerController player, Vector2 toCenter) {
        Vector3 delta = world - player.transform.position;
        var flat = new Vector2(delta.x, delta.z);
        GoVistaMinimap radar = GoVistaMinimap.Current;
        Vector2 onRadar = radar != null ? radar.WorldOffsetToRadar(flat) : flat / worldRadius * edgeRadius;
        Vector2 center = onRadar + toCenter;
        float limit = RimRadius(radar);
        if (center.magnitude > limit) center = center.normalized * limit;
        icon.anchoredPosition = center - toCenter;
        return flat.magnitude;
    }

    /// Jari-jari bingkai radar dalam satuan penanda ini (tempat ikon tempat penting menempel), atau edgeRadius.
    private float RimRadius(GoVistaMinimap radar) {
        if (radar == null) return edgeRadius;
        var rect = (RectTransform)radar.transform;
        Vector3 half = transform.InverseTransformVector(rect.TransformVector(new Vector3(rect.rect.width * 0.5f, 0f, 0f)));
        return Mathf.Abs(half.x);
    }

    private void ShowDistance(int meters) {
        if (meters == _shownMeters) return;
        _shownMeters = meters;
        distanceLabel.text = meters + " m";
    }
}
