using System.Collections.Generic;
using UnityEngine;

public enum TrafficEdgeKind
{
    Lane,                 // normal road segment between intersections / dead ends
    IntersectionStraight, // straight through an intersection box
    Turn,                 // left/right turn inside an intersection box
    UTurn                 // dead-end turnaround
}

/// <summary>
/// A directed, drivable segment of the lane graph. Cars live on exactly one edge at
/// a time and advance a scalar distance along the polyline. Replaces the old
/// <c>TrafficRoute</c> as the unit a car follows.
/// </summary>
public sealed class TrafficEdge
{
    private readonly Vector3[] points;
    private readonly Vector3[] ups;
    private readonly float[] cumulative;

    public int Id;
    public TrafficEdgeKind Kind;
    public float Length { get; }

    /// <summary>m/s. Lane/straight = <see cref="LaneSpeedLimit"/>, turn ≈5, U-turn ≈3.</summary>
    public float SpeedLimit;

    public TrafficNode StartNode;
    public TrafficNode EndNode;

    /// <summary>Owning intersection for straight/turn edges; null for Lane and UTurn.</summary>
    public TrafficIntersection Intersection;

    /// <summary>True when continuing onto this edge is forced into a U-turn with no other branch.</summary>
    public bool LeadsToDeadEnd;

    /// <summary>-1 = left, +1 = right, 0 = straight/none. Used for turn weighting.</summary>
    public sbyte TurnDirection;

    /// <summary>Cars currently on this edge. Authoritative for car-following; keep add/remove in sync on every transition.</summary>
    public readonly List<TrafficCar> Cars = new();

    public const float LaneSpeedLimit = 999f;

    public IReadOnlyList<Vector3> Points => points;

    public TrafficEdge(int id, TrafficEdgeKind kind, IReadOnlyList<Vector3> polyline,
        IReadOnlyList<Vector3> upVectors = null)
    {
        Id = id;
        Kind = kind;

        int count = polyline?.Count ?? 0;
        points = new Vector3[Mathf.Max(count, 1)];
        ups = new Vector3[points.Length];

        if (count == 0)
        {
            points[0] = Vector3.zero;
            ups[0] = Vector3.up;
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                points[i] = polyline[i];
                Vector3 up = upVectors != null && i < upVectors.Count ? upVectors[i] : Vector3.up;
                if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
                ups[i] = up.normalized;
            }
        }

        cumulative = new float[points.Length];
        float total = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            total += Vector3.Distance(points[i - 1], points[i]);
            cumulative[i] = total;
        }

        Length = total;
        SpeedLimit = LaneSpeedLimit;
    }

    /// <summary>Samples position/forward/up at a distance along the edge (distance is clamped to [0, Length]).</summary>
    public void Evaluate(float distance, out Vector3 position, out Vector3 forward, out Vector3 up)
    {
        if (points.Length == 1)
        {
            position = points[0];
            forward = Vector3.forward;
            up = ups[0];
            return;
        }

        distance = Mathf.Clamp(distance, 0f, Length);
        int segment = FindSegment(distance);
        float segmentStart = cumulative[segment];
        float segmentLength = cumulative[segment + 1] - segmentStart;
        float t = segmentLength > 0.0001f ? (distance - segmentStart) / segmentLength : 0f;

        Vector3 a = points[segment];
        Vector3 b = points[segment + 1];
        position = Vector3.LerpUnclamped(a, b, t);

        Vector3 direction = b - a;
        forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        up = Vector3.Slerp(ups[segment], ups[segment + 1], t).normalized;
        if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
    }

    public Vector3 StartPoint => points[0];
    public Vector3 EndPoint => points[points.Length - 1];

    public Vector3 StartForward
    {
        get
        {
            if (points.Length < 2) return Vector3.forward;
            Vector3 d = points[1] - points[0];
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }
    }

    public Vector3 EndForward
    {
        get
        {
            if (points.Length < 2) return Vector3.forward;
            Vector3 d = points[points.Length - 1] - points[points.Length - 2];
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }
    }

    public float ClosestDistanceTo(Vector3 position)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            float sqr = (points[i] - position).sqrMagnitude;
            if (sqr < best) best = sqr;
        }
        return Mathf.Sqrt(best);
    }

    private int FindSegment(float distance)
    {
        int low = 0;
        int high = points.Length - 2;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (cumulative[middle] <= distance) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    public override string ToString() =>
        $"Edge {Id} [{Kind}] {Length:0.0}m {StartNode?.Id}->{EndNode?.Id}";
}
