using System;

/// <summary>Tampilan tombol aksi di panel dealer.</summary>
public enum DealerButtonState {
    /// Belum dimiliki dan gold cukup: tombol hijau "Buy".
    Buy,
    /// Belum dimiliki dan gold kurang: tombol abu-abu, tidak bisa ditekan.
    BuyDisabled,
    /// Baru saja dibeli: tombol kuning "Bought!" sebentar, lalu berubah jadi Spawn.
    Bought,
    /// Sudah dimiliki: tombol biru "Spawn".
    Spawn,
}

/// <summary>
/// Aturan dealer mobil tanpa Unity (dites di Tests/EditMode/CarDealer). Dipakai CarDealerService dan CarDealerPanel.
/// </summary>
public static class CarDealerRules {
    /// <summary>Jumlah kotak bar Speed.</summary>
    public const int SpeedSegments = 10;
    /// <summary>Kecepatan maksimum yang mengisi bar Speed penuh (km/jam).</summary>
    public const float FullSpeedKmh = 100f;
    /// <summary>Tulisan di kotak Price untuk mobil berharga 0 (prototype).</summary>
    public const string FreeLabel = "Gratis";

    public static bool CanAfford(int price, int balance) => Math.Max(0, price) <= Math.Max(0, balance);

    public static DealerButtonState StateFor(bool owned, bool justBought, int price, int balance) {
        if (owned) return justBought ? DealerButtonState.Bought : DealerButtonState.Spawn;
        return CanAfford(price, balance) ? DealerButtonState.Buy : DealerButtonState.BuyDisabled;
    }

    /// <summary>Teks tombol aksi.</summary>
    public static string ButtonLabel(DealerButtonState state) => state switch {
        DealerButtonState.Bought => "Bought!",
        DealerButtonState.Spawn => "Spawn",
        _ => "Buy",
    };

    /// <summary>Teks kotak Price: "Gratis" kalau 0, selain itu "10,000G".</summary>
    public static string PriceLabel(int price) =>
        price <= 0 ? FreeLabel : price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "G";

    /// <summary>Jumlah kotak Speed yang menyala untuk kecepatan maksimum mobil (minimal 1, maksimal penuh).</summary>
    public static int SpeedFill(float maxSpeedKmh) {
        if (maxSpeedKmh <= 0f) return 0;
        int filled = (int)Math.Round(maxSpeedKmh / FullSpeedKmh * SpeedSegments, MidpointRounding.AwayFromZero);
        return Math.Clamp(filled, 1, SpeedSegments);
    }
}
