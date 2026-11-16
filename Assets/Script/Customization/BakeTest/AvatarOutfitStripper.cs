using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tahap 0b (tes bake), "mode pemain lain": setelah bake, outfit sumber yang mesh-nya sudah ikut
/// digabung dihapus supaya tidak menyimpan GameObject/mesh/material yang tidak terpakai lagi.
///
/// Sengaja TIDAK memakai cleanAfterMerge bawaan BoZo: opsi itu memanggil ReturnBones, sehingga tulang
/// tambahan (rambut, rok, dll.) ikut terhapus padahal mesh gabungan masih memakainya. Hasilnya mesh
/// tertarik seperti paku. Di sini outfit dihapus tanpa ReturnBones; tulangnya tetap di kerangka tubuh.
/// Juga tidak menyetel isStatic, karena OutfitSystem.OnDestroy hanya menghapus mesh/material gabungan
/// kalau isStatic = false (kalau true, keduanya bocor).
/// </summary>
public static class AvatarOutfitStripper {
    public static int StripMergedOutfits(OutfitSystem avatar) {
        var outfits = new List<Outfit>(avatar.GetOutfits());
        int removed = 0;
        foreach (Outfit outfit in outfits) {
            if (outfit == null || !outfit.mergeMesh) continue; // outfit dengan renderer sendiri tetap dipakai
            Object.Destroy(outfit.gameObject);
            removed++;
        }
        return removed;
    }
}
