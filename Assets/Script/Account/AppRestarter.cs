using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Memulai ulang game. Android: membuka ulang activity utama lalu mematikan proses lama (game terbuka lagi
/// sendiri). Editor: Play Mode dihentikan. Platform lain: game ditutup (pemain membukanya lagi).
/// </summary>
public static class AppRestarter {
#if UNITY_ANDROID && !UNITY_EDITOR
    private const int FlagActivityNewTask = 0x10000000;
    private const int FlagActivityClearTask = 0x00008000;
#endif

    /// <summary>true kalau platform ini bisa membuka ulang game sendiri (bukan hanya menutup).</summary>
    public static bool CanRelaunch {
        get {
#if UNITY_ANDROID || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static void Restart() {
        PlayerPrefs.Save(); // sesi login akun baru harus tersimpan sebelum proses dimatikan
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
        try {
            RelaunchAndroid();
        } catch (Exception e) {
            Debug.LogException(e);
            Application.Quit();
        }
#else
        Application.Quit();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void RelaunchAndroid() {
        AndroidJavaObject activity = AndroidApplication.currentActivity;
        string packageName = activity.Call<string>("getPackageName");
        using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
        using (AndroidJavaObject intent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", packageName)) {
            intent.Call<AndroidJavaObject>("addFlags", FlagActivityNewTask | FlagActivityClearTask);
            activity.Call("startActivity", intent);
        }
        using (var process = new AndroidJavaClass("android.os.Process")) {
            process.CallStatic("killProcess", process.CallStatic<int>("myPid"));
        }
    }
#endif
}
