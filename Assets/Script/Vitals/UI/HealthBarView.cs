using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Bar HP: isi, sisa damage putih yang menyusut pelan, dan tampilan kritis di bawah 20%.
public sealed class HealthBarView : MonoBehaviour {
    private const float TrailDelaySeconds = 0.4f;
    private const float TrailFractionPerSecond = 0.35f;
    private const float PulseScale = 0.12f;

    [SerializeField] private Image frame;
    [SerializeField] private Sprite frameNormal;
    [SerializeField] private Sprite frameCritical;
    [SerializeField] private Image badge;
    [SerializeField] private Sprite badgeNormal;
    [SerializeField] private Sprite badgeCritical;
    [SerializeField] private RectTransform badgeGroup;
    [SerializeField] private Image fill;
    [SerializeField] private Sprite fillNormal;
    [SerializeField] private Sprite fillCritical;
    [SerializeField] private Image trail;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float innerWidth = 394f;
    [SerializeField] private float minFillWidth = 29f;

    private int _shownHealth = -1;
    private bool _critical;
    private float _shownFraction = 1f;
    private float _trailFraction = 1f;
    private float _trailHoldUntil;

    public void Render(VitalsState state, float time, float deltaTime) {
        if (state.Health != _shownHealth) {
            if (state.HealthFraction < _shownFraction) _trailHoldUntil = time + TrailDelaySeconds;
            else _trailFraction = state.HealthFraction;
            _shownHealth = state.Health;
            _shownFraction = state.HealthFraction;
            label.text = $"{state.Health} / {VitalsRules.MaxHealth}";
            SetWidth(fill, _shownFraction);
            SetCritical(state.IsHealthCritical);
        }

        if (time >= _trailHoldUntil)
            _trailFraction = Mathf.MoveTowards(_trailFraction, _shownFraction, TrailFractionPerSecond * deltaTime);
        SetWidth(trail, _trailFraction);

        float scale = _critical ? 1f + PulseScale * HudAnimation.Pulse(time) : 1f;
        badgeGroup.localScale = new Vector3(scale, scale, 1f);
    }

    private void SetCritical(bool critical) {
        _critical = critical;
        frame.sprite = critical ? frameCritical : frameNormal;
        badge.sprite = critical ? badgeCritical : badgeNormal;
        fill.sprite = critical ? fillCritical : fillNormal;
    }

    /// Sprite Sliced penyok kalau lebih sempit dari bordernya, jadi lebarnya dijaga dan disembunyikan di 0.
    private void SetWidth(Image image, float fraction) {
        image.enabled = fraction > 0f;
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(Mathf.Max(minFillWidth, innerWidth * fraction), rect.sizeDelta.y);
    }
}
