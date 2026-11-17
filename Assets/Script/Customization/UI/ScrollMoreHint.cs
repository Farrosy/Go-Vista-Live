using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Penanda "masih ada isi" untuk ScrollRect: efek pudar + panah di bawah (vertikal) atau di kanan (horizontal)
/// viewport yang muncul selama isi belum discroll sampai ujung, lalu memudar saat sudah di ujung (atau isinya muat).
/// Pasang di objek ScrollRect; hint = objek terpisah di atas viewport (tidak ikut di-mask).
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public sealed class ScrollMoreHint : MonoBehaviour {
    /// Sisa jarak (px) ke bawah yang dianggap sudah sampai ujung.
    private const float EndTolerance = 4f;
    private const float FadeSpeed = 8f;

    [SerializeField] private CanvasGroup hint;
    [SerializeField] private bool horizontal;
    private ScrollRect _scroll;

    public void SetHint(CanvasGroup target) {
        hint = target;
        if (hint) {
            hint.alpha = 0f;
            hint.blocksRaycasts = false;
            hint.interactable = false;
        }
    }

    /// <summary>True = cek sisa isi di kanan (baris chip), false = di bawah (default).</summary>
    public void SetHorizontal(bool value) => horizontal = value;

    void Awake() => _scroll = GetComponent<ScrollRect>();

    void LateUpdate() {
        if (!hint || !_scroll || !_scroll.content || !_scroll.viewport) return;
        float target = HasMoreBelow() ? 1f : 0f;
        hint.alpha = Mathf.MoveTowards(hint.alpha, target, FadeSpeed * Time.unscaledDeltaTime);
    }

    private bool HasMoreBelow() {
        if (horizontal) {
            float hiddenX = _scroll.content.rect.width - _scroll.viewport.rect.width;
            return hiddenX > EndTolerance && (1f - _scroll.horizontalNormalizedPosition) * hiddenX > EndTolerance;
        }
        float hidden = _scroll.content.rect.height - _scroll.viewport.rect.height;
        if (hidden <= EndTolerance) return false;
        return _scroll.verticalNormalizedPosition * hidden > EndTolerance;
    }
}
