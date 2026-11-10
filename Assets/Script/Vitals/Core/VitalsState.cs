using System;

public enum OxygenLevel { Normal, Warning, Danger }

/// Kondisi HP dan O₂ pemain. Immutable: setiap perubahan menghasilkan nilai baru.
public readonly struct VitalsState {
    public static readonly VitalsState Full =
        new VitalsState(VitalsRules.MaxHealth, VitalsRules.OxygenDurationSeconds, DeathCause.None);

    public int Health { get; }
    private readonly float _oxygenSeconds;
    private readonly DeathCause _lastDamageSource;

    private VitalsState(int health, float oxygenSeconds, DeathCause lastDamageSource) {
        Health = health;
        _oxygenSeconds = oxygenSeconds;
        _lastDamageSource = lastDamageSource;
    }

    public float HealthFraction => (float)Health / VitalsRules.MaxHealth;
    public float OxygenFraction => _oxygenSeconds / VitalsRules.OxygenDurationSeconds;
    public bool IsHealthCritical => HealthFraction < VitalsRules.HealthCriticalFraction;
    public bool IsOxygenFull => _oxygenSeconds >= VitalsRules.OxygenDurationSeconds - VitalsRules.Epsilon;
    public bool IsDead => Health <= 0 || _oxygenSeconds <= VitalsRules.Epsilon;

    /// HP habis mengikuti sumber damage terakhir, O₂ habis berarti tenggelam.
    public DeathCause Cause {
        get {
            if (Health <= 0) return _lastDamageSource;
            return _oxygenSeconds <= VitalsRules.Epsilon ? DeathCause.Drowned : DeathCause.None;
        }
    }

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
        return new VitalsState(Health, seconds, _lastDamageSource);
    }

    public VitalsState ApplyDamage(int amount, DeathCause source) {
        return new VitalsState(Math.Max(0, Health - Math.Max(0, amount)), _oxygenSeconds, source);
    }
}
