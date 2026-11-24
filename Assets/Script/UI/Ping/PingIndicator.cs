using System.Collections;
using Normal.Realtime;
using UnityEngine;

/// Membaca ping pemain lokal ke room server Normcore (<see cref="Realtime.ping"/>, ms) sekitar 1x per detik,
/// menghaluskannya, lalu mengisi <see cref="PingIndicatorView"/>. Hanya lokal: ping pemain lain tidak dikirim.
///
/// Juga memunculkan peringatan "Koneksi tidak stabil" kalau ping di atas <see cref="PingIndicatorView.BadMs"/>
/// selama beberapa detik berturut-turut (dengan jeda supaya tidak berulang terus). Peringatan tetap muncul
/// walau chip ping dimatikan di PENGATURAN.
///
/// Dibangun oleh GameHudBuilder.Ping (Go Vista > HUD > Buat ulang GameHudCanvas). Ikut tersembunyi bersama
/// GameHudCanvas (belum ada pemain, editor kontrol terbuka, GameplayHud.Hidden).
public sealed class PingIndicator : MonoBehaviour {
    private const float SampleSeconds = 1f;
    // Bobot sampel baru pada rata-rata bergerak: cukup cepat bereaksi, tapi angka tidak loncat-loncat.
    private const float SmoothingWeight = 0.5f;
    private const int BadSamplesBeforeWarning = 5;
    private const float WarningCooldownSeconds = 60f;
    private const float WarningPopSeconds = 0.3f;
    private const float WarningHoldSeconds = 4f;
    private const float WarningFadeSeconds = 0.4f;

    [SerializeField] private PingIndicatorView view;
    [Tooltip("Chip ping (ikon + angka). Disembunyikan kalau GameSettings.ShowPing mati.")]
    [SerializeField] private GameObject chip;
    [Tooltip("Peringatan koneksi buruk di bawah chip.")]
    [SerializeField] private CanvasGroup warning;

    private Realtime _realtime;
    private float _smoothedMs = -1f;
    private int _badSamples;
    private float _nextWarningTime;
    private Coroutine _sampling;
    private Coroutine _warningRoutine;

    private void Awake() {
        warning.alpha = 0f;
        view.SetDisconnected();
    }

    private void OnEnable() {
        GameSettings.ShowPingChanged += ApplyVisibility;
        ApplyVisibility();
        _sampling = StartCoroutine(SampleLoop());
    }

    private void OnDisable() {
        GameSettings.ShowPingChanged -= ApplyVisibility;
        if (_sampling != null) StopCoroutine(_sampling);
        _sampling = null;
        _warningRoutine = null;
        warning.alpha = 0f;
    }

    private void ApplyVisibility() => chip.SetActive(GameSettings.ShowPing);

    private IEnumerator SampleLoop() {
        var wait = new WaitForSecondsRealtime(SampleSeconds);
        while (true) {
            Sample();
            yield return wait;
        }
    }

    private void Sample() {
        if (_realtime == null) _realtime = FindAnyObjectByType<Realtime>();
        if (_realtime == null || !_realtime.connected) {
            _smoothedMs = -1f;
            _badSamples = 0;
            view.SetDisconnected();
            return;
        }

        float raw = _realtime.ping;
        _smoothedMs = _smoothedMs < 0f ? raw : Mathf.Lerp(_smoothedMs, raw, SmoothingWeight);
        int ms = Mathf.RoundToInt(_smoothedMs);
        view.SetPing(ms);
        TrackBadConnection(ms);
    }

    private void TrackBadConnection(int ms) {
        _badSamples = ms > PingIndicatorView.BadMs ? _badSamples + 1 : 0;
        if (_badSamples < BadSamplesBeforeWarning || Time.unscaledTime < _nextWarningTime) return;
        _badSamples = 0;
        _nextWarningTime = Time.unscaledTime + WarningCooldownSeconds;
        if (_warningRoutine == null) _warningRoutine = StartCoroutine(ShowWarning());
    }

    private IEnumerator ShowWarning() {
        Transform rect = warning.transform;
        for (float t = 0f; t < WarningPopSeconds; t += Time.unscaledDeltaTime) {
            warning.alpha = t / WarningPopSeconds;
            rect.localScale = Vector3.one * HudAnimation.Pop(t / WarningPopSeconds);
            yield return null;
        }
        warning.alpha = 1f;
        rect.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(WarningHoldSeconds);
        for (float t = 0f; t < WarningFadeSeconds; t += Time.unscaledDeltaTime) {
            warning.alpha = 1f - t / WarningFadeSeconds;
            yield return null;
        }
        warning.alpha = 0f;
        _warningRoutine = null;
    }
}
