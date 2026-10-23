# Phase 6 — Animal care, hydration and health

Phase 6 is implemented and saved in FarmScene. The checks below ran in Unity Editor; the assistant did not perform Play Mode gameplay testing. Stop at Phase 6 and confirm manual test results before adding another phase.

## Recovery from the interruption

The saved work contained an extended AnimalStats with thirst progression, severe-need exposure timers, health damage, conditions and health restoration. WaterTrough and AnimalCare scripts and their metadata were also present. Those implementations were retained. A small exposure-timer correction ensures a new threshold crossing starts a fresh grace period, including after Inspector edits.

SheepBehaviour still had only its Phase 5 feeding logic, SheepInteractable still only inspected, and the scene/prefabs were unchanged from the Phase 5 baseline. There was no water prefab/material or PHASE6.md. There is no Git repository; the saved `.phase6-work/baseline.json` and scene/prefab snapshots were used to identify changes. No valid partial work was reset.

This continuation completed drinking AI, priorities, treatment/HUD integration, the water prefab and material, prefab/scene references, editor validation, and this report.

## Features and ownership

- **AnimalStats** remains the single owner of hunger, thirst, health, condition and behaviour. All three numeric needs/health values stay in 0–100.
- **WaterTrough** owns only water stock. E refills it, and a successful complete portion must be consumed before thirst decreases. Empty/partial/full states are shown by a blue water level and the contextual quantity text.
- **SheepBehaviour** retains the existing food functions, CharacterController, gravity, enclosure clamp, rotation and obstacle checks. It adds water seeking/drinking and need priorities.
- **AnimalCare** restores health only when treatment is needed and off cooldown. It provides confirmation and cooldown events.
- **SheepInteractable** routes the existing E interaction to eligible treatment, otherwise inspection. There is no new input handler and no direct feeding/hydration shortcut.
- **PlayerInteractor** and **FarmHUD** are unchanged. Their existing target selection, distance/occlusion checks, feedback events and compact overlay support the new targets and details.

When looking at Daisy, the overlay displays her name, hunger, thirst, health, behaviour, condition and a warning. Its prompt prioritizes treatment confirmation, treatment eligibility, cooldown, then inspection/status. Food and water troughs each display their own quantity and refill instruction. Looking away or leaving reach hides contextual information as before.

## Default Inspector settings

| Component | Setting | Default |
| --- | --- | --- |
| AnimalStats | Initial hunger / thirst / health | 45 / 20 / 100 |
| AnimalStats | Hunger / thirst increase | 0.8 / 0.6 per second |
| AnimalStats | Severe hunger / thirst threshold | 85 / 85 |
| AnimalStats | Severe Need Grace Seconds | 15 |
| AnimalStats | Health Damage Per Second | 1 |
| AnimalStats | Care Health Threshold | 70 |
| AnimalStats | Sick Health Threshold | 35 |
| WaterTrough | Capacity / Current Water | 10 / 0 |
| WaterTrough | Refill Amount / Units Per Drink | 5 / 1 |
| SheepBehaviour | Thirsty Threshold / Hydrated Threshold | 65 / 20 |
| SheepBehaviour | Drink Interval / Thirst Per Drink | 2 seconds / 20 |
| SheepBehaviour | Shared route retry interval (Food Retry Interval) | 3 seconds |
| AnimalCare | Health Restored | 35 |
| AnimalCare | Treatment Cooldown / Feedback Duration | 15 / 3 seconds |

Food settings remain capacity 10, refill 5, one unit per portion, hunger thresholds 65/20, and 20 hunger removed every two seconds while eating. Values are configurable on the scene instance or owned prefab. Threshold and nonnegative-value guards run at initialization/Inspector validation.

Higher hunger/thirst means greater need. Higher health means better health.

## Health and care rules

Each severe need has its own exposure timer. Hunger or thirst must remain at or above its severe threshold for the grace period before damage begins. Bringing that need below its severe threshold resets its timer. Normal needs do not damage health. If both needs are severe, the animal takes the configured single damage rate, not double damage.

- **Healthy:** health above 70 with no severe hunger/thirst.
- **Needs Care:** health at or below 70, or a severe hunger/thirst warning while health is still above 35.
- **Sick:** health at or below 35.

The condition can warn about severe thirst before treatment becomes eligible. E treatment is eligible only when health is at or below the Care Health Threshold. It restores 35 health, capped at 100, shows confirmation, then enforces a 15-second cooldown. Repeated E cannot bypass the cooldown. Healthy animals cannot be treated unnecessarily.

Treatment does not refill troughs, reduce hunger/thirst, or cancel severe-need damage. Keep food and water available to address the cause. There is no passive regeneration or death state: even at zero health Daisy can seek food/water and be treated. Recovery from zero with defaults takes three accepted treatments (35, 70, 100), separated by cooldowns, provided deterioration has been addressed.

## AI priorities and stability

Existing enum values were preserved; SeekingWater, Drinking, NeedsCare and Thirsty were appended. Hungry remains the food-waiting state.

1. A started drink continues until hydrated, water runs out, or its spot becomes blocked.
2. A started food activity normally continues. Severe thirst (85 by default) can interrupt it for an available, reachable water route. If that route fails, the existing food destination is retained.
3. When choosing a new activity, available water for an unfinished thirst need takes priority over food. Otherwise available food is selected.
4. Failed routes have a three-second backoff, allowing another available need to be addressed. The AI does not repeatedly attempt the same failed route every frame.
5. When no need can currently be serviced and health needs treatment, Daisy rests in NeedsCare. This rest state never blocks food/water seeking when a usable supply becomes available.
6. Otherwise an unfinished empty-trough need waits as Thirsty or Hungry. Thirst/hunger are not reduced without stock consumption.
7. With no unfinished need and adequate health, Daisy returns to Idle/Wander.

Separate start/stop thresholds provide hysteresis: after starting at 65, a need remains unfinished until 20 or less. Empty-trough interruption does not erase that need; refilling resumes it even below 65. Food and water never set competing movement targets within one frame. CharacterController movement and the existing enclosure clamp remain active in all states.

Idle remains the visual placeholder for movement, drinking, resting and treatment. The existing single Jump on the first paid food portion is retained. No walking, drinking or treatment clips were invented, and no Animator assets were changed.

## Scene and prefab layout

There is still one Daisy and one enclosure. The existing food trough remains north of the pen. The new reusable WaterTrough prefab is nested under Environment/AnimalEnclosure on the south side:

| Object/point | World position |
| --- | --- |
| Water trough | (-12.4, 0, -23.3) |
| Drinking position | (-12.4, 0, -21.95) |
| Water approach position | (-10.3, 0, -21.95) |
| Existing food trough | (-12.4, 0, -18.7) |
| Feeding position | (-12.4, 0, -20.05) |
| Daisy's start | (-11.5, 0.05, -21.5) |

The water trough reuses the existing trough model through project-owned copies/references; no original MarpaStudio or Quaternius file was edited. Its solid collider is 2.4 by 0.55 by 0.85 metres. The blue level has no collider. The pen entrance, planting area, buildings, player and crate remain unchanged.

FarmScene's sheep has the enclosure, food trough, water trough and Animator assigned. AnimalCare is on the owned Sheep prefab. Future separately placed sheep instances need their scene enclosure/food/water references assigned. FarmScene remains the first enabled startup scene.

## Files created and modified

Paths are relative to the project root. Each newly created asset has its matching `.meta` file.

Created before interruption, retained:

- `Assets/FarmingSimulation/Scripts/Animals/WaterTrough.cs` and `.meta`
- `Assets/FarmingSimulation/Scripts/Animals/AnimalCare.cs` and `.meta`

Created during continuation:

- `Assets/FarmingSimulation/Animals/Prefabs/WaterTrough.prefab` and `.meta`
- `Assets/FarmingSimulation/Animals/Materials/Trough_Water.mat` and `.meta`
- `Assets/FarmingSimulation/Editor/Phase6Setup.cs` and `.meta`
- `Assets/FarmingSimulation/Editor/Phase6Validation.cs` and `.meta`
- `Assets/FarmingSimulation/PHASE6.md` and `.meta`

Modified across Phase 6:

- `Assets/FarmingSimulation/Scripts/Animals/AnimalStats.cs`
- `Assets/FarmingSimulation/Scripts/Animals/SheepBehaviour.cs`
- `Assets/FarmingSimulation/Scripts/Animals/SheepInteractable.cs`
- `Assets/FarmingSimulation/Animals/Prefabs/Sheep.prefab`
- `Assets/FarmingSimulation/Animals/Prefabs/AnimalEnclosure.prefab`
- `Assets/FarmScene.unity`

The `.phase6-work` project-root folder holds local recovery snapshots, baseline hashes, compiler response/output files and validation results; it is not part of gameplay. The one-time setup request was consumed. No setup command is needed before playing the saved scene.

## Validation performed

- Runtime and editor C# compilation passed using the installed Unity compiler/references. The running Unity Editor compiled and executed setup successfully.
- Unity checked one sheep, one food trough and one water trough, assigned scene/care/Animator references and unchanged root-motion configuration.
- Physics queries confirmed all four trough approach/use points are inside the walking bounds and unobstructed. Routes from the starting sheep to each trough and between the two use points are clear.
- Editor preview instances exercised actual water methods: empty rejection, partial/full levels, capped refills, event counts, exact portions, insufficient portions and nonnegative stock.
- Actual needs/care methods passed normal-needs/no-damage, grace delay/reset, combined damage rate, min/max clamps, conditions, treatment amount/confirmation, cooldown rejection/expiry, unnecessary-treatment rejection and recovery from zero.
- Actual AI methods passed water seeking/drinking, empty interruption/resumption, hysteresis, tie priority, stable food activities, severe-thirst preemption, failed water-route preservation, zero-health hydration and NeedsCare recovery.
- The existing Phase 5 food-limit and meal/interruption checks were rerun successfully.
- Serialized GUID/local-ID auditing covers FarmScene and animal prefabs, animations, controller and materials. Hash auditing confirms imported sources, feeding code/prefab, movement/camera, HUD, plots, crate, Input System, packages and build settings are unchanged.

These are editor method-level and physics-query checks, not a full simulation of gameplay, navigation, input or rendered HUD. The assistant did not perform Play Mode gameplay testing.

## Manual Play Mode test checklist

1. **Startup/regression:** Open FarmScene, wait for compilation, and check for red Console errors. Enter Play Mode. Verify movement, camera orbit/collision, Escape/click cursor controls and the pen entrance. Confirm one sheep, a food trough to the north and a water trough to the south.
2. **Water quantity and visuals:** Approach the water trough within three metres. Expect Water: 0/10 and no blue water. E gives 5/10 with a partial level; another E gives 10/10 and a higher level. Repeated E cannot exceed capacity. Confirm the full prompt. Account for any water Daisy consumes during testing.
3. **Autonomous drinking:** From a fresh start normal thirst reaches 65 after approximately 75 seconds. To shorten this, use the Play Mode settings below. When thirsty with water available, Daisy should seek the south-side drinking position, stop and drink. Every two seconds one water unit disappears and thirst falls by about 20 (ongoing thirst progression explains small differences). She stops at 20 or less and checks food/care before wandering.
4. **Empty supply/resumption:** Start with empty water and elevated thirst. Daisy must not get free hydration or flicker continuously between SeekingWater/Drinking. Food remains usable if she also needs it. Fill the water trough and clear her path. She should begin drinking. Let stock run out during an unfinished drink; thirst remains elevated and she resumes after a refill, even when below 65.
5. **Health decline:** Use the isolated dehydration setup below. Health must remain unchanged during the grace period, then fall at the configured rate. Verify Needs Care/Sick conditions and the E treatment prompt once health reaches the care threshold. Refill water and let her drink; damage should stop after thirst drops below the severe threshold, provided hunger is not severe.
6. **Treatment/cooldown:** Aim at Daisy within reach and press E when eligible. Health rises by the configured amount, capped at 100, and confirmation appears. Repeated E during cooldown must not restore more health. After cooldown, another treatment works only if health is still at/below the care threshold. A healthy animal offers inspection rather than unnecessary treatment. Continued dehydration may still reduce health after treatment; that is intentional.
7. **Priorities/recovery:** Temporarily set both hunger and thirst to 70 with both troughs stocked. Water should be chosen first if no activity is underway. During Eating, ordinary thirst should not interrupt the meal; severe thirst should lead to reachable water. With low health and no active need, Daisy rests in NeedsCare; after sufficient treatment she resumes. At zero health she remains treatable and can still reach supplies.
8. **Obstacles and containment:** Stand in a route/use point briefly, then move away. Daisy should wait/retry without clipping through you, fences or troughs. Watch several food/water cycles. She must stay inside the pen. Blocked-water attempts should not continually cancel a workable food activity.
9. **HUD and interactions:** Check all animal values, condition, behaviour, treatment feedback/cooldown and water/food prompts. Look away, move out of reach or put a wall/fence between player and target: interactions should be blocked and prompts hidden as before. Plant and water all three plots; check independent states/no duplicates. Confirm crate landing behaviour.
10. **Reset:** Stop and restart Play Mode. Defaults return (hunger 45, thirst 20, health 100, empty troughs), with no duplicate objects or red errors.

## Safe accelerated tests in the Inspector

Change **scene-instance components only while already in Play Mode**. Pause Play Mode while editing if needed; then unpause. Select Gameplay/Animals/Sheep in the Hierarchy and edit AnimalStats/SheepBehaviour/AnimalCare. Do not edit prefab assets, press Apply, or save test values. Stopping Play Mode discards scene-instance test settings.

For a fast thirst demonstration:

- AnimalStats: Hunger = 20, Hunger Per Second = 0, Thirst = 60, Thirst Per Second = 5.
- Keep the water trough stocked and stay out of Daisy's route. She reaches the thirsty threshold in about one second. Change Thirst Per Second back to 0.6 once she starts seeking to observe normal drinking and satisfaction.

For a fast, isolated dehydration/health demonstration, start a fresh Play session with the water trough empty:

- AnimalStats: Hunger = 20, Hunger Per Second = 0, Thirst = 90, Thirst Per Second = 0, Health = 80.
- Severe Thirst Threshold = 85, Severe Need Grace Seconds = 3, Health Damage Per Second = 5.
- After unpausing, health stays at 80 for approximately three seconds, then declines. Around five seconds total it reaches 70 and treatment becomes eligible.
- Refill water to demonstrate cause-and-effect recovery. For an isolated cooldown test, pause and set Thirst = 20 and Hunger = 20 first; unpause to stop further deterioration, then treat.
- To demonstrate cooldown rejection without needing multiple damage cycles, temporarily set Health = 30. First treatment gives 65, further immediate presses do nothing, and the next treatment after cooldown reaches 100. You may shorten AnimalCare/Treatment Cooldown to 5 seconds before the first treatment for the presentation.
- To test recovery from zero, set Health = 0 with hunger/thirst at 20 and both progression rates at 0. Give three treatments separated by cooldowns. Daisy must eventually leave NeedsCare.

## Known limitations

Navigation remains direct-path movement with one optional approach waypoint, not general pathfinding. A persistent obstruction can leave a need waiting; retries are timed. Health, food and water reset when Play Mode restarts; there is no save system, inventory cost, multi-animal queue or resource reservation. Water is an opaque blue level indicator rather than a fluid simulation. Only the source Idle/Jump animations exist. No additional animals, reproduction, farm products, or Phase 7 work was added.
