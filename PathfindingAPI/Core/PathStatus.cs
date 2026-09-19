namespace PathfindingAPI.Core;

public enum PathStatus
{
    Searching,
    Found,
    Unreachable,
    LimitReached,
    Cancelled,
    InvalidEndpoint,
    Obstructed
}