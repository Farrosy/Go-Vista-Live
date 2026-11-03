using UnityEngine;

/// <summary>
/// Atur seberapa lama Unity boleh memakai main thread per frame untuk mengintegrasikan scene yang
/// di-load async. Low = dicicil ~2 ms per frame: load sedikit lebih lama, tapi tidak ada frame yang
/// macet berdetik-detik. Penting karena Normcore memproses jaringan di main thread; frame yang macet
/// lama membuat server menganggap client hilang ("ConnectionFailedWithNetworkError").
/// Dipakai saat load WorldScene (LobyUISceneController) dan scene venue (VenueTravelManager).
/// </summary>
public static class SceneLoadPriority {
    private static int s_users;
    private static ThreadPriority s_previous = ThreadPriority.Normal;

    public static void UseLow() {
        if (s_users == 0) {
            s_previous = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
        }
        s_users++;
    }

    public static void Restore() {
        if (s_users == 0)
            return;
        s_users--;
        if (s_users == 0)
            Application.backgroundLoadingPriority = s_previous;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        s_users = 0;
        s_previous = ThreadPriority.Normal;
    }
}
