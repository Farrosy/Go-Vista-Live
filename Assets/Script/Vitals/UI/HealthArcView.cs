using UnityEngine;
using UnityEngine.UI;

/// Busur HP di kiri minimap: isi, sisa damage pudar yang menyusut pelan, dan berkedip di bawah 20%.
public sealed class HealthArcView : MonoBehaviour {
    private const float TrailDelaySeconds = 0.4f;
    private const float TrailFractionPerSecond = 0.35f;
    private const float CriticalMinAlpha = 0.45f;

    [SerializeField] private Image fill;
    [SerializeField] private Image trail;

    private int _shownHealth = -1;
    private bool _critical;
    private float _shownFraction = 1f;
    private float _trailFraction = 1f;
    private float _trailHoldUntil;
    private Color _fillColor;

    private void Awake() => _fillColor = fill.color;

    public void Render(VitalsState state, float time, float deltaTime) {
        if (state.Health != _shownHealth) {
            if (state.HealthFraction < _shownFraction) _trailHoldUntil = time + TrailDelaySeconds;
            else _trailFraction = state.HealthFraction;
            _shownHealth = state.Health;
            _shownFraction = state.HealthFraction;
            _critical = state.IsHealthCritical;
            MinimapArc.SetFraction(fill, _shownFraction);
        }

        if (time >= _trailHoldUntil)
            _trailFraction = Mathf.MoveTowards(_trailFraction, _shownFraction, TrailFractionPerSecond * deltaTime);
        MinimapArc.SetFraction(trail, _trailFraction);

        Color color = _fillColor;
        if (_critical) color.a *= Mathf.Lerp(CriticalMinAlpha, 1f, HudAnimation.Pulse(time));
        fill.color = color;
    }
}
