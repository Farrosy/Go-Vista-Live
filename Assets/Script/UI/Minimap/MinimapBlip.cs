using System.Collections.Generic;
using MTAssets.EasyMinimapSystem;
using UnityEngine;

/// Ikon di radar (blip), lapisan tipis di atas MinimapItem milik Easy Minimap System.
/// Pasang di objek dunia (mis. tempat penting), lalu atur sprite dan warnanya di komponen Minimap Item.
///  - Ukuran dalam piksel layar, jadi tetap sama walau radar zoom (MinimapItem sendiri memakai meter).
///  - "Tetap tegak": ikon tidak ikut berputar saat peta berputar mengikuti kamera (seperti ikon di GTA SA).
///  - "Tempel di tepi": kalau di luar jangkauan, ikon menempel di tepi radar ke arah tujuannya.
[DisallowMultipleComponent]
[RequireComponent(typeof(MinimapItem))]
public sealed class MinimapBlip : MonoBehaviour {
    private static readonly List<MinimapBlip> _all = new List<MinimapBlip>();
    public static IReadOnlyList<MinimapBlip> All => _all;

    [Tooltip("Ukuran ikon di radar (px pada referensi 1080p).")]
    [SerializeField] private float pixelSize = 18f;
    [Tooltip("Ikon tetap tegak saat peta berputar. Matikan untuk panah yang harus menunjukkan arah hadap objek.")]
    [SerializeField] private bool keepUpright = true;
    [Tooltip("Kalau di luar jangkauan radar, tampilkan ikon di tepi radar ke arah objek ini.")]
    [SerializeField] private bool clampToEdge;

    private MinimapItem _item;
    private MinimapRenderer _highlightedOn;

    public MinimapItem Item => _item != null ? _item : _item = GetComponent<MinimapItem>();
    public float PixelSize => pixelSize;
    public bool KeepUpright => keepUpright;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _all.Clear();

    /// Untuk blip yang dibuat lewat kode (blip pemain).
    public void Configure(float pixels, bool upright, bool edge) {
        pixelSize = pixels;
        keepUpright = upright;
        clampToEdge = edge;
    }

    private void OnEnable() => _all.Add(this);

    private void OnDisable() {
        _all.Remove(this);
        SetHighlight(null);
    }

    /// Dipanggil GoVistaMinimap setiap frame.
    internal void Apply(float metersPerPixel, Transform radarHeading, float smoothing, MinimapRenderer radar) {
        MinimapItem item = Item;
        float size = pixelSize * metersPerPixel;
        if (Mathf.Abs(item.sizeOnMinimap.x - size) > 0.01f)
            item.sizeOnMinimap = new Vector3(size, 0f, size);
        item.sizeOnHighlight = pixelSize;
        item.movementsSmoothing = smoothing;
        item.raycastTarget = false;
        if (keepUpright) {
            item.followRotationOf = MinimapItem.FollowRotationOf.CustomGameObject;
            item.customGameObjectToFollowRotation = radarHeading;
        } else {
            item.followRotationOf = MinimapItem.FollowRotationOf.ThisGameObject;
        }
        SetHighlight(clampToEdge && item.enabled ? radar : null);
    }

    private void SetHighlight(MinimapRenderer radar) {
        if (_highlightedOn == radar) return;
        if (_highlightedOn != null) _highlightedOn.RemoveMinimapItemOfHighlight(Item);
        _highlightedOn = radar;
        if (radar != null) radar.AddMinimapItemToBeHighlighted(Item);
    }
}
