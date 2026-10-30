using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A physical connection point in the lane graph. Nodes sit at lane-run ends,
/// dead ends, and intersection entry/exit points. Edges reference nodes for
/// routing; a car transitions from one edge to the next at the shared node.
/// </summary>
public sealed class TrafficNode
{
    public int Id;
    public Vector3 Position;

    /// <summary>Entry/exit nodes carry their owning intersection; null for dead ends and plain lane joins.</summary>
    public TrafficIntersection Intersection;

    /// <summary>True when this node is an intersection entry: its outgoing edges are movement edges (straight/turn).</summary>
    public bool IsEntry;

    /// <summary>True when this node is an intersection exit: its outgoing edge is the lane leaving the box.</summary>
    public bool IsExit;

    /// <summary>True when this node is a dead end: its only outgoing edge is a U-turn.</summary>
    public bool IsDeadEnd;

    /// <summary>
    /// True when every forward path from this node is forced into a U-turn before
    /// reaching an intersection that offers a non-dead-end branch. Used by turn weighting.
    /// </summary>
    public bool LeadsToDeadEnd;

    public readonly List<TrafficEdge> Outgoing = new();
    public readonly List<TrafficEdge> Incoming = new();

    public TrafficNode(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }

    public override string ToString() =>
        $"Node {Id} @ {Position} (entry={IsEntry}, exit={IsExit}, deadEnd={IsDeadEnd}, out={Outgoing.Count})";
}
