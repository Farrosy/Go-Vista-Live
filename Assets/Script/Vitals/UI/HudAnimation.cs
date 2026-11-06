using UnityEngine;

/// Rumus animasi kecil yang dipakai bersama oleh tampilan HUD.
public static class HudAnimation {
    private const float PulseSeconds = 0.5f;

    /// Gelombang 0..1 dengan periode 0,5 detik, untuk ikon dan tepi layar yang berdenyut.
    public static float Pulse(float time) => 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / PulseSeconds);

    /// Mulus di kedua ujung; dipakai untuk fade dan geser.
    public static float EaseOut(float t) {
        float clamped = Mathf.Clamp01(t);
        return 1f - (1f - clamped) * (1f - clamped);
    }

    /// Mulai agak besar lalu menghentak ke ukuran normal (judul layar mati).
    public static float Punch(float t) => Mathf.LerpUnclamped(1.35f, 1f, EaseOut(t));
}
