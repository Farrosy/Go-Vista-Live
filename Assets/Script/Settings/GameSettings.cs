using UnityEngine;

/// <summary>
/// Pengaturan pemain yang disimpan per perangkat (PlayerPrefs) dan langsung diterapkan.
/// Saat ini: pengali sensitivitas kamera (dipakai CameraOrbit).
///
/// TODO (next develop): tambahkan MasterVolume (AudioListener.volume / AudioMixer) setelah game punya audio,
/// lalu slider-nya di BuildSettingsOverlay (LobyUISceneController).
/// TODO (next develop): kalau pengaturan harus ikut pindah saat login di HP lain, salin nilainya ke
/// PlayerProgress.settings (lihat TODO di PlayerProgress) setelah ProgressService.Loaded.
/// </summary>
public static class GameSettings {
    public const float MinCameraSensitivity = 0.5f;
    public const float MaxCameraSensitivity = 2f;

    private const string CameraSensitivityKey = "Settings.CameraSensitivity";

    // Di-cache karena CameraOrbit membaca sensitivitas setiap frame (PlayerPrefs di Android cukup mahal).
    private static float? _cameraSensitivity;

    /// <summary>Pengali sensitivitas kamera: 1 = default, 0.5 = lebih pelan, 2 = lebih cepat.</summary>
    public static float CameraSensitivity {
        get => _cameraSensitivity ??= PlayerPrefs.GetFloat(CameraSensitivityKey, 1f);
        set {
            _cameraSensitivity = Mathf.Clamp(value, MinCameraSensitivity, MaxCameraSensitivity);
            PlayerPrefs.SetFloat(CameraSensitivityKey, _cameraSensitivity.Value);
        }
    }

    public static void Save() {
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() {
        _cameraSensitivity = null;
    }
}
