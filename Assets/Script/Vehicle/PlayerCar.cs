using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mobil yang bisa dinaiki pemain. Pasang di root prefab mobil (sebelah CarDriveController dan
/// CarModelControl dari AllStarCharacterLibrary, yang dipakai sebagai data entry point + animasi pintu).
///
/// Satu kursi per entry point (pintu). Kursi sopir (EntryPoint.driver) bisa menyetir; kursi lain menumpang.
/// Alur: dekati pintu → tombol Interact → VehicleOccupant memutar animasi masuk → duduk (sopir: HUD menyetir) →
/// Interact lagi saat mobil hampir diam → animasi keluar.
///
/// Belum disinkronkan lewat Normcore: mobil dan penumpangnya hanya terlihat benar di client sendiri.
/// </summary>
[RequireComponent(typeof(CarDriveController))]
public class PlayerCar : MonoBehaviour {
    [Serializable]
    public class Seat {
        [Tooltip("Index entryPoints di CarModelControl (pintu kursi ini).")]
        public int entryIndex;
        [Tooltip("Zona trigger di depan pintu. Tombol Interact = masuk ke kursi ini.")]
        public Interactable enterInteractable;
        [NonSerialized] public VehicleOccupant occupant;
    }

    private static readonly int DoorGetInHash = Animator.StringToHash("GetIn");

    [Tooltip("Data entry point + animasi pintu dari asset AllStar.")]
    [SerializeField] private CarModelControl model;
    [SerializeField] private Seat[] seats = Array.Empty<Seat>();
    [Tooltip("Interactable tanpa zona. Dipakai tombol Interact selama duduk di mobil = Keluar.")]
    [SerializeField] private Interactable exitInteractable;

    [Header("Kamera saat di dalam mobil")]
    [SerializeField] private float cameraDistance = 7f;
    [SerializeField] private float cameraHeight = 1.6f;

    private CarDriveController drive;
    private Collider[] solidColliders;

    public CarDriveController Drive => drive;
    public float CameraDistance => cameraDistance;
    public float CameraHeight => cameraHeight;
    public int SeatCount => seats.Length;

    /// <summary>Collider padat mobil (bukan trigger). Diabaikan oleh collider pemain selama di dalam mobil.</summary>
    public IReadOnlyList<Collider> SolidColliders => solidColliders;

    /// <summary>Dipakai editor setup.</summary>
    public void Init(CarModelControl modelControl, Seat[] carSeats, Interactable exit) {
        model = modelControl;
        seats = carSeats ?? Array.Empty<Seat>();
        exitInteractable = exit;
    }

    void Awake() {
        drive = GetComponent<CarDriveController>();
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

    private EntryPoint EntryOf(int seat) {
        if (!model || model.entryPoints == null || seat < 0 || seat >= seats.Length) return null;
        int index = seats[seat].entryIndex;
        return index >= 0 && index < model.entryPoints.Length ? model.entryPoints[index] : null;
    }

    private void OnEnterPressed(int seat) {
        if (seats[seat].occupant || EntryOf(seat) == null) return;
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

    // ---- Dipanggil VehicleOccupant

    internal void OnEntering(VehicleOccupant occupant, int seat) {
        seats[seat].occupant = occupant;
        SetEnterZoneActive(seat, false);
        PlayDoor(seat, enter: true);
    }

    internal void OnSeated(VehicleOccupant occupant, int seat) {
        if (IsDriverSeat(seat)) drive.SetDriver(true);
        if (occupant.IsLocal && InteractButtonUI.Instance && exitInteractable)
            InteractButtonUI.Instance.SetForcedTarget(exitInteractable);
    }

    internal void OnExitStarted(VehicleOccupant occupant, int seat) {
        if (IsDriverSeat(seat)) drive.SetDriver(false);
        if (occupant.IsLocal && InteractButtonUI.Instance && exitInteractable)
            InteractButtonUI.Instance.ClearForcedTarget(exitInteractable);
        PlayDoor(seat, enter: false);
    }

    internal void OnLeft(VehicleOccupant occupant, int seat) {
        if (seat < 0 || seat >= seats.Length || seats[seat].occupant != occupant) return;
        if (IsDriverSeat(seat)) drive.SetDriver(false);
        if (occupant.IsLocal && InteractButtonUI.Instance && exitInteractable)
            InteractButtonUI.Instance.ClearForcedTarget(exitInteractable);
        seats[seat].occupant = null;
        SetEnterZoneActive(seat, true);
    }

    private void SetEnterZoneActive(int seat, bool active) {
        // GameObject dimatikan (bukan cuma komponen) supaya trigger-nya juga mati: pesan OnTriggerEnter
        // tetap dikirim ke MonoBehaviour yang disabled.
        Interactable zone = seats[seat].enterInteractable;
        if (zone) zone.gameObject.SetActive(active);
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
