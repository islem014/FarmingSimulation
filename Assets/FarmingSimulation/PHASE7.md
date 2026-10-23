# Phase 7 — Multi-species animal system

Phase 7 is implemented and saved in FarmScene, ready for manual Unity Play Mode testing. Unity Editor method, physics, Animator and saved-reference checks passed. The assistant did not perform Play Mode gameplay testing. Stop here and confirm the manual results before extending gameplay.

## Recovery summary

The interrupted implementation already contained shared FarmAnimalBehaviour and AnimalInteractable components, compatibility wrappers for Daisy, multi-portion trough consumption, the expanded HUD, model inspection, and the setup/validation scripts. These were retained. There is no Git repository; `.phase7-work/baseline.json` records the pre-Phase-7 hashes.

The first setup attempt stopped on Unity's missing-Animator placeholder: C# null-coalescing did not use Unity's overloaded null comparison. Phase7Setup now explicitly checks `animator == null` before adding the component. The resumed setup succeeded and saved all three animals and pens. No duplicate species or pens were found. Existing assets produced before interruption were reused.

Continuation completed the recovery audit, final compilation, validation after reloading the saved FarmScene, checks for missing scripts/broken serialized references in all project-owned prefabs, and this documentation. No previous files were deleted. Only the six existing files listed below changed from the baseline; original source assets, Daisy's prefab/enclosure, player/farming scripts, package manifests and build settings retained their hashes.

## Architecture and configuration

- AnimalStats remains the sole owner of name, species, hunger, thirst, health, condition and current AI state. AnimalCare retains per-instance treatment and cooldown logic.
- FarmAnimalBehaviour contains the existing sheep state machine, movement, gravity, obstacle queries, feeding and drinking. SheepBehaviour is a thin compatibility subclass retaining its original script GUID and serialized field names. Daisy keeps her saved settings.
- AnimalInteractable shares the existing inspect/treat behavior. SheepInteractable retains its GUID as a compatibility subclass. No duplicate per-species needs or input systems were introduced.
- The existing `AnimalBehaviour` enum is retained; the shared component is named FarmAnimalBehaviour to avoid a name collision. Species settings are ordinary serialized prefab fields, not ScriptableObjects.
- PlayerInteractor is unchanged: immediate New Input System E, camera-centre aim, three-metre player reach, and a second player-to-target obstruction ray. Animal/trough feedback events feed the existing FarmHUD. The HUD now wraps and sizes the panel for the additional species line.
- Each animal has its own stats, care clock, random seed, state and assigned enclosure/trough references. Troughs parented to another enclosure are rejected. Complete resource requests are checked and deducted before events fire; insufficient stock never produces a partial bite or negative quantity.

| Animal | Speed m/s | Hunger/s | Thirst/s | Food/water units per bite/sip | Need reduction per bite/sip | Treatment cooldown |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Daisy — Sheep (preserved) | 0.7 | 0.8 | 0.6 | 1 / 1 | 20 / 20 | 15s |
| Marguerite — Cow | 0.65 | 0.65 | 0.7 | 2 / 2 | 24 / 24 | 18s |
| Peppa — Pig | 0.6 | 1 | 0.5 | 1 / 1 | 20 / 20 | 12s |
| Spirit — Horse | 1.1 | 0.7 | 0.9 | 2 / 2 | 25 / 25 | 20s |

All start at hunger 45, thirst 20, health 100. Seek thresholds are 65, satisfaction thresholds 20, and bites/sips occur every 2 seconds. Each trough starts empty, holds 10 units, and E adds 5 (clamped to capacity). Cow and horse require at least 2 units for a complete bite/sip; a remaining 1 unit waits for refill.

Health uses the existing severe thresholds 85, grace period 15 seconds and damage rate 1 health/second. Both severe needs together do not double the damage. Treatment is eligible at health <=70, restores 35 up to 100, and has an independent cooldown. Sick means health <=35. Health reaching zero does not permanently disable movement or treatment.

Water is considered first when both needs are waiting. An active meal normally finishes; severe thirst can interrupt it if water is available. Need latches persist until satisfied, avoiding rapid switching. Empty or blocked resources use timed retries. NeedsCare permits feeding/drinking and returns to normal activity after recovery. The existing Hungry and Thirsty waiting states are retained alongside Idle, Wander, SeekingFood, Eating, SeekingWater, Drinking and NeedsCare.

## Scene and enclosures

All four pens are 8 by 8 metres. Existing scenery and Daisy's pen were left in place. New pens reuse the existing project-owned enclosure design, including MarpaStudio fence/gate meshes, wrapper colliders and nested food/water trough prefabs. No imported prefab was edited. Gates are already open; they are not interactive.

| Animal | Pen centre X,Y,Z | Entrance X,Z | Visual height | Controller radius / height |
| --- | --- | --- | --- | --- |
| Daisy | -11,0,-21 | -7,-21 (east) | existing ~1.1m | existing 0.55 / 1.2m |
| Marguerite | -10,0,-3 | -6,-3 (east) | 1.5m | 0.8 / 1.7m |
| Peppa | 12,0,-25 | 8,-25 (west) | 0.85m | 0.6 / 1.3m |
| Spirit | 12,0,1 | 8,1 (west) | 1.8m | 0.85 / 1.8m |

Enclosures are under Environment; animals are under Gameplay/Animals. New animals spawn at their pen centre, Y=0.05. Each has one CharacterController and no Rigidbody. Capsule heights must be at least their diameters, so the pig capsule extends above the visible back. Animal destinations are inset 1.1m from new pen boundaries, including the open entrance. Feeding/drinking stand-off distances are 1.50m (cow), 1.35m (pig), 1.55m (horse); approach points avoid trough colliders. The animal prefabs require their scene-specific enclosure/food/water references when placed in a new scene; the existing FarmScene instances are fully assigned.

## Actual model and animation inspection

All four imported rigs are Generic, with +Z forward (checked using head and hips), color-only source materials, and importer scale 1. Source FBX and importer metadata were not changed. Owned URP/Lit materials copy source colors. New visual placement was normalized from baked Idle mesh vertices, avoiding the conservative imported skinned-renderer bounds. Normalization factors were approximately 0.002914 cow, 0.001852 pig, 0.002607 horse; these apply within the imported rig hierarchy, not as arbitrary scene root scales.

| Model | Clips found | Clips used |
| --- | --- | --- |
| Sheep | Idle preview, Jump | Existing Idle / Jump unchanged |
| Cow | Idle, Walk, WalkSlow, Run, Jump, Death | Idle (6.25s), WalkSlow (2.083s) |
| Pig | Idle, Jump | Idle (6.25s), Jump (1.5s) |
| Horse | Idle, Walk, WalkSlow, Run, Jump, Death | Idle (6.25s), WalkSlow (2s) |

Cow/horse controllers transition between Idle and Walk using actual planar movement. Idle and WalkSlow loop. Pig uses looping Idle and a non-looping feeding Jump, like Daisy. Pig has no walking clip and visibly slides while translating; the Jump is feedback, not a fabricated eating animation. No eating/drinking clips were found or invented. Cow/horse stay Idle while consuming. Root motion is disabled. Actual controller transitions, curve binding paths and stable visual-root placement/scale were checked in Editor.

## Validation completed

- Runtime and editor C# compiled using the installed Unity 6000.6.7f1 compiler/reference assemblies. The additional reference checker uses Unity 6.6's EntityId API.
- One-time setup completed in the licensed open Unity Editor, then FarmScene was reloaded from disk and validated again.
- Exactly four animals, four distinct species and four independently assigned pens; one food and one water trough per pen; one interactable/controller per animal, valid care/Animator references, no conflicting Rigidbody.
- Existing Phase 5/6 editor regression checks passed after the shared-component extraction.
- All four species' actual AI methods tested in isolated editor preview objects: feeding/drinking entry, full consumption, satisfaction, independent stats, species HUD, treatment, cooldown and recovery. Wrong-enclosure resources are rejected. Competing two-portion requests, insufficient amounts, zero, negative and overflow requests cannot overspend.
- Physics queries verified clear use/approach points, starting and between-trough routes, accessible player entrances, and new pen footprints clear of existing rendered scenery and plots. 400 sampled destinations stayed within enclosure bounds.
- No missing scripts or broken serialized object references found in the reloaded scene or project-owned prefabs. Deliberately unassigned scene references in reusable standalone animal prefabs are not broken references.
- Additional YAML audit checked 8,951 references across 30 scene/prefab/controller/material files: no unresolved external GUIDs or local file IDs; all project-owned assets have metadata.
- Hash audit: six existing files modified, no deletions; original MarpaStudio and animal source assets, Daisy prefab/enclosure, player/plot/crate prefabs and scripts, packages and build settings unchanged. FarmScene remains the enabled startup scene.
- These are Editor checks, not end-to-end Play Mode tests. Continuous movement, rendering, aiming and visual animation quality still need the manual tests below. Prior setup/compile errors may remain as historical Console entries; the final checks passed. A zero-height Game view can produce a RenderTexture error in the editor layout: enlarge that pane before testing.

## Manual Play Mode tests

1. Open Assets/FarmScene.unity, let compilation finish, enlarge the Game view, then enter Play Mode. Click Game to capture the cursor. Confirm no new red errors. Test WASD, camera orbit/collision, Escape and click recapture.
2. Walk from the courtyard to the three entrances listed above. Confirm all animals render with correct colors, feet near ground and sensible sizes. Observe wandering/rotation and pen confinement for a minute. Cow/horse should use walking animation when moving. Pig's missing walk animation is the documented limitation.
3. Approach each animal and aim at its body. Verify the correct name/species, hunger, thirst, health, behavior and condition. Look away or move beyond reach: the contextual panel disappears. Aim through a fence/building: blocked targets must not accept E. Enter the pen to interact.
4. For controlled tests, select the scene animal under Gameplay/Animals **while in Play Mode**. Set AnimalStats Hunger Per Second and Thirst Per Second to 0; set Hunger=70, Thirst=20. E-refill its food trough twice to 10. Observe SeekingFood -> Eating, falling stock and hunger, then Idle when hunger <=20. Cow bites cost 2 and reduce hunger by 24; horse 2/25; pig and sheep 1/20. Confirm other pens' stock does not change.
5. Set Hunger=20, Thirst=70, refill that animal's water trough to 10. Observe SeekingWater -> Drinking -> Idle, correct species consumption, reduced thirst and visible water level. Repeat for all four animals.
6. Test insufficient stock: while in Play Mode set a cow/horse trough to 1 unit and its matching need to 70. It must not consume a partial portion or satisfy itself. E-refill; it should resume. With empty food/water, needs remain unmet until refilled. Repeated E at full capacity never exceeds 10. Stock never goes negative.
7. To demonstrate deterioration safely, use one scene animal in Play Mode: empty both troughs, Hunger=20, Thirst=90, Health=100, Severe Need Grace Seconds=3, Health Damage Per Second=5. With progression rates still zero, health should remain stable during the grace period, then fall. Repeat with hunger 90/thirst 20. At both needs 20, health should stop falling. Do not apply these temporary values to prefabs.
8. Set both needs to 20 and Health=30. Aim at the animal and press E: health becomes 65, confirmation appears, and immediate repeated E cannot heal again. Wait its configured cooldown, press E again: health reaches 100. Healthy animals cannot receive unnecessary treatment. Confirm normal behavior resumes and another animal's cooldown/health is unaffected.
9. Set two or more animals hungry/thirsty simultaneously and refill their own troughs. Confirm independent routes, stock, needs and HUD. Restore normal progression by leaving/re-entering Play Mode; verify ordinary gradual needs and prioritization. Watch for long-term route stalls around troughs and the open gate.
10. Regression: repeat Daisy's feeding, drinking, care and boundary tests; plant and water all three plots (one seedling per plot, independent states); check crosshair/prompts, crate landing and building collision. Stop Play Mode to discard temporary Inspector test values. Report any errors or movement/animation problems before proceeding.

## Complete file inventory

All paths below are relative to the project root. Each newly created asset/script/report below has its own accompanying `.meta` file; existing metadata/GUIDs were preserved.

Modified existing files:

- Assets/FarmScene.unity — adds three pens and three wired animal instances.
- Assets/FarmingSimulation/Scripts/Animals/SheepBehaviour.cs — compatibility subclass.
- Assets/FarmingSimulation/Scripts/Animals/SheepInteractable.cs — compatibility subclass.
- Assets/FarmingSimulation/Scripts/Animals/FeedingTrough.cs — safe configurable multi-portion consumption.
- Assets/FarmingSimulation/Scripts/Animals/WaterTrough.cs — corresponding water consumption.
- Assets/FarmingSimulation/Scripts/UI/FarmHUD.cs — wrapped, automatically sized contextual panel.

Created scripts and reports:

- Assets/FarmingSimulation/Scripts/Animals/FarmAnimalBehaviour.cs
- Assets/FarmingSimulation/Scripts/Animals/AnimalInteractable.cs
- Assets/FarmingSimulation/Editor/Phase7Inspection.cs
- Assets/FarmingSimulation/Editor/Phase7Setup.cs
- Assets/FarmingSimulation/Editor/Phase7Validation.cs
- Assets/FarmingSimulation/Animals/Phase7AssetReport.txt
- Assets/FarmingSimulation/PHASE7.md

Created prefabs in Assets/FarmingSimulation/Animals/Prefabs/:

- Cow.prefab, CowEnclosure.prefab
- Pig.prefab, PigEnclosure.prefab
- Horse.prefab, HorseEnclosure.prefab

Created animation assets in Assets/FarmingSimulation/Animals/Animations/:

- Cow.controller, Cow_Idle.anim, Cow_WalkSlow.anim
- Pig.controller, Pig_Idle.anim, Pig_Jump.anim
- Horse.controller, Horse_Idle.anim, Horse_WalkSlow.anim

Created materials in Assets/FarmingSimulation/Animals/Materials/:

- Cow_Black.mat, Cow_Pink.mat, Cow_White.mat
- Pig_Material.mat, Pig_Material_003.mat
- Horse_Material_003.mat, Horse_Material_006.mat

Project-root audit scratch directory `.phase7-work/` contains the original hash baseline, scene/sheep source snapshots, inspection report, compiler response files and DLL/PDB outputs, setup status, editor and saved-validation results, and file/reference audits. It is outside Assets and is not shipped as gameplay. One-shot setup/validation request files are consumed by the editor; setup will not run again without an explicit request/menu action. Do not rerun setup to test gameplay.

## Limits

### Visibility correction after manual feedback

The first Phase 7 prefabs were too small: the mesh measurement applied renderer scale twice. The earlier size claim and normalization factors above were incorrect. `BakeMesh(mesh, true)` now compensates for renderer scale before transforming vertices to world space, following Unity 6.6's API semantics (https://docs.unity.cn/6000.6/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).

AnimalVisualRepair.cs (and its .meta) was added under Editor. It corrected Cow/Pig/Horse.prefab in place, verified their animated sizes after reloading, and adjusted their three enclosure prefabs' body inset and trough stand-off. Correct visual-placement scales are approximately 0.291, 0.185 and 0.261 respectively. Measured heights are 1.50m, 0.85m and 1.80m. Daisy and original imported assets were unchanged. Phase7Setup.cs now uses the corrected measurement for future generation. Diagnostic results are in `.phase7-work/visual-check.txt` and `visual-repair.txt`. Compilation and editor AI/route tests were repeated; Play Mode visibility still needs confirmation from the user.

One animal per pen is configured. Resource accounting safely rejects overspending, but there is no multi-animal queue/reservation system. Navigation remains the existing simple obstacle probing and approach-point system, not a NavMesh/pathfinding package; adding arbitrary obstacles may require adjusting routes. Capsules approximate elongated bodies, and animation speed is not foot-locked. All resource assignment and gameplay are local/in-memory. No reproduction, breeding, products, inventory, saving or Phase 8 features were added.
