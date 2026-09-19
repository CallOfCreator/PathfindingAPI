namespace PathfindingAPI.Options;

public class PathOptions
{
    public float CellSize = 0.35f;
    public float Radius = 0.18f;
    public float ConnectionRange = 1f;
    public int NodeLimit = 30000;
    public int BatchSize = 96;
    public bool UseLadders = true;
    public bool UseZiplines = true;
    public bool UseDecontamination = true;
    public bool UseMovingPlatforms = true;
    public bool WaitForDoors = false;
    public bool UseVents = false;
    public float WallWeight = 1.5f;
    public float WallDistance = 0.6f;
    public bool AutoOpenDoors = false;
}