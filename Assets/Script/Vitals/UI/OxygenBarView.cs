using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Bar O₂: muncul dengan slide-in saat kepala terendam, kuning di 50%, merah di 20%.
public sealed class OxygenBarView : MonoBehaviour {
    private const float SlideDistance = 60f;
    private const float SlideSeconds = 0.25f;
    private const float PulseScale = 0.14f;
    private const float ShakePixels = 2.5f;
    private const float ShakeSpeed = 55f;
    private const string WarningMessage = "Segera kembali ke darat!";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform content;
    [SerializeField] private Image frame;
    [SerializeField] private Sprite frameNormal;
    [SerializeField] private Sprite frameWarning;
    [SerializeField] private Sprite frameDanger;
    [SerializeField] private Image badge;
    [SerializeField] private Sprite badgeNormal;
    [SerializeField] private Sprite badgeWarning;
    [SerializeField] private Sprite badgeDanger;
    [SerializeField] private RectTransform badgeGroup;
    [SerializeField] private Image[] segments;
    [SerializeField] private Sprite segmentFull;
    [SerializeField] private Sprite segmentWarning;
    [SerializeField] private Sprite segmentDanger;
    [SerializeField] private Sprite segmentEmpty;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image chip;
    [SerializeField] private Sprite chipWarning;
    [SerializeField] private Sprite chipDanger;
    [SerializeField] private TextMeshProUGUI chipLabel;

    private Vector2 _contentHome;
    private float _visibility;
    private int _shownPercent = -1;
    private int _shownSegments = -1;
    private OxygenLevel _shownLevel = (OxygenLevel)(-1);

    private void Awake() {
        _contentHome = content.anchoredPosition;
        group.alpha = 0f;
        chipLabel.text = WarningMessage;
    }

    public void Render(VitalsState state, bool headUnderWater, float time, float deltaTime) {
        bool show = headUnderWater || !state.IsOxygenFull;
        _visibility = Mathf.MoveTowards(_visibility, show ? 1f : 0f, deltaTime / SlideSeconds);
        group.alpha = _visibility;

        OxygenLevel level = state.Level;
        if (level != _shownLevel || state.OxygenSegments != _shownSegments) ApplyArt(state, level);

        int percent = Mathf.RoundToInt(state.OxygenFraction * 100f);
        if (percent != _shownPercent) {
            _shownPercent = percent;
            label.text = $"O<sub>2</sub> {percent}%";
        }

        bool danger = level == OxygenLevel.Danger;
        float shake = danger ? Mathf.Sin(time * ShakeSpeed) * ShakePixels : 0f;
        float slide = (1f - HudAnimation.EaseOut(_visibility)) * SlideDistance;
        content.anchoredPosition = _contentHome + new Vector2(shake - slide, 0f);

        float scale = danger ? 1f + PulseScale * HudAnimation.Pulse(time) : 1f;
        badgeGroup.localScale = new Vector3(scale, scale, 1f);
    }

    private void ApplyArt(VitalsState state, OxygenLevel level) {
        _shownLevel = level;
        _shownSegments = state.OxygenSegments;
        frame.sprite = Pick(level, frameNormal, frameWarning, frameDanger);
        badge.sprite = Pick(level, badgeNormal, badgeWarning, badgeDanger);
        Sprite lit = Pick(level, segmentFull, segmentWarning, segmentDanger);
        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = i < _shownSegments ? lit : segmentEmpty;

        chip.gameObject.SetActive(level != OxygenLevel.Normal);
        chip.sprite = level == OxygenLevel.Danger ? chipDanger : chipWarning;
    }

    private static Sprite Pick(OxygenLevel level, Sprite normal, Sprite warning, Sprite danger) {
        switch (level) {
            case OxygenLevel.Warning: return warning;
            case OxygenLevel.Danger: return danger;
            default: return normal;
        }
    }
}
