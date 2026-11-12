using Normal.Realtime;
using UnityEngine;

/// <summary>
/// Pasang di object yang punya Realtime (MainMultiplayerScene). Membuat avatar pemain lokal saat
/// tombol MULAI ditekan (LobyUISceneController memanggil BeginGame), lalu menyambungkan kamera.
///
/// Prefab avatar = karakter pilihan pemain (CharacterSelection / CharacterDatabase). Field Prefab di
/// bawah dipakai kalau database kosong. Prefab harus ada di folder Resources (Realtime.Instantiate
/// memanggil lewat nama) dan berisi RealtimeView, RealtimeTransform, PlayerController, dan PlayerAnimSync.
/// </summary>
[RequireComponent(typeof(Realtime))]
public class PlayerManager : MonoBehaviour {
    [SerializeField] private GameObject _camera = default;
    [Tooltip("Cadangan kalau CharacterDatabase kosong. Prefab avatar di folder Resources.")]
    [SerializeField] private GameObject _prefab;
    [Tooltip("Backdrop lobby (Floor + Cubes + cahaya) di MainMultiplayerScene. Disembunyikan saat gameplay " +
             "supaya lantai lobby tidak menembus/menimpa scene venue yang berada di dekat y=0.")]
    [SerializeField] private GameObject _lobbyEnvironment;

    private Realtime _realtime;
    private PlayerController _localPlayer;
    private bool _gameStarted;
    private Vector3 _waitingCameraPosition;
    private Quaternion _waitingCameraRotation;

    private void Awake() {
        _realtime = GetComponent<Realtime>();

        if (_camera != null) {
            _waitingCameraPosition = _camera.transform.position;
            _waitingCameraRotation = _camera.transform.rotation;
        }

        _realtime.didConnectToRoom += DidConnectToRoom;
        _realtime.didDisconnectFromRoom += DidDisconnectFromRoom;
    }

    private void OnDestroy() {
        if (_realtime != null) {
            _realtime.didConnectToRoom -= DidConnectToRoom;
            _realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        }
    }

    private void DidConnectToRoom(Realtime realtime) {
        // Terhubung ke room baru masuk lobby tunggu. Avatar dibuat setelah pemain menekan MULAI.
        VoiceSession.ResetForNewRoom();
        _localPlayer = null;
        _gameStarted = false;
        MobileControls.SetVisible(false);
        SetLobbyEnvironmentVisible(true);
        ResetWaitingCamera();
    }

    private void DidDisconnectFromRoom(Realtime realtime) {
        // Simpan progres (dialog, dll.) sebelum keluar room.
        _ = ProgressService.SaveAsync();
        _localPlayer = null;
        _gameStarted = false;
        MobileControls.SetVisible(false);
        SetLobbyEnvironmentVisible(true);
        ResetWaitingCamera();
    }

    public void BeginGame() {
        if (_gameStarted || _realtime == null || !_realtime.connected)
            return;

        _gameStarted = true;
        MobileControls.SetVisible(true);
        SetLobbyEnvironmentVisible(false);
        SpawnLocalPlayer(_realtime);
    }

    /// Tampilkan/sembunyikan backdrop lobby. Disembunyikan saat gameplay supaya tidak menimpa venue.
    private void SetLobbyEnvironmentVisible(bool visible) {
        if (_lobbyEnvironment != null)
            _lobbyEnvironment.SetActive(visible);
    }

    private void ResetWaitingCamera() {
        if (_camera == null)
            return;

        CameraOrbit cameraOrbit = _camera.GetComponent<CameraOrbit>();
        if (cameraOrbit != null)
            cameraOrbit.SetTarget(null);
        _camera.transform.SetPositionAndRotation(_waitingCameraPosition, _waitingCameraRotation);
    }

    private void SpawnLocalPlayer(Realtime realtime) {
        GameObject prefab = CharacterSelection.Current?.avatarPrefab;
        if (prefab == null)
            prefab = _prefab;
        if (prefab == null) {
            Debug.LogError("[Spawn] Prefab avatar belum diisi (CharacterDatabase / PlayerManager).");
            return;
        }

        var options = new Realtime.InstantiateOptions {
            ownedByClient            = true,    // RealtimeView avatar dimiliki client ini.
            preventOwnershipTakeover = true,    // Client lain tidak bisa RequestOwnership() avatar ini.
            useInstance              = realtime
        };

        int clientID = Mathf.Max(0, realtime.clientID);
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        // Spawn awal selalu di titik World/CentralPlaza scene world (posisi terakhir tidak disimpan).
        PlayerSpawnPoint spawnPoint = PlayerSpawnPoint.Find(VenueId.World, SpawnSlot.CentralPlaza);
        if (spawnPoint != null) {
            spawnPosition = spawnPoint.GetPosition(clientID);
            spawnRotation = spawnPoint.Rotation;
        } else {
            // Tanpa spawn point, sebar melingkar di sekitar (0, 0, 0) supaya avatar tidak tumpuk.
            Debug.LogWarning("[Spawn] PlayerSpawnPoint World/CentralPlaza tidak ada di scene world, spawn di sekitar (0, 0, 0).");
            float angle = clientID * 2.39996323f;
            float radius = 3.0f + Mathf.Sqrt(clientID) * 0.5f;
            spawnPosition = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
            spawnRotation = Quaternion.identity;
        }

        GameObject playerGameObject = Realtime.Instantiate(prefab.name, spawnPosition, spawnRotation, options);
        playerGameObject.name = PlayerNameRules.DisplayName(ProgressService.Current.playerName, clientID);

        _localPlayer = playerGameObject.GetComponent<PlayerController>();
        if (_localPlayer == null) {
            Debug.LogError($"[Spawn] Prefab '{prefab.name}' tidak punya PlayerController.");
            return;
        }

        if (_camera != null) {
            _localPlayer.SetCamera(_camera.transform);
            CameraOrbit cameraOrbit = _camera.GetComponent<CameraOrbit>();
            if (cameraOrbit != null)
                cameraOrbit.SetTarget(_localPlayer.transform);
        }
    }
}
