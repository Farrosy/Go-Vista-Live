using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITHappy;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Random = System.Random;

public sealed class TrafficNetwork : MonoBehaviour
{
    [Header("Route building")]
    [SerializeField] private float sampleStep = 2f;
    [SerializeField] private float mergeDistance = 1f;
    [SerializeField, Range(0f, 1f)] private float minTangentDot = 0.9f;
    [SerializeField] private float minRouteLength = 45f;

    [Header("Traffic")]
    [SerializeField] private List<GameObject> carPrefabs = new();
    [SerializeField] private int maxCars = 14;
    [SerializeField] private float carSpacing = 45f;
    [SerializeField] private float trafficRadius = 250f;
    [SerializeField] private Vector2 speedRange = new Vector2(6f, 9f);
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float brake = 35f;
    [SerializeField] private float lookAhead = 12f;
    [SerializeField] private float stopDistance = 4f;
    [SerializeField] private Transform carRoot;
    [SerializeField] private Transform trafficCenter;
    [SerializeField] private Vector3 fallbackCenter = new Vector3(159.078674f, 23.2f, 94.08188f);
    [SerializeField] private int randomSeed = 0;

    [Header("Scene")]
    [SerializeField] private bool addRoadColliders = true;
    [SerializeField] private bool drawRoutes = true;

    private readonly List<TrafficRoute> routes = new();
    private readonly List<TrafficCar> cars = new();
    private Random random;

    public IReadOnlyList<TrafficRoute> Routes => routes;
    public IReadOnlyList<TrafficCar> Cars => cars;

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

        Vector3 center = trafficCenter != null ? trafficCenter.position : fallbackCenter;
        SpawnCars(SelectRoutes(center));
        LogReport(sectionCount, center);
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

    private List<TrafficRoute> SelectRoutes(Vector3 center)
    {
        List<TrafficRoute> ordered = routes
            .OrderBy(r => r.ClosestDistanceTo(center))
            .ThenByDescending(r => r.Length)
            .ToList();

        List<TrafficRoute> selected = ordered
            .Where(r => r.ClosestDistanceTo(center) <= trafficRadius)
            .ToList();

        if (selected.Count == 0)
            selected.AddRange(ordered.Take(Mathf.Min(ordered.Count, Mathf.Max(1, maxCars))));

        int Capacity(TrafficRoute route) =>
            Mathf.Max(1, Mathf.FloorToInt(route.Length / Mathf.Max(1f, carSpacing)));

        if (selected.Sum(Capacity) < maxCars)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                if (selected.Contains(ordered[i])) continue;
                selected.Add(ordered[i]);
                if (selected.Sum(Capacity) >= maxCars) break;
            }
        }

        return selected;
    }

    private void SpawnCars(List<TrafficRoute> selected)
    {
        int remaining = maxCars;
        var assigned = new Dictionary<TrafficRoute, int>();
        for (int i = 0; i < selected.Count; i++) assigned[selected[i]] = 0;

        while (remaining > 0)
        {
            bool added = false;
            for (int i = 0; i < selected.Count && remaining > 0; i++)
            {
                TrafficRoute route = selected[i];
                int capacity = Mathf.Max(1, Mathf.FloorToInt(route.Length / Mathf.Max(1f, carSpacing)));
                if (assigned[route] >= capacity) continue;

                assigned[route]++;
                remaining--;
                added = true;
            }

            if (!added) break;
        }

        Transform root = carRoot != null ? carRoot : transform;
        foreach (KeyValuePair<TrafficRoute, int> pair in assigned)
        {
            TrafficRoute route = pair.Key;
            int count = pair.Value;
            if (count <= 0) continue;

            for (int i = 0; i < count; i++)
            {
                float slot = route.Length / count;
                float baseDistance = (i + 0.5f) * slot;
                float jitter = Mathf.Min(slot * 0.15f, 3f);
                float distance = baseDistance + (float)(random.NextDouble() * 2.0 - 1.0) * jitter;

                if (route.Closed)
                {
                    distance = Mathf.Repeat(distance, route.Length);
                }
                else
                {
                    float margin = Mathf.Min(10f, route.Length * 0.1f);
                    distance = Mathf.Clamp(distance, margin, Mathf.Max(margin, route.Length - margin));
                }

                SpawnCar(route, distance, root);
            }
        }

        if (remaining > 0)
            Debug.LogWarning($"[Traffic] Placed {maxCars - remaining}/{maxCars} cars; not enough route capacity near the player.");
    }

    private void SpawnCar(TrafficRoute route, float distance, Transform root)
    {
        GameObject prefab = carPrefabs[random.Next(carPrefabs.Count)];
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

        float maxSpeed = Mathf.Lerp(speedRange.x, speedRange.y, (float)random.NextDouble());
        car.Initialize(route, distance, maxSpeed, acceleration, brake, lookAhead, stopDistance);

        route.Cars.Add(car);
        cars.Add(car);
    }

    private void LogReport(int sectionCount, Vector3 center)
    {
        int closed = routes.Count(r => r.Closed);
        int open = routes.Count - closed;
        var builder = new StringBuilder();
        builder.AppendLine($"[Traffic] Road sections: {sectionCount}; routes: {routes.Count} ({open} open, {closed} closed); total {routes.Sum(r => r.Length):0} m.");
        builder.AppendLine($"[Traffic] Spawned {cars.Count} cars. Center: {center}; radius: {trafficRadius} m.");

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
        RoadSection[] sections = UnityEngine.Object.FindObjectsByType<RoadSection>();
        sectionCount = sections.Length;

        for (int s = 0; s < sections.Length; s++)
        {
            SplineContainer container = sections[s].GetComponent<SplineContainer>();
            if (container == null || container.Splines == null) continue;

            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                Spline spline = container.Splines[splineIndex];
                if (spline == null) continue;

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
                            segments.Add(new LaneSegment(previousPosition, worldPosition, delta / length, previousUp, worldUp));
                    }

                    previousPosition = worldPosition;
                    previousUp = worldUp;
                    hasPrevious = true;
                }
            }
        }

        return BuildChains(Deduplicate(segments), mergeDistance, minTangentDot, minRouteLength);
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

    private readonly struct LaneSegment
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly Vector3 Forward;
        public readonly Vector3 StartUp;
        public readonly Vector3 EndUp;

        public LaneSegment(Vector3 start, Vector3 end, Vector3 forward, Vector3 startUp, Vector3 endUp)
        {
            Start = start;
            End = end;
            Forward = forward;
            StartUp = startUp;
            EndUp = endUp;
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
