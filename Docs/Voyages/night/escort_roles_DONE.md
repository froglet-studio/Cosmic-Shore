# Per-vessel Ark-escort AI roles - DONE

Branch `claude/voyage-escort-roles`, worktree `D:\Voyage\wt-d`, 2026-10-31. Brief: `D:\Voyage\briefs\vessel_kits_ai.md`.
Paths are relative to `Assets/_Scripts/`.

## 1. What changed

### Fixes (brief section 0 and 5)
| Finding | Fix |
|---|---|
| `AIPilot.RetargetCell` never called on a leg change (crystal seekers blind) | `ArkEscortRun.TrackLeg` calls `ArkEscortPilot.OnLegChanged(cell)` for every seat; the pilot calls `RetargetCell(cell.RuntimeData)` (and `Arm` does it for the current cell), remembering the original cell data and restoring it on `Disarm`. |
| Leash teleport at 1260 u kills any nucleus run | `BringWingmates` asks each seat `ArkEscortPilot.Leash(baseLeash)`; a role may let it out (`SquirrelRole` while cultivating: distance to the nucleus + its radius + `ai.squirrel.nucleusLeashExtra`). The base factor is the gene `ai.escort.leashFactor` (was the const 1.4). |
| Sim seats `Element.None`, so level-5 safeties are off (friendly fire on the hull) | `ArkEscortSim.Parse` seats `ArkEscortRoles.DefaultElement(vessel)`: Sparrow Charge (Domain-Safe Skybursts), Dolphin Space (Clean Blast), Serpent Space (Shield Steal), Squirrel/Scarab/others Charge. The toy seeds seats at 0.5 = level 5. |
| Serpent wall recruits Ark hull prisms | `SerpentWallAssembler.IsRecruitable` refuses any prism with an `Ark` in its parent chain. |
| Falcon (Termite, Shrike) missing from the Vessel Prefab Container breaks companion spawn | `ArkEscortRun.Deploy` and `VoyageRun.Deploy` probe `swapper.VesselPrefabContainer.TryGetShipPrefab(vessel, out _, reportMissing: false)` and skip the seat with one warning naming it. |
| Seats collapse onto the same creature; target flaps every 0.5 s | `ArkEscortContext.Claim(seat, current)`: per-seat claims and hysteresis (`ai.escort.retargetMargin`). |
| Threats sorted by distance | `ArkEscortContext.Rank`: score = nearness to the hull (not the centre) x `threatW_dist` + closing speed x `threatW_closing` + 0.5 inside graze reach. |

### Dispatcher and roles
- `ArkEscortPilot` is now a dispatcher: `Arm` resolves `IArkEscortRole` by `VesselStatus.VesselType` (`ArkEscortRoles.Create`), feeds the role's `Steer` to the vessel's `AIPilot` as its external target, runs the role's `Tick` every frame, exposes `Leash` and `OnLegChanged`.
- `ArkEscortContext` (one per run, `ArkEscortRun.Context`, ticked by the run): ranked threats, claims, threat centroid, `LaneClear(from, to, extraPadding)` (segment vs hull sphere `HullHalfLength + ai.escort.arkLaneMargin + padding`), stations, and `ArkEscortCrystalSites` (where creatures swam and free crystals lie, for the Scarab; installed as `PlaceSwitchActionExecutor.PlacementResolver` for the run).
- `ArkEscortRole.cs`: `IArkEscortRole`, `ArkEscortRoleBase` (ability press/suppress, replicated taps, ammo), `HeartChaserRole` (the old behaviour, for hulls without a role), `ArkEscortRoles` (factory + default elements).
- `SparrowDestroyerRole`: guns and Skyburst rocket taken off the blind cycler (`AIPilot.SetAbilityCycleSuppressed`) and pressed by the role (`AIPilot.PressAbility`): guns with the claimed creature within `gunRange` and `gunAimDeg` of the nose and the lane clear; the rocket within [`rocketMinRange`, `rocketMaxRange`], `rocketAimDeg`, ammo in hand, and the lane clear with the blast radius as padding. Flies a lead-pursuit point at `standoff` on the creature's Ark side, then the pass. With fewer than `nucleusSweepWhenThreatsBelow` threats in a hostile world it sweeps the nucleus with the guns.
- `SquirrelRole`: joust (fly the heart) only when `Speed - creature.CurrentSpeed >= joustMargin` and the heart is within `joustMaxTurnDeg`; otherwise a run-up straight (boost ring tapped, `ringOnRunup`). With fewer than `cultivateWhenThreatsBelow` threats in a world not yet the squad's (gene `cultivate`), races to the nucleus and weaves a golden-angle spiral at `weaveRadiusFrac` of its radius until own nucleus volume > hostile x `pacifyMargin`.
- `DolphinRole`: steers at the free crystal (omni/own-team, within `crystalSearchRadius` of the Ark) whose approach best lines up with the threats (`approachAlignDeg`), threats within `reach x reachUse` of it, and whose blast axis clears the hull by `coneArkClearance`; `SetDriftLookTargetProvider` aims the commit drift's nose at the threat centroid (null when the hull is on that line). Idle: skims the Ark's wake `wakeOffset` astern.
- `SerpentWallRole` (light): wall point `ark + dirToThreats x wallStandoff` (+ lateral spread per seat), approached from the threat side so the heading runs threats -> Ark; cloak pressed within `wallPlaceRadius`, off the cycler, every `wallCooldown`; optional stance hold (`useStance`, `holdSeconds`).
- `ScarabRingRole` (light): `TryAutopilotDash` at a claimed heart within `plateRange`; rings only on crystal sites (the resolver refuses a free press): the site on the Ark-to-threats line within `ringStandoff`, pressed when the ring centre (150 u ahead) is within `siteReach` of it, every `ringCooldown`.
- `AIPilot` additions: `TryFindAbility<T>()`, `SetAbilityCycleSuppressed(ability, bool)` (StartAIPilot skips suppressed abilities; restarts the cycle), `PressAbility(ability, down)`.

### Friendly-fire counter
- `Ark.HullPrismLost` (event, raised in the 0.5 s integrity scan for each hull prism found destroyed since the last scan).
- `ArkEscortRun.OnHullPrismLost`: counts prisms not `DestroyedByCreature` into `ArkEscortReport.FriendlyFire` and `LegReport.FriendlyFire`; `Summary()` prints kills and friendly fire.

## 2. Gene registry (`EscortGeneRegistry.cs`)

`EscortGeneRegistry.TrySet(string key, float value)` (clamped to range; false for an unknown key), `TryGet`, `ResetAll()`, `All`, `Describe()`, `ApplyPairs(IEnumerable<KeyValuePair<string,float>>)`. Keys are case-insensitive. All genes are declared as static fields of the registry so the whole set exists on first touch (`EnsureDefined()`).

| Key | Default | Range | Plugs into |
|---|---|---|---|
| ai.escort.guardRadius | 900 | 400-1400 | threat gathering radius (`ArkEscortPilot.GuardRadius`) |
| ai.escort.retargetSeconds | 0.5 | 0.2-2 | threat re-ranking period |
| ai.escort.retargetMargin | 0.15 | 0-0.5 | claim hysteresis (share of top score) |
| ai.escort.threatW_dist | 1 | 0-1 | threat score: nearness to hull |
| ai.escort.threatW_closing | 0.5 | 0-1 | threat score: closing speed |
| ai.escort.stationRadius | 260 | 120-600 | idle station offset |
| ai.escort.leashFactor | 1.4 | 1-3 | leash = min(toy WingmateLeash, guardRadius x this) |
| ai.escort.arkLaneMargin | 40 | 10-120 | hull padding in `LaneClear` |
| ai.sparrow.standoff | 150 | 60-300 | firing position offset on the creature's Ark side |
| ai.sparrow.leadSeconds | 0.4 | 0-1.5 | lead pursuit |
| ai.sparrow.rocketMinRange | 60 | 40-120 | rocket gate |
| ai.sparrow.rocketMaxRange | 220 | 120-320 | rocket gate |
| ai.sparrow.rocketAimDeg | 5 | 1-15 | rocket gate |
| ai.sparrow.rocketCooldown | 1.5 | 0.5-6 | seconds between rockets |
| ai.sparrow.rocketBlastRadius | 170 | 60-260 | lane padding for the rocket |
| ai.sparrow.gunAimDeg | 8 | 2-20 | gun gate |
| ai.sparrow.gunRange | 240 | 100-260 | gun gate |
| ai.sparrow.nucleusSweepWhenThreatsBelow | 1 | 0-4 | nucleus sweep trigger (0 never) |
| ai.sparrow.nucleusSweepRadiusFrac | 0.6 | 0.2-0.9 | sweep radius / nucleus radius |
| ai.squirrel.joustMargin | 10 | 1-60 | joust speed gate (u/s) |
| ai.squirrel.joustMaxTurnDeg | 70 | 15-120 | joust turn gate |
| ai.squirrel.runupDistance | 260 | 100-600 | run-up straight length |
| ai.squirrel.cultivate | 1 | 0-1 | 1: nucleus cultivation when idle |
| ai.squirrel.cultivateWhenThreatsBelow | 1 | 0-6 | cultivation trigger |
| ai.squirrel.pacifyMargin | 1.3 | 1-2 | own/hostile nucleus volume to stop |
| ai.squirrel.weaveRadiusFrac | 0.6 | 0.2-0.9 | nucleus weave radius |
| ai.squirrel.nucleusLeashExtra | 600 | 0-1500 | leash past the nucleus |
| ai.squirrel.ringOnRunup | 1 | 0-1 | tap the boost ring on a run-up |
| ai.dolphin.reach | 800 | 200-2400 | assumed cone reach |
| ai.dolphin.reachUse | 0.7 | 0.1-1 | threats must be within reach x this of the crystal |
| ai.dolphin.approachAlignDeg | 35 | 5-60 | preferred approach/fire alignment |
| ai.dolphin.coneArkClearance | 120 | 0-300 | hull padding for the cone axis |
| ai.dolphin.crystalSearchRadius | 1200 | 300-2500 | crystal candidates within this of the Ark |
| ai.dolphin.wakeOffset | 60 | 20-200 | idle wake-skim point astern |
| ai.serpent.wallStandoff | 320 | 150-600 | wall point distance from the Ark |
| ai.serpent.wallLateralSpread | 80 | 0-200 | per-seat sideways offset |
| ai.serpent.wallPlaceRadius | 50 | 20-120 | cloak within this of the wall point |
| ai.serpent.wallCooldown | 16 | 8-40 | seconds between walls |
| ai.serpent.useStance | 0 | 0-1 | stance hold at the wall point |
| ai.serpent.holdSeconds | 4 | 0-12 | stance hold length |
| ai.scarab.plateRange | 160 | 60-260 | dash gate |
| ai.scarab.ringStandoff | 250 | 100-500 | ring sites within this of the Ark |
| ai.scarab.siteMaxAge | 8 | 1-20 | site memory (s) |
| ai.scarab.siteReach | 70 | 30-150 | ring centre to site tolerance |
| ai.scarab.ringCooldown | 12 | 2-60 | seconds between ring presses |

Hook for the other branch (`claude/voyage-ark-core`, `-arkEscortParams`): parse the JSON into key/value pairs and call `EscortGeneRegistry.TrySet(key, value)` per pair (or `ApplyPairs`), ideally before `SetSail`; call `ResetAll()` between runs when a run should not inherit the last file.

## 3. Compile and sim check

- Compile: `Unity.exe -batchmode -quit -nographics -projectPath D:\Voyage\wt-d` (log `D:\VoyageLogs\compile_wt-d.log`): 0 `error CS`, "Exiting batchmode successfully".
- Sim: `run_sim.ps1 -Plan "4:Sparrow,Sparrow,Sparrow" -Tag roles_check -Proj D:\Voyage\wt-d` (time scale 3, one run, log `D:\VoyageLogs\sim_roles_check.log`, report `D:\VoyageLogs
oles_check.jsonl`). No exceptions from the new code in the log (only Vivox/UGS network noise).

| d4 Sparrow x3 | Result | Kills (legs 1-5) | Hull min | Friendly fire |
|---|---|---|---|---|
| Baseline (`D:\VoyageLogsaseline.jsonl`) | **Fallen** (overrun in The Maw) | 44 (1 / 0 / 0 / 8 / 35) | 27 % | n/a |
| Roles (this branch) | **Survived** | 69 (1 / 1 / 0 / 19 / 48) | 36 % | 0 hull plates |

One noisy sample each (ARK_ESCORT.md section 4 shows about 20 points of hull noise per run), but the direction is the intended one: more kills per leg in the hostile worlds (The Swarm 8 -> 19, The Maw 35 -> 48), the hull held above the breach line, and no hull plates lost to the squad's own fire with the lane tests and level-5 Domain-Safe Skybursts in place. Escorts near 3.0 / 2.7 / 3.0 / 2.1 / 1.7 per leg: the squad stays with the Ark.

Commits on `claude/voyage-escort-roles` (pushed): `88e103457` fix(escort) standalone guards and AI hooks, `22c3cac3a` feat(escort) roles, context, registry. Unity's import touched `packages-lock.json`, two materials and `ProjectSettings/EditorBuildSettings.asset` / `VirtualProjectsConfig.json` in the worktree; those are left uncommitted.

## 4. What is left
- Serpent and Scarab roles are first drafts (the brief's advice: evolve them after a human escort with each). Not verified in a sim.
- Squirrel cultivation and Dolphin crystal blasting are built to the brief but their kill effect was not measured separately (only the Sparrow squad was sim-checked).
- The friendly-fire counter counts every non-creature hull loss (no per-destroyer attribution); a destroyer-name breakdown would need `Prism.DestroyerName` matched to the squad's player names.
- The Sparrow's sector split (`sectorDeg`) from the brief is not implemented; seat spreading relies on claims.
- A `SparrowAIBoostPolicy_ArkEscort` asset was not added: the role presses the rocket directly instead of through the boost driver, so no Resources asset is needed.

## Round 2: measuring and hand-tuning the Squirrel and Dolphin

Sims at d3, time scale 3, one run each (`D:\VoyageLogs
un_sim2.ps1`, a copy of `run_sim.ps1` with `-Genes` -> `-escortGenes`). Reports in `D:\VoyageLogs
ound2_*.jsonl`, logs `D:\VoyageLogs\sim_round2_*.log`. "near" = escorts within guardRadius of the Ark, per sample (max 3).

### Batch A (defaults as committed in round 1; Squirrel cultivation ON by default)

| d3 squad | Result | Hull min | Kills (legs 1-5) | near per leg | Friendly fire |
|---|---|---|---|---|---|
| Squirrel x3 | Survived | 24 % | 67 (0 / 1 / 0 / 22 / 44) | 2.6 / 1.1 / 1.3 / 1.9 / 1.9 | 0 |
| Dolphin x3 | Survived | **40 %** | 28 (0 / 0 / - / 4 / 24) | 2.9 / 3.0 / - / 2.6 / 2.3 | 0 |
| Sparrow+Squirrel+Dolphin | Survived | 25 % | 70 (0 / 0 / 0 / 14 / 56) | 2.8 / 2.2 / 2.1 / 1.9 / 2.0 | 3 plates (pilot "Kyoki", hull unknown in this batch) |
| (round 1 reference) Sparrow x3 at d3, old heart-chasing AI | Survived | 30 % | 58 (1 / 4 / 1 / 27 / 25) | - | - |

Reading: the Squirrel joust kills (44 in The Maw, the same order as the Sparrow's guns) and costs no hull; its near count drops to 1.1-1.3 in legs 2-3, which is the cultivation run (two of three Squirrels at the nucleus while the Ark is quiet). The Dolphin squad kept the most hull of any squad so far (40 %) but blasted rarely (4 kills in The Swarm against 14-22 for the others): the crystal filter was too strict (reach 800 x 0.7 from the crystal to the threats, 35 degrees of approach alignment). The mixed squad took the most kills and the only friendly fire.

Changes after batch A: per-seat role counters in the report (`ArkEscortReport.RoleNotes`: rockets fired and held by the lane test, gun seconds, joust commits and seconds, run-up seconds, cultivate seconds and nuclei won, Dolphin crystals chosen/reached/none-available and looks vetoed by the hull); friendly fire attributed by hull as `Vessel(name):count`; Dolphin defaults loosened (`ai.dolphin.reach` 800 -> 1200, `reachUse` 0.7 -> 0.9, `approachAlignDeg` 35 -> 50, `crystalSearchRadius` 1200 -> 1600).

### Batch B (role counters in; Dolphin loosened; Squirrel run with `-escortGenes "ai.squirrel.cultivate=0"`)

| d3 squad | Result | Hull min | Kills (legs 1-5) | near per leg | Friendly fire |
|---|---|---|---|---|---|
| Squirrel x3, cultivation OFF | Survived | 24 % | 76 (0 / 1 / 0 / 19 / 56) | 2.7 / 1.6 / 3.0 / 2.6 / 1.6 | 0 |
| Dolphin x3 (loosened) | Survived | **64 %** | 48 (0 / 0 / - / 10 / 38) | 2.7 / 3.0 / - / 2.4 / 2.4 | 3 plates, Dolphin(Dolphin Bot 3), all in leg 1 (deploy) |
| Sparrow+Squirrel+Dolphin | Survived | 54 % | 55 (0 / 1 / 1 / 13 / 40) | 2.7 / 2.1 / 2.5 / 2.2 / 2.0 | 8 plates, Sparrow(Kyoki), all in leg 2 |

Role notes (per seat, from the report):
- Squirrels: joust 77-93 s (11-31 commits), run-up 27-47 s, boost ring 4-7 taps, station 115-146 s. Cultivation ON (batch A) vs OFF (batch B): same hull (24 %), 67 vs 76 kills; "nuclei won 0" could not be read in batch A (counters landed after it). Cultivation is cheap at d3 and worth keeping on for the farm to judge; its leash extension did not cost hull.
- Dolphins: "crystals chosen 4-5, reached 0, choices with no usable crystal 412-443 (candidates seen 261-471), looks vetoed by the hull 115-161, 107-141 s with threats, 21-33 s steering at a crystal". So the Dolphin spends most of its threat time with NO usable free crystal, and never reached the ones it chose: the cone is still not the Dolphin's kill path in the escort. Its hull result (64 %) comes from staying with the Ark (near 2.4-3.0) and its cycled drift/charge abilities, not from blasting. Open for round 3: why chosen crystals are never reached (the commit loop's LookingAtCrystal needs the course within 25 degrees of the crystal; a crystal 1000 u off may be re-chosen before arrival) and whether the Dolphin needs Mass seeding (crystal supply) more than Space's Clean Blast.
- Sparrow (mixed squad): rockets 4 (2 more held back by the Ark lane), guns 4 s, 141 s on a target. The 8 plates of friendly fire cannot be its 4 Domain-Safe rockets or 4 s of guns: it is the hull itself passing through the plating while hunting grazers (a creature ON the hull puts the heart, and the standoff point on its Ark side, inside the Ark). The Dolphin's 3 plates in leg 1 are the deploy/leash teleport setting a hull down in the plating.

### Friendly-fire fix (after the coordinator's d4 Sparrow x3 on the merged build lost 26 plates)
- `ArkEscortContext.HullRadius` is now the larger of half the authored hull length and the measured reach of the live hull prisms (+8 u), re-measured every 3 s: a modelled hull is wider than the 110 u spindle, and every lane and avoidance test was using the spindle's 55 u.
- `ArkEscortContext.AvoidHull(from, target)`: every seat's steering target passes through it (`ArkEscortPilot.Target`). A path that would cross the hull sphere (+ lane margin) is bent to a waypoint beside the hull; a hull already inside the sphere is sent straight out.
- `SparrowDestroyerRole`: the firing position for a creature on the hull is now beside the creature, tangent to the hull on the Sparrow's own side, instead of inside the plating.
- `ArkEscortRun.BringWingmates`: the teleport lands at least `HullRadius + laneMargin + 60` abeam, never in the plating.
- `ai.dolphin.wakeOffset` 60 -> 150 (range to 300): the idle wake-skim point sits well astern of a modelled hull.
- Merged `origin/claude/voyage-night` (ark-core per-slot `Ark.HullPrismLost(Prism, PrismKind)` kept; `CountFriendlyFire` now also records the destroyer name for `FriendlyFireBy`).

### Friendly-fire check on the merged build (d4 Sparrow x3, `round2_ff`)

Merged `origin/claude/voyage-night` (cell-aware pacing: 852 s under way, 661 s in cells, vs round 1's 272 s dash).

| d4 Sparrow x3 | Result | Hull min | Plates lost | Friendly fire | Kills |
|---|---|---|---|---|---|
| Coordinator's run, merged build before this fix | - | - | - | 26 | - |
| After the hull-avoidance fix | Survived | 6 % (Scarab Reef) | 159 | **8** (Sparrow Bot 2: 6, Sparrow Bot 1: 2; 6 in leg 2, 2 in leg 5) | 287 (90 on the Ark) |

Role notes exposed the real Sparrow failure: "rockets 0-1 (held by the Ark lane 0-1), guns 0-2 s, 342 s on a target" for all three seats. The guns and the rocket leave along the nose and the gates need the heart within 8 / 5 degrees of it, but the pilot was steering at the STANDOFF point beside the creature, so the nose was almost never on the heart: the Sparrow's weapons were silent and its kills came from elsewhere (the merged build's hull-plate eating and the cycled ModeSwitchingFire). Fix: inside gun range the Sparrow flies the pass at the lead point itself (`SparrowDestroyerRole.Steer`), and the aim gates widen (`ai.sparrow.gunAimDeg` 8 -> 12, range to 25; `ai.sparrow.rocketAimDeg` 5 -> 8, range to 20). The remaining 8 plates are hull contact around grazers at the plating; AvoidHull bends the path but a pass at a heart on the hull still brushes it. Candidates for round 3: refuse a pass at a heart inside HullRadius + margin (fire from the tangent point only) and a larger `arkLaneMargin`.

### Verification of the pass fix: NOT RUN (queued behind another agent's sim)

The d4 Sparrow x3 check of the pass fix (`round2_pass`) was launched once the wt-a sim had exited, but the editor hung at startup (log stopped at 6.6 KB, `D:\VoyageLogs\sim_round2_pass_hung.log`) while another agent's sim started on the same account; the relaunch was skipped because a sim was running again. To verify:

```
powershell -File D:\VoyageLogs
un_sim2.ps1 -Plan "4:Sparrow,Sparrow,Sparrow" -Tag round2_pass -Proj D:\Voyage\wt-d -Out D:\VoyageLogs
ound2_pass.jsonl
```
(only with no other `ArkEscortSimLauncher` Unity.exe running; `run_sim2.ps1` also takes `-Genes "key=value;..."`). Read `RoleNotes` in the JSON line: the Sparrow seats should now show tens of rockets and tens of seconds of guns, and `FriendlyFireBy` should stay in single digits or lower.

### Round 2 commits (branch `claude/voyage-escort-roles`, merged onto `origin/claude/voyage-night`, pushed)
- `c48f6919a` friendly-fire attribution by destroyer; `-escortGenes` command line
- `1aa57d5a3` per-seat role counters (`RoleNotes`); Dolphin crystal choice loosened
- `8977ec1fe` merge of `origin/claude/voyage-night` (ark-core `Ark.HullPrismLost(Prism, PrismKind)` kept)
- `5ecb8a371` hull avoidance: measured `HullRadius`, `AvoidHull` on every seat's target, Sparrow tangent standoff, wingmate teleport abeam of the hull, `wakeOffset` 150
- `e899ab0e4` the Sparrow flies its pass inside gun range; `gunAimDeg` 12, `rocketAimDeg` 8

### Gene defaults changed in round 2
| Key | Was | Now |
|---|---|---|
| ai.dolphin.reach | 800 | 1200 |
| ai.dolphin.reachUse | 0.7 | 0.9 |
| ai.dolphin.approachAlignDeg | 35 | 50 |
| ai.dolphin.crystalSearchRadius | 1200 | 1600 |
| ai.dolphin.wakeOffset | 60 (20-200) | 150 (20-300) |
| ai.sparrow.gunAimDeg | 8 (2-20) | 12 (2-25) |
| ai.sparrow.rocketAimDeg | 5 (1-15) | 8 (1-20) |

### Round 3 candidates
- Dolphin: chosen crystals are never reached (`reached 0`); check the commit loop against a far crystal, and whether the Dolphin should seat Mass (crystal seeding) instead of Space.
- Sparrow: refuse a pass at a heart inside `HullRadius + margin` (tangent firing only) if friendly fire persists; measure rockets/guns per leg.
- Squirrel: cultivation's effect on `PacifiedLegs` (the merged report has it) with the counters now in.

## Round 3: Sparrow verification, the Dolphin cone

### Launching sims on wt-d after the merge
Two GUI launches of the d4 Sparrow x3 verification hung at startup (log stops after the licensing block, before "Refreshing native plugins"; `sim_round3_sparrow_hung.log`, `sim_round2_pass_hung.log`), with no other Unity instance holding wt-d. A `-batchmode` launch (`D:\VoyageLogs
un_sim3.ps1`) gets through startup and into the menu, but the sim then reports "never ready - giving up" after 600 s: the freestyle hand-off needs the GUI editor. So the sim must run in the GUI editor, and the GUI hang at the licensing step (a modal nobody can see; same Unity account across agents) is the open operational problem for every wt-d sim.

### Dolphin: why the cone never fired (read from the code, confirmed by the counters)
1. The hull veto in round 2 tested the segment from the crystal toward the THREAT CENTROID, extended by the whole reach. With 150 grazers on the plating the centroid is the Ark's own position, so every crystal failed the veto: identical "no usable crystal 412 (candidates 261)" on all three seats, because the test did not depend on the seat.
2. "reached 0" was a counter bug: a collected crystal is relocated by `CrystalManager.RespawnCrystal` (`Crystal.MoveToNewPos`), not deactivated, so `Usable` never went false.
3. The blast itself (`VesselExplosionByCrystalEffectSO.Execute`) has no energy gate: every free-crystal touch fires a cone sized by the resource at `_resourceIndex`, and at Space 5 (`_spaceUpgradeSparesAllies`) it spares the squad's own domain, hull included - so for the sim's Space-5 Dolphins the hull veto was never needed at all.

Fix (`DolphinRole`): claim a threat like every seat and aim (drift-look) at ITS heart; choose the crystal by the crystal-to-heart line (reach x reachUse from the crystal to the heart); veto the hull only when Clean Blast is not active, and only on the crystal-to-heart segment; count pickups by the crystal's position jump; count blasts through a new static `VesselExplosionByCrystalEffectSO.Fired(status, resourceIndex)` event (energy at the blast recorded). Notes line now reads "blasts N (mean energy E), crystals chosen, picked up, ..., clean blast on/OFF".

### Round 3 sims: NOT COMPLETED (wt-d GUI editor hangs)
The combined GUI run (`4:Sparrow,Sparrow,Sparrow;3:Dolphin,Dolphin,Dolphin;3:Sparrow,Squirrel,Dolphin`, lock-aware `run_sim2.ps1`) got through startup this time (the lock held, no other editor up) but stalled after the launcher opened Bootstrap, before play mode: `sim_round3.log` stops at 05:15 with no `[ArkEscortSim]` line, and its licensing block shows "Failed to handshake to channel LicenseClient-gradi", "Access token is unavailable", "Code 404 ... 0 entitlement groups". Together with the two earlier hangs (same account used by other agents' editors and the wt-c batchmode build at the same time), the working theory is a licensing/sign-in modal in the GUI editor that no script can answer; wt-a's editor ran the same merged code fine between those windows. Killed the editor so the lock is released.

So, for round 3: the Dolphin fix and the Sparrow aim fix are committed, compiled and pushed but **unverified by sim**. To verify, with no other Unity.exe (sim OR batch build) on the account:
```
powershell -File D:\VoyageLogs
un_sim2.ps1 -Plan "4:Sparrow,Sparrow,Sparrow;3:Dolphin,Dolphin,Dolphin;3:Sparrow,Squirrel,Dolphin" -Tag round3 -Proj D:\Voyage\wt-d -Out D:\VoyageLogs
ound3.jsonl
```
Expect in `RoleNotes`: Sparrow seats with tens of rockets and tens of seconds of guns (was 0-1 / 0-2 s), `FriendlyFireBy` single digits; Dolphin seats with "blasts N > 0 (mean energy > 0)", "picked up > 0", "no usable crystal" well below the ~400 of round 2, "clean blast on".

### Round 3 commits (pushed, on top of the voyage-night merge)
- `f129ed594` fix(escort): the Dolphin blasts the creature it claims, not the Ark-shaped centroid (+ `VesselExplosionByCrystalEffectSO.Fired` event)
- `D:\VoyageLogs
un_sim3.ps1`: a `-batchmode` variant of run_sim2 - startup works but the sim never becomes ready (needs the GUI), kept only as a record.

### Round 3 sims (merged `voyage-night` incl. pacify-tune; `round3.jsonl`, `sim_round3.log`)

Launched by `D:\VoyageLogsun_sim_gated.ps1` (waits for other GUI editors, then `run_sim2.ps1` with the sim lock). The GUI editor started cleanly this time (the plugin refresh took 18 s under three other Unity instances) but needed 30 min from launch to play mode under that load.

| Run | Result | Hull min | Plates lost | Friendly fire | Kills | RoleNotes |
|---|---|---|---|---|---|---|
| d4 Sparrow x3 (pass fix) | Survived | 5 % | 329 | **26** (Kyoki 14, Bot 1 12; 22 of them in The Boneyard) | 305 | rockets 6-7 per seat (13-17 more HELD by the Ark lane), guns 15-20 s of 520 s on a target |
| d3 Dolphin x3 (claimed-threat aim) | Survived after leg 2 (route ended; 1 world pacified) | 87 % | 35 | 0 | 0 | blasts 1 / 0 / 1 (mean energy 0.12), crystals chosen 3, no usable crystal 673-761 (candidates 186-266), looks vetoed 0, clean blast on |
| d3 Sparrow+Squirrel+Dolphin | did not sail | - | - | - | - | `MissingReferenceException` in `AIPilot.UpdateCellContent` (line 646) every frame: a destroyed Crystal in the torn-down world's `CellItems` |

Readings:
- Sparrow: the pass fix made the weapons fire (0-1 rockets / 0-2 s guns -> 6-7 / 15-20 s) but (a) 13-17 rockets per seat were still withheld by the lane test, and (b) flying the pass at a heart ON the plating rammed the hull (friendly fire 8 -> 26). Guns can never be fired at a hull-grazer without hitting the hull (projectiles take own prisms); the Domain-Safe Skyburst (Charge 5) can.
- Dolphin: the veto bug is gone (0 vetoed looks, clean blast on) and the role now aims at its claimed creature, but the escort's worlds offer almost no free crystals near the fight, and the Dolphins seated in Space get no Crystal Seeding (the Mass passive). The 2 blasts fired at energy 0.12: no crystals and little banked energy.
- The mixed run's failure is a sim-harness bug, not a role bug: the local seat's pilot kept a dead world's cell data; `UpdateCellContent` threw before steering.

Fixes applied after these runs (compiled, committed, pushed; NOT yet sim-verified):
- `SparrowDestroyerRole`: rockets ignore the Ark lane when Domain-Safe Skybursts (Charge 5) are active; no pass at a heart inside `HullRadius + laneMargin + 20` (fire from the tangent point); RoleNotes say "domain-safe on/OFF".
- `ArkEscortRoles.DefaultElement(Dolphin)`: Space -> **Mass** (Crystal Seeding supplies team crystals; Clean Blast is off, so the role's segment hull veto is live); `ai.dolphin.crystalSearchRadius` 1600 -> 2500 (range to 4000).
- `AIPilot.UpdateCellContent` skips destroyed items; `ArkEscortPilot.Disarm` always restores the home cell data (null included).

Next verification (the same plan, same command as above): expect Sparrow friendly fire back in single digits with rockets in the tens per seat; Dolphin "picked up > 0, blasts > 0" if Mass seeding supplies crystals - if not, the owner's intent needs a crystal source in escort worlds (seeding on the Ark, or the Squirrel's trail is not crystals).
