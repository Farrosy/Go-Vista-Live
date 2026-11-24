using UnityEngine;
using UnityEngine.SceneManagement;

/// Normcore membuat objek jaringan (Realtime.Instantiate, termasuk salinan milik pemain lain yang
/// diterima dari server) di scene yang sedang AKTIF di perangkat ini. VenueTravelManager menjadikan
/// scene venue aktif saat travel, jadi avatar/NPC/mobil pemain lain bisa tercipta di WorldScene atau
/// interior, lalu ikut ter-destroy saat scene itu di-unload (mis. host masuk toko: avatar tamu hilang).
///
/// Panggil Adopt dari Awake komponen di root prefab jaringan supaya objeknya selalu tinggal di scene
/// persisten (MainMultiplayerScene, tempat VenueTravelManager berada).
public static class PersistentSceneAdopter
{
    public static void Adopt(GameObject go)
    {
        if (go == null || go.transform.parent != null) return;
        VenueTravelManager manager = VenueTravelManager.Instance;
        if (manager == null) return; // DevScene / offline: tidak ada scene persisten.
        Scene persistent = manager.gameObject.scene;
        if (!persistent.IsValid() || !persistent.isLoaded || go.scene == persistent) return;
        SceneManager.MoveGameObjectToScene(go, persistent);
    }
}
