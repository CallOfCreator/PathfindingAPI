using PathfindingAPI.Core;
using PathfindingAPI.Navigation;
using PathfindingAPI.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PathfindingAPI.Movement;

public static class PlayerPathExtensions
{
    public static readonly HashSet<PlayerControl> MovingPlayers = new();

    public static IEnumerator CoMoveTo(this PlayerControl player, Vector2 goal, Action<PathMovementResult> completed = null, PathOptions options = null, PathMovementSettings movement = null)
    {
        if (!player || !player.AmOwner)
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.NotOwner, null));
            yield break;
        }

        var request = MapPathfinding.CreateRequest(player, goal, options);
        yield return request.Run();

        yield return player.FollowPath(request.Result, completed, movement);
    }

    public static IEnumerator FollowPath(this PlayerControl player, MapPath path, Action<PathMovementResult> completed = null, PathMovementSettings options = null)
    {
        options ??= new PathMovementSettings();
        if (!float.IsFinite(options.Tolerance) || options.Tolerance <= 0f || options.Tolerance > 0.25f || !float.IsFinite(options.StuckTimeout) || options.StuckTimeout <= 0f || !float.IsFinite(options.TraversalTimeout) || options.TraversalTimeout <= 0f)
            throw new ArgumentOutOfRangeException(nameof(options));

        var status = PathMovementStatus.Arrived;
        var ship = ShipStatus.Instance;
        if (path == null)
            status = PathMovementStatus.InvalidPath;
        else if (path.Status == PathStatus.Cancelled)
            status = PathMovementStatus.Cancelled;
        else if (!player || !player.AmOwner)
            status = PathMovementStatus.NotOwner;
        else if (path.Ship != ship || !CanContinue(player, ship))
            status = PathMovementStatus.Interrupted;
        else if (path.Status != PathStatus.Found)
            status = PathMovementStatus.NoPath;
        else if (path.Points.Length == 0)
            status = PathMovementStatus.InvalidPath;
        else if (!player.CanMove || !MovingPlayers.Add(player))
            status = PathMovementStatus.Busy;

        if (status != PathMovementStatus.Arrived)
        {
            completed?.Invoke(new PathMovementResult(status, path));
            yield break;
        }

        var pointIndex = 0;
        var crossingIndex = 0;
        try
        {
            foreach (var crossing in path.Crossings)
            {
                if (crossing.PointIndex < crossingIndex || crossing.PointIndex + 1 >= path.Points.Length)
                {
                    status = PathMovementStatus.InvalidPath;
                    break;
                }

                crossingIndex = crossing.PointIndex + 1;
            }

            crossingIndex = 0;

            for (; pointIndex < path.Points.Length && status == PathMovementStatus.Arrived; pointIndex++)
            {
                if (!CanContinue(player, ship) || !player.CanMove)
                {
                    status = PathMovementStatus.Interrupted;
                    break;
                }

                var target = path.Points[pointIndex];
                if (!float.IsFinite(target.x) || !float.IsFinite(target.y))
                {
                    status = PathMovementStatus.InvalidPath;
                    break;
                }

                var walking = player.MyPhysics.WalkPlayerTo(target, options.Tolerance * options.Tolerance, player.MyPhysics.TrueSpeed / player.MyPhysics.Speed);
                var position = player.GetTruePosition();
                var stalled = 0f;
                player.moveable = false;
                try
                {
                    while (true)
                    {
                        if (!CanContinue(player, ship))
                        {
                            status = PathMovementStatus.Interrupted;
                            break;
                        }

                        if (Vector2.Distance(player.GetTruePosition(), target) <= options.Tolerance)
                            break;
                        if (!walking.MoveNext())
                        {
                            if (Vector2.Distance(player.GetTruePosition(), target) > options.Tolerance)
                                status = PathMovementStatus.Blocked;
                            break;
                        }

                        if (Vector2.Distance(position, player.GetTruePosition()) >= 0.01f)
                        {
                            position = player.GetTruePosition();
                            stalled = 0f;
                        }
                        else
                        {
                            stalled += Time.deltaTime;
                        }

                        if (stalled >= options.StuckTimeout)
                        {
                            status = PathMovementStatus.Blocked;
                            break;
                        }

                        yield return null;
                    }
                }
                finally
                {
                    if (player)
                    {
                        player.MyPhysics.body.velocity = Vector2.zero;
                        if (ship == ShipStatus.Instance && !MeetingHud.Instance && player.Data && !player.Data.IsDead)
                            player.moveable = true;
                    }
                }

                if (status != PathMovementStatus.Arrived)
                    break;
                if (crossingIndex < path.Crossings.Length && path.Crossings[crossingIndex].PointIndex == pointIndex)
                {
                    var traversed = false;
                    yield return PathTraversalActions.Traverse(player, path, path.Crossings[crossingIndex++], success => traversed = success, options.TraversalTimeout);
                    if (!CanContinue(player, ship))
                        status = PathMovementStatus.Interrupted;
                    else if (!traversed)
                        status = PathMovementStatus.TraversalFailed;
                    if (status != PathMovementStatus.Arrived)
                        break;
                }
            }
        }
        finally
        {
            MovingPlayers.Remove(player);
        }

        completed?.Invoke(new PathMovementResult(status, path, Math.Min(pointIndex, path.Points.Length - 1)));
    }

    public static bool CanContinue(PlayerControl player, ShipStatus ship)
    {
        return player && player.AmOwner && player.Data && !player.Data.IsDead && !player.Data.Disconnected && ship && ship == ShipStatus.Instance && !MeetingHud.Instance && !ExileController.Instance && !Minigame.Instance;
    }
}