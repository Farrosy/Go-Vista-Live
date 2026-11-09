using UnityEngine;

/// Rumus animasi kecil yang dipakai bersama oleh tampilan HUD.
public static class HudAnimation {
    private const float PulseSeconds = 0.5f;
    private const float FlashPeak = 0.85f;
    private const float FlashPeakAt = 0.2f;
    private const float PopOvershoot = 1.15f;
    private const float PopPeakAt = 0.7f;
    private const float DropStartOffset = 397f;
    private const float DropBounceHeight = 20f;
    private const float DropLandAt = 0.5f;

    // Titik goyang layar saat benturan, dalam piksel mockup.
    private static readonly Vector2[] ShakeKeys = {
        new Vector2(-14f, 10f), new Vector2(12f, -8f), new Vector2(-9f, 6f),
        new Vector2(7f, -4f), new Vector2(-3f, 2f), Vector2.zero,
    };

    /// Gelombang 0..1 dengan periode 0,5 detik, untuk ikon dan tepi layar yang berdenyut.
    public static float Pulse(float time) => 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / PulseSeconds);

    /// Mulus di kedua ujung; dipakai untuk fade dan geser.
    public static float EaseOut(float t) {
        float clamped = Mathf.Clamp01(t);
        return 1f - (1f - clamped) * (1f - clamped);
    }

    /// Mulai agak besar lalu menghentak ke ukuran normal (judul layar mati).
    public static float Punch(float t) => Mathf.LerpUnclamped(1.35f, 1f, EaseOut(t));

    /// Alpha kilat putih: cepat naik ke puncak lalu memudar.
    public static float Flash(float t) {
        float clamped = Mathf.Clamp01(t);
        return clamped < FlashPeakAt
            ? Mathf.Lerp(0f, FlashPeak, clamped / FlashPeakAt)
            : Mathf.Lerp(FlashPeak, 0f, (clamped - FlashPeakAt) / (1f - FlashPeakAt));
    }

    /// Skala badge: tumbuh melewati ukuran normal lalu kembali.
    public static float Pop(float t) {
        float clamped = Mathf.Clamp01(t);
        return clamped < PopPeakAt
            ? Mathf.Lerp(0f, PopOvershoot, EaseOut(clamped / PopPeakAt))
            : Mathf.Lerp(PopOvershoot, 1f, (clamped - PopPeakAt) / (1f - PopPeakAt));
    }

    /// Jarak judul dari posisi akhirnya: jatuh dari atas, lalu memantul sekali.
    public static float DropBounceOffset(float t) {
        float clamped = Mathf.Clamp01(t);
        if (clamped < DropLandAt) {
            float fall = clamped / DropLandAt;
            return DropStartOffset * (1f - fall * fall);
        }
        return DropBounceHeight * Mathf.Sin(Mathf.PI * (clamped - DropLandAt) / (1f - DropLandAt));
    }

    /// Skala judul: gepeng saat menyentuh posisi akhir lalu kembali normal.
    public static Vector3 DropBounceScale(float t) {
        float clamped = Mathf.Clamp01(t);
        if (clamped < DropLandAt) return Vector3.one;
        float recover = (clamped - DropLandAt) / (1f - DropLandAt);
        return new Vector3(Mathf.Lerp(1.08f, 1f, recover), Mathf.Lerp(0.86f, 1f, recover), 1f);
    }

    /// Posisi goyang layar (piksel mockup) untuk progres 0..1.
    public static Vector2 Shake(float t) {
        float position = Mathf.Clamp01(t) * (ShakeKeys.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(position), ShakeKeys.Length - 2);
        return Vector2.Lerp(ShakeKeys[index], ShakeKeys[index + 1], position - index);
    }
}
