namespace PathfindingAPI.Movement;

public enum PathMovementStatus
{
    Arrived,
    NoPath,
    Cancelled,
    Interrupted,
    NotOwner,
    Busy,
    Blocked,
    TraversalFailed,
    InvalidPath
}