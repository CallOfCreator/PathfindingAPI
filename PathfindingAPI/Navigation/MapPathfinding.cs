using PathfindingAPI.Movement;
using PathfindingAPI.Compatibility;
using PathfindingAPI.Options;
using System;
using System.Collections;
using UnityEngine;

namespace PathfindingAPI.Navigation;

public static class MapPathfinding
{
    public static MapPathRequest CreateRequest(GameObject go, Vector2 goal, PathOptions options = null)
    {
        return new MapPathRequest(go ? ShipStatus.Instance : null, go ? (Vector2)go.transform.position : default, goal, options, go: go);
    }

    public static IEnumerator FindPath(GameObject go, Vector2 goal, Action<MapPath> completed = null, PathOptions options = null)
    {
        var request = CreateRequest(go, goal, options);
        yield return request.Run();
        completed?.Invoke(request.Result);
    }

    public static MapPathRequest CreateRequest(PlayerControl player, Console console, PathOptions options = null)
    {
        var canVent = player && player.Data && player.Data.Role && !player.Data.IsDead && player.Data.Role.CanVent;
        options ??= new PathOptions { UseVents = canVent };
        return new MapPathRequest(player && console ? ShipStatus.Instance : null, player ? player.GetTruePosition() : default, console ? (Vector2)console.transform.position : default, options, player, console);
    }

    public static IEnumerator FindPath(PlayerControl player, Console console, Action<MapPath> completed = null, PathOptions options = null)
    {
        var request = CreateRequest(player, console, options);
        yield return request.Run();
        completed?.Invoke(request.Result);
    }

    public static bool IsSubmergedLoaded()
    {
        return SubmergedCompatibility.IsLoaded();
    }

    public static bool IsSubmergedMap()
    {
        return ShipStatus.Instance && (int)ShipStatus.Instance.Type == 6 && SubmergedCompatibility.IsLoaded();
    }

    public static MapPathRequest CreateRequest(PlayerControl player, Vector2 goal, PathOptions options = null)
    {
        if (!player)
            return new MapPathRequest(null, default, goal, options ?? new PathOptions());
        var canVent = player.Data && player.Data.Role && !player.Data.IsDead && player.Data.Role.CanVent;
        options ??= new PathOptions { UseVents = canVent };
        return new MapPathRequest(ShipStatus.Instance, player.GetTruePosition(), goal, options, player);
    }

    public static IEnumerator FindPath(PlayerControl player, Vector2 goal, Action<MapPath> completed = null, PathOptions options = null)
    {
        var request = CreateRequest(player, goal, options);
        yield return request.Run();
        completed?.Invoke(request.Result);
    }

    public static IEnumerator Traverse(PlayerControl player, MapPath path, PathCrossing crossing, Action<bool> completed = null, float timeout = 30f)
    {
        return PathTraversalActions.Traverse(player, path, crossing, completed, timeout);
    }

    public static IEnumerator FindPath(Vector2 start, Vector2 goal, Action<MapPath> completed = null, PathOptions options = null)
    {
        var request = CreateRequest(start, goal, options);
        yield return request.Run();
        completed?.Invoke(request.Result);
    }

    public static MapPathRequest CreateRequest(Vector2 start, Vector2 goal, PathOptions options = null)
    {
        return new MapPathRequest(ShipStatus.Instance, start, goal, options ?? new PathOptions());
    }
}
