using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITHappy;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Random = System.Random;

/// <summary>
/// Tunables for building the lane graph. Plain serializable class so it can live as an
/// inline field on <c>TrafficNetwork</c> today, or be lifted into a ScriptableObject later
/// without touching the graph layer. The graph build is scene-agnostic: it only reads
/// <see cref="RoadSection"/> splines from the active scene.
/// </summary>
[Serializable]
public sealed class GraphSettings
{
    [Header("Sampling / lane runs")]
    public float SampleStep = 2f;
    public float MergeDistance = 1f;
    [Range(0f, 1f)] public float MinTangentDot = 0.9f;
    public float MinRunLength = 8f;      // drop noise fragments; the plan's "lane run" metric counts runs >= 45 m
    public float MaxYDelta = 0.5f;       // ignore flyovers / different-height roads

    [Header("Intersections")]
    public float IntersectionHalfSize = 6f;  // clamped to nearestCenter/2 - 0.5
    public float ClusterRadius = 5f;
    [Range(0f, 1f)] public float CrossMaxParallelDot = 0.3f;  // skip crossings between near-parallel lanes
    [Range(0f, 1f)] public float PerpendicularDot = 0.5f;     // a cluster needs a pair below this to be an intersection

    [Header("Turns / U-turns")]
    public float TurnHandle = 5f;
    public float TurnSpeedLimit = 5f;
    public float UTurnRadius = 4f;
    public float UTurnSpeedLimit = 3f;
    public float DeadEndMatchRadius = 6f;    // max gap between a lane end and the opposite lane start for a U-turn

    [Header("Routing weights")]
    public float StraightWeight = 0.5f;
    public float LeftWeight = 0.25f;
    public float RightWeight = 0.25f;
    public float DeadEndPenalty = 0.05f;

    public GraphSettings Clone() => (GraphSettings)MemberwiseClone();
}

/// <summary>
/// The drivable lane graph: nodes, directed edges, and intersection boxes built from the
/// scene's <see cref="RoadSection"/> splines. Replaces the old flat <c>TrafficRoute</c> list.
///
/// Build pipeline (see the design doc): sample lane runs -> find intersection clusters ->
/// split runs into Lane edges + IntersectionStraight edges -> add Turn beziers and U-turns ->
/// assemble nodes, flag dead-end branches, validate.
/// </summary>
public sealed class TrafficLaneGraph
{
    public readonly List<TrafficNode> Nodes = new();
    public readonly List<TrafficEdge> Edges = new();
    public readonly List<TrafficIntersection> Intersections = new();

    public int RoadSectionCount;
    public int LaneRunCount;
    public int LaneRunsOver45;
    public int OrphanNodeCount;
    public int DeadEndNodeCount;
    public int LargestComponentNodes;

    private TrafficLaneGraph() { }

    /// <summary>Builds the graph from the active scene. Pass a non-null report sink to collect a human log.</summary>
    public static TrafficLaneGraph Build(GraphSettings settings, StringBuilder report = null)
    {
        settings ??= new GraphSettings();
        var builder = new Builder(settings, report);
        return builder.Build();
    }

    /// <summary>Builds the graph and returns a human-readable report of the counts the design doc calls for.</summary>
    public static string BuildAndDescribe(GraphSettings settings, Vector3 center)
    {
        var report = new StringBuilder();
        TrafficLaneGraph graph = Build(settings, report);
        graph.AppendNearestEdges(report, center);
        return report.ToString();
    }

    /// <summary>
    /// Picks the next movement edge leaving an intersection entry node, weighted by
    /// straight/left/right and penalized toward dead-end branches.
    /// </summary>
    /// <remarks>
    /// Deviation from the frozen signature <c>SelectNextEdge(TrafficCar, Random)</c>: the car
    /// rewrite (T5) is a later pass and does not yet expose its current node, so this pass takes
    /// the node + the car's incoming direction directly. The car pass can wrap this.
    /// </remarks>
    public TrafficEdge SelectNextEdge(TrafficNode node, Vector3 incomingDir, GraphSettings settings, Random random)
    {
        if (node == null || node.Outgoing.Count == 0) return null;
        if (node.Outgoing.Count == 1) return node.Outgoing[0];

        settings ??= new GraphSettings();
        random ??= new Random();

        float total = 0f;
        Span<float> weights = node.Outgoing.Count <= 16
            ? stackalloc float[node.Outgoing.Count]
            : new float[node.Outgoing.Count];

        TrafficEdge straight = null;
        for (int i = 0; i < node.Outgoing.Count; i++)
        {
            TrafficEdge edge = node.Outgoing[i];
            float weight = edge.Kind switch
            {
                TrafficEdgeKind.IntersectionStraight => settings.StraightWeight,
                TrafficEdgeKind.Turn => edge.TurnDirection < 0 ? settings.LeftWeight : settings.RightWeight,
                _ => settings.StraightWeight // plain Lane / U-turn forced moves
            };

            if (edge.Kind == TrafficEdgeKind.IntersectionStraight) straight = edge;
            if (edge.LeadsToDeadEnd) weight *= settings.DeadEndPenalty;

            weight = Mathf.Max(0f, weight);
            weights[i] = weight;
            total += weight;
        }

        if (total <= 0f)
            return straight ?? node.Outgoing[0];

        double roll = random.NextDouble() * total;
        for (int i = 0; i < node.Outgoing.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0d) return node.Outgoing[i];
        }
        return node.Outgoing[node.Outgoing.Count - 1];
    }

    /// <summary>
    /// Returns candidate Lane edges to spawn on, nearest the center first, preferring edges that
    /// do not lead to a dead end. Actual slot placement is the network's job (T8); this just ranks
    /// the drivable lanes so the spawner has a clean list.
    /// </summary>
    public List<TrafficEdge> SelectSpawnEdges(Vector3 center, float radius, float spacing, int maxCars, Random random)
    {
        var result = new List<TrafficEdge>();
        float capacity = 0f;
        float minLen = Mathf.Max(1f, spacing * 0.5f);

        IEnumerable<TrafficEdge> ranked = Edges
            .Where(e => e.Kind == TrafficEdgeKind.Lane && e.Length >= minLen && e.ClosestDistanceTo(center) <= radius)
            .OrderBy(e => e.LeadsToDeadEnd ? 1 : 0)
            .ThenBy(e => e.ClosestDistanceTo(center))
            .ThenByDescending(e => e.Length);

        foreach (TrafficEdge edge in ranked)
        {
            result.Add(edge);
            capacity += Mathf.Max(1f, Mathf.Floor(edge.Length / Mathf.Max(1f, spacing)));
            if (maxCars > 0 && capacity >= maxCars) break;
        }

        return result;
    }

    private void AppendNearestEdges(StringBuilder report, Vector3 center)
    {
        report.AppendLine($"Nearest lane edges to {center}:");
        foreach (TrafficEdge edge in Edges
                     .Where(e => e.Kind == TrafficEdgeKind.Lane)
                     .OrderBy(e => e.ClosestDistanceTo(center))
                     .Take(6))
            report.AppendLine($"  edge {edge.Id}: {edge.Length:0} m, nearest {edge.ClosestDistanceTo(center):0} m, deadEnd={edge.LeadsToDeadEnd}");
    }

    // ------------------------------------------------------------------ build

    private sealed class Builder
    {
        private readonly GraphSettings s;
        private readonly StringBuilder report;
        private readonly TrafficLaneGraph graph = new();

        private List<LaneRun> runs;
        private int nextNodeId;
        private int nextEdgeId;

        // entry/exit node metadata needed while wiring turns
        private readonly Dictionary<TrafficNode, Vector3> nodeDir = new();     // run downstream direction at the node
        private readonly Dictionary<TrafficNode, int> nodeRun = new();         // owning lane-run index
        private readonly Dictionary<TrafficIntersection, HashSet<int>> arms = new();

        public Builder(GraphSettings settings, StringBuilder report)
        {
            s = settings;
            this.report = report;
        }

        public TrafficLaneGraph Build()
        {
            runs = SampleLaneRuns(out int sectionCount);
            graph.RoadSectionCount = sectionCount;
            graph.LaneRunCount = runs.Count;
            graph.LaneRunsOver45 = runs.Count(r => r.Length >= 45f);

            BuildIntersections();
            ComputePorts();
            SliceRuns();
            BuildTurns();
            BuildUTurns();
            MarkDeadEndChains();
            Validate();
            ComputeLargestComponent();
            WriteReport(sectionCount);
            return graph;
        }

        // --- phase 1: lane runs (ported from the old TrafficNetwork chain builder) ---

        private List<LaneRun> SampleLaneRuns(out int sectionCount)
        {
            var segments = new List<LaneSegment>();
            RoadSection[] sections = UnityEngine.Object.FindObjectsByType<RoadSection>();
            sectionCount = sections.Length;

            for (int si = 0; si < sections.Length; si++)
            {
                SplineContainer container = sections[si].GetComponent<SplineContainer>();
                if (container == null || container.Splines == null) continue;

                for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                {
                    Spline spline = container.Splines[splineIndex];
                    if (spline == null) continue;

                    float localLength = spline.GetLength();
                    int steps = Mathf.Clamp(Mathf.CeilToInt(localLength / Mathf.Max(0.1f, s.SampleStep)), 2, 128);

                    Vector3 prevPos = default;
                    Vector3 prevUp = Vector3.up;
                    bool hasPrev = false;

                    for (int step = 0; step <= steps; step++)
                    {
                        float t = step / (float)steps;
                        if (!container.Evaluate(splineIndex, t, out float3 position, out _, out float3 up))
                        {
                            hasPrev = false;
                            continue;
                        }

                        Vector3 worldPos = (Vector3)position;
                        Vector3 worldUp = (Vector3)up;
                        if (worldUp.sqrMagnitude < 0.0001f) worldUp = Vector3.up;
                        worldUp.Normalize();

                        if (hasPrev)
                        {
                            Vector3 delta = worldPos - prevPos;
                            float length = delta.magnitude;
                            if (length > 0.05f)
                                segments.Add(new LaneSegment(prevPos, worldPos, delta / length, prevUp, worldUp));
                        }

                        prevPos = worldPos;
                        prevUp = worldUp;
                        hasPrev = true;
                    }
                }
            }

            return BuildChains(Deduplicate(segments));
        }

        private List<LaneSegment> Deduplicate(List<LaneSegment> segments)
        {
            var result = new List<LaneSegment>(segments.Count);
            var seen = new HashSet<SegmentKey>();
            for (int i = 0; i < segments.Count; i++)
                if (seen.Add(new SegmentKey(segments[i].Start, segments[i].End)))
                    result.Add(segments[i]);
            return result;
        }

        private List<LaneRun> BuildChains(List<LaneSegment> segments)
        {
            var result = new List<LaneRun>();
            int count = segments.Count;
            if (count == 0) return result;

            int[] next = new int[count];
            bool[] hasPredecessor = new bool[count];
            for (int i = 0; i < count; i++) next[i] = -1;

            float mergeSqr = s.MergeDistance * s.MergeDistance;
            var candidates = new List<LinkCandidate>();

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    float sqr = (segments[i].End - segments[j].Start).sqrMagnitude;
                    if (sqr > mergeSqr) continue;

                    float dot = Vector3.Dot(segments[i].Forward, segments[j].Forward);
                    if (dot < s.MinTangentDot) continue;
                    if (Vector3.Dot(segments[i].EndUp, segments[j].StartUp) < 0.9f) continue;

                    candidates.Add(new LinkCandidate(i, j, dot * 10f - Mathf.Sqrt(sqr)));
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < candidates.Count; i++)
            {
                LinkCandidate c = candidates[i];
                if (next[c.From] >= 0 || hasPredecessor[c.To]) continue;
                next[c.From] = c.To;
                hasPredecessor[c.To] = true;
            }

            bool[] visited = new bool[count];
            int index = 0;

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
                AddRun(segments, chain, false, result, ref index);
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
                    if (current == i) { closed = true; break; }
                }
                AddRun(segments, chain, closed, result, ref index);
            }

            return result;
        }

        private void AddRun(List<LaneSegment> segments, List<int> chain, bool closed, List<LaneRun> result, ref int index)
        {
            if (chain.Count == 0) return;

            var points = new List<Vector3>(chain.Count + 2);
            var ups = new List<Vector3>(chain.Count + 2);
            LaneSegment first = segments[chain[0]];
            points.Add(first.Start);
            ups.Add(first.StartUp);

            for (int i = 0; i < chain.Count; i++)
            {
                LaneSegment seg = segments[chain[i]];
                points.Add(seg.End);
                ups.Add(seg.EndUp);
            }

            if (closed && (points[points.Count - 1] - points[0]).sqrMagnitude > 0.0001f)
            {
                points.Add(points[0]);
                ups.Add(ups[0]);
            }

            var run = new LaneRun(index, points, ups, closed);
            if (run.Length < s.MinRunLength) return;
            result.Add(run);
            index++;
        }

        // --- phase 2: intersection clusters ---

        private void BuildIntersections()
        {
            var candidates = new List<Vector3>();

            // lane-run crossings
            for (int i = 0; i < runs.Count; i++)
            {
                for (int j = i + 1; j < runs.Count; j++)
                {
                    if (!BoundsOverlap(runs[i].Bounds, runs[j].Bounds, s.MergeDistance)) continue;
                    CollectCrossings(runs[i], runs[j], candidates);
                }
            }

            // lane-run ends
            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i].Closed) continue;
                candidates.Add(runs[i].Points[0]);
                candidates.Add(runs[i].Points[runs[i].Points.Count - 1]);
            }

            List<Vector3> centers = ClusterPoints(candidates, s.ClusterRadius);
            MergeCloseCenters(centers, 1f);

            foreach (Vector3 center in centers)
            {
                var participants = new List<Vector3>();   // directions at the center
                for (int i = 0; i < runs.Count; i++)
                {
                    runs[i].NearestInfo(center, out _, out Vector3 nearest, out Vector3 dir);
                    if ((nearest - center).sqrMagnitude > s.IntersectionHalfSize * s.IntersectionHalfSize) continue;
                    participants.Add(Flatten(dir));
                }

                if (participants.Count < 2) continue;
                if (!HasPerpendicularPair(participants)) continue;

                var intersection = new TrafficIntersection(graph.Intersections.Count, center, s.IntersectionHalfSize);
                graph.Intersections.Add(intersection);
                arms[intersection] = new HashSet<int>();
            }

            ClampHalfSizes();
        }

        private void ClampHalfSizes()
        {
            for (int i = 0; i < graph.Intersections.Count; i++)
            {
                float nearest = float.PositiveInfinity;
                for (int j = 0; j < graph.Intersections.Count; j++)
                {
                    if (i == j) continue;
                    float d = Vector3.Distance(graph.Intersections[i].Center, graph.Intersections[j].Center);
                    if (d < nearest) nearest = d;
                }

                float half = s.IntersectionHalfSize;
                if (!float.IsPositiveInfinity(nearest))
                    half = Mathf.Min(half, nearest * 0.5f - 0.5f);
                graph.Intersections[i].HalfSize = Mathf.Max(1f, half);
            }
        }

        // --- phase 3: entry/exit ports per run ---

        private void ComputePorts()
        {
            for (int ri = 0; ri < runs.Count; ri++)
            {
                LaneRun run = runs[ri];
                foreach (TrafficIntersection intersection in graph.Intersections)
                {
                    if (!RunParticipation(run, intersection, out float entryDist, out Vector3 entryPos, out bool hasEntry,
                            out float exitDist, out Vector3 exitPos, out bool hasExit, out Vector3 dir))
                        continue;

                    arms[intersection].Add(ri);

                    if (hasEntry)
                    {
                        TrafficNode node = NewNode(entryPos);
                        node.Intersection = intersection;
                        node.IsEntry = true;
                        intersection.Entries.Add(node);
                        nodeDir[node] = dir;
                        nodeRun[node] = ri;
                        run.Ports.Add(new RunPort(entryDist, node, true, intersection));
                    }

                    if (hasExit)
                    {
                        TrafficNode node = NewNode(exitPos);
                        node.Intersection = intersection;
                        node.IsExit = true;
                        intersection.Exits.Add(node);
                        nodeDir[node] = dir;
                        nodeRun[node] = ri;
                        run.Ports.Add(new RunPort(exitDist, node, false, intersection));
                    }
                }

                run.Ports.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }
        }

        private bool RunParticipation(LaneRun run, TrafficIntersection intersection,
            out float entryDist, out Vector3 entryPos, out bool hasEntry,
            out float exitDist, out Vector3 exitPos, out bool hasExit, out Vector3 dir)
        {
            entryDist = exitDist = 0f;
            entryPos = exitPos = default;
            hasEntry = hasExit = false;

            Vector3 center = intersection.Center;
            float half = intersection.HalfSize;

            run.NearestInfo(center, out _, out Vector3 nearest, out Vector3 rawDir);
            dir = Flatten(rawDir);
            if (dir.sqrMagnitude < 1e-6f) return false;
            if ((nearest - center).sqrMagnitude > half * half) return false;

            // scalar projection along the run direction, relative to the box planes at +/- half
            FindPlaneCrossing(run, center, dir, -half, out hasEntry, out entryDist, out entryPos);
            FindPlaneCrossing(run, center, dir, +half, out hasExit, out exitDist, out exitPos);
            return hasEntry || hasExit;
        }

        // --- phase 4a: slice runs into Lane + IntersectionStraight edges ---

        private void SliceRuns()
        {
            foreach (LaneRun run in runs)
            {
                var pts = new List<Vector3>();
                var ups = new List<Vector3>();

                // Determine where the first drivable lane starts.
                TrafficNode current;
                float currentDist;
                int portIndex = 0;

                if (run.Ports.Count > 0 && !run.Ports[0].IsEntry)
                {
                    // run begins inside a box (exit-only arm): lane starts at that exit node
                    current = run.Ports[0].Node;
                    currentDist = run.Ports[0].Distance;
                    portIndex = 1;
                }
                else
                {
                    current = NewNode(run.Points[0]);
                    currentDist = 0f;
                    if (!run.Closed) run.StartFreeNode = current;
                }

                RunPort pendingEntry = null;
                for (; portIndex < run.Ports.Count; portIndex++)
                {
                    RunPort port = run.Ports[portIndex];
                    if (port.IsEntry)
                    {
                        SliceLane(run, currentDist, port.Distance, pts, ups, current, port.Node);
                        current = port.Node;
                        currentDist = port.Distance;
                        pendingEntry = port;
                    }
                    else
                    {
                        if (pendingEntry != null && pendingEntry.Intersection == port.Intersection)
                            SliceStraight(run, pendingEntry.Distance, port.Distance, pts, ups,
                                pendingEntry.Node, port.Node, port.Intersection);
                        current = port.Node;
                        currentDist = port.Distance;
                        pendingEntry = null;
                    }
                }

                // trailing lane to the run end, unless the run terminated inside a box (entry-only arm)
                bool endsInBox = run.Ports.Count > 0 && run.Ports[run.Ports.Count - 1].IsEntry;
                if (!endsInBox)
                {
                    TrafficNode endNode;
                    if (run.Closed && run.StartFreeNode != null)
                    {
                        endNode = run.StartFreeNode; // close the loop back to the start node
                    }
                    else
                    {
                        endNode = NewNode(run.Points[run.Points.Count - 1]);
                        if (!run.Closed) run.EndFreeNode = endNode;
                    }
                    SliceLane(run, currentDist, run.Length, pts, ups, current, endNode);
                }
            }
        }

        private void SliceLane(LaneRun run, float d0, float d1, List<Vector3> pts, List<Vector3> ups,
            TrafficNode start, TrafficNode end)
        {
            if (d1 - d0 < 0.05f) return;
            SliceGeometry(run, d0, d1, pts, ups);
            AddEdge(TrafficEdgeKind.Lane, pts, ups, start, end, null, TrafficEdge.LaneSpeedLimit, 0);
        }

        private void SliceStraight(LaneRun run, float d0, float d1, List<Vector3> pts, List<Vector3> ups,
            TrafficNode start, TrafficNode end, TrafficIntersection intersection)
        {
            if (d1 - d0 < 0.05f) return;
            SliceGeometry(run, d0, d1, pts, ups);
            AddEdge(TrafficEdgeKind.IntersectionStraight, pts, ups, start, end, intersection, TrafficEdge.LaneSpeedLimit, 0);
        }

        private static void SliceGeometry(LaneRun run, float d0, float d1, List<Vector3> pts, List<Vector3> ups)
        {
            pts.Clear();
            ups.Clear();

            run.Evaluate(d0, out Vector3 p0, out _, out Vector3 u0);
            pts.Add(p0); ups.Add(u0);

            for (int i = 0; i < run.Points.Count; i++)
            {
                float d = run.Cumulative[i];
                if (d > d0 + 0.01f && d < d1 - 0.01f)
                {
                    pts.Add(run.Points[i]);
                    ups.Add(run.Ups[i]);
                }
            }

            run.Evaluate(d1, out Vector3 p1, out _, out Vector3 u1);
            pts.Add(p1); ups.Add(u1);
        }

        // --- phase 4b: turn edges ---

        private void BuildTurns()
        {
            foreach (TrafficIntersection intersection in graph.Intersections)
            {
                float handle = Mathf.Min(s.TurnHandle, intersection.HalfSize * 0.75f);

                foreach (TrafficNode entry in intersection.Entries)
                {
                    Vector3 inDir = nodeDir[entry];
                    int entryRun = nodeRun[entry];

                    foreach (TrafficNode exit in intersection.Exits)
                    {
                        if (nodeRun[exit] == entryRun) continue;          // same run = the straight-through
                        Vector3 outDir = nodeDir[exit];
                        if (Vector3.Dot(inDir, outDir) < -0.5f) continue; // reversing = U-turn, not allowed through a box

                        List<Vector3> pts = SampleBezier(entry.Position, exit.Position, inDir, outDir, handle, 10);
                        sbyte turnDir = (sbyte)(Cross2D(inDir, outDir) < 0f ? -1 : 1);
                        AddEdge(TrafficEdgeKind.Turn, pts, null, entry, exit, intersection, s.TurnSpeedLimit, turnDir);
                    }
                }
            }
        }

        // --- phase 4c: U-turns from unmatched free ends ---

        private void BuildUTurns()
        {
            // free "end" nodes = a lane that arrives and stops; pair with the opposite lane's free "start" node
            var starts = new List<(TrafficNode node, Vector3 dir, int run)>();
            foreach (LaneRun run in runs)
            {
                if (run.StartFreeNode != null)
                    starts.Add((run.StartFreeNode, Flatten(run.DirectionAt(0f)), run.Index));
            }

            foreach (LaneRun run in runs)
            {
                if (run.EndFreeNode == null) continue;
                Vector3 endNode = run.EndFreeNode.Position;
                Vector3 inDir = Flatten(run.DirectionAt(run.Length));

                TrafficNode bestStart = null;
                float bestSqr = s.DeadEndMatchRadius * s.DeadEndMatchRadius;
                for (int i = 0; i < starts.Count; i++)
                {
                    if (starts[i].run == run.Index) continue;
                    if (Vector3.Dot(inDir, starts[i].dir) > -0.9f) continue; // must be the opposite lane
                    float sqr = (starts[i].node.Position - endNode).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; bestStart = starts[i].node; }
                }

                run.EndFreeNode.IsDeadEnd = true;
                if (bestStart == null) continue; // orphan one-way dead end; flagged in Validate

                Vector3 p0 = run.EndFreeNode.Position;
                Vector3 p3 = bestStart.Position;
                Vector3 p1 = p0 + inDir * s.UTurnRadius;
                Vector3 p2 = p3 + inDir * s.UTurnRadius;
                List<Vector3> pts = SampleBezierRaw(p0, p1, p2, p3, 10);
                AddEdge(TrafficEdgeKind.UTurn, pts, null, run.EndFreeNode, bestStart, null, s.UTurnSpeedLimit, 0);
            }
        }

        // --- phase 5: dead-end flagging + validation ---

        private void MarkDeadEndChains()
        {
            int n = graph.Nodes.Count;
            var id = new Dictionary<TrafficNode, int>(n);
            for (int i = 0; i < n; i++) id[graph.Nodes[i]] = i;

            // progressOut = outgoing edges that make forward progress (not a U-turn)
            int[] unforced = new int[n];
            bool[] forced = new bool[n];
            var queue = new Queue<int>();

            for (int i = 0; i < n; i++)
            {
                int progress = 0;
                foreach (TrafficEdge e in graph.Nodes[i].Outgoing)
                    if (e.Kind != TrafficEdgeKind.UTurn) progress++;
                unforced[i] = progress;
                if (progress == 0) { forced[i] = true; queue.Enqueue(i); }
            }

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                foreach (TrafficEdge e in graph.Nodes[v].Incoming)
                {
                    if (e.Kind == TrafficEdgeKind.UTurn) continue;
                    int u = id[e.StartNode];
                    if (forced[u]) continue;
                    if (--unforced[u] == 0) { forced[u] = true; queue.Enqueue(u); }
                }
            }

            foreach (TrafficEdge e in graph.Edges)
            {
                if (e.Kind == TrafficEdgeKind.UTurn) continue;
                e.LeadsToDeadEnd = forced[id[e.EndNode]];
            }
            for (int i = 0; i < n; i++) graph.Nodes[i].LeadsToDeadEnd = forced[i];
        }

        private void Validate()
        {
            int orphans = 0;
            int deadEnds = 0;

            foreach (TrafficNode node in graph.Nodes)
            {
                if (node.IsDeadEnd) deadEnds++;
                if (node.Outgoing.Count == 0)
                {
                    orphans++;
                    Debug.LogError($"[TrafficGraph] Orphan node with no outgoing edge at {node.Position}.");
                }
            }

            // duplicate edges (same start/end/kind/intersection)
            var seen = new HashSet<(int, int, TrafficEdgeKind, int)>();
            foreach (TrafficEdge e in graph.Edges)
            {
                var key = (e.StartNode?.Id ?? -1, e.EndNode?.Id ?? -1, e.Kind, e.Intersection?.Id ?? -1);
                if (!seen.Add(key))
                    Debug.LogWarning($"[TrafficGraph] Duplicate edge {e}.");
                if (e.Length < 0.05f)
                    Debug.LogWarning($"[TrafficGraph] Near-zero-length edge {e}.");
            }

            graph.OrphanNodeCount = orphans;
            graph.DeadEndNodeCount = deadEnds;
        }

        private void ComputeLargestComponent()
        {
            // iterative Tarjan SCC; record the largest component's node count
            int n = graph.Nodes.Count;
            if (n == 0) return;

            var id = new Dictionary<TrafficNode, int>(n);
            for (int i = 0; i < n; i++) id[graph.Nodes[i]] = i;

            int[] indexOf = new int[n];
            int[] lowlink = new int[n];
            bool[] onStack = new bool[n];
            bool[] visited = new bool[n];
            for (int i = 0; i < n; i++) indexOf[i] = -1;

            var sccStack = new Stack<int>();
            var work = new Stack<(int node, int child)>();
            int counter = 0;
            int largest = 0;

            for (int start = 0; start < n; start++)
            {
                if (visited[start]) continue;
                work.Push((start, 0));

                while (work.Count > 0)
                {
                    (int v, int ci) = work.Pop();
                    if (ci == 0)
                    {
                        visited[v] = true;
                        indexOf[v] = lowlink[v] = counter++;
                        sccStack.Push(v);
                        onStack[v] = true;
                    }

                    var outs = graph.Nodes[v].Outgoing;
                    bool recursed = false;
                    for (int k = ci; k < outs.Count; k++)
                    {
                        int w = id[outs[k].EndNode];
                        if (indexOf[w] == -1)
                        {
                            work.Push((v, k + 1));
                            work.Push((w, 0));
                            recursed = true;
                            break;
                        }
                        if (onStack[w]) lowlink[v] = Mathf.Min(lowlink[v], indexOf[w]);
                    }
                    if (recursed) continue;

                    if (lowlink[v] == indexOf[v])
                    {
                        int size = 0;
                        while (true)
                        {
                            int w = sccStack.Pop();
                            onStack[w] = false;
                            size++;
                            if (w == v) break;
                        }
                        if (size > largest) largest = size;
                    }

                    if (work.Count > 0)
                    {
                        int parent = work.Peek().node;
                        lowlink[parent] = Mathf.Min(lowlink[parent], lowlink[v]);
                    }
                }
            }

            graph.LargestComponentNodes = largest;
        }

        // --- helpers ---

        private TrafficNode NewNode(Vector3 position)
        {
            var node = new TrafficNode(nextNodeId++, position);
            graph.Nodes.Add(node);
            return node;
        }

        private TrafficEdge AddEdge(TrafficEdgeKind kind, List<Vector3> pts, List<Vector3> ups,
            TrafficNode start, TrafficNode end, TrafficIntersection intersection, float speedLimit, sbyte turnDir)
        {
            var edge = new TrafficEdge(nextEdgeId++, kind, pts.ToArray(), ups?.ToArray())
            {
                StartNode = start,
                EndNode = end,
                Intersection = intersection,
                SpeedLimit = speedLimit,
                TurnDirection = turnDir
            };
            start.Outgoing.Add(edge);
            end.Incoming.Add(edge);
            graph.Edges.Add(edge);
            return edge;
        }

        private void CollectCrossings(LaneRun a, LaneRun b, List<Vector3> output)
        {
            for (int i = 0; i + 1 < a.Points.Count; i++)
            {
                Vector3 a0 = a.Points[i];
                Vector3 a1 = a.Points[i + 1];
                Vector3 dirA = Flatten(a1 - a0);
                for (int j = 0; j + 1 < b.Points.Count; j++)
                {
                    Vector3 b0 = b.Points[j];
                    Vector3 b1 = b.Points[j + 1];
                    if (Mathf.Abs(((a0.y + a1.y) - (b0.y + b1.y)) * 0.5f) > s.MaxYDelta) continue;

                    Vector3 dirB = Flatten(b1 - b0);
                    if (Mathf.Abs(Vector3.Dot(dirA, dirB)) > s.CrossMaxParallelDot) continue;
                    if (SegmentIntersectXZ(a0, a1, b0, b1, out Vector3 point))
                        output.Add(point);
                }
            }
        }

        private static List<Vector3> ClusterPoints(List<Vector3> points, float radius)
        {
            var sums = new List<Vector3>();
            var counts = new List<int>();
            float radiusSqr = radius * radius;

            foreach (Vector3 p in points)
            {
                int best = -1;
                float bestSqr = radiusSqr;
                for (int i = 0; i < sums.Count; i++)
                {
                    Vector3 center = sums[i] / counts[i];
                    float sqr = (center - p).sqrMagnitude;
                    if (sqr <= bestSqr) { bestSqr = sqr; best = i; }
                }
                if (best >= 0) { sums[best] += p; counts[best]++; }
                else { sums.Add(p); counts.Add(1); }
            }

            var centers = new List<Vector3>(sums.Count);
            for (int i = 0; i < sums.Count; i++) centers.Add(sums[i] / counts[i]);
            return centers;
        }

        private static void MergeCloseCenters(List<Vector3> centers, float minDistance)
        {
            float minSqr = minDistance * minDistance;
            for (int i = 0; i < centers.Count; i++)
            {
                for (int j = centers.Count - 1; j > i; j--)
                {
                    if ((centers[i] - centers[j]).sqrMagnitude <= minSqr)
                    {
                        centers[i] = (centers[i] + centers[j]) * 0.5f;
                        centers.RemoveAt(j);
                    }
                }
            }
        }

        private bool HasPerpendicularPair(List<Vector3> dirs)
        {
            for (int i = 0; i < dirs.Count; i++)
                for (int j = i + 1; j < dirs.Count; j++)
                    if (Mathf.Abs(Vector3.Dot(dirs[i], dirs[j])) < s.PerpendicularDot)
                        return true;
            return false;
        }

        private static void FindPlaneCrossing(LaneRun run, Vector3 center, Vector3 dir, float target,
            out bool found, out float distance, out Vector3 position)
        {
            found = false;
            distance = 0f;
            position = default;

            float prevS = Vector3.Dot(Flatten(run.Points[0] - center), dir);
            for (int i = 1; i < run.Points.Count; i++)
            {
                float curS = Vector3.Dot(Flatten(run.Points[i] - center), dir);
                if ((prevS <= target && curS >= target) || (prevS >= target && curS <= target))
                {
                    float span = curS - prevS;
                    float t = Mathf.Abs(span) < 1e-6f ? 0f : (target - prevS) / span;
                    distance = Mathf.Lerp(run.Cumulative[i - 1], run.Cumulative[i], t);
                    run.Evaluate(distance, out position, out _, out _);
                    found = true;
                    return;
                }
                prevS = curS;
            }
        }

        private static List<Vector3> SampleBezier(Vector3 p0, Vector3 p3, Vector3 inDir, Vector3 outDir,
            float handle, int samples)
        {
            Vector3 p1 = p0 + inDir * handle;
            Vector3 p2 = p3 - outDir * handle;
            return SampleBezierRaw(p0, p1, p2, p3, samples);
        }

        private static List<Vector3> SampleBezierRaw(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
        {
            var pts = new List<Vector3>(samples + 1);
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float mt = 1f - t;
                Vector3 p = mt * mt * mt * p0
                    + 3f * mt * mt * t * p1
                    + 3f * mt * t * t * p2
                    + t * t * t * p3;
                pts.Add(p);
            }
            return pts;
        }

        private static bool SegmentIntersectXZ(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 point)
        {
            point = default;
            float x1 = p1.x, z1 = p1.z, x2 = p2.x, z2 = p2.z;
            float x3 = p3.x, z3 = p3.z, x4 = p4.x, z4 = p4.z;

            float denom = (x1 - x2) * (z3 - z4) - (z1 - z2) * (x3 - x4);
            if (Mathf.Abs(denom) < 1e-6f) return false;

            float t = ((x1 - x3) * (z3 - z4) - (z1 - z3) * (x3 - x4)) / denom;
            float u = ((x1 - x3) * (z1 - z2) - (z1 - z3) * (x1 - x2)) / denom;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;

            point = new Vector3(Mathf.Lerp(x1, x2, t), (p1.y + p2.y) * 0.5f, Mathf.Lerp(z1, z2, t));
            return true;
        }

        private static bool BoundsOverlap(Bounds a, Bounds b, float margin)
        {
            a.Expand(margin * 2f);
            return a.Intersects(b);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        private static float Cross2D(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;

        private void WriteReport(int sectionCount)
        {
            int straight = graph.Edges.Count(e => e.Kind == TrafficEdgeKind.IntersectionStraight);
            int left = graph.Edges.Count(e => e.Kind == TrafficEdgeKind.Turn && e.TurnDirection < 0);
            int right = graph.Edges.Count(e => e.Kind == TrafficEdgeKind.Turn && e.TurnDirection > 0);
            int uturn = graph.Edges.Count(e => e.Kind == TrafficEdgeKind.UTurn);
            int lane = graph.Edges.Count(e => e.Kind == TrafficEdgeKind.Lane);
            float totalLength = graph.Edges.Sum(e => e.Length);

            int threeWay = graph.Intersections.Count(x => arms.TryGetValue(x, out HashSet<int> a) && a.Count == 3);
            int fourWay = graph.Intersections.Count(x => arms.TryGetValue(x, out HashSet<int> a) && a.Count >= 4);

            if (report == null) return;
            report.AppendLine($"Road sections: {sectionCount}");
            report.AppendLine($"Lane runs: {graph.LaneRunCount} (>=45 m: {graph.LaneRunsOver45})");
            report.AppendLine($"Intersections: {graph.Intersections.Count} (3-way: {threeWay}, 4-way+: {fourWay})");
            report.AppendLine($"Edges: {graph.Edges.Count} total | lane {lane}, straight {straight}, left {left}, right {right}, U-turn {uturn}");
            report.AppendLine($"Dead-end nodes: {graph.DeadEndNodeCount}, orphan nodes: {graph.OrphanNodeCount}");
            report.AppendLine($"Nodes: {graph.Nodes.Count}, largest connected component: {graph.LargestComponentNodes} nodes");
            report.AppendLine($"Total edge length: {totalLength:0} m");
        }
    }

    // ------------------------------------------------------------------ internal lane-run model

    private sealed class LaneRun
    {
        public readonly int Index;
        public readonly List<Vector3> Points;
        public readonly List<Vector3> Ups;
        public readonly float[] Cumulative;
        public readonly float Length;
        public readonly bool Closed;
        public readonly Bounds Bounds;
        public readonly List<RunPort> Ports = new();

        public TrafficNode StartFreeNode;
        public TrafficNode EndFreeNode;

        public LaneRun(int index, List<Vector3> points, List<Vector3> ups, bool closed)
        {
            Index = index;
            Points = points;
            Ups = ups;
            Closed = closed;

            Cumulative = new float[points.Count];
            float total = 0f;
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                Cumulative[i] = total;
                bounds.Encapsulate(points[i]);
            }
            Length = total;
            Bounds = bounds;
        }

        public void Evaluate(float distance, out Vector3 position, out Vector3 forward, out Vector3 up)
        {
            if (Points.Count == 1)
            {
                position = Points[0];
                forward = Vector3.forward;
                up = Ups.Count > 0 ? Ups[0] : Vector3.up;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, Length);
            int seg = FindSegment(distance);
            float segStart = Cumulative[seg];
            float segLen = Cumulative[seg + 1] - segStart;
            float t = segLen > 0.0001f ? (distance - segStart) / segLen : 0f;

            Vector3 a = Points[seg];
            Vector3 b = Points[seg + 1];
            position = Vector3.LerpUnclamped(a, b, t);
            Vector3 dir = b - a;
            forward = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            up = Vector3.Slerp(Ups[seg], Ups[seg + 1], t).normalized;
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
        }

        public Vector3 DirectionAt(float distance)
        {
            Evaluate(distance, out _, out Vector3 forward, out _);
            return forward;
        }

        public void NearestInfo(Vector3 target, out float distanceAlong, out Vector3 point, out Vector3 forward)
        {
            distanceAlong = 0f;
            point = Points[0];
            forward = Vector3.forward;
            float best = float.PositiveInfinity;

            for (int i = 0; i + 1 < Points.Count; i++)
            {
                Vector3 a = Points[i];
                Vector3 b = Points[i + 1];
                Vector3 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(target - a, ab) / len2) : 0f;
                Vector3 proj = a + ab * t;
                float sqr = (proj - target).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    point = proj;
                    forward = len2 > 1e-6f ? ab / Mathf.Sqrt(len2) : Vector3.forward;
                    distanceAlong = Cumulative[i] + Mathf.Sqrt(len2) * t;
                }
            }
        }

        private int FindSegment(float distance)
        {
            int low = 0;
            int high = Points.Count - 2;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Cumulative[middle] <= distance) low = middle;
                else high = middle - 1;
            }
            return low;
        }
    }

    private sealed class RunPort
    {
        public readonly float Distance;
        public readonly TrafficNode Node;
        public readonly bool IsEntry;   // false = exit
        public readonly TrafficIntersection Intersection;

        public RunPort(float distance, TrafficNode node, bool isEntry, TrafficIntersection intersection)
        {
            Distance = distance;
            Node = node;
            IsEntry = isEntry;
            Intersection = intersection;
        }
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
        private readonly int ax, ay, az, bx, by, bz;

        public SegmentKey(Vector3 start, Vector3 end)
        {
            ax = Quantize(start.x); ay = Quantize(start.y); az = Quantize(start.z);
            bx = Quantize(end.x); by = Quantize(end.y); bz = Quantize(end.z);
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
