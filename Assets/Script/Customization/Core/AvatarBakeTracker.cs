using System.Collections.Generic;

/// <summary>Status satu avatar di antrean bake.</summary>
public enum AvatarBakeState {
    /// <summary>Menunggu giliran (avatar disembunyikan di perangkat ini).</summary>
    Queued,
    /// <summary>Sedang dirakit BoZo.</summary>
    Baking,
    /// <summary>Selesai: tampil dalam wujud custom.</summary>
    Ready,
    /// <summary>Gagal: tampil sebagai template asalnya (keputusan user 2026-11-17, tanpa coba ulang).</summary>
    Failed
}

/// <summary>
/// Antrean + status bake avatar custom di satu perangkat (Tahap 3). Logika murni supaya bisa dites:
///  - avatar sendiri selalu didahulukan, sisanya urut kedatangan;
///  - avatar yang diminta ulang (data berubah) dipindah ke belakang antrean dengan data baru;
///  - "siap spawn" = tidak ada lagi yang Queued/Baking (Failed dihitung selesai karena tampil template).
/// Kunci = id avatar (mis. RealtimeView), atau <see cref="LocalKey"/> untuk avatar sendiri yang di-bake di lobby.
/// </summary>
public sealed class AvatarBakeTracker<TPayload> {
    public const int LocalKey = -1;

    private readonly List<int> _queue = new List<int>();
    private readonly Dictionary<int, AvatarBakeState> _states = new Dictionary<int, AvatarBakeState>();
    private readonly Dictionary<int, TPayload> _payloads = new Dictionary<int, TPayload>();

    public int Count => _states.Count;

    /// <summary>Minta bake (atau bake ulang dengan data baru). Avatar sendiri masuk paling depan.</summary>
    public void Request(int key, TPayload payload) {
        _queue.Remove(key);
        if (key == LocalKey) _queue.Insert(0, key);
        else _queue.Add(key);
        _states[key] = AvatarBakeState.Queued;
        _payloads[key] = payload;
    }

    /// <summary>Avatar keluar room / tidak perlu bake lagi (mis. memakai template).</summary>
    public void Forget(int key) {
        _queue.Remove(key);
        _states.Remove(key);
        _payloads.Remove(key);
    }

    /// <summary>Ambil giliran berikutnya dan tandai Baking.</summary>
    public bool TryStartNext(out int key, out TPayload payload) {
        if (_queue.Count == 0) {
            key = 0;
            payload = default;
            return false;
        }
        key = _queue[0];
        _queue.RemoveAt(0);
        payload = _payloads[key];
        _states[key] = AvatarBakeState.Baking;
        return true;
    }

    /// <summary>Hasil bake. Diabaikan kalau avatar sudah dilupakan atau diminta ulang selama bake.</summary>
    public bool Complete(int key, bool success) {
        if (!_states.TryGetValue(key, out AvatarBakeState state) || state != AvatarBakeState.Baking) return false;
        _states[key] = success ? AvatarBakeState.Ready : AvatarBakeState.Failed;
        return true;
    }

    public AvatarBakeState? StateOf(int key) => _states.TryGetValue(key, out AvatarBakeState state) ? state : (AvatarBakeState?)null;

    /// <summary>Jumlah avatar yang sudah selesai (Ready/Failed) dan total yang dilacak, untuk "Menyiapkan karakter 3/5".</summary>
    public (int done, int total) Progress() {
        int done = 0;
        foreach (AvatarBakeState state in _states.Values) {
            if (state == AvatarBakeState.Ready || state == AvatarBakeState.Failed) done++;
        }
        return (done, _states.Count);
    }

    /// <summary>Semua avatar yang dilacak sudah selesai: pemain boleh masuk world.</summary>
    public bool AllDone {
        get {
            (int done, int total) = Progress();
            return done == total;
        }
    }
}
