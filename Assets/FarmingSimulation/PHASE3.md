# Phase 3 — third-person farmer

## Setup and controls

Reload the saved FarmScene if it was already open during the external edits. Allow Unity to import and compile before entering Play Mode.

The same Player prefab and CharacterController are used. Farmer Visual contains 15 cube mesh parts, including head, torso, arms, hands, trousers, boots, eyes and a hat. These visual parts have no colliders or Rigidbody. Only the visual child turns toward movement; the player capsule is not rotated by the orbit camera.

- WASD: camera-relative movement, with normalized diagonal speed.
- Mouse: smooth orbit, independent of the farmer's facing direction.
- E: immediate planting/watering, as before.
- Escape: release cursor; click inside Game view to resume.

On Main Camera's ThirdPersonCamera component, the defaults are distance 4, height 1.45, shoulder offset 0.75, sensitivity 0.12, pitch limits -15 to 65 degrees, and initial pitch 20 degrees. Follow and orbit smoothing are configurable. The camera casts a sphere from the actual player pivot toward its desired position, ignoring the player's own collider. It pulls in immediately at obstacles and eases back out. When forced extremely close, only the farmer's renderers are temporarily hidden so the camera does not show the inside of the head.

Camera collision relies on solid scene colliders. Phase 1's building blockers and boundary walls are preserved; decorative props without colliders still do not obstruct the camera. Inspect camera behavior around corners manually.

## Farming interaction

The camera-center ray selects the nearest solid surface, ignoring only the player's own hierarchy and triggers. The camera's distance behind the farmer does not increase interaction reach: the selected point must be within 3 metres of the player root. A second ray from chest height to the selected point must first hit the same plot. This prevents interaction through a wall or crate that blocks the farmer even when the offset camera can see over it.

ThirdPersonCamera reads input before movement and follows in LateUpdate. PlayerInteractor runs later, so its ray uses the camera's final position for the frame. FarmPlot, FarmHUD, seedlings, soil materials, plot events, state transitions, and duplicate prevention are unchanged.

## Manual Play Mode tests

1. Open FarmScene, clear the Console, enter Play Mode. Confirm no red errors, one camera view, and the farmer visible from behind and slightly to the left of the crosshair. Check the blue shirt, brown trousers, boots, head, arms, legs and hat.
2. Move forward, backward, sideways, and diagonally. Orbit the mouse about 90 degrees and press W again: movement should follow the camera direction. The visual farmer should turn smoothly toward movement. Standing still and orbiting should not turn the farmer.
3. Move the mouse vertically to both limits. Confirm the orbit stops at the configured pitch limits. Run around the courtyard and inspect follow smoothing. Adjust sensitivity and smoothing in the Inspector if needed.
4. Back toward a building or boundary so it lies between the farmer and camera. Orbit around its corner. The camera should pull forward without entering the solid obstacle and ease back when clear. Repeat near the ground with low pitch. Very close camera positions may hide the farmer briefly; moving away must restore it.
5. Confirm ground contact and building collisions remain intact. Press Escape: cursor releases and movement, orbit input, and farming stop. Click the Game view to resume without a large mouse jump. Test switching applications and returning as well.
6. Walk toward the three plots and aim down at visible soil beside the farmer. The first prompt must be Press E to Plant / Empty. Tap E: one seedling appears and the HUD becomes Press E to Water / Planted. Release and tap E again: soil darkens and the HUD becomes Plot Watered / Watered. Extra taps must do nothing. Repeat on all three plots, leaving them in different states temporarily to check independence.
7. Walk backward while keeping a plot under the crosshair. The prompt must disappear when the selected soil point is more than 3 metres from the farmer, regardless of camera distance. Look away and verify the prompt also disappears. Hold E rather than repeatedly tapping: it must not advance multiple states.
8. Test player-side obstruction in Play Mode only: move the existing crate between the farmer and a nearby plot using the Inspector, with the crate resting on the ground and outside the player's capsule. Aim over it from the camera so it blocks the chest-to-soil line. No prompt or interaction should be available until the farmer moves around it. If the camera itself aims at the crate, it should also block interaction. Stop Play Mode to discard temporary crate edits; do not save them into the scene.
9. Confirm the crate still falls and logs its first landing once on each fresh Play Mode run. Stop and restart twice; check that plots reset, input re-enables, the cursor releases on exit, and the farmer becomes visible again.

## Files

Modified:
- Assets/FarmScene.unity: player instance display name; existing scenery and gameplay objects retained.
- Assets/FarmingSimulation/Prefabs/Player.prefab: farmer visual, third-person camera component, updated controller references, serialized orbit defaults.
- Assets/FarmingSimulation/Scripts/Player/FarmPlayerController.cs: camera-relative movement and visual facing; existing gravity, CharacterController settings and cursor controls retained.
- Assets/FarmingSimulation/Scripts/Player/PlayerInteractor.cs: camera aim plus player reach/obstruction checks, execution after the camera.

Created under Assets/FarmingSimulation, each with a matching .meta file:
- Scripts/Player/ThirdPersonCamera.cs
- Scripts/Player/PlayerPhysicsQueries.cs
- Materials/Farmer_Shirt.mat
- Materials/Farmer_Pants.mat
- Materials/Farmer_Boots.mat
- Materials/Farmer_Skin.mat
- Materials/Farmer_Hat.mat
- PHASE3.md

## Validation performed

All runtime scripts compiled successfully with the installed Unity 6000.6.7f1 compiler and existing project assembly references. Serialized local/external references and hierarchy links were checked, including the scene HUD's reference into the Player prefab. The player still has exactly one camera, AudioListener and CharacterController with its original dimensions, and no extra physical components on the farmer. Basic numeric checks covered cardinal camera-relative movement directions and shoulder aiming clearance.

Hash comparisons confirmed the imported MarpaStudio assets, farming state/HUD/physics scripts, non-player prefabs, existing materials, packages and build settings were unchanged. FarmScene remains the startup scene. No packages were added.

Unity Play Mode and a player build were not run for Phase 3. Compilation and serialized checks do not establish runtime camera feel, collision correctness, or successful interactions; use the tests above. No harvesting, growth, animals, inventory, or VR were added. Stop here pending user confirmation.
