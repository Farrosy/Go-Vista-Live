using UnityEngine;

/// <summary>
/// Kunci game ke landscape saat runtime. Player Settings sudah mematikan Portrait, tapi nilai itu hanya
/// dibaca saat startup: Device Simulator di Editor menyimpan nilai lama sejak window-nya dibuka, dan
/// plugin bisa mengubah Screen.autorotateTo*. Ini memastikan HUD/lobby (yang hanya didesain landscape)
/// tidak pernah tampil portrait. Landscape kiri ↔ kanan tetap ikut putaran HP.
/// </summary>
public static class LandscapeOrientationLock {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // Kalau sedang portrait, paksa ke landscape dulu; lalu kembali auto-rotate (hanya antar landscape).
        if (Screen.orientation == ScreenOrientation.Portrait ||
            Screen.orientation == ScreenOrientation.PortraitUpsideDown)
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
