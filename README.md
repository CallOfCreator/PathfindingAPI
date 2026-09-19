<p align="center">
  <img src="github/logo.png" width="180" alt="Pathfinding API Logo">
</p>

# Pathfinding API

An Among Us mod that adds pathfinding support for other mod developers to use.

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
- [Credits](#credits)

# Features

- Uses A* pathfinding to find routes through the map while checking for collisions
- Supports Airship moving platforms, ladders, Fungle ziplines, doors, decontamination doors, and vents

Submerged is not fully supported yet. The API may be able to find normal walking routes, but it does not currently handle Submerged elevators or other custom map mechanics.

# Installation

1. Download `PathfindingAPI.dll` from the Releases tab.
2. Place it in `BepInEx/plugins` alongside `Reactor.dll`. Reactor is required.
3. Install a mod that uses the API. Pathfinding API does not create NPCs by itself

All players in the lobby need both Pathfinding API and Reactor installed.

# Usage

## Add a reference to your project

Reference `PathfindingAPI.dll` directly or add it as a project reference.

There is currently no NuGet package.

Then add the following dependency to your main plugin class:

```csharp
[BepInDependency(PathfindingPlugin.Id)]
```

## Move a player

```csharp
using PathfindingAPI.Core;
using PathfindingAPI.Movement;
using PathfindingAPI.Navigation;
using PathfindingAPI.Options;
using Reactor.Utilities;
using UnityEngine;
```

```csharp
Coroutines.Start(PlayerControl.LocalPlayer.CoMoveTo(
    new Vector2(x, y),
    result =>
    {
        if (result.Status == PathMovementStatus.Arrived)
            DoSmtArrived();
        else
            DoSmt(result);
    },
    options: new PathOptions { AutoOpenDoors = true }));
```

Replace `DoSmtArrived` and `DoSmt` with your own handlers.

The callback runs when movement either finishes or fails. The result includes the movement status, planned path, and the waypoint where movement stopped.

Call this on the client that owns the player. If you want to move another player's character, send the request to that player's client first Movement itself will synchronize normally.

`PathOptions` controls which types of routes the pathfinder is allowed to use:

| Option | What it does |
| --- | --- |
| `UseLadders` | Allows routes that use ladders. |
| `UseZiplines` | Allows routes that use Fungle ziplines. |
| `UseDecontamination` | Allows routes through decontamination doors and waits for their normal cycle. |
| `UseMovingPlatforms` | Allows routes that use the Airship moving platform. |
| `WaitForDoors` | Allows routes through closed doors and waits for them to open. When disabled, closed doors are avoided unless `AutoOpenDoors` is enabled. |
| `UseVents` | Allows routes through connected vents when the player's role is allowed to vent. |
| `AutoOpenDoors` | Requests that a closed door opens when the player reaches it. |

Ladders, ziplines, decontamination, and moving platforms are enabled by default

`WaitForDoors`, `AutoOpenDoors`, and `UseVents` are disabled in a new `PathOptions` instance.

When no options are provided for a player-based search, vent usage follows that player's normal vent permissions.

The API can also find routes for NPCs, but it does not create or manage custom NPCs for you, vent traversal is only available if the player's current role allows them to vent.

For grid spacing, wall costs, and other pathfinding settings, see [PathOptions.cs](PathfindingAPI/Options/PathOptions.cs).

Movement tolerance is in [PathMovementSettings.cs](PathfindingAPI/Options/PathMovementSettings.cs). Pass these settings to `CoMoveTo` or `FollowPath` when you need to change them.

## Find a route without moving

Create a request using two `Vector2` positions. `start` is the starting position and `goal` is the destination.

```csharp
var request = MapPathfinding.CreateRequest(start, goal);
```

Call `request.Step()` once per frame until `request.Status` is no longer `PathStatus.Searching`.

After that, read `request.Result`. Only use the returned points if the result status is `PathStatus.Found`.

To cancel an unfinished search, call:

```csharp
request.Cancel();
```

Do not set `PathStatus.Cancelled` manually.

You can also yield `request.Run()` from a coroutine to let the search run over multiple frames

If you prefer using a callback:

```csharp
Coroutines.Start(MapPathfinding.FindPath(start, goal, path =>
{
    if (path.Status == PathStatus.Found)
        ShowRoute(path.Points);
}));
```

Replace `ShowRoute` with your own handler

## Follow a route you already planned

```csharp
Coroutines.Start(player.FollowPath(
    path,
    result => OnMovementEnded(result)));
```

The player should start near the route's first waypoint

`FollowPath` follows the route you provide, while `CoMoveTo` first calculates a route from the player's current position.

Movement stops if the player becomes blocked or cannot use one of the required crossings.

Your callback can start another pathfinding request if needed. For example, you could retry with `UseMovingPlatforms = false` if the Airship moving platform cannot be used

## Results

| Movement status | Meaning |
| --- | --- |
| `Arrived` | The player reached the final waypoint. |
| `NoPath` | Pathfinding failed. Check the path's search status for more information. |
| `Cancelled` | The supplied pathfinding request was cancelled. |
| `Interrupted` | Movement was interrupted because the player died or disconnected, a meeting started, or the game ended. |
| `NotOwner` | This client does not own the player |
| `Busy` | The player cannot move or already has an active path follower. |
| `Blocked` | The player stopped moving or failed to make progress. |
| `TraversalFailed` | A transport, crossing, or doorway could not be used. |
| `InvalidPath` | The supplied path contains invalid waypoint or crossing data. |

# Credits

- [Nebula](https://github.com/Dolly1016/Nebula-Public) - Inspiration for the pathfinding API
- [WanderingPix](https://github.com/WanderingPix) - Project icon
- [Reactor](https://github.com/NuclearPowered/Reactor) - Main dependency used by the mod

# Disclaimer

This mod is not affiliated with Among Us or Innersloth LLC, and the content contained within it is not endorsed or sponsored by Innersloth LLC. Portions of the materials contained herein are the property of Innersloth LLC.