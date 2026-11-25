using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kursi yang bisa diduduki (dipakai kursi barber; nanti jadi tempat ganti rambut / jenggot). Pemain berdiri di
/// lingkaran cahaya (BuildingEntranceMarker), menekan "Duduk" (Interactable di objek ini), lalu avatar berjalan
/// sendiri ke <see cref="Entry"/> (lantai di samping pijakan kaki). Dari sana SeatOccupant mengambil alih: naik ke
/// pijakan (<see cref="StandSpot"/>), berbalik, duduk dengan animasi, dan bergeser ke <see cref="Seat"/>. Tombol
/// Interact berubah jadi "Berdiri" (<see cref="standUp"/>) selama duduk; urutannya dibalik sampai kembali di Entry.
///
/// Collider kursi + meja cermin diabaikan oleh avatar sendiri selama urutan ini (badan masuk ke celah antara dudukan
/// dan meja). Joystick, lompat, dan lari disembunyikan. Kamera dipegang ChairSeatCamera (kalau ada di objek ini):
/// berayun ke depan pemain dan terkunci sampai kembali berdiri. Animasi duduk terlihat pemain lain
/// lewat PlayerAnimSync, kaki di pijakan + tangan menjauhi meja lewat SeatIK.
/// Dipasang oleh menu Go Vista > Interior > Pasang Kursi Barber (BarberChairBuilder).
/// </summary>
[RequireComponent(typeof(Interactable))]
public sealed class ChairSeat : MonoBehaviour {
    private const float WaypointRadius = 0.15f;
    private const float ArriveDistance = 0.05f;
    private const float SlowDownDistance = 0.4f;
    private const float MinScriptedSpeed = 0.3f;
    private const float LegTimeout = 3f;

    private enum Phase { Idle, Approaching, Sitting, Seated, Leaving }

    private static readonly List<ChairSeat> All = new List<ChairSeat>();

    [Tooltip("Pose root avatar saat duduk: posisi = di atas dudukan (tinggi sudah termasuk), sumbu biru = arah hadap.")]
    [SerializeField] private Transform seat;
    [Tooltip("Di atas pijakan kaki, di depan dudukan: tempat avatar berdiri sebelum duduk mundur / setelah berdiri.")]
    [SerializeField] private Transform standSpot;
    [Tooltip("Lantai di samping pijakan kaki: avatar berjalan biasa ke sini, lalu melangkah naik ke StandSpot.")]
    [SerializeField] private Transform entry;
    [Tooltip("Permukaan atas pijakan kaki (garis tempat telapak kaki diletakkan saat duduk), sumbu biru = arah hadap.")]
    [SerializeField] private Transform footrest;
    [Tooltip("Setengah lebar pijakan kaki (meter).")]
    [SerializeField] private float footrestHalfWidth = 0.3f;
    [Tooltip("Batas maju-mundur telapak kaki saat berdiri di pijakan, relatif Footrest (meter, + = ke arah hadap).")]
    [SerializeField] private float footMin = -0.05f;
    [SerializeField] private float footMax = 0.15f;
    [Tooltip("Batas maju-mundur tangan saat berjalan di celah (relatif Footrest): belakang = sandaran tangan, depan = meja.")]
    [SerializeField] private float handMin = -0.14f;
    [SerializeField] private float handMax = 0.16f;
    [Tooltip("Setengah panjang celah dudukan–meja (meter ke samping dari tengah kursi) tempat tangan dijaga.")]
    [SerializeField] private float gapHalfLength = 1.0f;
    [Tooltip("Titik lantai opsional dari lingkaran ke Entry (urut).")]
    [SerializeField] private Transform[] path = new Transform[0];
    [Tooltip("Interactable \"Berdiri\" (child, nonaktif). Diaktifkan + jadi target tombol Interact selama duduk.")]
    [SerializeField] private Interactable standUp;
    [Tooltip("Collider kursi + meja cermin yang diabaikan avatar sendiri selama naik/duduk/turun.")]
    [SerializeField] private Collider[] chairColliders = new Collider[0];
    [Tooltip("Lingkaran cahaya di lantai: disembunyikan dari tekan Duduk sampai avatar kembali di lantai.")]
    [SerializeField] private Renderer[] hideWhileBusy = new Renderer[0];

    /// Pemain lokal menekan "Duduk" (avatar mulai berjalan ke kursi).
    public event Action SitStarted;
    /// Avatar lokal sudah duduk diam (mis. buka editor rambut di sini).
    public event Action Seated;
    /// Avatar lokal mulai berdiri (tutup UI yang dibuka saat duduk).
    public event Action StandingUp;
    /// Avatar lokal sudah turun dari kursi dan kontrol kembali.
    public event Action Left;

    public bool IsLocalSeated => _phase == Phase.Seated;
    public Transform Seat => seat;
    public Transform StandSpot => standSpot;
    public Transform Entry => entry;
    public Transform Footrest => footrest;
    public float FootrestHalfWidth => footrestHalfWidth;
    public float FootMin => footMin;
    public float FootMax => footMax;
    public float HandMin => handMin;
    public float HandMax => handMax;
    public float GapHalfLength => gapHalfLength;
    /// Collider kursi + meja cermin (diabaikan avatar selama duduk, dan oleh jepit tembok ChairSeatCamera).
    public Collider[] ChairColliders => chairColliders;

    private Interactable _interactable;
    private Phase _phase = Phase.Idle;
    private PlayerController _player;
    private SeatOccupant _occupant;
    private Collider[] _playerColliders = new Collider[0];
    private bool _ignoring;
    private bool _controlsHidden;

    /// Kursi terdekat dari <paramref name="position"/> dalam radius (dipakai SeatIK avatar pemain lain).
    public static ChairSeat FindNear(Vector3 position, float radius) {
        ChairSeat best = null;
        float bestSqr = radius * radius;
        foreach (ChairSeat chair in All) {
            if (!chair || !chair.seat) continue;
            float sqr = (chair.seat.position - position).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = chair;
        }
        return best;
    }

    void Awake() {
        _interactable = GetComponent<Interactable>();
        _interactable.onInteract.AddListener(Sit);
        if (standUp) {
            standUp.onInteract.AddListener(Stand);
            standUp.gameObject.SetActive(false);
        }
    }

    void OnEnable() => All.Add(this);

    void OnDisable() {
        All.Remove(this);
        Abort();
    }

    void OnDestroy() {
        if (_interactable) _interactable.onInteract.RemoveListener(Sit);
        if (standUp) standUp.onInteract.RemoveListener(Stand);
        Abort();
    }

    // ---------------------------------------------------------------- Duduk

    private void Sit() {
        PlayerController local = PlayerController.Local;
        if (_phase != Phase.Idle || !seat || !standSpot || !entry || !local || local.InVehicle || local.IsSeated ||
            local.IsKnockedDown || local.IsFrozen)
            return;
        if (!SeatOccupant.HasSeatedParam(local.Animator)) return;
        _player = local;
        _occupant = local.GetComponent<SeatOccupant>();
        if (!_occupant) _occupant = local.gameObject.AddComponent<SeatOccupant>();

        _phase = Phase.Approaching;
        _interactable.enabled = false; // prompt "Duduk" hilang
        SetControlsHidden(true);
        SetIgnoreChair(true);
        SetRingHidden(true);
        StartCoroutine(SitRoutine());
        SitStarted?.Invoke();
    }

    private IEnumerator SitRoutine() {
        foreach (Transform point in path) {
            if (point) yield return WalkTo(point.position, WaypointRadius);
        }
        yield return WalkTo(entry.position, ArriveDistance);
        if (!_player || !_occupant.BeginSit(this)) {
            Finish();
            yield break;
        }

        _phase = Phase.Sitting;
        while (_player && _occupant.IsBusy && !_occupant.IsSeated) yield return null;
        if (!_player || !_occupant.IsSeated) {
            Finish();
            yield break;
        }

        _phase = Phase.Seated;
        if (standUp) {
            standUp.gameObject.SetActive(true);
            if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetForcedTarget(standUp);
        }
        Seated?.Invoke();
    }

    // ---------------------------------------------------------------- Berdiri

    /// Tombol "Berdiri". Bisa juga dipanggil UI lain (mis. tombol tutup editor rambut).
    public void Stand() {
        if (_phase != Phase.Seated || !_player || !_occupant.BeginStand()) return;
        _phase = Phase.Leaving;
        HideStandButton();
        StandingUp?.Invoke();
        StartCoroutine(StandRoutine());
    }

    private IEnumerator StandRoutine() {
        while (_player && _occupant.IsBusy) yield return null;
        for (int i = path.Length - 1; i >= 0; i--) {
            if (path[i]) yield return WalkTo(path[i].position, i == 0 ? ArriveDistance : WaypointRadius);
        }
        Finish();
    }

    // ---------------------------------------------------------------- Gerak skrip (di lantai, CharacterController hidup)

    private IEnumerator WalkTo(Vector3 goal, float radius) {
        float deadline = Time.time + LegTimeout;
        while (_player && Time.time < deadline) {
            Vector3 offset = goal - _player.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= radius) break;
            float speed = radius > ArriveDistance ? 1f : Mathf.Clamp(distance / SlowDownDistance, MinScriptedSpeed, 1f);
            _player.SetScriptedMove(offset / distance * speed);
            yield return null;
        }
        if (_player) _player.SetScriptedMove(null);
    }

    // ---------------------------------------------------------------- Selesai / batal

    private void Finish() {
        if (_player) _player.SetScriptedMove(null);
        HideStandButton();
        SetIgnoreChair(false);
        SetControlsHidden(false);
        SetRingHidden(false);
        bool wasActive = _phase != Phase.Idle;
        _phase = Phase.Idle;
        _player = null;
        if (_interactable) {
            _interactable.enabled = true;
            // Pemain berakhir di dalam lingkaran: trigger tidak masuk ulang, jadi daftarkan lagi tombol "Duduk".
            PlayerController local = PlayerController.Local;
            var zone = GetComponent<Collider>();
            if (local && zone && InteractButtonUI.Instance && zone.bounds.Contains(local.transform.position))
                InteractButtonUI.Instance.Register(_interactable);
        }
        if (wasActive) Left?.Invoke();
    }

    /// Interior di-unload / objek dimatikan di tengah urutan: lepas avatar seketika.
    private void Abort() {
        if (_phase == Phase.Idle) return;
        StopAllCoroutines();
        if (_occupant) _occupant.ExitImmediate();
        if (_player) _player.SetScriptedMove(null);
        HideStandButton();
        SetIgnoreChair(false);
        SetControlsHidden(false);
        SetRingHidden(false);
        _phase = Phase.Idle;
        _player = null;
    }

    private void SetRingHidden(bool hidden) {
        foreach (Renderer r in hideWhileBusy)
            if (r) r.enabled = !hidden;
    }

    private void HideStandButton() {
        if (!standUp) return;
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.ClearForcedTarget(standUp);
        standUp.gameObject.SetActive(false);
    }

    private void SetIgnoreChair(bool ignore) {
        if (ignore == _ignoring) return;
        if (ignore) _playerColliders = _player ? _player.GetComponentsInChildren<Collider>(true) : new Collider[0];
        _ignoring = ignore;
        foreach (Collider own in _playerColliders) {
            if (!own) continue;
            foreach (Collider chair in chairColliders)
                if (chair) Physics.IgnoreCollision(own, chair, ignore);
        }
        if (!ignore) _playerColliders = new Collider[0];
    }

    /// Joystick, lompat, dan lari disembunyikan (seperti di mobil); tombol Interact tetap ada untuk Berdiri.
    private void SetControlsHidden(bool hidden) {
        if (hidden == _controlsHidden) return;
        _controlsHidden = hidden;
        MobileControls.SetDriving(hidden);
    }
}
