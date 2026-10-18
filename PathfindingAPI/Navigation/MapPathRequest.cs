using PathfindingAPI.Core;
using PathfindingAPI.Compatibility;
using PathfindingAPI.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace PathfindingAPI.Navigation;

public class MapPathRequest
{
    public readonly List<PathLink> links = new();
    public readonly List<Component> linkSources = new();
    public readonly Il2CppStructArray<RaycastHit2D> crossingHits = new(32);
    public readonly SubmergedCompatibility submerged;
    public readonly PlayerControl actor;
    public readonly GameObject go;
    public readonly Console console;
    public readonly ShipStatus ship;
    public readonly PathOptions options;
    public readonly OpenableDoor[] doors;
    public readonly bool[] doorStates;
    public readonly ContactFilter2D filter;
    public readonly Il2CppStructArray<RaycastHit2D> hits;
    public readonly Il2CppReferenceArray<Collider2D> overlaps;
    public readonly WalkingGrid grid;
    public readonly AStarSearch<int> search;

    public MapPath Result { get; private set; }
    public PathStatus Status => Result?.Status ?? search.Status;
    public int ExpandedNodes => search?.ExpandedNodes ?? 0;

    public MapPathRequest(ShipStatus ship, Vector2 start, Vector2 goal, PathOptions options, PlayerControl actor = null, Console console = null, GameObject go = null)
    {
        options ??= new PathOptions();
        if (!float.IsFinite(options.CellSize) || options.CellSize <= 0f || !float.IsFinite(options.Radius) || options.Radius <= 0f || !float.IsFinite(options.ConnectionRange) || options.ConnectionRange < options.CellSize || options.NodeLimit <= 0 || options.BatchSize <= 0 || !float.IsFinite(options.WallWeight) || options.WallWeight < 0f || !float.IsFinite(options.WallDistance) || options.WallDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(options));

        this.actor = actor;
        this.console = console;
        this.go = go;
        this.ship = ship;
        this.options = options;
        hits = new Il2CppStructArray<RaycastHit2D>(go ? 32 : 1);
        overlaps = new Il2CppReferenceArray<Collider2D>(go ? 32 : 1);
        filter = new ContactFilter2D { useLayerMask = true, layerMask = Constants.ShipAndAllObjectsMask, useTriggers = false };
        if (!ship || !float.IsFinite(start.x) || !float.IsFinite(start.y) || !float.IsFinite(goal.x) || !float.IsFinite(goal.y))
        {
            Finish(PathStatus.InvalidEndpoint);
            return;
        }

        submerged = SubmergedCompatibility.Get(ship);
        if ((int)ship.Type == 6 && SubmergedCompatibility.IsLoaded() && (submerged == null || !submerged.Supported))
        {
            Finish(PathStatus.Obstructed);
            return;
        }

        doors = ship.AllDoors;
        doorStates = new bool[doors.Length];
        for (var i = 0; i < doors.Length; i++)
            doorStates[i] = doors[i] && doors[i].IsOpen;

        var bounds = new Bounds();
        var hasBounds = false;
        foreach (var room in ship.AllRooms)
        {
            if (!room.roomArea)
                continue;
            if (hasBounds)
                bounds.Encapsulate(room.roomArea.bounds);
            else
                bounds = room.roomArea.bounds;
            hasBounds = true;
        }

        bounds.Expand(options.CellSize * 2f);
        var area = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        if (!hasBounds || !area.Contains(start) || !IsClear(start) || !console && (!area.Contains(goal) || !IsClear(goal)))
        {
            Finish(PathStatus.InvalidEndpoint);
            return;
        }

        if (!go)
            CollectLinks();
        grid = new WalkingGrid(start, goal, area, options.CellSize, options.ConnectionRange, IsClear, CanMove, links, options.WallWeight > 0f ? WallPenalty : null);
        var usableDistance = console ? console.UsableDistance : 0f;
        search = console ? new AStarSearch<int>(WalkingGrid.StartNode, WalkingGrid.GoalNode, grid.Edges, node => Math.Max(0f, grid.Estimate(node) - usableDistance), options.NodeLimit, node => CanUseConsole(grid.Position(node))) : new AStarSearch<int>(WalkingGrid.StartNode, WalkingGrid.GoalNode, grid.Edges, grid.Estimate, options.NodeLimit);
    }

    public IEnumerator Run()
    {
        while (Status == PathStatus.Searching)
        {
            Step();
            if (Status == PathStatus.Searching)
                yield return null;
        }
    }

    public void Step()
    {
        if (Result != null)
            return;
        if (!ship)
        {
            Cancel();
            return;
        }

        if (DoorsChanged())
        {
            Finish(PathStatus.Obstructed);
            return;
        }

        search.Step(options.BatchSize);
        if (search.Status == PathStatus.Found)
            BuildPath();
        else if (search.Status != PathStatus.Searching)
            Finish(search.Status);
    }

    public void Cancel()
    {
        if (Result == null)
        {
            search.Cancel();
            Finish(PathStatus.Cancelled);
        }
    }

    public bool IsClear(Vector2 point)
    {
        return IsClear(point, options.Radius);
    }

    public bool IsClear(Vector2 point, float radius)
    {
        var count = Physics2D.OverlapCircle(point, radius, filter, overlaps);
        if (count == overlaps.Length)
            return false;
        for (var i = 0; i < count; i++)
        {
            if (IsObstacle(overlaps[i]))
                return false;
        }
        return true;
    }

    public bool IsObstacle(Collider2D collider)
    {
        return !go || !collider.transform.IsChildOf(go.transform);
    }

    public bool CanUseConsole(Vector2 point)
    {
        if (!console || !console.isActiveAndEnabled)
            return false;
        var position = console.transform.position;
        if (Vector2.Distance(point, position) > console.UsableDistance * 0.9f || console.onlyFromBelow && point.y >= position.y - 0.1f)
            return false;
        if (console.onlySameRoom && (!ship.FastRooms.TryGetValue(console.Room, out var room) || !room || !room.roomArea || !room.roomArea.OverlapPoint(point)))
            return false;
        return !console.checkWalls || !PhysicsHelpers.AnythingBetween(point, position, Constants.ShadowMask, false);
    }

    public float WallPenalty(Vector2 point)
    {
        for (var i = 1; i <= 3; i++)
        {
            if (!IsClear(point, options.Radius + options.WallDistance * i / 3f))
                return options.WallWeight * (4 - i) / 3f;
        }
        return 0f;
    }

    public bool CanMove(Vector2 from, Vector2 to)
    {
        if (submerged != null && submerged.IsUpper(from) != submerged.IsUpper(to)) return false;
        var delta = to - from;
        if (!IsClear(from) || !IsClear(to))
            return false;
        if (delta.sqrMagnitude < 0.000001f)
            return true;
        var count = Physics2D.CircleCast(from, options.Radius, delta.normalized, filter, hits, delta.magnitude);
        if (count == hits.Length)
            return false;
        for (var i = 0; i < count; i++)
        {
            if (IsObstacle(hits[i].collider))
                return false;
        }
        return true;
    }

    public bool FindRecoveryPoint(Vector2 from, Vector2 target, out Vector2 point)
    {
        var forward = (target - from).normalized;
        var side = new Vector2(-forward.y, forward.x);
        var best = float.MaxValue;
        point = from;
        for (var i = 1; i <= 4; i++)
        {
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var candidate = from + side * (sign * options.CellSize * i / 2f) - forward * (options.CellSize / 2f);
                if (!CanMove(from, candidate) || !CanMove(candidate, target))
                    continue;
                var weight = Vector2.Distance(candidate, target) + Vector2.Distance(from, candidate) + WallPenalty(candidate) * 2f;
                if (weight >= best)
                    continue;
                best = weight;
                point = candidate;
            }
        }
        return best < float.MaxValue;
    }

    public bool DoorsChanged()
    {
        for (var i = 0; i < doors.Length; i++)
        {
            if (submerged != null && submerged.IsElevatorDoor(doors[i])) continue;
            if (!doors[i] || doors[i].IsOpen != doorStates[i]) return true;
        }
        return false;
    }

    public void CollectLinks()
    {
        submerged?.AddLinks(this);
        if (options.UseLadders)
            foreach (var ladder in ship.GetComponentsInChildren<Ladder>())
            {
                if (ladder.isActiveAndEnabled && ladder.Destination && ladder.Destination.isActiveAndEnabled)
                    AddTransportLink(ladder.transform.position, ladder.Destination.transform.position, ladder.UsableDistance, ladder.Destination.UsableDistance, PathTraversal.Ladder, ladder);
            }

        if (options.UseZiplines)
            foreach (var console in ship.GetComponentsInChildren<ZiplineConsole>())
            {
                if (!console.isActiveAndEnabled || !console.zipline || !console.zipline.isActiveAndEnabled || !console.destination || !console.destination.isActiveAndEnabled)
                    continue;
                var zipline = console.zipline;
                var landing = console.atTop ? zipline.landingPositionBottom : zipline.landingPositionTop;
                AddTransportLink(console.transform.position, zipline.transform.TransformPoint(landing.position), console.UsableDistance, console.destination.UsableDistance, PathTraversal.Zipline, console);
            }

        if (options.UseVents)
            foreach (var vent in ship.AllVents)
            {
                if (!VentAvailable(vent) || submerged != null && vent.Id == submerged.EngineVentId) continue;
                foreach (var next in new[] { vent.Left, vent.Right, vent.Center })
                {
                    if (VentAvailable(next))
                    {
                        if (submerged != null) submerged.AddVentLink(this, vent, next);
                        else AddTransportLink(vent.transform.position + vent.Offset, next.transform.position + next.Offset, vent.UsableDistance, next.UsableDistance, PathTraversal.Vent, vent);
                    }
                }
            }

        if (options.UseMovingPlatforms)
            foreach (var platform in ship.GetComponentsInChildren<MovingPlatformBehaviour>())
            {
                if (!platform.isActiveAndEnabled || !platform.transform.parent || GameOptionsManager.Instance.CurrentGameOptions.GameMode == AmongUs.GameOptions.GameModes.HideNSeek && platform.DisabledPosition != Vector3.zero)
                    continue;
                var parent = platform.transform.parent;
                var left = (Vector2)parent.TransformPoint(platform.LeftUsePosition);
                var right = (Vector2)parent.TransformPoint(platform.RightUsePosition);
                AddTransportLink(left, right, options.ConnectionRange, options.ConnectionRange, PathTraversal.MovingPlatform, platform);
                AddTransportLink(right, left, options.ConnectionRange, options.ConnectionRange, PathTraversal.MovingPlatform, platform);
            }

        if (options.WaitForDoors || options.AutoOpenDoors)
            foreach (var door in doors)
                AddDoorLink(door, PathTraversal.Door);

        if (options.UseDecontamination)
            foreach (var decon in ship.GetComponentsInChildren<DeconSystem>())
            {
                if (decon.isActiveAndEnabled)
                {
                    AddDoorLink(decon.UpperDoor);
                    AddDoorLink(decon.LowerDoor);
                }
            }
    }

    public bool VentAvailable(Vent vent)
    {
        if (!vent || !vent.isActiveAndEnabled) return false;
        if (actor && (actor != PlayerControl.LocalPlayer || actor.Data == null || actor.Data.IsDead || !actor.Data.Role || !actor.Data.Role.CanVent || !actor.Data.Role.CanUse(vent.Cast<IUsable>()) || actor.MustCleanVent(vent.Id))) return false;
        if (ship.Systems.TryGetValue(SystemTypes.Ventilation, out var system))
        {
            var ventilation = system.TryCast<VentilationSystem>();
            if (ventilation != null && (submerged != null ? submerged.IsVentBlocked(vent, ventilation) : ventilation.IsVentCurrentlyBeingCleaned(vent.Id))) return false;
        }

        return true;
    }

    public void AddTransportLink(Vector2 from, Vector2 to, float entryRange, float exitRange, PathTraversal type, Component source)
    {
        if (!WalkingGrid.TryApproach(from, from - to, entryRange, IsClear, CanReachInteraction, out var entry) || !WalkingGrid.TryApproach(to, to - from, exitRange, IsClear, CanReachInteraction, out var exit))
            return;
        AddLink(entry, exit, type, source);
    }

    public bool CanReachInteraction(Vector2 from, Vector2 to)
    {
        return !PhysicsHelpers.AnythingBetween(from, to, Constants.ShipOnlyMask, false);
    }

    public void AddLink(Vector2 from, Vector2 to, PathTraversal type, Component source)
    {
        if (!IsClear(from) || !IsClear(to))
            return;
        links.Add(new PathLink(from, to, type));
        linkSources.Add(source);
    }

    public void AddDoorLink(SomeKindaDoor door, PathTraversal type = PathTraversal.Decontamination)
    {
        if (!door || !door.isActiveAndEnabled || submerged != null && submerged.IsElevatorDoor(door))
            return;
        var manual = door.TryCast<ManualDoor>();
        var plain = door.TryCast<PlainDoor>();
        var collider = manual ? manual.myCollider : plain ? plain.myCollider : null;
        if (!collider)
            return;
        var bounds = collider.bounds;
        var margin = options.Radius + options.CellSize;
        var offset = bounds.size.x > bounds.size.y ? new Vector2(0f, bounds.extents.y + margin) : new Vector2(bounds.extents.x + margin, 0f);
        var from = (Vector2)bounds.center - offset;
        var to = (Vector2)bounds.center + offset;
        if (!CanCrossDoor(from, to, door))
            return;
        AddLink(from, to, type, door);
        AddLink(to, from, type, door);
    }

    public bool CanCrossDoor(Vector2 from, Vector2 to, SomeKindaDoor door)
    {
        if (!IsClear(from) || !IsClear(to))
            return false;
        var manual = door.TryCast<ManualDoor>();
        var plain = door.TryCast<PlainDoor>();
        var collider = manual ? manual.myCollider : plain ? plain.myCollider : null;
        var shadow = manual ? manual.shadowCollider : plain ? plain.shadowCollider : null;
        var delta = to - from;
        var count = Physics2D.CircleCast(from, options.Radius, delta.normalized, filter, crossingHits, delta.magnitude);
        if (count == crossingHits.Length)
            return false;
        for (var i = 0; i < count; i++)
        {
            if (crossingHits[i].collider != collider && crossingHits[i].collider != shadow)
                return false;
        }
        return true;
    }

    public bool CanTraverse(int index)
    {
        var source = linkSources[index];
        if (!source || !source.Cast<Behaviour>().isActiveAndEnabled)
            return false;
        var link = links[index];
        return link.Type is PathTraversal.Decontamination or PathTraversal.Door ? CanCrossDoor(link.Start, link.End, source.Cast<SomeKindaDoor>()) : IsClear(link.Start) && IsClear(link.End);
    }

    public void BuildPath()
    {
        if (!grid.SmoothPath(search.Path, CanTraverse, out var nodes))
        {
            Finish(PathStatus.Obstructed);
            return;
        }

        var points = new List<Vector2>();
        var crossings = new List<PathCrossing>();
        for (var i = 0; i < nodes.Count; i++)
        {
            points.Add(grid.Position(nodes[i]));
            if (i == 0)
                continue;
            var link = grid.LinkIndex(nodes[i - 1], nodes[i]);
            if (link >= 0)
                crossings.Add(new PathCrossing(i - 1, links[link].Type, linkSources[link]));
        }

        var length = 0f;
        for (var i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);
        Result = new MapPath(PathStatus.Found, points.ToArray(), length, ExpandedNodes) { Ship = ship, Crossings = crossings.ToArray(), AutoOpenDoors = options.AutoOpenDoors, Radius = options.Radius, Console = console };
    }

    public void Finish(PathStatus status)
    {
        Result = new MapPath(status, Array.Empty<Vector2>(), 0f, ExpandedNodes) { Ship = ship };
    }
}
