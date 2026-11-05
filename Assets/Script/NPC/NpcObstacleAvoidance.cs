using UnityEngine;
using UnityEngine.AI;

/// Pasang di root NPC (bersama NavMeshAgent + NpcNavigation).
/// NavMesh sudah menghindari geometri yang ikut di-bake, tapi objek yang TIDAK ada di NavMesh
/// (props baru, objek bergerak, NPC lain, dll.) tetap bisa ditabrak. Komponen ini "melihat" ke depan
/// dengan SphereCast searah gerak NPC; kalau ada obstacle, NPC membelok ke sisi yang kosong SEBELUM menabrak.
/// Player sengaja diabaikan.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcObstacleAvoidance : MonoBehaviour
{
    [Header("Deteksi")]
    [Tooltip("Layer yang dianggap obstacle. Default: semua")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Jarak lihat ke depan minimum (meter)")]
    [SerializeField] private float lookAhead = 1.5f;
    [Tooltip("Tambahan jarak lihat per m/s kecepatan")]
    [SerializeField] private float lookAheadPerSpeed = 0.6f;
    [Tooltip("Tinggi titik deteksi, relatif ke tinggi agent (0.5 = pinggang)")]
    [Range(0.1f, 0.9f)] [SerializeField] private float probeHeight = 0.5f;
    [Tooltip("Permukaan dengan normal.y di atas ini dianggap lantai/tangga, bukan obstacle")]
    [Range(0f, 1f)] [SerializeField] private float groundNormalY = 0.7f;
    [Tooltip("Berapa kali per detik melakukan deteksi")]
    [SerializeField] private float sensorRate = 15f;

    [Header("Menghindar")]
    [Tooltip("Sudut belok yang dicoba (derajat), dari kecil ke besar")]
    [SerializeField] private float[] probeAngles = { 25f, 45f, 65f, 90f };
    [Tooltip("Seberapa cepat arah gerak diubah (m/s²)")]
    [SerializeField] private float steerAcceleration = 8f;
    [Tooltip("Lama tetap di sisi menghindar setelah jalan bersih, supaya tidak zig-zag")]
    [SerializeField] private float holdSideTime = 0.4f;
    [Tooltip("Kalau terhalang total selama ini (detik), NPC memilih tujuan lain")]
    [SerializeField] private float stuckTimeout = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private NavMeshAgent agent;
    private NpcNavigation navigation;
    private Collider[] ownColliders;
    private readonly RaycastHit[] hits = new RaycastHit[8];

    private float sensorTimer;
    private bool obstacleAhead;
    private bool fullyBlocked;
    private Vector3 avoidDirection;
    private int lastSide;          // -1 kiri, +1 kanan, 0 belum ada
    private float clearTimer;
    private float blockedTimer;
    private Vector3 lastHitPoint;
    // Our own steering velocity. The agent blends its velocity back toward the path every step, so
    // blending from agent.velocity would cancel out; we integrate here and overwrite agent.velocity.
    private Vector3 steerVelocity;
    private bool steering;

    /// True selama NPC sedang membelok menghindari sesuatu.
    public bool IsAvoiding => obstacleAhead;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        navigation = GetComponent<NpcNavigation>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    void Update()
    {
        if (!agent.isOnNavMesh || agent.isStopped || !agent.hasPath || agent.pathPending)
        {
            ResetState();
            return;
        }

        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.01f) { ResetState(); return; }

        sensorTimer -= Time.deltaTime;
        if (sensorTimer <= 0f)
        {
            sensorTimer = 1f / Mathf.Max(sensorRate, 1f);
            Sense(desired.normalized);
        }

        if (!obstacleAhead) { steering = false; return; }
        if (!steering) { steering = true; steerVelocity = agent.velocity; }

        if (fullyBlocked)
        {
            // Tidak ada sisi kosong: pelankan sampai berhenti, lalu cari tujuan lain.
            steerVelocity = Vector3.MoveTowards(steerVelocity, Vector3.zero, steerAcceleration * Time.deltaTime);
            agent.velocity = steerVelocity;
            blockedTimer += Time.deltaTime;
            if (blockedTimer >= stuckTimeout)
            {
                blockedTimer = 0f;
                ResetState();
                if (navigation) navigation.PickNewDestination();
                else agent.ResetPath();
            }
            return;
        }

        blockedTimer = 0f;
        Vector3 target = avoidDirection * agent.speed;
        steerVelocity = Vector3.MoveTowards(steerVelocity, target, steerAcceleration * Time.deltaTime);
        agent.velocity = steerVelocity;
    }

    private void Sense(Vector3 forward)
    {
        Vector3 origin = ProbeOrigin();
        float radius = Mathf.Max(agent.radius * 0.9f, 0.05f);
        float distance = lookAhead + agent.velocity.magnitude * lookAheadPerSpeed;

        // Obstacle yang lebih jauh dari tujuan tidak perlu dihindari (mis. tembok di belakang titik tujuan).
        float maxRelevant = Mathf.Min(distance, agent.remainingDistance + radius);

        if (!CastForObstacle(origin, radius, forward, maxRelevant, out var hit))
        {
            // Jalan di depan bersih: tahan sebentar di sisi menghindar supaya tidak zig-zag.
            if (obstacleAhead)
            {
                clearTimer += 1f / Mathf.Max(sensorRate, 1f);
                if (clearTimer < holdSideTime && IsDirectionFree(origin, radius, avoidDirection, distance)) return;
            }
            ResetState();
            return;
        }

        clearTimer = 0f;
        lastHitPoint = hit.point;

        // Sisi yang diutamakan: menjauh dari titik tabrak. Kalau sebelumnya sudah memilih sisi, pertahankan.
        Vector3 toHit = hit.point - transform.position;
        float cross = forward.x * toHit.z - forward.z * toHit.x; // >0: obstacle di kiri
        int preferred = lastSide != 0 ? lastSide : (cross > 0f ? 1 : -1);

        foreach (float angle in probeAngles)
        {
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? preferred : -preferred;
                Vector3 dir = Quaternion.AngleAxis(angle * side, Vector3.up) * forward;
                if (!IsDirectionFree(origin, radius, dir, distance)) continue;

                obstacleAhead = true;
                fullyBlocked = false;
                avoidDirection = dir;
                lastSide = side;
                return;
            }
        }

        obstacleAhead = true;
        fullyBlocked = true;
    }

    private bool IsDirectionFree(Vector3 origin, float radius, Vector3 dir, float distance)
    {
        if (CastForObstacle(origin, radius, dir, distance, out _)) return false;

        // Jangan belok keluar NavMesh (tepi jalan, jurang, dll.).
        Vector3 from = transform.position;
        Vector3 to = from + dir * Mathf.Min(distance, 1.5f);
        return !NavMesh.Raycast(from, to, out _, agent.areaMask);
    }

    private bool CastForObstacle(Vector3 origin, float radius, Vector3 dir, float distance, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.SphereCastNonAlloc(origin, radius, dir, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f) continue;                 // sudah overlap sejak awal (biasanya collider sendiri)
            if (h.normal.y > groundNormalY) continue;       // lantai / tangga / slope
            if (IsOwnCollider(h.collider)) continue;
            if (IsPlayer(h.collider)) continue;
            if (h.distance < best) { best = h.distance; nearest = h; found = true; }
        }
        return found;
    }

    private bool IsOwnCollider(Collider c)
    {
        foreach (var own in ownColliders)
            if (own == c) return true;
        return c.transform.IsChildOf(transform);
    }

    private static bool IsPlayer(Collider c) => c.GetComponentInParent<PlayerController>() != null;

    private Vector3 ProbeOrigin() => transform.position + Vector3.up * (agent.height * probeHeight);

    private void ResetState()
    {
        obstacleAhead = false;
        fullyBlocked = false;
        lastSide = 0;
        clearTimer = 0f;
        blockedTimer = 0f;
        steering = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        var a = agent ? agent : GetComponent<NavMeshAgent>();
        if (!a) return;

        Vector3 origin = transform.position + Vector3.up * (a.height * probeHeight);
        Vector3 fwd = Application.isPlaying && a.desiredVelocity.sqrMagnitude > 0.01f
            ? Vector3.ProjectOnPlane(a.desiredVelocity, Vector3.up).normalized
            : transform.forward;
        float dist = lookAhead + (Application.isPlaying ? a.velocity.magnitude * lookAheadPerSpeed : 0f);

        Gizmos.color = obstacleAhead ? (fullyBlocked ? Color.red : new Color(1f, 0.6f, 0f)) : Color.green;
        Gizmos.DrawLine(origin, origin + fwd * dist);
        Gizmos.DrawWireSphere(origin + fwd * dist, a.radius * 0.9f);

        if (Application.isPlaying && obstacleAhead)
        {
            Gizmos.DrawSphere(lastHitPoint, 0.08f);
            if (!fullyBlocked)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(origin, origin + avoidDirection * dist);
            }
        }
    }
}
