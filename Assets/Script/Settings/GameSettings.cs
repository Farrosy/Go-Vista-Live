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
/// <summary>Tipe kontrol belok saat menyetir mobil.</summary>
public enum DriveControlMode {
    /// <summary>Roda setir yang diputar dengan jari.</summary>
    SteeringWheel = 0,
    /// <summary>Dua tombol: belok kiri dan belok kanan.</summary>
    Buttons = 1,
}

public static class GameSettings {
    public const float MinCameraSensitivity = 0.5f;
    public const float MaxCameraSensitivity = 2f;

    private const string CameraSensitivityKey = "Settings.CameraSensitivity";
    private const string DriveControlKey = "Settings.DriveControl";

    // Di-cache karena CameraOrbit membaca sensitivitas setiap frame (PlayerPrefs di Android cukup mahal).
    private static float? _cameraSensitivity;
    private static DriveControlMode? _driveControl;

    /// <summary>Dipanggil saat tipe kontrol setir diganti (HUD menyetir yang sedang tampil langsung ikut berganti).</summary>
    public static event System.Action DriveControlChanged;

    /// <summary>Tipe kontrol belok mobil. Default: roda setir.</summary>
    public static DriveControlMode DriveControl {
        get => _driveControl ??= ParseDriveControl(PlayerPrefs.GetInt(DriveControlKey, (int)DriveControlMode.SteeringWheel));
        set {
            if (_driveControl == value) return;
            _driveControl = value;
            PlayerPrefs.SetInt(DriveControlKey, (int)value);
            DriveControlChanged?.Invoke();
        }
    }

    private static DriveControlMode ParseDriveControl(int value) =>
        value == (int)DriveControlMode.Buttons ? DriveControlMode.Buttons : DriveControlMode.SteeringWheel;

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
        _driveControl = null;
        DriveControlChanged = null;
    }
}
