using Hazel;
using PathfindingAPI.Compatibility;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using UnityEngine;

namespace PathfindingAPI.Networking;

[RegisterCustomRpc((uint)CustomRPC.OpenDoor)]
public sealed class OpenDoorRpc(PathfindingPlugin plugin, uint id) : PlayerCustomRpc<PathfindingPlugin, int>(plugin, id)
{
    public override RpcLocalHandling LocalHandling => RpcLocalHandling.Before;
    public override int Read(MessageReader reader) => reader.ReadInt32();
    public override void Write(MessageWriter writer, int data) => writer.Write(data);
    public override void Handle(PlayerControl innerNetObject, int data) => PathfindingRpc.OpenDoor(innerNetObject, data);
}

public static class PathfindingRpc
{
    public static void RpcOpenDoor(PlayerControl source, int doorId)
    {
        Rpc<OpenDoorRpc>.Instance.Send(source, doorId);
    }

    public static void OpenDoor(PlayerControl source, int doorId)
    {
        if (!AmongUsClient.Instance.AmHost || !ShipStatus.Instance || MeetingHud.Instance || source.Data.IsDead || source.Data.Disconnected) return;
        var ship = ShipStatus.Instance;
        if (!ship.Systems.TryGetValue(SystemTypes.Doors, out var system)) return;
        for (var i = 0; i < ship.AllDoors.Length; i++)
        {
            var door = ship.AllDoors[i];
            if (!door || door.Id != doorId || door.IsOpen || !door.isActiveAndEnabled) continue;
            if (SubmergedCompatibility.Get(ship)?.IsElevatorDoor(door) == true) return;
            var manual = door.TryCast<ManualDoor>();
            var plain = door.TryCast<PlainDoor>();
            var collider = manual ? manual.myCollider : plain ? plain.myCollider : null;
            if (!collider || DistanceToCollider(collider, source.GetTruePosition()) > 1.5f)
                return;
            var automatic = system.TryCast<AutoDoorsSystemType>();
            if (automatic != null)
            {
                door.SetDoorway(true);
                automatic.dirtyBits |= 1U << i;
            }
            else if (system.TryCast<DoorsSystemType>() != null && door.Id >= 0 && door.Id <= 31)
            {
                ship.UpdateSystem(SystemTypes.Doors, source, (byte)(door.Id | 64));
            }

            return;
        }
    }

    private static float DistanceToCollider(Collider2D collider, Vector2 point)
    {
        if (!collider) return float.MaxValue;
        var bounds = collider.bounds;
        var clampX = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
        var clampY = Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
        return Vector2.Distance(new Vector2(clampX, clampY), point);
    }
}