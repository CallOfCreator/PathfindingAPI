using PathfindingAPI.Core;
using UnityEngine;

namespace PathfindingAPI.Navigation;

public class PathCrossing
{
    public readonly int PointIndex;
    public readonly PathTraversal Type;
    public readonly Component Source;

    public PathCrossing(int pointIndex, PathTraversal type, Component source)
    {
        PointIndex = pointIndex;
        Type = type;
        Source = source;
    }
}