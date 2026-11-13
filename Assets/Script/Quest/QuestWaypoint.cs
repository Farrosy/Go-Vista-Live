using System.Collections.Generic;
using UnityEngine;

/// Tujuan quest yang sedang aktif, untuk penunjuk arah di minimap dan rute GPS (GpsService / GpsRoute).
/// Hanya satu tujuan dalam satu waktu. Urutan pencarian:
///  1. Override manual lewat Set (untuk adegan khusus dari kode). Dilepas lagi dengan Clear.
///  2. Quest yang dipin, lalu quest aktif lainnya (urutan Daftar Quest). Di tiap quest, tujuan pertama
///     yang bisa dikerjakan dan punya titik di dunia:
///     - GoToLocation  → QuestLocation dengan locationId yang sama (otomatis, tanpa setup tambahan).
///     - tipe lain     → titik yang didaftarkan lewat Register (QuestTargetPoint, CityTourStop, atau kode).
///     Kalau ada beberapa titik (beberapa tujuan bebas urutan, atau satu tujuan dengan beberapa area),
///     yang terdekat ke pemain saat tujuan dihitung yang dipilih.
/// Quest baru cukup memakai QuestLocation atau QuestTargetPoint; tidak perlu kode GPS tambahan.
public static class QuestWaypoint {
    private struct Point {
        public string QuestId;
        public string ObjectiveId;
        public Transform Transform;
    }

    private static readonly List<Point> Points = new List<Point>();
    private static Transform _override;
    private static Transform _resolved;
    private static bool _dirty = true;
    private static bool _subscribed;

    /// Tujuan aktif, atau null kalau tidak ada quest yang punya tujuan di dunia.
    public static Transform Target {
        get {
            if (_override != null) return _override;
            if (!_subscribed) Subscribe();
            // Titik yang hancur (mis. WorldScene di-unload saat pindah venue) dianggap tidak ada.
            if (_dirty || (_resolved == null && !ReferenceEquals(_resolved, null))) Resolve();
            return _resolved;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Points.Clear();
        _override = null;
        _resolved = null;
        _dirty = true;
        _subscribed = false;
    }

    /// Override manual: paksa tujuan ke objek ini sampai Clear dipanggil oleh pemilik yang sama.
    public static void Set(Transform target) => _override = target;

    /// Lepas override hanya kalau masih milik pemanggil, supaya tidak menghapus override baru milik objek lain.
    public static void Clear(Transform owner) {
        if (_override == owner) _override = null;
    }

    /// Daftarkan titik dunia untuk satu tujuan quest. GPS mengarah ke sini selama tujuan itu bisa dikerjakan.
    public static void Register(string questId, string objectiveId, Transform point) {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId) || point == null) return;
        Unregister(point);
        Points.Add(new Point { QuestId = questId, ObjectiveId = objectiveId, Transform = point });
        MarkDirty();
    }

    public static void Unregister(Transform point) {
        if (Points.RemoveAll(p => p.Transform == point) > 0) MarkDirty();
    }

    /// Hitung ulang tujuan di akses berikutnya (dipanggil saat quest atau titik berubah).
    public static void MarkDirty() => _dirty = true;

    private static void Subscribe() {
        _subscribed = true;
        QuestService.Changed += MarkDirty;
    }

    private static void Resolve() {
        _dirty = false;
        _resolved = null;
        if (ProgressService.Current == null) return;

        Vector3 from = PlayerController.Local != null ? PlayerController.Local.transform.position : Vector3.zero;
        foreach (QuestService.Entry entry in QuestService.GetActiveQuests()) {
            // Quest berurutan hanya punya satu tujuan yang bisa dikerjakan; quest bebas urutan memilih yang terdekat.
            Transform best = null;
            float bestDistance = float.MaxValue;
            QuestData quest = entry.Data;
            for (int i = 0; i < quest.objectives.Length; i++)
                if (QuestService.IsObjectiveAvailable(quest, entry.Progress, i))
                    FindPoint(quest, i, from, ref best, ref bestDistance);
            if (best == null) continue;
            _resolved = best;
            return;
        }
    }

    private static void FindPoint(QuestData quest, int index, Vector3 from, ref Transform best, ref float bestDistance) {
        string objectiveId = quest.ObjectiveId(index);
        foreach (Point p in Points) {
            if (p.Transform == null || p.QuestId != quest.Id || p.ObjectiveId != objectiveId) continue;
            Consider(p.Transform, from, ref best, ref bestDistance);
        }
        QuestObjective objective = quest.objectives[index];
        if (objective.type == QuestObjectiveType.GoToLocation)
            foreach (QuestLocation location in QuestLocation.All)
                if (location.LocationId == objective.locationId)
                    Consider(location.transform, from, ref best, ref bestDistance);
    }

    private static void Consider(Transform candidate, Vector3 from, ref Transform best, ref float bestDistance) {
        float distance = (candidate.position - from).sqrMagnitude;
        if (distance >= bestDistance) return;
        best = candidate;
        bestDistance = distance;
    }
}
