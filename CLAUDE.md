# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Roboter is a small Godot 4.7 (C#/.NET 8) 3D RTS-style game. It is also used to teach a 10-year-old how to make games, so gameplay scripts (especially in `Godot/Entities/`) must stay simple and readable for a beginner: plain `_Process`/`_PhysicsProcess` logic, `[Export]` fields for values worth tweaking in the Inspector, no clever abstractions.

Repository layout:
- `Godot/`: the Godot project (`project.godot`, `Roboter.csproj`). All code lives here.
- `MODELLE/`: source model files (`.io`, `.glb`, `.model`) before they are imported into `Godot/Assets/Models/`.
- `notes.md`: the game design (units, buildings, costs, rules) in German. Code is always English (`Blech` = `Metal`, `Schrott` = `Scrap`); only player-visible text is German.
- `RoboterVault/`: Obsidian vault with the kanban backlog (`Backlog.md`, in German).

## Commands

Godot editor binary: `C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe` (use the `_console.exe` next to it to see output in a terminal).

```powershell
# Build the C# assembly
dotnet build C:\dev\Roboter\Godot\Roboter.csproj

# Run a single scene headless as a smoke test (prints script/resource errors, quits after N frames)
& 'C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path C:\dev\Roboter\Godot res://Testing/GameplayTest.tscn --quit-after 600

# Test multiplayer with two windows: one hosts, one joins
& 'C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --path C:\dev\Roboter\Godot -- --host
& 'C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --path C:\dev\Roboter\Godot -- --join=127.0.0.1

# Rebake a map's navigation mesh after changing terrain, bridges or obstacles (takes a few seconds)
& 'C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path C:\dev\Roboter\Godot --script res://Systems/HelperScripts/bake-navmesh.gd -- res://Maps/1v1.tscn res://Maps/MapData/1v1/navmesh.res
```

There is no test suite or linter. Verification is: build, headless smoke run of the affected scene, then the user watches it in the editor.

The user usually has the Godot editor open. After hand-editing `.tscn` files, tell them to reload the scene and not save the stale editor copy over the change.

## Architecture

- **Entry flow:** `UI/MainMenu.tscn` (main scene) lets a player Host or Join. `Systems/NetworkManager.cs` is an autoload singleton (`NetworkManager.Instance`) that owns the ENet peer (port 7777, 1v1, max one client). Host is team 0, client is team 1. When the client connects, the host RPCs `StartMatch` with the peer-to-team map and both sides load `Maps/1v1.tscn` in the background behind `UI/LoadingScreen.tscn` (owned by NetworkManager), which stays up until `Match` reports the match really started. Disconnects and failures return to the main menu with a status message (`StatusChanged` signal / `LastStatus`).
- **Running a map directly** (F6 without going through the menu) works: unknown peers, including the offline peer, fall back to the host team.
- **Match:** every playable map instances `Systems/Match.tscn` and points its `StartPositions` export at a node of `Marker3D`s. `Match.cs` (`Match.Instance`) waits until all peers loaded the map (`NetworkManager.AllPeersLoaded`) and the navigation map is synced, then spawns 12 workers + a bunker per team at shuffled start markers, tracks per-team `TeamResources` (Metal, Gas), and ends the match when a team has no units and no finished bunker. `Match.tscn` also holds the HUD, chat, camera rig and `PlayerCommands`. The 1v1 ground is a `Terrain3D` node (the `addons/terrain_3d` GDExtension) with `collision_mode` full so mouse picking works everywhere.
- **Networking model:** the host simulates everything (`if (!Multiplayer.IsServer()) return;` in `_PhysicsProcess`). Units and buildings are created only on the host via `Match.CreateUnit/CreateBuilding` + `AddEntity` under `Match/Entities`, replicated by the `EntitySpawner` (`MultiplayerSpawner`; new spawnable scenes must be added to its list) and each scene's `MultiplayerSynchronizer`. Scene-placed neutral objects (scrap piles, gas volcanoes) are updated with host RPCs instead. Player actions go through `Systems/PlayerCommands.cs` (`RpcId(1, Request...)`), where the host checks the sender's team owns the units and can pay. Cosmetic effects are `Rpc(..., CallLocal)`.
- **Navigation:** units move on a baked `NavigationMesh` (`NavigationRegion3D` in each map, baked by `Systems/HelperScripts/bake-navmesh.gd` from Terrain3D + nodes in the `navigation_source` group, cell size 0.5 to match `navigation/3d/default_cell_size`, max slope 50° and max climb 1 m: hills are walkable, the steep mountain walls keep the arena closed; at 55° units can climb out to the east). Terrain below -1 m (river beds) is left out, so rivers are crossed only on bridges; `Entities/Neutral/bridge.tscn` has an invisible 2 m wide `WalkableDeck` because the real deck is too narrow for the navmesh. The helper warns about scrap piles, gas volcanoes and start markers that end up off the navmesh. Units follow their path themselves (`Unit.WalkTo`): they move along the navmesh incl. its height, reach waypoints by flat distance, skip a waypoint after 0.5 s without progress, and their model stands on the real ground below (`Match.GroundUnder`). The navmesh floats up to 1 m above the ground, so never compare unit positions with navmesh points in 3D. `NavigationAgent3D` is only used for avoidance (`NavigationServer3D.AgentSetVelocity`). Units have no physics collisions. Units count as arrived when they are close enough or cannot get closer (`Unit.HasArrivedAt`). Decorations (`Entities/Neutral/Obstacle.cs`) and gas volcanoes are in `navigation_source`, so they cut holes in the navmesh; decorations also block building placement.
- **Camera:** `Systems/RtsController.tscn` is a Node3D rig, `Elevation` child for pitch, `Camera3D` below it. In matches it has a fixed 45° pitch, no rotation, WASD + edge panning and zoom. Input actions (`camera_forward/back/left/right/sprint/rotate`, `zoom_in/out`) are defined in `project.godot`. Keyboard movement is suppressed while a `LineEdit` (chat) has focus.
- **Fog of war:** `Systems/FogOfWar.cs` runs on every peer for its own team (client-local, not network-secure). Every 0.2 s it builds a 2 m visibility grid from own units (`SightRange`) and buildings (`SightRange`), hides enemy units outside it, keeps enemy buildings visible once seen with their last seen state (`Building.ShownHealth`/`ShownBuildProgress`), and leaves a `BuildingGhost` for enemy buildings destroyed while unseen. The same grid darkens the minimap and, via `UI/fog-of-war.gdshader` on a screen-covering quad on render layer 20, the 3D view. The minimap rotates with the camera rig.
- **Input and HUD:** `Systems/PlayerInput.cs` (local only) does click/box selection, right-click commands and building placement. `UI/Hud.tscn` has the resource bar, command buttons (built from `Match.UnitTypes`/`BuildingTypes`), unit portrait (bottom left, from `Unit.Portrait`), minimap (bottom center), health bars and the game-over screen. In-game text is German.
- **Voice lines:** `UI/Voices.cs` (`Voices.Instance`, in `Hud.tscn`, local only) plays the announcer (`Assets/Sounds/no-*.ogg`: missing workers first, then no-metal / no-gas / no-resources when both are missing; ignored while one is still playing) and unit lines (`Unit.SelectedSound`/`CommandSound`, on selection and on commands). Command buttons the player can't afford stay clickable but dimmed, so the click can announce what's missing. When units waiting at a training building can't be paid for, the host announces it once via `PlayerCommands.AnnounceMissingResources`.
- **Chat:** `UI/Chat.tscn` is part of `Match.tscn`. All messages go through the host: clients `RpcId(1, SubmitMessage)`, the host trims/limits and broadcasts `ReceiveMessage`. Follow this host-authoritative pattern for new networked features.
- **Entities:** `Godot/Entities/` holds `.tscn` wrappers around the imported `.glb` models with scripts next to them: `Wolfsmen/Units/Unit.cs` (+ `Worker.cs`), `Wolfsmen/Buildings/Building.cs` (+ `Bunker.cs`, `GasMine.cs`), `Neutral/ScrapPile.cs`, `GasVolcano.cs`, `Obstacle.cs`, and `Effects/`. Gas mines dock onto a volcano's +Z side (`GasVolcano.MineDockPosition`); the mine assigns jobs: 1st worker inside, 2nd carries, 3rd inside, the rest carry (`GasMine.ChooseJobFor`). Stats are `[Export]`s on the scenes; costs and training rules are `UnitType`/`BuildingType` `.tres` files next to them. Maps should instance these wrappers, not the raw `.glb`. Models face **+Z** by default (Godot's `LookAt` faces -Z unless `useModelFront` is true).
- `Testing/` contains scenes for trying features. `GameplayTest.tscn` (small flat map, lots of starting resources) and `CombatTest.tscn` (armies next to each other) use the real `Match`; when run offline the player controls team 0 and team 1 just defends itself. Older throwaway scenes there (e.g. `TurretTest.tscn`) may have no camera; use the editor's Game tab camera override.

## Known noise

`instance_reset_physics_interpolation() is deprecated` warnings come from the Terrain3D 1.0.2 native library compiled against an older Godot API. Nothing to fix in this repo.
