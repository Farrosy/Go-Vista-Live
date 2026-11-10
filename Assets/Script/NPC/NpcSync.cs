using Normal.Realtime;
using UnityEngine;
using UnityEngine.AI;

/// Attach to NpcAgent.prefab (next to NavMeshAgent + NpcAgent + RealtimeView + RealtimeTransform).
/// MUST be registered in the prefab's RealtimeView._components list (same as PlayerAnimSync on the
/// avatar prefab) or it will never sync.
///
/// The authority (the client that owns this NPC and is simulating it) writes the agent's walking
/// speed into the model. Every other client reads that synced speed and drives its Animator from it,
/// because RealtimeTransform only carries position/rotation/scale — not animation. This is the hook
/// the real character art plugs into (idle/walk/run blend tree); on the capsule prototype there is no
/// Speed parameter so the remote branch is a harmless no-op. Mirrors PlayerAnimSync.
///
/// Authority gating of the NavMeshAgent itself lives in NpcAgent; this component only moves the
/// animation Speed across the wire.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcSync : RealtimeComponent<NpcSyncModel>
{
    // Speed changes smaller than this aren't sent, so a standing-still NPC generates no traffic.
    private const float SpeedEpsilon = 0.01f;

    [Tooltip("Float parameter on the Animator driven on REMOTE clients from the synced speed. Capsule prototype has none; that's fine.")]
    [SerializeField] private string speedParameter = "Speed";
    [Tooltip("Damp time for the remote Speed parameter (matches NpcAgent's local damping).")]
    [SerializeField] private float animationDamp = 0.1f;

    private NavMeshAgent agent;
    private Animator animator;
    private int speedHash;
    private bool hasSpeedParam;

    /// True only once the Normcore model exists AND this client owns the NPC. Reading ownership
    /// before the model is ready throws, so the model-null guard comes first (mirrors
    /// PlayerVenueSync.IsLocalAvatar). NpcAgent uses this to decide whether to run the agent.
    public bool IsAuthority => model != null && isOwnedLocallyInHierarchy;

    /// True once the Normcore model has been handed to this component (ownership is readable).
    public bool IsModelReady => model != null;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        speedHash = Animator.StringToHash(speedParameter);
        if (animator)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float)
                    hasSpeedParam = true;
        }
    }

    void Update()
    {
        if (model == null) return; // offline / DevScene: no Realtime, model never arrives — do nothing.

        if (isOwnedLocallyInHierarchy)
        {
            // Authority: publish the agent's speed. NpcAgent drives this client's own Animator; we
            // only write the synced value here (don't touch the Animator, to avoid double-driving it).
            float speed = agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            if (Mathf.Abs(model.speed - speed) > SpeedEpsilon) model.speed = speed;
        }
        else
        {
            // Remote: the NavMeshAgent is disabled on this client (NpcAgent gates it off), so the
            // Animator has no local velocity to read — drive it from the synced speed instead.
            if (hasSpeedParam) animator.SetFloat(speedHash, model.speed, animationDamp, Time.deltaTime);
        }
    }
}
