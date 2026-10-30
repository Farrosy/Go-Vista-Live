using System;
using System.Collections.Generic;

/// <summary>
/// Semua data progres satu akun. Disimpan ke Unity Cloud Save sebagai JSON di bawah satu key ("progress").
///
/// Aturan menambah field baru:
///  - Beri nilai default. Save lama yang belum punya field itu otomatis memakai nilai default.
///  - Jangan rename/hapus field yang sudah rilis. Tambah field baru, naikkan CurrentVersion,
///    lalu pindahkan datanya di ProgressService.Migrate().
///  - Pakai tipe sederhana: int, float, bool, string, List, atau class [Serializable].
///    JANGAN simpan Vector3/Quaternion langsung (serializer Newtonsoft bisa error), pecah jadi float.
/// </summary>
[Serializable]
public class PlayerProgress {
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;

    // ---------------------------------------------------------------- Dialog
    /// <summary>ID dialog (DialogueData.Id) yang sudah selesai dibaca sampai baris terakhir.</summary>
    public List<string> completedDialogues = new List<string>();

    // ---------------------------------------------------------------- Posisi terakhir (TIDAK DIPAKAI)
    // Dulu dipakai untuk spawn di posisi terakhir. Sekarang pemain selalu spawn di PlayerSpawnPoint,
    // dan ProgressService.Migrate() mengosongkan field ini. Dibiarkan supaya save lama tetap terbaca.
    public bool hasLastPosition;
    /// <summary>Nama world tempat posisi ini berlaku, supaya posisi tidak dipakai di world lain.</summary>
    public string lastWorld = "";
    public float lastPosX, lastPosY, lastPosZ;
    public float lastRotY;

    // =====================================================================
    // NEXT DEVELOP: belum dipakai, tinggal di-uncomment dan dihubungkan.
    // =====================================================================

    // TODO (next develop) – Koin, poin, dan item
    //   public int coins;
    //   public int points;
    //   public List<InventoryItem> items = new List<InventoryItem>();
    //   // [Serializable] public class InventoryItem { public string itemId; public int amount; }
    //
    //   Tambahkan helper di ProgressService, misalnya AddCoins(int amount) dan AddItem(string itemId, int amount),
    //   yang mengubah nilai lalu memanggil MarkDirty().
    //
    //   ⚠ KEAMANAN: data ini disimpan dari sisi client, jadi bisa dicurangi (misalnya koin diedit
    //   lewat memory editor). Untuk game kasual/edukasi ini masih wajar. Tapi kalau koin/item
    //   jadi PENTING (bisa ditukar, ada leaderboard, ada pembelian), pindahkan logika
    //   tambah/kurangi ke Unity Cloud Code (berjalan di server). Simpan nilainya sebagai Cloud Save
    //   "Protected" player data (client hanya bisa membaca), dan client cukup memanggil fungsi
    //   Cloud Code seperti "ClaimReward" atau "BuyItem".

    // TODO (next develop) – Distrik yang sudah dikunjungi / dibuka
    //   public List<string> visitedDistricts = new List<string>();   // contoh: "business-district"
    //   public List<string> unlockedDistricts = new List<string>();
    //
    //   Cara pakai: pasang trigger collider di gerbang distrik, panggil
    //   ProgressService.MarkDistrictVisited("business-district") saat pemain lokal masuk.
    //   Polanya sama dengan MarkDialogueCompleted(). Kalau "unlock" nanti jadi hadiah atau
    //   berbayar, lihat catatan KEAMANAN di atas (Cloud Code).

    // TODO (next develop) – Pengaturan (volume, sensitivitas kamera)
    //   public GameSettings settings = new GameSettings();
    //   // [Serializable] public class GameSettings {
    //   //     public float masterVolume = 1f, musicVolume = 1f, sfxVolume = 1f;
    //   //     public float cameraSensitivity = 1f;
    //   // }
    //
    //   Pertimbangkan dulu: pengaturan biasanya per-perangkat (HP kecil vs tablet), jadi
    //   PlayerPrefs saja sering sudah cukup. Simpan di sini hanya kalau pengaturan harus ikut
    //   pindah saat pemain login di HP lain. Terapkan ke AudioMixer dan CameraOrbit setelah
    //   event ProgressService.Loaded.
}
