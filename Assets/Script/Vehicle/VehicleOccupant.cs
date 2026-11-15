using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Status pemain lokal di dalam mobil. Ditambahkan otomatis ke root player oleh PlayerCar saat pertama masuk.
///
/// Selama di dalam mobil:
///  - PlayerController berhenti membaca joystick (SetInVehicle), CharacterController dan NavMeshObstacle dimatikan,
///    collider pemain mengabaikan collider mobil.
///  - Posisi player disimpan relatif ke mobil dan ditempelkan tiap frame (player TIDAK di-parent ke mobil, supaya
///    RealtimeTransform Normcore tidak bermasalah dengan reparenting).
///  - Animasi masuk/keluar (root motion) menggeser posisi relatif itu dari pintu ke kursi. Root motion diskalakan ke
///    ukuran Tommy (karakter asli animasinya) supaya karakter lain tetap mendarat di kursi.
///
/// Animasi (HumanoidPlayerController, sama seperti asset AllStar): pintu kiri = animasi sopir, pintu kanan = animasi
/// penumpang, duduk = Drive (kursi sopir) atau Ride (kursi lain). Tinggi kursi di-blend lewat SeatHeight
/// (0 = mobil rendah, 1 = mobil tinggi seperti van).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class VehicleOccupant : MonoBehaviour {
    private enum Phase { None, Entering, Seated, Exiting }

    /// Tag state Animator: masuk/keluar = "Vehicle", duduk (Drive/Ride) = "VehicleSeated".
    public const string VehicleStateTag = "Vehicle";
    public const string SeatedStateTag = "VehicleSeated";
    public const string InVehicleParam = "InVehicle";
    public const string CarEnterParam = "CarEnter";
    public const string CarLeftParam = "CarLeft";
    public const string CarDriverParam = "CarDriver";
    public const string SeatHeightParam = "SeatHeight";

    private const float ReferenceHumanScale = 1.111f;   // humanScale Tommy (AllStarCharacterLibrary)
    // Batas aman kalau Animator belum punya state mobil / transisinya macet.
    private const float EnterTimeout = 3f;
    private const float ExitTimeout = 3f;
    // Posisi akhir animasi keluar yang lebih jauh dari ini dari titik pintu dianggap meleset: pakai titik pintu.
    private const float MaxExitDrift = 1f;

    private static readonly int InVehicleHash = Animator.StringToHash(InVehicleParam);
    private static readonly int CarEnterHash = Animator.StringToHash(CarEnterParam);
    private static readonly int CarLeftHash = Animator.StringToHash(CarLeftParam);
    private static readonly int CarDriverHash = Animator.StringToHash(CarDriverParam);
    private static readonly int SeatHeightHash = Animator.StringToHash(SeatHeightParam);

    private PlayerController player;
    private CharacterController characterController;
    private NavMeshObstacle obstacle;
    private bool obstacleWasEnabled;
    private Animator animator;
    private bool rootMotionWasOn;
    private Collider[] ownColliders;
    private VehicleRootMotionRelay relay;

    private PlayerCar car;
    private int seat = -1;
    private Phase phase;
    private float phaseTime;
    private Vector3 localPosition;
    private Quaternion localRotation = Quaternion.identity;
    private float rootMotionScale = 1f;
    private bool exitBlocked;

    public bool InVehicle => phase != Phase.None;
    /// True selama duduk di kursi sopir (mobil bisa disetir).
    public bool IsDriving => phase == Phase.Seated && car && car.IsDriverSeat(seat);
    public bool IsLocal => player && player.IsLocal;
    public PlayerCar Car => car;
    public int Seat => seat;

    void Awake() {
        player = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        obstacle = GetComponent<NavMeshObstacle>();
        animator = player.Animator;
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    void OnDisable() {
        if (InVehicle) ExitImmediate();
    }

    /// <summary>Mulai animasi masuk ke kursi seatIndex.</summary>
    public bool Enter(PlayerCar target, int seatIndex) {
        if (InVehicle || !target || !animator) return false;
        Transform entry = target.EntryPointOf(seatIndex);
        if (!entry) return false;

        car = target;
        seat = seatIndex;
        SetPhase(Phase.Entering);
        player.SetInVehicle(true);
        characterController.enabled = false;
        if (obstacle) {
            obstacleWasEnabled = obstacle.enabled;
            obstacle.enabled = false;
        }
        SetIgnoreCarCollisions(true);

        localPosition = car.transform.InverseTransformPoint(entry.position);
        localRotation = Quaternion.Inverse(car.transform.rotation) * entry.rotation;
        ApplyPose();

        rootMotionScale = animator.humanScale > 0f ? ReferenceHumanScale / animator.humanScale : 1f;
        if (!relay) relay = animator.GetComponent<VehicleRootMotionRelay>();
        if (!relay) relay = animator.gameObject.AddComponent<VehicleRootMotionRelay>();
        relay.Init(this);
        rootMotionWasOn = animator.applyRootMotion;
        animator.applyRootMotion = true;
        animator.SetBool(CarLeftHash, car.IsLeftSide(seat));
        animator.SetBool(CarDriverHash, car.IsDriverSeat(seat));
        animator.SetFloat(SeatHeightHash, car.SeatHeightOf(seat));
        animator.SetBool(InVehicleHash, true);
        animator.SetTrigger(CarEnterHash);

        MobileControls.SetDriving(true);
        car.OnEntering(this, seat);
        return true;
    }

    /// <summary>Tombol Keluar. Ditolak kalau mobil masih melaju.</summary>
    public bool RequestExit() {
        if (phase != Phase.Seated || !car || !DriveRules.CanExit(car.Drive.ForwardSpeed)) return false;
        SetPhase(Phase.Exiting);
        SetExitBlocked(false);
        VehicleDriveHud.Hide();
        ReleaseCamera();
        car.OnExitStarted(this, seat);
        animator.SetBool(InVehicleHash, false);
        return true;
    }

    /// <summary>Keluar seketika tanpa animasi (teleport, respawn, mobil dihapus). Posisi akhir: titik pintu.</summary>
    public void ExitImmediate() {
        if (!InVehicle) return;
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Transform entry = car ? car.EntryPointOf(seat) : null;
        if (entry) {
            position = entry.position;
            rotation = YawOnly(entry.rotation);
        }
        if (animator) {
            animator.SetBool(InVehicleHash, false);
            animator.ResetTrigger(CarEnterHash);
            animator.Play("Locomotion", 0, 0f);
        }
        Restore();
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }

    /// Dipanggil VehicleRootMotionRelay tiap frame animasi.
    public void OnRootMotion(Vector3 deltaPosition, Quaternion deltaRotation) {
        if (!car || (phase != Phase.Entering && phase != Phase.Exiting)) return;
        localPosition += Quaternion.Inverse(car.transform.rotation) * (deltaPosition * rootMotionScale);
        localRotation *= deltaRotation;
    }

    void Update() {
        if (!InVehicle) return;
        if (!car) {
            ExitImmediate();
            return;
        }
        phaseTime += Time.deltaTime;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool transitioning = animator.IsInTransition(0);
        switch (phase) {
            case Phase.Entering:
                if ((state.IsTag(SeatedStateTag) && !transitioning) || phaseTime > EnterTimeout) BeginSeated();
                break;
            case Phase.Seated:
                SetExitBlocked(!DriveRules.CanExit(car.Drive.ForwardSpeed));
                break;
            case Phase.Exiting:
                bool stillInCar = transitioning || state.IsTag(VehicleStateTag) || state.IsTag(SeatedStateTag);
                if (!stillInCar || phaseTime > ExitTimeout) FinishExit();
                break;
        }
    }

    void LateUpdate() {
        // Setelah Animator dan interpolasi Rigidbody mobil, supaya pemain tidak bergetar di kursi.
        if (InVehicle && car) ApplyPose();
    }

    private void BeginSeated() {
        SetPhase(Phase.Seated);
        car.OnSeated(this, seat);
        if (car.IsDriverSeat(seat)) VehicleDriveHud.Show(car.Drive);
        CameraOrbit orbit = FindCamera();
        if (orbit) orbit.SetVehicleView(car.transform, car.CameraDistance, car.CameraHeight);
    }

    private void FinishExit() {
        Transform entry = car.EntryPointOf(seat);
        Vector3 position = transform.position;
        if (entry) {
            Vector3 drift = position - entry.position;
            drift.y = 0f;
            if (drift.magnitude > MaxExitDrift) position = entry.position;
        }
        Quaternion rotation = YawOnly(transform.rotation);
        Restore();
        player.TeleportTo(position, rotation);   // CharacterController hidup lagi + menapak tanah
    }

    /// Kembalikan semua yang diubah Enter(). Tidak memindahkan player.
    private void Restore() {
        PlayerCar leftCar = car;
        int leftSeat = seat;
        SetPhase(Phase.None);
        SetExitBlocked(false);
        VehicleDriveHud.Hide();
        ReleaseCamera();
        if (animator) animator.applyRootMotion = rootMotionWasOn;
        SetIgnoreCarCollisions(false, leftCar);
        if (obstacle) obstacle.enabled = obstacleWasEnabled;
        characterController.enabled = true;
        player.SetInVehicle(false);
        MobileControls.SetDriving(false);
        car = null;
        seat = -1;
        if (leftCar) leftCar.OnLeft(this, leftSeat);
    }

    private void ApplyPose() {
        Transform t = car.transform;
        transform.SetPositionAndRotation(t.TransformPoint(localPosition), t.rotation * localRotation);
    }

    private void SetPhase(Phase value) {
        phase = value;
        phaseTime = 0f;
    }

    private void SetExitBlocked(bool blocked) {
        if (exitBlocked == blocked) return;
        exitBlocked = blocked;
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetDisabled(blocked);
    }

    private void SetIgnoreCarCollisions(bool ignore, PlayerCar target = null) {
        target = target ? target : car;
        if (!target || target.SolidColliders == null) return;
        foreach (Collider own in ownColliders) {
            if (!own) continue;
            foreach (Collider other in target.SolidColliders)
                if (other) Physics.IgnoreCollision(own, other, ignore);
        }
    }

    private void ReleaseCamera() {
        CameraOrbit orbit = FindCamera();
        if (orbit) orbit.ClearVehicleView();
    }

    private static CameraOrbit FindCamera() {
        Camera main = Camera.main;
        return main ? main.GetComponent<CameraOrbit>() : null;
    }

    private static Quaternion YawOnly(Quaternion rotation) =>
        Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
}
