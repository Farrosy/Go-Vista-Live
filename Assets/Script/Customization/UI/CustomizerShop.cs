using System;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bagian toko di editor karakter: semua item boleh dicoba gratis; item yang belum dimiliki diberi label harga,
/// dan SIMPAN membuka dialog "Beli N item" (total + saldo) sebelum penampilan disimpan. Saldo kurang = tidak bisa
/// disimpan sampai item berlabel harga dilepas. Dipakai editor lobby dan ruang ganti.
/// </summary>
public sealed class CustomizerShop {
    private const int MaxListedNames = 3;

    private readonly CustomizerUi.ConfirmDialog _dialog;
    private readonly ChatUiAssets _a;
    private readonly List<(string id, GameObject tag)> _tags = new List<(string, GameObject)>();
    private List<string> _pending;
    private Action _save;

    /// <summary>Item baru saja dibeli (label harga perlu disembunyikan).</summary>
    public event Action Purchased;

    public CustomizerShop(CustomizerUi.ConfirmDialog dialog, ChatUiAssets assets) {
        _dialog = dialog;
        _a = assets;
        _dialog.Cancel.onClick.AddListener(Close);
        _dialog.Confirm.onClick.AddListener(Buy);
    }

    public bool IsOpen => _dialog.Overlay.activeSelf;

    public void Close() {
        _dialog.Overlay.SetActive(false);
        _pending = null;
        _save = null;
    }

    /// <summary>Label harga untuk item grid ini kalau belum dimiliki. Dipanggil setiap grid dibangun ulang.</summary>
    public void Tag(Button item, string id) {
        int price = OutfitShop.PriceOf(id);
        if (price <= 0 || OutfitShop.IsOwned(id)) return;
        _tags.Add((id, CustomizerEditorView.AddPriceTag(item, _a, price)));
    }

    /// <summary>Lupakan label grid yang sudah dihancurkan (grid dibangun ulang).</summary>
    public void ForgetDestroyedTags() => _tags.RemoveAll(t => !t.tag);

    /// <summary>
    /// true = record punya item yang belum dibeli: dialog beli dibuka dan <paramref name="save"/> dijalankan
    /// setelah pembelian berhasil. false = semua sudah dimiliki, simpan langsung.
    /// </summary>
    public bool InterceptSave(CustomCharacterRecord record, Action save) {
        List<string> unpaid = OutfitShop.Unpaid(record);
        if (unpaid.Count == 0) return false;
        int total = OutfitShop.Total(unpaid);
        int balance = PlayerCoins.Balance;
        bool affordable = OutfitShopRules.CanAfford(balance, total);
        string names = ListNames(unpaid);
        _dialog.Title.text = affordable ? $"Beli {unpaid.Count} item?" : "Koin tidak cukup";
        _dialog.Body.text = affordable
            ? $"{names}\nTotal <b>{Coins(total)}</b> · Saldo {Coins(balance)}"
            : $"{names}\nButuh {Coins(total)}, saldo {Coins(balance)}. Lepas item berlabel harga atau kumpulkan koin dulu.";
        _dialog.Confirm.interactable = affordable;
        _pending = affordable ? unpaid : null;
        _save = affordable ? save : null;
        _dialog.Overlay.SetActive(true);
        return true;
    }

    private void Buy() {
        List<string> pending = _pending;
        Action save = _save;
        if (pending == null) return;
        if (!OutfitShop.TryBuy(pending)) {
            Close();
            return; // saldo berubah sejak dialog dibuka: SIMPAN lagi menampilkan dialog baru
        }
        Close();
        foreach ((string id, GameObject tag) in _tags) {
            if (tag && OutfitShop.IsOwned(id)) tag.SetActive(false);
        }
        Purchased?.Invoke();
        save?.Invoke();
    }

    private static string ListNames(List<string> ids) {
        var names = new List<string>();
        for (int i = 0; i < ids.Count && i < MaxListedNames; i++) names.Add(LabelOf(ids[i]));
        string list = string.Join(", ", names);
        return ids.Count > MaxListedNames ? $"{list} +{ids.Count - MaxListedNames} lainnya" : list;
    }

    /// <summary>Nama item untuk ditampilkan (nama outfit BoZo / label stiker).</summary>
    public static string LabelOf(string id) {
        if (OutfitShopRules.IsSticker(id)) {
            string decal = OutfitShopRules.StickerName(id);
            return TexturePackageCatalog.TryGetSticker(decal, out TexturePackageCatalog.Entry entry)
                ? "Stiker " + entry.Label : "Stiker " + decal;
        }
        Outfit outfit = OutfitCatalog.Load(id);
        if (outfit == null) return id;
        return string.IsNullOrWhiteSpace(outfit.OutfitName) ? outfit.name : outfit.OutfitName;
    }

    private static string Coins(int amount) => CoinRules.FormatBalance(amount) + " G";
}
