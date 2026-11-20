/// <summary>
/// Permintaan "sembunyikan seluruh HUD gameplay" dari layar penuh yang tidak ikut menu pause, mis. editor
/// ruang ganti. Setiap tampilan HUD (koin + minimap, Quest, Hadiah, peta, mic) mengecek <see cref="Hidden"/> di
/// Update-nya sendiri, sama seperti <see cref="ControlLayoutEditor.IsOpen"/>. Joystick, tombol interaksi, dan
/// chat disembunyikan lewat API-nya masing-masing oleh pemanggil.
/// </summary>
public static class GameplayHud {
    public static bool Hidden { get; private set; }

    public static void SetHidden(bool hidden) => Hidden = hidden;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Hidden = false;
}
