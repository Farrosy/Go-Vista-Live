using System;
using System.Collections.Generic;

/// <summary>
/// Aturan toko pakaian (tanpa Unity, bisa dites): harga item naik sesuai urutan di grid per tipe, id item
/// (path outfit BoZo atau "sticker:&lt;nama&gt;"), dan daftar item record yang belum dibayar.
/// Kepemilikan dan saldo dikelola OutfitShop (Runtime).
/// </summary>
public static class OutfitShopRules {
    public const int BasePrice = 50;
    public const int PriceStep = 25;
    public const string StickerPrefix = "sticker:";

    public static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Harga item ke-<paramref name="index"/> (0 = pertama) di tipenya. Indeks tidak dikenal = gratis.</summary>
    public static int PriceAt(int index) => index < 0 ? 0 : BasePrice + PriceStep * index;

    public static string StickerId(string decal) => StickerPrefix + decal;

    public static bool IsSticker(string id) => id != null && id.StartsWith(StickerPrefix, StringComparison.Ordinal);

    public static string StickerName(string id) => IsSticker(id) ? id.Substring(StickerPrefix.Length) : "";

    /// <summary>Semua item yang dipakai record ini (path outfit + stiker), tiap item sekali.</summary>
    public static List<string> ItemsOf(CustomCharacterRecord record) {
        var items = new List<string>();
        if (record == null) return items;
        var seen = new HashSet<string>(IdComparer);
        foreach (OutfitRecord outfit in record.outfits ?? new List<OutfitRecord>()) {
            if (outfit != null && !string.IsNullOrEmpty(outfit.path) && seen.Add(outfit.path)) items.Add(outfit.path);
        }
        foreach (DecalRecord decal in record.decals ?? new List<DecalRecord>()) {
            if (decal == null || string.IsNullOrEmpty(decal.decal)) continue;
            string id = StickerId(decal.decal);
            if (seen.Add(id)) items.Add(id);
        }
        return items;
    }

    /// <summary>Item record yang belum dimiliki dan berharga (harga 0 = gratis, tidak perlu dibeli).</summary>
    public static List<string> Unpaid(CustomCharacterRecord record, Func<string, bool> isOwned, Func<string, int> priceOf) {
        var unpaid = new List<string>();
        foreach (string id in ItemsOf(record)) {
            if (!isOwned(id) && priceOf(id) > 0) unpaid.Add(id);
        }
        return unpaid;
    }

    public static int Total(IEnumerable<string> ids, Func<string, int> priceOf) {
        long total = 0;
        foreach (string id in ids) total += Math.Max(0, priceOf(id));
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public static bool CanAfford(int balance, int total) => balance >= total;
}
