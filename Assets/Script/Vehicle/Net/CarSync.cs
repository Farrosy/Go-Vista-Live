using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sinkronisasi mobil pemain lewat Normcore. Pasang di root prefab mobil (sebelah RealtimeView, PlayerCar,
/// CarDriveController). Mobil di-spawn VehicleDirector lewat Realtime.Instantiate.
///
/// Fisika: hanya client SIMULATOR (sopir) yang menjalankan fisika dan mengirim pose. Client lain membuat
/// Rigidbody kinematic dan menginterpolasi pose itu. Tanpa sopir, mobil parkir: semua client menaruhnya tepat di
/// pose parkir (kinematic), jadi posisinya selalu sama di semua layar.
///
/// Kursi: siapa duduk di mana dan fasenya (masuk/duduk/keluar). Avatar pemain lain diberi VehicleOccupant yang
/// memutar animasi sama dan menempel ke mobil.
///
/// Mobil disembunyikan (dan dibekukan) di client yang sedang berada di venue lain, karena WorldScene-nya
/// di-unload di client itu.
///
/// Tanpa Realtime (mode offline/DevScene) komponen ini diam dan PlayerCar memakai state kursi lokal.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCar))]
public class CarSync : RealtimeComponent<CarSyncModel> {
    // Interpolasi pose mobil pemain lain: tampilkan pose yang tertinggal sebanyak ini (detik) supaya selalu ada
    // dua snapshot untuk diinterpolasi.
    private const float InterpolationDelay = 0.12f;
    // Kalau snapshot terlambat, pose diekstrapolasi dari kecepatan paling lama segini (detik).
    private const float MaxExtrapolation = 0.25f;
    // Jeda antar kirim pose oleh simulator (detik). Normcore mengirim ±20x/detik.
    private const float SendInterval = 0.05f;
    // Setelah sopir keluar, simulator menunggu mobil berhenti (atau batas waktu) sebelum memarkirkan.
    private const float ParkSpeed = 0.15f;
    private const float ParkTimeout = 3f;
    // Pemain yang tercatat di kursi / sebagai simulator tapi avatarnya hilang (keluar room) lebih lama dari ini
    // dianggap sudah pergi: kursinya dikosongkan, mobil diparkir.
    private const float MissingOwnerGrace = 3f;
    // Mobil kosong yang menghalangi traffic NPC lebih lama dari ini dikembalikan ke titik spawn.
    private const float RespawnAfterBlocking = 30f;

    private static readonly List<CarSync> all = new List<CarSync>();
    /// <summary>Semua mobil yang ada di room (dipakai VehicleDirector).</summary>
    public static IReadOnlyList<CarSync> All => all;

    private struct Snapshot {
        public double time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
    }

    private PlayerCar car;
    private CarDriveController drive;
    private Rigidbody body;
    private Renderer[] renderers;
    private bool[] rendererWasEnabled;
    private Collider[] colliders;
    private bool[] colliderWasEnabled;
    private bool visible = true;
    private RigidbodyInterpolation simulatedInterpolation;

    private readonly Snapshot[] snapshots = new Snapshot[8];
    private int snapshotCount;
    private double lastStamp = double.NegativeInfinity;
    private float nextSend;
    private int appliedParkedVersion = -1;
    private bool pendingPark;
    private float parkTimer;
    private bool pendingSpawnInit;
    private Vector3 pendingSpawnPosition;
    private Quaternion pendingSpawnRotation;
    private int pendingSpawnOwner;
    private float simulatorMissingSince = -1f;
    private readonly float[] seatMissingSince = { -1f, -1f, -1f, -1f };
    private readonly VehicleOccupant[] remoteOccupants = new VehicleOccupant[CarSyncModel.MaxSeats];

    /// <summary>True kalau terhubung ke room dan model sudah ada (state kursi dibaca dari jaringan).</summary>
    public bool IsOnline => model != null && realtime != null && realtime.connected;
    public int LocalClientId => realtime != null ? realtime.clientID : -1;
    /// <summary>True kalau client ini yang menjalankan fisika mobil.</summary>
    public bool IsLocalSimulator => IsOnline && model.simulator == LocalClientId + 1;

    void Awake() {
        // Salinan mobil dari client lain tercipta di scene aktif; tinggal di scene persisten (lihat VehicleDirector).
        PersistentSceneAdopter.Adopt(gameObject);
        car = GetComponent<PlayerCar>();
        drive = GetComponent<CarDriveController>();
        body = GetComponent<Rigidbody>();
        simulatedInterpolation = body.interpolation;
        // Hanya collider padat (bukan trigger zona kursi, bukan WheelCollider) yang dimatikan saat di venue lain.
        var solids = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger && !(c is WheelCollider)) solids.Add(c);
        colliders = solids.ToArray();
        colliderWasEnabled = new bool[colliders.Length];
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererWasEnabled = new bool[renderers.Length];
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start() {
        // Remote client membuat object hasil Realtime.Instantiate di active scene, yang bisa saja WorldScene
        // (di-unload saat pindah venue). Pindahkan ke scene Realtime (MainMultiplayerScene) yang tidak pernah di-unload.
        if (realtime && gameObject.scene != realtime.gameObject.scene && transform.parent == null)
            SceneManager.MoveGameObjectToScene(gameObject, realtime.gameObject.scene);
    }

    protected override void OnRealtimeModelReplaced(CarSyncModel previousModel, CarSyncModel currentModel) {
        appliedParkedVersion = -1;
        snapshotCount = 0;
        lastStamp = double.NegativeInfinity;
        if (currentModel != null && pendingSpawnInit) {
            pendingSpawnInit = false;
            WriteSpawn(pendingSpawnPosition, pendingSpawnRotation, pendingSpawnOwner);
        }
    }

    /// <summary>Pemilik mobil (clientID + 1), 0 = tanpa pemilik.</summary>
    public int OwnerClientPlusOne => model != null ? model.ownerClient : pendingSpawnOwner;

    /// <summary>True kalau tidak ada yang duduk, masuk/keluar, atau menyetir (aman dihapus).</summary>
    public bool IsUnoccupied {
        get {
            if (!car.IsEmpty) return false;
            if (model == null) return true;
            if (model.simulator != 0) return false;
            for (int seat = 0; seat < CarSyncModel.MaxSeats; seat++)
                if (model.GetSeatClient(seat) != 0) return false;
            return true;
        }
    }

    /// <summary>Dipanggil VehicleDirector tepat setelah spawn: simpan pemilik dan titik spawn, lalu parkir di sana.</summary>
    public void InitSpawn(Vector3 position, Quaternion rotation, int ownerClientPlusOne) {
        transform.SetPositionAndRotation(position, rotation);
        if (model == null) {
            pendingSpawnInit = true;
            pendingSpawnPosition = position;
            pendingSpawnRotation = rotation;
            pendingSpawnOwner = ownerClientPlusOne;
            return;
        }
        WriteSpawn(position, rotation, ownerClientPlusOne);
    }

    private void WriteSpawn(Vector3 position, Quaternion rotation, int ownerClientPlusOne) {
        model.ownerClient = ownerClientPlusOne;
        model.spawnPosition = position;
        model.spawnRotation = rotation;
        WriteParked(position, rotation);
    }

    private void WriteParked(Vector3 position, Quaternion rotation) {
        model.parkedPosition = position;
        model.parkedRotation = rotation;
        model.parkedVersion++;
    }

    // ------------------------------------------------------------------ Kursi (dipakai PlayerCar / VehicleOccupant)

    public bool IsSeatFree(int seat) => !IsOnline || model.GetSeatClient(seat) == 0;

    /// <summary>True kalau pemain lokal sedang masuk/duduk/keluar di salah satu kursi mobil ini.</summary>
    public bool HasLocalOccupant {
        get {
            if (!IsOnline) return !car.IsEmpty;
            int me = LocalClientId + 1;
            for (int seat = 0; seat < CarSyncModel.MaxSeats; seat++)
                if (model.GetSeatClient(seat) == me) return true;
            return false;
        }
    }

    /// <summary>Pemain lokal menulis fasenya di kursi ini.</summary>
    public void WriteLocalSeat(int seat, VehicleSeatPhase phase) {
        if (!IsOnline || seat < 0 || seat >= CarSyncModel.MaxSeats) return;
        model.SetSeat(seat, phase == VehicleSeatPhase.Empty ? 0 : LocalClientId + 1, phase);
    }

    public void WriteLocalSeatPose(int seat, Vector3 localPosition, float localYaw) {
        if (!IsOnline || seat < 0 || seat >= CarSyncModel.MaxSeats) return;
        model.SetSeatPose(seat, localPosition, localYaw);
    }

    /// <summary>Sopir lokal mulai menyetir: client ini jadi simulator fisika.</summary>
    public void BeginLocalSimulation() {
        if (!IsOnline) return;
        pendingPark = false;
        model.simulator = LocalClientId + 1;
        snapshotCount = 0;
    }

    /// <summary>Sopir lokal keluar: simulasi diteruskan sampai mobil berhenti, lalu diparkir.</summary>
    public void EndLocalSimulation(bool immediate) {
        if (!IsLocalSimulator) return;
        if (immediate) {
            Park();
            return;
        }
        pendingPark = true;
        parkTimer = 0f;
    }

    // ------------------------------------------------------------------ Loop

    void FixedUpdate() {
        if (!IsOnline) return;
        if (!IsLocalSimulator || !visible) return;

        if (pendingPark) {
            parkTimer += Time.fixedDeltaTime;
            if (body.linearVelocity.magnitude < ParkSpeed || parkTimer > ParkTimeout) {
                Park();
                return;
            }
        }
        if (Time.time >= nextSend) {
            nextSend = Time.time + SendInterval;
            model.position = body.position;
            model.rotation = body.rotation;
            model.velocity = body.linearVelocity;
            model.steer = drive.SteerAngle;
            model.stamp = RoomTime;
        }
    }

    void Update() {
        if (!IsOnline) {
            drive.SetSimulating(true);
            return;
        }

        UpdateVisibility();
        bool localSim = IsLocalSimulator && visible;
        drive.SetSimulating(localSim);
        if (body.isKinematic == localSim) {
            body.isKinematic = !localSim;
            // Mobil yang digerakkan dari jaringan dipindah lewat transform: interpolasi Rigidbody justru membuatnya bergetar.
            body.interpolation = localSim ? simulatedInterpolation : RigidbodyInterpolation.None;
        }

        if (!localSim) {
            if (model.simulator == 0) ApplyParked();
            else InterpolateRemote();
        }

        CheckMissingSimulator();
        UpdateSeats();
        UpdateRespawn();
    }

    private void Park() {
        pendingPark = false;
        WriteParked(body.position, body.rotation);
        appliedParkedVersion = model.parkedVersion;
        model.simulator = 0;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void ApplyParked() {
        if (appliedParkedVersion == model.parkedVersion) return;
        appliedParkedVersion = model.parkedVersion;
        if (model.parkedVersion == 0) return;   // belum pernah ditulis (spawn belum selesai)
        transform.SetPositionAndRotation(model.parkedPosition, model.parkedRotation);
        snapshotCount = 0;
        drive.SetRemoteSteer(0f);
    }

    private void InterpolateRemote() {
        // Ambil snapshot baru kalau stamp berubah.
        if (model.stamp > lastStamp) {
            lastStamp = model.stamp;
            PushSnapshot(new Snapshot {
                time = model.stamp, position = model.position, rotation = model.rotation, velocity = model.velocity,
            });
            drive.SetRemoteSteer(model.steer);
        }
        if (snapshotCount == 0) return;

        double renderTime = RoomTime - InterpolationDelay;
        Snapshot newest = snapshots[snapshotCount - 1];
        if (renderTime >= newest.time) {
            float ahead = Mathf.Min((float)(renderTime - newest.time), MaxExtrapolation);
            transform.SetPositionAndRotation(newest.position + newest.velocity * ahead, newest.rotation);
            return;
        }
        for (int i = snapshotCount - 1; i > 0; i--) {
            Snapshot a = snapshots[i - 1], b = snapshots[i];
            if (renderTime < a.time) continue;
            float t = b.time > a.time ? (float)((renderTime - a.time) / (b.time - a.time)) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(a.position, b.position, t),
                                             Quaternion.Slerp(a.rotation, b.rotation, t));
            return;
        }
        transform.SetPositionAndRotation(snapshots[0].position, snapshots[0].rotation);
    }

    private void PushSnapshot(Snapshot snapshot) {
        if (snapshotCount == snapshots.Length) {
            for (int i = 1; i < snapshots.Length; i++) snapshots[i - 1] = snapshots[i];
            snapshotCount--;
        }
        snapshots[snapshotCount++] = snapshot;
    }

    private double RoomTime => realtime != null && realtime.room != null ? realtime.room.time : Time.timeAsDouble;

    // ------------------------------------------------------------------ Pemain yang keluar room

    private void CheckMissingSimulator() {
        int sim = model.simulator;
        if (sim == 0 || sim == LocalClientId + 1 || VehicleAvatars.Find(sim - 1)) {
            simulatorMissingSince = -1f;
            return;
        }
        if (simulatorMissingSince < 0f) simulatorMissingSince = Time.time;
        if (Time.time - simulatorMissingSince < MissingOwnerGrace) return;
        // Sopir keluar room saat menyetir: parkir di pose terakhir yang DITERIMA (bukan hasil ekstrapolasi).
        simulatorMissingSince = -1f;
        if (snapshotCount > 0) {
            Snapshot last = snapshots[snapshotCount - 1];
            WriteParked(last.position, last.rotation);
        } else {
            WriteParked(transform.position, transform.rotation);
        }
        model.simulator = 0;
    }

    // ------------------------------------------------------------------ Kursi pemain lain

    private void UpdateSeats() {
        int local = LocalClientId + 1;
        for (int seat = 0; seat < Mathf.Min(car.SeatCount, CarSyncModel.MaxSeats); seat++) {
            int client = model.GetSeatClient(seat);
            VehicleSeatPhase phase = model.GetSeatPhase(seat);
            car.SetEnterZoneAvailable(seat, client == 0);

            VehicleOccupant current = remoteOccupants[seat];
            if (client == 0 || client == local) {
                if (current) current.RemoteSeatCleared();
                remoteOccupants[seat] = null;
                seatMissingSince[seat] = -1f;
                continue;
            }

            PlayerController avatar = VehicleAvatars.Find(client - 1);
            if (!avatar) {
                if (seatMissingSince[seat] < 0f) seatMissingSince[seat] = Time.time;
                if (Time.time - seatMissingSince[seat] > MissingOwnerGrace) model.SetSeat(seat, 0, VehicleSeatPhase.Empty);
                continue;
            }
            seatMissingSince[seat] = -1f;

            VehicleOccupant occupant = avatar.GetComponent<VehicleOccupant>();
            if (!occupant) occupant = avatar.gameObject.AddComponent<VehicleOccupant>();
            if (current && current != occupant) current.RemoteSeatCleared();
            remoteOccupants[seat] = occupant;
            occupant.ApplyRemoteSeat(car, seat, phase, model.GetSeatPose(seat), model.GetSeatYaw(seat));
        }
    }

    // ------------------------------------------------------------------ Respawn

    private void UpdateRespawn() {
        TrafficNetwork traffic = TrafficNetwork.Active;
        if (!traffic || !traffic.IsAuthority || model.simulator != 0 || !car.IsEmpty) return;
        if (car.BlockingTrafficSeconds < RespawnAfterBlocking) return;
        car.ClearTrafficBlock();
        Debug.Log($"[CarSync] {name} menghalangi traffic {RespawnAfterBlocking} detik tanpa pengemudi: dikembalikan ke titik spawn.", this);
        WriteParked(model.spawnPosition, model.spawnRotation);
    }

    // ------------------------------------------------------------------ Venue

    private void UpdateVisibility() {
        bool inWorld = VenueTravelManager.LocalVenue == VenueId.World;
        if (inWorld == visible) return;
        visible = inWorld;
        // Simpan status asli saat disembunyikan (sebagian renderer prefab memang mati, mis. penanda EnterL/EnterR).
        for (int i = 0; i < renderers.Length; i++) {
            if (!renderers[i]) continue;
            if (!inWorld) rendererWasEnabled[i] = renderers[i].enabled;
            renderers[i].enabled = inWorld && rendererWasEnabled[i];
        }
        for (int i = 0; i < colliders.Length; i++) {
            if (!colliders[i]) continue;
            if (!inWorld) colliderWasEnabled[i] = colliders[i].enabled;
            colliders[i].enabled = inWorld && colliderWasEnabled[i];
        }
        car.SetHiddenInVenue(!inWorld);
    }
}
