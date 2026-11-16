using Normal.Realtime;

/// <summary>State spawner mobil pemain (satu object di MainMultiplayerScene). Dibuat source generator Normcore.</summary>
[RealtimeModel]
public partial class VehicleDirectorModel {
    // True setelah mobil pertama kali di-spawn di room ini. Mencegah dua client men-spawn bersamaan.
    [RealtimeProperty(1, true, true)] private bool _spawned;
}
