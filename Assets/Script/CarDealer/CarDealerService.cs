using System.Collections.Generic;

/// <summary>
/// Kepemilikan mobil pemain lokal. Disimpan di PlayerProgress.ownedCars (Cloud Save), gold dipotong lewat PlayerCoins.
/// Tutorial dealer (sekali saja) dicatat di PlayerProgress.completedTutorials dengan ID TutorialId (ProgressService.MarkTutorialDone).
/// </summary>
public static class CarDealerService {
    public const string TutorialId = "johnny_dealer_tutorial";

    private static List<string> Owned {
        get {
            PlayerProgress p = ProgressService.Current;
            if (p.ownedCars == null) p.ownedCars = new List<string>();
            return p.ownedCars;
        }
    }

    public static bool IsOwned(string carId) => !string.IsNullOrEmpty(carId) && Owned.Contains(carId);

    public static int Balance => PlayerCoins.Balance;

    /// <summary>Beli mobil: potong gold, catat milik, simpan sekali. False kalau sudah punya atau gold kurang.</summary>
    public static bool TryBuy(CarCatalog.Entry car) {
        if (car == null || IsOwned(car.id)) return false;
        if (!CarDealerRules.CanAfford(car.price, Balance)) return false;
        if (car.price > 0 && !PlayerCoins.TrySpend(car.price, saveNow: false)) return false;
        Owned.Add(car.id);
        ProgressService.MarkDirty();
        _ = ProgressService.SaveAsync();
        return true;
    }

    public static bool TutorialDone => ProgressService.IsTutorialDone(TutorialId);

    public static void MarkTutorialDone() => ProgressService.MarkTutorialDone(TutorialId);
}
