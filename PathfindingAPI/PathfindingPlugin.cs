using BepInEx;
using HarmonyLib;
using PathfindingAPI.Compatibility;
using BepInEx.Unity.IL2CPP;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using Reactor.Utilities;

namespace PathfindingAPI;

[BepInPlugin(Id, "Pathfinding API", Version)]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(SubmergedCompatibility.PluginId, BepInDependency.DependencyFlags.SoftDependency)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
[BepInProcess("Among Us.exe")]
public class PathfindingPlugin : BasePlugin
{
    public const string Id = "com.callofcreator.pathfinding";
    public const string Version = "1.0.2";

    public override void Load()
    {
        Harmony.CreateAndPatchAll(typeof(SubmergedShipPatch), Id);
        ReactorCredits.Register("Pathfinding API", Version, false, ReactorCredits.AlwaysShow);
        Log.LogInfo($"Loaded Pathfinding API v{Version}");
    }
}