using Normal.Realtime;

/// <summary>State spawner mobil pemain (satu object di MainMultiplayerScene). Dibuat source generator Normcore.</summary>
[RealtimeModel]
public partial class VehicleDirectorModel {
    // TIDAK DIPAKAI lagi sejak spawn per pemain lewat dealer (dulu: penanda mobil otomatis sudah di-spawn).
    // Dibiarkan supaya RealtimeView di scene tidak perlu dibangun ulang.
    [RealtimeProperty(1, true, true)] private bool _spawned;
}
