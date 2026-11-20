using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ruang ganti di clothing shop: pemain berjalan masuk ke bilik, berdiri di lingkaran cahaya, lalu menekan
/// "Ganti Baju" (Interactable di objek ini). Editor karakter (scene KARAKTER, tab khusus ruang ganti) dibuka
/// dengan karakter berdiri di bilik ini dan kamera di toko, jadi latarnya toko asli, bukan warna polos.
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

    [Tooltip("Titik lantai tempat karakter preview berdiri; sumbu biru (forward) menghadap ke kamera.")]
    [SerializeField] private Transform subject;
    [Tooltip("Pose kamera editor (di toko, di depan bilik).")]
    [SerializeField] private Transform viewpoint;
    [SerializeField] private float fieldOfView = 33f;
    [Tooltip("Disembunyikan selama editor terbuka (dinding depan bilik + tirai), hanya di layar pemain ini.")]
    [SerializeField] private Renderer[] hideWhileEditing = new Renderer[0];

    private Interactable _interactable;
    private bool _opening;
    private bool _active;
    private bool _lockedInput;
    private bool _hudHidden;
    private Camera _mainCamera;
    private int _mainCullingMask;
    private float _nextPlayerScan;
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    private readonly HashSet<Renderer> _hiddenRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
    private readonly List<Canvas> _canvasBuffer = new List<Canvas>();
    private PlayerController[] _players = new PlayerController[0];

    void Awake() {
        _interactable = GetComponent<Interactable>();
        _interactable.onInteract.AddListener(Open);
    }

    void OnDestroy() {
        if (_interactable) _interactable.onInteract.RemoveListener(Open);
        // Interior di-unload saat editor masih terbuka (mis. keluar room): tutup editornya juga.
        if (_opening || _active) {
            Restore();
            CharacterSceneLoader.Close();
        }
    }

    private void Open() {
        if (_opening || _active || CharacterSceneLoader.IsOpen || !subject || !viewpoint || !PlayerController.Local) return;
        Camera main = Camera.main;
        var view = new FittingRoomView(new Pose(subject.position, subject.rotation),
                                       new Pose(viewpoint.position, viewpoint.rotation), fieldOfView,
                                       main ? main.cullingMask : ~0);
        _opening = true;
        SetInputLocked(true);
        _interactable.enabled = false; // prompt + tombol Interact hilang selama editor terbuka
        CharacterSceneLoader.OpenFittingRoom(view, OnOpened, OnClosed);
        if (!CharacterSceneLoader.IsOpen && CharacterSceneLoader.FittingRoom == null) {
            // Scene tidak bisa dimuat (lihat Console): jangan biarkan pemain terkunci.
            _opening = false;
            SetInputLocked(false);
            _interactable.enabled = true;
        }
    }

    private void OnOpened() {
        if (!this) return; // interior sudah di-unload saat scene masih dimuat (loader langsung menutupnya)
        _opening = false;
        _active = true;
        SetHudHidden(true);
        // Editor merender toko dengan kameranya sendiri; kamera utama tidak perlu merender dunia dua kali.
        _mainCamera = Camera.main;
        if (_mainCamera) {
            _mainCullingMask = _mainCamera.cullingMask;
            _mainCamera.cullingMask = 0;
        }
        _nextPlayerScan = 0f;
        HideScenery();
    }

    private void OnClosed() {
        Restore();
        if (!_interactable) return;
        _interactable.enabled = true;
        // Pemain masih berdiri di lingkaran: trigger tidak masuk ulang, jadi daftarkan lagi tombol "Ganti Baju".
        PlayerController local = PlayerController.Local;
        var zone = GetComponent<Collider>();
        if (local && zone && InteractButtonUI.Instance && zone.bounds.Contains(local.transform.position))
            InteractButtonUI.Instance.Register(_interactable);
    }

    void LateUpdate() {
        if (!_active) return;
        // Avatar sendiri bisa berganti renderer (hasil bake baru tiba setelah SIMPAN): sembunyikan lagi.
        if (PlayerController.Local) HidePlayer(PlayerController.Local);
        if (Time.unscaledTime < _nextPlayerScan) return;
        _nextPlayerScan = Time.unscaledTime + PlayerScanSeconds;
        _players = FindObjectsByType<PlayerController>();
        foreach (PlayerController player in _players) {
            if (player && player != PlayerController.Local && InClearZone(player.transform.position)) HidePlayer(player);
        }
    }

    // ---------------------------------------------------------------- Sembunyikan / kembalikan

    private void HideScenery() {
        foreach (Renderer renderer in hideWhileEditing) HideRenderer(renderer);
    }

    private void HidePlayer(PlayerController player) {
        player.GetComponentsInChildren(true, _rendererBuffer);
        foreach (Renderer renderer in _rendererBuffer) HideRenderer(renderer);
        player.GetComponentsInChildren(true, _canvasBuffer);
        foreach (Canvas canvas in _canvasBuffer) HideCanvas(canvas); // nama di atas kepala
    }

    private void HideRenderer(Renderer renderer) {
        if (!renderer || renderer.forceRenderingOff || !_hiddenRenderers.Add(renderer)) return;
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

    private void Restore() {
        _opening = false;
        _active = false;
        foreach (Renderer renderer in _hiddenRenderers) {
            if (renderer) renderer.forceRenderingOff = false;
        }
        _hiddenRenderers.Clear();
        foreach (Canvas canvas in _hiddenCanvases) {
            if (canvas) canvas.enabled = true;
        }
        _hiddenCanvases.Clear();
        SetHudHidden(false);
        if (_mainCamera) _mainCamera.cullingMask = _mainCullingMask;
        _mainCamera = null;
        SetInputLocked(false);
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
