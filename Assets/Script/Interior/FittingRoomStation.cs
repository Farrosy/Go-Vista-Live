using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ruang ganti di clothing shop: pemain berjalan masuk ke bilik, berdiri di lingkaran cahaya, lalu menekan
/// "Ganti Baju" (Interactable di objek ini). Editor karakter (scene KARAKTER, tab khusus ruang ganti) dibuka
/// dengan karakter berdiri di bilik ini dan kamera di toko, jadi latarnya toko asli, bukan warna polos.
///
/// Transisi: kamera gameplay meluncur halus ke pose kamera editor, layar fade singkat menutupi momen scene
/// dimuat dan karakter preview di-bake, lalu editor muncul. Saat ditutup urutannya dibalik (fade, unload,
/// buka layar, kamera meluncur kembali), baru HUD dan kontrol dikembalikan.
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

    [Header("Syarat")]
    [Tooltip("Ruang ganti terkunci sampai pemain lokal menyelesaikan dialog ini (perkenalan kasir). Kosong = bebas.")]
    [SerializeField] private string requiredDialogueId = "mira_intro";
    [SerializeField] private string lockedTitle = "Ruang ganti belum bisa dipakai";
    [SerializeField] private string lockedDetail = "Ngobrol dulu dengan Mira di kasir, ya!";

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

    void Awake() {
        _interactable = GetComponent<Interactable>();
        _interactable.onInteract.AddListener(Open);
    }

    void OnDestroy() {
        if (_interactable) _interactable.onInteract.RemoveListener(Open);
        if (_phase == Phase.Idle) return;
        // Interior di-unload di tengah transisi / saat editor terbuka (mis. keluar room): kembalikan semuanya
        // seketika. Overlay fade milik objek ini, jadi ikut hilang.
        StopAligning();
        RestoreActors();
        RestoreScenery();
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
        HideScenery(); // dinding depan hilang sejak awal luncuran, jadi kamera melihat bilik
        _nextPlayerScan = 0f;
        StartCoroutine(OpenRoutine(view));
    }

    private IEnumerator OpenRoutine(FittingRoomView view) {
        // Sambil kamera meluncur, avatar asli merapikan posisinya ke titik karakter preview (menghadap kamera),
        // jadi pergantian ke karakter preview di balik layar hitam tidak terlihat melompat.
        StartCoroutine(AlignPlayer(PlayerController.Local, new Pose(subject.position, subject.rotation)));
        yield return Glide(true);
        while (_aligning) yield return null; // AlignPlayer punya batas waktu sendiri
        yield return Fade.Cover();

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
        yield return Glide(false);
        FinishClose();
    }

    private void OnClosed() {
        if (!this) return;
        _sceneClosed = true;
    }

    private void FinishClose() {
        RestoreScenery();
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

    /// Avatar berjalan (animasi jalan biasa) ke titik karakter preview, lalu berbalik menghadap kamera editor.
    /// Pemilik avatar yang menggerakkannya, jadi pemain lain melihat gerakan yang sama lewat sinkron biasa.
    private IEnumerator AlignPlayer(PlayerController player, Pose target) {
        if (!player) yield break;
        _aligning = true;
        float deadline = Time.unscaledTime + MaxAlignSeconds;
        Vector3 goal = target.position;
        while (player && Time.unscaledTime < deadline) {
            Vector3 offset = goal - player.transform.position;
            offset.y = 0f;
            if (offset.magnitude <= ArriveDistance) break;
            player.SetScriptedMove(offset.normalized);
            yield return null;
        }
        if (player) player.SetScriptedMove(null);

        Quaternion endRotation = Flatten(target.rotation);
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
