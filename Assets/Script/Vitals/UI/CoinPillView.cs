using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Saldo koin: angka menghitung ke saldo baru, chip −N turun lalu hilang.
public sealed class CoinPillView : MonoBehaviour {
    private const float RollSeconds = 0.5f;
    private const float ChipSeconds = 1.6f;
    private const float ChipDropPixels = 22f;

    [SerializeField] private TextMeshProUGUI balanceLabel;
    [SerializeField] private CanvasGroup minusChip;
    [SerializeField] private TextMeshProUGUI minusChipLabel;

    private RectTransform _chipRect;
    private Vector2 _chipHome;
    private float _displayed;
    private float _rollPerSecond;
    private int _shownBalance = -1;
    private float _chipTime = float.PositiveInfinity;

    private void Awake() {
        _chipRect = (RectTransform)minusChip.transform;
        _chipHome = _chipRect.anchoredPosition;
        minusChip.alpha = 0f;
        _displayed = SessionCoins.Balance;
        ShowBalance(SessionCoins.Balance);
    }

    private void OnEnable() => SessionCoins.Changed += OnCoinsChanged;
    private void OnDisable() => SessionCoins.Changed -= OnCoinsChanged;

    private void OnCoinsChanged(int delta) {
        _rollPerSecond = Mathf.Abs(SessionCoins.Balance - _displayed) / RollSeconds;
        if (delta >= 0) return;
        minusChipLabel.text = $"−{-delta}";
        _chipTime = 0f;
    }

    private void Update() {
        _displayed = Mathf.MoveTowards(_displayed, SessionCoins.Balance, _rollPerSecond * Time.deltaTime);
        ShowBalance(Mathf.RoundToInt(_displayed));

        if (_chipTime > ChipSeconds) return;
        _chipTime += Time.deltaTime;
        float t = _chipTime / ChipSeconds;
        minusChip.alpha = t < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, t);
        _chipRect.anchoredPosition = _chipHome + Vector2.down * (HudAnimation.EaseOut(t) * ChipDropPixels);
    }

    private void ShowBalance(int balance) {
        if (balance == _shownBalance) return;
        _shownBalance = balance;
        balanceLabel.text = CoinRules.FormatBalance(balance);
    }
}
