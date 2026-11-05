using UnityEngine;
using UnityEngine.AI;

/// Pasang di root NPC (bersama NavMeshAgent). Membuat kaki NPC benar-benar napak di tanah.
///
/// NavMeshAgent menaruh NPC di atas permukaan NavMesh, bukan di atas collider tanah. NavMesh
/// adalah perkiraan (voxel) dari tanah, jadi permukaannya bisa beberapa cm–puluhan cm di atas
/// tanah asli, apalagi di jalan/trotoar yang tidak rata. Hasilnya NPC terlihat melayang.
/// Komponen ini raycast ke collider tanah tiap frame dan mengatur agent.baseOffset supaya
/// pivot (telapak kaki) NPC menempel ke tanah asli. Jalurnya tetap mengikuti NavMesh.
///
/// Tips: centang "Build Height Mesh" di NavMeshSurface lalu Bake ulang supaya NavMesh sendiri
/// lebih akurat. Komponen ini tetap berguna sebagai penyelaras akhir.
[RequireComponent(typeof(NavMeshAgent))]
[DefaultExecutionOrder(1000)]
public class NpcGroundSnap : MonoBehaviour
{
    [Tooltip("Layer yang dianggap tanah. Default: semua")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Mulai raycast dari sekian meter di atas permukaan NavMesh")]
    [SerializeField] private float probeUp = 0.6f;
    [Tooltip("Koreksi maksimal ke bawah/atas (meter). Lebih dari ini dianggap bukan tanah yang sama")]
    [SerializeField] private float maxCorrection = 0.5f;
    [Tooltip("Permukaan dengan normal.y di bawah ini (dinding) diabaikan")]
    [Range(0f, 1f)] [SerializeField] private float minGroundNormalY = 0.5f;
    [Tooltip("Kecepatan penyesuaian tinggi (lebih besar = lebih cepat)")]
    [SerializeField] private float smoothing = 15f;
    [Tooltip("Geser tambahan (mis. -0.02 kalau sepatu sedikit melayang)")]
    [SerializeField] private float extraOffset = 0f;

    private NavMeshAgent agent;
    private Collider[] ownColliders;
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private float offset;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        offset = agent.baseOffset;
    }

    void LateUpdate()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // Tinggi permukaan NavMesh di posisi agent (posisi transform dikurangi offset yang sedang dipakai).
        float navY = transform.position.y - agent.baseOffset;
        Vector3 origin = new Vector3(transform.position.x, navY + probeUp, transform.position.z);

        float target = 0f;
        if (TryFindGround(origin, probeUp + maxCorrection, out float groundY))
            target = Mathf.Clamp(groundY - navY, -maxCorrection, maxCorrection);
        target += extraOffset;

        offset = Mathf.Lerp(offset, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        agent.baseOffset = offset;
    }

    private bool TryFindGround(Vector3 origin, float distance, out float groundY)
    {
        groundY = 0f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.normal.y < minGroundNormalY) continue;
            if (IsOwn(h.collider) || h.collider.GetComponentInParent<PlayerController>()) continue;
            // Permukaan pertama dari atas = tanah tempat berdiri.
            if (h.distance < best) { best = h.distance; groundY = h.point.y; found = true; }
        }
        return found;
    }

    private bool IsOwn(Collider c)
    {
        foreach (var own in ownColliders)
            if (own == c) return true;
        return c.transform.IsChildOf(transform);
    }
}
