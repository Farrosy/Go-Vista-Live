using System;

public enum OxygenLevel { Normal, Warning, Danger }

/// Kondisi HP dan O₂ pemain. Immutable: setiap perubahan menghasilkan nilai baru.
public readonly struct VitalsState {
    public static readonly VitalsState Full =
        new VitalsState(VitalsRules.MaxHealth, VitalsRules.OxygenDurationSeconds);

    public int Health { get; }
    private readonly float _oxygenSeconds;

    private VitalsState(int health, float oxygenSeconds) {
        Health = health;
        _oxygenSeconds = oxygenSeconds;
    }

    public float HealthFraction => (float)Health / VitalsRules.MaxHealth;
    public float OxygenFraction => _oxygenSeconds / VitalsRules.OxygenDurationSeconds;
    public bool IsHealthCritical => HealthFraction < VitalsRules.HealthCriticalFraction;
    public bool IsOxygenFull => _oxygenSeconds >= VitalsRules.OxygenDurationSeconds - VitalsRules.Epsilon;
    public bool IsDead => Health <= 0 || _oxygenSeconds <= VitalsRules.Epsilon;

    /// Jumlah segmen yang masih menyala: berkurang satu begitu satu detik penuh terpakai.
    public int OxygenSegments {
        get {
            float secondsPerSegment = VitalsRules.OxygenDurationSeconds / VitalsRules.OxygenSegmentCount;
            return (int)Math.Ceiling(_oxygenSeconds / secondsPerSegment - VitalsRules.Epsilon);
        }
    }

    public OxygenLevel Level {
        get {
            float fraction = OxygenFraction;
            if (fraction <= VitalsRules.OxygenDangerFraction + VitalsRules.Epsilon) return OxygenLevel.Danger;
            if (fraction <= VitalsRules.OxygenWarningFraction + VitalsRules.Epsilon) return OxygenLevel.Warning;
            return OxygenLevel.Normal;
        }
    }

    public VitalsState TickOxygen(float deltaSeconds, bool headUnderWater) {
        float refillPerSecond = VitalsRules.OxygenDurationSeconds / VitalsRules.OxygenRefillSeconds;
        float change = headUnderWater ? -deltaSeconds : deltaSeconds * refillPerSecond;
        float seconds = Math.Max(0f, Math.Min(VitalsRules.OxygenDurationSeconds, _oxygenSeconds + change));
        return new VitalsState(Health, seconds);
    }

    public VitalsState ApplyDamage(int amount) {
        return new VitalsState(Math.Max(0, Health - Math.Max(0, amount)), _oxygenSeconds);
    }
}
