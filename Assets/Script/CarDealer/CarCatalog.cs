using System;
using UnityEngine;

/// <summary>
/// Daftar mobil yang dijual dealer. Asset: Assets/Resources/CarDealer/CarCatalog.asset
/// (dibuat/diisi lewat Go Vista > Dealer Mobil > 1. Bangun Katalog, Gambar & Preview).
/// Satu entry = satu kartu di grid panel dealer, urut sesuai daftar.
/// </summary>
[CreateAssetMenu(menuName = "Go Vista/Car Catalog")]
public sealed class CarCatalog : ScriptableObject {
    public const string ResourcePath = "CarDealer/CarCatalog";

    [Serializable]
    public sealed class Entry {
        [Tooltip("ID unik untuk simpan kepemilikan (Cloud Save). JANGAN diganti setelah rilis.")]
        public string id = "matador";
        public string displayName = "Matador";
        [Tooltip("Prefab mobil yang bisa dikendarai, path di Resources tanpa .prefab (Realtime.Instantiate).")]
        public string drivablePrefab = "Vehicles/Matador_Drivable";
        [Tooltip("Harga dalam gold. 0 = gratis (prototype).")]
        [Min(0)] public int price;
        [Tooltip("Kecepatan maksimum (km/jam) untuk bar Speed. Diisi otomatis dari CarDriveController prefab.")]
        public float maxSpeedKmh = 60f;
        [Tooltip("Gambar statis untuk kartu di grid.")]
        public Sprite thumbnail;
        [Tooltip("Model tampilan saja (tanpa fisika/jaringan) yang diputar di preview besar.")]
        public GameObject previewModel;
    }

    public Entry[] cars = Array.Empty<Entry>();

    public Entry Find(string id) {
        foreach (Entry e in cars)
            if (e != null && e.id == id) return e;
        return null;
    }

    private static CarCatalog _loaded;

    public static CarCatalog Load() {
        if (_loaded == null) _loaded = Resources.Load<CarCatalog>(ResourcePath);
        return _loaded;
    }
}
