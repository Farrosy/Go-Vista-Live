using UnityEngine;
using UnityEngine.UI;

/// Akar HUD gameplay. Membaca LocalPlayerVitals tiap frame lalu meneruskannya ke tampilan bar.
/// HUD hanya terlihat selama ada avatar lokal.
public sealed class GameHudView : MonoBehaviour {
    private const float VignetteAfterDamageSeconds = 3f;
    private const float CarHitVignetteSeconds = 0.5f;
    private const float VignetteMinAlpha = 0.35f;
    private const float FadePerSecond = 4f;
    private const float MinimapFadePerSecond = 8f;

    [SerializeField] private LocalPlayerVitals vitals;
    [SerializeField] private HealthArcView health;
    [SerializeField] private OxygenArcView oxygen;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasGroup vitalsGroup;
    [SerializeField] private CanvasGroup minimapGroup;
    [SerializeField] private Canvas coinCanvas;
    [SerializeField] private Image redVignette;
    [SerializeField] private Image underwaterOverlay;
    [SerializeField] private int coinSortingNormal = 25;
    [SerializeField] private int coinSortingOnDeathScreen = 56;

    private Camera _camera;
    private float _underwaterAlpha;

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
        UpdateMinimapVisibility(delta);
        UpdateRedVignette(state, time);
        UpdateUnderwaterOverlay(delta);
    }

    /// Selama layar mati hanya koin yang tetap terlihat, di atas layar mati.
    public void SetDeathScreenMode(bool active) {
        vitalsGroup.alpha = active ? 0f : 1f;
        coinCanvas.sortingOrder = active ? coinSortingOnDeathScreen : coinSortingNormal;
        if (active) redVignette.enabled = false;
    }

    /// Panel chat terbuka di pojok yang sama dengan minimap, jadi minimap (dengan busur HP/O₂) disembunyikan
    /// selama chat terbuka. Chip peringatan O₂ ada di tengah atas, jadi tetap terlihat.
    private void UpdateMinimapVisibility(float delta) {
        float target = ChatPanel.IsPanelOpen ? 0f : 1f;
        minimapGroup.alpha = Mathf.MoveTowards(minimapGroup.alpha, target, MinimapFadePerSecond * delta);
    }

    private void UpdateRedVignette(VitalsState state, float time) {
        if (vitalsGroup.alpha <= 0f) return;
        // Saat mati, tepi merah tidak boleh menimpa kilat dan layar gelap benturan.
        bool recentCriticalHit = !state.IsDead && state.IsHealthCritical &&
                                 time - vitals.LastDamageTime < VignetteAfterDamageSeconds;
        bool pulsing = recentCriticalHit || state.Level == OxygenLevel.Danger;
        float carHitAlpha = state.IsDead ? 0f : Mathf.Clamp01(1f - (time - vitals.LastCarHitTime) / CarHitVignetteSeconds);
        redVignette.enabled = pulsing || carHitAlpha > 0f;
        if (!redVignette.enabled) return;
        Color color = redVignette.color;
        float pulseAlpha = pulsing ? Mathf.Lerp(VignetteMinAlpha, 1f, HudAnimation.Pulse(time)) : 0f;
        color.a = Mathf.Max(pulseAlpha, carHitAlpha);
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
