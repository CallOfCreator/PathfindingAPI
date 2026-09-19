using BepInEx;
using BepInEx.Unity.IL2CPP;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using Reactor.Utilities;

namespace PathfindingAPI;

[BepInPlugin(Id, "Pathfinding API", Version)]
[BepInDependency(ReactorPlugin.Id)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
[BepInProcess("Among Us.exe")]
public class PathfindingPlugin : BasePlugin
{
    public const string Id = "com.callofcreator.pathfinding";
    public const string Version = "1.0.0";

    public override void Load()
    {
        ReactorCredits.Register("Pathfinding API", Version, false, ReactorCredits.AlwaysShow);
        Log.LogInfo($"Loaded Pathfinding API v{Version}");
    }
}