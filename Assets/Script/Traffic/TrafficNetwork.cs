using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITHappy;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Random = System.Random;

/// Runs once per client (it lives in WorldScene), but only one client simulates: the lowest clientID
/// among avatars in the World venue. That client publishes car snapshots through TrafficSync and the
/// others follow them, so everyone sees the same cars. Without a room connection it simulates alone.
public sealed class TrafficNetwork : MonoBehaviour
{
    public const int EpochMask = 0x7F;

    private const int k_BytesPerCar = 6;
    private const byte k_HiddenFlag = 0x80;
    private const ushort k_NoRoute = ushort.MaxValue;
    private const float k_DistanceScale = 10f;
    private const float k_SpeedScale = 10f;
    private const float k_SpawnSampleStep = 10f;
    private const float k_SpawnEndMargin = 40f;
    private const float k_SpawnClearance = 20f;
    private const float k_FirstSpawnMinDistance = 30f;
    private const int k_MaxMovesPerTick = 3;
    private const int k_MaxOcclusionChecks = 12;

    [Header("Route building")]
    [SerializeField] private float sampleStep = 2f;
    [SerializeField] private float mergeDistance = 1f;
    [SerializeField, Range(0f, 1f)] private float minTangentDot = 0.9f;
    [SerializeField] private float minRouteLength = 45f;

    [Header("Traffic")]
    [SerializeField] private List<GameObject> carPrefabs = new();
    [SerializeField] private int maxCars = 14;
    [SerializeField] private Vector2 speedRange = new Vector2(6f, 9f);
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float brake = 35f;
    [SerializeField] private float lookAhead = 12f;
    [SerializeField] private float stopDistance = 4f;
    [SerializeField] private Transform carRoot;
    [SerializeField] private Transform trafficCenter;
    [SerializeField] private Vector3 fallbackCenter = new Vector3(159.078674f, 23.2f, 94.08188f);
    [SerializeField] private int randomSeed = 0;

    [Header("Follow players")]
    [Tooltip("Cars are moved to a route point at least this far from every player.")]
    [SerializeField] private float spawnMinDistance = 80f;
    [Tooltip("Cars are moved to a route point at most this far from the player who needs them.")]
    [SerializeField] private float spawnMaxDistance = 170f;
    [Tooltip("Cars further than this from all players can be moved to a player who has too few.")]
    [SerializeField] private float releaseDistance = 250f;
    [SerializeField] private float rebalanceInterval = 1f;
    [SerializeField] private float publishInterval = 0.2f;

    [Header("Scene")]
    [SerializeField] private bool addRoadColliders = true;
    [SerializeField] private bool drawRoutes = true;

    private readonly List<TrafficRoute> routes = new();
    private readonly List<TrafficCar> cars = new();
    private readonly List<SpawnSample> spawnSamples = new();
    private readonly List<Vector3> anchors = new();
    private Random random;
    private int routeHash;
    private bool isAuthority;
    private bool warnedRouteMismatch;
    private byte[] lastState;
    private float nextRebalance;
    private float nextPublish;

    public IReadOnlyList<TrafficRoute> Routes => routes;
    public IReadOnlyList<TrafficCar> Cars => cars;

    /// Lalu lintas yang sedang berjalan. Null kalau scene jalan raya tidak dimuat.
    public static TrafficNetwork Active { get; private set; }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void Awake()
    {
        if (addRoadColliders) AddRoadColliders();
    }

    private void Start()
    {
        random = randomSeed == 0 ? new Random(Guid.NewGuid().GetHashCode()) : new Random(randomSeed);

        if (carPrefabs == null || carPrefabs.Count == 0)
        {
            Debug.LogError("[Traffic] No car prefabs assigned. Run Go Vista > Setup Demo Traffic.");
            return;
        }

        routes.Clear();
        routes.AddRange(BuildRoutes(sampleStep, mergeDistance, minTangentDot, minRouteLength, out int sectionCount));
        if (routes.Count == 0)
        {
            Debug.LogError("[Traffic] No lane routes found. Check that the scene contains RoadSection splines.");
            return;
        }

        routeHash = ComputeRouteHash();
        BuildSpawnSamples();
        LogReport(sectionCount);
        Debug.Log($"[Traffic] Route conflicts: {TrafficConflictBuilder.Build(routes)}.");
    }

    private void Update()
    {
        if (routes.Count == 0) return;

        TrafficSync sync = TrafficSync.Instance;
        bool online = sync != null && sync.IsReady;
        int authorityClient = RefreshAnchors(online);
        bool authority = !online || (authorityClient >= 0 && authorityClient == sync.LocalClientId);

        if (cars.Count == 0 && !TrySpawnCars(online, authority, sync)) return;

        if (authority != isAuthority)
        {
            // A new authority continues from the last known state rather than a stale local one.
            if (authority && online) ApplyLatestState(sync);
            SetAuthority(authority);
        }

        if (authority)
        {
            if (Time.time >= nextRebalance)
            {
                nextRebalance = Time.time + rebalanceInterval;
                Rebalance();
            }

            if (online && Time.time >= nextPublish)
            {
                nextPublish = Time.time + publishInterval;
                sync.PublishState(EncodeState());
            }
        }
        else if (online)
        {
            ApplyLatestState(sync);
        }

        if (online && !warnedRouteMismatch && sync.RouteHash != 0 && sync.RouteHash != routeHash)
        {
            warnedRouteMismatch = true;
            Debug.LogError($"[Traffic] Route list differs from the other clients ({routeHash} vs {sync.RouteHash}); cars will not line up.");
        }
    }

    private void SetAuthority(bool authority)
    {
        isAuthority = authority;
        for (int i = 0; i < cars.Count; i++) cars[i].SetSimulating(authority);
    }

    /// Fills anchors with the positions traffic should gather around and returns the authority's clientID.
    private int RefreshAnchors(bool online)
    {
        anchors.Clear();
        int lowest = -1;

        IReadOnlyList<PlayerVenueSync> players = PlayerVenueSync.All;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerVenueSync player = players[i];
            if (player == null || player.Venue != VenueId.World) continue;

            int owner = player.OwnerClientId;
            if (online && owner < 0) continue;

            anchors.Add(player.transform.position);
            if (owner >= 0 && (lowest < 0 || owner < lowest)) lowest = owner;
        }

        if (!online && anchors.Count == 0)
            anchors.Add(trafficCenter != null ? trafficCenter.position : fallbackCenter);

        return lowest;
    }

    private bool TrySpawnCars(bool online, bool authority, TrafficSync sync)
    {
        int seed;
        if (online)
        {
            seed = sync.Seed;
            if (seed == 0)
            {
                if (!authority) return false;
                seed = NewSeed();
                sync.PublishSetup(seed, routeHash);
            }
        }
        else
        {
            seed = randomSeed != 0 ? randomSeed : NewSeed();
        }

        var carRandom = new Random(seed);
        Transform root = carRoot != null ? carRoot : transform;
        for (int i = 0; i < maxCars; i++) SpawnCar(carRandom, root);

        SetAuthority(authority);
        if (online) ApplyLatestState(sync);
        return true;
    }

    private static int NewSeed()
    {
        int seed = Guid.NewGuid().GetHashCode();
        return seed != 0 ? seed : 1;
    }

    // ---------------------------------------------------------------- Follow players

    private void Rebalance()
    {
        if (anchors.Count == 0 || spawnSamples.Count == 0) return;

        int[] counts = new int[anchors.Count];
        int[] owners = new int[cars.Count];
        var free = new List<TrafficCar>();
        var unplaced = new List<TrafficCar>();

        for (int i = 0; i < cars.Count; i++)
        {
            TrafficCar car = cars[i];
            owners[i] = -1;
            if (car.Route == null)
            {
                unplaced.Add(car);
                continue;
            }

            int nearest = NearestAnchor(car.transform.position, out float distanceToAnchor);
            if (distanceToAnchor > releaseDistance)
            {
                free.Add(car);
                continue;
            }

            owners[i] = nearest;
            counts[nearest]++;
        }

        int moves = 0;
        while (true)
        {
            int needy = -1;
            int bestDeficit = 0;
            for (int a = 0; a < anchors.Count; a++)
            {
                int deficit = Quota(a) - counts[a];
                if (deficit > bestDeficit)
                {
                    bestDeficit = deficit;
                    needy = a;
                }
            }

            if (needy < 0) break;

            bool first = unplaced.Count > 0;
            if (!first && moves >= k_MaxMovesPerTick) break;

            TrafficCar donor = first ? unplaced[unplaced.Count - 1]
                : free.Count > 0 ? free[free.Count - 1]
                : TakeSurplusCar(owners, counts);
            if (donor == null) break;

            float minDistance = first ? k_FirstSpawnMinDistance : spawnMinDistance;
            if (!TryFindSpawn(needy, minDistance, donor, out TrafficRoute route, out float distance))
                break;

            if (first) unplaced.RemoveAt(unplaced.Count - 1);
            else if (free.Count > 0) free.RemoveAt(free.Count - 1);

            donor.Place(route, distance);
            counts[needy]++;
            moves++;
        }
    }

    private int Quota(int anchor)
    {
        int quota = cars.Count / anchors.Count;
        return anchor < cars.Count % anchors.Count ? quota + 1 : quota;
    }

    private int NearestAnchor(Vector3 position, out float distance)
    {
        int nearest = 0;
        float best = float.PositiveInfinity;
        for (int a = 0; a < anchors.Count; a++)
        {
            float sqr = (anchors[a] - position).sqrMagnitude;
            if (sqr < best)
            {
                best = sqr;
                nearest = a;
            }
        }

        distance = Mathf.Sqrt(best);
        return nearest;
    }

    /// The furthest car of an over-quota player, as long as no player is close to it.
    private TrafficCar TakeSurplusCar(int[] owners, int[] counts)
    {
        int bestIndex = -1;
        float bestDistance = spawnMinDistance;
        for (int i = 0; i < cars.Count; i++)
        {
            int owner = owners[i];
            if (owner < 0 || counts[owner] <= Quota(owner)) continue;

            NearestAnchor(cars[i].transform.position, out float distance);
            if (distance >= bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return null;

        counts[owners[bestIndex]]--;
        owners[bestIndex] = -1;
        return cars[bestIndex];
    }

    /// Picks a route point near the anchor, preferring one a building hides from every player.
    /// Other clients' cameras are unknown here, so sight lines are checked from the avatars.
    private bool TryFindSpawn(int anchor, float minDistance, TrafficCar self, out TrafficRoute route, out float distance)
    {
        route = null;
        distance = 0f;

        float minSqr = minDistance * minDistance;
        float maxSqr = spawnMaxDistance * spawnMaxDistance;
        float clearanceSqr = k_SpawnClearance * k_SpawnClearance;
        int offset = random.Next(spawnSamples.Count);
        int occlusionChecks = 0;
        bool found = false;

        for (int n = 0; n < spawnSamples.Count; n++)
        {
            SpawnSample sample = spawnSamples[(n + offset) % spawnSamples.Count];

            float anchorSqr = (sample.Position - anchors[anchor]).sqrMagnitude;
            if (anchorSqr < minSqr || anchorSqr > maxSqr) continue;
            if (!IsClear(sample.Position, minSqr, clearanceSqr, self)) continue;

            if (!found)
            {
                route = sample.Route;
                distance = sample.Distance;
                found = true;
            }

            if (IsHiddenFromAnchors(sample.Position))
            {
                route = sample.Route;
                distance = sample.Distance;
                return true;
            }

            if (++occlusionChecks >= k_MaxOcclusionChecks) break;
        }

        return found;
    }

    private bool IsClear(Vector3 position, float anchorMinSqr, float clearanceSqr, TrafficCar self)
    {
        for (int a = 0; a < anchors.Count; a++)
            if ((anchors[a] - position).sqrMagnitude < anchorMinSqr) return false;

        for (int i = 0; i < cars.Count; i++)
        {
            TrafficCar other = cars[i];
            if (other == self || other.Route == null) continue;
            if ((other.transform.position - position).sqrMagnitude < clearanceSqr) return false;
        }

        return true;
    }

    private bool IsHiddenFromAnchors(Vector3 position)
    {
        for (int a = 0; a < anchors.Count; a++)
        {
            if (!Physics.Linecast(anchors[a] + Vector3.up * 1.6f, position + Vector3.up, ~0, QueryTriggerInteraction.Ignore))
                return false;
        }
        return true;
    }

    private void BuildSpawnSamples()
    {
        spawnSamples.Clear();
        for (int r = 0; r < routes.Count; r++)
        {
            TrafficRoute route = routes[r];
            float end = route.Closed ? route.Length : route.Length - k_SpawnEndMargin;
            for (float d = 0f; d < end; d += k_SpawnSampleStep)
            {
                route.Evaluate(d, out Vector3 position, out _, out _);
                spawnSamples.Add(new SpawnSample(route, d, position));
            }
        }
    }

    // ---------------------------------------------------------------- Snapshot

    private byte[] EncodeState()
    {
        // Normcore keeps a reference to the array it was given, so each publish needs its own.
        byte[] state = new byte[cars.Count * k_BytesPerCar];

        for (int i = 0; i < cars.Count; i++)
        {
            TrafficCar car = cars[i];
            int o = i * k_BytesPerCar;
            ushort route = car.Route != null ? (ushort)car.Route.Index : k_NoRoute;
            ushort distance = (ushort)Mathf.Clamp(Mathf.RoundToInt(car.Distance * k_DistanceScale), 0, ushort.MaxValue);

            state[o] = (byte)route;
            state[o + 1] = (byte)(route >> 8);
            state[o + 2] = (byte)distance;
            state[o + 3] = (byte)(distance >> 8);
            state[o + 4] = (byte)Mathf.Clamp(Mathf.RoundToInt(car.Speed * k_SpeedScale), 0, 255);
            state[o + 5] = (byte)(car.Epoch | (car.Hidden ? k_HiddenFlag : 0));
        }

        return state;
    }

    private void ApplyLatestState(TrafficSync sync)
    {
        byte[] state = sync.State;
        if (state == null || state.AsSpan().SequenceEqual(lastState)) return;

        // Normcore overwrites the received array in place when the length is unchanged, so a new
        // snapshot can arrive in the same array: compare contents against a private copy.
        if (lastState == null || lastState.Length != state.Length) lastState = new byte[state.Length];
        Buffer.BlockCopy(state, 0, lastState, 0, state.Length);
        DecodeState(lastState);
    }

    private void DecodeState(byte[] state)
    {
        int count = Mathf.Min(cars.Count, state.Length / k_BytesPerCar);
        for (int i = 0; i < count; i++)
        {
            int o = i * k_BytesPerCar;
            int routeIndex = state[o] | (state[o + 1] << 8);
            TrafficRoute route = routeIndex < routes.Count ? routes[routeIndex] : null;
            float distance = (state[o + 2] | (state[o + 3] << 8)) / k_DistanceScale;
            float speed = state[o + 4] / k_SpeedScale;
            byte flags = state[o + 5];

            cars[i].ApplyState(route, distance, speed, (flags & k_HiddenFlag) != 0, (byte)(flags & EpochMask));
        }
    }

    private int ComputeRouteHash()
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < routes.Count; i++)
                hash = hash * 31 + Mathf.RoundToInt(routes[i].Length);
            return hash != 0 ? hash : 1;
        }
    }

    private void AddRoadColliders()
    {
        RoadSection[] sections = UnityEngine.Object.FindObjectsByType<RoadSection>();
        int added = 0;

        for (int i = 0; i < sections.Length; i++)
        {
            MeshFilter filter = sections[i].GetComponentInParent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            GameObject target = filter.gameObject;
            if (target.GetComponent<Collider>() != null) continue;

            Bounds bounds = filter.sharedMesh.bounds;
            if (bounds.size.sqrMagnitude < 0.0001f) continue;

            Vector3 size = bounds.size;
            size.y = Mathf.Max(size.y, 0.2f);

            BoxCollider box = target.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = size;
            added++;
        }

        Debug.Log($"[Traffic] Added {added} road colliders for the character.");
    }

    // Consumes the same random numbers per car whatever happens, so every client builds identical cars.
    private void SpawnCar(Random carRandom, Transform root)
    {
        GameObject prefab = carPrefabs[carRandom.Next(carPrefabs.Count)];
        float maxSpeed = Mathf.Lerp(speedRange.x, speedRange.y, (float)carRandom.NextDouble());
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab);
        instance.name = $"Car_{cars.Count:00}";
        instance.transform.SetParent(root, true);

        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            Type type = behaviour.GetType();
            if (type.FullName != null && type.FullName.StartsWith("ITHappy."))
                Destroy(behaviour);
        }

        TrafficCar car = instance.GetComponent<TrafficCar>();
        if (car == null) car = instance.AddComponent<TrafficCar>();

        car.Initialize(cars, maxSpeed, acceleration, brake, lookAhead, stopDistance);
        cars.Add(car);
    }

    private void LogReport(int sectionCount)
    {
        Vector3 center = trafficCenter != null ? trafficCenter.position : fallbackCenter;
        int closed = routes.Count(r => r.Closed);
        int open = routes.Count - closed;
        var builder = new StringBuilder();
        builder.AppendLine($"[Traffic] Road sections: {sectionCount}; routes: {routes.Count} ({open} open, {closed} closed); total {routes.Sum(r => r.Length):0} m.");

        foreach (TrafficRoute route in routes.OrderByDescending(r => r.Length).Take(6))
            builder.AppendLine($"[Traffic]   route {route.Index}: {route.Length:0} m, closed={route.Closed}, nearest={route.ClosestDistanceTo(center):0} m");

        Debug.Log(builder.ToString());
    }

    private void OnDrawGizmos()
    {
        if (!drawRoutes || routes.Count == 0) return;

        for (int i = 0; i < routes.Count; i++)
        {
            TrafficRoute route = routes[i];
            Gizmos.color = Color.HSVToRGB((i * 0.137f) % 1f, 0.85f, 0.95f);
            IReadOnlyList<Vector3> points = route.Points;
            for (int p = 1; p < points.Count; p++)
                Gizmos.DrawLine(points[p - 1], points[p]);
        }
    }

    public static List<TrafficRoute> BuildRoutes(float sampleStep, float mergeDistance, float minTangentDot,
        float minRouteLength, out int sectionCount)
    {
        var segments = new List<LaneSegment>();
        int splineId = 0;
        // Route indices are sent over the network, so every client must build routes in the same order;
        // FindObjectsByType does not guarantee one. Some sections share a position and name, so the
        // hierarchy path is the final tie-break.
        RoadSection[] sections = UnityEngine.Object.FindObjectsByType<RoadSection>()
            .OrderBy(section => section.transform.position.x)
            .ThenBy(section => section.transform.position.y)
            .ThenBy(section => section.transform.position.z)
            .ThenBy(section => section.name, StringComparer.Ordinal)
            .ThenBy(section => HierarchyPath(section.transform), StringComparer.Ordinal)
            .ToArray();
        sectionCount = sections.Length;

        for (int s = 0; s < sections.Length; s++)
        {
            SplineContainer container = sections[s].GetComponent<SplineContainer>();
            if (container == null || container.Splines == null) continue;

            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                Spline spline = container.Splines[splineIndex];
                if (spline == null) continue;
                splineId++;

                float localLength = spline.GetLength();
                int steps = Mathf.Clamp(Mathf.CeilToInt(localLength / Mathf.Max(0.1f, sampleStep)), 2, 128);

                Vector3 previousPosition = default;
                Vector3 previousUp = Vector3.up;
                bool hasPrevious = false;

                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    if (!container.Evaluate(splineIndex, t, out float3 position, out _, out float3 up))
                    {
                        hasPrevious = false;
                        continue;
                    }

                    Vector3 worldPosition = (Vector3)position;
                    Vector3 worldUp = (Vector3)up;
                    if (worldUp.sqrMagnitude < 0.0001f) worldUp = Vector3.up;
                    worldUp.Normalize();

                    if (hasPrevious)
                    {
                        Vector3 delta = worldPosition - previousPosition;
                        float length = delta.magnitude;
                        if (length > 0.05f)
                            segments.Add(new LaneSegment(previousPosition, worldPosition, delta / length, previousUp, worldUp,
                                splineId, step));
                    }

                    previousPosition = worldPosition;
                    previousUp = worldUp;
                    hasPrevious = true;
                }
            }
        }

        return BuildChains(Deduplicate(segments), mergeDistance, minTangentDot, minRouteLength);
    }

    /// Sibling indices from the scene root down: unique within a scene and identical on every client.
    private static string HierarchyPath(Transform transform)
    {
        string path = transform.GetSiblingIndex().ToString("D5");
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            path = parent.GetSiblingIndex().ToString("D5") + "/" + path;
        return path;
    }

    public static string BuildAndDescribe(float sampleStep, float mergeDistance, float minTangentDot,
        float minRouteLength, Vector3 center)
    {
        List<TrafficRoute> built = BuildRoutes(sampleStep, mergeDistance, minTangentDot, minRouteLength, out int sectionCount);
        int closed = built.Count(r => r.Closed);
        var builder = new StringBuilder();
        builder.AppendLine($"Road sections: {sectionCount}");
        builder.AppendLine($"Lane routes: {built.Count} ({built.Count - closed} open, {closed} closed), total {built.Sum(r => r.Length):0} m");

        foreach (TrafficRoute route in built.OrderByDescending(r => r.Length).Take(10))
            builder.AppendLine($"  route {route.Index}: {route.Length:0} m, closed={route.Closed}, nearest={route.ClosestDistanceTo(center):0} m");

        return builder.ToString();
    }

    private static List<LaneSegment> Deduplicate(List<LaneSegment> segments)
    {
        var result = new List<LaneSegment>(segments.Count);
        var seen = new HashSet<SegmentKey>();

        for (int i = 0; i < segments.Count; i++)
        {
            LaneSegment segment = segments[i];
            if (seen.Add(new SegmentKey(segment.Start, segment.End)))
                result.Add(segment);
        }

        return result;
    }

    private static List<TrafficRoute> BuildChains(List<LaneSegment> segments, float mergeDistance,
        float minTangentDot, float minRouteLength)
    {
        var routes = new List<TrafficRoute>();
        int count = segments.Count;
        if (count == 0) return routes;

        int[] next = new int[count];
        bool[] hasPredecessor = new bool[count];
        for (int i = 0; i < count; i++) next[i] = -1;

        float mergeSqr = mergeDistance * mergeDistance;
        var candidates = new List<LinkCandidate>();

        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;

                float sqr = (segments[i].End - segments[j].Start).sqrMagnitude;
                if (sqr > mergeSqr) continue;

                // Consecutive samples of one spline always belong together; sharp turn splines
                // (Road_011 Section_1B/3B) bend more than minTangentDot between two samples.
                if (segments[i].SplineId == segments[j].SplineId && segments[j].Step == segments[i].Step + 1)
                {
                    candidates.Add(new LinkCandidate(i, j, 100f));
                    continue;
                }

                float dot = Vector3.Dot(segments[i].Forward, segments[j].Forward);
                if (dot < minTangentDot) continue;
                if (Vector3.Dot(segments[i].EndUp, segments[j].StartUp) < 0.9f) continue;

                candidates.Add(new LinkCandidate(i, j, dot * 10f - Mathf.Sqrt(sqr)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        for (int i = 0; i < candidates.Count; i++)
        {
            LinkCandidate candidate = candidates[i];
            if (next[candidate.From] >= 0 || hasPredecessor[candidate.To]) continue;

            next[candidate.From] = candidate.To;
            hasPredecessor[candidate.To] = true;
        }

        // At a merge (e.g. a T-junction turn joining a straight lane) the target segment already has a
        // predecessor, so the losing chain would end in the middle of the junction and its cars would
        // vanish there. Remember those links so open routes can continue through the shared lane.
        // The same applies backwards at a diverge, where the losing chain would start mid-junction.
        int[] previous = new int[count];
        int[] mergeNext = new int[count];
        int[] mergePrevious = new int[count];
        for (int i = 0; i < count; i++)
        {
            previous[i] = -1;
            mergeNext[i] = -1;
            mergePrevious[i] = -1;
        }

        for (int i = 0; i < count; i++)
            if (next[i] >= 0) previous[next[i]] = i;

        for (int i = 0; i < candidates.Count; i++)
        {
            LinkCandidate candidate = candidates[i];
            if (next[candidate.From] < 0 && mergeNext[candidate.From] < 0)
                mergeNext[candidate.From] = candidate.To;
            if (!hasPredecessor[candidate.To] && mergePrevious[candidate.To] < 0)
                mergePrevious[candidate.To] = candidate.From;
        }

        bool[] visited = new bool[count];
        int routeIndex = 0;

        for (int i = 0; i < count; i++)
        {
            if (visited[i] || hasPredecessor[i]) continue;

            var chain = new List<int>();
            int current = i;
            while (current >= 0 && !visited[current])
            {
                chain.Add(current);
                visited[current] = true;
                current = next[current];
            }

            ExtendThroughJunctions(chain, next, mergeNext, previous, mergePrevious);
            AddRoute(segments, chain, false, minRouteLength, routes, ref routeIndex);
        }

        for (int i = 0; i < count; i++)
        {
            if (visited[i]) continue;

            var chain = new List<int>();
            int current = i;
            bool closed = false;

            while (current >= 0 && !visited[current])
            {
                chain.Add(current);
                visited[current] = true;
                current = next[current];

                if (current == i)
                {
                    closed = true;
                    break;
                }
            }

            AddRoute(segments, chain, closed, minRouteLength, routes, ref routeIndex);
        }

        return routes;
    }

    private static void ExtendThroughJunctions(List<int> chain, int[] next, int[] mergeNext, int[] previous,
        int[] mergePrevious)
    {
        var used = new HashSet<int>(chain);

        int Follow(int[] primary, int[] fallback, int segment) =>
            primary[segment] >= 0 ? primary[segment] : fallback[segment];

        for (int k = Follow(next, mergeNext, chain[chain.Count - 1]); k >= 0 && used.Add(k);
             k = Follow(next, mergeNext, k))
            chain.Add(k);

        var prefix = new List<int>();
        for (int k = Follow(previous, mergePrevious, chain[0]); k >= 0 && used.Add(k);
             k = Follow(previous, mergePrevious, k))
            prefix.Add(k);

        prefix.Reverse();
        chain.InsertRange(0, prefix);
    }

    private static void AddRoute(List<LaneSegment> segments, List<int> chain, bool closed,
        float minRouteLength, List<TrafficRoute> routes, ref int routeIndex)
    {
        if (chain.Count == 0) return;

        var points = new List<Vector3>(chain.Count + 2);
        var ups = new List<Vector3>(chain.Count + 2);
        LaneSegment first = segments[chain[0]];

        points.Add(first.Start);
        ups.Add(first.StartUp);

        for (int i = 0; i < chain.Count; i++)
        {
            LaneSegment segment = segments[chain[i]];
            points.Add(segment.End);
            ups.Add(segment.EndUp);
        }

        if (closed)
        {
            if ((points[points.Count - 1] - points[0]).sqrMagnitude > 0.0001f)
            {
                points.Add(points[0]);
                ups.Add(ups[0]);
            }
            else
            {
                points[points.Count - 1] = points[0];
                ups[ups.Count - 1] = ups[0];
            }
        }

        var route = new TrafficRoute(routeIndex, points, ups, closed);
        if (route.Length < minRouteLength) return;

        routes.Add(route);
        routeIndex++;
    }

    private readonly struct SpawnSample
    {
        public readonly TrafficRoute Route;
        public readonly float Distance;
        public readonly Vector3 Position;

        public SpawnSample(TrafficRoute route, float distance, Vector3 position)
        {
            Route = route;
            Distance = distance;
            Position = position;
        }
    }

    private readonly struct LaneSegment
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly Vector3 Forward;
        public readonly Vector3 StartUp;
        public readonly Vector3 EndUp;
        public readonly int SplineId;
        public readonly int Step;

        public LaneSegment(Vector3 start, Vector3 end, Vector3 forward, Vector3 startUp, Vector3 endUp,
            int splineId, int step)
        {
            Start = start;
            End = end;
            Forward = forward;
            StartUp = startUp;
            EndUp = endUp;
            SplineId = splineId;
            Step = step;
        }
    }

    private readonly struct LinkCandidate
    {
        public readonly int From;
        public readonly int To;
        public readonly float Score;

        public LinkCandidate(int from, int to, float score)
        {
            From = from;
            To = to;
            Score = score;
        }
    }

    private readonly struct SegmentKey : IEquatable<SegmentKey>
    {
        private readonly int ax;
        private readonly int ay;
        private readonly int az;
        private readonly int bx;
        private readonly int by;
        private readonly int bz;

        public SegmentKey(Vector3 start, Vector3 end)
        {
            ax = Quantize(start.x);
            ay = Quantize(start.y);
            az = Quantize(start.z);
            bx = Quantize(end.x);
            by = Quantize(end.y);
            bz = Quantize(end.z);
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 20f);

        public bool Equals(SegmentKey other) =>
            ax == other.ax && ay == other.ay && az == other.az &&
            bx == other.bx && by == other.by && bz == other.bz;

        public override bool Equals(object obj) => obj is SegmentKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ax;
                hash = hash * 31 + ay;
                hash = hash * 31 + az;
                hash = hash * 31 + bx;
                hash = hash * 31 + by;
                hash = hash * 31 + bz;
                return hash;
            }
        }
    }
}
