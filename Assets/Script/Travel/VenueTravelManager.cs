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

    [Tooltip("Skala avatar saat berada di venue (World tetap 1). Interior venue lebih sempit, jadi avatar " +
             "dikecilkan supaya proporsional dengan furnitur/pintu normal.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float _venueAvatarScale = 0.7f;

    private ISceneTransition _transition;
    private Realtime _realtime;
    private bool _traveling;
    private Coroutine _travelRoutine;
    private bool _holdsLowLoadPriority;

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
        _travelRoutine = StartCoroutine(TravelRoutine(target, slot));
    }

    /// <summary>
    /// Kembalikan semua state venue ke kondisi awal (World). Dipanggil LobyUISceneController saat lepas
    /// dari room (koneksi putus atau keluar room). Tanpa ini scene venue, state pad, panel pilihan, dan
    /// kunci input dari sesi lama tertinggal, sehingga pindah pulau tidak merespons setelah masuk lagi.
    /// unloadVenueScenes = false kalau scene utama akan di-load ulang (Single) sebentar lagi.
    /// </summary>
    public void ResetToWorld(bool unloadVenueScenes)
    {
        if (_travelRoutine != null)
            StopCoroutine(_travelRoutine);
        _travelRoutine = null;
        _traveling = false;
        ReleaseLoadPriority();

        if (TravelChoiceUI.Instance != null && TravelChoiceUI.Instance.IsOpen)
            TravelChoiceUI.Instance.Close();
        if (PlayerInputHub.Instance != null)
            PlayerInputHub.Instance.SetPauseLocked(false);
        Transition?.ResetImmediate();

        CurrentVenue = VenueId.World;
        SetLocalVenue(VenueId.World);
        if (unloadVenueScenes)
            UnloadVenueScenes();
    }

    private void ReleaseLoadPriority()
    {
        if (!_holdsLowLoadPriority)
            return;
        _holdsLowLoadPriority = false;
        SceneLoadPriority.Restore();
    }

    /// WorldScene tidak disentuh di sini; itu diatur LobyUISceneController.
    private void UnloadVenueScenes()
    {
        Scene home = gameObject.scene;
        foreach (VenueId venue in (VenueId[])Enum.GetValues(typeof(VenueId)))
        {
            if (venue == VenueId.World)
                continue;
            Scene scene = SceneManager.GetSceneByName(VenueMap.SceneName(venue));
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            if (SceneManager.GetActiveScene() == scene && home.IsValid() && home.isLoaded)
                SceneManager.SetActiveScene(home);
            SceneManager.UnloadSceneAsync(scene);
        }
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
            // Cicil integrasi scene per frame supaya main thread tidak macet lama (Normcore ikut
            // memproses jaringan di main thread; macet lama = koneksi room putus).
            SceneLoadPriority.UseLow();
            _holdsLowLoadPriority = true;
            AsyncOperation load = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                ReleaseLoadPriority();
                Debug.LogError($"[VenueTravel] Gagal memuat '{targetSceneName}'. Pastikan ada & enabled di Build Settings.");
                yield return AbortReveal(hub);
                yield break;
            }
            yield return load;
            ReleaseLoadPriority();
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
            // Skala avatar sesuai venue (World = 1, venue = lebih kecil) selagi CharacterController mati.
            // RealtimeTransform menyinkronkan skala, jadi pemain se-venue melihat ukuran yang sama.
            float scale = target == VenueId.World ? 1f : Mathf.Clamp(_venueAvatarScale, 0.05f, 1f);
            avatar.localScale = new Vector3(scale, scale, scale);
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
        _travelRoutine = null;
    }

    /// Reveal + buka kunci saat travel gagal, supaya tidak soft-lock di layar hitam.
    private IEnumerator AbortReveal(PlayerInputHub hub)
    {
        yield return Transition.Reveal();
        if (hub != null) hub.SetPauseLocked(false);
        MobileControls.SetVisible(true);
        _traveling = false;
        _travelRoutine = null;
    }

    private int LocalClientID()
    {
        if (_realtime == null) _realtime = FindAnyObjectByType<Realtime>();
        return _realtime != null ? Mathf.Max(0, _realtime.clientID) : 0;
    }

    /// Matikan semua Camera dan hapus AudioListener di sebuah scene (pola sama dengan DisableWorldCameras).
    /// Listener dihapus, bukan di-disable: echo cancellation Normcore memakai listener pertama yang ditemukan,
    /// termasuk yang disabled, sehingga bisa salah target.
    private static void DisableSceneCamerasAndAudio(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                Destroy(listener);
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
