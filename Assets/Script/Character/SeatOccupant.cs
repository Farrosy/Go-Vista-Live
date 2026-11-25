using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Duduk di kursi (mis. kursi barber). Dipasang otomatis di root avatar LOKAL oleh <see cref="ChairSeat"/>.
///
/// Urutan duduk, meniru cara orang naik kursi barber (dudukan tinggi + pijakan kaki, meja cermin di depan):
///  1. StepIn: dari titik masuk di lantai (samping pijakan) melangkah ke samping naik ke pijakan kaki, di celah
///     antara dudukan dan meja cermin.
///  2. Turn: berbalik membelakangi kursi (menghadap cermin).
///  3. SittingDown: klip duduk mundur (MoCapCentral Stand_Trans_SitChair_F01b), root motion supaya kaki tidak
///     meluncur, root naik sedikit ke tinggi dudukan.
///  4. ScootBack: bergeser mundur ke posisi duduk akhir (dekat sandaran) dengan kaki tetap di pijakan.
/// Berdiri = kebalikannya (ScootForward, StandingUp, Turn, StepOut).
///
/// Selama urutan ini CharacterController mati dan root digerakkan skrip ini. Kaki ditempelkan ke pijakan oleh
/// <see cref="SeatIK"/>. Pemain lain melihat posisi lewat RealtimeTransform, langkah lewat Speed (PlayerAnimSync)
/// dan animasi duduk lewat bool <see cref="SeatedParam"/>, jadi skrip ini hanya bekerja di avatar sendiri.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]   // LateUpdate setelah Animator dan komponen lain menulis posisi
[RequireComponent(typeof(PlayerController))]
public sealed class SeatOccupant : MonoBehaviour {
    public const string SeatedParam = "ChairSeated";
    public const string SitDownState = "ChairSitDown";
    public const string SitIdleState = "ChairSitIdle";
    public const string StandUpState = "ChairStandUp";
    /// Tag state transisi (duduk/berdiri) dan state duduk diam.
    public const string TransitionTag = "Chair";
    public const string SeatedTag = "ChairSeated";

    /// Jarak mundur root di klip duduk (Stand_Trans_SitChair_F01b) dan maju di klip berdiri, satuan skala humanoid.
    /// Kalikan dengan Animator.humanScale untuk meter.
    public const float SitTravel = 0.41f;

    private const float StepSpeed = 0.9f;        // m/s saat naik/turun pijakan
    private const float StepTurnSpeed = 540f;    // derajat/detik, menghadap arah langkah
    private const float TurnSpeed = 300f;        // derajat/detik, berbalik di atas pijakan
    private const float ScootSeconds = 0.6f;
    private const float PhaseTimeout = 4f;
    // Bagian klip (normalized time) saat pinggul turun ke dudukan / naik lagi: root diangkat/diturunkan di sini.
    private const float LiftStart = 0.35f;
    private const float LiftEnd = 0.8f;
    private const float DropStart = 0.05f;
    private const float DropEnd = 0.55f;

    private static readonly int SeatedHash = Animator.StringToHash(SeatedParam);
    private static readonly int SitDownHash = Animator.StringToHash(SitDownState);
    private static readonly int SitIdleHash = Animator.StringToHash(SitIdleState);
    private static readonly int StandUpHash = Animator.StringToHash(StandUpState);
    private static readonly Dictionary<RuntimeAnimatorController, bool> ParamCache =
        new Dictionary<RuntimeAnimatorController, bool>();

    private enum Phase { None, StepIn, TurnToSit, SittingDown, ScootBack, Seated, ScootForward, StandingUp, TurnToLeave, StepOut }

    private PlayerController player;
    private CharacterController characterController;
    private Animator animator;
    private SeatRootMotionRelay relay;
    private bool rootMotionWasOn;
    private bool controllerWasEnabled;

    private Phase phase;
    private float phaseTime;
    private ChairSeat chair;
    private Vector3 entry;        // lantai di samping pijakan (root, termasuk skin width)
    private Vector3 standSpot;    // di atas pijakan, tempat duduk dimulai / berdiri berakhir
    private Vector3 landing;      // posisi root tepat setelah klip duduk (sebelum bergeser mundur)
    private Vector3 seat;         // posisi root duduk akhir
    private Quaternion facing;    // menghadap cermin (arah hadap kursi)
    private Vector3 from;         // posisi root di awal fase
    private Vector3 to;           // posisi root di akhir fase
    private Vector3 rootMotion;   // akumulasi root motion horizontal sejak awal fase

    /// True dari mulai naik pijakan sampai turun lagi: gerak, lompat, dan lari mati.
    public bool IsBusy => phase != Phase.None;
    /// True saat sudah duduk diam (tombol Berdiri bisa dipakai).
    public bool IsSeated => phase == Phase.Seated;
    /// Nilai bool Animator yang dikirim ke pemain lain (PlayerAnimSync).
    public bool WantsSeated => phase == Phase.SittingDown || phase == Phase.ScootBack || phase == Phase.Seated ||
                               phase == Phase.ScootForward;
    private float SitTravelMeters => SitTravel * (animator && animator.humanScale > 0f ? animator.humanScale : 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ParamCache.Clear();

    void Awake() {
        player = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        animator = player.Animator;
    }

    void OnDisable() => ExitImmediate();

    // ---------------------------------------------------------------- API (dipanggil ChairSeat)

    /// <summary>Mulai naik ke kursi. Avatar sebaiknya sudah berdiri di <see cref="ChairSeat.Entry"/>.</summary>
    public bool BeginSit(ChairSeat target) {
        if (IsBusy || !target || !animator || !HasSeatedParam(animator)) return false;
        chair = target;
        float skin = characterController.skinWidth * transform.lossyScale.y;
        facing = YawOnly(target.Seat.rotation);
        Vector3 forward = facing * Vector3.forward;
        entry = target.Entry.position + Vector3.up * skin;
        standSpot = target.StandSpot.position + Vector3.up * skin;
        seat = target.Seat.position;
        landing = standSpot - forward * SitTravelMeters;
        landing.y = seat.y;
        Attach();
        StartPhase(Phase.StepIn, transform.position, standSpot);
        return true;
    }

    /// <summary>Berdiri dari kursi; berakhir di titik masuk (lantai), lalu kontrol kembali.</summary>
    public bool BeginStand() {
        if (phase != Phase.Seated) return false;
        StartPhase(Phase.ScootForward, seat, landing);
        return true;
    }

    /// <summary>Lepas seketika tanpa animasi (interior di-unload, teleport). Root ditaruh di titik masuk.</summary>
    public void ExitImmediate() {
        if (!IsBusy) return;
        if (animator && animator.isActiveAndEnabled && HasSeatedParam(animator)) {
            animator.SetBool(SeatedHash, false);
            animator.Play("Locomotion", 0, 0f);
        }
        transform.position = entry;
        Detach();
    }

    /// <summary>Dipakai PlayerAnimSync untuk avatar pemain lain (dan aman untuk controller tanpa parameter ini).</summary>
    public static void ApplyRemote(Animator anim, bool seated, bool snap) {
        if (!anim || !anim.isActiveAndEnabled || !HasSeatedParam(anim)) return;
        anim.SetBool(SeatedHash, seated);
        if (seated) SeatIK.Ensure(anim);
        if (snap && seated) anim.Play(SitIdleHash, 0, 0f);   // masuk room saat pemain ini sudah duduk
    }

    public static bool HasSeatedParam(Animator anim) {
        RuntimeAnimatorController controller = anim ? anim.runtimeAnimatorController : null;
        if (!controller) return false;
        if (ParamCache.TryGetValue(controller, out bool has)) return has;
        has = false;
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.nameHash == SeatedHash && p.type == AnimatorControllerParameterType.Bool) has = true;
        if (!has) Debug.LogWarning($"[SeatOccupant] Animator '{controller.name}' belum punya parameter {SeatedParam}. " +
                                   "Jalankan Go Vista > Interior > Pasang Kursi Barber.");
        ParamCache[controller] = has;
        return has;
    }

    /// Dipakai SeatIK: kursi yang sedang diduduki avatar ini.
    public ChairSeat Chair => IsBusy ? chair : null;

    // ---------------------------------------------------------------- Frame

    /// Dipanggil SeatRootMotionRelay tiap frame animasi.
    public void OnRootMotion(Vector3 deltaPosition) {
        if (phase != Phase.SittingDown && phase != Phase.StandingUp) return;
        deltaPosition.y = 0f;
        rootMotion += deltaPosition;
    }

    void Update() {
        if (!IsBusy) return;
        phaseTime += Time.deltaTime;
        bool timedOut = phaseTime > PhaseTimeout;
        switch (phase) {
            case Phase.StepIn:
                if (ReachedFlat(to) || timedOut) StartPhase(Phase.TurnToSit, to, to);
                break;
            case Phase.TurnToSit:
                if (Quaternion.Angle(transform.rotation, facing) < 1f || timedOut) {
                    StartPhase(Phase.SittingDown, standSpot, landing);
                    animator.SetBool(SeatedHash, true);
                }
                break;
            case Phase.SittingDown:
                if (IsSettledIn(SitIdleHash) || timedOut) StartPhase(Phase.ScootBack, landing, seat);
                break;
            case Phase.ScootBack:
                if (phaseTime >= ScootSeconds) StartPhase(Phase.Seated, seat, seat);
                break;
            case Phase.ScootForward:
                if (phaseTime >= ScootSeconds) {
                    StartPhase(Phase.StandingUp, landing, standSpot);
                    animator.SetBool(SeatedHash, false);
                }
                break;
            case Phase.StandingUp:
                if (HasLeftChairStates() || timedOut) StartPhase(Phase.TurnToLeave, standSpot, standSpot);
                break;
            case Phase.TurnToLeave:
                if (Quaternion.Angle(transform.rotation, LookFlat(entry - standSpot)) < 1f || timedOut)
                    StartPhase(Phase.StepOut, standSpot, entry);
                break;
            case Phase.StepOut:
                if (ReachedFlat(to) || timedOut) FinishStand();
                break;
        }
    }

    void LateUpdate() {
        if (!IsBusy) return;
        Vector3 position = to;
        Quaternion rotation = facing;
        float stepSpeed = 0f;
        switch (phase) {
            case Phase.StepIn:
            case Phase.StepOut:
                position = Step(out rotation);
                stepSpeed = StepSpeed;
                break;
            case Phase.TurnToSit:
                rotation = Quaternion.RotateTowards(transform.rotation, facing, TurnSpeed * Time.deltaTime);
                break;
            case Phase.TurnToLeave:
                rotation = Quaternion.RotateTowards(transform.rotation, LookFlat(entry - standSpot), TurnSpeed * Time.deltaTime);
                break;
            case Phase.SittingDown: {
                float t = Progress(SitDownHash, SitIdleHash);
                position = Blend(t, Smooth(t, LiftStart, LiftEnd));
                break;
            }
            case Phase.StandingUp: {
                float t = Progress(StandUpHash, 0);
                position = Blend(t, Smooth(t, DropStart, DropEnd));
                break;
            }
            case Phase.ScootBack:
            case Phase.ScootForward:
                position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, phaseTime / ScootSeconds));
                break;
        }
        player.SetSeatedSpeed(stepSpeed);
        transform.SetPositionAndRotation(position, rotation);
    }

    /// Langkah datar ke <see cref="to"/> dengan kecepatan jalan; tinggi naik/turun saat kaki melewati tepi pijakan.
    private Vector3 Step(out Quaternion rotation) {
        Vector3 current = transform.position;
        Vector3 offset = to - current;
        offset.y = 0f;
        Vector3 next = current + Vector3.ClampMagnitude(offset, StepSpeed * Time.deltaTime);
        // Tinggi mengikuti jarak ke tepi pijakan: di luar tepi = lantai, di dalam = atas pijakan.
        float edge = chair ? chair.FootrestHalfWidth : 0.3f;
        float lateral = Vector3.Distance(Flat(next), Flat(standSpot));
        float onFootrest = 1f - Smooth(lateral, edge - 0.1f, edge + 0.1f);
        next.y = Mathf.Lerp(entry.y, standSpot.y, onFootrest);
        rotation = offset.sqrMagnitude > 0.0001f
            ? Quaternion.RotateTowards(transform.rotation, LookFlat(offset), StepTurnSpeed * Time.deltaTime)
            : transform.rotation;
        return next;
    }

    /// Root motion (kaki tetap menapak) yang perlahan dikoreksi ke titik akhir, plus tinggi dari kurva angkat.
    private Vector3 Blend(float t, float height) {
        Vector3 xz = Vector3.Lerp(Flat(from + rootMotion), Flat(to), t * t);
        return new Vector3(xz.x, Mathf.Lerp(from.y, to.y, height), xz.z);
    }

    // ---------------------------------------------------------------- Bantu

    private void StartPhase(Phase value, Vector3 start, Vector3 end) {
        phase = value;
        phaseTime = 0f;
        from = start;
        to = end;
        rootMotion = Vector3.zero;
    }

    private void Attach() {
        player.SetSeated(true);
        controllerWasEnabled = characterController.enabled;
        characterController.enabled = false;
        if (!relay) relay = animator.GetComponent<SeatRootMotionRelay>();
        if (!relay) relay = animator.gameObject.AddComponent<SeatRootMotionRelay>();
        relay.Init(this);
        SeatIK.Ensure(animator);
        rootMotionWasOn = animator.applyRootMotion;
        animator.applyRootMotion = true;
    }

    private void FinishStand() {
        transform.SetPositionAndRotation(entry, transform.rotation);
        Detach();
    }

    /// Kembalikan semua yang diubah Attach(). Tidak memindahkan avatar.
    private void Detach() {
        phase = Phase.None;
        chair = null;
        if (animator) animator.applyRootMotion = rootMotionWasOn;
        Physics.SyncTransforms();
        characterController.enabled = controllerWasEnabled;
        player.SetSeatedSpeed(0f);
        player.SetSeated(false);
    }

    /// 0..1 sepanjang state <paramref name="state"/>; 1 kalau sudah lewat ke <paramref name="next"/>.
    private float Progress(int state, int next) {
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == state) {
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash != state) return 1f;
            return Mathf.Clamp01(current.normalizedTime);
        }
        if (next != 0 && current.shortNameHash == next) return 1f;
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == state) return 0f;
        // Animator tidak masuk ke state ini (mis. controller berbeda): ikuti waktu supaya tetap selesai.
        return Mathf.Clamp01(phaseTime / PhaseTimeout);
    }

    private bool ReachedFlat(Vector3 point) => (Flat(transform.position) - Flat(point)).sqrMagnitude < 0.0001f;

    private bool IsSettledIn(int state) =>
        !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state;

    private bool HasLeftChairStates() {
        if (animator.IsInTransition(0)) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return !current.IsTag(TransitionTag) && !current.IsTag(SeatedTag);
    }

    private static float Smooth(float t, float start, float end) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, t));

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static Quaternion LookFlat(Vector3 direction) {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
    }

    private static Quaternion YawOnly(Quaternion rotation) => Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
}
