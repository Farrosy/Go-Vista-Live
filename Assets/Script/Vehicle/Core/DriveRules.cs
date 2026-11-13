using System;

/// Aturan menyetir mobil pemain (gas/rem/mundur, batas kecepatan, sudut setir). Murni matematika supaya bisa
/// dites tanpa Unity, dan semua angka penyetelan ada di satu tempat.
public static class DriveRules {
    public const float KmhPerMetrePerSecond = 3.6f;

    /// Di bawah kecepatan ini (m/s) mobil dianggap berhenti: pedal rem berubah jadi mundur, pedal gas saat
    /// mundur berubah jadi maju.
    public const float StoppedSpeed = 0.5f;

    /// Pemain hanya boleh keluar kalau mobil hampir diam (m/s).
    public const float ExitMaxSpeed = 1f;

    /// Mulai dari persentase batas kecepatan ini, tenaga mesin dikurangi perlahan sampai nol di batasnya.
    public const float LimiterStart = 0.85f;

    /// Roda setir UI paling jauh bisa diputar sejauh ini (derajat) ke kiri/kanan.
    public const float WheelMaxAngle = 135f;

    /// Ubah pedal pemain (0..1) jadi tenaga mesin dan rem.
    /// drive: -1..1 (negatif = mundur). brake: 0..1.
    /// forwardSpeed: kecepatan searah depan mobil (m/s), negatif saat mundur.
    public static void ResolvePedals(float gas, float brakePedal, float forwardSpeed, out float drive, out float brake) {
        gas = Clamp01(gas);
        brakePedal = Clamp01(brakePedal);
        drive = 0f;
        brake = 0f;

        // Dua pedal diinjak bersamaan: rem menang.
        if (gas > 0f && brakePedal > 0f) {
            brake = Math.Max(gas, brakePedal);
            return;
        }
        if (gas > 0f) {
            if (forwardSpeed < -StoppedSpeed) brake = gas;   // masih mundur: rem dulu sampai berhenti
            else drive = gas;
            return;
        }
        if (brakePedal > 0f) {
            if (forwardSpeed > StoppedSpeed) brake = brakePedal;   // masih maju: rem
            else drive = -brakePedal;                              // sudah berhenti: mundur
        }
    }

    /// Pengali tenaga mesin: 1 sampai LimiterStart dari batas kecepatan, turun linear ke 0 tepat di batasnya.
    public static float LimiterFactor(float speed, float maxSpeed) {
        if (maxSpeed <= 0f) return 0f;
        float ratio = Math.Abs(speed) / maxSpeed;
        if (ratio <= LimiterStart) return 1f;
        return Clamp01((1f - ratio) / (1f - LimiterStart));
    }

    /// Sudut belok roda (derajat). Makin kencang, makin kecil sudut maksimalnya supaya tidak mudah terguling/selip.
    public static float SteerAngle(float steerInput, float speed, float maxSpeed, float lowSpeedAngle, float highSpeedAngle) {
        float t = maxSpeed > 0f ? Clamp01(Math.Abs(speed) / maxSpeed) : 0f;
        float limit = lowSpeedAngle + (highSpeedAngle - lowSpeedAngle) * t;
        return Clamp(steerInput, -1f, 1f) * limit;
    }

    /// Torsi per roda (Nm) supaya mobil bermassa mass (kg) mendapat percepatan accel (m/s²).
    public static float WheelTorque(float mass, float accel, float wheelRadius, int wheelCount) {
        if (wheelCount <= 0) return 0f;
        return mass * accel * wheelRadius / wheelCount;
    }

    public static bool CanExit(float speed) => Math.Abs(speed) < ExitMaxSpeed;

    /// Sudut roda setir UI (derajat, positif = kanan) jadi input setir -1..1.
    public static float WheelAngleToSteer(float angle) => Clamp(angle / WheelMaxAngle, -1f, 1f);

    /// Selisih sudut terpendek dari a ke b (derajat), -180..180. Dipakai saat jari memutar roda setir melewati ±180°.
    public static float DeltaAngle(float a, float b) {
        float d = (b - a) % 360f;
        if (d > 180f) d -= 360f;
        if (d < -180f) d += 360f;
        return d;
    }

    public static float ToKmh(float metresPerSecond) => metresPerSecond * KmhPerMetrePerSecond;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
