using System.Collections;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Networked singleton that owns the shared NPC crowd. Put it on a persistent scene object in
/// MainMultiplayerScene (next to PlayerManager / VenueTravelManager), with a RealtimeView whose
/// _components list registers this component. Needs its own RealtimeView because the roster-spawned
/// guard (NpcDirectorModel) must sync across clients.
///
/// Responsibilities:
///  • Elect the authority: the client with the lowest owner-clientID among avatars currently in the
///    World venue (avatar-scan — no second model). Only that client, while in World, does any work.
///  • Spawn the roster ONCE: the first authority instantiates N networked NpcAgent prefabs onto random
///    walkable navmesh points and moves them into MainMultiplayerScene (persistent — they must survive
///    WorldScene unloading when a client travels into a venue). A synced flag blocks a double-spawn.
///  • Migration: a client that becomes authority after the previous one leaves World / disconnects
///    RequestOwnership()s every existing NPC view, which flips their NpcAgent.ShouldSimulate true and
///    resumes the agents. Nothing destructive on losing authority — NPCs persist
///    (destroyWhenOwnerLeaves = false) and hold their last synced pose until someone resumes them.
///
/// Re-evaluated on room connect, on VenueChanged, on the roster flag changing, and on a low-frequency
/// timer (covers clients joining/leaving without a dedicated event). Replaces NpcDevSpawner in the
/// networked flow (that one stands down whenever a Realtime is present).
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class NpcDirector : RealtimeComponent<NpcDirectorModel>
{
    [Header("Roster")]
    [Tooltip("How many networked NPCs to spawn. Owner chose 120 to match single-client density; watch bandwidth (Phase 5).")]
    [SerializeField] private int count = 120;
    [Tooltip("Crowd character prefab names (in Resources) spawned at random per NPC. Empty = the default 4-way set. Normcore replicates each NPC's chosen prefab to all clients automatically, so no extra sync is needed.")]
    [SerializeField] private string[] npcPrefabNames;

    // The random 4-way crowd. Used when npcPrefabNames is left empty (e.g. the pre-existing scene
    // instance that was serialized before this field existed). Kept in sync with NpcDevSpawner.
    private static readonly string[] DefaultPrefabNames =
        { "NpcAgent_Jane", "NpcAgent_Sallyy", "NpcAgent_Fany", "NpcAgent_Chloe" };
    [Tooltip("Minimum spacing between spawned NPCs so they don't start life stacked.")]
    [SerializeField] private float minSpacing = 1.2f;

    [Header("Spawn area (Phase-1 walkable bounds — same as NpcDevSpawner)")]
    [SerializeField] private Vector2 xRange = new Vector2(61.8f, 269.3f);
    [SerializeField] private Vector2 zRange = new Vector2(86.8f, 281.1f);
    [Tooltip("Ground height random points drop from before snapping to the navmesh.")]
    [SerializeField] private float groundY = 23.2f;
    [Tooltip("NavMesh.SamplePosition search radius when snapping a random point onto the mesh.")]
    [SerializeField] private float sampleRadius = 3f;

    [Header("Scheduling")]
    [Tooltip("Seconds between authority re-evaluations (catches clients joining/leaving World). Kept low-frequency, not per-frame.")]
    [SerializeField] private float reEvalInterval = 1f;
    [Tooltip("How many NPCs the authority instantiates per frame when spawning the roster. Lower = smoother entry, roster takes longer to fill.")]
    [SerializeField] private int spawnPerFrame = 4;

    private Realtime _realtime;
    private RealtimeView _view;
    private Coroutine _loop;
    private Coroutine _spawnRoutine;
    private bool _spawning;

    void Awake()
    {
        _view = GetComponent<RealtimeView>();
    }

    void OnEnable()
    {
        _realtime = FindAnyObjectByType<Realtime>();
        if (_realtime != null) _realtime.didConnectToRoom += OnConnected;
        VenueTravelManager.VenueChanged += Evaluate;
        _loop = StartCoroutine(EvalLoop());
    }

    void OnDisable()
    {
        if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
        VenueTravelManager.VenueChanged -= Evaluate;
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        // StopCoroutine does not run the iterator's finally block, so clear the guard here too.
        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
        _spawning = false;
    }

    protected override void OnRealtimeModelReplaced(NpcDirectorModel previousModel, NpcDirectorModel currentModel)
    {
        if (previousModel != null) previousModel.rosterSpawnedDidChange -= OnRosterSpawnedChanged;
        if (currentModel != null)
        {
            currentModel.rosterSpawnedDidChange += OnRosterSpawnedChanged;
            Evaluate();
        }
    }

    private void OnConnected(Realtime realtime) => Evaluate();
    private void OnRosterSpawnedChanged(NpcDirectorModel m, bool value) => Evaluate();

    private IEnumerator EvalLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.1f, reEvalInterval));
        while (true)
        {
            yield return wait;
            Evaluate();
        }
    }

    /// Core: if we're the elected authority and in World, make sure the roster exists (spawn once) or
    /// resume it (claim ownership of the existing NPCs). Otherwise do nothing — we're not in charge.
    private void Evaluate()
    {
        if (_realtime == null || !_realtime.connected) return;
        if (model == null) return;
        if (!ComputeIsAuthority()) return;

        // We need to own the director view to write the roster-spawned guard. Ask, then act next tick.
        if (_view != null && !_view.isOwnedLocallyInHierarchy)
        {
            _view.RequestOwnership();
            return;
        }

        if (_spawning) return;   // a batched spawn is in progress; it finishes (or aborts) on its own

        if (!model.rosterSpawned)
        {
            // Flag BEFORE StartCoroutine: its first step runs synchronously and may re-enter Evaluate.
            _spawning = true;
            _spawnRoutine = StartCoroutine(SpawnRoster());
        }
        else
        {
            ResumeRoster();
        }
    }

    /// Authority = lowest owner-clientID among avatars currently in the World venue, and that's us,
    /// and we're in World. Reads each avatar's PlayerVenueSync (venue) + RealtimeView owner id.
    private bool ComputeIsAuthority()
    {
        if (VenueTravelManager.LocalVenue != VenueId.World) return false;

        int myId = _realtime.clientID;
        if (myId < 0) return false;

        int authorityId = int.MaxValue;
        var avatars = FindObjectsByType<PlayerVenueSync>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < avatars.Length; i++)
        {
            var sync = avatars[i];
            if (sync.Venue != VenueId.World) continue;
            var view = sync.GetComponent<RealtimeView>();
            if (view == null) continue;
            int owner = view.ownerIDInHierarchy;
            if (owner < 0) continue; // view not registered yet — skip until it is.
            if (owner < authorityId) authorityId = owner;
        }

        return authorityId != int.MaxValue && myId == authorityId;
    }

    /// Authority only: instantiate the crowd into the persistent MainMultiplayerScene.
    /// Spread over several frames (spawnPerFrame each) — instantiating the whole roster in one frame
    /// froze the game for a moment right after entering the world.
    /// Resumable: it only spawns what's missing (count − NPCs that already exist), and rosterSpawned is
    /// set only once the roster is complete. If this authority disconnects or loses authority mid-spawn,
    /// it stops, and the next authority tops the roster up instead of leaving it half-filled or doubling it.
    private IEnumerator SpawnRoster()
    {
        try
        {
            foreach (object step in SpawnMissingNpcs()) yield return step;
        }
        finally
        {
            _spawning = false;
            _spawnRoutine = null;
        }
    }

    private IEnumerable<object> SpawnMissingNpcs()
    {
        var options = new Realtime.InstantiateOptions
        {
            ownedByClient               = true,   // the elected authority owns each NPC
            preventOwnershipTakeover    = false,  // the next authority can RequestOwnership() on migration
            destroyWhenOwnerLeaves      = false,  // NPCs survive when the authority leaves World / disconnects
            destroyWhenLastClientLeaves = true,   // clean up the roster when the room empties
            useInstance                 = _realtime,
        };

        Scene persistent = gameObject.scene; // the director lives in MainMultiplayerScene.
        var rng = new System.Random();
        var placed = new List<Vector3>();
        for (int i = 0; i < NpcAgent.All.Count; i++)
            if (NpcAgent.All[i] != null) placed.Add(NpcAgent.All[i].transform.position);
        float minSpacingSqr = minSpacing * minSpacing;
        int existing = placed.Count;
        int spawned = 0;

        for (int i = existing; i < count; i++)
        {
            // Between batches the room may have dropped or another client may have become authority.
            if (!_realtime.connected || model == null || !ComputeIsAuthority())
            {
                Debug.Log($"[NpcDirector] Roster spawn stopped at {existing + spawned}/{count}; the next authority will finish it.", this);
                yield break;
            }
            if (!TrySampleWalkable(rng, minSpacingSqr, placed, out var pos)) continue;
            var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var go = Realtime.Instantiate(PickPrefabName(rng), pos, rot, options);
            if (go == null) continue;
            // Keep NPCs out of WorldScene so a client travelling into a venue (which unloads WorldScene)
            // doesn't destroy its copies. The baked navmesh is global, so the agent still paths fine.
            if (go.scene != persistent) SceneManager.MoveGameObjectToScene(go, persistent);
            placed.Add(pos);
            spawned++;
            if (spawned % Mathf.Max(1, spawnPerFrame) == 0) yield return null;
        }

        model.rosterSpawned = true;
        Debug.Log($"[NpcDirector] Spawned {spawned} networked NPCs ({existing} already existed, target {count}) into '{persistent.name}'.", this);
    }

    /// Authority after a migration (roster already exists): take ownership of every NPC so its agent
    /// resumes on this client. Freshly-spawned NPCs are already owned, so this is a no-op for them.
    private void ResumeRoster()
    {
        var all = NpcAgent.All;
        for (int i = 0; i < all.Count; i++)
        {
            var view = all[i] != null ? all[i].View : null;
            if (view != null && !view.isOwnedLocallyInHierarchy)
                view.RequestOwnership();
        }
    }

    /// Random character for one NPC. Normcore sends the chosen prefab name in the instantiate message,
    /// so every client spawns the same character for that NPC with no extra synced state.
    private string PickPrefabName(System.Random rng)
    {
        var names = (npcPrefabNames != null && npcPrefabNames.Length > 0) ? npcPrefabNames : DefaultPrefabNames;
        return names[rng.Next(names.Length)];
    }

    private bool TrySampleWalkable(System.Random rng, float minSpacingSqr, List<Vector3> placed, out Vector3 result)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float x = Mathf.Lerp(xRange.x, xRange.y, (float)rng.NextDouble());
            float z = Mathf.Lerp(zRange.x, zRange.y, (float)rng.NextDouble());
            // The int arg is the area mask, not an agent-type id — pass AllAreas (same as NpcDevSpawner).
            if (!NavMesh.SamplePosition(new Vector3(x, groundY, z), out var hit, sampleRadius, NavMesh.AllAreas))
                continue;
            bool tooClose = false;
            for (int i = 0; i < placed.Count; i++)
                if ((placed[i] - hit.position).sqrMagnitude < minSpacingSqr) { tooClose = true; break; }
            if (tooClose) continue;
            result = hit.position;
            return true;
        }
        result = Vector3.zero;
        return false;
    }
}
