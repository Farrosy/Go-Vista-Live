using System.Collections.Generic;
using UnityEngine;

public sealed class TrafficRoute
{
    private readonly List<Vector3> points;
    private readonly List<Vector3> ups;
    private readonly float[] cumulative;

    public TrafficRoute(int index, List<Vector3> points, List<Vector3> ups, bool closed)
    {
        Index = index;
        this.points = points;
        this.ups = ups;
        Closed = closed;

        cumulative = new float[points.Count];
        float total = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            total += Vector3.Distance(points[i - 1], points[i]);
            cumulative[i] = total;
        }

        Length = total;
    }

    public int Index { get; }
    public bool Closed { get; }
    public float Length { get; }
    public IReadOnlyList<Vector3> Points => points;
    public List<TrafficCar> Cars { get; } = new();

    public void Evaluate(float distance, out Vector3 position, out Vector3 forward, out Vector3 up)
    {
        if (points.Count == 0)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            up = Vector3.up;
            return;
        }

        if (points.Count == 1)
        {
            position = points[0];
            forward = Vector3.forward;
            up = ups.Count > 0 ? ups[0] : Vector3.up;
            return;
        }

        distance = Closed
            ? (Length > 0.0001f ? Mathf.Repeat(distance, Length) : 0f)
            : Mathf.Clamp(distance, 0f, Length);

        int segment = FindSegment(distance);
        float segmentStart = cumulative[segment];
        float segmentLength = cumulative[segment + 1] - segmentStart;
        float t = segmentLength > 0.0001f ? (distance - segmentStart) / segmentLength : 0f;

        Vector3 a = points[segment];
        Vector3 b = points[segment + 1];
        position = Vector3.LerpUnclamped(a, b, t);

        Vector3 direction = b - a;
        forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        Vector3 upA = segment < ups.Count ? ups[segment] : Vector3.up;
        Vector3 upB = segment + 1 < ups.Count ? ups[segment + 1] : upA;
        up = Vector3.Slerp(upA, upB, t).normalized;
        if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
    }

    public float ClosestDistanceTo(Vector3 position)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < points.Count; i++)
        {
            float sqr = (points[i] - position).sqrMagnitude;
            if (sqr < best) best = sqr;
        }
        return Mathf.Sqrt(best);
    }

    public float GapAhead(TrafficCar self)
    {
        float best = float.PositiveInfinity;
        float selfDistance = self.Distance;
        for (int i = 0; i < Cars.Count; i++)
        {
            TrafficCar car = Cars[i];
            if (car == null || car == self) continue;

            float gap = car.Distance - selfDistance;
            if (Closed)
            {
                if (gap < 0f) gap += Length;
            }
            else if (gap <= 0f)
            {
                continue;
            }

            if (gap > 0.01f && gap < best) best = gap;
        }

        return best;
    }

    private int FindSegment(float distance)
    {
        int low = 0;
        int high = points.Count - 2;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (cumulative[middle] <= distance) low = middle;
            else high = middle - 1;
        }
        return low;
    }
}
