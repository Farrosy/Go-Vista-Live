using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dynamic prototype pedestrian. Drives a plain capsule to roam the confined "Pedestrian"
/// NavMesh (sidewalks + park only) <b>fully on its own</b> — no authored destinations, no
/// waypoints. Deliberately kept to the <b>standard NavMeshAgent model</b>: the agent moves itself
/// along its path and uses Unity's built-in local avoidance (RVO) to steer around other agents.
///
/// Why this (vs. a hand-rolled separation/steering layer): RVO is reciprocal and velocity-aware,
/// so a crowd doesn't permanently deadlock; it's a fraction of the code; and because the agent
/// <b>faces its own velocity</b> (`updateRotation`), a plain forward walk animation drops straight
/// in with no foot-sliding. When the real character art replaces the capsule, add an Animator with
/// a <c>Speed</c> float (already wired below) feeding an idle/walk/run blend tree from
/// <c>agent.velocity.magnitude</c> — the agent drives position, the clip plays "in place".
///
/// What this script owns: picking natural roam targets (purposeful heading-biased wander + varied
/// dwell), staying on the mesh, and the animation Speed hook. Avoidance/path-following are the
/// agent's job. Un-networked on purpose (single-client); Phase 4 wraps authority gating around this
/// with no movement change. Attach to NpcAgent.prefab (NavMeshAgent must use the Pedestrian type).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcAgent : MonoBehaviour
{
    [Header("Pace (per-NPC, randomized in Awake)")]
    [Tooltip("Walking speed is rolled once per NPC inside this range so the crowd isn't a lockstep march.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.9f, 1.6f);
    [Tooltip("How quickly the agent speeds up / eases to a stop (m/s^2). Lower = softer starts and stops.")]
    [SerializeField] private float acceleration = 3.5f;
    [Tooltip("Turn rate (deg/s). Moderate so the NPC leans into a turn (and so facing matches a walk anim) rather than snapping.")]
    [SerializeField] private float angularSpeed = 300f;
    [SerializeField] private float stoppingDistance = 0.3f;

    [Header("Roaming")]
    [Tooltip("Farthest the next stroll target can be chosen from the current position.")]
    [SerializeField] private float roamRadius = 14f;
    [Tooltip("Nearest the next target can be, so the NPC actually travels rather than shuffling in place.")]
    [SerializeField] private float minStepDistance = 4f;
    [Tooltip("Half-angle of the cone (deg) the next target is usually picked within, around the current heading. Keeps walking purposeful.")]
    [SerializeField] private float headingConeDeg = 70f;
    [Tooltip("Chance a given pick ignores the heading cone and turns anywhere (so NPCs don't only ever drift one way).")]
    [Range(0f, 1f)]
    [SerializeField] private float wideTurnChance = 0.15f;
    [Tooltip("Prefer targets at least this far from the nearest NavMesh edge, so NPCs aim for open pavement, not the brink.")]
    [SerializeField] private float targetEdgeMargin = 1.5f;

    [Header("Dwell")]
    [Tooltip("Chance the NPC actually pauses on arrival (a proper rest) vs. just rounding the corner and strolling on.")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseChance = 0.35f;
    [Tooltip("Rest duration when it does pause (seconds, min-max).")]
    [SerializeField] private Vector2 pauseTime = new Vector2(1.5f, 5f);
    [Tooltip("Tiny pause when it doesn't rest, so turns aren't perfectly robotic (seconds, min-max).")]
    [SerializeField] private Vector2 strollPause = new Vector2(0f, 0.6f);

    [Header("Avoidance (Unity built-in RVO)")]
    [Tooltip("Reciprocal local avoidance quality. Good is the usual crowd choice; drop to Medium/Low on the authority device if 100+ agents cost too much (Phase 5).")]
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidance = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;
    [Tooltip("Agent radius; a gap-keeping value so two capsules route around each other.")]
    [SerializeField] private float agentRadius = 0.3f;
    [SerializeField] private float agentHeight = 1.8f;
    [Tooltip("Per-NPC avoidancePriority is randomized in this range so symmetric standoffs resolve (lower number = higher priority, gets right of way).")]
    [SerializeField] private Vector2Int avoidancePriorityRange = new Vector2Int(20, 80);

    [Header("Robustness")]
    [Tooltip("Search radius used to snap back onto the NavMesh if displaced off it.")]
    [SerializeField] private float resampleRadius = 3f;
    [Tooltip("Safety net: if an NPC wants to move but is near-stationary this long (s), re-pick a fresh target. RVO rarely locks, but this guarantees recovery.")]
    [SerializeField] private float stuckResetTime = 2f;

    [Header("Animation (no-op on the capsule prototype)")]
    [Tooltip("Empty = auto-search children. When real art is in, point a Speed float param at a locomotion blend tree.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float animationDamp = 0.1f;

    private NavMeshAgent agent;
    private float waitTimer;
    private float nextDwell;       // dwell to apply on arrival at the target just chosen
    private float stuckTimer;
    private int speedHash;
    private bool hasSpeedParam;
    private NavMeshPath reusablePath; // created in Awake (NavMeshPath can't be built in a field initializer)

    // ---- Phase 4 authority gating (movement above is untouched) ----
    // This NPC only runs its NavMeshAgent on the ONE client that is its Normcore authority and is
    // currently in the World venue. Every other client disables the agent and lets RealtimeTransform
    // drive the transform (and NpcSync drive the animation). Single-client / DevScene (no Realtime in
    // the scene) always simulates, so Phase-3 behaviour is unchanged.
    private NpcSync npcSync;       // ownership/model-ready source; null-safe
    private RealtimeView realtimeView; // this NPC's view; used by NpcDirector to claim ownership on migration
    private bool simulating;       // current gate state (false until resolved → networked remotes stay put)

    /// Live registry of every NPC instance (networked or dev), mirroring PlayerSpawnPoint's pattern.
    /// NpcDirector iterates this on migration to RequestOwnership each NPC's view.
    public static readonly List<NpcAgent> All = new List<NpcAgent>();

    /// This NPC's RealtimeView (null in the single-client DevScene flow is still fine — it just isn't used).
    public RealtimeView View => realtimeView;

    // Resolved once: is a Normcore Realtime present in the loaded scenes? If not, we're in the
    // single-client DevScene flow (NpcDevSpawner) and every NPC simulates, exactly as before. This
    // mirrors how NpcDevSpawner itself decides whether the networked stack is live, and keys off the
    // global flow rather than per-object registration timing. -1 = unknown, 0 = none, 1 = present.
    private static int _realtimePresence = -1;

    private static bool IsNetworked
    {
        get
        {
            if (_realtimePresence < 0)
                _realtimePresence = FindAnyObjectByType<Realtime>() != null ? 1 : 0;
            return _realtimePresence == 1;
        }
    }

    // Reset the cached presence flag on entering play mode (survives "no domain reload").
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetNetworkStatics() => _realtimePresence = -1;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        npcSync = GetComponent<NpcSync>();
        realtimeView = GetComponent<RealtimeView>();
        reusablePath = new NavMeshPath();
        if (!animator) animator = GetComponentInChildren<Animator>();

        // Per-NPC pace so the crowd isn't a lockstep march.
        agent.speed = Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.radius = agentRadius;
        agent.height = agentHeight;

        // Let the agent move AND rotate itself: facing its velocity is what lets a plain forward
        // walk animation match the movement (no manual velocity override, no foot-sliding).
        agent.updatePosition = true;
        agent.updateRotation = true;

        // Built-in reciprocal avoidance + a varied priority so symmetric standoffs break.
        agent.obstacleAvoidanceType = obstacleAvoidance;
        int lo = Mathf.Clamp(Mathf.Min(avoidancePriorityRange.x, avoidancePriorityRange.y), 0, 99);
        int hi = Mathf.Clamp(Mathf.Max(avoidancePriorityRange.x, avoidancePriorityRange.y), 0, 99);
        agent.avoidancePriority = Random.Range(lo, hi + 1);

        speedHash = Animator.StringToHash(speedParameter);
        if (animator)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float)
                    hasSpeedParam = true;
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        // Resolve the gate up front so a networked remote never runs its agent for even one frame
        // (the prefab's NavMeshAgent defaults enabled). ApplySimulating is forced here regardless of
        // the starting state; after this, Update only re-applies on a flip.
        ApplySimulating(ComputeShouldSimulate());
    }

    void Update()
    {
        // Re-evaluate the gate every frame (covers venue changes AND ownership/migration) and only
        // run the enter/exit side-effects on an actual flip. Cheap: a couple of bool reads per NPC.
        bool target = ComputeShouldSimulate();
        if (target != simulating) ApplySimulating(target);

        // Not our NPC to drive: RealtimeTransform moves the transform, NpcSync drives the animation.
        if (!simulating) return;

        UpdateAnimation();

        // If pushed/warped off the mesh at runtime, snap back before pathing.
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, resampleRadius, agent.areaMask))
                agent.Warp(hit.position);
            if (!agent.isOnNavMesh) return;
        }

        if (!agent.pathPending)
        {
            bool arrived = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            if (arrived)
            {
                if (waitTimer > 0f) waitTimer -= Time.deltaTime;
                else
                {
                    if (TryPickWanderTarget(out var dest)) agent.SetDestination(dest);
                    waitTimer = nextDwell;
                }
            }
        }

        // Safety net: if it wants to move but is pinned near-still (a rare tight-spot RVO standoff),
        // re-pick a target to break the symmetry. This changes its path direction and frees it.
        bool traveling = !agent.pathPending && agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.2f;
        if (traveling && agent.velocity.sqrMagnitude < 0.0036f) // < 0.06 m/s
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckResetTime)
            {
                if (TryPickWanderTarget(out var d)) agent.SetDestination(d);
                stuckTimer = 0f;
            }
        }
        else stuckTimer = 0f;
    }

    /// <summary>
    /// Should THIS client simulate THIS NPC right now? True when either (a) we're single-client /
    /// DevScene (no Realtime present — behaves exactly like Phase 3), or (b) we're the Normcore
    /// authority for this NPC (own it, model ready) and the local player is in the World venue.
    /// False until the model is ready, so a freshly-received remote NPC stays put rather than fighting
    /// RealtimeTransform.
    /// </summary>
    private bool ComputeShouldSimulate()
    {
        if (!IsNetworked) return true;                          // DevScene / offline: unchanged.
        if (npcSync == null || !npcSync.IsAuthority) return false; // not owner (or model not ready).
        return VenueTravelManager.LocalVenue == VenueId.World;  // authority only simulates while in World.
    }

    /// Enter/exit simulation. On enter: snap onto the navmesh then enable the agent (gotcha: a running
    /// agent and RealtimeTransform must never both write the transform). On exit: disable the agent and
    /// let RealtimeTransform drive.
    private void ApplySimulating(bool target)
    {
        simulating = target;
        if (target) EnterSimulation();
        else if (agent) agent.enabled = false;
    }

    private void EnterSimulation()
    {
        if (!agent.enabled) agent.enabled = true;

        // Ensure we're on the mesh before pathing (we may be entering from a RealtimeTransform-driven
        // pose after a migration, or from a fresh spawn).
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, resampleRadius, agent.areaMask))
            agent.Warp(hit.position);

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[NpcAgent] Not on a NavMesh (is the Pedestrian surface baked and is this a walkable spawn?). Disabling.", this);
            enabled = false;
            return;
        }

        waitTimer = Random.Range(0f, pauseTime.y); // desync so NPCs don't all set off together
    }

    /// <summary>
    /// Pick the next roam target: usually ahead within a heading cone (purposeful walking), sometimes
    /// a wider turn, always a reachable spot on this island, preferring a margin off the mesh edge.
    /// Also rolls the dwell. Falls back to any reachable nearby point so a tight island never traps it.
    /// </summary>
    private bool TryPickWanderTarget(out Vector3 destination)
    {
        destination = transform.position;

        Vector3 heading = Flatten(agent.velocity);
        if (heading.sqrMagnitude < 0.01f) heading = Flatten(transform.forward);
        heading.Normalize();

        nextDwell = Random.value < pauseChance
            ? Random.Range(Mathf.Min(pauseTime.x, pauseTime.y), Mathf.Max(pauseTime.x, pauseTime.y))
            : Random.Range(Mathf.Min(strollPause.x, strollPause.y), Mathf.Max(strollPause.x, strollPause.y));

        // Preferred: a reachable target ahead, a margin off the edge.
        for (int i = 0; i < 18; i++)
        {
            float angle = Random.value < wideTurnChance
                ? Random.Range(-180f, 180f)
                : Random.Range(-headingConeDeg, headingConeDeg);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * heading;
            float dist = Random.Range(minStepDistance, roamRadius);
            Vector3 candidate = transform.position + dir * dist;

            if (SampleReachable(candidate, 2f, out destination) && EdgeClearance(destination) >= targetEdgeMargin)
                return true;
        }

        // Fallback: any reachable nearby point (edge margin relaxed for tight islands / dead-ends).
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = transform.position + new Vector3(r.x, 0f, r.y);
            if ((candidate - transform.position).sqrMagnitude < minStepDistance * minStepDistance) continue;
            if (SampleReachable(candidate, 2f, out destination)) return true;
        }
        return false;
    }

    /// Sample onto the NavMesh AND confirm a complete path (not a disconnected island).
    private bool SampleReachable(Vector3 position, float maxDistance, out Vector3 result)
    {
        result = position;
        if (!NavMesh.SamplePosition(position, out var hit, maxDistance, agent.areaMask)) return false;
        if (!agent.CalculatePath(hit.position, reusablePath)) return false;
        if (reusablePath.status != NavMeshPathStatus.PathComplete) return false;
        result = hit.position;
        return true;
    }

    /// Distance from a point to the nearest NavMesh edge (large when well inside the walkable area).
    private float EdgeClearance(Vector3 position)
    {
        if (NavMesh.FindClosestEdge(position, out var edge, agent.areaMask)) return edge.distance;
        return 0f;
    }

    private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    private void UpdateAnimation()
    {
        if (!hasSpeedParam) return;
        float speed = agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
        animator.SetFloat(speedHash, speed, animationDamp, Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, roamRadius);
    }
}
