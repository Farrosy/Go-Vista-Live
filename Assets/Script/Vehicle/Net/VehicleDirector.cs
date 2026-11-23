using System.Collections;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Hasil permintaan spawn mobil dari dealer.</summary>
public enum CarSpawnResult {
    Spawned,
    /// Belum terhubung ke room.
    Offline,
    /// Pemain sedang di venue lain (mobil hanya ada di World).
    NotInWorld,
    /// Patokan parkiran (SpawnPointMobil) belum dimuat / tidak ada.
    NoParking,
    /// Mobil lama pemain sedang dinaiki orang, jadi tidak bisa dipindah.
    OldCarInUse,
    /// Parkiran terhalang mobil/pemain lain.
    ParkingBlocked,
    Failed,
}

/// <summary>
/// Spawner mobil pemain. Pasang di object di MainMultiplayerScene bersama RealtimeView.
///
/// Mobil di-spawn atas permintaan pemain lewat dealer (CarDealerPanel > Spawn): SATU mobil per pemain. Spawn lagi =
/// mobil lama pemain itu dihapus, lalu mobil baru muncul di parkiran (patokan SpawnPointMobil di WorldScene,
/// menempel ke tanah, menghadap sumbu X lokal patokan). Pemilik dicatat di CarSyncModel.ownerClient.
///
/// Mobil tidak bisa ditaruh langsung di WorldScene, karena WorldScene di-unload saat pemain pindah venue. Jadi
/// mobil di-spawn lewat Realtime.Instantiate lalu dipindah ke MainMultiplayerScene (pola sama dengan NpcDirector).
///
/// Pembersihan (hanya client otoritas = clientID terkecil di World): mobil yang pemiliknya sudah keluar room dihapus
/// setelah kosong. Mobil tanpa pemilik (spawner otomatis versi lama) juga dihapus.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class VehicleDirector : RealtimeComponent<VehicleDirectorModel> {
    [Tooltip("Nama object patokan posisi spawn (parkiran depan dealer) di WorldScene.")]
    [SerializeField] private string spawnMarkerName = "SpawnPointMobil";
    [Tooltip("Ukuran kotak yang harus kosong di parkiran sebelum spawn (meter, lebar x tinggi x panjang mobil).")]
    [SerializeField] private Vector3 parkingClearance = new Vector3(2.2f, 1.6f, 4.6f);
    [Tooltip("Jeda antar pengecekan pembersihan (detik).")]
    [SerializeField] private float evaluateInterval = 1f;
    [Tooltip("Mobil yang pemiliknya tidak terlihat selama ini (detik) dihapus.")]
    [SerializeField] private float missingOwnerGrace = 5f;

    public static VehicleDirector Instance { get; private set; }

    private Realtime _realtime;
    private Coroutine _loop;
    private readonly Dictionary<CarSync, float> _ownerMissingSince = new Dictionary<CarSync, float>();
    private readonly List<CarSync> _scratch = new List<CarSync>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    void OnEnable() {
        Instance = this;
        _realtime = FindAnyObjectByType<Realtime>();
        _loop = StartCoroutine(CleanupLoop());
    }

    void OnDisable() {
        if (Instance == this) Instance = null;
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    private bool Connected => _realtime != null && _realtime.connected && _realtime.clientID >= 0;

    /// <summary>Posisi parkiran (untuk tutorial/kamera). False kalau WorldScene belum dimuat.</summary>
    public bool TryGetParking(out Vector3 position, out Quaternion rotation) {
        GameObject marker = GameObject.Find(spawnMarkerName);
        if (!marker) {
            position = default;
            rotation = default;
            return false;
        }
        position = GroundBelow(marker.transform);
        Vector3 forward = Vector3.ProjectOnPlane(marker.transform.right, Vector3.up).normalized;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        return true;
    }

    /// <summary>Spawn mobil milik pemain lokal di parkiran. Mobil lama pemain (kalau ada) dihapus dulu.</summary>
    public CarSpawnResult SpawnLocalCar(string prefabName) {
        if (!Connected) return CarSpawnResult.Offline;
        if (VenueTravelManager.LocalVenue != VenueId.World) return CarSpawnResult.NotInWorld;
        if (!TryGetParking(out Vector3 position, out Quaternion rotation)) return CarSpawnResult.NoParking;

        int me = _realtime.clientID + 1;
        _scratch.Clear();
        foreach (CarSync car in CarSync.All)
            if (car && car.OwnerClientPlusOne == me) _scratch.Add(car);
        foreach (CarSync old in _scratch)
            if (!old.IsUnoccupied) return CarSpawnResult.OldCarInUse;

        if (IsParkingBlocked(position, rotation, _scratch)) return CarSpawnResult.ParkingBlocked;

        foreach (CarSync old in _scratch) Realtime.Destroy(old.gameObject);

        var options = new Realtime.InstantiateOptions {
            ownedByClient = false,                // model mobil tidak dimiliki siapa pun: tiap client menulis bagiannya
            preventOwnershipTakeover = false,
            destroyWhenOwnerLeaves = false,
            destroyWhenLastClientLeaves = true,
            useInstance = _realtime,
        };
        GameObject spawned = Realtime.Instantiate(prefabName, position, rotation, options);
        if (!spawned) {
            Debug.LogError($"[VehicleDirector] Gagal spawn '{prefabName}'. Prefab harus ada di folder Resources.", this);
            return CarSpawnResult.Failed;
        }
        if (spawned.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(spawned, gameObject.scene);
        if (spawned.TryGetComponent(out CarSync sync)) sync.InitSpawn(position, rotation, me);
        Debug.Log($"[VehicleDirector] {prefabName} milik client {me - 1} di-spawn di {position}.", this);
        return CarSpawnResult.Spawned;
    }

    /// <summary>Ada mobil lain (selain mobil lama sendiri yang akan dihapus) atau pemain di kotak parkiran?</summary>
    private bool IsParkingBlocked(Vector3 position, Quaternion rotation, List<CarSync> ignore) {
        Vector3 half = parkingClearance * 0.5f;
        // Kotak sejajar mobil: panjang mengikuti arah hadap (Z dari rotation).
        Vector3 center = position + Vector3.up * (half.y + 0.15f);
        foreach (Collider hit in Physics.OverlapBox(center, half, rotation, Physics.DefaultRaycastLayers,
                                                    QueryTriggerInteraction.Ignore)) {
            CarSync car = hit.GetComponentInParent<CarSync>();
            if (car && !ignore.Contains(car)) return true;
            if (hit.GetComponentInParent<PlayerController>()) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ Pembersihan

    private IEnumerator CleanupLoop() {
        var wait = new WaitForSeconds(Mathf.Max(0.2f, evaluateInterval));
        while (true) {
            yield return wait;
            Cleanup();
        }
    }

    private void Cleanup() {
        if (!Connected || !IsAuthority()) {
            _ownerMissingSince.Clear();
            return;
        }
        _scratch.Clear();
        _scratch.AddRange(CarSync.All);
        foreach (CarSync car in _scratch) {
            if (!car) continue;
            int owner = car.OwnerClientPlusOne;
            bool ownerHere = owner != 0 && VehicleAvatars.Find(owner - 1);
            if (ownerHere) {
                _ownerMissingSince.Remove(car);
                continue;
            }
            if (!_ownerMissingSince.TryGetValue(car, out float since)) {
                _ownerMissingSince[car] = Time.time;
                continue;
            }
            if (Time.time - since < missingOwnerGrace || !car.IsUnoccupied) continue;
            _ownerMissingSince.Remove(car);
            Debug.Log($"[VehicleDirector] Pemilik {car.name} (client {owner - 1}) sudah keluar room: mobil dihapus.", this);
            Realtime.Destroy(car.gameObject);
        }
        // Entry mobil yang sudah hilang.
        _scratch.Clear();
        foreach (CarSync key in _ownerMissingSince.Keys) if (!key) _scratch.Add(key);
        foreach (CarSync key in _scratch) _ownerMissingSince.Remove(key);
    }

    /// <summary>Tanah tepat di bawah patokan (collider patokan sendiri dan trigger diabaikan).</summary>
    private static Vector3 GroundBelow(Transform marker) {
        Vector3 position = marker.position;
        Collider markerCollider = marker.GetComponent<Collider>();
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 5f, Vector3.down, 30f,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.collider == markerCollider || hit.distance >= best) continue;
            if (hit.collider.GetComponentInParent<PlayerController>()) continue;
            if (hit.collider.GetComponentInParent<CarSync>()) continue;
            best = hit.distance;
            position.y = hit.point.y;
        }
        return position;
    }

    /// <summary>Sama dengan NpcDirector: clientID terkecil di antara avatar yang sedang di World, dan itu kita.</summary>
    private bool IsAuthority() {
        if (VenueTravelManager.LocalVenue != VenueId.World) return false;
        int myId = _realtime.clientID;
        if (myId < 0) return false;

        int authority = int.MaxValue;
        foreach (PlayerVenueSync avatar in FindObjectsByType<PlayerVenueSync>(FindObjectsInactive.Exclude)) {
            if (avatar.Venue != VenueId.World) continue;
            RealtimeView view = avatar.GetComponent<RealtimeView>();
            int owner = view ? view.ownerIDInHierarchy : -1;
            if (owner >= 0 && owner < authority) authority = owner;
        }
        return authority != int.MaxValue && myId == authority;
    }
}
