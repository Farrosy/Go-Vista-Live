using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Normal.Realtime;

/// <summary>
/// DEV-ONLY, un-networked crowd spawner for single-client testing of the dynamic pedestrian
/// prototype. Scatters a packed crowd of <c>NpcAgent</c> prefabs onto random walkable
/// Pedestrian-navmesh samples when WorldScene is opened standalone and played. Because the mesh
/// is disconnected into islands by the roads, random samples land roughly in proportion to island
/// area, so the whole populated city reads as busy. Each NPC then roams its island on its own.
///
/// This is a testing aid — it is REPLACED by the networked <c>NpcDirector</c> in Phase 4, which
/// owns the real shared roster (authority-simulated + synced). It is deliberately inert in the
/// actual game:
///   • Editor-only — self-destructs in a player build (<see cref="Application.isEditor"/>).
///   • Bails if a Normcore <see cref="Realtime"/> exists in the loaded scenes (the multiplayer flow
///     loads WorldScene additively under MainMultiplayerScene, which carries Realtime), so it never
///     spawns per-client ghost crowds.
/// Safe to leave in WorldScene, but it only does anything when you open WorldScene by itself and
/// press Play.
/// </summary>
[DisallowMultipleComponent]
public class NpcDevSpawner : MonoBehaviour
{
    [Header("Crowd")]
    [Tooltip("How many NPCs to try to spawn across the walkable navmesh (packed crowd).")]
    [SerializeField] private int count = 120;

    [Tooltip("Crowd characters picked at random per NPC (the 4 variant prefabs). Empty = auto-load the default 4-way set from Resources.")]
    [SerializeField] private GameObject[] npcPrefabs;

    [Tooltip("Optional single explicit prefab (legacy). Used only when the list above is empty.")]
    [SerializeField] private GameObject npcPrefab;

    // The random 4-way crowd. Kept in sync with NpcDirector.DefaultPrefabNames.
    private static readonly string[] DefaultPrefabNames =
        { "NpcAgent_Jane", "NpcAgent_Sallyy", "NpcAgent_Fany", "NpcAgent_Chloe" };

    [Tooltip("Minimum spacing between spawned NPCs so they don't start life stacked inside each other.")]
    [SerializeField] private float minSpacing = 1.2f;

    [Header("Spawn area (Phase-1 walkable bounds)")]
    [Tooltip("World-X range to scatter spawn points across.")]
    [SerializeField] private Vector2 xRange = new Vector2(61.8f, 269.3f);
    [Tooltip("World-Z range to scatter spawn points across.")]
    [SerializeField] private Vector2 zRange = new Vector2(86.8f, 281.1f);
    [Tooltip("Ground height the random points are dropped from before snapping to the navmesh.")]
    [SerializeField] private float groundY = 23.2f;
    [Tooltip("NavMesh.SamplePosition search radius when snapping a random point onto the mesh.")]
    [SerializeField] private float sampleRadius = 3f;

    [Header("Scheduling (same rule as NpcDirector)")]
    [Tooltip("Maximum NPCs instantiated in one frame (also capped by Spawn Budget Ms).")]
    [SerializeField] private int spawnPerFrame = 4;
    [Tooltip("Milliseconds per frame that may be spent instantiating NPCs. At least one NPC per frame.")]
    [SerializeField] private float spawnBudgetMs = 4f;

    [Header("Determinism")]
    [Tooltip("0 = fully random each run; any other value seeds the layout so runs are repeatable.")]
    [SerializeField] private int randomSeed = 0;

    private readonly List<Vector3> placed = new List<Vector3>();

    IEnumerator Start()
    {
        // Never in a build.
        if (!Application.isEditor)
        {
            Destroy(gameObject);
            yield break;
        }

        // Never when the networked stack is live — Phase-4 NpcDirector owns the crowd there.
        if (FindAnyObjectByType<Realtime>() != null)
        {
            Debug.Log("[NpcDevSpawner] Realtime present — standing down (NpcDirector owns the crowd in multiplayer).", this);
            yield break;
        }

        var pool = BuildPool();
        if (pool.Length == 0)
        {
            Debug.LogWarning("[NpcDevSpawner] No prefabs (assign the crowd variants or keep them in Assets/Resources).", this);
            yield break;
        }

        var rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        float minSpacingSqr = minSpacing * minSpacing;
        int spawned = 0;
        int spawnedThisFrame = 0;
        var frameTimer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            if (!SpawnBudget.CanSpawnMore(spawnedThisFrame, frameTimer.Elapsed.TotalMilliseconds, spawnBudgetMs, spawnPerFrame))
            {
                yield return null;
                spawnedThisFrame = 0;
                frameTimer.Restart();
            }
            if (!TrySampleWalkable(rng, minSpacingSqr, out var pos)) continue;
            var prefab = pool[rng.Next(pool.Length)]; // random 4-way character
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), transform);
            go.name = "NpcAgent_Dev_" + i;
            DisableNetworking(go);
            placed.Add(pos);
            spawned++;
            spawnedThisFrame++;
        }
        Debug.Log($"[NpcDevSpawner] Spawned {spawned}/{count} dev NPCs onto the Pedestrian navmesh.", this);
    }

    /// The crowd prefab carries networking components (RealtimeView/RealtimeTransform/NpcSync) for the
    /// real NpcDirector flow. The dev spawner only runs when NO Realtime is present, so on these plain
    /// GameObject.Instantiate'd copies those components have no model and spam "RealtimeView model is
    /// null" every frame. Switch them off — NpcAgent drives the transform + animation directly in the
    /// single-client flow, so nothing else is needed.
    private static void DisableNetworking(GameObject go)
    {
        var rt = go.GetComponent<RealtimeTransform>(); if (rt) rt.enabled = false;
        var ns = go.GetComponent<NpcSync>();           if (ns) ns.enabled = false;
        var rv = go.GetComponent<RealtimeView>();       if (rv) rv.enabled = false;
    }

    /// Resolve the random crowd pool: the inspector list if set, else a single legacy prefab, else
    /// the default 4-way set loaded from Resources.
    private GameObject[] BuildPool()
    {
        var list = new List<GameObject>();
        if (npcPrefabs != null && npcPrefabs.Length > 0)
        {
            foreach (var p in npcPrefabs) if (p != null) list.Add(p);
            if (list.Count > 0) return list.ToArray();
        }
        if (npcPrefab != null) return new[] { npcPrefab };
        foreach (var n in DefaultPrefabNames)
        {
            var g = Resources.Load<GameObject>(n);
            if (g != null) list.Add(g);
        }
        return list.ToArray();
    }

    private bool TrySampleWalkable(System.Random rng, float minSpacingSqr, out Vector3 result)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float x = Mathf.Lerp(xRange.x, xRange.y, (float)rng.NextDouble());
            float z = Mathf.Lerp(zRange.x, zRange.y, (float)rng.NextDouble());
            // NavMesh.AllAreas, not an agent-type id — the int arg here is the area mask.
            if (!NavMesh.SamplePosition(new Vector3(x, groundY, z), out var hit, sampleRadius, NavMesh.AllAreas))
                continue;
            if (TooClose(hit.position, minSpacingSqr)) continue;
            result = hit.position;
            return true;
        }
        result = Vector3.zero;
        return false;
    }

    private bool TooClose(Vector3 candidate, float minSpacingSqr)
    {
        for (int i = 0; i < placed.Count; i++)
            if ((placed[i] - candidate).sqrMagnitude < minSpacingSqr) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Vector3 center = new Vector3((xRange.x + xRange.y) * 0.5f, groundY, (zRange.x + zRange.y) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(xRange.y - xRange.x), 0.1f, Mathf.Abs(zRange.y - zRange.x));
        Gizmos.DrawWireCube(center, size);
    }
}
