using System.Collections.Generic;
using UnityEngine;

/// A stretch of one route where cars cross or merge into another route. Cars take turns through it.
public sealed class TrafficConflict
{
    public readonly float Start;
    public readonly float End;
    public readonly TrafficRoute Other;
    public readonly float OtherStart;
    public readonly float OtherEnd;

    public TrafficConflict(float start, float end, TrafficRoute other, float otherStart, float otherEnd)
    {
        Start = start;
        End = end;
        Other = other;
        OtherStart = otherStart;
        OtherEnd = otherEnd;
    }

    /// A car already inside goes first, otherwise whoever arrived first (lowest ticket). A ticket of 0 has not arrived.
    public static bool MustYield(bool selfInside, int selfTicket, bool otherInside, int otherTicket)
    {
        if (selfInside != otherInside) return otherInside;
        return otherTicket != 0 && otherTicket < selfTicket;
    }
}

/// Finds where the fixed routes cross or merge by comparing their sampled points.
public static class TrafficConflictBuilder
{
    // Centre-line distance at which two car bodies (about 2.4 m wide) start to need separate turns.
    private const float k_Clearance = 3f;
    private const float k_MaxHeightGap = 2f;
    // Beyond this the routes run side by side: lanes in the same direction are handled by car following.
    private const float k_ParallelDot = 0.85f;
    private const float k_SharedLaneGap = 1f;
    // Routes are sampled every 2 m, so a span can start up to one sample late.
    private const float k_SamplePad = 1f;
    private const int k_MaxPointGap = 2;
    private const int k_MaxOtherPointGap = 4;

    private enum HitKind { Conflict, SharedLane, Parallel }

    private readonly struct Hit
    {
        public readonly int Point;
        public readonly int OtherPoint;
        public readonly HitKind Kind;
        public readonly float SqrDistance;

        public Hit(int point, int otherPoint, HitKind kind, float sqrDistance)
        {
            Point = point;
            OtherPoint = otherPoint;
            Kind = kind;
            SqrDistance = sqrDistance;
        }
    }

    /// Fills every route's Conflicts list and returns how many spans were found (each counted once).
    public static int Build(IReadOnlyList<TrafficRoute> routes)
    {
        var grid = new Dictionary<(int, int), List<(int route, int point)>>();
        for (int r = 0; r < routes.Count; r++)
        {
            routes[r].Conflicts.Clear();
            IReadOnlyList<Vector3> points = routes[r].Points;
            for (int p = 0; p < points.Count; p++)
            {
                (int, int) cell = Cell(points[p]);
                if (!grid.TryGetValue(cell, out List<(int route, int point)> entries))
                    grid[cell] = entries = new List<(int route, int point)>();
                entries.Add((r, p));
            }
        }

        int count = 0;
        var closest = new Dictionary<int, Hit>();
        var hitsByOther = new Dictionary<int, List<Hit>>();

        for (int a = 0; a < routes.Count; a++)
        {
            hitsByOther.Clear();
            TrafficRoute route = routes[a];
            for (int i = 0; i < route.Points.Count; i++)
            {
                CollectClosestHits(routes, grid, a, i, closest);
                foreach (KeyValuePair<int, Hit> pair in closest)
                {
                    if (!hitsByOther.TryGetValue(pair.Key, out List<Hit> hits))
                        hitsByOther[pair.Key] = hits = new List<Hit>();
                    hits.Add(pair.Value);
                }
            }

            foreach (KeyValuePair<int, List<Hit>> pair in hitsByOther)
                count += AddSpans(route, routes[pair.Key], pair.Value);
        }

        return count;
    }

    private static (int, int) Cell(Vector3 point) =>
        (Mathf.FloorToInt(point.x / k_Clearance), Mathf.FloorToInt(point.z / k_Clearance));

    private static void CollectClosestHits(IReadOnlyList<TrafficRoute> routes,
        Dictionary<(int, int), List<(int route, int point)>> grid, int routeIndex, int pointIndex, Dictionary<int, Hit> closest)
    {
        closest.Clear();
        TrafficRoute route = routes[routeIndex];
        Vector3 point = route.Points[pointIndex];
        (int cellX, int cellZ) = Cell(point);
        float clearanceSqr = k_Clearance * k_Clearance;

        for (int x = cellX - 1; x <= cellX + 1; x++)
        {
            for (int z = cellZ - 1; z <= cellZ + 1; z++)
            {
                if (!grid.TryGetValue((x, z), out List<(int route, int point)> entries)) continue;

                for (int e = 0; e < entries.Count; e++)
                {
                    // Each pair of routes is handled once, from its lower index.
                    if (entries[e].route <= routeIndex) continue;

                    TrafficRoute other = routes[entries[e].route];
                    Vector3 otherPoint = other.Points[entries[e].point];
                    if (Mathf.Abs(point.y - otherPoint.y) > k_MaxHeightGap) continue;

                    float sqr = Sqr(point.x - otherPoint.x) + Sqr(point.z - otherPoint.z);
                    if (sqr > clearanceSqr) continue;
                    if (closest.TryGetValue(entries[e].route, out Hit best) && best.SqrDistance <= sqr) continue;

                    float dot = Vector3.Dot(FlatTangent(route, pointIndex), FlatTangent(other, entries[e].point));
                    HitKind kind = Mathf.Abs(dot) < k_ParallelDot ? HitKind.Conflict
                        : dot > 0f && sqr <= k_SharedLaneGap * k_SharedLaneGap ? HitKind.SharedLane
                        : HitKind.Parallel;
                    closest[entries[e].route] = new Hit(pointIndex, entries[e].point, kind, sqr);
                }
            }
        }
    }

    private static int AddSpans(TrafficRoute route, TrafficRoute other, List<Hit> hits)
    {
        int count = 0;
        int index = 0;
        while (index < hits.Count)
        {
            if (hits[index].Kind != HitKind.Conflict)
            {
                index++;
                continue;
            }

            int first = index;
            int last = index;
            int minOther = hits[index].OtherPoint;
            int maxOther = minOther;
            while (last + 1 < hits.Count && hits[last + 1].Kind == HitKind.Conflict
                   && hits[last + 1].Point - hits[last].Point <= k_MaxPointGap
                   && Mathf.Abs(hits[last + 1].OtherPoint - hits[last].OtherPoint) <= k_MaxOtherPointGap)
            {
                last++;
                minOther = Mathf.Min(minOther, hits[last].OtherPoint);
                maxOther = Mathf.Max(maxOther, hits[last].OtherPoint);
            }

            index = last + 1;

            // Routes leaving a shared lane only move apart, and the car in front is already followed there.
            if (StartsFromSharedLane(hits, first)) continue;

            ExpandOtherSpan(route, other, hits[first].Point, hits[last].Point, ref minOther, ref maxOther);

            float start = Mathf.Max(0f, route.DistanceAt(hits[first].Point) - k_SamplePad);
            float end = Mathf.Min(route.Length, route.DistanceAt(hits[last].Point) + k_SamplePad);
            float otherStart = Mathf.Max(0f, other.DistanceAt(minOther) - k_SamplePad);
            float otherEnd = Mathf.Min(other.Length, other.DistanceAt(maxOther) + k_SamplePad);

            route.Conflicts.Add(new TrafficConflict(start, end, other, otherStart, otherEnd));
            other.Conflicts.Add(new TrafficConflict(otherStart, otherEnd, route, start, end));
            count++;
        }

        return count;
    }

    private static bool StartsFromSharedLane(List<Hit> hits, int first)
    {
        for (int i = first - 1; i >= 0; i--)
        {
            if (hits[i + 1].Point - hits[i].Point > k_MaxPointGap) return false;
            if (hits[i].Kind == HitKind.SharedLane) return true;
        }
        return false;
    }

    // Each hit keeps only the nearest point of the other route, which at a square crossing is a single
    // point; the other route needs its whole stretch within clearance of this span.
    private static void ExpandOtherSpan(TrafficRoute route, TrafficRoute other, int firstPoint, int lastPoint,
        ref int minOther, ref int maxOther)
    {
        while (minOther > 0 && IsNearSpan(route, firstPoint, lastPoint, other.Points[minOther - 1])) minOther--;
        while (maxOther < other.Points.Count - 1 && IsNearSpan(route, firstPoint, lastPoint, other.Points[maxOther + 1])) maxOther++;
    }

    private static bool IsNearSpan(TrafficRoute route, int firstPoint, int lastPoint, Vector3 point)
    {
        float clearanceSqr = k_Clearance * k_Clearance;
        for (int i = firstPoint; i <= lastPoint; i++)
        {
            Vector3 spanPoint = route.Points[i];
            if (Sqr(spanPoint.x - point.x) + Sqr(spanPoint.z - point.z) <= clearanceSqr) return true;
        }
        return false;
    }

    private static Vector3 FlatTangent(TrafficRoute route, int point)
    {
        int from = Mathf.Min(point, route.Points.Count - 2);
        Vector3 direction = route.Points[from + 1] - route.Points[from];
        direction.y = 0f;
        return direction.normalized;
    }

    private static float Sqr(float value) => value * value;
}
