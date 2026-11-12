using System;
using System.Collections;
using UnityEngine;

/// Vio & Vira yang "menemani" tur: muncul (poof) di depan gedung saat pemain sampai, menjelaskan gedung
/// lewat dialog, lalu menghilang lagi. Hanya ada di client pemain itu (tidak disinkronkan), jadi aman untuk
/// multiplayer dan tidak perlu pathfinding.
public sealed class CityTourGuide : MonoBehaviour {
    private const float PopSeconds = 0.35f;
    private const float PauseBeforeTalkSeconds = 0.25f;
    private const float PauseBeforeLeaveSeconds = 0.4f;
    private const float MinDistanceFromPlayer = 2.6f;

    public static CityTourGuide Instance { get; private set; }

    [SerializeField] private MascotDuo duo;
    [Tooltip("Efek asap 'poof' saat muncul dan hilang. Opsional.")]
    [SerializeField] private ParticleSystem poof;

    private Vector3 _baseScale;

    public bool IsPresenting { get; private set; }

    private void Awake() {
        Instance = this;
        _baseScale = duo.transform.localScale;
        duo.gameObject.SetActive(false);
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    // Pindah venue/scene di tengah kemunculan: coroutine berhenti, jadi lepaskan kunci gerak di sini
    // supaya pemain tidak tertahan (kalau dialog sudah berjalan, DialogueUI yang melepasnya).
    private void OnDisable() {
        if (!IsPresenting) return;
        IsPresenting = false;
        duo.Disengage();
        duo.gameObject.SetActive(false);
        if (DialogueUI.Instance == null || !DialogueUI.Instance.IsActive) SetPlayerLocked(false);
    }

    /// True kalau pemain lokal bebas diajak bicara: tidak sedang dialog, mati, membuka panel/pause, atau terjatuh.
    /// TODO (saat pemain bisa menyetir): tambahkan cek "sedang di kendaraan" di sini, supaya maskot baru muncul
    /// setelah pemain turun.
    public static bool CanPresent {
        get {
            PlayerController player = PlayerController.Local;
            if (player == null || player.IsKnockedDown || !player.IsGroundedForAnim) return false;
            if (PlayerInputHub.Instance == null || PlayerInputHub.Instance.InputLocked) return false;
            return DialogueUI.Instance != null && !DialogueUI.Instance.IsActive;
        }
    }

    /// Munculkan maskot di 'spot' (menghadap pemain), putar dialog, lalu hilangkan. 'done' dipanggil setelah selesai.
    public bool Present(Transform spot, DialogueData dialogue, Action done) {
        if (IsPresenting || spot == null || dialogue == null || !CanPresent) return false;
        StartCoroutine(PresentRoutine(spot, dialogue, done));
        return true;
    }

    private IEnumerator PresentRoutine(Transform spot, DialogueData dialogue, Action done) {
        IsPresenting = true;
        // Kunci gerak pemain sejak maskot muncul: kalau pemain masih berjalan selama animasi muncul, maskot
        // menghadap posisi lama dan dari kamera dialog terlihat berbaris depan-belakang, bukan berdampingan.
        // Kunci dilepas DialogueUI saat dialog selesai.
        SetPlayerLocked(true);
        Transform duoTransform = duo.transform;
        Vector3 position = AppearPosition(spot);
        duoTransform.SetPositionAndRotation(position, FacingPlayer(position, spot.rotation));
        duo.gameObject.SetActive(true);
        PlayPoof(position);
        yield return Scale(0f, 1f);
        yield return new WaitForSeconds(PauseBeforeTalkSeconds);

        // Hadapkan ulang tepat sebelum bicara, supaya Vio & Vira berdampingan di depan pemain.
        duoTransform.rotation = FacingPlayer(duoTransform.position, duoTransform.rotation);
        bool ended = false;
        duo.Engage();
        bool started = DialogueUI.Instance != null &&
                       DialogueUI.Instance.StartDialogue(dialogue, duo.FocusPoint, () => ended = true);
        if (!started) {
            Debug.LogWarning($"[Tour] Dialog '{dialogue.name}' tidak bisa dimulai.", this);
            SetPlayerLocked(false);
        }
        while (started && !ended) yield return null;
        duo.Disengage();

        yield return new WaitForSeconds(PauseBeforeLeaveSeconds);
        PlayPoof(duoTransform.position);
        yield return Scale(1f, 0f);
        duo.gameObject.SetActive(false);
        IsPresenting = false;
        done?.Invoke();
    }

    /// Titik muncul di depan pintu, tapi tidak pernah lebih dekat dari MinDistanceFromPlayer ke pemain
    /// (kalau pemain berdiri di titik itu, maskot digeser menjauh dari pemain, ke arah luar).
    private static Vector3 AppearPosition(Transform spot) {
        PlayerController player = PlayerController.Local;
        if (player == null) return spot.position;
        Vector3 fromPlayer = spot.position - player.transform.position;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude >= MinDistanceFromPlayer * MinDistanceFromPlayer) return spot.position;
        Vector3 direction = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : spot.forward;
        Vector3 position = player.transform.position + direction * MinDistanceFromPlayer;
        position.y = spot.position.y;
        return position;
    }

    private static Quaternion FacingPlayer(Vector3 from, Quaternion fallback) {
        PlayerController player = PlayerController.Local;
        if (player == null) return fallback;
        Vector3 direction = player.transform.position - from;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : fallback;
    }

    private static void SetPlayerLocked(bool locked) {
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetLocked(locked);
    }

    private IEnumerator Scale(float from, float to) {
        for (float t = 0f; t < PopSeconds; t += Time.deltaTime) {
            float k = Mathf.LerpUnclamped(from, to, HudAnimation.EaseOut(t / PopSeconds));
            duo.transform.localScale = _baseScale * k;
            yield return null;
        }
        duo.transform.localScale = _baseScale * to;
    }

    private void PlayPoof(Vector3 position) {
        if (poof == null) return;
        poof.transform.position = position;
        poof.Play(true);
    }
}
