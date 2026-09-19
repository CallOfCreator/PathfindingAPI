namespace PathfindingAPI.Core;

public readonly struct PathEdge<T>
{
    public readonly T Node;
    public readonly float Cost;

    public PathEdge(T node, float cost)
    {
        Node = node;
        Cost = cost;
    }
}