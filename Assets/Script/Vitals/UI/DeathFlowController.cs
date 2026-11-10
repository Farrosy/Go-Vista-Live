using System.Collections;
using UnityEngine;

/// Mengatur alur mati dari O₂/HP habis sampai pemain bangkit atau kembali ke lobby.
/// Game ini multiplayer, jadi dunia tetap berjalan: tidak ada Time.timeScale.
public sealed class DeathFlowController : MonoBehaviour {
    private const float ZoomSeconds = 1f;
    private const float ZoomDistanceScale = 0.6f;
    private const float ShakeSeconds = 0.35f;
    private const float ShakeMetresPerMockPixel = 0.006f;
    private const float AutoReviveSeconds = 8f;
    private const float SyncBeforeShowSeconds = 0.3f;

    [SerializeField] private LocalPlayerVitals vitals;
    [SerializeField] private DeathScreenView screen;
    [SerializeField] private GameHudView hud;
    [SerializeField] private FallFxView fallFx;

    private enum Choice { None, Revive, Lobby }

    private bool _active;
    private Choice _choice;
    private PlayerController _player;
    private PlayerVenueSync _venueSync;
    private CameraOrbit _orbit;

    private void Awake() => screen.Hide();

    private void OnEnable() {
        vitals.Died += OnDied;
        screen.RevivePressed += OnRevivePressed;
        screen.LobbyPressed += OnLobbyPressed;
    }

    private void OnDisable() {
        vitals.Died -= OnDied;
        screen.RevivePressed -= OnRevivePressed;
        screen.LobbyPressed -= OnLobbyPressed;
        if (_active) ResetCamera();
    }

    private void Update() {
        // Avatar hilang di tengah alur (koneksi putus): jangan tinggalkan layar mati dan kunci input.
        if (_active && _player == null) Abort();
    }

    private void OnDied() {
        if (_active) return;
        _active = true;
        _choice = Choice.None;
        _player = PlayerController.Local;
        _venueSync = _player.GetComponent<PlayerVenueSync>();
        _orbit = FindAnyObjectByType<CameraOrbit>();
        StartCoroutine(RunDeath());
    }

    private void OnRevivePressed() => Choose(Choice.Revive);
    private void OnLobbyPressed() => Choose(Choice.Lobby);

    private void Choose(Choice choice) {
        if (_choice != Choice.None) return;
        _choice = choice;
        screen.SetButtonsInteractable(false);
    }

    private IEnumerator RunDeath() {
        LockPlayer();
        int coinsLost = SessionCoins.ApplyDeathPenalty();
        DeathScreenContent content = screen.Prepare(coinsLost, vitals.State.Cause);
        if (content.impactEffects) PlayImpactEffects();
        StartCoroutine(ZoomCamera());
        // Tertabrak sampai mati: tunggu animasi rebahan dan masuk ke tanah selesai baru avatar disembunyikan.
        StartCoroutine(HideAvatarAfterDelay(Mathf.Max(content.hideAvatarAfterSeconds, _player.DeathSinkRemaining)));
        StartCoroutine(HideHudAfterDelay(content.hideHudAfterSeconds));

        yield return screen.PlayIntro();

        float remaining = AutoReviveSeconds;
        while (_choice == Choice.None && remaining > 0f) {
            screen.SetCountdown(Mathf.CeilToInt(remaining));
            remaining -= Time.deltaTime;
            yield return null;
        }
        if (_choice == Choice.None) Choose(Choice.Revive);

        if (_choice == Choice.Lobby && TryLeaveToLobby()) yield break;
        yield return Revive();
    }

    private void LockPlayer() {
        _player.SetFrozen(true);
        MobileControls.SetVisible(false);
        ChatPanel.SetSuppressed(true);
        if (InteractButtonUI.Instance != null) InteractButtonUI.Instance.SetSuppressed(true);
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetDeathLocked(true);
    }

    private IEnumerator ZoomCamera() {
        if (_orbit == null) yield break;
        for (float t = 0f; t < ZoomSeconds; t += Time.deltaTime) {
            _orbit.DistanceScale = Mathf.Lerp(1f, ZoomDistanceScale, HudAnimation.EaseOut(t / ZoomSeconds));
            yield return null;
        }
        _orbit.DistanceScale = ZoomDistanceScale;
    }

    private void PlayImpactEffects() {
        Bounds body = _player.GetComponent<CharacterController>().bounds;
        fallFx.PlayImpact(new Vector3(body.center.x, body.min.y, body.center.z));
        StartCoroutine(ShakeCamera());
    }

    private IEnumerator ShakeCamera() {
        if (_orbit == null) yield break;
        for (float t = 0f; t < ShakeSeconds; t += Time.deltaTime) {
            _orbit.ShakeOffset = HudAnimation.Shake(t / ShakeSeconds) * ShakeMetresPerMockPixel;
            yield return null;
        }
        _orbit.ShakeOffset = Vector2.zero;
    }

    private IEnumerator HideAvatarAfterDelay(float seconds) {
        yield return new WaitForSeconds(seconds);
        if (_venueSync != null) _venueSync.SetHidden(true);
    }

    private IEnumerator HideHudAfterDelay(float seconds) {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        hud.SetDeathScreenMode(true);
    }

    private bool TryLeaveToLobby() {
        LobyUISceneController lobby = FindAnyObjectByType<LobyUISceneController>();
        if (lobby == null) {
            Debug.LogWarning("[DeathFlow] LobyUISceneController tidak ditemukan; pemain dibangkitkan di darat.", this);
            return false;
        }
        screen.ShowLeaving();
        lobby.LeaveRoomToLobby();
        return true;
    }

    private IEnumerator Revive() {
        ISceneTransition transition = VenueTravelManager.Instance != null ? VenueTravelManager.Instance.Transition : null;
        if (transition != null) yield return transition.Cover();
        if (_player == null) {
            Abort();
            yield break;
        }

        screen.Hide();
        vitals.GetRespawnPose(out Vector3 position, out Quaternion rotation);
        _player.TeleportTo(position, rotation);
        vitals.Revive();
        if (_orbit != null) {
            _orbit.DistanceScale = 1f;
            _orbit.SetTarget(_player.transform);
        }

        // Beri waktu posisi baru sampai ke pemain lain supaya avatar tidak terlihat meluncur dari laut.
        yield return new WaitForSeconds(SyncBeforeShowSeconds);
        if (_player == null) {
            Abort();
            yield break;
        }
        if (_venueSync != null) _venueSync.SetHidden(false);
        UnlockPlayer();
        if (transition != null) yield return transition.Reveal();
        _active = false;
    }

    private void UnlockPlayer() {
        if (_player != null) _player.SetFrozen(false);
        hud.SetDeathScreenMode(false);
        ChatPanel.SetSuppressed(false);
        if (InteractButtonUI.Instance != null) InteractButtonUI.Instance.SetSuppressed(false);
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetDeathLocked(false);
        if (_player != null) MobileControls.SetVisible(true);
    }

    private void Abort() {
        StopAllCoroutines();
        _active = false;
        screen.Hide();
        fallFx.Stop();
        ResetCamera();
        if (VenueTravelManager.Instance != null) VenueTravelManager.Instance.Transition?.ResetImmediate();
        UnlockPlayer();
    }

    private void ResetCamera() {
        if (_orbit == null) return;
        _orbit.DistanceScale = 1f;
        _orbit.ShakeOffset = Vector2.zero;
    }
}
