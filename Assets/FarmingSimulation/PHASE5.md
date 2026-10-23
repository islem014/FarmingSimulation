# Phase 5 — Animal feeding

Phase 5 is implemented and saved in FarmScene. Editor validation passed; assistant Play Mode gameplay testing was not performed. This report supersedes Phase 4's direct-feeding instructions. No Phase 6 features were added.

## Inspection and reuse

The existing AnimalStats component already owned hunger, thirst, health, name, species, and behaviour. Hunger started at 45, increased by 0.8/second and was clamped to 0–100. SheepBehaviour used a hungry threshold of 65, a CharacterController, enclosure bounds, seeded random destinations and sphere casts for obstacle avoidance. Phase 4 direct feeding removed 35 hunger with a 10-second cooldown.

PlayerInteractor already selected a FarmInteractable using the camera-centre ray, a three-metre player reach limit and a second line-of-sight ray from the player. FarmHUD already subscribed to target feedback events. These components and FarmPlot needed no changes.

The enclosure contained one decorative MarpaStudio trough under a project-owned collider wrapper. The wrapper was converted to a reusable FeedingTrough prefab and nested back into the enclosure at the same location. Imported source assets were not edited.

## Final interaction design

- Look at the trough within player reach and press E to add food. This uses the existing input handler, range and obstruction checks.
- The trough displays `Food: 0/10`, `Press E to Refill Trough`, or `Trough Full`, as appropriate. It also shows portion size and refill amount.
- Look at Daisy to see her name, hunger, state, thirst and health. E inspects her feeding status; it does not feed her. After inspection the prompt line displays her current feeding status for the remainder of that Play session.
- Contextual information still disappears when looking away, moving out of range, or releasing the cursor.
- Planting and watering continue to use E with their original prompts and independent plot states.

Direct feeding and its cooldown fields/method were removed from SheepBehaviour. There is now one food source: a stocked trough. Unlimited refills are intentional for this phase; no inventory or resource cost was introduced.

## Inspector defaults

| Component | Setting | Default |
| --- | --- | --- |
| FeedingTrough | Capacity / initial food | 10 / 0 |
| FeedingTrough | Refill amount | 5 units per E press |
| FeedingTrough | Units per portion | 1 |
| AnimalStats | Initial hunger / increase | 45 / 0.8 per second |
| SheepBehaviour | Hungry / satisfied thresholds | 65 / 20 |
| SheepBehaviour | Portion interval | 2 seconds |
| SheepBehaviour | Hunger reduction per portion | 20 |
| SheepBehaviour | Retry interval | 3 seconds |
| SheepBehaviour | Speed / idle wait | 0.7 metres per second / 3 seconds |

Higher hunger means greater need. Thirst and health remain available but unchanged. Runtime/Inspector guards clamp food to capacity and require positive refill/portion quantities. Satisfied threshold must be lower than hungry threshold.

The hay visual has no collider. Empty hides it, partial food gives a lower hay level, and full food gives the highest level. No shared source material is changed at runtime.

## AI and script communication

`PlayerInteractor -> FeedingTrough.Interact -> food quantity + events`

`SheepBehaviour -> FeedingTrough.TryConsumePortion -> AnimalStats.ReduceHunger -> events -> FarmHUD`

SeekingFood was appended to the existing behaviour enum, preserving the serialized numbers of previous states.

1. Idle/Wander continue until hunger reaches 65.
2. The sheep remembers that it needs a meal until hunger reaches 20 or less. Falling below 65 during eating does not prematurely end the meal.
3. An empty trough leaves the sheep in Hungry. It checks again every three seconds without entering Eating or receiving free hunger reduction. A remainder smaller than the configured portion is also insufficient food.
4. Available food permits SeekingFood. The sheep first checks a direct route to the feeding position, then an optional approach waypoint. Invalid/out-of-bounds/blocked routes leave it Hungry with a retry status.
5. On arrival, Eating consumes one complete portion every two seconds. Only successful consumption reduces hunger. The first successful portion in a meal triggers the existing Jump animation; Idle remains the visual placeholder for movement/eating.
6. Satisfaction returns the sheep to Idle/Wander. An empty trough interrupts an unfinished meal and leaves it Hungry, including when hunger is now below 65. Refilling lets that unfinished meal resume.

The original enclosure clamp, CharacterController, gravity, rotation and obstacle probes remain in use. Eating is allowed only near the feeding position. A displaced trough or blocked feeding spot interrupts the meal. The logic uses serialized component references, not Daisy's GameObject name.

## Scene and prefab references

- Environment/AnimalEnclosure contains the new nested FeedingTrough prefab.
- Trough world position: **(-12.4, 0, -18.7)**, unchanged from the old decorative wrapper.
- Feeding position: **(-12.4, 0, -20.05)**, on the south side of the trough.
- Approach position: **(-10.3, 0, -20.05)**, beside its east end.
- Daisy starts at **(-11.5, 0.05, -21.5)**, with the scene enclosure and trough assigned.
- Enclosure walking bounds remain X [-14, -8], Z [-24, -18]; fence bounds remain X [-15, -7], Z [-25, -17].

The original trough collider retains its 2.4 by 0.55 by 0.85 metre size. Fences, gate, existing scenery, plots, player and crate were preserved. FarmScene remains the first enabled startup scene.

The standalone Sheep prefab cannot hold a scene-object reference: manually placed future instances need their enclosure and feeding trough assigned. The existing FarmScene instance is fully wired. A future trough placement also needs an accessible feeding point and, if needed, an approach point inside its animal's enclosure.

## Created and modified files

Paths below are relative to the project root.

Created:

- `Assets/FarmingSimulation/Scripts/Animals/FeedingTrough.cs`
- `Assets/FarmingSimulation/Animals/Prefabs/FeedingTrough.prefab`
- `Assets/FarmingSimulation/Animals/Materials/Trough_Hay.mat`
- `Assets/FarmingSimulation/Editor/Phase5Setup.cs`
- `Assets/FarmingSimulation/PHASE5.md`
- A matching `.meta` file for each of these five files.

Modified:

- `Assets/FarmingSimulation/Scripts/Animals/AnimalStats.cs` — appended SeekingFood; existing needs data retained.
- `Assets/FarmingSimulation/Scripts/Animals/SheepBehaviour.cs` — trough reference, seeking/eating logic, satisfaction threshold, timed portions/retries; removed direct feeding.
- `Assets/FarmingSimulation/Scripts/Animals/SheepInteractable.cs` — E now inspects status without changing hunger.
- `Assets/FarmingSimulation/Animals/Prefabs/Sheep.prefab` — serialized Phase 5 settings.
- `Assets/FarmingSimulation/Animals/Prefabs/AnimalEnclosure.prefab` — replaced decorative wrapper with the functional trough prefab at the same position.
- `Assets/FarmScene.unity` — assigned the scene trough to the existing sheep.

The project-root `.phase5-work` directory contains the before-change hash baseline, scene/prefab/script snapshots, compiler response files and validation outputs. These are local audit artifacts, not runtime game assets. Phase5Setup is an editor-only, explicit one-time migration; the request was consumed and the saved scene is ready without another setup run. It refuses to duplicate an existing trough prefab.

## Validation completed

- Runtime and editor C# compilation passed with the installed Unity compiler/references. Unity also compiled and executed the setup successfully.
- All checked serialized GUIDs and local file IDs resolve in FarmScene and the animal prefabs, animations, controller and materials.
- Unity verified one existing sheep/enclosure, assigned trough, in-bounds feeding/approach points, unobstructed feeding position and clear route from Daisy's starting position.
- Actual FeedingTrough methods were exercised on isolated editor preview instances: empty consumption rejection; partial/full refill; repeated refill cap; exactly ten one-unit portions; no negative food; hay visibility/height; event counts; and rejection of an incomplete three-unit portion.
- Actual sheep methods were exercised on preview instances with controlled state/timing: repeated empty-trough waiting; SeekingFood -> Eating; paid portions -> satisfaction; interrupted meal -> Hungry; refill and resume below the hungry threshold; and E inspection without hunger reduction.
- Those method-level checks do not simulate gameplay movement or frame timing. No assistant Play Mode gameplay test was performed.
- A hash comparison confirmed that only the six listed existing files changed. No baseline files were deleted. Imported MarpaStudio/Quaternius assets, player/camera code and prefabs, FarmPlot, PlayerInteractor, FarmHUD, crate assets, animations, packages and project settings are unchanged.

## Manual Unity Play Mode tests

1. **Start clean.** Open FarmScene, wait for compilation and check for red Console errors. Confirm one Daisy and one functional trough in the pen. Enter Play Mode. Recheck movement, orbit, camera collision, Escape, and click to recapture.
2. **Empty trough.** Walk through the east gate around (-7, 0, -21). Look at the trough: expect Food: 0/10 and no hay. Do not refill initially. After about 25 seconds from a fresh start, Daisy should become Hungry and stop. Inspect her with E: expect a refill status. Wait another ten seconds; hunger must not fall, and she must not repeatedly enter Eating.
3. **Refill and full limit.** Aim at the trough within three metres and press E once: food becomes 5/10 and hay appears. Press again promptly: normally 10/10 and a higher hay level. Repeated presses cannot exceed 10; `Trough Full` appears whenever full. If Daisy has already consumed a portion, account for that deduction rather than expecting an exact 10.
4. **Autonomous meal.** Step out of Daisy's path and away from the south-side feeding position. Within the retry interval she should enter SeekingFood, turn and move toward the trough, then Eating. Each portion removes one food and approximately 20 hunger (normal hunger progression continues). The first portion triggers one Jump. She should stop eating at 20 hunger or less and resume Idle/Wander. E on Daisy must never feed her directly.
5. **Food runs out.** Let later meals drain the trough without refilling. If it empties before satisfaction, Daisy stays Hungry with no further hunger reductions. Refill and verify she resumes her unfinished meal even if hunger is below 65. For a quicker temporary test, stop Play Mode, set the scene trough's Current Food to 1 and Daisy's Hunger to 70, then play. Restore the default scene values 0 and 45 afterward; do not save test settings as permanent defaults.
6. **Blocked route.** While Daisy seeks food, stand in her route/feeding spot. She should stop safely, show a waiting/retry status after inspection, and avoid clipping through you. Move away and allow a few seconds for retry. Watch several meals and wandering cycles to check enclosure containment and trough/fence avoidance.
7. **Interaction rules and HUD.** Looking away, exceeding reach, or putting a fence/wall between the farmer and target should remove the prompt/block E. Inspect Daisy for current name, hunger and state. Check hay differences from above and HUD readability at your Game view size.
8. **Regression and restart.** Plant and water all three plots; verify independent states and no duplicate seedlings. Confirm the crate still falls/logs its first landing. Stop/restart Play Mode: food returns to 0, hunger to 45, and there are no duplicate animals/troughs or red Console errors.

## Limits of Phase 5

The route planner uses direct paths plus one optional waypoint; it is not a general maze navigator. A persistent obstruction can leave Daisy waiting Hungry until the path clears. Food is not reserved and there is no queue for multiple animals; this phase has one sheep. Walk/Eat animation clips do not exist in the source model, so no such animations were invented. Food/state reset on restart. No thirst progression, animal watering, treatment, reproduction, new species, inventory or saving system was implemented.

Stop here and confirm the manual test results before Phase 6.
