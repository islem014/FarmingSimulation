# Phase 8A — Energy, resting, personalities and welfare

Implemented for Daisy, Marguerite, Peppa and Spirit. Ready for manual Play Mode testing. The assistant ran compilation and Unity Editor checks, not Play Mode gameplay tests. Unity was already in the user's Play session when accessed; it was stopped to apply prefab settings safely. No Phase 8B features were added.

## Inspection and reuse

AnimalStats already owned hunger, thirst, health, behavior and change events. FarmAnimalBehaviour shared the Phase 5–7 state machine, routes, CharacterController movement and consumption; SheepBehaviour is its GUID-preserving compatibility wrapper. AnimalCare handles treatment and per-animal cooldowns. AnimalInteractable and FarmHUD already provided event-driven contextual information. There were no energy, fatigue or calculated welfare statistics. NeedsCare previously stopped movement but had no energy recovery.

This phase extends these same components. There is no duplicate needs manager, no separate species AI, and no configuration ScriptableObject. Existing component fields on the four prefabs are the reusable personality configuration. Each scene instance inherits its own prefab values. Animal meshes, transforms, controllers, clips, enclosures and troughs are unchanged, including the Phase 7 visual-size correction. Cow/horse use their existing movement animations; sheep/pig have no walking clip. All use existing Idle while resting. Sleeping was omitted because there is no separate sleep mechanic or animation to distinguish it from Resting.

## Energy

Energy is independent from hunger, thirst and health and starts at 100. AnimalStats owns it and clamps it to 0–100. After CharacterController.Move, the AI measures actual horizontal displacement:

`energy = clamp(energy - metresMoved × EnergyPerMetre + secondsResting × RestRecoveryPerSecond, 0, 100)`

Gravity does not count as movement. Attempting to move against an obstacle does not drain energy unless the controller actually travels. Eating, drinking, treatment, Idle and ordinary waiting do not restore energy. Resting and stationary NeedsCare restore energy; care recovery is intentional because the animal is already resting. An animal at zero energy can still move to essential resources or a resting spot; it is never permanently disabled.

Rest Threshold latches the need for rest. It clears only at Recovered Threshold, which must be at least one point higher. Crossing back above the entry threshold alone does not cancel rest. Both thresholds and both energy rates are Inspector-configurable. Recovery has a minimum of 0.1/second to avoid accidental permanent exhaustion.

## Rest locations and priorities

SeekingRest and Resting are appended to the existing enum, preserving old serialized state values. The AI first considers its current position, checking enclosure bounds, controller clearance and distance from trough use points. If unsuitable, it tries at most eight sampled locations within three metres, accepting only unobstructed points and routes. Travel is limited to ten seconds. If no route succeeds or a route becomes blocked, it rests where its CharacterController already stands instead of repeatedly starting navigation. This fallback may be near a trough; there is only one animal per pen, and it does not teleport or ignore physics.

Resting has zero commanded horizontal movement, with normal gravity and enclosure clamping retained. Once recovered, the animal checks food/water/care before returning to ordinary activity.

Priority rules:

1. Preserve an active drinking activity. Preserve an active meal unless severe thirst has reachable water; this retains the validated consumption/hysteresis behavior.
2. Outside an active meal/drink, reachable severe thirst comes first, then reachable severe hunger. These can interrupt rest. Empty or blocked troughs do not repeatedly cancel recovery, and failed routes retain the existing retry delay.
3. Low health interrupts rest into NeedsCare. Essential food/water remains available even at zero health. Treatment is unchanged.
4. Otherwise a latched low-energy need takes priority over ordinary hunger/thirst. Rest continues to the recovery threshold, unless interrupted above.
5. After recovery, ordinary water then food are considered, followed by care/waiting or Idle/Wander.

Severe thresholds remain on AnimalStats (default 85); ordinary seeking thresholds remain on the behavior (65). Keep severe thresholds at or above the corresponding ordinary threshold when tuning. Empty food/water never supplies needs or energy for free. Welfare itself does not select an AI state or alter health.

## Personality presets

These are gameplay tuning choices, not scientifically validated animal behavior.

| Animal | Personality | Speed (unchanged) | Idle pause | Maximum wander trip | Energy/metre | Rest recovery/s | Rest entry / exit | Bite interval |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| Daisy | Calm, frequent rest | 0.7 | 4s | 2.5m | 3 | 7 | 35 / 80 | 2s |
| Marguerite | Calm, slow meals | 0.65 | 5s | 2m | 2.5 | 5 | 25 / 80 | 3s |
| Peppa | Curious, short pauses | 0.6 | 1.5s | 3m | 3 | 7 | 25 / 80 | 2s |
| Spirit | Active, longer trips | 1.1 | 2s | 5m | 5 | 6 | 30 / 80 | 2s |

Personality is an Inspector label; the adjacent numeric settings drive behavior. Maximum trip distance is a cap, not a guaranteed distance: the enclosure and obstacles may require shorter trips. Existing species hunger/thirst rates, consumption costs/reductions, drinking intervals and care cooldowns remain unchanged. Cow meals now take longer because its bite interval is three seconds.

## Welfare and HUD

`welfare = ((100 - hunger) + (100 - thirst) + health + energy) / 4`

Each factor has an equal 25% weight. The score is calculated, not a second editable or stored need; it stays within 0–100 and does not cause extra damage. Default classifications:

- Excellent: 85–100
- Good: 65–below 85
- Needs Attention: 40–below 65
- Critical: below 40

The three boundaries are independently configurable on AnimalStats and clamped into ascending order. HUD rounding does not change classification: classification uses the unrounded score. This is a simplified gameplay indicator, not a veterinary diagnosis. An average can hide a severe single need, so individual values, care condition and severe warnings remain visible and authoritative.

AnimalInteractable adds one compact line: `Energy …/100 | Welfare …/100 (status)`. Name/species, hunger/thirst/health, AI state, condition and warnings remain. Existing FarmHUD wrapping/panel sizing required no changes. Energy/welfare changes publish the existing stats event, including status transitions. E still inspects/treats animals or refills the targeted trough. Planting and watering keep their original routes. No dashboard was introduced.

## Verification

Runtime and editor code compile against the installed Unity 6000.6.7f1 compiler and assemblies. Phase8ASetup ran in the open licensed Unity Editor and saved only the four animal prefab configurations. It reloaded FarmScene and validated:

- Existing Phase 5/6 component regression tests.
- Phase 7 feeding/drinking, independent resource ownership, treatment/cooldown, scene/Animator references, route clearance, entrances and 400 bounded sample destinations.
- Energy calculation and both clamps, negative-input rejection, rest recovery, and no energy restoration from feeding/drinking/treatment.
- All 16 extreme combinations of the four welfare inputs, classification boundaries, and independent per-species settings.
- 100 consecutive priority evaluations while partially rested, and 100 with unavailable severe-thirst resources, without leaving Resting; severe food/water and low-health interrupts; recovery to Idle and bounded rest-route timeout.
- Personality travel-distance caps and enclosure bounds.
- Corrected animated cow/pig/horse heights after Animator initialization, guarding against the earlier Phase 7 scale bug.
- Missing scripts and broken serialized references in the saved scene and all project-owned prefabs.

Results are in `.phase8a-work/validation.txt`. These are isolated Editor method/physics checks, not continuous gameplay/navigation or visual HUD testing. The user must run the tests below. Scene/build settings, imported assets, animation files, enclosure prefabs, third-person scripts, planting, crate, packages and existing GUIDs were preserved.

## Exact manual tests

Use scene instances under Gameplay/Animals **only after entering Play Mode**. Pause while setting Inspector values if helpful, then unpause. Do not apply overrides to prefab assets. Stop/restart Play Mode to discard test values.

1. **Regression and appearance:** Open FarmScene, enter Play Mode and check for new Console errors. Confirm all four animals are visible at their established sizes. Test farmer movement, camera orbit/collision, Escape/click capture, animal/trough E interactions, all three plots, and the crate. Confirm contextual panels hide out of range/behind obstacles.
2. **Natural differences:** Observe healthy animals with full supplies. Peppa pauses less often/for less time; Marguerite pauses longer and takes three seconds per bite; Spirit moves faster and can choose longer trips. Every animal remains within its pen. The lack of a pig/sheep walking clip is unchanged.
3. **Movement energy:** On one animal set Hunger=20, Thirst=20, Health=100, both need progression rates=0, Energy=100, Energy Per Metre=10. Watch several wander trips: energy decreases while translating and stays unchanged during Idle. Vertical settling should not drain it. Standing in the route should not cause ongoing energy loss when completely stationary.
4. **Fast rest cycle:** Use the same stable-needs settings; set Energy=10, Rest Threshold=25, Recovered Threshold=80, Rest Recovery Per Second=10. Expect SeekingRest briefly if a quiet point is needed, then Resting with no horizontal movement and increasing energy. From 10 to 80 takes roughly seven stationary seconds. It then resumes Idle/Wander. Confirm no repeated Resting/Wander switching around 25.
5. **Hysteresis:** While Resting, pause and set Energy=40 (above entry but below 80). Resume: it must keep resting. Set Energy=80: it should leave rest. Repeat on all four animals, accounting for their prefab entry thresholds when not overridden.
6. **Severe thirst interruption:** Refill that animal's water trough to 10. With Energy=10 and ordinary needs 20, wait for Resting; set Thirst=90. It should seek/drink its own water despite exhaustion. The drink reduces thirst but does not directly refill energy. It returns to rest when the activity finishes if still tired. Keep both progression rates zero for clarity.
7. **Severe hunger interruption:** Repeat with water need 20, food stocked, Hunger=90. Expect seeking/eating and correct stock consumption (cow/horse two units per bite, sheep/pig one). After satisfaction it can resume rest. Ordinary Hunger=70 with low energy should wait until rest completes.
8. **Empty supply and recovery:** Set water stock to 0, Thirst=90, Energy=10 and temporarily Health Damage Per Second=0. The animal can rest while water is unavailable; it must not gain hydration or flicker every frame between objectives. Refill water: severe thirst should interrupt rest and drink. Restore damage by restarting Play Mode afterward.
9. **Care interruption:** With both needs=20, both progression rates=0 and Energy=10, let it rest; set Health=30. Expect NeedsCare and normal treatment prompt. E gives 35 health, repeated E cannot bypass cooldown. Resting in NeedsCare can recover energy. After sufficient treatment and energy recovery it resumes normal behavior.
10. **Welfare formula:** Pause an animal with rates zero. Set Hunger=20, Thirst=20, Health=100, Energy=100: expect Welfare=90, Excellent. Set all four values to 50: expect Welfare=50, Needs Attention. Set Hunger=100, Thirst=100, Health=0, Energy=0: expect Welfare=0, Critical. Resume briefly to refresh events; AI changes may begin affecting values. Check individual warnings remain even when an average score is relatively high.
11. **Bounds and routing:** Observe multiple rest/feeding cycles and all entrances. Stand temporarily in a rest path, then move away. It must stop safely or fall back to resting instead of navigating forever or passing through colliders. No animal should leave its enclosure.
12. **Independence/reset:** Change only Spirit's energy/rates and verify the other animals retain their values/settings and own troughs. Stop Play Mode: the four prefab defaults return. Re-run Daisy's previous food, water, care and cooldown tests.

## File inventory

Modified:

- `Assets/FarmingSimulation/Scripts/Animals/AnimalStats.cs` — energy, welfare, thresholds and feedback; appended rest states.
- `Assets/FarmingSimulation/Scripts/Animals/FarmAnimalBehaviour.cs` — rest priorities/navigation, measured movement energy, personality distance/pause tuning.
- `Assets/FarmingSimulation/Scripts/Animals/AnimalInteractable.cs` — energy/welfare details.
- `Assets/FarmingSimulation/Animals/Prefabs/Sheep.prefab`
- `Assets/FarmingSimulation/Animals/Prefabs/Cow.prefab`
- `Assets/FarmingSimulation/Animals/Prefabs/Pig.prefab`
- `Assets/FarmingSimulation/Animals/Prefabs/Horse.prefab`

Created:

- `Assets/FarmingSimulation/Editor/Phase8ASetup.cs` and `.meta` — one-shot preset application and Editor validation.
- `Assets/FarmingSimulation/PHASE8A.md` and `.meta` — this report.

Project-only scratch files under `.phase8a-work/`: baseline hashes, compiler response files and DLL/PDB outputs, validation/status and file audit. They are outside Assets and not included as gameplay. The setup request is consumed; prefab configuration is not automatically reset on subsequent Editor starts. Do not request setup again after customizing the animal presets.

## Limitations

There is no sleeping animation, day/night schedule, general pathfinding, multi-animal resting reservation, dashboard or persistence. Rest locations use the existing simple obstacle queries and a bounded search, not navigation meshes. Fallback rest can occur near a trough if no quiet route is available. Continuous animation, long-running movement and HUD readability still require the manual tests. Welfare is an average gameplay indicator, not a diagnosis or a new source of health damage. No Phase 8B, reproduction, products, inventory or additional species were implemented.
