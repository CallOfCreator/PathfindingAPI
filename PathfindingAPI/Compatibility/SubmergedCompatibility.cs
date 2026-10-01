using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using PathfindingAPI.Core;
using PathfindingAPI.Navigation;
using UnityEngine;

namespace PathfindingAPI.Compatibility;

public class SubmergedCompatibility
{
    public const string PluginId = "Submerged";
    public const ShipStatus.MapType MapType = (ShipStatus.MapType)6;
    public static Assembly Assembly;
    public static Type StatusType;
    public static FieldInfo StatusInstance;
    public static SubmergedCompatibility Current;
    public readonly ShipStatus Ship;
    public readonly object Status;
    public readonly List<SubmergedElevator> Elevators = new();
    public readonly HashSet<int> ElevatorDoors = new();
    public readonly float FloorCutoff;
    public readonly int EngineVentId;
    public readonly int LowerCentralVentId;
    public readonly PropertyInfo VentTransition;
    public readonly IList Sources;
    public readonly int SourceCount;
    public bool Supported;
    public bool Initializing;

    public SubmergedCompatibility(ShipStatus ship, object status)
    {
        Ship = ship;
        Status = status;
        var assembly = status.GetType().Assembly;
        var floorCutoff = assembly.GetType("Submerged.Floors.FloorHandler")?.GetField("FLOOR_CUTOFF");
        var ventType = assembly.GetType("Submerged.Vents.VentPatchData");
        var engineVent = ventType?.GetField("ENGINE_ROOM_VENT_ID");
        var centralVent = ventType?.GetField("LOWER_CENTRAL_VENT_ID");
        VentTransition = ventType?.GetProperty("InTransition");
        var consoleType = assembly.GetType("Submerged.Systems.Elevator.ElevatorConsole");
        var elevators = status.GetType().GetField("elevators");
        if (floorCutoff?.FieldType != typeof(float) || engineVent?.FieldType != typeof(int) || centralVent?.FieldType != typeof(int) ||
            VentTransition?.PropertyType != typeof(bool) || consoleType == null || elevators == null)
            return;

        FloorCutoff = (float)floorCutoff.GetValue(null);
        EngineVentId = (int)engineVent.GetValue(null);
        LowerCentralVentId = (int)centralVent.GetValue(null);
        Sources = elevators.GetValue(status) as IList;
        SourceCount = Sources?.Count ?? 0;
        if (SourceCount == 0)
        {
            Initializing = true;
            return;
        }

        var consoles = new List<object>();
        foreach (var component in ship.GetComponentsInChildren(Il2CppType.From(consoleType)))
        {
            if (!component) continue;
            var console = ClassInjectorBase.GetMonoObjectFromIl2CppPointer(component.Pointer);
            if (!consoleType.IsInstanceOfType(console))
            {
                Initializing = true;
                return;
            }
            consoles.Add(console);
        }

        foreach (var source in Sources)
        {
            if (Elevators.Exists(item => item.Source == (Component)source)) continue;
            var elevator = new SubmergedElevator(this, source, consoleType, consoles);
            if (!elevator.Initialized)
            {
                Initializing = !elevator.Incompatible;
                Elevators.Clear();
                ElevatorDoors.Clear();
                return;
            }
            Elevators.Add(elevator);
            ElevatorDoors.Add(elevator.LowerOuterDoor.GetInstanceID());
            ElevatorDoors.Add(elevator.LowerInnerDoor.GetInstanceID());
            ElevatorDoors.Add(elevator.UpperOuterDoor.GetInstanceID());
            ElevatorDoors.Add(elevator.UpperInnerDoor.GetInstanceID());
        }
        Supported = true;
    }

    public static bool IsLoaded() => IsLoaded(out _);

    public static bool IsLoaded(out Assembly assembly)
    {
        if (Assembly == null && IL2CPPChainloader.Instance.Plugins.TryGetValue(PluginId, out var plugin) && plugin.Instance != null)
        {
            Assembly = plugin.Instance.GetType().Assembly;
            StatusType = Assembly.GetType("Submerged.Map.SubmarineStatus");
            StatusInstance = StatusType?.GetField("instance");
        }
        assembly = Assembly;
        return assembly != null;
    }

    public static SubmergedCompatibility Get(ShipStatus ship)
    {
        if (!ship || ship.Type != MapType || !IsLoaded())
        {
            Current = null;
            return null;
        }
        var status = StatusInstance?.GetValue(null);
        if (status is not Component component || !component || component.gameObject != ship.gameObject)
        {
            Current = null;
            return null;
        }
        if (Current != null && Current.Ship == ship && ReferenceEquals(Current.Status, status) && !Current.Initializing &&
            (!Current.Supported || Current.SourceCount == Current.Sources.Count)) return Current;
        Current = new SubmergedCompatibility(ship, status);
        return Current;
    }

    public bool IsUpper(Vector2 point) => point.y > FloorCutoff;
    public bool InVentTransition() => (bool)VentTransition.GetValue(null);
    public bool IsElevatorDoor(SomeKindaDoor door) => door && ElevatorDoors.Contains(door.GetInstanceID());

    public bool IsVentBlocked(Vent vent, VentilationSystem ventilation)
    {
        if (vent.Id != EngineVentId) return ventilation.IsVentCurrentlyBeingCleaned(vent.Id);
        foreach (var entry in ventilation.PlayersCleaningVents)
            if (entry.Value == vent.Id) return true;
        foreach (var entry in ventilation.PlayersInsideVents)
        {
            if (entry.Value != vent.Id) continue;
            var player = GameData.Instance.GetPlayerById(entry.Key);
            if (player && !player.IsDead && !player.Disconnected) return true;
        }
        return false;
    }

    public void AddVentLink(MapPathRequest request, Vent vent, Vent next)
    {
        var from = (Vector2)(vent.transform.position + vent.Offset);
        var to = (Vector2)(next.transform.position + next.Offset);
        if (!WalkingGrid.TryApproach(from, from - to, vent.UsableDistance, request.IsClear,
                (point, anchor) => vent.Id == LowerCentralVentId || request.CanReachInteraction(point, anchor), out var entry) ||
            !WalkingGrid.TryApproach(to, to - from, next.UsableDistance, request.IsClear,
                (point, anchor) => next.Id == LowerCentralVentId || request.CanReachInteraction(point, anchor), out var exit)) return;
        request.AddLink(entry, exit, PathTraversal.Vent, vent);
    }

    public void AddLinks(MapPathRequest request)
    {
        if (!request.options.UseElevators) return;
        foreach (var elevator in Elevators)
            elevator.AddLinks(request);
    }
}
