using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Notifikasi singkat di tengah atas layar: "Quest baru" saat quest diterima dan "Quest selesai" (+ hadiah koin)
/// saat quest selesai. Beberapa notifikasi diantrekan, tidak saling menimpa.
public sealed class QuestToastView : MonoBehaviour {
    private const float PopSeconds = 0.35f;
    private const float HoldSeconds = 2.6f;
    private const float FadeSeconds = 0.4f;

    [SerializeField] private CanvasGroup toast;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI detailLabel;

    private readonly Queue<(string title, string detail)> _pending = new Queue<(string, string)>();
    private Coroutine _showing;

    private void Awake() => toast.alpha = 0f;

    private void OnEnable() {
        QuestService.QuestAccepted += OnAccepted;
        QuestService.QuestCompleted += OnCompleted;
    }

    private void OnDisable() {
        QuestService.QuestAccepted -= OnAccepted;
        QuestService.QuestCompleted -= OnCompleted;
        _pending.Clear();
        _showing = null;
        toast.alpha = 0f;
    }

    private void OnAccepted(QuestData quest) => Enqueue("Quest baru!", quest.title);

    private void OnCompleted(QuestData quest) {
        string reward = quest.rewardCoins > 0 ? $"\n+{CoinRules.FormatBalance(quest.rewardCoins)} Go Vista Coin" : "";
        Enqueue("Quest selesai!", quest.title + reward);
    }

    private void Enqueue(string title, string detail) {
        _pending.Enqueue((title, detail));
        if (_showing == null && isActiveAndEnabled) _showing = StartCoroutine(ShowAll());
    }

    private IEnumerator ShowAll() {
        while (_pending.Count > 0) {
            (string title, string detail) = _pending.Dequeue();
            titleLabel.text = title;
            detailLabel.text = detail;
            Transform rect = toast.transform;
            for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime) {
                toast.alpha = t / PopSeconds;
                rect.localScale = Vector3.one * HudAnimation.Pop(t / PopSeconds);
                yield return null;
            }
            toast.alpha = 1f;
            rect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(HoldSeconds);
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime) {
                toast.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            toast.alpha = 0f;
        }
        _showing = null;
    }
}
