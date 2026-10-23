# Phase 4: Sheep system

## Recovery and completion

The interrupted work already contained complete animal statistics, enclosure bounds, sheep behaviour, sheep interaction, and a shared interaction base class. FarmPlot, PlayerInteractor, and FarmHUD had already been adapted to share interaction between plots and animals. Those implementations were reused. There was no Git repository; a file-hash baseline was recorded before continuing.

The missing pieces were the saved sheep/enclosure prefabs, exported animation clips/controller, URP materials, and scene instances. Earlier temporary export files were no longer available. This continuation created those pieces through Unity's editor APIs, corrected the obstacle-probe radius to match the sheep controller, and saved FarmScene. Setup completed successfully in the normal licensed editor after the headless editor could not obtain its required license entitlement.

## What is in the scene

- Environment/AnimalEnclosure: 8 by 8 metres, centred at (-11, 0, -21). Bounds X [-15, -7], Z [-25, -17].
- Gameplay/Animals/Sheep - Daisy: starts at (-11.5, 0.05, -21.5), with its enclosure assigned.
- East entrance at X -7, Z -21: two metres wide. The gate is held open inward; it is not an interactive gate.
- Sixteen fence sections, an open gate, and a decorative feeding trough reuse MarpaStudio meshes with project-owned wrapper colliders. Feed the sheep directly with E.
- Sheep has a CharacterController, AnimalStats, SheepBehaviour, SheepInteractable, and a child Generic rig/Animator. Its visual height is approximately 1.1 metres.
- The pen is west of the courtyard, clear of the three planting plots, player spawn, and crate demonstration. Movement targets stay one metre inside the pen boundary, including at the open entrance.

## Behaviour and controls

The existing camera-centre aiming ray and player-distance/line-of-sight checks select either a plot or sheep. The same immediate New Input System E action handles both. The HUD listens to target feedback events.

Hunger starts at 45 and increases by 0.8 per second: 0 means satisfied, 100 means very hungry. At 65 the sheep stops and waits in Hungry state. Otherwise it alternates Idle (3 seconds) and Wander (0.7 metres/second), rotating toward its destination. Obstacle probes reject blocked routes; CharacterController movement handles collisions and gravity.

Feeding requires hunger of at least 25. An accepted feed removes 35 hunger, clamped at zero, enters Eating for 4 seconds, triggers one Jump, and starts a 10-second cooldown. The HUD displays confirmation, cooldown, or lack of hunger as appropriate. Thirst (20) and health (100) are displayed but remain constant.

## Animation inspection and limitations

The original Sheep.fbx and importer were preserved. The Generic rig contains Idle and Jump; there are no Walk or Eat takes. Idle was available as an imported preview take and was cloned into a standalone looping clip. It lasts 6.25 seconds and contains 37 changing curves out of 250: movement can be subtle. Jump lasts 1.125 seconds, with 90 changing curves. No animation was fabricated.

The controller uses Idle, a Fed trigger leading to Jump, then a return to Idle. Root motion is disabled. Idle also serves as the visual placeholder during wandering/eating, so locomotion can look like gliding. A direct editor Animator evaluation passed Idle -> Fed -> Jump -> Idle, with stable root position/scale and valid animation binding paths. See Animals/Phase4AssetReport.txt for measured asset details.

This is a simple bounded wandering system, not a pathfinding system. A blocked sheep waits and tries another destination. The enclosure assumes an axis-aligned pen on level ground. A future manually placed Sheep prefab instance must have its SheepBehaviour enclosure field assigned; the existing scene instance is already wired.

## Files

All paths below are relative to the project root. New assets have matching .meta files; new folders also have .meta files.

Created in the interrupted implementation and retained:

- Assets/FarmingSimulation/Scripts/Animals/AnimalStats.cs
- Assets/FarmingSimulation/Scripts/Animals/AnimalEnclosure.cs
- Assets/FarmingSimulation/Scripts/Animals/SheepBehaviour.cs (obstacle clearance corrected during continuation)
- Assets/FarmingSimulation/Scripts/Animals/SheepInteractable.cs
- Assets/FarmingSimulation/Scripts/Interactions/FarmInteractable.cs
- Corresponding script and Animals/Interactions folder metadata.

Modified across Phase 4:

- Assets/FarmingSimulation/Scripts/Farming/FarmPlot.cs: shared interactable contract; original plot event and planting/watering retained.
- Assets/FarmingSimulation/Scripts/Player/PlayerInteractor.cs: shared target selection; existing input/range/occlusion behaviour retained.
- Assets/FarmingSimulation/Scripts/UI/FarmHUD.cs: shared feedback and two-line animal details.
- Assets/FarmScene.unity: enclosure and one sheep instance added.

Created during continuation:

- Assets/FarmingSimulation/Editor/Phase4Setup.cs
- Assets/FarmingSimulation/Animals/Animations/Sheep_Idle.anim
- Assets/FarmingSimulation/Animals/Animations/Sheep_Jump.anim
- Assets/FarmingSimulation/Animals/Animations/Sheep.controller
- Assets/FarmingSimulation/Animals/Materials/Sheep_White.mat
- Assets/FarmingSimulation/Animals/Materials/Sheep_Black.mat
- Assets/FarmingSimulation/Animals/Prefabs/Sheep.prefab
- Assets/FarmingSimulation/Animals/Prefabs/AnimalEnclosure.prefab
- Assets/FarmingSimulation/Animals/Phase4AssetReport.txt
- Assets/FarmingSimulation/PHASE4.md
- Matching metadata, plus Editor, Animations, Materials, and Prefabs folder metadata.

Phase4Setup is an editor-only creation utility. Its one-time request has been consumed; it does not rebuild on each launch. It refuses to overwrite existing sheep/enclosure prefabs. No setup action is required to play the saved scene.

The project-root .phase4-work directory contains local recovery hashes, a pre-continuation scene snapshot, compiler response files/outputs, and setup status. These are audit artifacts, not gameplay assets.

## Validation and preservation

Runtime and editor scripts compiled successfully. Unity created and saved the assets and scene; editor animation evaluation passed. Serialized GUID/local-reference checks cover FarmScene and the new prefabs, clips, controller, and materials. Hash comparison against the continuation baseline confirms no imported asset changes, no deleted baseline files, and no changes to player movement/camera scripts, existing player/plot/crate prefabs, packages, or build settings. FarmScene remains the first enabled startup scene.

Gameplay has not been tested by the assistant in Play Mode. Editor animation evaluation and compilation do not establish that every interaction works during gameplay. Complete the following manual checks before considering Phase 4 accepted.

## Unity Play Mode checklist

1. Open Assets/FarmScene.unity. Wait for compilation; check Console for red errors. Confirm one enclosure under Environment and one sheep under Gameplay/Animals.
2. Enter Play Mode. Check WASD, orbit, camera collision, Escape release, and click-to-recapture. Walk west from the courtyard to the open entrance around (-7, 0, -21). Confirm fence/trough collisions and comfortable entrance access.
3. Watch Daisy for at least 20 seconds without feeding: she should alternate waiting and moving, turn smoothly, and stay inside the pen without passing through the trough. At about 25 seconds from a fresh start, hunger reaches 65 and she should stop in Hungry state.
4. Move within 3 metres and aim at her body. Confirm name, species, hunger, state, thirst, health, and Press E to Feed Sheep. Look away or move out of range: the contextual text should disappear. Aim through a fence or wall from outside: feeding must be blocked.
5. Press E once. Hunger should drop by about 35, confirmation should appear, Eating should last 4 seconds, and one Jump should play before Idle returns. Repeated E presses during Eating/cooldown must not feed again or cause errors. After 10 seconds feeding is allowed only if hunger is at least 25; otherwise the HUD says Sheep is not hungry. After feeding from Hungry, normal behaviour should resume.
6. Continue for several minutes, feeding as needed so wandering resumes. Check that Daisy cannot escape through the open gate and does not intersect fences. Check the HUD remains readable at your Game view resolution.
7. Return to all three plots: E plants one seedling, E waters, further presses cause no duplicates. Confirm independent plot states/prompts. Confirm the crate still falls and logs its first landing.
8. Stop and restart Play Mode. Confirm exactly one sheep, initial hunger reset, working interactions, and no red Console errors. Do not save temporary Play Mode changes as scene configuration.

Stop at Phase 4 and report test results before adding more systems.
