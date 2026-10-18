using PathfindingAPI.Navigation;
using PathfindingAPI.Options;
using System;
using System.Collections;

namespace PathfindingAPI.Movement;

public static class PlayerTaskPathExtensions
{
    public static IEnumerator CoMoveTo(this PlayerControl player, Console console, Action<PathMovementResult> completed = null, PathOptions options = null, PathMovementSettings movement = null)
    {
        if (!player.AmOwner)
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.NotOwner, null));
            yield break;
        }
        if (!console || !console.isActiveAndEnabled)
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.InvalidTarget, null));
            yield break;
        }

        var request = MapPathfinding.CreateRequest(player, console, options);
        yield return request.Run();
        yield return player.FollowPath(request.Result, completed, movement);
    }

    public static IEnumerator CoDoTask(this PlayerControl player, TaskTypes taskType, Func<PlayerControl, PlayerTask, Console, IEnumerator> action, Action<PathMovementResult> completed = null, PathOptions options = null, PathMovementSettings movement = null)
    {
        PlayerTask task = null;
        if (player)
            foreach (var candidate in player.myTasks)
                if (candidate && !candidate.IsComplete && candidate.TaskType == taskType)
                {
                    task = candidate;
                    break;
                }

        yield return player.CoDoTask(task, action, completed, options, movement);
    }

    public static IEnumerator CoDoTask(this PlayerControl player, PlayerTask task, Func<PlayerControl, PlayerTask, Console, IEnumerator> action, Action<PathMovementResult> completed = null, PathOptions options = null, PathMovementSettings movement = null)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (!player || !player.AmOwner)
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.NotOwner, null));
            yield break;
        }
        if (!task || task.Owner != player || task.IsComplete || !player.myTasks.Contains(task))
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.InvalidTarget, null));
            yield break;
        }

        var ship = ShipStatus.Instance;
        if (!PlayerPathExtensions.CanContinue(player, ship))
        {
            completed?.Invoke(new PathMovementResult(PathMovementStatus.Interrupted, null));
            yield break;
        }

        var step = task.TaskStep;
        var result = new PathMovementResult(PathMovementStatus.NoPath, null);
        foreach (var console in ship.AllConsoles)
        {
            if (!console || !console.isActiveAndEnabled || !task.ValidConsole(console))
                continue;

            yield return player.CoMoveTo(console, value => result = value, options, movement);
            if (!PlayerPathExtensions.CanContinue(player, ship) || !task || task.Owner != player || task.IsComplete || task.TaskStep != step || !player.myTasks.Contains(task))
            {
                result = new PathMovementResult(PathMovementStatus.Interrupted, result.Path, result.PointIndex);
                break;
            }
            if (result.Status == PathMovementStatus.NoPath)
                continue;
            if (result.Status != PathMovementStatus.Arrived)
                break;
            if (!console || !task.ValidConsole(console))
            {
                result = new PathMovementResult(PathMovementStatus.InvalidTarget, result.Path, result.PointIndex);
                break;
            }

            yield return action(player, task, console);
            break;
        }

        completed?.Invoke(result);
    }
}