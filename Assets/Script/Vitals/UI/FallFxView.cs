using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Efek jatuh di layar: garis kecepatan selama jatuh yang pasti mematikan, lalu kilat, gelombang,
/// dan debu di kaki karakter saat benturan. Semua objeknya tetap dan dipakai ulang, jadi ringan di HP.
public sealed class FallFxView : MonoBehaviour {
    private const float LineFadeInSeconds = 0.1f;
    private const float LineFadeOutSeconds = 0.15f;
    private const float LineWidthMin = 7f;
    private const float LineWidthMax = 17f;
    private const float LineHeightMin = 368f;
    private const float LineHeightMax = 623f;
    private const float LineSpeedMin = 3117f;
    private const float LineSpeedMax = 4392f;

    private const float ImpactSeconds = 0.8f;
    private const float FlashSeconds = 0.25f;
    private const float RingSeconds = 0.4f;
    private const float RingScaleStart = 0.3f;
    private const float RingScaleEnd = 1.4f;
    private const float DustSeconds = 0.8f;
    private const float DustStaggerSeconds = 0.0175f;
    private const float DustScaleStart = 0.3f;
    private const float DustScaleEnd = 1.6f;
    private const float DustRisePixels = 34f;
    private const float DustPeakAlpha = 0.9f;
    private const float DustPeakAt = 0.25f;

    private static readonly Vector2[] DustOffsets = {
        new Vector2(-150f, 8f), new Vector2(-75f, -4f), new Vector2(0f, -10f),
        new Vector2(80f, -2f), new Vector2(155f, 10f),
    };

    [SerializeField] private LocalPlayerVitals vitals;
    [SerializeField] private RectTransform area;
    [SerializeField] private CanvasGroup speedLines;
    [SerializeField] private RectTransform[] lines;
    [SerializeField] private Image flash;
    [SerializeField] private Image ring;
    [SerializeField] private Image[] dust;

    private float[] _lineSpeeds;

    private void Awake() {
        _lineSpeeds = new float[lines.Length];
        speedLines.alpha = 0f;
        speedLines.gameObject.SetActive(false);
        SetImpactVisible(false);
    }

    private void Update() {
        bool falling = vitals.IsFallingLethally;
        if (!falling && speedLines.alpha <= 0f) return;

        float fadeSeconds = falling ? LineFadeInSeconds : LineFadeOutSeconds;
        speedLines.alpha = Mathf.MoveTowards(speedLines.alpha, falling ? 1f : 0f, Time.deltaTime / fadeSeconds);
        bool visible = speedLines.alpha > 0f;
        if (visible != speedLines.gameObject.activeSelf) {
            speedLines.gameObject.SetActive(visible);
            if (visible) ScatterLines();
        }
        if (visible) MoveLines();
    }

    /// Memutar efek benturan di posisi kaki karakter (posisi dunia).
    public void PlayImpact(Vector3 feetPosition) {
        Stop();
        StartCoroutine(RunImpact(feetPosition));
    }

    public void Stop() {
        StopAllCoroutines();
        SetImpactVisible(false);
    }

    private void ScatterLines() {
        float halfHeight = area.rect.height * 0.5f;
        for (int i = 0; i < lines.Length; i++) RespawnLine(i, Random.Range(-halfHeight, halfHeight));
    }

    private void MoveLines() {
        float halfHeight = area.rect.height * 0.5f;
        for (int i = 0; i < lines.Length; i++) {
            RectTransform line = lines[i];
            float y = line.anchoredPosition.y + _lineSpeeds[i] * Time.deltaTime;
            if (y - line.rect.height * 0.5f > halfHeight) RespawnLine(i, -halfHeight - line.rect.height * 0.5f);
            else line.anchoredPosition = new Vector2(line.anchoredPosition.x, y);
        }
    }

    private void RespawnLine(int index, float y) {
        float halfWidth = area.rect.width * 0.5f;
        lines[index].sizeDelta = new Vector2(Random.Range(LineWidthMin, LineWidthMax),
            Random.Range(LineHeightMin, LineHeightMax));
        lines[index].anchoredPosition = new Vector2(Random.Range(-halfWidth, halfWidth), y);
        _lineSpeeds[index] = Random.Range(LineSpeedMin, LineSpeedMax);
    }

    private IEnumerator RunImpact(Vector3 feetPosition) {
        Camera view = Camera.main;
        SetImpactVisible(true);
        for (float t = 0f; t < ImpactSeconds; t += Time.deltaTime) {
            SetAlpha(flash, HudAnimation.Flash(t / FlashSeconds));
            bool onScreen = TryProject(view, feetPosition, out Vector2 anchor);
            RenderRing(t, anchor, onScreen);
            RenderDust(t, anchor, onScreen);
            yield return null;
        }
        SetImpactVisible(false);
    }

    /// Dihitung tiap frame supaya debu dan gelombang tetap menempel di kaki selama kamera zoom dan goyang.
    private bool TryProject(Camera view, Vector3 worldPosition, out Vector2 anchor) {
        anchor = Vector2.zero;
        if (view == null) return false;
        Vector3 screen = view.WorldToScreenPoint(worldPosition);
        return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out anchor);
    }

    private void RenderRing(float time, Vector2 anchor, bool onScreen) {
        float progress = time / RingSeconds;
        ring.enabled = onScreen && progress < 1f;
        if (!ring.enabled) return;
        ring.rectTransform.anchoredPosition = anchor;
        float scale = Mathf.Lerp(RingScaleStart, RingScaleEnd, HudAnimation.EaseOut(progress));
        ring.rectTransform.localScale = new Vector3(scale, scale, 1f);
        SetAlpha(ring, 1f - progress);
    }

    private void RenderDust(float time, Vector2 anchor, bool onScreen) {
        for (int i = 0; i < dust.Length; i++) {
            float progress = (time - i * DustStaggerSeconds) / DustSeconds;
            Image puff = dust[i];
            puff.enabled = onScreen && progress >= 0f && progress < 1f;
            if (!puff.enabled) continue;
            Vector2 offset = DustOffsets[i % DustOffsets.Length] + Vector2.up * (DustRisePixels * progress);
            puff.rectTransform.anchoredPosition = anchor + offset;
            float scale = Mathf.Lerp(DustScaleStart, DustScaleEnd, HudAnimation.EaseOut(progress));
            puff.rectTransform.localScale = new Vector3(scale, scale, 1f);
            SetAlpha(puff, DustAlpha(progress));
        }
    }

    private static float DustAlpha(float progress) {
        return progress < DustPeakAt
            ? Mathf.Lerp(0f, DustPeakAlpha, progress / DustPeakAt)
            : Mathf.Lerp(DustPeakAlpha, 0f, (progress - DustPeakAt) / (1f - DustPeakAt));
    }

    private void SetImpactVisible(bool visible) {
        flash.gameObject.SetActive(visible);
        ring.gameObject.SetActive(visible);
        foreach (Image puff in dust) puff.gameObject.SetActive(visible);
    }

    private static void SetAlpha(Image image, float alpha) {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
