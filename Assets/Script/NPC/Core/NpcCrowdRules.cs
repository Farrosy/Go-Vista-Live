/// <summary>
/// Aturan murni (tanpa Unity) untuk kerumunan NPC jaringan, dipakai NpcDirector dan lobby.
///
/// Authority = client yang men-spawn dan menjalankan NPC:
///  • Ada avatar di World → client dengan owner-clientID avatar terkecil (aturan lama).
///  • Belum ada avatar di World (semua masih di lobby / daftar pemain) → pemilik view NpcDirector,
///    asal WorldScene (navmesh) sudah ter-load di client ini. View yang belum dimiliki siapa pun boleh
///    diklaim, jadi kerumunan sudah di-spawn (tersembunyi) sebelum ada yang menekan MULAI.
/// </summary>
public static class NpcCrowdRules {
    public const int NoClient = -1;

    /// <param name="myClientId">clientID lokal (negatif = belum terhubung).</param>
    /// <param name="localInWorld">LocalVenue client ini World.</param>
    /// <param name="worldSceneLoaded">WorldScene + navmesh-nya sudah ter-load di client ini.</param>
    /// <param name="lowestWorldAvatarOwner">Owner-clientID avatar terkecil di World, atau NoClient kalau tidak ada.</param>
    /// <param name="directorOwner">Owner view NpcDirector, atau NoClient kalau belum dimiliki.</param>
    public static bool IsAuthority(int myClientId, bool localInWorld, bool worldSceneLoaded,
                                   int lowestWorldAvatarOwner, int directorOwner) {
        if (myClientId < 0 || !localInWorld) return false;
        if (lowestWorldAvatarOwner >= 0) return myClientId == lowestWorldAvatarOwner;
        if (!worldSceneLoaded) return false;
        return directorOwner < 0 || directorOwner == myClientId;
    }

    /// Kerumunan siap ditampilkan: roster sudah ditandai lengkap, atau jumlah NPC sudah mencapai target.
    public static bool IsCrowdReady(bool rosterSpawned, int presentCount, int targetCount) {
        return rosterSpawned || presentCount >= targetCount;
    }
}
