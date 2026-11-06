using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Layar mati: backdrop, judul menghentak, lalu card, tombol, dan hitung mundur muncul berurutan.
/// Hanya tampilan; alurnya diatur DeathFlowController.
public sealed class DeathScreenView : MonoBehaviour {
    private const float StepSeconds = 0.3f;
    private const float TitleSeconds = 0.25f;
    private const string LeavingLabel = "Keluar…";

    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private CanvasGroup badge;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private CanvasGroup subtitle;
    [SerializeField] private CanvasGroup tip;
    [SerializeField] private CanvasGroup card;
    [SerializeField] private TextMeshProUGUI cardAmount;
    [SerializeField] private CanvasGroup buttons;
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private TextMeshProUGUI lobbyLabel;
    [SerializeField] private CanvasGroup countdown;
    [SerializeField] private TextMeshProUGUI countdownLabel;

    public event Action RevivePressed;
    public event Action LobbyPressed;

    private int _shownSeconds = -1;

    private void Awake() {
        reviveButton.onClick.AddListener(() => RevivePressed?.Invoke());
        lobbyButton.onClick.AddListener(() => LobbyPressed?.Invoke());
    }

    public void Prepare(int coinsLost) {
        gameObject.SetActive(true);
        foreach (CanvasGroup group in new[] { backdrop, badge, title, subtitle, tip, card, buttons, countdown })
            group.alpha = 0f;
        SetButtonsInteractable(false);
        card.gameObject.SetActive(coinsLost > 0);
        cardAmount.text = $"−{coinsLost}";
        _shownSeconds = -1;
    }

    public IEnumerator FadeInBackdrop(float seconds) => FadeIn(backdrop, seconds);

    /// Menampilkan isi layar sesuai urutan prototipe. Tombol baru bisa ditekan setelah terlihat.
    public IEnumerator PlayContent() {
        badge.alpha = 1f;
        yield return PunchIn(title);
        yield return new WaitForSeconds(StepSeconds);
        yield return FadeIn(subtitle, StepSeconds);
        tip.alpha = 1f;
        yield return FadeIn(card, StepSeconds);
        yield return FadeIn(buttons, StepSeconds);
        SetButtonsInteractable(true);
        yield return FadeIn(countdown, StepSeconds);
    }

    public void SetCountdown(int seconds) {
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;
        countdownLabel.text = $"Otomatis bangkit dalam <color=#FFD166>{seconds}</color> detik";
    }

    public void SetButtonsInteractable(bool interactable) {
        reviveButton.interactable = interactable;
        lobbyButton.interactable = interactable;
    }

    public void ShowLeaving() {
        SetButtonsInteractable(false);
        lobbyLabel.text = LeavingLabel;
    }

    public void Hide() => gameObject.SetActive(false);


    private static IEnumerator FadeIn(CanvasGroup group, float seconds) {
        for (float t = 0f; t < seconds; t += Time.deltaTime) {
            group.alpha = HudAnimation.EaseOut(t / seconds);
            yield return null;
        }
        group.alpha = 1f;
    }

    private static IEnumerator PunchIn(CanvasGroup group) {
        Transform target = group.transform;
        for (float t = 0f; t < TitleSeconds; t += Time.deltaTime) {
            float progress = t / TitleSeconds;
            group.alpha = HudAnimation.EaseOut(progress);
            float scale = HudAnimation.Punch(progress);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        group.alpha = 1f;
        target.localScale = Vector3.one;
    }
}
