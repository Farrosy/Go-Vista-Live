using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Busur O₂ di kanan minimap. Redup selama di darat dengan O₂ penuh, menyala saat kepala terendam.
/// Di 50% chip peringatan kuning muncul di tengah atas layar; di 20% busur berubah merah, berdenyut, dan bergetar.
public sealed class OxygenArcView : MonoBehaviour {
    private const float DimAlpha = 0.35f;
    private const float FadeSeconds = 0.25f;
    private const float DangerMinAlpha = 0.5f;
    private const float ShakePixels = 2.5f;
    private const float ShakeSpeed = 55f;
    private const string WarningMessage = "Segera kembali ke darat!";

    [SerializeField] private CanvasGroup arcGroup;
    [SerializeField] private RectTransform arcRect;
    [SerializeField] private Image fill;
    [SerializeField] private Image dangerFill;
    [SerializeField] private Image chip;
    [SerializeField] private Sprite chipWarning;
    [SerializeField] private Sprite chipDanger;
    [SerializeField] private TextMeshProUGUI chipLabel;

    private Vector2 _arcHome;
    private float _lit;

    private void Awake() {
        _arcHome = arcRect.anchoredPosition;
        chipLabel.text = WarningMessage;
        chip.gameObject.SetActive(false);
    }

    public void Render(VitalsState state, bool headUnderWater, float time, float deltaTime) {
        bool active = headUnderWater || !state.IsOxygenFull;
        _lit = Mathf.MoveTowards(_lit, active ? 1f : 0f, deltaTime / FadeSeconds);

        OxygenLevel level = state.Level;
        bool danger = level == OxygenLevel.Danger;
        fill.gameObject.SetActive(!danger);
        dangerFill.gameObject.SetActive(danger);
        MinimapArc.SetFraction(danger ? dangerFill : fill, state.OxygenFraction);

        float alpha = Mathf.Lerp(DimAlpha, 1f, _lit);
        if (danger) alpha *= Mathf.Lerp(DangerMinAlpha, 1f, HudAnimation.Pulse(time));
        arcGroup.alpha = alpha;
        float shake = danger ? Mathf.Sin(time * ShakeSpeed) * ShakePixels : 0f;
        arcRect.anchoredPosition = _arcHome + new Vector2(shake, 0f);

        chip.gameObject.SetActive(level != OxygenLevel.Normal);
        chip.sprite = danger ? chipDanger : chipWarning;
    }
}
