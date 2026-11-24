using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ruang ganti di clothing shop: pemain berjalan masuk ke bilik, berdiri di lingkaran cahaya, lalu menekan
/// "Ganti Baju" (Interactable di objek ini). Editor karakter (scene KARAKTER, tab khusus ruang ganti) dibuka
/// dengan karakter berdiri di bilik ini dan kamera di toko, jadi latarnya toko asli, bukan warna polos.
///
/// Transisi: kamera gameplay meluncur halus ke pose kamera editor (dinding bilik masih ada), layar fade singkat
/// menutupi momen dinding depan disembunyikan, scene dimuat dan karakter preview di-bake, lalu editor muncul.
/// Saat ditutup urutannya dibalik (fade, unload, buka layar, kamera meluncur kembali sambil avatar berjalan
/// keluar bilik ke depan cermin luar), baru HUD dan kontrol dikembalikan.
///
/// Selama editor terbuka, HANYA di layar pemain ini: gerak terkunci, HUD disembunyikan, dinding depan bilik
/// (+ tirai) disembunyikan (tampilan potongan), avatar asli pemain disembunyikan (diganti karakter preview),
/// dan pemain lain di antara kamera dan bilik disembunyikan supaya tidak menghalangi. Semua dikembalikan
/// saat editor ditutup. Dipasang oleh menu Go Vista > Interior > Pasang Ruang Ganti (FittingRoomBuilder).
/// </summary>
[RequireComponent(typeof(Interactable))]
public sealed class FittingRoomStation : MonoBehaviour {
    // Area bebas halangan dari kamera ke bilik (meter): lebar ke kiri/kanan, tinggi dari lantai, lebih ke belakang.
    private const float ClearHalfWidth = 1.4f;
    private const float ClearHeight = 2.6f;
    private const float ClearMargin = 0.6f;
    private const float PlayerScanSeconds = 0.5f;
    // Batas tunggu (detik) scene dimuat/di-unload dan karakter preview selesai di-bake, supaya layar tidak macet hitam.
    private const float MaxWaitSeconds = 6f;
    // Avatar merapikan posisi: dianggap sampai dalam jarak ini (m), putar di tempat (derajat/detik), batas waktu.
    private const float ArriveDistance = 0.08f;
    private const float AlignTurnSpeed = 360f;
    private const float MaxAlignSeconds = 2.5f;
    // Jalan keluar ke cermin: titik antara dianggap dilewati dalam jarak ini (m) supaya tidak berhenti-berhenti.
    private const float WaypointPassDistance = 0.25f;
    private const float MaxExitSeconds = 6f;

    private enum Phase { Idle, Opening, Active, Closing }

    [Tooltip("Titik lantai tempat karakter preview berdiri; sumbu biru (forward) menghadap ke kamera.")]
    [SerializeField] private Transform subject;
    [Tooltip("Pose kamera editor (di toko, di depan bilik).")]
    [SerializeField] private Transform viewpoint;
    [SerializeField] private float fieldOfView = 33f;
    [Tooltip("Disembunyikan selama editor terbuka (dinding depan bilik + tirai), hanya di layar pemain ini.")]
    [SerializeField] private Renderer[] hideWhileEditing = new Renderer[0];

    [Header("Transisi")]
    [Tooltip("Lama kamera meluncur dari kamera gameplay ke kamera editor (dan kembali), detik.")]
    [SerializeField] private float glideSeconds = 0.7f;
    [Tooltip("Setelah editor ditutup, avatar berjalan melewati titik-titik ini berurutan (keluar pintu bilik), " +
             "berhenti di titik terakhir menghadap sumbu birunya (cermin luar), baru kontrol dikembalikan. " +
             "Kosong = tidak berjalan.")]
    [SerializeField] private Transform[] exitPath = new Transform[0];

    [Header("Syarat")]
    [Tooltip("Ruang ganti terkunci sampai pemain lokal menyelesaikan dialog ini (perkenalan kasir). Kosong = bebas.")]
    [SerializeField] private string requiredDialogueId = "mira_intro";
    [SerializeField] private string lockedTitle = "Ruang ganti belum bisa dipakai";
    [SerializeField] private string lockedDetail = "Ngobrol dulu dengan Mira di kasir, ya!";

    [Header("Quest")]
    [Tooltip("Opsional: tujuan quest (Manual) yang selesai saat pemain menyimpan penampilan BARU di ruang ganti ini, " +
             "mis. Tampil Beda > ganti-gaya. SIMPAN tanpa perubahan tidak dihitung.")]
    [SerializeField] private QuestData completesQuest;
    [SerializeField] private string completesObjectiveId = "";

    private Interactable _interactable;
    private Phase _phase = Phase.Idle;
    private bool _sceneOpened;
    private bool _sceneClosed;
    private bool _aligning;
    private bool _lockedInput;
    private bool _hudHidden;
    private CameraOrbit _orbit;
    private FadeToBlackTransition _fade;
    private Camera _mainCamera;
    private int _mainCullingMask;
    private float _nextPlayerScan;
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    private readonly HashSet<Renderer> _hiddenRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> _hiddenScenery = new HashSet<Renderer>();
    private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
    private readonly List<Canvas> _canvasBuffer = new List<Canvas>();
    private PlayerController[] _players = new PlayerController[0];
    // Pasangan collider (avatar lokal, pemain lain/NPC) yang sedang diabaikan selama berjalan keluar bilik.
    private readonly List<(Collider mine, Collider other)> _ignoredPairs = new List<(Collider, Collider)>();
    private readonly List<Collider> _colliderBuffer = new List<Collider>();

    void Awake() {
        _interactable = GetComponent<Interactable>();
        _interactable.onInteract.AddListener(Open);
        CharacterSceneLoader.FittingRoomLookChanged += OnLookChanged;
    }

    void OnDestroy() {
        CharacterSceneLoader.FittingRoomLookChanged -= OnLookChanged;
        if (_interactable) _interactable.onInteract.RemoveListener(Open);
        if (_phase == Phase.Idle) return;
        // Interior di-unload di tengah transisi / saat editor terbuka (mis. keluar room): kembalikan semuanya
        // seketika. Overlay fade milik objek ini, jadi ikut hilang.
        StopAligning();
        RestoreActors();
        RestoreScenery();
        RestoreCollisions();
        SetPresence(FittingRoomPresence.None);
        if (_orbit) _orbit.ReleasePoseImmediate();
        SetHudHidden(false);
        SetInputLocked(false);
        _phase = Phase.Idle;
        CharacterSceneLoader.CloseImmediate();
    }

    /// Progres per akun dan hanya dicek di perangkat ini, jadi pemain lain tidak terpengaruh.
    private bool IsUnlocked => string.IsNullOrEmpty(requiredDialogueId) ||
                               (ProgressService.IsLoaded && ProgressService.IsDialogueCompleted(requiredDialogueId));

    private FadeToBlackTransition Fade {
        get {
            if (!_fade) _fade = GetComponent<FadeToBlackTransition>();
            if (!_fade) _fade = gameObject.AddComponent<FadeToBlackTransition>();
            return _fade;
        }
    }

    /// Penampilan baru tersimpan di editor yang dibuka dari station INI: selesaikan tujuan quest-nya (kalau aktif).
    private void OnLookChanged() {
        if (_phase == Phase.Idle || completesQuest == null || string.IsNullOrEmpty(completesObjectiveId)) return;
        QuestService.CompleteObjective(completesQuest.Id, completesObjectiveId);
    }

    // ---------------------------------------------------------------- Buka

    private void Open() {
        if (_phase != Phase.Idle || CharacterSceneLoader.IsOpen || !subject || !viewpoint || !PlayerController.Local) return;
        if (!IsUnlocked) {
            QuestToastView.ShowMessage(lockedTitle, lockedDetail);
            return;
        }
        Camera main = Camera.main;
        var view = new FittingRoomView(new Pose(subject.position, subject.rotation),
                                       new Pose(viewpoint.position, viewpoint.rotation), fieldOfView,
                                       main ? main.cullingMask : ~0);
        _phase = Phase.Opening;
        _orbit = main ? main.GetComponent<CameraOrbit>() : null;
        SetInputLocked(true);
        _interactable.enabled = false; // prompt + tombol Interact hilang selama editor terbuka
        SetHudHidden(true);
        _nextPlayerScan = 0f;
        StartCoroutine(OpenRoutine(view));
    }

    private IEnumerator OpenRoutine(FittingRoomView view) {
        // Sambil kamera meluncur, avatar asli merapikan posisinya ke titik karakter preview (menghadap kamera),
        // jadi pergantian ke karakter preview di balik layar hitam tidak terlihat melompat.
        StartCoroutine(WalkPath(PlayerController.Local, new[] { subject.position }, subject.rotation, MaxAlignSeconds));
        yield return Glide(true);
        while (_aligning) yield return null; // WalkPath punya batas waktu sendiri
        yield return Fade.Cover();
        // Pemain lain melihat avatar masuk ke bilik, lalu hilang saat layar ini hitam (suara + minimap tetap).
        SetPresence(FittingRoomPresence.Hidden);
        // Dinding depan + tirai baru hilang di balik layar hitam; selama avatar merapikan posisi dinding tetap ada.
        HideScenery();

        _sceneOpened = false;
        _sceneClosed = false;
        CharacterSceneLoader.OpenFittingRoom(view, OnOpened, OnClosed, BeforeUnload);
        if (CharacterSceneLoader.FittingRoom == null && !CharacterSceneLoader.IsOpen) {
            // Scene tidak bisa dimuat (lihat Console): kembalikan pemain, jangan biarkan terkunci.
            yield return Fade.Reveal();
            yield return Glide(false);
            FinishClose();
            yield break;
        }

        float deadline = Time.unscaledTime + MaxWaitSeconds;
        while (!_sceneOpened && Time.unscaledTime < deadline) yield return null;
        yield return null; // beri editor satu frame untuk mulai merakit karakter preview
        var stage = FindAnyObjectByType<CharacterPreviewStage>();
        while (stage && stage.IsBusy && Time.unscaledTime < deadline) yield return null;
        yield return Fade.Reveal();
    }

    private void OnOpened() {
        if (!this) return; // interior sudah di-unload saat scene masih dimuat
        _sceneOpened = true;
        _phase = Phase.Active;
        // Editor merender toko dengan kameranya sendiri; kamera utama tidak perlu merender dunia dua kali.
        _mainCamera = Camera.main;
        if (_mainCamera) {
            _mainCullingMask = _mainCamera.cullingMask;
            _mainCamera.cullingMask = 0;
        }
    }

    // ---------------------------------------------------------------- Tutup

    /// Editor minta ditutup (SIMPAN / KEMBALI / Back): tutup layar dulu, baru scene di-unload.
    private void BeforeUnload(System.Action unload) {
        if (!this) {
            unload();
            return;
        }
        StopAllCoroutines(); // bisa terjadi saat layar masih tertutup menunggu preview
        StopAligning();
        StartCoroutine(CloseRoutine(unload));
    }

    private IEnumerator CloseRoutine(System.Action unload) {
        _phase = Phase.Closing;
        yield return Fade.Cover();
        // Avatar asli melanjutkan pose karakter preview terakhir (termasuk putaran meja putar).
        var stage = FindAnyObjectByType<CharacterPreviewStage>();
        PlayerController local = PlayerController.Local;
        if (stage && local) local.TeleportTo(subject.position, Flatten(stage.TurntableRotation));
        _sceneClosed = false;
        unload();
        float deadline = Time.unscaledTime + MaxWaitSeconds;
        while (!_sceneClosed && Time.unscaledTime < deadline) yield return null;
        // Kamera utama masih di pose editor, jadi saat layar dibuka sudut pandangnya sama dengan editor.
        RestoreActors();
        yield return Fade.Reveal();
        // Avatar berjalan keluar bilik ke depan cermin luar sambil kamera meluncur kembali ke kamera gameplay;
        // kontrol baru dikembalikan setelah keduanya selesai. Dinding depan + tirai kembali begitu mulai berjalan.
        RestoreScenery();
        // Terlihat lagi oleh pemain lain, tapi tembus pemain lain + NPC sampai kontrol dikembalikan.
        SetPresence(FittingRoomPresence.PassThrough);
        IgnoreCharacterCollisions(local);
        StartExitWalk(local);
        yield return Glide(false);
        while (_aligning) yield return null; // WalkPath punya batas waktu sendiri
        FinishClose();
    }

    private void StartExitWalk(PlayerController player) {
        if (!player || exitPath == null) return;
        var points = new List<Vector3>(exitPath.Length);
        Transform last = null;
        foreach (Transform point in exitPath) {
            if (!point) continue;
            points.Add(point.position);
            last = point;
        }
        if (last) StartCoroutine(WalkPath(player, points, last.rotation, MaxExitSeconds));
    }

    private void OnClosed() {
        if (!this) return;
        _sceneClosed = true;
    }

    private void FinishClose() {
        RestoreScenery();
        RestoreCollisions();
        SetPresence(FittingRoomPresence.None);
        SetHudHidden(false);
        SetInputLocked(false);
        _phase = Phase.Idle;
        if (!_interactable) return;
        _interactable.enabled = true;
        // Pemain masih berdiri di lingkaran: trigger tidak masuk ulang, jadi daftarkan lagi tombol "Ganti Baju".
        PlayerController local = PlayerController.Local;
        var zone = GetComponent<Collider>();
        if (local && zone && InteractButtonUI.Instance && zone.bounds.Contains(local.transform.position))
            InteractButtonUI.Instance.Register(_interactable);
    }

    /// Avatar berjalan (animasi jalan biasa) melewati <paramref name="points"/> berurutan, lalu berputar ke
    /// <paramref name="facing"/>. Pemilik avatar yang menggerakkannya, jadi pemain lain melihat gerakan yang sama
    /// lewat sinkron biasa. Berhenti paksa setelah <paramref name="maxSeconds"/> supaya tidak macet di halangan.
    private IEnumerator WalkPath(PlayerController player, IReadOnlyList<Vector3> points, Quaternion facing,
                                 float maxSeconds) {
        if (!player) yield break;
        _aligning = true;
        float deadline = Time.unscaledTime + maxSeconds;
        for (int i = 0; i < points.Count && player && Time.unscaledTime < deadline; i++) {
            float arrive = i == points.Count - 1 ? ArriveDistance : WaypointPassDistance;
            while (player && Time.unscaledTime < deadline) {
                Vector3 offset = points[i] - player.transform.position;
                offset.y = 0f;
                if (offset.magnitude <= arrive) break;
                player.SetScriptedMove(offset.normalized);
                yield return null;
            }
        }
        if (player) player.SetScriptedMove(null);

        Quaternion endRotation = Flatten(facing);
        while (player && Time.unscaledTime < deadline && Quaternion.Angle(player.transform.rotation, endRotation) > 1f) {
            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, endRotation,
                                                                 AlignTurnSpeed * Time.deltaTime);
            yield return null;
        }
        if (player) player.transform.rotation = endRotation;
        _aligning = false;
    }

    private void StopAligning() {
        if (!_aligning) return;
        _aligning = false;
        if (PlayerController.Local) PlayerController.Local.SetScriptedMove(null);
    }

    private static Quaternion Flatten(Quaternion rotation) {
        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
    }

    /// Kamera meluncur ke pose editor (<paramref name="toEditor"/>) atau kembali ke kamera gameplay.
    private IEnumerator Glide(bool toEditor) {
        if (!_orbit) yield break;
        if (toEditor) _orbit.BlendToPose(new Pose(viewpoint.position, viewpoint.rotation), fieldOfView, glideSeconds);
        else _orbit.ReleasePose(glideSeconds);
        while (_orbit && !_orbit.PoseBlendSettled) yield return null;
    }

    void LateUpdate() {
        if (_phase != Phase.Opening && _phase != Phase.Active) return;
        // Avatar sendiri bisa berganti renderer (hasil bake baru tiba setelah SIMPAN): sembunyikan lagi.
        // Selama luncuran avatar asli tetap terlihat; ditukar dengan karakter preview di balik layar hitam.
        if (_phase == Phase.Active && PlayerController.Local) HidePlayer(PlayerController.Local);
        if (Time.unscaledTime < _nextPlayerScan) return;
        _nextPlayerScan = Time.unscaledTime + PlayerScanSeconds;
        _players = FindObjectsByType<PlayerController>();
        foreach (PlayerController player in _players) {
            if (player && player != PlayerController.Local && InClearZone(player.transform.position)) HidePlayer(player);
        }
    }

    // ---------------------------------------------------------------- Jaringan + tabrakan

    /// Status ruang ganti avatar lokal untuk pemain lain (VenueVisibilityFilter di perangkat mereka).
    private static void SetPresence(FittingRoomPresence presence) {
        PlayerController local = PlayerController.Local;
        if (local && local.TryGetComponent(out PlayerVenueSync sync)) sync.SetFittingRoom(presence);
    }

    /// Avatar lokal tembus pemain lain dan NPC (di layar ini) selama berjalan keluar bilik; tembok/lantai tetap padat.
    /// Pemain dan NPC tidak punya layer sendiri, jadi diabaikan per pasangan collider.
    private void IgnoreCharacterCollisions(PlayerController local) {
        RestoreCollisions();
        if (!local) return;
        Collider[] mine = local.GetComponentsInChildren<Collider>(true);
        foreach (PlayerController player in FindObjectsByType<PlayerController>()) {
            if (player && player != local) IgnoreAgainst(mine, player);
        }
        foreach (NpcAgent npc in FindObjectsByType<NpcAgent>()) {
            if (npc) IgnoreAgainst(mine, npc);
        }
    }

    private void IgnoreAgainst(Collider[] mine, Component other) {
        other.GetComponentsInChildren(true, _colliderBuffer);
        foreach (Collider theirs in _colliderBuffer) {
            if (!theirs || theirs.isTrigger) continue;
            foreach (Collider own in mine) {
                if (!own || own.isTrigger) continue;
                Physics.IgnoreCollision(own, theirs, true);
                _ignoredPairs.Add((own, theirs));
            }
        }
    }

    private void RestoreCollisions() {
        foreach ((Collider mine, Collider other) in _ignoredPairs) {
            if (mine && other) Physics.IgnoreCollision(mine, other, false);
        }
        _ignoredPairs.Clear();
    }

    // ---------------------------------------------------------------- Sembunyikan / kembalikan

    private void HideScenery() {
        foreach (Renderer renderer in hideWhileEditing) HideRenderer(renderer, _hiddenScenery);
    }

    private void HidePlayer(PlayerController player) {
        player.GetComponentsInChildren(true, _rendererBuffer);
        foreach (Renderer renderer in _rendererBuffer) HideRenderer(renderer, _hiddenRenderers);
        player.GetComponentsInChildren(true, _canvasBuffer);
        foreach (Canvas canvas in _canvasBuffer) HideCanvas(canvas); // nama di atas kepala
    }

    private static void HideRenderer(Renderer renderer, HashSet<Renderer> into) {
        if (!renderer || renderer.forceRenderingOff || !into.Add(renderer)) return;
        renderer.forceRenderingOff = true;
    }

    private void HideCanvas(Canvas canvas) {
        if (!canvas || !canvas.enabled) return;
        canvas.enabled = false;
        _hiddenCanvases.Add(canvas);
    }

    /// HUD layar (minimap, koin, Quest, Hadiah, peta, mic, chat, joystick, tombol interaksi). Skrip HUD menyalakan
    /// canvas-nya sendiri tiap frame, jadi disembunyikan lewat flag/API yang mereka cek, bukan mematikan canvas.
    private void SetHudHidden(bool hidden) {
        if (hidden == _hudHidden) return;
        _hudHidden = hidden;
        GameplayHud.SetHidden(hidden);
        MobileControls.SetVisible(!hidden);
        ChatPanel.SetSuppressed(hidden);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(hidden);
    }

    /// Avatar sendiri + pemain lain + nama di atas kepala tampil lagi, kamera utama merender dunia lagi.
    private void RestoreActors() {
        RestoreRenderers(_hiddenRenderers);
        foreach (Canvas canvas in _hiddenCanvases) {
            if (canvas) canvas.enabled = true;
        }
        _hiddenCanvases.Clear();
        if (_mainCamera) _mainCamera.cullingMask = _mainCullingMask;
        _mainCamera = null;
    }

    private void RestoreScenery() => RestoreRenderers(_hiddenScenery);

    private static void RestoreRenderers(HashSet<Renderer> set) {
        foreach (Renderer renderer in set) {
            if (renderer) renderer.forceRenderingOff = false;
        }
        set.Clear();
    }

    private void SetInputLocked(bool locked) {
        if (locked == _lockedInput || PlayerInputHub.Instance == null) return;
        _lockedInput = locked;
        PlayerInputHub.Instance.SetLocked(locked);
    }

    /// Kotak dari kamera sampai sedikit di belakang karakter: pemain lain di sini menghalangi pandangan.
    private bool InClearZone(Vector3 point) {
        Vector3 from = viewpoint.position;
        Vector3 to = subject.position;
        Vector3 flat = to - from;
        flat.y = 0f;
        float length = flat.magnitude;
        if (length < 0.01f) return false;
        Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(flat / length)) * (point - from);
        float height = point.y - to.y;
        return local.z >= -ClearMargin && local.z <= length + ClearMargin && Mathf.Abs(local.x) <= ClearHalfWidth &&
               height >= -0.5f && height <= ClearHeight;
    }
}
