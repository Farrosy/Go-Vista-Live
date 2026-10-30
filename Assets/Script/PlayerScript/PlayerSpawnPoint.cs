using UnityEngine;

/// Taruh di scene world (mis. WorldScene), cukup satu per scene. Saat pemain menekan MULAI, avatar selalu spawn di sini
/// (bukan di posisi terakhir). Arah biru (forward) objek ini = arah hadap pemain saat spawn.
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Pemain disebar melingkar di sekitar titik ini supaya tidak saling tumpuk (meter). 0 = semua di titik yang sama.")]
    [SerializeField] private float spreadRadius = 2f;

    /// Spawn point yang aktif saat ini (scene world di-load additive, jadi dicari lewat static).
    public static PlayerSpawnPoint Active { get; private set; }

    void OnEnable() => Active = this;

    void OnDisable()
    {
        if (Active == this) Active = null;
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
