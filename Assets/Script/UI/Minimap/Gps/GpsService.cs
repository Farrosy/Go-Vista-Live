using System;
using UnityEngine;

public enum GpsDestinationKind {
    None,
    /// Tujuan quest aktif (QuestWaypoint.Target).
    Quest,
    /// Tujuan yang dipasang pemain di peta layar penuh.
    Custom,
}

/// Tujuan GPS pemain lokal, seperti GPS di GTA: tujuan yang dipasang sendiri di peta menang dari tujuan quest,
/// dan otomatis hilang saat pemain sampai. Tanpa tujuan sendiri, GPS mengikuti QuestWaypoint.Target.
/// Rute di radar dan peta penuh digambar oleh GpsRoute (di WorldScene) dari tujuan ini.
public static class GpsService {
    private static Vector3 _custom;
    private static bool _hasCustom;

    /// Dipanggil saat tujuan buatan pemain dipasang atau dilepas.
    public static event Action Changed;

    public static bool HasCustomWaypoint => _hasCustom;
    public static Vector3 CustomWaypoint => _custom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        _hasCustom = false;
        _custom = Vector3.zero;
        Changed = null;
    }

    public static void SetCustomWaypoint(Vector3 position) {
        _custom = position;
        _hasCustom = true;
        Changed?.Invoke();
    }

    public static void ClearCustomWaypoint() {
        if (!_hasCustom) return;
        _hasCustom = false;
        Changed?.Invoke();
    }

    /// Tujuan yang sedang dipandu GPS. False kalau tidak ada.
    public static bool TryGetDestination(out Vector3 position, out GpsDestinationKind kind) {
        if (_hasCustom) {
            position = _custom;
            kind = GpsDestinationKind.Custom;
            return true;
        }
        Transform quest = QuestWaypoint.Target;
        if (quest != null) {
            position = quest.position;
            kind = GpsDestinationKind.Quest;
            return true;
        }
        position = default;
        kind = GpsDestinationKind.None;
        return false;
    }
}
