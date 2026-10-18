# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Roboter is a small Godot 4.7 (C#/.NET 8) 3D RTS-style game. It is also used to teach a 10-year-old how to make games, so gameplay scripts (especially in `Godot/ScriptedModels/`) must stay simple and readable for a beginner: plain `_Process`/`_PhysicsProcess` logic, `[Export]` fields for values worth tweaking in the Inspector, no clever abstractions.

Repository layout:
- `Godot/`: the Godot project (`project.godot`, `Roboter.csproj`). All code lives here.
- `MODELLE/`: source model files (`.io`, `.glb`, `.model`) before they are imported into `Godot/Models/`.
- `RoboterVault/`: Obsidian vault with the kanban backlog (`Backlog.md`, in German).

## Commands

Godot editor binary: `C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe` (use the `_console.exe` next to it to see output in a terminal).

```powershell
# Build the C# assembly
dotnet build C:\dev\Roboter\Godot\Roboter.csproj

# Run a single scene headless as a smoke test (prints script/resource errors, quits after N frames)
& 'C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path C:\dev\Roboter\Godot res://TestScenes/TurretTest.tscn --quit-after 600
```

There is no test suite or linter. Verification is: build, headless smoke run of the affected scene, then the user watches it in the editor.

The user usually has the Godot editor open. After hand-editing `.tscn` files, tell them to reload the scene and not save the stale editor copy over the change.

## Architecture

- **Entry flow:** `UI/MainMenu.tscn` (main scene) lets a player Host or Join. `Systems/NetworkManager.cs` is an autoload singleton (`NetworkManager.Instance`) that owns the ENet peer (port 7777, 1v1, max one client). Host is team 0, client is team 1. When the client connects, the host RPCs `StartMatch` with the peer-to-team map and both sides change to `Maps/1v1.tscn`. Disconnects and failures return to the main menu with a status message (`StatusChanged` signal / `LastStatus`).
- **Running a map directly** (F6 without going through the menu) works: unknown peers, including the offline peer, fall back to the host team.
- **Match scene:** `Maps/1v1.tscn` has `Systems/MatchSetup.cs` on its root, which moves the `RtsController` camera rig to `StartPositions/Team{LocalTeam}`. The ground is a `Terrain3D` node (the `addons/terrain_3d` GDExtension).
- **Camera:** `Systems/RtsController.tscn` is a Node3D rig, `Elevation` child for pitch, `Camera3D` below it. Input actions (`camera_forward/back/left/right/sprint/rotate`, `zoom_in/out`) are defined in `project.godot`. Keyboard movement is suppressed while a `LineEdit` (chat) has focus.
- **Chat:** `UI/Chat.tscn` is instanced into the map. All messages go through the host: clients `RpcId(1, SubmitMessage)`, the host trims/limits and broadcasts `ReceiveMessage`. Follow this host-authoritative pattern for new networked features.
- **Scripted models:** `Godot/ScriptedModels/` holds `.tscn` wrappers around imported `.glb` models that add a script and gameplay nodes (e.g. the bunker turret with a `TurretRange` Area3D, the `walking-dummy` CharacterBody3D). Scenes elsewhere should instance these wrappers, not the raw `.glb`, or the behavior is missing. Models face **+Z** by default (Godot's `LookAt` faces -Z unless `useModelFront` is true). Units that turrets should target are physics bodies in the group `Enemy`.
- `TestScenes/` contains throwaway scenes for trying single features (e.g. `TurretTest.tscn`). Some have no camera; use the editor's Game tab camera override.

## Known noise

`instance_reset_physics_interpolation() is deprecated` warnings come from the Terrain3D 1.0.2 native library compiled against an older Godot API. Nothing to fix in this repo.
