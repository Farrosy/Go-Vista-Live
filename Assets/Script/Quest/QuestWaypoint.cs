using UnityEngine;

/// Tujuan quest yang sedang aktif untuk penunjuk arah (beacon di dunia sudah ada di objeknya sendiri,
/// ini untuk marker di minimap). Hanya satu tujuan dalam satu waktu.
public static class QuestWaypoint {
    public static Transform Target { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Target = null;

    public static void Set(Transform target) => Target = target;

    /// Lepas tujuan hanya kalau masih milik pemanggil, supaya tidak menghapus tujuan baru milik objek lain.
    public static void Clear(Transform owner) {
        if (Target == owner) Target = null;
    }
}
