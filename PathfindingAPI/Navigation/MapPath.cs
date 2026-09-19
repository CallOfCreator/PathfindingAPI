using PathfindingAPI.Core;
using System;
using UnityEngine;

namespace PathfindingAPI.Navigation;

public class MapPath
{
    public readonly PathStatus Status;
    public readonly Vector2[] Points;
    public readonly float Length;
    public readonly int ExpandedNodes;
    public ShipStatus Ship;
    public bool AutoOpenDoors;
    public PathCrossing[] Crossings = Array.Empty<PathCrossing>();

    public MapPath(PathStatus status, Vector2[] points, float length, int expandedNodes)
    {
        Status = status;
        Points = points;
        Length = length;
        ExpandedNodes = expandedNodes;
    }
}