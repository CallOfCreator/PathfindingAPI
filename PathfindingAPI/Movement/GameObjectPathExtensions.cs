using PathfindingAPI.Core;
using PathfindingAPI.Navigation;
using PathfindingAPI.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PathfindingAPI.Movement;

public static class GameObjectPathExtensions
{
    public static readonly HashSet<GameObject> MovingObjects = new();

    public static IEnumerator CoMoveTo(this GameObject go, Vector2 goal, float speed, Action<PathMovementResult> completed = null, PathOptions options = null, PathMovementSettings movement = null)
    {
        if (!go || !go.activeInHierarchy)
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.InvalidTarget, null));
            yield break;
        }

        var request = MapPathfinding.CreateRequest(go, goal, options);
        yield return request.Run();
        yield return go.FollowPath(request.Result, speed, completed, movement);
    }

    public static IEnumerator FollowPath(this GameObject go, MapPath path, float speed, Action<PathMovementResult> completed = null, PathMovementSettings options = null)
    {
        if (!float.IsFinite(speed) || speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));
        options ??= new PathMovementSettings();
        if (!float.IsFinite(options.Tolerance) || options.Tolerance <= 0f || options.Tolerance > 0.25f || !float.IsFinite(options.StuckTimeout) || options.StuckTimeout <= 0f || options.RecoveryAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(options));

        var ship = ShipStatus.Instance;
        var status = path switch
        {
            null => PathMovementStatus.InvalidPath,
            { Status: PathStatus.Cancelled } => PathMovementStatus.Cancelled,
            _ when !go || !go.activeInHierarchy => PathMovementStatus.InvalidTarget,
            _ when !ship || path.Ship != ship => PathMovementStatus.Interrupted,
            { Status: not PathStatus.Found } => PathMovementStatus.NoPath,
            { Points.Length: 0 } => PathMovementStatus.InvalidPath,
            _ when path.Crossings.Length != 0 => PathMovementStatus.TraversalFailed,
            _ when !MovingObjects.Add(go) => PathMovementStatus.Busy,
            _ => PathMovementStatus.Arrived
        };

        if (status != PathMovementStatus.Arrived)
        {
            completed?.Invoke(new PathMovementResult(status, path));
            yield break;
        }

        var pointIndex = 0;
        try
        {
            var transform = go.transform;
            var request = MapPathfinding.CreateRequest(go, path.Points[^1], new PathOptions { Radius = path.Radius });
            request.Cancel();
            for (; pointIndex < path.Points.Length; pointIndex++)
            {
                var target = path.Points[pointIndex];
                if (!float.IsFinite(target.x) || !float.IsFinite(target.y))
                {
                    status = PathMovementStatus.InvalidPath;
                    break;
                }

                var position = (Vector2)transform.position;
                var stalled = 0f;
                var attempts = 0;
                var recovering = false;
                var tolerance = pointIndex == path.Points.Length - 1 ? options.Tolerance : 0f;
                while (true)
                {
                    if (!go || !go.activeInHierarchy || !ship || ship != ShipStatus.Instance)
                    {
                        status = PathMovementStatus.Interrupted;
                        break;
                    }

                    var current = transform.position;
                    if (Vector2.Distance(current, target) <= (recovering ? 0f : tolerance))
                    {
                        if (!recovering)
                            break;
                        target = path.Points[pointIndex];
                        recovering = false;
                        stalled = 0f;
                    }
                    if (Vector2.Distance(position, current) >= 0.01f)
                    {
                        position = current;
                        stalled = 0f;
                    }
                    else
                    {
                        stalled += Time.deltaTime;
                    }

                    var next = Vector2.MoveTowards(current, target, speed * Time.deltaTime);
                    if (stalled >= options.StuckTimeout || !request.CanMove(current, next))
                    {
                        if (attempts++ < options.RecoveryAttempts && request.FindRecoveryPoint(current, path.Points[pointIndex], out target))
                        {
                            recovering = true;
                            stalled = 0f;
                            continue;
                        }
                        status = PathMovementStatus.Blocked;
                        break;
                    }

                    transform.position = new Vector3(next.x, next.y, current.z);
                    yield return null;
                }

                if (status != PathMovementStatus.Arrived)
                    break;
            }
        }
        finally
        {
            MovingObjects.Remove(go);
        }

        completed?.Invoke(new PathMovementResult(status, path, Math.Min(pointIndex, path.Points.Length - 1)));
    }
}
