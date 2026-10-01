using HarmonyLib;

namespace PathfindingAPI.Compatibility;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Awake))]
public static class SubmergedShipPatch
{
    public static bool Prepare() => SubmergedCompatibility.IsLoaded();

    [HarmonyPrefix]
    public static void Prefix(ShipStatus __instance)
    {
        // assign Skeld's social medium prefab to Submerged to prevent spawning outside the map
        if (__instance.Type != SubmergedCompatibility.MapType || __instance.socialMediumFeedSystemPrefab) return;
        var skeld = SubmergedCompatibility.Assembly.GetType("Submerged.Map.MapLoader")?.GetProperty("Skeld")?.GetValue(null) as ShipStatus;
        if (skeld && skeld.socialMediumFeedSystemPrefab)
            __instance.socialMediumFeedSystemPrefab = skeld.socialMediumFeedSystemPrefab;
    }
}