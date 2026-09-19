using Reactor.Networking.Attributes;
using UnityEngine;

namespace PathfindingAPI.Networking;

public static class PathfindingRpc
{
    [MethodRpc((uint)CustomRPC.OpenDoor)]
    public static void RpcOpenDoor(PlayerControl source, int doorId)
    {
        if (!AmongUsClient.Instance.AmHost || !ShipStatus.Instance || MeetingHud.Instance || source.Data.IsDead || source.Data.Disconnected) return;
        var ship = ShipStatus.Instance;
        if (!ship.Systems.TryGetValue(SystemTypes.Doors, out var system)) return;
        for (var i = 0; i < ship.AllDoors.Length; i++)
        {
            var door = ship.AllDoors[i];
            if (!door || door.Id != doorId || door.IsOpen || !door.isActiveAndEnabled) continue;
            var manual = door.TryCast<ManualDoor>();
            var plain = door.TryCast<PlainDoor>();
            var collider = manual ? manual.myCollider : plain ? plain.myCollider : null;
            if (!collider || Vector2.Distance(collider.ClosestPoint(source.GetTruePosition()), source.GetTruePosition()) > 1.5f)
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
}