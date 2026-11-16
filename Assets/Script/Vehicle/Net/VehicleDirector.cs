using System.Collections;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawner mobil pemain (versi minimal: satu mobil di satu titik). Pasang di object di MainMultiplayerScene
/// bersama RealtimeView.
///
/// Mobil tidak bisa ditaruh langsung di WorldScene, karena WorldScene di-unload saat pemain pindah venue. Jadi
/// mobil di-spawn lewat Realtime.Instantiate lalu dipindah ke MainMultiplayerScene (pola sama dengan NpcDirector).
///
/// Otoritas = client dengan clientID terkecil di antara pemain yang sedang di World. Hanya otoritas yang men-spawn,
/// dan hanya kalau belum ada mobil di room. Posisi = cube patokan (SpawnPointMobil) di WorldScene, menempel ke tanah,
/// menghadap sumbu X lokal cube.
///
/// TODO (sistem spawn berikutnya): banyak titik spawn, pilihan mobil, respawn mobil yang ditinggal jauh.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class VehicleDirector : RealtimeComponent<VehicleDirectorModel> {
    [Tooltip("Nama prefab mobil di folder Resources (tanpa .prefab).")]
    [SerializeField] private string prefabName = "Vehicles/Matador_Drivable";
    [Tooltip("Nama object patokan posisi spawn di WorldScene.")]
    [SerializeField] private string spawnMarkerName = "SpawnPointMobil";
    [Tooltip("Jeda antar pengecekan (detik).")]
    [SerializeField] private float evaluateInterval = 1f;
    [Tooltip("Kalau penanda 'sudah spawn' ada tapi mobilnya tidak ada selama ini (detik), spawn ulang. " +
             "Memberi waktu mobil yang sudah ada di room muncul di client yang baru masuk.")]
    [SerializeField] private float missingCarGrace = 4f;

    private Realtime _realtime;
    private RealtimeView _view;
    private Coroutine _loop;
    private float _missingSince = -1f;

    void Awake() => _view = GetComponent<RealtimeView>();

    void OnEnable() {
        _realtime = FindAnyObjectByType<Realtime>();
        _loop = StartCoroutine(EvaluateLoop());
    }

    void OnDisable() {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    private IEnumerator EvaluateLoop() {
        var wait = new WaitForSeconds(Mathf.Max(0.2f, evaluateInterval));
        while (true) {
            yield return wait;
            Evaluate();
        }
    }

    private void Evaluate() {
        if (_realtime == null || !_realtime.connected || model == null) return;

        if (CarSync.All.Count > 0) {
            _missingSince = -1f;
            return;
        }
        // Penanda sudah spawn, tapi mobilnya belum terlihat: tunggu dulu (bisa jadi baru masuk room), lalu spawn ulang.
        if (model.spawned) {
            if (_missingSince < 0f) _missingSince = Time.time;
            if (Time.time - _missingSince < missingCarGrace) return;
        }
        if (!IsAuthority()) return;

        if (!_view.isOwnedLocallyInHierarchy) {
            _view.RequestOwnership();   // perlu memiliki view untuk menulis penanda; lanjut di pengecekan berikutnya
            return;
        }

        GameObject marker = GameObject.Find(spawnMarkerName);
        if (!marker) return;   // WorldScene belum selesai dimuat
        Spawn(marker.transform);
    }

    private void Spawn(Transform marker) {
        Vector3 position = GroundBelow(marker);
        Vector3 forward = Vector3.ProjectOnPlane(marker.right, Vector3.up).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        var options = new Realtime.InstantiateOptions {
            ownedByClient = false,                // model mobil tidak dimiliki siapa pun: tiap client menulis bagiannya
            preventOwnershipTakeover = false,
            destroyWhenOwnerLeaves = false,
            destroyWhenLastClientLeaves = true,
            useInstance = _realtime,
        };
        GameObject car = Realtime.Instantiate(prefabName, position, rotation, options);
        if (!car) {
            Debug.LogError($"[VehicleDirector] Gagal spawn '{prefabName}'. Prefab harus ada di folder Resources.", this);
            return;
        }
        if (car.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(car, gameObject.scene);
        if (car.TryGetComponent(out CarSync sync)) sync.InitSpawn(position, rotation);
        model.spawned = true;
        _missingSince = -1f;
        Debug.Log($"[VehicleDirector] {prefabName} di-spawn di {position}.", this);
    }

    /// Tanah tepat di bawah patokan (collider patokan sendiri dan trigger diabaikan).
    private static Vector3 GroundBelow(Transform marker) {
        Vector3 position = marker.position;
        Collider markerCollider = marker.GetComponent<Collider>();
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 5f, Vector3.down, 30f,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.collider == markerCollider || hit.distance >= best) continue;
            if (hit.collider.GetComponentInParent<PlayerController>()) continue;
            best = hit.distance;
            position.y = hit.point.y;
        }
        return position;
    }

    /// Sama dengan NpcDirector: clientID terkecil di antara avatar yang sedang di World, dan itu kita.
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
