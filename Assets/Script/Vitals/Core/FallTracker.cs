using System;

/// Mengukur jarak jatuh: dari titik tertinggi selama di udara sampai menapak lagi.
public sealed class FallTracker {
    private bool _airborne;
    private float _peakY;

    /// Mengembalikan jarak jatuh (meter) pada frame mendarat, selain itu 0.
    public float Update(float y, bool grounded) {
        if (!grounded) {
            _peakY = _airborne ? Math.Max(_peakY, y) : y;
            _airborne = true;
            return 0f;
        }
        if (!_airborne) return 0f;
        _airborne = false;
        return Math.Max(0f, _peakY - y);
    }

    /// Dipanggil saat teleport supaya perpindahan posisi tidak terhitung sebagai jatuh.
    public void Reset() => _airborne = false;
}
