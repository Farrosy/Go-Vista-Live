using UnityEngine;

/// <summary>
/// Pasang di tombol/joystick mobile yang boleh dipindah & diubah ukurannya oleh pemain
/// (mode ATUR KONTROL, lihat ControlLayoutEditor). Posisi & ukuran di prefab = layout default.
///
/// Ukuran diubah lewat sizeDelta (bukan localScale) supaya jangkauan joystick ikut membesar/mengecil,
/// karena Joystick menghitung radius dari sizeDelta.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomizableControl : MonoBehaviour {
    [Tooltip("ID untuk menyimpan layout. Kosong = nama GameObject. Jangan diganti setelah rilis, " +
             "karena layout yang sudah disimpan pemain ikut hilang.")]
    [SerializeField] private string id;
    [Tooltip("Nama yang tampil di mode ATUR KONTROL. Kosong = nama GameObject.")]
    [SerializeField] private string displayName;

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public RectTransform Rect { get; private set; }
    public float Scale { get; private set; } = 1f;

    private Vector2 defaultPosition;
    private RectTransform[] parts;
    private Vector2[] defaultSizes;
    private Vector2[] defaultPartPositions;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly Vector3[] boundsCorners = new Vector3[4];

    void Awake() {
        CaptureDefaults();
        ApplySaved();
    }

    private void CaptureDefaults() {
        if (parts != null) return;
        Rect = (RectTransform)transform;
        defaultPosition = Rect.anchoredPosition;
        parts = GetComponentsInChildren<RectTransform>(true);   // [0] = objek ini sendiri
        defaultSizes = new Vector2[parts.Length];
        defaultPartPositions = new Vector2[parts.Length];
        for (int i = 0; i < parts.Length; i++) {
            defaultSizes[i] = parts[i].sizeDelta;
            defaultPartPositions[i] = parts[i].anchoredPosition;
        }
    }

    /// Terapkan layout yang tersimpan (atau default kalau belum pernah diatur).
    public void ApplySaved() {
        CaptureDefaults();
        if (ControlLayoutSettings.TryGet(Id, out Vector2 position, out float scale))
            Apply(position, scale);
        else
            Apply(defaultPosition, 1f);
    }

    public void ResetToDefault() => Apply(defaultPosition, 1f);

    public void SetScale(float scale) => Apply(Rect.anchoredPosition, scale);

    public void MoveBy(Vector2 delta) => Apply(Rect.anchoredPosition + delta, Scale);

    /// Simpan ke ControlLayoutSettings (panggil ControlLayoutSettings.Save() sesudahnya).
    public void SaveCurrent() {
        bool isDefault = Mathf.Approximately(Scale, 1f) &&
                         (Rect.anchoredPosition - defaultPosition).sqrMagnitude < 0.01f;
        if (isDefault) ControlLayoutSettings.Remove(Id);
        else ControlLayoutSettings.Set(Id, Rect.anchoredPosition, Scale);
    }

    private void Apply(Vector2 position, float scale) {
        CaptureDefaults();
        Scale = Mathf.Clamp(scale, ControlLayoutSettings.MinScale, ControlLayoutSettings.MaxScale);
        for (int i = 0; i < parts.Length; i++) {
            parts[i].sizeDelta = defaultSizes[i] * Scale;
            if (i > 0) parts[i].anchoredPosition = defaultPartPositions[i] * Scale;
        }
        Rect.anchoredPosition = position;
        KeepInsideScreen();
    }

    // Jangan sampai tombol keluar layar (mis. layout disimpan di HP lain / layar lebih kecil).
    private void KeepInsideScreen() {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;
        var bounds = (RectTransform)canvas.rootCanvas.transform;

        Rect.GetWorldCorners(corners);
        bounds.GetWorldCorners(boundsCorners);
        if (boundsCorners[2].x - boundsCorners[0].x < 1f) return;   // canvas belum punya ukuran
        Vector3 shift = Vector3.zero;
        if (corners[0].x < boundsCorners[0].x) shift.x = boundsCorners[0].x - corners[0].x;
        else if (corners[2].x > boundsCorners[2].x) shift.x = boundsCorners[2].x - corners[2].x;
        if (corners[0].y < boundsCorners[0].y) shift.y = boundsCorners[0].y - corners[0].y;
        else if (corners[2].y > boundsCorners[2].y) shift.y = boundsCorners[2].y - corners[2].y;
        if (shift != Vector3.zero) Rect.position += shift;
    }
}
