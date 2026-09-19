using PathfindingAPI.Core;
using PathfindingAPI.Navigation;
using PathfindingAPI.Networking;
using System;
using System.Collections;
using UnityEngine;

namespace PathfindingAPI.Movement;

public static class PathTraversalActions
{
    public static IEnumerator Traverse(PlayerControl player, MapPath path, PathCrossing crossing, Action<bool> completed = null, float timeout = 30f)
    {
        if (!float.IsFinite(timeout) || timeout <= 0f)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (path == null || crossing == null || !PlayerPathExtensions.CanContinue(player, path.Ship) || !crossing.Source || crossing.PointIndex < 0 || crossing.PointIndex + 1 >= path.Points.Length || Vector2.Distance(player.GetTruePosition(), path.Points[crossing.PointIndex]) > 0.75f)
        {
            completed?.Invoke(false);
            yield break;
        }

        var elapsed = 0f;
        var traversed = true;
        var destination = path.Points[crossing.PointIndex + 1];
        if (crossing.Type == PathTraversal.Door && path.AutoOpenDoors)
            PathfindingRpc.RpcOpenDoor(player, crossing.Source.Cast<OpenableDoor>().Id);
        if (crossing.Type == PathTraversal.MovingPlatform)
        {
            var platform = crossing.Source.Cast<MovingPlatformBehaviour>();
            while (platform && platform.InUse && elapsed < timeout && PlayerPathExtensions.CanContinue(player, path.Ship))
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!PlayerPathExtensions.CanContinue(player, path.Ship) || !platform || platform.InUse || Vector2.Distance(platform.transform.position, player.transform.position) > 3f)
            {
                completed?.Invoke(false);
                yield break;
            }

            var allowed = false;
            foreach (var console in ShipStatus.Instance.GetComponentsInChildren<PlatformConsole>())
                if (console.Platform == platform)
                {
                    console.CanUse(player.Data, out var canUse, out _);
                    allowed |= canUse;
                }

            if (!allowed)
            {
                completed?.Invoke(false);
                yield break;
            }

            player.RpcUsePlatform();
        }
        else if (crossing.Type == PathTraversal.Ladder)
        {
            var ladder = crossing.Source.Cast<Ladder>();
            ladder.CanUse(player.Data, out var allowed, out _);
            if (!allowed || ladder.IsCoolingDown())
            {
                completed?.Invoke(false);
                yield break;
            }

            player.MyPhysics.RpcClimbLadder(ladder);
        }
        else if (crossing.Type == PathTraversal.Zipline)
        {
            var console = crossing.Source.Cast<ZiplineConsole>();
            console.CanUse(player.Data, out var allowed, out _);
            if (!allowed || console.IsCoolingDown())
            {
                completed?.Invoke(false);
                yield break;
            }

            player.CmdCheckUseZipline(player, console.zipline, console.atTop);
        }
        else if (crossing.Type == PathTraversal.Vent)
        {
            if (player != PlayerControl.LocalPlayer)
            {
                completed?.Invoke(false);
                yield break;
            }

            var vent = crossing.Source.Cast<Vent>();
            var request = MapPathfinding.CreateRequest(player, path.Points[crossing.PointIndex + 1]);
            Vent exit = null;
            foreach (var neighbour in new[] { vent.Left, vent.Right, vent.Center })
                if (request.VentAvailable(neighbour) && Vector2.Distance(neighbour.transform.position + neighbour.Offset, path.Points[crossing.PointIndex + 1]) <= neighbour.UsableDistance)
                    exit = neighbour;
            request.Cancel();
            vent.CanUse(player.Data, out var allowed, out _);
            if (!allowed || !exit)
            {
                completed?.Invoke(false);
                yield break;
            }

            player.MyPhysics.RpcEnterVent(vent.Id);
            do
            {
                yield return null;
                elapsed += Time.deltaTime;
            } while (PlayerPathExtensions.CanContinue(player, path.Ship) && (!player.inVent || player.walkingToVent || player.Visible) && elapsed < timeout);

            if (!PlayerPathExtensions.CanContinue(player, path.Ship) || !player.inVent || elapsed >= timeout)
            {
                completed?.Invoke(false);
                yield break;
            }

            if (!request.VentAvailable(exit) || !vent.TryMoveToVent(exit, out _))
            {
                traversed = false;
                destination = path.Points[crossing.PointIndex];
                exit = vent;
            }

            player.MyPhysics.RpcExitVent(exit.Id);
            exit.SetButtons(false);
        }
        else
        {
            var request = MapPathfinding.CreateRequest(player.GetTruePosition(), path.Points[crossing.PointIndex + 1]);
            while (PlayerPathExtensions.CanContinue(player, path.Ship) && elapsed < timeout && !request.CanMove(player.GetTruePosition(), path.Points[crossing.PointIndex + 1]))
            {
                if (crossing.Type == PathTraversal.Decontamination)
                    foreach (var system in ShipStatus.Instance.GetComponentsInChildren<DeconSystem>())
                        if (system.CurState == DeconSystem.States.Idle && (system.UpperDoor == crossing.Source || system.LowerDoor == crossing.Source))
                        {
                            var upper = system.UpperDoor == crossing.Source;
                            if (system.RoomArea.OverlapPoint(player.GetTruePosition())) system.OpenFromInside(upper);
                            else system.OpenDoor(upper);
                        }

                elapsed += Time.deltaTime;
                yield return null;
            }

            request.Cancel();
            completed?.Invoke(traversed && PlayerPathExtensions.CanContinue(player, path.Ship) && elapsed < timeout);
            yield break;
        }

        do
        {
            yield return null;
            elapsed += Time.deltaTime;
        } while (PlayerPathExtensions.CanContinue(player, path.Ship) && elapsed < timeout && (player.onLadder || player.inMovingPlat || player.inVent || !player.moveable || Vector2.Distance(player.GetTruePosition(), destination) > 1f));

        completed?.Invoke(traversed && PlayerPathExtensions.CanContinue(player, path.Ship) && elapsed < timeout);
    }
}