using System;
using System.Collections.Generic;
using UnityEngine;

/// Data peta satu venue (KPOP Arena / KPOP Community), dipasang di objek "Venue Minimap Scanner" bersama
/// MinimapScanner (gambar hasil bake VenueMinimapBaker). Radar dan peta penuh membaca komponen ini supaya:
///  - jangkauan radar lebih kecil (ruangan ±43 m, bukan kota),
///  - peta penuh menampilkan daftar area, bukan tempat penting + GPS,
///  - pemain di lantai atas (balkon) diberi label BALKON; peta tetap satu lantai.
/// Data awal (nama area, posisi, warna) dari mockup yang disetujui 2026-11-18 (memory: venue-map-decisions),
/// diisi oleh VenueMinimapBaker saat komponen belum ada.
[DisallowMultipleComponent]
public sealed class VenueMapData : MonoBehaviour {
    [Serializable]
    public struct Area {
        public string name;
        [Tooltip("Tengah area di dunia (x, z).")]
        public Vector2 center;
        [Min(0.1f)] public float radius;
        public Color color;
        [Tooltip("Area di lantai atas (balkon).")]
        public bool upper;
    }

    private static readonly List<VenueMapData> Active = new List<VenueMapData>();

    /// Venue yang sedang dimuat (null = pulau / lobi).
    public static VenueMapData Current => Active.Count > 0 ? Active[Active.Count - 1] : null;

    [SerializeField] private string displayName = "Venue";
    [Tooltip("Warna di luar ruangan: tepi radar dan latar peta penuh.")]
    [SerializeField] private Color backgroundColor = new Color32(13, 10, 22, 255);
    [Tooltip("Tinggi lantai utama (Y dunia).")]
    [SerializeField] private float floorY;
    [Tooltip("Kaki pemain lebih tinggi dari ini (meter di atas lantai) = lantai atas. 0 = venue tanpa lantai atas.")]
    [SerializeField] private float upperLift;
    [SerializeField] private List<Area> areas = new List<Area>();
    [Tooltip("Persegi (x, z dunia) lantai atas, digaris putus-putus di peta penuh saat pemain ada di atas.")]
    [SerializeField] private List<Rect> upperOutline = new List<Rect>();

    private VenueArea[] _rules;

    public string DisplayName => displayName;
    public Color BackgroundColor => backgroundColor;
    public float FloorY => floorY;
    public IReadOnlyList<Area> Areas => areas;
    public IReadOnlyList<Rect> UpperOutline => upperOutline;

    private void OnEnable() {
        if (!Active.Contains(this)) Active.Add(this);
        _rules = null;
    }

    private void OnDisable() => Active.Remove(this);

    private void OnValidate() => _rules = null;

    /// Kaki di posisi ini ada di lantai atas (balkon)?
    public bool IsUpper(Vector3 worldPosition) => VenueMapRules.IsUpperLevel(worldPosition.y, floorY, upperLift);

    /// Indeks area di posisi ini (lantai ikut dihitung), atau -1 kalau di lorong.
    public int AreaIndexAt(Vector3 worldPosition) =>
        VenueMapRules.AreaIndexAt(Rules(), worldPosition.x, worldPosition.z, IsUpper(worldPosition));

    private VenueArea[] Rules() {
        if (_rules != null && _rules.Length == areas.Count) return _rules;
        _rules = new VenueArea[areas.Count];
        for (int i = 0; i < areas.Count; i++) {
            Area a = areas[i];
            _rules[i] = new VenueArea(a.name, a.center.x, a.center.y, a.radius, a.upper);
        }
        return _rules;
    }

#if UNITY_EDITOR
    /// Dipakai VenueMinimapBaker untuk mengisi data awal.
    public void EditorSetup(string name, Color background, float floor, float lift, List<Area> newAreas, List<Rect> outline) {
        displayName = name;
        backgroundColor = background;
        floorY = floor;
        upperLift = lift;
        areas = newAreas;
        upperOutline = outline;
        _rules = null;
    }

    private void OnDrawGizmosSelected() {
        foreach (Area a in areas) {
            Gizmos.color = a.color;
            Vector3 c = new Vector3(a.center.x, floorY + (a.upper ? upperLift + 1f : 0.1f), a.center.y);
            Gizmos.DrawWireSphere(c, a.radius);
        }
        Gizmos.color = new Color(1f, 0.77f, 0.24f);
        foreach (Rect r in upperOutline)
            Gizmos.DrawWireCube(new Vector3(r.center.x, floorY + upperLift + 1f, r.center.y), new Vector3(r.width, 0.1f, r.height));
    }
#endif
}
