using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Layar mati: isinya mengikuti penyebab mati, lalu backdrop, judul, card, tombol, dan hitung mundur
/// muncul berurutan. Hanya tampilan; alurnya diatur DeathFlowController.
public sealed class DeathScreenView : MonoBehaviour {
    private const float StepSeconds = 0.3f;
    private const float TitleSeconds = 0.25f;
    private const float SlamBackdropSeconds = 1f;
    private const string LeavingLabel = "Keluar…";

    private const float CountdownY = -372f;
    private const float CountdownYBelowInfo = -410f;

    // Urutan intro Drop, dihitung dari detik benturan.
    private const float DropBackdropAt = 0.4f;
    private const float DropBackdropSeconds = 0.8f;
    private const float DropBadgeAt = 1.6f;
    private const float DropBadgeSeconds = 0.4f;
    private const float DropTitleAt = 1.9f;
    private const float DropTitleSeconds = 0.6f;
    private const float DropSubtitleAt = 2.5f;
    private const float DropTipAt = 2.6f;
    private const float DropCardAt = 2.8f;
    private const float DropRiseSeconds = 0.35f;
    private const float DropRisePixels = 37f;
    private const float DropReviveAt = 3.1f;
    private const float DropLobbyAt = 3.2f;
    private const float DropButtonSeconds = 0.3f;
    private const float DropCountdownAt = 3.5f;
    private const float DropInteractableAt = 3.4f;

    [SerializeField] private DeathScreenContent[] contents;
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private Image backdropFill;
    [SerializeField] private Image backdropVignette;
    [SerializeField] private GameObject bubblesRoot;
    [SerializeField] private CanvasGroup badge;
    [SerializeField] private Image badgeCircle;
    [SerializeField] private Image badgeIcon;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private CanvasGroup subtitle;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private CanvasGroup tip;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private CanvasGroup card;
    [SerializeField] private Image cardImage;
    [SerializeField] private TextMeshProUGUI cardLabel;
    [SerializeField] private TextMeshProUGUI cardAmount;
    [SerializeField] private CanvasGroup buttons;
    [SerializeField] private Button reviveButton;
    [SerializeField] private CanvasGroup reviveGroup;
    [SerializeField] private TextMeshProUGUI reviveLabel;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private CanvasGroup lobbyGroup;
    [SerializeField] private TextMeshProUGUI lobbyLabel;
    [SerializeField] private CanvasGroup info;
    [SerializeField] private TextMeshProUGUI infoLabel;
    [SerializeField] private CanvasGroup countdown;
    [SerializeField] private TextMeshProUGUI countdownLabel;

    public event Action RevivePressed;
    public event Action LobbyPressed;

    // Posisi asal elemen yang digeser animasi, supaya bisa dipulihkan kalau alur terhenti di tengah.
    private readonly Dictionary<RectTransform, Vector2> _homePositions = new Dictionary<RectTransform, Vector2>();
    private DeathScreenContent _content;
    private int _shownSeconds = -1;

    private void Awake() {
        reviveButton.onClick.AddListener(() => RevivePressed?.Invoke());
        lobbyButton.onClick.AddListener(() => LobbyPressed?.Invoke());
    }

    public DeathScreenContent Prepare(int coinsLost, DeathCause cause) {
        gameObject.SetActive(true);
        _content = FindContent(cause);
        ApplyContent(_content);
        RestoreLayout();
        foreach (CanvasGroup group in new[] {
            backdrop, badge, title, subtitle, tip, card, buttons, reviveGroup, lobbyGroup, info, countdown,
        }) group.alpha = 0f;
        SetButtonsInteractable(false);
        card.gameObject.SetActive(coinsLost > 0);
        cardAmount.text = $"−{coinsLost}";
        _shownSeconds = -1;
        return _content;
    }

    /// Menampilkan isi layar sesuai gaya intro penyebab mati. Tombol baru bisa ditekan setelah terlihat.
    public IEnumerator PlayIntro() {
        return _content.intro == DeathScreenContent.IntroStyle.Drop ? PlayDropIntro() : PlaySlamIntro();
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

    private DeathScreenContent FindContent(DeathCause cause) {
        foreach (DeathScreenContent candidate in contents)
            if (candidate.cause == cause) return candidate;
        Debug.LogWarning($"[DeathScreen] Tidak ada isi layar untuk penyebab {cause}; memakai isi pertama.", this);
        return contents[0];
    }

    private void ApplyContent(DeathScreenContent content) {
        titleText.text = content.title;
        titleText.fontSize = content.titleSize;
        titleText.characterSpacing = content.titleSpacing;
        titleText.fontSharedMaterial = content.titleMaterial;
        subtitleText.text = content.subtitle;
        subtitleText.color = content.subtitleColor;
        tipText.text = content.tip;
        tipText.color = content.tipColor;
        cardLabel.color = content.cardLabelColor;
        reviveLabel.text = content.reviveLabel;
        countdownLabel.color = content.countdownColor;
        ApplyRespawnInfo(content);

        badgeCircle.sprite = content.badge;
        badgeIcon.sprite = content.icon;
        badgeIcon.rectTransform.sizeDelta = new Vector2(content.iconSize, content.iconSize);
        cardImage.sprite = content.card;
        backdropFill.color = content.backdropColor;
        backdropVignette.sprite = content.vignette;
        bubblesRoot.SetActive(content.showBubbles);

        lobbyButton.image.sprite = content.lobbyButton;
        lobbyButton.spriteState = new SpriteState {
            pressedSprite = content.lobbyButtonPressed,
            disabledSprite = content.lobbyButtonDisabled,
        };
    }

    private void ApplyRespawnInfo(DeathScreenContent content) {
        bool hasInfo = !string.IsNullOrEmpty(content.respawnInfo);
        info.gameObject.SetActive(hasInfo);
        infoLabel.text = content.respawnInfo;
        infoLabel.color = content.respawnInfoColor;
        RectTransform countdownRect = countdownLabel.rectTransform;
        countdownRect.anchoredPosition = new Vector2(countdownRect.anchoredPosition.x,
            hasInfo ? CountdownYBelowInfo : CountdownY);
    }

    private void RestoreLayout() {
        foreach (KeyValuePair<RectTransform, Vector2> home in _homePositions)
            home.Key.anchoredPosition = home.Value;
        badge.transform.localScale = Vector3.one;
        title.transform.localScale = Vector3.one;
    }

    private IEnumerator PlaySlamIntro() {
        reviveGroup.alpha = 1f;
        lobbyGroup.alpha = 1f;
        yield return FadeIn(backdrop, SlamBackdropSeconds);
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

    private IEnumerator PlayDropIntro() {
        buttons.alpha = 1f;
        StartCoroutine(After(DropBackdropAt, FadeIn(backdrop, DropBackdropSeconds)));
        StartCoroutine(After(DropBadgeAt, PopIn(badge, DropBadgeSeconds)));
        StartCoroutine(After(DropTitleAt, DropIn(title, DropTitleSeconds)));
        StartCoroutine(After(DropSubtitleAt, RiseIn(subtitle, DropRiseSeconds)));
        StartCoroutine(After(DropTipAt, RiseIn(tip, DropRiseSeconds)));
        StartCoroutine(After(DropCardAt, RiseIn(card, DropRiseSeconds)));
        StartCoroutine(After(DropReviveAt, FadeIn(reviveGroup, DropButtonSeconds)));
        StartCoroutine(After(DropLobbyAt, FadeIn(lobbyGroup, DropButtonSeconds)));
        StartCoroutine(After(DropCountdownAt, FadeIn(info, StepSeconds)));
        StartCoroutine(After(DropCountdownAt, FadeIn(countdown, StepSeconds)));
        yield return new WaitForSeconds(DropInteractableAt);
        SetButtonsInteractable(true);
    }

    private static IEnumerator After(float seconds, IEnumerator step) {
        yield return new WaitForSeconds(seconds);
        yield return step;
    }

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

    private static IEnumerator PopIn(CanvasGroup group, float seconds) {
        Transform target = group.transform;
        group.alpha = 1f;
        for (float t = 0f; t < seconds; t += Time.deltaTime) {
            float scale = HudAnimation.Pop(t / seconds);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private IEnumerator DropIn(CanvasGroup group, float seconds) {
        var rect = (RectTransform)group.transform;
        Vector2 home = HomeOf(rect);
        group.alpha = 1f;
        for (float t = 0f; t < seconds; t += Time.deltaTime) {
            float progress = t / seconds;
            rect.anchoredPosition = home + Vector2.up * HudAnimation.DropBounceOffset(progress);
            rect.localScale = HudAnimation.DropBounceScale(progress);
            yield return null;
        }
        rect.anchoredPosition = home;
        rect.localScale = Vector3.one;
    }

    private IEnumerator RiseIn(CanvasGroup group, float seconds) {
        var rect = (RectTransform)group.transform;
        Vector2 home = HomeOf(rect);
        for (float t = 0f; t < seconds; t += Time.deltaTime) {
            float progress = HudAnimation.EaseOut(t / seconds);
            group.alpha = progress;
            rect.anchoredPosition = home + Vector2.down * (DropRisePixels * (1f - progress));
            yield return null;
        }
        group.alpha = 1f;
        rect.anchoredPosition = home;
    }

    private Vector2 HomeOf(RectTransform rect) {
        if (!_homePositions.TryGetValue(rect, out Vector2 home)) {
            home = rect.anchoredPosition;
            _homePositions[rect] = home;
        }
        return home;
    }
}
