using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using PathfindingAPI.Core;
using PathfindingAPI.Movement;
using PathfindingAPI.Navigation;
using PathfindingAPI.Options;
using UnityEngine;

namespace PathfindingAPI.Compatibility;

public class SubmergedElevator
{
    public readonly SubmergedCompatibility Map;
    public readonly Component Source;
    public readonly object System;
    public readonly FieldInfo MovingField;
    public readonly FieldInfo TargetFloorField;
    public readonly bool Initialized;
    public readonly bool Incompatible;
    public readonly SystemTypes SystemType;
    public readonly BoxCollider2D LowerCabin;
    public readonly BoxCollider2D UpperCabin;
    public readonly PlainDoor LowerOuterDoor;
    public readonly PlainDoor LowerInnerDoor;
    public readonly PlainDoor UpperOuterDoor;
    public readonly PlainDoor UpperInnerDoor;
    public Component LowerConsole;
    public Component UpperConsole;
    public Component LowerControl;
    public Component UpperControl;
    public float LowerControlRange;
    public float UpperControlRange;
    public float LowerRange;
    public float UpperRange;

    public SubmergedElevator(SubmergedCompatibility map, object elevator, Type consoleType, List<object> consoles)
    {
        Map = map;
        Source = (Component)elevator;
        if (!Source) return;
        var type = elevator.GetType();
        var systemField = type.GetField("system");
        var lowerCabin = type.GetField("lowerElevatorCollider")?.GetValue(elevator) as Il2CppReferenceField<BoxCollider2D>;
        var upperCabin = type.GetField("upperElevatorCollider")?.GetValue(elevator) as Il2CppReferenceField<BoxCollider2D>;
        var lowerOuter = type.GetField("lowerOuterDoor")?.GetValue(elevator) as Il2CppReferenceField<PlainDoor>;
        var lowerInner = type.GetField("lowerInnerDoor")?.GetValue(elevator) as Il2CppReferenceField<PlainDoor>;
        var upperOuter = type.GetField("upperOuterDoor")?.GetValue(elevator) as Il2CppReferenceField<PlainDoor>;
        var upperInner = type.GetField("upperInnerDoor")?.GetValue(elevator) as Il2CppReferenceField<PlainDoor>;
        var ownerField = consoleType.GetField("elevator");
        var ownerValue = ownerField?.FieldType.GetProperty("Value");
        var distance = consoleType.GetProperty("UsableDistance");

        System = systemField?.GetValue(elevator);
        if (System == null) return;
        var systemType = System.GetType();
        MovingField = systemType.GetField("moving");
        TargetFloorField = systemType.GetField("upperDeckIsTargetFloor");
        var systemId = systemType.GetField("systemTypes");
        if (MovingField?.FieldType != typeof(bool) || TargetFloorField?.FieldType != typeof(bool) || systemId?.FieldType != typeof(SystemTypes))
        {
            Incompatible = true;
            return;
        }

        SystemType = (SystemTypes)systemId.GetValue(System);
        LowerCabin = lowerCabin.Value;
        UpperCabin = upperCabin.Value;
        LowerOuterDoor = lowerOuter.Value;
        LowerInnerDoor = lowerInner.Value;
        UpperOuterDoor = upperOuter.Value;
        UpperInnerDoor = upperInner.Value;
        if (!LowerCabin || !UpperCabin || !LowerOuterDoor || !LowerInnerDoor || !UpperOuterDoor || !UpperInnerDoor) return;

        foreach (var console in consoles)
        {
            var reference = ownerField.GetValue(console);
            if (reference == null) continue;
            if ((Component)ownerValue.GetValue(reference) != Source) continue;
            var component = (Component)console;
            var upper = Map.IsUpper(component.transform.position);
            var range = (float)distance.GetValue(console);
            if (!float.IsFinite(range) || range <= 0f) continue;
            if ((upper ? UpperCabin : LowerCabin).OverlapPoint(component.transform.position))
            {
                if (upper)
                {
                    UpperControl = component;
                    UpperControlRange = range;
                }
                else
                {
                    LowerControl = component;
                    LowerControlRange = range;
                }

                continue;
            }

            if (upper)
            {
                UpperConsole = component;
                UpperRange = range;
            }
            else
            {
                LowerConsole = component;
                LowerRange = range;
            }
        }

        Initialized = LowerConsole && UpperConsole && LowerControl && UpperControl;
    }

    public bool Moving() => (bool)MovingField.GetValue(System);
    public bool TargetUpper() => (bool)TargetFloorField.GetValue(System);
    public bool Contains(PlayerControl player) => LowerCabin.OverlapPoint(player.transform.position) || UpperCabin.OverlapPoint(player.transform.position);

    public bool Ready(bool upper)
    {
        return Initialized && Source && LowerOuterDoor && LowerInnerDoor && UpperOuterDoor && UpperInnerDoor && !Moving() && TargetUpper() == upper && (upper ? UpperOuterDoor.IsOpen && UpperInnerDoor.IsOpen : LowerOuterDoor.IsOpen && LowerInnerDoor.IsOpen);
    }

    public void AddLinks(MapPathRequest request)
    {
        if (!Initialized || !Source || !LowerConsole || !UpperConsole) return;
        if (!WalkingGrid.TryApproach(LowerConsole.transform.position, LowerConsole.transform.position - LowerCabin.bounds.center, LowerRange, point => request.IsClear(point) && !LowerCabin.OverlapPoint(point), request.CanReachInteraction, out var lower) || !WalkingGrid.TryApproach(UpperConsole.transform.position, UpperConsole.transform.position - UpperCabin.bounds.center, UpperRange, point => request.IsClear(point) && !UpperCabin.OverlapPoint(point), request.CanReachInteraction, out var upper)) return;
        request.AddLink(lower, upper, PathTraversal.Elevator, Source);
        request.AddLink(upper, lower, PathTraversal.Elevator, Source);
        request.AddLink(LowerCabin.bounds.center, upper, PathTraversal.Elevator, Source);
        request.AddLink(UpperCabin.bounds.center, lower, PathTraversal.Elevator, Source);
    }

    public IEnumerator Traverse(PlayerControl player, Vector2 destination, Action<bool> completed, float timeout)
    {
        if (!float.IsFinite(timeout) || timeout <= 0f) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (!Initialized || !PlayerPathExtensions.CanContinue(player, Map.Ship))
        {
            completed?.Invoke(false);
            yield break;
        }

        var deadline = Time.time + timeout;
        var fromUpper = Map.IsUpper(player.GetTruePosition());
        var toUpper = Map.IsUpper(destination);
        if (fromUpper == toUpper || !Source || !LowerCabin || !UpperCabin)
        {
            completed?.Invoke(false);
            yield break;
        }

        var success = false;
        player.moveable = false;
        try
        {
            if (!Contains(player))
            {
                var console = fromUpper ? UpperConsole : LowerConsole;
                var range = fromUpper ? UpperRange : LowerRange;
                if (!console || Vector2.Distance(player.GetTruePosition(), console.transform.position) > range || PhysicsHelpers.AnythingBetween(player.GetTruePosition(), console.transform.position, Constants.ShipOnlyMask, false))
                    yield break;
                yield return WaitForFloor(player, fromUpper, deadline, false);
                if (!PlayerPathExtensions.CanContinue(player, Map.Ship) || Time.time >= deadline || !Ready(fromUpper)) yield break;
                var boarded = false;
                yield return WalkTo(player, (Vector2)(fromUpper ? UpperCabin : LowerCabin).bounds.center + player.Collider.offset, deadline, result => boarded = result);
                if (!Contains(player) || (!boarded && Map.IsUpper(player.GetTruePosition()) == fromUpper)) yield break;
            }

            if (Map.IsUpper(player.GetTruePosition()) == fromUpper)
            {
                var control = fromUpper ? UpperControl : LowerControl;
                var range = fromUpper ? UpperControlRange : LowerControlRange;
                var cabin = fromUpper ? UpperCabin : LowerCabin;
                var probe = new MapPathRequest(Map.Ship, player.GetTruePosition(), player.GetTruePosition(), new PathOptions { UseElevators = false }, player);
                var found = WalkingGrid.TryApproach(control.transform.position, cabin.bounds.center - control.transform.position, range, point => probe.IsClear(point) && cabin.OverlapPoint(point - player.Collider.offset), probe.CanReachInteraction, out var target);
                probe.Cancel();
                if (!found) yield break;
                var reached = false;
                yield return WalkTo(player, target, deadline, result => reached = result);
                if (!reached && Map.IsUpper(player.GetTruePosition()) == fromUpper) yield break;
            }

            yield return WaitForFloor(player, toUpper, deadline, true);
            if (!PlayerPathExtensions.CanContinue(player, Map.Ship) || Time.time >= deadline || !Ready(toUpper) || Map.IsUpper(player.GetTruePosition()) != toUpper) yield break;
            yield return WalkTo(player, destination, deadline, result => success = result);
        }
        finally
        {
            if (player)
            {
                player.MyPhysics.body.velocity = Vector2.zero;
                if (PlayerPathExtensions.CanContinue(player, Map.Ship)) player.moveable = true;
            }

            completed?.Invoke(success);
        }
    }

    public IEnumerator WaitForFloor(PlayerControl player, bool upper, float deadline, bool aboard)
    {
        var requested = false;
        while (Source && PlayerPathExtensions.CanContinue(player, Map.Ship) && Time.time < deadline)
        {
            if (aboard && !Contains(player)) yield break;
            if (Ready(upper) && (!aboard || Map.IsUpper(player.GetTruePosition()) == upper)) yield break;
            if (Moving()) requested = false;
            else if (!requested && TargetUpper() != upper)
            {
                var currentUpper = Map.IsUpper(player.GetTruePosition());
                var console = aboard ? (currentUpper ? UpperControl : LowerControl) : (currentUpper ? UpperConsole : LowerConsole);
                var moveable = player.moveable;
                bool allowed;
                try
                {
                    player.moveable = true;
                    console.Cast<IUsable>().CanUse(player.Data, out allowed, out _);
                }
                finally
                {
                    player.moveable = moveable;
                }

                if (!allowed) yield break;
                Map.Ship.RpcUpdateSystem(SystemType, (byte)2);
                requested = true;
            }

            yield return null;
        }
    }

    public IEnumerator WalkTo(PlayerControl player, Vector2 target, float deadline, Action<bool> completed)
    {
        var request = new MapPathRequest(Map.Ship, player.GetTruePosition(), target, new PathOptions
        {
            UseElevators = false,
            UseLadders = false,
            UseZiplines = false,
            UseMovingPlatforms = false,
            UseDecontamination = false,
            UseVents = false
        }, player);
        while (request.Status == PathStatus.Searching && PlayerPathExtensions.CanContinue(player, Map.Ship) && Time.time < deadline)
        {
            request.Step();
            if (request.Status == PathStatus.Searching) yield return null;
        }

        if (request.Status != PathStatus.Found)
        {
            request.Cancel();
            completed(false);
            yield break;
        }

        foreach (var point in request.Result.Points)
        {
            var walking = player.MyPhysics.WalkPlayerTo(point, 0.01f, player.MyPhysics.TrueSpeed / player.MyPhysics.Speed);
            while (Source && PlayerPathExtensions.CanContinue(player, Map.Ship) && Time.time < deadline && Map.IsUpper(player.GetTruePosition()) == Map.IsUpper(point))
            {
                if (Vector2.Distance(player.GetTruePosition(), point) <= 0.1f || !walking.MoveNext()) break;
                yield return null;
            }

            if (player) player.MyPhysics.body.velocity = Vector2.zero;
            if (!Source || !PlayerPathExtensions.CanContinue(player, Map.Ship) || Time.time >= deadline || Vector2.Distance(player.GetTruePosition(), point) > 0.1f)
            {
                completed(false);
                yield break;
            }
        }

        completed(true);
    }
}