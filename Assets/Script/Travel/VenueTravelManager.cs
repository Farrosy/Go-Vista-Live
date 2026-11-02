using System;
using System.Collections;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Pengelola perpindahan venue (teleport antar scene additive dalam SATU Normcore room).
///
/// Singleton pada object persisten di MainMultiplayerScene (sebelah PlayerManager). MainMultiplayerScene
/// tidak pernah di-unload, jadi object ini (dan avatar lokal) bertahan lintas pergantian scene venue.
/// Travel = tukar scene additive + reposisi avatar yang SAMA (bukan destroy/respawn).
///
/// Juga memegang state venue LOKAL statik (dipakai PlayerVenueSync & VenueVisibilityFilter) untuk
/// visibilitas "satu room, hanya lihat yang se-venue".
public class VenueTravelManager : MonoBehaviour
{
    // ---- State venue lokal (statik, dari Fase 2) ----

    /// Venue yang sedang ditempati pemain lokal. Cerminan dari PlayerVenueSync avatar lokal.
    public static VenueId LocalVenue { get; private set; } = VenueId.World;

    /// Dipicu saat LocalVenue berubah. VenueVisibilityFilter mendengarkan ini untuk re-evaluasi.
    public static event Action VenueChanged;

    /// Dipanggil PlayerVenueSync avatar lokal saat venue-nya berubah.
    public static void SetLocalVenue(VenueId venue)
    {
        if (LocalVenue == venue)
            return;
        LocalVenue = venue;
        VenueChanged?.Invoke();
    }

    // ---- Singleton + teleporter (Fase 3) ----

    public static VenueTravelManager Instance { get; private set; }

    /// Venue yang scene-nya sedang ter-load sebagai venue aktif (yang akan di-unload saat hop berikut).
    public VenueId CurrentVenue { get; private set; } = VenueId.World;

    /// True selama satu TravelTo sedang berjalan (anti re-entry).
    public bool IsTraveling => _traveling;

    [Tooltip("Komponen transisi (harus implement ISceneTransition). Kosong = pakai FadeToBlackTransition otomatis.")]
    [SerializeField] private MonoBehaviour _transitionComponent;

    private ISceneTransition _transition;
    private Realtime _realtime;
    private bool _traveling;

    /// Transisi aktif. Default ke FadeToBlackTransition supaya LoadingScreenTransition bisa
    /// menggantikannya di satu tempat saja (serialized field) tanpa menyentuh TravelTo.
    public ISceneTransition Transition
    {
        get
        {
            if (_transition != null)
                return _transition;
            if (_transitionComponent is ISceneTransition fromField)
            {
                _transition = fromField;
                return _transition;
            }
            var existing = GetComponent<FadeToBlackTransition>();
            if (existing != null)
            {
                _transition = existing;
                return _transition;
            }
            // Jangan pernah membuat komponen/UI saat edit mode (mis. inspector/tooling membaca properti ini).
            if (Application.isPlaying)
                _transition = gameObject.AddComponent<FadeToBlackTransition>();
            return _transition;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// Dipanggil setelah spawn pertama (LobyUISceneController): daftarkan WorldScene yang sudah
    /// ter-load sebagai venue awal. LocalVenue sudah World lewat PlayerVenueSync avatar.
    public void AdoptInitialVenue(VenueId venue)
    {
        CurrentVenue = venue;
    }

    /// Pindah pemain lokal ke venue lain. Hanya pemain yang memanggil yang berpindah (locked decision).
    public void TravelTo(VenueId target, SpawnSlot slot)
    {
        if (_traveling)
        {
            Debug.LogWarning("[VenueTravel] Travel sedang berjalan; permintaan diabaikan.");
            return;
        }
        StartCoroutine(TravelRoutine(target, slot));
    }

    private IEnumerator TravelRoutine(VenueId target, SpawnSlot slot)
    {
        _traveling = true;
        VenueId previous = CurrentVenue;

        // 1-2. Kunci input, sembunyikan kontrol mobile, tutup layar (fade ke hitam). Tunggu sampai tertutup.
        var hub = PlayerInputHub.Instance;
        if (hub != null) hub.SetPauseLocked(true);
        MobileControls.SetVisible(false);
        yield return Transition.Cover();

        // 3. Load scene target additive kalau belum ter-load.
        string targetSceneName = VenueMap.SceneName(target);
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[VenueTravel] Gagal memuat '{targetSceneName}'. Pastikan ada & enabled di Build Settings.");
                yield return AbortReveal(hub);
                yield break;
            }
            yield return load;
            targetScene = SceneManager.GetSceneByName(targetSceneName);
        }

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"[VenueTravel] Scene '{targetSceneName}' tidak valid setelah load.");
            yield return AbortReveal(hub);
            yield break;
        }

        // 4. Matikan kamera + AudioListener scene target (satu-satunya kamera hidup ada di MainMultiplayerScene).
        DisableSceneCamerasAndAudio(targetScene);

        // 5. Jadikan scene target aktif supaya lighting/skybox/reflection (dan NavMesh NPC) ikut.
        SceneManager.SetActiveScene(targetScene);

        // 6. Unload scene venue sebelumnya (jangan kalau target == previous; MainMultiplayerScene tak pernah di-unload).
        if (previous != target)
        {
            Scene previousScene = SceneManager.GetSceneByName(VenueMap.SceneName(previous));
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(previousScene);
                if (unload != null)
                    yield return unload;
            }
        }

        // 7. Teleport avatar yang SAMA dengan aman (disable CharacterController, set pose, re-enable).
        PlayerController local = PlayerController.Local;
        if (local != null)
        {
            Transform avatar = local.transform;
            CharacterController cc = local.GetComponent<CharacterController>();
            PlayerSpawnPoint spawn = PlayerSpawnPoint.Find(target, slot);

            Vector3 position = avatar.position;
            Quaternion rotation = avatar.rotation;
            if (spawn != null)
            {
                position = spawn.GetPosition(LocalClientID());
                rotation = spawn.Rotation;
            }
            else
            {
                Debug.LogWarning($"[VenueTravel] Spawn {target}/{slot} tidak ditemukan; avatar tetap di tempat.");
            }

            if (cc != null) cc.enabled = false;
            avatar.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;

            // 8. Set venue avatar lokal → PlayerVenueSync fires VenueChanged → filter re-evaluasi (selagi hitam).
            var venueSync = local.GetComponent<PlayerVenueSync>();
            if (venueSync != null) venueSync.SetVenue(target);

            // 9. Pastikan kamera tetap mengikuti avatar yang sama.
            var orbit = FindAnyObjectByType<CameraOrbit>();
            if (orbit != null) orbit.SetTarget(avatar);
        }
        else
        {
            Debug.LogWarning("[VenueTravel] PlayerController.Local null; tidak ada avatar untuk dipindah.");
        }

        CurrentVenue = target;

        // 9. Buka layar (fade in), buka kunci input, kembalikan kontrol mobile.
        yield return Transition.Reveal();
        if (hub != null) hub.SetPauseLocked(false);
        MobileControls.SetVisible(true);
        _traveling = false;
    }

    /// Reveal + buka kunci saat travel gagal, supaya tidak soft-lock di layar hitam.
    private IEnumerator AbortReveal(PlayerInputHub hub)
    {
        yield return Transition.Reveal();
        if (hub != null) hub.SetPauseLocked(false);
        MobileControls.SetVisible(true);
        _traveling = false;
    }

    private int LocalClientID()
    {
        if (_realtime == null) _realtime = FindAnyObjectByType<Realtime>();
        return _realtime != null ? Mathf.Max(0, _realtime.clientID) : 0;
    }

    /// Matikan semua Camera + AudioListener di sebuah scene (pola sama dengan DisableWorldCameras).
    private static void DisableSceneCamerasAndAudio(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }
    }

    // Reset state statik saat masuk play mode / domain reload supaya tidak ada subscriber/instance basi.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalVenue = VenueId.World;
        VenueChanged = null;
        Instance = null;
    }
}
