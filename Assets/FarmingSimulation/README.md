# FarmingSimulation — Phase 1

Open `Assets/FarmScene.unity`. If it was already open while files changed externally, reload the saved scene before testing; do not overwrite it with an older in-memory copy.

## Controls

- WASD: walk at 4 metres per second.
- Mouse: look around. Vertical look is limited to 80 degrees.
- Escape: release the cursor and stop keyboard movement.
- Left-click inside the Game view: capture the cursor again.
- Losing application focus releases the cursor; click the Game view after returning.

There is no jumping, planting, watering, animal logic, gameplay UI, or VR in this phase.

## Scene organization

- Environment: all 23 original farm model instances under project-owned area parents, a 72 by 64 metre ground, a central earth courtyard, and four visible solid boundary walls.
- Lighting: the original directional light, aimed downward.
- Player: an instance of our Player prefab, starting at (0, 0.1, -22), looking along positive Z.
- Gameplay and UI: empty organizational groups reserved for future phases.

The barn and house sit on opposite sides of the northern courtyard. Utility buildings and props sit around its edges. Eight area parents have simple solid BoxColliders. These are exterior blockers, not navigable building interiors; inspect their fit against the imported meshes in Unity before presenting. Small decorative props do not all have colliders.

## Project-owned files

- Scripts/Player/FarmPlayerController.cs: CharacterController movement, gravity, New Input System actions, and cursor handling.
- Prefabs/Player.prefab: controller, script, camera, and AudioListener. Change speed and mouse sensitivity on the controller component in the Inspector.
- Materials/Ground_Grass.mat, Courtyard_Earth.mat, Boundary_Wood.mat: simple URP/Lit materials used by the new environment geometry.

The input actions are created and enabled by the controller, then disabled/disposed with the component. The existing template input asset is unchanged. There are no new package dependencies. Imported prefabs, models, materials, metadata, and DemoScene are unchanged.

FarmScene is the first enabled scene in the shared build scene list. SampleScene is retained but disabled.

## Validation performed

- Compiled the controller with Unity 6000.6.7f1's Roslyn compiler and the project's existing assembly references: success.
- Checked serialized local IDs, external asset GUIDs, hierarchy parent/child links, retained prefab references, one camera/player, and spawn clearance against building blockers.
- Compared SHA-256 hashes for every MarpaStudio file and metadata file: unchanged.
- Unity Play Mode and a Windows build were not run as part of this implementation.

## Manual Unity checklist

1. Allow Unity to import the changes. Open FarmScene and confirm the Console has no errors or missing-script messages.
2. Confirm the five root groups, visible ground, correctly lit models, and no unexpected pink materials. The imported asset still includes five legacy-shader materials; inspect any affected meshes without editing the originals.
3. Enter Play Mode. Confirm one camera view, eye height around 1.65 metres, and that gravity settles the player onto the courtyard.
4. Walk in every direction and diagonally. Confirm normal speed, mouse yaw, and clamped pitch.
5. Walk into the selected buildings and the four boundary walls. Check that collisions stop movement at reasonable positions. Inspect area BoxCollider gizmos if any invisible gap or visible penetration occurs.
6. Press Escape, move the mouse, and press WASD: movement/look should stop while unlocked. Click inside the Game view to resume. Also test switching to another application and back.
7. Stop and restart Play Mode twice to check cursor cleanup and input re-enabling.
8. In File > Build Profiles, confirm the active profile uses the shared scene list, or includes FarmScene first if it has its own override. A Windows build still needs a manual launch test.

Confirm the player moves correctly before starting Phase 2.
