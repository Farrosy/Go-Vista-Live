using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Normal.Realtime;
using UnityEngine;

/// A room server in the nearest region can be unreachable from a player's network even though other
/// regions work (seen with Asia Southeast). After a network failure the connection is retried on the
/// other regions, nearest first, and for the next day connections go straight there. Quickmatch would
/// put the player back in the same empty room whatever the region, so failed rooms are excluded. Other
/// failures (room full, wrong code) are final and go through the normal handlers.
public partial class LobyUISceneController {
    private const int MaxConnectAttempts = 3;
    private const float ConnectRetryDelaySeconds = 1.5f;
    // A failed attempt normally reports after about 24 s, but some never report at all.
    private const float ConnectAttemptTimeoutSeconds = 28f;
    // A healthy connection takes 3-5 s, so the nearest region gets little time before the others are tried.
    private const float NearestRegionTimeoutSeconds = 10f;
    private const string RegionFailureTimeKey = "Lobby.RegionFailureUtc";
    private const double RegionFailureMemoryHours = 24d;
    private const float RegionListTimeoutSeconds = 8f;
    private const string ConnectFailedMessage =
        "Tidak bisa terhubung ke server room. Coba lagi beberapa saat lagi.";

    private static Room.RegionMetadata[] s_fallbackRegions;
    private static readonly List<string> s_unreachableRoomCodes = new List<string>();

    private System.Action<Room.ConnectOptions> _connectAction;
    private string _connectStatus;
    private int _connectAttempt;
    private bool _connectTimedOut;
    private Coroutine _connectWatchdog;
    private Coroutine _connectRetryRoutine;

    private void BeginConnect() {
        if (s_fallbackRegions == null && HasRecentRegionFailure())
            _connectRetryRoutine = StartCoroutine(LoadRegionsThenConnect());
        else
            RunConnectAttempt();
    }

    private IEnumerator LoadRegionsThenConnect() {
        yield return LoadFallbackRegions();
        _connectRetryRoutine = null;
        RunConnectAttempt();
    }

    private static bool HasRecentRegionFailure() {
        return long.TryParse(PlayerPrefs.GetString(RegionFailureTimeKey, ""), out long ticks) &&
               ticks > 0 && ticks <= System.DateTime.UtcNow.Ticks &&
               (System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc)).TotalHours < RegionFailureMemoryHours;
    }

    private void RunConnectAttempt() {
        _connectTimedOut = false;
        StopConnectRoutines();
        _connectWatchdog = StartCoroutine(WatchConnectAttempt());
        try {
            _connectAction(new Room.ConnectOptions { preferredRegions = s_fallbackRegions });
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
        yield return new WaitForSecondsRealtime(s_fallbackRegions == null ? NearestRegionTimeoutSeconds : ConnectAttemptTimeoutSeconds);
        _connectWatchdog = null;
        if (realtime == null || realtime.connected)
            yield break;

        RememberUnreachableRoom(realtime.room != null ? realtime.room.quickmatchRoomCode : null);

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
        RememberUnreachableRoom(disconnectEvent.quickmatchRoomCode);
        if (_connectAttempt >= MaxConnectAttempts)
            return false;

        ScheduleConnectRetry();
        return true;
    }

    private static bool IsNetworkConnectFailure(DisconnectEvent disconnectEvent) {
        return disconnectEvent != null && !disconnectEvent.didConnectToRoom &&
               (disconnectEvent is FailedToConnectToServer || disconnectEvent is ConnectionFailedWithNetworkError);
    }

    private static void RememberUnreachableRoom(string roomCode) {
        if (!string.IsNullOrEmpty(roomCode) && !s_unreachableRoomCodes.Contains(roomCode))
            s_unreachableRoomCodes.Add(roomCode);
    }

    private Room.QuickmatchOptions QuickmatchOptionsExcludingRooms() {
        var excluded = new List<string>(_excludedRoomCodes);
        excluded.AddRange(s_unreachableRoomCodes);
        return new Room.QuickmatchOptions { excludeRoomCodes = excluded.ToArray() };
    }

    private void ScheduleConnectRetry() {
        StopConnectRoutines();
        if (s_fallbackRegions == null) {
            PlayerPrefs.SetString(RegionFailureTimeKey, System.DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }
        _connectAttempt++;
        string status = $"{_connectStatus} (mencoba server lain)";
        Debug.LogWarning($"[Lobby] Koneksi gagal, mencoba lagi ({_connectAttempt}/{MaxConnectAttempts}).");
        SetStatus(status, StatusKind.Busy);
        ShowConnectingModal(status);
        _connectRetryRoutine = StartCoroutine(RetryConnectAfterDelay());
    }

    private IEnumerator RetryConnectAfterDelay() {
        yield return new WaitForSecondsRealtime(ConnectRetryDelaySeconds);
        if (s_fallbackRegions == null)
            yield return LoadFallbackRegions();
        _connectRetryRoutine = null;
        RunConnectAttempt();
    }

    /// Every region this client can ping except the nearest, which is the one that just failed.
    private IEnumerator LoadFallbackRegions() {
        var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(RegionListTimeoutSeconds));
        Task<Room.GetRegionsListResponse> request = realtime.GetRegionsListAsync(default, timeout.Token);
        while (!request.IsCompleted)
            yield return null;
        timeout.Dispose();

        if (request.Status != TaskStatus.RanToCompletion) {
            Debug.LogWarning("[Lobby] Daftar region tidak bisa diambil: " + request.Exception?.GetBaseException().Message);
            yield break;
        }

        var reachable = new List<Room.RegionMetadata>();
        foreach (Room.RegionMetadata region in request.Result.regions) {
            if (region.ping.HasValue)
                reachable.Add(region);
        }
        reachable.Sort((a, b) => a.ping.Value.CompareTo(b.ping.Value));
        if (reachable.Count < 2)
            yield break;

        reachable.RemoveAt(0);
        s_fallbackRegions = reachable.ToArray();
        Debug.LogWarning($"[Lobby] Memakai region cadangan, mulai dari {s_fallbackRegions[0].displayName}.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFallbackRegions() {
        s_fallbackRegions = null;
        s_unreachableRoomCodes.Clear();
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
