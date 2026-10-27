using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // VSync mati di Quality Mobile, jadi frame rate dikunci manual.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60; // pakai 30 kalau HP target kelas low-end

        // Layar tidak mati saat pemain diam (misal lagi baca dialog).
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}