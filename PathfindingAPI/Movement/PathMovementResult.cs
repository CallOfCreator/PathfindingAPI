using PathfindingAPI.Navigation;

namespace PathfindingAPI.Movement;

public class PathMovementResult
{
    public readonly PathMovementStatus Status;
    public readonly MapPath Path;
    public readonly int PointIndex;

    public PathMovementResult(PathMovementStatus status, MapPath path, int pointIndex = -1)
    {
        Status = status;
        Path = path;
        PointIndex = pointIndex;
    }
}