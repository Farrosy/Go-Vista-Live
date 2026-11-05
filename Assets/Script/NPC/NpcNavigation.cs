using UnityEngine;
using UnityEngine.AI;

/// Pasang di root NPC. Membuat NPC berjalan sendiri di atas NavMesh:
/// - Waypoints : patroli lewat titik-titik yang ditentukan (Loop / PingPong / Random)
/// - RandomWander : jalan acak di sekitar posisi awal dalam radius tertentu
/// Otomatis berhenti saat dialog dimulai (DialogueTrigger) dan lanjut lagi setelah selesai.
/// Animator: buat parameter Float "Speed" (0 = idle, >0.1 = jalan) untuk transisi Idle <-> Walk.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcNavigation : MonoBehaviour
{
    public enum MoveMode { Waypoints, RandomWander }
    public enum WaypointOrder { Loop, PingPong, Random }

    [Header("Mode")]
    [SerializeField] private MoveMode mode = MoveMode.RandomWander;

    [Header("Waypoints")]
    [Tooltip("Titik tujuan NPC (pakai Empty GameObject)")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private WaypointOrder order = WaypointOrder.Loop;

    [Header("Random Wander")]
    [Tooltip("Radius jalan acak dari posisi awal NPC")]
    [SerializeField] private float wanderRadius = 10f;
    [Tooltip("Jarak minimal tujuan baru dari posisi sekarang")]
    [SerializeField] private float minWanderDistance = 2f;

    [Header("Gerakan")]
    [SerializeField] private float walkSpeed = 1.5f;
    [Tooltip("Lama diam di setiap titik (detik, min-max)")]
    [SerializeField] private Vector2 waitTime = new Vector2(1.5f, 4f);
    [Tooltip("Jeda setelah dialog selesai sebelum jalan lagi")]
    [SerializeField] private float resumeDelay = 1f;

    [Header("Animasi")]
    [Tooltip("Kosong = otomatis cari di child")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float animationDamp = 0.1f;

    private NavMeshAgent agent;
    private DialogueTrigger dialogue;
    private NpcController npcController;

    private Vector3 homePosition;
    private int waypointIndex = -1;
    private int pingPongDirection = 1;
    private float waitTimer;
    private float resumeTimer;
    private bool isTalking;
    private int speedHash;
    private bool hasSpeedParam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        dialogue = GetComponent<DialogueTrigger>();
        npcController = GetComponent<NpcController>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        agent.speed = walkSpeed;
        agent.autoBraking = true;

        speedHash = Animator.StringToHash(speedParameter);
        if (animator)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float)
                    hasSpeedParam = true;
            if (!hasSpeedParam)
                Debug.LogWarning($"[NpcNavigation] Animator '{animator.name}' tidak punya parameter Float '{speedParameter}'. Animasi jalan tidak akan diputar.", this);
        }
    }

    void OnEnable()
    {
        if (!dialogue) return;
        dialogue.DialogueStarted += HandleDialogueStarted;
        dialogue.DialogueEnded += HandleDialogueEnded;
    }

    void OnDisable()
    {
        if (!dialogue) return;
        dialogue.DialogueStarted -= HandleDialogueStarted;
        dialogue.DialogueEnded -= HandleDialogueEnded;
    }

    void Start()
    {
        // Pastikan NPC berdiri di atas NavMesh.
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 3f, agent.areaMask))
            agent.Warp(hit.position);

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[NpcNavigation] NPC tidak berada di atas NavMesh. Sudah bake NavMesh Surface?", this);
            enabled = false;
            return;
        }

        homePosition = transform.position;
        waitTimer = Random.Range(0f, waitTime.y); // supaya NPC tidak jalan serempak
    }

    void Update()
    {
        UpdateAnimation();

        if (isTalking) return;

        // Tunggu NpcController selesai memutar badan kembali, lalu jeda sebentar.
        if (resumeTimer > 0f)
        {
            if (npcController && npcController.IsBusy) return;
            resumeTimer -= Time.deltaTime;
            if (resumeTimer > 0f) return;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        if (agent.pathPending) return;

        bool arrived = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f;
        if (!arrived) return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        if (TryGetNextDestination(out var dest))
            agent.SetDestination(dest);

        waitTimer = Random.Range(waitTime.x, waitTime.y);
    }

    /// Dipanggil NpcObstacleAvoidance saat jalan terhalang total: langsung pilih tujuan lain.
    public void PickNewDestination()
    {
        if (isTalking || !agent.isOnNavMesh) return;
        if (TryGetNextDestination(out var dest)) agent.SetDestination(dest);
        else agent.ResetPath();
        waitTimer = 0f;
    }

    private bool TryGetNextDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (mode == MoveMode.Waypoints && waypoints != null && waypoints.Length > 0)
        {
            waypointIndex = NextWaypointIndex();
            var wp = waypoints[waypointIndex];
            if (!wp) return false;
            return SampleOnNavMesh(wp.position, 2f, out destination);
        }

        // Random wander (juga fallback kalau waypoint kosong)
        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = homePosition + new Vector3(r.x, 0f, r.y);
            if ((candidate - transform.position).sqrMagnitude < minWanderDistance * minWanderDistance) continue;
            if (!SampleOnNavMesh(candidate, 2f, out var point)) continue;

            // Pastikan benar-benar bisa dicapai (bukan pulau NavMesh terpisah).
            var path = new NavMeshPath();
            if (agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                destination = point;
                return true;
            }
        }
        return false;
    }

    private int NextWaypointIndex()
    {
        int count = waypoints.Length;
        if (count == 1) return 0;

        switch (order)
        {
            case WaypointOrder.Random:
                int next;
                do next = Random.Range(0, count); while (next == waypointIndex);
                return next;

            case WaypointOrder.PingPong:
                if (waypointIndex < 0) return 0;
                if (waypointIndex + pingPongDirection >= count || waypointIndex + pingPongDirection < 0)
                    pingPongDirection = -pingPongDirection;
                return waypointIndex + pingPongDirection;

            default: // Loop
                return (waypointIndex + 1) % count;
        }
    }

    private bool SampleOnNavMesh(Vector3 position, float maxDistance, out Vector3 result)
    {
        if (NavMesh.SamplePosition(position, out var hit, maxDistance, agent.areaMask))
        {
            result = hit.position;
            return true;
        }
        result = position;
        return false;
    }

    private void UpdateAnimation()
    {
        if (!hasSpeedParam) return;
        float speed = agent.isOnNavMesh && !agent.isStopped ? agent.velocity.magnitude : 0f;
        animator.SetFloat(speedHash, speed, animationDamp, Time.deltaTime);
    }

    private void HandleDialogueStarted()
    {
        isTalking = true;
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updateRotation = false; // rotasi diambil alih NpcController (menghadap player)
    }

    private void HandleDialogueEnded()
    {
        isTalking = false;
        resumeTimer = Mathf.Max(resumeDelay, 0.01f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        if (mode == MoveMode.RandomWander)
        {
            Vector3 center = Application.isPlaying ? homePosition : transform.position;
            Gizmos.DrawWireSphere(center, wanderRadius);
        }
        else if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!waypoints[i]) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.25f);
                var next = waypoints[(i + 1) % waypoints.Length];
                if (next && (order == WaypointOrder.Loop || i < waypoints.Length - 1))
                    Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
