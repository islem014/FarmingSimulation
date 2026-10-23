# Phase 2 — planting and watering

Open the saved `Assets/FarmScene.unity` and allow Unity to import/compile. If an older copy of this scene is already open, reload the saved scene before testing rather than saving the stale in-memory version over it.

## What to test in Unity

1. Clear the Console, enable ordinary Log messages, and enter Play Mode. Check that there are no red errors and that the small crosshair appears. The separate crate starts at (-5, 4, -21), to the left of the spawn point; within about a second the Console should show `Physics demo crate landed on Courtyard.` once. Expand Gameplay and select the crate to inspect its Rigidbody and BoxCollider. Watch it fall again by restarting Play Mode.
2. Walk forward from the spawn point toward the three brown plots at (-4, 0.02, -14), (0, 0.02, -14), and (4, 0.02, -14). Look down at the soil from close range. The raycast reaches 3 metres from the camera's viewport ray origin. The HUD should show `Press E to Plant` and the target's name with `State: Empty`.
3. Tap E once. Exactly one green seedling should appear at that plot's center; the HUD should immediately change to `Press E to Water` and `State: Planted`. No hold is required. Keep E held: it must not automatically water. Release E before the next step.
4. Tap E again. The soil should become darker, and the HUD should show `Plot Watered` and `State: Watered`. Press E repeatedly: the plot must keep exactly one seedling and remain Watered without errors. Expand the plot in the Hierarchy to inspect the dynamically created Seedling(Clone).
5. Visit another plot: it must still be Empty. Test independence by leaving one plot Empty, one Planted, and one Watered, then look between them. Each prompt and state must match its own plot. Finally repeat the full interaction on all three plots.
6. Look away or move beyond interaction range. The prompt and state panel must disappear while the crosshair remains. E must do nothing when there is no target. Aim directly at the seedling as well: its visuals have no colliders, so the soil can still be targeted beneath it.
7. Press Escape: the cursor releases and the HUD hides. E must not interact while the cursor is free. Click inside the Game view to resume. Verify the original WASD, mouse look, ground, and building collisions still work.
8. Stop and restart Play Mode. All plots should start Empty again, seedlings from the previous run should be gone, and the crate should log one landing again. No state is saved between runs.

The crate is seven metres south of the plot row, and the plots have 1.6-metre gaps. All are in the clear courtyard, away from the building blockers. Actual appearance, movement, event behavior, and collisions still require the Play Mode checks above.

## How the scripts communicate

PlayerInteractor runs after movement in LateUpdate, casts toward the crosshair, and calls FarmPlot.Interact on the closest visible plot when E is pressed. It emits TargetChanged when the player looks at a different plot or looks away.

FarmPlot owns its own Empty → Planted → Watered state, creates a seedling with Instantiate, and emits StateChanged after a successful change. FarmHUD subscribes to the current target's event and unsubscribes when the target changes or the HUD is disabled. FarmHUD only displays information; it does not plant or water. Its lightweight IMGUI overlay needs no Canvas, EventSystem, font download, or extra package.

The controller and default input asset are unchanged. PlayerInteractor owns a separate immediate E action with no Hold interaction. Dry and wet materials are assigned to each plot renderer; shared material assets are never edited at runtime.

PhysicsCrate uses OnCollisionEnter and checks for an upward supporting contact before logging its first landing. It is a separate physics demonstration, not a collectible or inventory item.

## File inventory

Modified:
- `Assets/FarmScene.unity`: children added to Gameplay and UI; existing scenery blocks preserved.
- `Assets/FarmingSimulation/Prefabs/Player.prefab`: added PlayerInteractor with its camera reference; the controller and its settings are unchanged.

Created under `Assets/FarmingSimulation/` (each listed asset has a matching new `.meta` file):
- `Scripts/Farming/FarmPlot.cs`
- `Scripts/Player/PlayerInteractor.cs`
- `Scripts/UI/FarmHUD.cs`
- `Scripts/Physics/PhysicsCrate.cs`
- `Prefabs/FarmPlot.prefab`
- `Prefabs/Seedling.prefab`
- `Prefabs/PhysicsCrate.prefab`
- `Materials/Soil_Dry.mat`
- `Materials/Soil_Watered.mat`
- `Materials/Seedling_Green.mat`
- `PHASE2.md`

New folder metadata: `Scripts/Farming.meta`, `Scripts/UI.meta`, and `Scripts/Physics.meta`.

## Validation completed outside Play Mode

- Compiled all runtime C# scripts using the installed Unity 6000.6.7f1 Roslyn compiler and the project's existing assembly references: success.
- Checked unique serialized IDs, local references, external GUIDs, project-owned prefab/material file IDs, hierarchy links, and the scene HUD's reference into the Player prefab.
- Checked three plot instances, their spacing, courtyard placement, spawn separation, collider-free seedling geometry, and crate Rigidbody settings.
- Hash comparisons confirmed all original MarpaStudio files, the Phase 1 controller, package manifests, and build settings were unchanged. Only the existing Gameplay and UI child lists changed among pre-existing scene blocks.
- FarmScene remains the first enabled scene in the shared build settings.
- Unity Play Mode and a player build were not run for Phase 2.

No harvesting, animals, inventory, growth timers, saving, or VR are implemented. Confirm the Phase 2 tests before proceeding.
