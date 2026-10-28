using UnityEngine;

/// <summary>
/// Mengecilkan panel secara proporsional supaya selalu muat di dalam parent-nya (biasanya SafeAreaFitter)
/// dengan margin yang sama, apa pun rasio layar HP-nya (16:9, 20:9, tablet 4:3, ada notch atau tidak).
/// Ukuran desain panel tidak diubah, jadi jarak dan padding di dalamnya tetap seimbang.
/// Panel harus memakai anchor dan pivot di tengah.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class ResponsivePanel : MonoBehaviour {
    [Tooltip("Jarak minimum dari tepi layar/safe area, dalam unit canvas (referensi 1920x1080).")]
    [SerializeField] private float margin = 40f;
    [Tooltip("Skala maksimum. 1 = panel tidak pernah lebih besar dari ukuran desainnya.")]
    [SerializeField] private float maxScale = 1f;

    private Vector2 _lastAvailable;
    private Vector2 _lastSize;

    public void Configure(float edgeMargin, float maximumScale) {
        margin = edgeMargin;
        maxScale = maximumScale;
        Apply(true);
    }

    private void OnEnable() {
        Apply(true);
    }

    // Dicek tiap frame karena ukuran parent (rotasi layar, safe area) dan ukuran panel
    // (ContentSizeFitter di layar login) bisa berubah. Hanya menulis kalau ada perubahan.
    private void LateUpdate() {
        Apply(false);
    }

    private void Apply(bool force) {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        if (parent == null)
            return;

        Vector2 available = parent.rect.size - new Vector2(2f * margin, 2f * margin);
        Vector2 size = rect.rect.size;
        if (!force && available == _lastAvailable && size == _lastSize)
            return;
        _lastAvailable = available;
        _lastSize = size;
        if (size.x <= 0f || size.y <= 0f || available.x <= 0f || available.y <= 0f)
            return;

        float scale = Mathf.Min(maxScale, available.x / size.x, available.y / size.y);
        rect.localScale = new Vector3(scale, scale, 1f);
    }
}
