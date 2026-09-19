using UnityEngine;

namespace PathfindingAPI.Core;

public class PathLink
{
    public readonly Vector2 Start;
    public readonly Vector2 End;
    public readonly PathTraversal Type;

    public PathLink(Vector2 start, Vector2 end, PathTraversal type)
    {
        Start = start;
        End = end;
        Type = type;
    }
}