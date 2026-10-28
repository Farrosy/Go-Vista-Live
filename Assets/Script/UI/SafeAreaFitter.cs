using UnityEngine;

/// <summary>
/// Membuat RectTransform ini mengikuti Screen.safeArea: area layar yang tidak tertutup notch,
/// kamera depan, atau sudut layar yang membulat. Taruh UI (panel, tombol pojok) di dalamnya.
/// Cek hasilnya lewat Window > General > Device Simulator.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour {
    private Rect _appliedSafeArea;
    private Vector2Int _appliedScreenSize;

    private void OnEnable() {
        Apply(true);
    }

    private void LateUpdate() {
        Apply(false);
    }

    private void Apply(bool force) {
        Rect safeArea = Screen.safeArea;
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && safeArea == _appliedSafeArea && screenSize == _appliedScreenSize)
            return;
        _appliedSafeArea = safeArea;
        _appliedScreenSize = screenSize;
        if (screenSize.x <= 0 || screenSize.y <= 0)
            return;

        var rect = (RectTransform)transform;
        Vector2 min = safeArea.position;
        Vector2 max = safeArea.position + safeArea.size;
        rect.anchorMin = new Vector2(min.x / screenSize.x, min.y / screenSize.y);
        rect.anchorMax = new Vector2(max.x / screenSize.x, max.y / screenSize.y);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
