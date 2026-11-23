using System;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bagian toko di editor karakter: semua item boleh dicoba gratis; item yang belum dimiliki diberi label harga.
/// SIMPAN membuka keranjang belanja: daftar item belum dimiliki (bisa dicentang/tidak), total, saldo, dan sisa
/// saldo. Item yang tidak dicentang tidak dibeli dan kembali ke pilihan saat terakhir disimpan. Saldo kurang =
/// tombol beli mati sampai beberapa centang dihapus. Dipakai editor lobby dan ruang ganti.
/// </summary>
public sealed class CustomizerShop {
    private const string HeadType = "Head";
    private const float DisabledAlpha = 0.4f;

    private readonly CartRefs _cart;
    private readonly ChatUiAssets _a;
    private readonly List<(string id, GameObject tag)> _tags = new List<(string, GameObject)>();
    private readonly List<CartLine> _lines = new List<CartLine>();
    private CustomCharacterRecord _record;
    private CustomCharacterRecord _baseline;
    private Action<CustomCharacterRecord> _save;

    private sealed class CartLine {
        public string Id;
        public int Price;
        public bool Checked = true;
        public GameObject CheckOn;
    }

    /// <summary>Item baru saja dibeli (label harga perlu disembunyikan).</summary>
    public event Action Purchased;

    public CustomizerShop(CartRefs cart, ChatUiAssets assets) {
        _cart = cart;
        _a = assets;
        _cart.Cancel.onClick.AddListener(Close);
        _cart.Confirm.onClick.AddListener(Checkout);
    }

    public bool IsOpen => _cart.Overlay.activeSelf;

    public void Close() {
        _cart.Overlay.SetActive(false);
        _record = _baseline = null;
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
    /// true = record punya item yang belum dibeli: keranjang dibuka, dan <paramref name="save"/> dipanggil setelah
    /// pembayaran berhasil, dengan record final tanpa item yang tidak dicentang (null = record tidak berubah).
    /// false = semua sudah dimiliki, simpan langsung.
    /// </summary>
    /// <param name="baseline">Penampilan saat terakhir disimpan: tujuan item yang tidak dicentang.</param>
    public bool InterceptSave(CustomCharacterRecord record, CustomCharacterRecord baseline, Action<CustomCharacterRecord> save) {
        List<string> unpaid = OutfitShop.Unpaid(record);
        if (unpaid.Count == 0) return false;
        _record = record;
        _baseline = baseline;
        _save = save;
        BuildLines(unpaid);
        Refresh();
        _cart.Overlay.SetActive(true);
        return true;
    }

    // ---------------------------------------------------------------- Keranjang

    private void BuildLines(List<string> ids) {
        ChatUiKit.DestroyChildren(_cart.List);
        _lines.Clear();
        foreach (string id in ids) {
            var line = new CartLine { Id = id, Price = OutfitShop.PriceOf(id) };
            Button row = CustomizerCartView.CreateRow(_cart.List, _a, IconOf(id), LabelOf(id), CategoryOf(id), line.Price,
                                                      out line.CheckOn, out _);
            row.onClick.AddListener(() => {
                line.Checked = !line.Checked;
                Refresh();
            });
            _lines.Add(line);
        }
    }

    private void Refresh() {
        int total = 0, chosen = 0;
        foreach (CartLine line in _lines) {
            line.CheckOn.SetActive(line.Checked);
            if (!line.Checked) continue;
            total += line.Price;
            chosen++;
        }
        int balance = PlayerCoins.Balance;
        int remaining = balance - total;
        bool affordable = OutfitShopRules.CanAfford(balance, total);
        int dropped = _lines.Count - chosen;

        _cart.Subtitle.text = $"{chosen} dari {_lines.Count} item dipilih · ketuk untuk centang / hapus centang";
        _cart.Total.text = Coins(total);
        _cart.Balance.text = Coins(balance);
        _cart.Remaining.text = remaining < 0 ? "-" + Coins(-remaining) : Coins(remaining);
        _cart.Remaining.color = affordable ? FriendsTheme.Teal : FriendsTheme.Danger;
        if (!affordable) {
            _cart.Hint.text = $"Koin kurang {Coins(-remaining)}. Hapus centang beberapa item supaya cukup.";
            _cart.Hint.color = FriendsTheme.Danger;
        } else if (dropped > 0) {
            _cart.Hint.text = "Item yang tidak dicentang tidak dibeli dan kembali ke pilihan sebelumnya.";
            _cart.Hint.color = FriendsTheme.Muted;
        } else {
            _cart.Hint.text = "Item yang dibeli jadi milikmu dan bisa dipakai lagi kapan saja.";
            _cart.Hint.color = FriendsTheme.Muted;
        }
        _cart.Confirm.interactable = affordable;
        if (!_cart.Confirm.TryGetComponent(out CanvasGroup group)) group = _cart.Confirm.gameObject.AddComponent<CanvasGroup>();
        group.alpha = affordable ? 1f : DisabledAlpha; // jelas mati saat koin kurang
        _cart.ConfirmLabel.text = chosen == 0 ? "SIMPAN" : "BELI & SIMPAN";
    }

    private void Checkout() {
        if (_record == null) return;
        var buy = new List<string>();
        var drop = new List<string>();
        foreach (CartLine line in _lines) (line.Checked ? buy : drop).Add(line.Id);
        if (!OutfitShop.TryBuy(buy)) {
            Refresh(); // saldo berubah sejak keranjang dibuka
            return;
        }
        CustomCharacterRecord final = drop.Count == 0 ? null : WithoutDropped(_record, _baseline, drop);
        Action<CustomCharacterRecord> save = _save;
        Close();
        foreach ((string id, GameObject tag) in _tags) {
            if (tag && OutfitShop.IsOwned(id)) tag.SetActive(false);
        }
        if (buy.Count > 0) Purchased?.Invoke();
        save?.Invoke(final);
    }

    /// Lepas item yang tidak dicentang (kembali ke penampilan terakhir disimpan). Stiker di baju yang diganti ikut
    /// dibuang, dan kalau kepala berganti gaya, bagian wajah yang tidak cocok ikut dikembalikan.
    private static CustomCharacterRecord WithoutDropped(CustomCharacterRecord record, CustomCharacterRecord baseline,
                                                        List<string> drop) {
        CustomCharacterRecord result = OutfitShopRules.Revert(record, baseline, drop, out List<string> types);
        foreach (string type in types) result = CharacterRecordConverter.WithoutDecalsOn(result, type);
        if (!types.Contains(HeadType)) return result;
        bool anime = OutfitCatalog.IsAnimeHead(result);
        var misfits = new List<string>();
        foreach (OutfitRecord outfit in result.outfits) {
            if (outfit != null && !OutfitCatalog.Fits(outfit.path, anime)) misfits.Add(outfit.path);
        }
        return misfits.Count == 0 ? result : OutfitShopRules.Revert(result, baseline, misfits, out _);
    }

    // ---------------------------------------------------------------- Nama & ikon

    /// <summary>Nama item untuk ditampilkan (nama outfit BoZo / label stiker).</summary>
    public static string LabelOf(string id) {
        if (OutfitShopRules.IsSticker(id)) {
            string decal = OutfitShopRules.StickerName(id);
            return TexturePackageCatalog.TryGetSticker(decal, out TexturePackageCatalog.Entry entry) ? entry.Label : decal;
        }
        Outfit outfit = OutfitCatalog.Load(id);
        if (outfit == null) return id;
        return string.IsNullOrWhiteSpace(outfit.OutfitName) ? outfit.name : outfit.OutfitName;
    }

    private static string CategoryOf(string id) {
        if (OutfitShopRules.IsSticker(id)) return "Stiker";
        string type = RecordEdits.TypeOf(id);
        return EditorCategories.LabelOf(type) ?? type;
    }

    private static Sprite IconOf(string id) {
        if (OutfitShopRules.IsSticker(id)) {
            return TexturePackageCatalog.TryGetSticker(OutfitShopRules.StickerName(id), out TexturePackageCatalog.Entry entry)
                ? entry.Icon : null;
        }
        Outfit outfit = OutfitCatalog.Load(id);
        return outfit ? outfit.OutfitIcon : null;
    }

    private static string Coins(int amount) => CoinRules.FormatBalance(amount) + " G";
}
