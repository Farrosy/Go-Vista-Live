using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An intersection box in the lane graph. Holds the entry/exit nodes for every
/// arm and a single-occupant token so only one car crosses the box at a time.
/// No invisible traffic lights: cars wait at the entry until <see cref="TryAcquire"/> succeeds.
/// </summary>
public sealed class TrafficIntersection
{
    public int Id;
    public Vector3 Center;

    /// <summary>Half the box side length; the box is 2 * HalfSize on each axis (xz).</summary>
    public float HalfSize = 6f;

    public readonly List<TrafficNode> Entries = new();
    public readonly List<TrafficNode> Exits = new();

    /// <summary>The car currently allowed inside the box, or null when free.</summary>
    public TrafficCar Occupant;
    public float OccupiedSince;

    public TrafficIntersection(int id, Vector3 center, float halfSize)
    {
        Id = id;
        Center = center;
        HalfSize = halfSize;
    }

    /// <summary>
    /// Claims the box for <paramref name="car"/>. Returns true if the car already
    /// holds it or the box was free; false if another car holds it.
    /// </summary>
    public bool TryAcquire(TrafficCar car)
    {
        if (Occupant != null && Occupant != car) return false;
        if (Occupant == null)
        {
            Occupant = car;
            OccupiedSince = Time.time;
        }
        return true;
    }

    /// <summary>Releases the box if <paramref name="car"/> is the current occupant.</summary>
    public void Release(TrafficCar car)
    {
        if (Occupant == car) Occupant = null;
    }

    /// <summary>True when the xz position lies inside the box (y is ignored).</summary>
    public bool BoxContains(Vector3 position)
    {
        return Mathf.Abs(position.x - Center.x) <= HalfSize
            && Mathf.Abs(position.z - Center.z) <= HalfSize;
    }

    public override string ToString() =>
        $"Intersection {Id} @ {Center} half={HalfSize:0.0} arms(in={Entries.Count}, out={Exits.Count})";
}
