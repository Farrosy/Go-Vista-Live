using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mobil yang bisa dinaiki pemain. Pasang di root prefab mobil (sebelah CarDriveController, CarSync, dan
/// CarModelControl dari AllStarCharacterLibrary, yang dipakai sebagai data entry point + animasi pintu).
///
/// Satu kursi per entry point (pintu). Kursi sopir (EntryPoint.driver) bisa menyetir; kursi lain menumpang.
/// Alur: dekati pintu → tombol Interact → VehicleOccupant memutar animasi masuk → duduk (sopir: HUD menyetir) →
/// Interact lagi saat mobil hampir diam → animasi keluar.
///
/// Online: siapa duduk di kursi mana dibaca dari CarSync (sama di semua client). Offline (tanpa Realtime): state
/// kursi lokal.
/// </summary>
[RequireComponent(typeof(CarDriveController))]
public class PlayerCar : MonoBehaviour {
    [Serializable]
    public class Seat {
        [Tooltip("Index entryPoints di CarModelControl (pintu kursi ini).")]
        public int entryIndex;
        [Tooltip("Zona trigger di depan pintu. Tombol Interact = masuk ke kursi ini.")]
        public Interactable enterInteractable;
        [NonSerialized] public VehicleOccupant occupant;   // pemain LOKAL di kursi ini (atau semua pemain saat offline)
        [NonSerialized] public bool availableOnline = true;
    }

    private static readonly int DoorGetInHash = Animator.StringToHash("GetIn");
    // Jeda tanpa laporan dari traffic sebelum hitungan "menghalangi jalan" direset (detik).
    private const float TrafficBlockGap = 1.5f;

    [Tooltip("Data entry point + animasi pintu dari asset AllStar.")]
    [SerializeField] private CarModelControl model;
    [SerializeField] private Seat[] seats = Array.Empty<Seat>();
    [Tooltip("Interactable tanpa zona. Dipakai tombol Interact selama duduk di mobil = Keluar.")]
    [SerializeField] private Interactable exitInteractable;

    [Header("Kamera saat di dalam mobil")]
    [SerializeField] private float cameraDistance = 7f;
    [SerializeField] private float cameraHeight = 1.6f;

    private CarDriveController drive;
    private CarSync sync;
    private Collider[] solidColliders;
    private bool hiddenInVenue;
    private float trafficBlockStart = -1f;
    private float trafficBlockLast = -1f;

    public CarDriveController Drive => drive;
    public CarSync Sync => sync;
    public float CameraDistance => cameraDistance;
    public float CameraHeight => cameraHeight;
    public int SeatCount => seats.Length;

    /// <summary>Collider padat mobil (bukan trigger). Diabaikan oleh collider pemain selama di dalam mobil.</summary>
    public IReadOnlyList<Collider> SolidColliders => solidColliders;

    /// <summary>True kalau tidak ada pemain di kursi mana pun (dipakai respawn).</summary>
    public bool IsEmpty {
        get {
            for (int i = 0; i < seats.Length; i++)
                if (!IsSeatFree(i)) return false;
            return true;
        }
    }

    /// <summary>Lama (detik) mobil ini terus-menerus menghalangi traffic NPC.</summary>
    public float BlockingTrafficSeconds =>
        trafficBlockStart >= 0f && Time.time - trafficBlockLast <= TrafficBlockGap ? trafficBlockLast - trafficBlockStart : 0f;

    /// <summary>Dipakai editor setup.</summary>
    public void Init(CarModelControl modelControl, Seat[] carSeats, Interactable exit) {
        model = modelControl;
        seats = carSeats ?? Array.Empty<Seat>();
        exitInteractable = exit;
    }

    void Awake() {
        drive = GetComponent<CarDriveController>();
        sync = GetComponent<CarSync>();
        if (!model) model = GetComponent<CarModelControl>();
        var solids = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger) solids.Add(c);
        solidColliders = solids.ToArray();

        for (int i = 0; i < seats.Length; i++) {
            int seatIndex = i;
            if (EntryOf(i) == null) Debug.LogError($"[PlayerCar] Entry point kursi {i} tidak ditemukan di '{name}'.", this);
            if (seats[i].enterInteractable) seats[i].enterInteractable.onInteract.AddListener(() => OnEnterPressed(seatIndex));
        }
        if (exitInteractable) exitInteractable.onInteract.AddListener(OnExitPressed);
        if (seats.Length > CarSyncModel.MaxSeats)
            Debug.LogWarning($"[PlayerCar] '{name}' punya {seats.Length} kursi; hanya {CarSyncModel.MaxSeats} yang disinkronkan.", this);
    }

    void OnDestroy() {
        foreach (Seat seat in seats)
            if (seat.occupant) seat.occupant.ExitImmediate();
    }

    // ---- Data kursi (dipakai VehicleOccupant)

    public Transform EntryPointOf(int seat) => EntryOf(seat)?.entryPoint;
    public bool IsDriverSeat(int seat) => EntryOf(seat)?.driver ?? false;
    public bool IsLeftSide(int seat) => EntryOf(seat)?.leftSide ?? true;
    /// 0 = mobil rendah (animasi Low*), 1 = mobil tinggi (animasi StepVan*). Nilai di antaranya di-blend.
    public float SeatHeightOf(int seat) => EntryOf(seat)?.seatHeight ?? 0f;

    public bool IsSeatFree(int seat) {
        if (seat < 0 || seat >= seats.Length || seats[seat].occupant) return false;
        return !sync || sync.IsSeatFree(seat);
    }

    private EntryPoint EntryOf(int seat) {
        if (!model || model.entryPoints == null || seat < 0 || seat >= seats.Length) return null;
        int index = seats[seat].entryIndex;
        return index >= 0 && index < model.entryPoints.Length ? model.entryPoints[index] : null;
    }

    private void OnEnterPressed(int seat) {
        if (!IsSeatFree(seat) || EntryOf(seat) == null || hiddenInVenue) return;
        PlayerController local = PlayerController.Local;
        if (!local || local.IsKnockedDown || local.IsFrozen || local.InVehicle) return;
        VehicleOccupant occupant = local.GetComponent<VehicleOccupant>();
        if (!occupant) occupant = local.gameObject.AddComponent<VehicleOccupant>();
        occupant.Enter(this, seat);
    }

    private void OnExitPressed() {
        PlayerController local = PlayerController.Local;
        VehicleOccupant occupant = local ? local.GetComponent<VehicleOccupant>() : null;
        if (occupant && occupant.Car == this) occupant.RequestExit();
    }

    // ---- Dipanggil CarSync

    /// <summary>Kursi ditempati pemain lain (online): zona masuknya dimatikan di client ini.</summary>
    internal void SetEnterZoneAvailable(int seat, bool available) {
        if (seat < 0 || seat >= seats.Length || seats[seat].availableOnline == available) return;
        seats[seat].availableOnline = available;
        RefreshEnterZone(seat);
    }

    internal void SetHiddenInVenue(bool hidden) {
        hiddenInVenue = hidden;
        for (int i = 0; i < seats.Length; i++) RefreshEnterZone(i);
    }

    // ---- Dipanggil TrafficCar (hanya di client otoritas traffic)

    /// <summary>Traffic NPC sedang berhenti karena mobil ini.</summary>
    public void ReportBlockingTraffic() {
        if (trafficBlockStart < 0f || Time.time - trafficBlockLast > TrafficBlockGap) trafficBlockStart = Time.time;
        trafficBlockLast = Time.time;
    }

    public void ClearTrafficBlock() {
        trafficBlockStart = -1f;
        trafficBlockLast = -1f;
    }

    // ---- Dipanggil VehicleOccupant (pemain lokal maupun avatar pemain lain)

    internal void OnEntering(VehicleOccupant occupant, int seat) {
        if (occupant.IsLocal) {
            seats[seat].occupant = occupant;
            RefreshEnterZone(seat);
            if (sync) sync.WriteLocalSeat(seat, VehicleSeatPhase.Entering);
        }
        PlayDoor(seat, enter: true);
    }

    internal void OnSeated(VehicleOccupant occupant, int seat, Vector3 localPosition, float localYaw) {
        if (!occupant.IsLocal) return;
        if (sync) {
            sync.WriteLocalSeatPose(seat, localPosition, localYaw);
            sync.WriteLocalSeat(seat, VehicleSeatPhase.Seated);
        }
        if (IsDriverSeat(seat)) {
            drive.SetDriver(true);
            if (sync) sync.BeginLocalSimulation();
        }
        if (InteractButtonUI.Instance && exitInteractable) InteractButtonUI.Instance.SetForcedTarget(exitInteractable);
    }

    internal void OnExitStarted(VehicleOccupant occupant, int seat) {
        if (occupant.IsLocal) {
            if (sync) sync.WriteLocalSeat(seat, VehicleSeatPhase.Exiting);
            ReleaseDriver(seat, immediate: false);
            if (InteractButtonUI.Instance && exitInteractable) InteractButtonUI.Instance.ClearForcedTarget(exitInteractable);
        }
        PlayDoor(seat, enter: false);
    }

    internal void OnLeft(VehicleOccupant occupant, int seat, bool immediate) {
        if (!occupant.IsLocal || seat < 0 || seat >= seats.Length || seats[seat].occupant != occupant) return;
        ReleaseDriver(seat, immediate);
        if (sync) sync.WriteLocalSeat(seat, VehicleSeatPhase.Empty);
        if (InteractButtonUI.Instance && exitInteractable) InteractButtonUI.Instance.ClearForcedTarget(exitInteractable);
        seats[seat].occupant = null;
        RefreshEnterZone(seat);
    }

    private void ReleaseDriver(int seat, bool immediate) {
        if (!IsDriverSeat(seat)) return;
        drive.SetDriver(false);
        if (sync) sync.EndLocalSimulation(immediate);
    }

    private void RefreshEnterZone(int seat) {
        // GameObject dimatikan (bukan cuma komponen) supaya trigger-nya juga mati: pesan OnTriggerEnter
        // tetap dikirim ke MonoBehaviour yang disabled.
        Interactable zone = seats[seat].enterInteractable;
        bool active = !hiddenInVenue && !seats[seat].occupant && seats[seat].availableOnline;
        if (zone && zone.gameObject.activeSelf != active) zone.gameObject.SetActive(active);
    }

    /// <summary>
    /// Animasi pintu. Sebagian mobil memakai Animator di pintu (mis. Stepvan, parameter GetIn), sisanya kurva
    /// engsel di CarModelControl. Durasinya pas dengan animasi masuk/keluar.
    /// </summary>
    private void PlayDoor(int seat, bool enter) {
        EntryPoint entry = EntryOf(seat);
        if (entry == null || !entry.hasDoor) return;
        if (entry.animator) {
            entry.animator.SetBool(DoorGetInHash, enter);
            return;
        }
        entry.enter = enter;
        entry.exit = !enter;
        entry.startTime = Time.time;
    }
}
