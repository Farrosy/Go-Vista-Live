using System.Collections.Generic;
using UnityEngine;

/// Slot pemunculan di dalam sebuah venue. Satu venue bisa punya beberapa slot
/// (mis. WorldScene punya CentralPlaza untuk spawn awal dan Docks untuk kembali dari venue).
public enum SpawnSlot
{
    CentralPlaza = 0,
    Docks = 1,
    VenueCube = 2,
}

/// Taruh di scene venue (WorldScene, Demo_DanceClub_01, Demo_RooftopBar_01). Setiap titik menandai
/// satu (Venue, Slot). Saat travel/MULAI, avatar dipindah ke titik yang cocok. Arah biru (forward)
/// objek ini = arah hadap pemain saat spawn.
///
/// Kompatibel mundur: titik lama tanpa data (field baru default ke World/CentralPlaza) tetap jadi
/// spawn awal WorldScene seperti sebelumnya.
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Venue tempat titik ini berada.")]
    [SerializeField] private VenueId venue = VenueId.World;

    [Tooltip("Slot pemunculan di dalam venue (spawn awal, kembali ke dermaga, cube venue, dst.).")]
    [SerializeField] private SpawnSlot slot = SpawnSlot.CentralPlaza;

    [Tooltip("Pemain disebar melingkar di sekitar titik ini supaya tidak saling tumpuk (meter). 0 = semua di titik yang sama.")]
    [SerializeField] private float spreadRadius = 2f;

    public VenueId Venue => venue;
    public SpawnSlot Slot => slot;

    /// Registry semua titik spawn yang sedang aktif, dikunci lewat (Venue, Slot).
    /// Scene di-load additive, jadi titik dicari lewat static ini, bukan referensi langsung.
    private static readonly Dictionary<(VenueId, SpawnSlot), PlayerSpawnPoint> Registry =
        new Dictionary<(VenueId, SpawnSlot), PlayerSpawnPoint>();

    void OnEnable()
    {
        var key = (venue, slot);
        if (Registry.TryGetValue(key, out var existing) && existing != null && existing != this)
        {
            Debug.LogWarning(
                $"[PlayerSpawnPoint] Titik spawn ganda untuk {venue}/{slot} di '{name}'. Menimpa '{existing.name}'.",
                this);
        }
        Registry[key] = this;
    }

    void OnDisable()
    {
        var key = (venue, slot);
        if (Registry.TryGetValue(key, out var current) && current == this)
            Registry.Remove(key);
    }

    /// Cari titik spawn aktif untuk (venue, slot) tertentu. Null kalau tidak ada yang ter-load.
    public static PlayerSpawnPoint Find(VenueId venue, SpawnSlot slot)
    {
        Registry.TryGetValue((venue, slot), out var spawnPoint);
        return spawnPoint;
    }

    /// Posisi spawn untuk client tertentu, disebar pakai golden angle supaya tiap client dapat tempat sendiri.
    public Vector3 GetPosition(int clientID)
    {
        if (clientID <= 0 || spreadRadius <= 0f) return transform.position;

        float angle = clientID * 2.39996323f;
        float radius = spreadRadius * Mathf.Sqrt(clientID) * 0.5f;
        return transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
    }
}
