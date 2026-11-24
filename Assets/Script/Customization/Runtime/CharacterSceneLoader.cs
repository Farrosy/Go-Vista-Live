using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Membuka/menutup scene KARAKTER (Assets/Scenes/Customization/CharacterScene.unity) di atas lobby.
///
/// Scene dimuat ADDITIVE, bukan Single: MainMultiplayerScene membawa Normcore Realtime, chat room,
/// PlayerManager, dan NPC director yang tidak boleh ikut ter-unload. Scene karakter punya Canvas
/// full-screen + kamera preview sendiri, jadi tampil sebagai layar penuh, bukan overlay kartu.
/// Scene ini sengaja tanpa EventSystem (lobby sudah punya satu).
/// </summary>
public static class CharacterSceneLoader {
    public const string SceneName = "CharacterScene";
    public const string ScenePath = "Assets/Scenes/Customization/CharacterScene.unity";

    private static Action _onOpened;
    private static Action _onClosed;
    private static bool _busy;
    // Close() dipanggil saat scene masih dimuat: tutup begitu selesai dimuat (tanpa memanggil "opened").
    private static bool _loading;
    private static bool _closeWhenLoaded;
    // Ruang ganti: dipanggil sebelum unload (mis. fade layar); scene baru di-unload saat lanjutannya dipanggil.
    private static Action<Action> _beforeUnload;
    private static bool _closeDeferred;

    public static bool IsOpen => SceneManager.GetSceneByName(SceneName).isLoaded;

    /// <summary>
    /// Scene dibuka dari ruang ganti di toko (bukan dari lobby): langsung ke editor karakter aktif, karakter
    /// berdiri di bilik itu. null = dibuka dari tile KARAKTER di lobby.
    /// </summary>
    public static FittingRoomView FittingRoom { get; private set; }

    /// <summary>
    /// Dipicu saat penampilan disimpan di ruang ganti dan berbeda dari penampilan waktu editor dibuka
    /// (SIMPAN tanpa perubahan tidak dihitung). Dipakai FittingRoomStation untuk tujuan quest "ganti gaya".
    /// </summary>
    public static event Action FittingRoomLookChanged;

    public static void NotifyFittingRoomLookChanged() => FittingRoomLookChanged?.Invoke();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        _onOpened = null;
        _onClosed = null;
        _busy = false;
        _loading = false;
        _closeWhenLoaded = false;
        _beforeUnload = null;
        _closeDeferred = false;
        FittingRoom = null;
    }

    /// <summary>Buka editor ruang ganti: karakter tampil di <paramref name="view"/>, bukan di panggung lobby.</summary>
    /// <param name="beforeUnload">Opsional: dipanggil saat editor minta ditutup, SEBELUM scene di-unload (mis. layar
    /// di-fade dulu). Panggil Action yang diberikan untuk melanjutkan unload.</param>
    public static void OpenFittingRoom(FittingRoomView view, Action opened, Action closed,
                                       Action<Action> beforeUnload = null) {
        if (_busy || IsOpen || view == null) return;
        FittingRoom = view;
        Open(opened, () => {
            FittingRoom = null;
            closed?.Invoke();
        });
        if (!_busy) {
            FittingRoom = null; // gagal dimuat (scene tidak terdaftar)
            return;
        }
        _beforeUnload = beforeUnload;
    }

    /// Tutup tanpa menunggu <c>beforeUnload</c> (pemiliknya sudah hilang, mis. interior di-unload).
    public static void CloseImmediate() {
        _beforeUnload = null;
        _closeDeferred = false;
        Close();
    }

    /// <param name="opened">Dipanggil setelah scene selesai dimuat (sembunyikan menu lobby di sini).</param>
    /// <param name="closed">Dipanggil setelah scene di-unload (tampilkan lagi menu lobby).</param>
    public static void Open(Action opened, Action closed) {
        if (_busy || IsOpen) return;
        AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        if (load == null) {
            Debug.LogError($"[CharacterScene] Scene \"{SceneName}\" tidak ada di Build Profiles. " +
                           "Jalankan Go Vista > Customization > Buat CharacterScene.");
            return;
        }
        _busy = true;
        _loading = true;
        _onOpened = opened;
        _onClosed = closed;
        load.completed += _ => {
            _busy = false;
            _loading = false;
            if (_closeWhenLoaded) {
                _closeWhenLoaded = false;
                _onOpened = null;
                Close();
                return;
            }
            Action callback = _onOpened;
            _onOpened = null;
            callback?.Invoke();
        };
    }

    /// <summary>Dipanggil tombol KEMBALI / Back Android di scene karakter.</summary>
    public static void Close() {
        if (_loading) {
            _closeWhenLoaded = true;
            return;
        }
        Scene scene = SceneManager.GetSceneByName(SceneName);
        if (_busy || _closeDeferred || !scene.isLoaded) return;
        if (SceneManager.sceneCount <= 1) {
            Debug.Log("[CharacterScene] Dibuka sendiri (tanpa lobby): tidak ada scene untuk kembali.");
            return;
        }
        if (_beforeUnload != null) {
            Action<Action> hook = _beforeUnload;
            _beforeUnload = null;
            _closeDeferred = true; // tombol Kembali yang ditekan lagi selama fade diabaikan
            hook(() => {
                _closeDeferred = false;
                Close();
            });
            return;
        }
        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
        if (unload == null) return;
        _busy = true;
        unload.completed += _ => {
            _busy = false;
            Action callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        };
    }
}
