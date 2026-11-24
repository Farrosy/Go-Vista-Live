using System.Collections.Generic;
using UnityEngine;

/// Area tujuan quest "Pergi ke lokasi". Pasang pada objek dengan Collider (otomatis dijadikan trigger),
/// lalu isi locationId yang sama dengan QuestObjective.locationId.
/// GPS dan marker minimap otomatis mengarah ke sini selama tujuan GoToLocation-nya aktif (lihat QuestWaypoint).
[RequireComponent(typeof(Collider))]
public sealed class QuestLocation : MonoBehaviour {
    [Tooltip("ID lokasi, mis. 'pasar-baru'. Samakan dengan locationId di tujuan QuestData.")]
    [SerializeField] private string locationId;

    private static readonly List<QuestLocation> Occupied = new List<QuestLocation>();
    private static readonly List<QuestLocation> Enabled = new List<QuestLocation>();
    // Toleransi (m) saat memastikan pemain benar-benar di dalam collider area.
    private const float InsideTolerance = 0.05f;

    private Collider _area;

    public string LocationId => locationId;

    /// Semua QuestLocation yang aktif di scene yang sedang dimuat.
    public static IReadOnlyList<QuestLocation> All => Enabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Occupied.Clear();
        Enabled.Clear();
    }

    /// <summary>True kalau pemain lokal sedang berdiri di salah satu area dengan ID ini.</summary>
    /// Catatan masuk/keluar trigger dicek ulang dengan posisi pemain: teleport (mis. pindah venue lewat pintu) tidak
    /// selalu memicu OnTriggerExit, jadi tanpa cek ini area yang sudah ditinggalkan bisa tetap dianggap ditempati.
    public static bool IsLocalPlayerInside(string id) {
        if (string.IsNullOrEmpty(id)) return false;
        PlayerController player = PlayerController.Local;
        foreach (QuestLocation location in Occupied)
            if (location.locationId == id && (player == null || location.Contains(player.transform.position))) return true;
        return false;
    }

    private bool Contains(Vector3 point) =>
        _area == null || (_area.ClosestPoint(point) - point).sqrMagnitude <= InsideTolerance * InsideTolerance;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void Awake() {
        if (string.IsNullOrEmpty(locationId)) Debug.LogWarning("[Quest] QuestLocation tanpa locationId.", this);
        _area = GetComponent<Collider>();
        _area.isTrigger = true;
    }

    private void OnEnable() {
        Enabled.Add(this);
        QuestWaypoint.MarkDirty();
    }

    private void OnDisable() {
        Occupied.Remove(this);
        Enabled.Remove(this);
        QuestWaypoint.MarkDirty();
    }

    private void OnTriggerEnter(Collider other) {
        if (!IsLocalPlayer(other) || Occupied.Contains(this)) return;
        Occupied.Add(this);
        QuestService.ReportLocationReached(locationId);
    }

    private void OnTriggerExit(Collider other) {
        if (IsLocalPlayer(other)) Occupied.Remove(this);
    }

    private static bool IsLocalPlayer(Collider other) {
        var player = other.GetComponentInParent<PlayerController>();
        return player != null && player == PlayerController.Local;
    }
}
