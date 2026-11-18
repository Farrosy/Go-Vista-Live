using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Lapisan peta penuh khusus venue (KPOP Arena / Community), di atas gambar peta dan di bawah ikon pemain:
///  - nama area di tengah tiap area,
///  - lingkaran sorotan untuk area yang dipilih dari daftar,
///  - garis putus-putus lantai atas (balkon) selama pemain lokal ada di atas,
///  - tag "BALKON" di atas panah pemain.
/// Dikendalikan GoVistaFullMap (posisi dihitung di sana); dipasang oleh GameHudBuilder.VenueMap.cs.
public sealed class VenueMapOverlay : MonoBehaviour {
    [Tooltip("Contoh label (non-aktif): latar + anak \"Dot\" (Image) dan \"Label\" (TMP).")]
    [SerializeField] private RectTransform labelTemplate;
    [Tooltip("Contoh garis putus-putus lantai atas (non-aktif).")]
    [SerializeField] private RectTransform outlineTemplate;
    [SerializeField] private Image highlight;
    [SerializeField] private RectTransform levelTag;
    [Tooltip("Jarak tag BALKON di atas titik pemain (px).")]
    [SerializeField] private float levelTagOffset = 70f;
    [SerializeField] private float pulseSpeed = 4f;

    private readonly List<RectTransform> _labels = new List<RectTransform>();
    private readonly List<RectTransform> _outlines = new List<RectTransform>();
    private VenueMapData _venue;
    private Color _highlightColor;

    /// Indeks area yang disorot (-1 = tidak ada).
    public int Selected { get; private set; } = -1;

    private void Awake() {
        labelTemplate.gameObject.SetActive(false);
        outlineTemplate.gameObject.SetActive(false);
        _highlightColor = highlight.color;
        highlight.gameObject.SetActive(false);
        levelTag.gameObject.SetActive(false);
    }

    /// Ganti venue (null = pulau: lapisan disembunyikan).
    public void Bind(VenueMapData venue) {
        _venue = venue;
        Selected = -1;
        gameObject.SetActive(venue != null);
        if (venue == null) return;

        IReadOnlyList<VenueMapData.Area> areas = venue.Areas;
        Ensure(_labels, labelTemplate, areas.Count);
        for (int i = 0; i < _labels.Count; i++) {
            bool used = i < areas.Count;
            _labels[i].gameObject.SetActive(used);
            if (!used) continue;
            _labels[i].Find("Dot").GetComponent<Image>().color = areas[i].color;
            _labels[i].Find("Label").GetComponent<TextMeshProUGUI>().text = areas[i].name;
        }
        Ensure(_outlines, outlineTemplate, venue.UpperOutline.Count);
    }

    /// Ketuk area yang sama lagi = lepas sorotan. Mengembalikan true kalau area sekarang tersorot.
    public bool ToggleSelect(int index) {
        Selected = Selected == index ? -1 : index;
        return Selected >= 0;
    }

    /// <param name="toLocal">Posisi dunia (x, z) → posisi lokal di lapisan ini.</param>
    /// <param name="zoom">Piksel per meter.</param>
    /// <param name="player">Posisi pemain lokal (null = belum ada).</param>
    public void Draw(Func<Vector2, Vector2> toLocal, float zoom, Vector3? player) {
        if (_venue == null) return;
        IReadOnlyList<VenueMapData.Area> areas = _venue.Areas;
        for (int i = 0; i < areas.Count && i < _labels.Count; i++)
            _labels[i].anchoredPosition = toLocal(areas[i].center);

        bool hasSelection = Selected >= 0 && Selected < areas.Count;
        if (highlight.gameObject.activeSelf != hasSelection) highlight.gameObject.SetActive(hasSelection);
        if (hasSelection) {
            VenueMapData.Area area = areas[Selected];
            RectTransform rect = highlight.rectTransform;
            rect.anchoredPosition = toLocal(area.center);
            rect.sizeDelta = Vector2.one * (area.radius * 2f * zoom);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            highlight.color = new Color(_highlightColor.r, _highlightColor.g, _highlightColor.b, _highlightColor.a * (0.7f + 0.3f * pulse));
        }

        bool upper = player.HasValue && _venue.IsUpper(player.Value);
        IReadOnlyList<Rect> outline = _venue.UpperOutline;
        for (int i = 0; i < _outlines.Count; i++) {
            bool show = upper && i < outline.Count;
            if (_outlines[i].gameObject.activeSelf != show) _outlines[i].gameObject.SetActive(show);
            if (!show) continue;
            _outlines[i].anchoredPosition = toLocal(outline[i].center);
            _outlines[i].sizeDelta = outline[i].size * zoom;
        }

        if (levelTag.gameObject.activeSelf != upper) levelTag.gameObject.SetActive(upper);
        if (upper) levelTag.anchoredPosition = toLocal(new Vector2(player.Value.x, player.Value.z)) + Vector2.up * levelTagOffset;
    }

    private static void Ensure(List<RectTransform> pool, RectTransform template, int count) {
        while (pool.Count < count) {
            RectTransform item = Instantiate(template, template.parent);
            item.name = template.name.Replace("Template", pool.Count.ToString());
            pool.Add(item);
        }
        for (int i = count; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }
}
