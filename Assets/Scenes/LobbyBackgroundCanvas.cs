using Normal.Realtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the customizable lobby image only when an image is assigned and no room is active.</summary>
public sealed class LobbyBackgroundCanvas : MonoBehaviour {
    private static readonly Color PlaceholderColor = new Color(0.12f, 0.13f, 0.14f, 1f);
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Image backgroundImage;

    private Realtime _realtime;

    private void Awake() {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();
        _realtime = FindFirstObjectByType<Realtime>();
        RefreshVisibility();
    }

    private void LateUpdate() {
        if (_realtime == null)
            _realtime = FindFirstObjectByType<Realtime>();
        RefreshVisibility();
    }

    private void RefreshVisibility() {
        bool hasImage = backgroundImage != null && backgroundImage.sprite != null;
        if (backgroundImage != null)
            backgroundImage.color = hasImage ? Color.white : PlaceholderColor;
        if (targetCanvas != null)
            targetCanvas.enabled = _realtime == null || !_realtime.connected;
    }
}
