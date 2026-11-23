using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Toko pakaian: harga item (OutfitShopRules, sesuai urutan grid per tipe), kepemilikan per akun
/// (PlayerProgress.ownedItems, Cloud Save), dan pembelian dengan koin (PlayerCoins).
///
/// Gratis/milik tanpa membeli: semua item 4 karakter default, dan item yang sudah dipakai di slot custom saat
/// toko pertama kali ada (dicatat sekali per akun, lihat MigrateWornItems). Ekspresi, warna, motif, dan bentuk
/// wajah tidak dijual. Hanya lokal: pemain lain tidak membaca kepemilikan, cuma penampilan yang tersinkron.
/// </summary>
public static class OutfitShop {
    private static HashSet<string> _defaultItems;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        ProgressService.Loaded -= MigrateWornItems;
        _defaultItems = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() {
        ProgressService.Loaded += MigrateWornItems;
        if (ProgressService.IsLoaded) MigrateWornItems();
    }

    // ---------------------------------------------------------------- Harga & kepemilikan

    /// <summary>Harga item (0 = gratis / tidak dijual).</summary>
    public static int PriceOf(string id) {
        if (string.IsNullOrEmpty(id)) return 0;
        if (OutfitShopRules.IsSticker(id)) return OutfitShopRules.PriceAt(StickerIndex(OutfitShopRules.StickerName(id)));
        string type = RecordEdits.TypeOf(id);
        IReadOnlyList<Outfit> outfits = OutfitCatalog.ForType(type);
        for (int i = 0; i < outfits.Count; i++) {
            if (OutfitShopRules.IdComparer.Equals(OutfitCatalog.PathOf(type, outfits[i]), id)) return OutfitShopRules.PriceAt(i);
        }
        return 0; // tidak tampil di grid: tidak dijual
    }

    public static int PriceOfSticker(string decal) => PriceOf(OutfitShopRules.StickerId(decal));

    public static bool IsOwned(string id) {
        if (string.IsNullOrEmpty(id) || PriceOf(id) <= 0) return true;
        if (DefaultItems.Contains(id)) return true;
        List<string> owned = ProgressService.Current.ownedItems;
        if (owned == null) return false;
        foreach (string item in owned) {
            if (OutfitShopRules.IdComparer.Equals(item, id)) return true;
        }
        return false;
    }

    public static bool IsStickerOwned(string decal) => IsOwned(OutfitShopRules.StickerId(decal));

    /// <summary>Item record yang harus dibeli dulu sebelum disimpan.</summary>
    public static List<string> Unpaid(CustomCharacterRecord record) => OutfitShopRules.Unpaid(record, IsOwned, PriceOf);

    public static int Total(IEnumerable<string> ids) => OutfitShopRules.Total(ids, PriceOf);

    /// <summary>Bayar semua item ini sekaligus. false = saldo kurang (tidak ada yang berubah).</summary>
    public static bool TryBuy(IReadOnlyCollection<string> ids) {
        if (ids == null || ids.Count == 0) return true;
        int total = Total(ids);
        if (!PlayerCoins.TrySpend(total, saveNow: false)) return false;
        AddOwned(ids);
        _ = ProgressService.SaveAsync(); // koin + item masuk ke cloud dalam satu save
        return true;
    }

    // ---------------------------------------------------------------- Item gratis

    /// Item yang dipakai 4 karakter default (male, female, anime male, anime female).
    private static HashSet<string> DefaultItems {
        get {
            if (_defaultItems != null) return _defaultItems;
            _defaultItems = new HashSet<string>(OutfitShopRules.IdComparer);
            foreach (CharacterTemplate template in CharacterTemplates.All) {
                CustomCharacterRecord record = CharacterRecordConverter.FromTemplate(template);
                foreach (string id in OutfitShopRules.ItemsOf(record)) _defaultItems.Add(id);
            }
            return _defaultItems;
        }
    }

    /// Pemain lama: item yang sudah dipakai di slot custom sebelum toko ada dianggap sudah dibeli, sekali per akun.
    private static void MigrateWornItems() {
        PlayerProgress progress = ProgressService.Current;
        if (progress == null || progress.shopMigrated) return;
        var worn = new List<string>();
        foreach (CustomCharacterRecord record in CustomCharacterStore.All()) worn.AddRange(OutfitShopRules.ItemsOf(record));
        AddOwned(worn);
        progress.shopMigrated = true;
        ProgressService.MarkDirty();
        _ = ProgressService.SaveAsync();
    }

    private static void AddOwned(IEnumerable<string> ids) {
        PlayerProgress progress = ProgressService.Current;
        progress.ownedItems ??= new List<string>();
        var known = new HashSet<string>(progress.ownedItems, OutfitShopRules.IdComparer);
        foreach (string id in ids) {
            if (!string.IsNullOrEmpty(id) && known.Add(id)) progress.ownedItems.Add(id);
        }
        ProgressService.MarkDirty();
    }

    private static int StickerIndex(string decal) {
        IReadOnlyList<TexturePackageCatalog.Entry> stickers = TexturePackageCatalog.Stickers;
        for (int i = 0; i < stickers.Count; i++) {
            if (OutfitShopRules.IdComparer.Equals(stickers[i].Id, decal)) return i;
        }
        return -1;
    }
}
