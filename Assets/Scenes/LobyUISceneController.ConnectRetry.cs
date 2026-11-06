using System.Collections;
using Normal.Realtime;
using UnityEngine;

/// Connecting to a room server fails now and then on some networks, and trying again usually works.
/// Network failures are retried here so the player does not have to; other failures (room full,
/// wrong code) are final and go through the normal handlers.
public partial class LobyUISceneController {
    private const int MaxConnectAttempts = 3;
    private const float ConnectRetryDelaySeconds = 1.5f;
    // A failed attempt normally reports after about 24 s, but some never report at all.
    private const float ConnectAttemptTimeoutSeconds = 28f;
    private const string ConnectFailedMessage =
        "Koneksi ke server gagal setelah beberapa kali percobaan. Periksa internetmu, lalu coba lagi.";

    private System.Action _connectAction;
    private string _connectStatus;
    private int _connectAttempt;
    private bool _connectTimedOut;
    private Coroutine _connectWatchdog;
    private Coroutine _connectRetryRoutine;

    private void RunConnectAttempt() {
        _connectTimedOut = false;
        StopConnectRoutines();
        _connectWatchdog = StartCoroutine(WatchConnectAttempt());
        try {
            _connectAction();
        } catch (System.Exception exception) {
            ClearConnectRetry();
            _busy = false;
            _quickmatchIntent = QuickmatchIntent.None;
            HideModal();
            SetStatus("Gagal memulai koneksi: " + exception.Message, StatusKind.Error);
            Debug.LogException(exception);
        }
    }

    private IEnumerator WatchConnectAttempt() {
        yield return new WaitForSecondsRealtime(ConnectAttemptTimeoutSeconds);
        _connectWatchdog = null;
        if (realtime == null || realtime.connected)
            yield break;

        // The disconnect events this causes are swallowed by TryRetryConnect while the flag is set.
        _connectTimedOut = true;
        realtime.Disconnect();
        yield return null;

        if (_connectAttempt < MaxConnectAttempts) {
            ScheduleConnectRetry();
            yield break;
        }

        ClearConnectRetry();
        _busy = false;
        _quickmatchIntent = QuickmatchIntent.None;
        HideModal();
        SetStatus(ConnectFailedMessage, StatusKind.Error);
        ShowAlertModal(JoinFailedTitle, ConnectFailedMessage);
    }

    /// True = this disconnect belongs to a connect attempt that is being retried, so ignore it.
    /// Called from both Normcore disconnect events, which fire for one and the same failure.
    private bool TryRetryConnect(DisconnectEvent disconnectEvent) {
        if (_connectRetryRoutine != null || _connectTimedOut)
            return true;
        if (_connectAction == null || !_busy || !IsNetworkConnectFailure(disconnectEvent))
            return false;
        if (_connectAttempt >= MaxConnectAttempts)
            return false;

        ScheduleConnectRetry();
        return true;
    }

    private static bool IsNetworkConnectFailure(DisconnectEvent disconnectEvent) {
        return disconnectEvent != null && !disconnectEvent.didConnectToRoom &&
               (disconnectEvent is FailedToConnectToServer || disconnectEvent is ConnectionFailedWithNetworkError);
    }

    private void ScheduleConnectRetry() {
        StopConnectRoutines();
        _connectAttempt++;
        string status = $"{_connectStatus} (percobaan {_connectAttempt}/{MaxConnectAttempts})";
        Debug.LogWarning($"[Lobby] Koneksi gagal, mencoba lagi ({_connectAttempt}/{MaxConnectAttempts}).");
        SetStatus(status, StatusKind.Busy);
        ShowConnectingModal(status);
        _connectRetryRoutine = StartCoroutine(RetryConnectAfterDelay());
    }

    private IEnumerator RetryConnectAfterDelay() {
        yield return new WaitForSecondsRealtime(ConnectRetryDelaySeconds);
        _connectRetryRoutine = null;
        RunConnectAttempt();
    }

    private void StopConnectRoutines() {
        if (_connectWatchdog != null)
            StopCoroutine(_connectWatchdog);
        if (_connectRetryRoutine != null)
            StopCoroutine(_connectRetryRoutine);
        _connectWatchdog = null;
        _connectRetryRoutine = null;
    }

    private void ClearConnectRetry() {
        StopConnectRoutines();
        _connectAction = null;
        _connectTimedOut = false;
    }
}
