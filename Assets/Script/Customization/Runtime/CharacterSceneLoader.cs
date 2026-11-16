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

    public static bool IsOpen => SceneManager.GetSceneByName(SceneName).isLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        _onOpened = null;
        _onClosed = null;
        _busy = false;
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
        _onOpened = opened;
        _onClosed = closed;
        load.completed += _ => {
            _busy = false;
            Action callback = _onOpened;
            _onOpened = null;
            callback?.Invoke();
        };
    }

    /// <summary>Dipanggil tombol KEMBALI / Back Android di scene karakter.</summary>
    public static void Close() {
        Scene scene = SceneManager.GetSceneByName(SceneName);
        if (_busy || !scene.isLoaded) return;
        if (SceneManager.sceneCount <= 1) {
            Debug.Log("[CharacterScene] Dibuka sendiri (tanpa lobby): tidak ada scene untuk kembali.");
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
