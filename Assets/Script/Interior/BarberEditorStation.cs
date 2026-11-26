using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor rambut + jenggot di kursi barber. Selama avatar lokal duduk diam (<see cref="ChairSeat.Seated"/>), tombol
/// HUD "Gaya Rambut" muncul di samping tombol Interact (Berdiri). Menekannya:
///  1. kamera kursi meluncur ke bidikan editor (A yang di-zoom, <see cref="ChairSeatCamera.SetEditorShot"/>), siklus
///     bidikan diam berhenti;
///  2. layar fade singkat menutupi momen scene KARAKTER dimuat dan karakter preview dirakit;
///  3. editor (tab Rambut + Jenggot, EditorCategories.Barber) tampil dengan karakter preview DUDUK di kursi ini,
///     dilihat dari pose kamera yang sama, jadi tidak ada lompatan sudut pandang.
/// SIMPAN menyimpan ke slot karakter custom seperti ruang ganti (template → slot baru; slot penuh → pilih yang
/// ditimpa), slot itu langsung dipakai, dan pemain lain menerima penampilan baru (PlayerAppearanceSync).
/// Menutup editor (SIMPAN lalu KEMBALI, atau KEMBALI / Back) membalik urutannya: fade, unload, kamera meluncur
/// kembali ke A dan siklus berlanjut. Avatar TETAP duduk; Berdiri dipakai setelah editor ditutup.
///
/// Selama editor terbuka, hanya di layar pemain ini: HUD + chat + tombol Interact disembunyikan, input dikunci,
/// avatar asli disembunyikan (diganti karakter preview), kamera utama tidak merender dunia (kamera preview yang
/// merender). Interior di-unload saat editor terbuka = semua dikembalikan seketika.
/// Dipasang oleh Go Vista > Interior > Pasang Kursi Barber (BarberChairBuilder) di objek yang sama dengan ChairSeat.
/// </summary>
[RequireComponent(typeof(ChairSeat))]
public sealed class BarberEditorStation : MonoBehaviour {
    // Batas tunggu (detik) kamera, scene dimuat/di-unload, dan karakter preview selesai dirakit: layar tidak macet hitam.
    private const float MaxGlideSeconds = 2f;
    private const float MaxWaitSeconds = 6f;

    private enum Phase { Idle, Opening, Active, Closing }

    [SerializeField] private string buttonLabel = "Gaya Rambut";
    [Tooltip("Ikon tombol HUD (Resources/UI/Icons, tanpa ekstensi).")]
    [SerializeField] private string buttonIcon = "icon-hair";

    private ChairSeat _chair;
    private ChairSeatCamera _chairCamera;
    private HudActionButton _button;
    private Phase _phase = Phase.Idle;
    private bool _sceneOpened;
    private bool _sceneClosed;
    private bool _lockedInput;
    private bool _hudHidden;
    private FadeToBlackTransition _fade;
    private Camera _mainCamera;
    private int _mainCullingMask;
    private readonly HashSet<Renderer> _hiddenRenderers = new HashSet<Renderer>();
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
    private readonly List<Canvas> _canvasBuffer = new List<Canvas>();

    /// True dari tekan "Gaya Rambut" sampai editor selesai ditutup.
    public bool IsEditing => _phase != Phase.Idle;

    private FadeToBlackTransition Fade {
        get {
            if (!_fade) _fade = GetComponent<FadeToBlackTransition>();
            if (!_fade) _fade = gameObject.AddComponent<FadeToBlackTransition>();
            return _fade;
        }
    }

    void Awake() {
        _chair = GetComponent<ChairSeat>();
        _chairCamera = GetComponent<ChairSeatCamera>();
    }

    void OnEnable() {
        _chair.Seated += OnSeated;
        _chair.StandingUp += OnLeaving;
        _chair.Left += OnLeaving;
    }

    void OnDisable() {
        _chair.Seated -= OnSeated;
        _chair.StandingUp -= OnLeaving;
        _chair.Left -= OnLeaving;
        if (_button) Destroy(_button.gameObject);
        _button = null;
        RestoreImmediate();
    }

    // ---------------------------------------------------------------- Tombol HUD

    private void OnSeated() => SetButtonShown(true);

    private void OnLeaving() {
        SetButtonShown(false);
        RestoreImmediate(); // tidak terjadi lewat UI (Interact tersembunyi selama editor), hanya jaring pengaman
    }

    private void SetButtonShown(bool shown) {
        if (shown && !_button)
            _button = HudActionButton.Create("Barber Editor Button", ProfileMenuBuilder.LoadIcon(buttonIcon), buttonLabel, Open);
        if (_button) _button.Shown = shown;
    }

    // ---------------------------------------------------------------- Buka

    private void Open() {
        if (_phase != Phase.Idle || !_chair.IsLocalSeated || CharacterSceneLoader.IsOpen || !PlayerController.Local ||
            !_chairCamera || !_chairCamera.IsActive)
            return;
        _phase = Phase.Opening;
        SetButtonShown(false);
        SetInputLocked(true);
        SetHudHidden(true);
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine() {
        _chairCamera.SetEditorShot(true);
        float glideDeadline = Time.unscaledTime + MaxGlideSeconds;
        while (_chairCamera && !_chairCamera.EditorShotSettled && Time.unscaledTime < glideDeadline) yield return null;
        yield return Fade.Cover();

        FittingRoomView view = BuildView();
        _sceneOpened = false;
        _sceneClosed = false;
        if (view != null) CharacterSceneLoader.OpenFittingRoom(view, OnOpened, OnClosed, BeforeUnload);
        if (view == null || (CharacterSceneLoader.FittingRoom == null && !CharacterSceneLoader.IsOpen)) {
            // Scene tidak bisa dimuat (lihat Console): kembalikan pemain, jangan biarkan terkunci.
            yield return Fade.Reveal();
            yield return GlideBack();
            FinishClose();
            yield break;
        }

        float deadline = Time.unscaledTime + MaxWaitSeconds;
        while (!_sceneOpened && Time.unscaledTime < deadline) yield return null;
        yield return null; // beri editor satu frame untuk mulai merakit karakter preview
        var stage = FindAnyObjectByType<CharacterPreviewStage>();
        while (stage && stage.IsBusy && Time.unscaledTime < deadline) yield return null;
        yield return null; // pose duduk + IK kaki sudah dievaluasi sekali sebelum layar dibuka
        yield return Fade.Reveal();
    }

    /// Karakter preview menempati pose model avatar asli yang sedang duduk; kamera = bidikan editor kursi.
    private FittingRoomView BuildView() {
        PlayerController local = PlayerController.Local;
        if (!local || !_chairCamera || !_chairCamera.TryGetEditorPose(out Pose view, out float fov)) return null;
        Transform model = local.Animator ? local.Animator.transform : local.transform;
        Camera main = Camera.main;
        return new FittingRoomView(new Pose(model.position, YawOnly(model.rotation)), view, fov,
                                   main ? main.cullingMask : ~0, WorldEditorKind.Barber);
    }

    private void OnOpened() {
        if (!this) return; // interior sudah di-unload saat scene masih dimuat
        _sceneOpened = true;
        _phase = Phase.Active;
        // Kamera preview merender kursi; kamera utama tidak perlu merender dunia dua kali.
        _mainCamera = Camera.main;
        if (_mainCamera) {
            _mainCullingMask = _mainCamera.cullingMask;
            _mainCamera.cullingMask = 0;
        }
    }

    void LateUpdate() {
        // Avatar sendiri bisa berganti renderer (hasil bake baru tiba setelah SIMPAN): sembunyikan lagi.
        if (_phase == Phase.Active && PlayerController.Local) HidePlayer(PlayerController.Local);
    }

    // ---------------------------------------------------------------- Tutup

    /// Editor minta ditutup (KEMBALI / Back): tutup layar dulu, baru scene di-unload.
    private void BeforeUnload(System.Action unload) {
        if (!this) {
            unload();
            return;
        }
        StopAllCoroutines(); // bisa terjadi saat layar masih tertutup menunggu preview
        StartCoroutine(CloseRoutine(unload));
    }

    private IEnumerator CloseRoutine(System.Action unload) {
        _phase = Phase.Closing;
        yield return Fade.Cover();
        _sceneClosed = false;
        unload();
        float deadline = Time.unscaledTime + MaxWaitSeconds;
        while (!_sceneClosed && Time.unscaledTime < deadline) yield return null;
        // Kamera utama masih di bidikan editor, jadi saat layar dibuka sudut pandangnya sama dengan editor.
        RestoreActors();
        yield return Fade.Reveal();
        yield return GlideBack();
        FinishClose();
    }

    private void OnClosed() {
        if (!this) return;
        _sceneClosed = true;
    }

    /// Kamera meluncur dari bidikan editor kembali ke A; siklus bidikan diam berlanjut sendiri.
    private IEnumerator GlideBack() {
        if (!_chairCamera) yield break;
        _chairCamera.SetEditorShot(false);
        float deadline = Time.unscaledTime + MaxGlideSeconds;
        while (_chairCamera && !_chairCamera.EditorShotReleased && Time.unscaledTime < deadline) yield return null;
    }

    private void FinishClose() {
        SetHudHidden(false);
        SetInputLocked(false);
        _phase = Phase.Idle;
        SetButtonShown(_chair && _chair.IsLocalSeated);
    }

    /// Interior di-unload / kursi dilepas di tengah transisi atau saat editor terbuka: kembalikan semuanya seketika.
    private void RestoreImmediate() {
        if (_phase == Phase.Idle) return;
        StopAllCoroutines();
        if (_fade) _fade.ResetImmediate(); // objek tetap ada (mis. kursi dilepas): layar jangan tertinggal hitam
        RestoreActors();
        if (_chairCamera) _chairCamera.SetEditorShot(false);
        SetHudHidden(false);
        SetInputLocked(false);
        _phase = Phase.Idle;
        CharacterSceneLoader.CloseImmediate();
    }

    // ---------------------------------------------------------------- Sembunyikan / kembalikan

    private void HidePlayer(PlayerController player) {
        player.GetComponentsInChildren(true, _rendererBuffer);
        foreach (Renderer renderer in _rendererBuffer) {
            if (!renderer || renderer.forceRenderingOff || !_hiddenRenderers.Add(renderer)) continue;
            renderer.forceRenderingOff = true;
        }
        player.GetComponentsInChildren(true, _canvasBuffer);
        foreach (Canvas canvas in _canvasBuffer) { // nama di atas kepala
            if (!canvas || !canvas.enabled) continue;
            canvas.enabled = false;
            _hiddenCanvases.Add(canvas);
        }
    }

    /// Avatar sendiri + nama di atas kepala tampil lagi, kamera utama merender dunia lagi.
    private void RestoreActors() {
        foreach (Renderer renderer in _hiddenRenderers) {
            if (renderer) renderer.forceRenderingOff = false;
        }
        _hiddenRenderers.Clear();
        foreach (Canvas canvas in _hiddenCanvases) {
            if (canvas) canvas.enabled = true;
        }
        _hiddenCanvases.Clear();
        if (_mainCamera) _mainCamera.cullingMask = _mainCullingMask;
        _mainCamera = null;
    }

    /// HUD layar (minimap, koin, Quest, peta, mic, chat, kontrol, tombol Interact + tombol ini). Lihat
    /// FittingRoomStation.SetHudHidden. Status "menyetir" kursi (joystick/lompat/lari tersembunyi) tidak diubah.
    private void SetHudHidden(bool hidden) {
        if (hidden == _hudHidden) return;
        _hudHidden = hidden;
        GameplayHud.SetHidden(hidden);
        MobileControls.SetVisible(!hidden);
        ChatPanel.SetSuppressed(hidden);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(hidden);
    }

    private void SetInputLocked(bool locked) {
        if (locked == _lockedInput || PlayerInputHub.Instance == null) return;
        _lockedInput = locked;
        PlayerInputHub.Instance.SetLocked(locked);
    }

    private static Quaternion YawOnly(Quaternion rotation) => Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
}
