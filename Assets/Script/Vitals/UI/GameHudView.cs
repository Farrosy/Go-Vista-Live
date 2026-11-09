using UnityEngine;
using UnityEngine.UI;

/// Akar HUD gameplay. Membaca LocalPlayerVitals tiap frame lalu meneruskannya ke tampilan bar.
/// HUD hanya terlihat selama ada avatar lokal.
public sealed class GameHudView : MonoBehaviour {
    private const float VignetteAfterDamageSeconds = 3f;
    private const float VignetteMinAlpha = 0.35f;
    private const float FadePerSecond = 4f;
    private const float VitalsLeft = 187f;
    private const float GapFromChatPanel = 24f;
    private const float ShiftPixelsPerSecond = 2000f;

    [SerializeField] private LocalPlayerVitals vitals;
    [SerializeField] private HealthBarView health;
    [SerializeField] private OxygenBarView oxygen;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasGroup vitalsGroup;
    [SerializeField] private Canvas coinCanvas;
    [SerializeField] private Image redVignette;
    [SerializeField] private Image underwaterOverlay;
    [SerializeField] private int coinSortingNormal = 25;
    [SerializeField] private int coinSortingOnDeathScreen = 56;

    private Camera _camera;
    private RectTransform _vitalsRect;
    private float _underwaterAlpha;

    private void Awake() => _vitalsRect = (RectTransform)vitalsGroup.transform;

    private void Update() {
        bool hasPlayer = PlayerController.Local != null;
        rootCanvas.enabled = hasPlayer;
        coinCanvas.enabled = hasPlayer;
        if (!hasPlayer) return;

        VitalsState state = vitals.State;
        float time = Time.time;
        float delta = Time.deltaTime;
        health.Render(state, time, delta);
        oxygen.Render(state, vitals.HeadUnderWater, time, delta);
        UpdateVitalsPosition(delta);
        UpdateRedVignette(state, time);
        UpdateUnderwaterOverlay(delta);
    }

    /// Selama layar mati hanya koin yang tetap terlihat, di atas layar mati.
    public void SetDeathScreenMode(bool active) {
        vitalsGroup.alpha = active ? 0f : 1f;
        coinCanvas.sortingOrder = active ? coinSortingOnDeathScreen : coinSortingNormal;
        if (active) redVignette.enabled = false;
    }

    /// Panel chat terbuka otomatis saat masuk game dan berada di pojok yang sama. Bar digeser ke sampingnya
    /// supaya HP dan peringatan O₂ tidak pernah tertutup.
    private void UpdateVitalsPosition(float delta) {
        float besideChat = ChatTheme.ScreenMargin + ChatTheme.PanelWidth + GapFromChatPanel - VitalsLeft;
        float target = ChatPanel.IsPanelOpen ? besideChat : 0f;
        Vector2 position = _vitalsRect.anchoredPosition;
        position.x = Mathf.MoveTowards(position.x, target, ShiftPixelsPerSecond * delta);
        _vitalsRect.anchoredPosition = position;
    }

    private void UpdateRedVignette(VitalsState state, float time) {
        if (vitalsGroup.alpha <= 0f) return;
        // Saat mati, tepi merah tidak boleh menimpa kilat dan layar gelap benturan.
        bool recentCriticalHit = !state.IsDead && state.IsHealthCritical &&
                                 time - vitals.LastDamageTime < VignetteAfterDamageSeconds;
        bool show = recentCriticalHit || state.Level == OxygenLevel.Danger;
        redVignette.enabled = show;
        if (!show) return;
        Color color = redVignette.color;
        color.a = Mathf.Lerp(VignetteMinAlpha, 1f, HudAnimation.Pulse(time));
        redVignette.color = color;
    }

    /// Air hanya dirender dari atas, jadi dari bawah permukaan layar perlu ditint supaya terasa di dalam air.
    private void UpdateUnderwaterOverlay(float delta) {
        if (_camera == null) _camera = Camera.main;
        bool underwater = _camera != null &&
                          WaterSurface.TryGetLevelAt(_camera.transform.position, out float level) &&
                          _camera.transform.position.y < level;
        _underwaterAlpha = Mathf.MoveTowards(_underwaterAlpha, underwater ? 1f : 0f, FadePerSecond * delta);
        underwaterOverlay.enabled = _underwaterAlpha > 0f;
        Color color = underwaterOverlay.color;
        color.a = _underwaterAlpha;
        underwaterOverlay.color = color;
    }
}
