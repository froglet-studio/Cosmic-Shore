# Vessel kits vs. the Ark-escort AI (research brief)

Repo `D:\Voyage\wt-c`, read-only survey, 2026-10-31. Paths are relative to `Assets/_Scripts/` unless they start
with `Assets/` or `Docs/`. Nothing here has been run.

## 0. Five findings that change the plan

1. **The hull joust works for the Squirrel only.** `ArkEscortPilot.Target()` (Controller/Voyages/ArkEscortPilot.cs:55-61)
   steers every seat through a creature's heart on the assumption that "a faster vessel through the heart jousts it".
   A heart hit only runs `VesselLifeformCrystalEffects` (Controller/ImpactEffects/Impactors/VesselImpactor.cs:188-203).
   Of the vessel containers in `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/`, only
   `SquirrelImpactorDataContainer` lists `SquirrelVesselWitherLifeformByCrystalEffect` (Manta has a plant-bomb joust).
   Sparrow, Dolphin, Serpent and Rhino author no such list, and the Scarab's is `[]`. So a Sparrow, Dolphin, Serpent or
   Scarab flying through a heart does nothing, and its pass-5 kills came from its weapons going off on timers while
   it pointed at the creature. The other kill paths are the Sparrow missile warhead (`MissileWarheadWitherLifeformEffect`),
   the Scarab plate (`ScarabCavitationWitherLifeformEffect`, both `ExplosionWitherLifeformByCrystalEffectSO`), and
   gunfire or blasts eating the creatures' bodies (`HealthPrism`s).
2. **The AI's crystal list is never pointed at the escort's worlds.** `AIPilot.RetargetCell` (AI/AIPilot.cs:554) is
   only called by `ModePreviewSession`. The escort's worlds each have their own `cell.RuntimeData`
   (ArkEscortRun.cs:189), so `UpdateCellContent` (AIPilot.cs:564) most likely reads the cell the pilot spawned in.
   Everything that seeks crystals (the Dolphin's commit loop, crystal pickups, the Serpent's wall shielding) is
   probably blind in the escort. Verify by logging `pilot.CellData` against `_corridor.CurrentCell.RuntimeData`.
3. **The leash cancels "zoom ahead to the nucleus".** `BringWingmates` (ArkEscortRun.cs:439-457) teleports any wingmate
   more than `min(WingmateLeash 1600, 900 x 1.4) = 1260 u` from the Ark (line 444) back to its flank, every 2 s.
4. **Friendly fire on the Ark.** The sim seats `Element.None` (ArkEscortSim.cs:77), and `SquadDeployment.SeedElement`
   returns early for None (System/Squads/SquadDeployment.cs:127). So every L5 upgrade is off: the Sparrow's Domain-Safe
   Skybursts (Charge 5), the Dolphin's Clean Blast (Space 5) and the Serpent's Shield Steal. At rest, the Dolphin's cone
   and the Sparrow's rocket blast destroy own-domain prisms, and the Ark's hull is own-domain prism mass (Environment/Ark.cs
   header). `ProjectileDamagePrismEffectSO` has no domain test, and `ProjectileImpactor` filters only vessels (line 50),
   so the guns are likely to chew the hull too. A blind-cycled weapon (AIPilot.cs:1516-1528) pointed through the Ark
   costs hull.
5. **The Falcon has no registered prefab.** This is the Grizzly+Urchin+Falcon bug; see section 5.

## 1. Eligibility (a complete ability matrix plus real playtesting)

The matrix comes from Docs/ElementalAbilitySystem/FLEET_MAPS.md section 1 (abilities / scaling / L5 gates) and FLEET_GAPS.md. The flight
record comes from Docs/AI_SYSTEM/VESSEL_MODE_CHART.md section 3 (229 stored flights).

| Hull | Matrix | Hand-flown races (finished) | AI-rival races | Verdict |
|---|---|---|---|---|
| **Squirrel** | 4/4/4, re-cut 09-24 and stable | 79 (33) over Regatta, SkimRace, Scurry and Joust, 6 pilots | 97 | **Eligible, the best evidenced** |
| **Dolphin** | 4/4/4 | 20 (16), Switchback at I1-I4, 3 pilots | 91 | **Eligible** |
| **Sparrow** | 4/4/4 (the Space row's number is a declared HOLE since 09-28) | 21 (10), Breakwater at I1-I4 | 34 | **Eligible** |
| Manta | 4/4/3 (Time L5 deliberately empty) | 22 (9) | 66 | Eligible |
| Stoat | 4/4/4 | 22 (17), Warpline | 43 | Eligible |
| Urchin, Rhino | 4/4/4 | 8 (3), 10 (2) | 28, 18 | Eligible but thin |
| **Serpent** | 4/4/3 per the table. The Time L5 "Fifth Pellet" shipped 10-30 per FLEET_MAPS, the Charge row was re-cut 10-30, and BACKLOG 5.2 says "still needs a playtest" | 2 (1), Regatta only | 15 | **Not yet**: the matrix is complete but recent, and there is almost no playtesting |
| **Scarab** | 4/4/4 after the **10-29 re-cut** (2 days old) | 7 (0) over Regatta, ScarabScramble and WreckingBall | 7 | **Not yet**: 0 finished hand races, and the kit changed this week |
| Grizzly | 4/3/3 (the only `--gaps` vessel) | 8 (0) | 18 | No |
| Falcon, Shrike, Termite | no prefab in the container | - | - | No |

The owner's five: Squirrel, Dolphin and Sparrow qualify now. Serpent and Scarab are worth building but should be
evolved after a human escort or two, because their numbers will move under them.

## 2. Per-vessel kit, today's escort AI, gaps, and the fix

Shared AI facts (AI/AIPilot.cs):
- Steering: `SetExternalTargetProvider` (292) overrides everything, every frame (Update, 965-970).
- `SetDriftLookTargetProvider` (441) aims the nose during a commit drift (the Dolphin's cone; `ResolveDriftLookDirection` 668).
- The abilities in the prefab's `abilities` list are blind-cycled: start, wait Duration, stop, wait Cooldown, after a 3 s
  warm-up (1516-1528). A boost driver that `Drives` an ability takes it off that cycle (StartAIPilot, 862-871).
- The boost policy is resolved once, in `Initialize` (822-831, `AIBoostPolicySO.ForMode`). The escort runs in
  freestyle, so no `_ArkEscort` variant is ever picked up.
- Triggers that are inert under autopilot get pulled from mode code, the way Broadside does
  (Arcade/Broadside/BroadsideController.cs:280-320): `ScarabJukeController.TryAutopilotDash` (Vessel/ScarabJukeController.cs:419)
  and `R_VesselActionHandler.PerformShipControllerActionsReplicated(input)`, with the input found by
  `TryGetInputForAction<TSO>`.

**Shared change (do first): `ArkEscortPilot` becomes a dispatcher.** Keep `Arm`/`Disarm`. In `Arm`, resolve
`IArkEscortRole` by `VesselStatus.VesselType`: `SparrowDestroyerRole`, `SquirrelCultivatorRole`, `DolphinBlasterRole`,
`SerpentWallRole`, `ScarabRingRole`, with `HeartChaserRole` (today's `Target()`) as the fallback. Each role gets
`Target()`, an optional `DriftLook()`, and a `Tick(ArkEscortContext)` for its triggers, which runs in `ArkEscortPilot.Update`.
`ArkEscortContext` is built once per frame by `ArkEscortRun`. It carries the Ark, the threat list (sorted by a threat
score, not by distance), the current cell, `cell.IsInsideNucleus`, `cell.NucleusWorldRadius`, `cell.DominantDomain`,
`cell.GetNucleusDomainVolume` (Environment/Cell.cs:504-536, 1018), and the escort's genome.
- Threat score (new `ArkEscortRun.RankThreats`, replacing the sort in `CountThreats` at 392-416): distance from the
  hull, closing speed (from the position delta between 0.5 s samples), and whether the creature sits within graze reach
  of the hull.
- Seat assignment with hysteresis: keep the target unless a new one beats it by `retargetMargin`. This fixes all seats
  collapsing onto `_threats[count-1]` when there are fewer threats than seats (ArkEscortPilot.cs:53).
- `ArkEscortRun.TrackLeg` (314-327): on a leg change, call `pilot.RetargetCell(cell.RuntimeData)` for every AI seat
  (finding 2).
- `BringWingmates` (439-457): a per-role leash. A role exposes `LeashOverride` (the Squirrel's nucleus run returns the
  distance to the cell's centre plus a margin). This needs `_pilots` indexed alongside `_wingmates`.
- **Ark no-fire cone** (new static `ArkEscortSafety.LaneClear(from, dir, range, ark, hullRadius)`): every role gates
  its weapon on a segment-vs-hull-sphere test (finding 4).
- `AIPilot.SetBoostPolicyOverride(AIBoostPolicySO)` (new): rebuild `_boostDriver` the way `Initialize` 822-831 does,
  then restart the cycler (StartAIPilot). This is how a role takes a weapon off the blind timer.
- `AIPilot.SetAbilityCycleSuppressed(ShipActionSO, bool)` (new): `StartAIPilot` already skips `_commitBoundActions`
  and driver-driven actions (864-869). Add a third skip set so a role can own an ability's timing.

### 2.1 Sparrow (destroyer), the first squad: three Sparrows
**Kit** (FLEET_MAPS section 2, Sparrow; `R_VesselActions/SPARROW_*.md`):
- Pulsefire guns (`FullAutoActionSO`): fixed 1350 u/s and about 258 u range. They destroy prisms (fauna bodies, hostile
  trail, nucleus mass) and pay the rocket rack, 0.01 per prism destroyed by **gunfire** (25 prisms per base rocket).
- Skyburst rocket (`FireGunActionSO` `SkyBurstGunAction`): a proximity fuze at 20x hit radius (76 u) trips on a
  **living fauna heart** or a rival vessel, and the warhead (95 u) **jousts creatures** without touching mass. A
  second, destructive blast (radius 100-170 by Charge) takes prisms. At Charge 5 it spares own prisms.
- Turret stance (Mass): anchors prism rounds as own mass.
- Afterburner (Time, `BoostAction`): unlimited hold. The strafing roll is base kit.
- Single-stick, so `XDiff` throttle is not written.

**Prefab AI today** (Sparrow.prefab line 1316 onward): the cycler runs SkyBurst 2 s on / 5 s off, FullAuto 3 / 0.8,
Boost 2 / 10 and ModeSwitchingFire 2 / 10. `SparrowAIBoostPolicy` is a `HoldBoostAIPolicySO` with
`clearWayRocket: {fileID: 0}`, so the rockets stay on the blind timer.

**In the escort today:** flies at the heart (no joust), and guns and rockets fire on their timers along the nose.
That is why it kills a little. It ignores the Ark's line of fire and never goes for nucleus mass.

**Missing:** aimed rockets, gun discipline (fire only with a target on the nose and the Ark out of the lane), a
standoff (rocket min range is 60 u, and passing through the creature is pointless), and a nucleus job.

**Change: `SparrowDestroyerRole`.**
- (a) Target is a lead-pursuit point on the threat's heart, offset back to the `standoff` distance on the
  creature-to-Ark side, so the Sparrow sits between the creature and the hull.
- (b) Add an `Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_ArkEscort.asset` (a `HoldBoostAIPolicySO`) with
  `clearWayRocket = SkyBurstGunAction`, applied through `SetBoostPolicyOverride`. `TickRocket` (AI/Boost/HoldBoostAIPolicySO.cs:110-121)
  already fires on range [60, 220] and 4 degrees of alignment. Add a `RocketVeto` delegate on the driver that calls
  `ArkEscortSafety.LaneClear` (the rocket's 100-170 u blast must not reach the hull).
- (c) Take FullAuto off the cycler (`SetAbilityCycleSuppressed`) and `Tick` it: press while the angle to target is
  under `gunAimDeg`, the distance is under `gunRange`, and the lane is clear.
- (d) Nucleus sweep: when `threats == 0` and `cell.DominantDomain` is hostile, fly the nucleus and shoot the hostile
  mass inside it. Gunfire also restocks rockets.
- (e) Two-Sparrow and three-Sparrow squads: seats split by bearing sector around the Ark (`sectorDeg`), not by
  nearest-N.

### 2.2 Squirrel (cultivator)
**Kit** (FLEET_MAPS, the Squirrel re-cut of 2026-09-24):
- Crystal Joust (Charge, passive). The hull through an opposing creature's heart withers it when
  `status.Speed >= creature.CurrentSpeed + speedMargin (1)` (ImpactEffects/.../VesselWitherLifeformByCrystalEffectSO.cs:60).
  An own-domain heart is nourished, but only with the Charge L5 Shepherd.
- Boost Ring (Mass, RT, `SquirrelTubeActionSO`): 1 ring, 20 s cooldown (x0.5 at Mass 10, floor 0.35); L5 Twin Rings.
- Steal (Space, passive): the skimmer sphere flips opposing prisms (15 to 30 u reach).
- Skimming (Time, passive): energy per skimmed prism, and danger prisms pay 10x with L5 Live Wire.
- Drift lays shielded trail.

**Prefab AI:** `abilities: []`, `skillLevel 10`, and a `SkimRingAIPolicySO` boost policy. Fully featured AI lives in
`SkimRacePilot` and `RegattaRingObjective`, but not in the escort.

**In the escort today:** the only hull for which "fly at the heart" actually kills, but only when it is faster than the
creature. Nothing makes sure it is (no run-up and no skim boost before contact).

**Missing:** a speed check before committing to the joust, the nucleus run, territory-taking, and the leash exemption.

**Change: `SquirrelCultivatorRole`, a two-mode state machine.**
- **Joust:** commit to a threat only if `Speed - target.CurrentSpeed >= joustMargin` and the turn needed to reach it is
  under `joustMaxTurnDeg`. Otherwise run a skim lap along own or Ark-adjacent trail (the closest existing helper is
  `SkimRaceDriver`, but a simple "nearest prism lane ahead" target will do) until fast enough.
- **Cultivate:** when the Ark is inside a cell that is not yet owned (or `threats <= cultivateWhenThreatsBelow`), fly to
  the next or current cell's nucleus. Weave inside `NucleusWorldRadius` (a golden-angle spiral at
  `weaveRadiusFrac x radius`), laying trail (own environment volume inside the nucleus is the **only** input to
  `DominantDomain`, Cell.cs:324-331 and 504-526) and stealing hostile prisms with the skimmer. Once
  `GetNucleusDomainVolume(own) > hostile x pacifyMargin`, the cell's fauna spawn in our colour
  (`CellControlRules.ControllingDomain`, Data/Enums/InitialControllingDomain.cs:51), and `CountThreats` already ignores
  own-domain fauna. Return to the Ark.
- Requires the leash override (finding 3).
- Pressing the Boost Ring (find it with `TryGetInputForAction<SquirrelTubeActionSO>`) on a straight run-up to a joust
  is an optional gene.

### 2.3 Dolphin (blaster)
**Kit** (FLEET_MAPS, Dolphin; `DOLPHIN_ENERGY_ECONOMY.md`, `DOLPHIN_CRYSTAL_SEEDING.md`):
- Skimming banks energy, and touching a **free** crystal (omni or team) at energy spends it all on a cone.
- The cone is a fan from 4.8 to 23.4 degrees by energy, with an axial reach of `2400 x size` (x2 at Space 10), fired
  along the nose. Below Space 5 it is **indiscriminate** (it destroys own prisms).
- Crystal Seeding (Mass, passive): a team or omni crystal in the cell's cytoplasm about every 30 s.
- Echo Sight (RT); the Charge Boost drift charge (Time).

**Prefab AI:** `drift: 1` (the commit loop, AIPilot.cs:1011-1060). It cycles DolphinDrift, DriftTrail and ChargeBoost
2 s / 2 s, under `ChargeBoostAIPolicySO`.

**In the escort today:** the external target overrides crystal seeking (the trap BendsController.cs:54-61 records),
so it commits drifts at fauna hearts. A heart is embedded, which is a joust surface and not a pickup, so **no blast
ever fires**. Finding 2 compounds it.

**Change: `DolphinBlasterRole`.**
- Do **not** set `ExternalTargetProvider` while charged. Choose the free crystal C (from `cell.RuntimeData.CellItems`,
  the same filter as AIPilot.cs:584-600) that minimises the angle between the approach direction (C minus self) and the
  line from C to the threat cluster's centroid, subject to `|C - threat| < reach x reachUse`. Steer to C via the external
  provider only for that leg.
- Install `SetDriftLookTargetProvider` to return the threat centroid. This is the Bends pattern
  (BendsController.cs:351-363) with fauna in place of rivals.
- Veto: if the Ark's hull falls inside the cone's capsule, using `BlastVolume.Contains` (named in FLEET_MAPS Pilot Echo),
  the look provider returns null and the role picks another crystal.
- With no charge or no crystal, skim the Ark's wake to bank energy (`skimEnergyGoal`).

### 2.4 Serpent (guardian, walls)
**Kit** (FLEET_MAPS, Serpent; `SERPENT_SEED_WALL.md`):
- Stopped stance (B, `ToggleTranslationModeActionSO`): the Serpent is the seed and grows a wall while it stays
  stopped. It is debuff-immune while stopped.
- Cloak (RT unscoped, `CloakSeedWallActionSO`, 15 s cooldown): leaves a super-shielded seed that grows a wall until
  the seed's super-shield breaks.
- The wall is a herringbone (15 u pitch at rest, brick size x2 at Mass 10) **perpendicular to the Serpent's heading**
  (its trail runs back along the wall's normal). It grows from its edge by stealing loose prisms within
  `stealRadius 40 x Mass x Space(1 to 2)`.
- Every crystal the Serpent collects ripples a shield through its active walls.
- Sniper (RT while scoped): destroys every hostile prism in a 1.5-degree cone, super-shielded included; recovery
  12 s to 5.4 s.
- Pellets (A).

**Prefab AI:** cycles ConsumeBoost (0 / 8.1) and CloakSeedWall (0 / 4.3). That means it cloaks wherever it happens to
be, every 4.3 s plus cooldown.

**Danger:** `SerpentWallAssembler.IsRecruitable` (Controller/Assemblers/SerpentWallAssembler.cs:365-377) takes any
**own-domain, non-super-shielded** prism. A wall seeded within its steal radius of the Ark will **strip the hull**.

**Change: `SerpentWallRole`.**
- Pick a wall point `W = arkPos + dirToThreatCluster x wallStandoff` (wallStandoff > hull radius + steal radius x 2 + margin).
- Approach W flying **along the line from the threat to the Ark** (so the wall faces the threat), then press cloak via
  `PerformShipControllerActionsReplicated`. Take `CloakSeedWallAction` off the cycler.
- Option, by gene: at W stop with the stance and hold for `holdSeconds` while threats close.
- Then collect crystals to shield the walls.
- Add an Ark guard in `IsRecruitable` (`if (p.GetComponentInParent<Ark>()) return false;`, or a flag stamped on hull
  lays). This is needed for any Serpent near an Ark, AI or human.
- Leave the sniper at its default for now; later, use it to clear hostile mass in the nucleus.

### 2.5 Scarab (guardian, goal rings)
**Kit** (`SCARAB.md` section 2 and the 10-29 re-cut):
- Throttle on analog RT; drift on LT; the juke dash on the right stick.
- Shove plate (Charge): debuffs, and **jousts creatures through a heart** (`ScarabCavitationWitherLifeformEffect`,
  SCARAB.md section 3.10).
- Switch (A, `PlaceSwitchActionSO`): one ring, 150 u ahead along the course. Ring radius 24 x Mass(1 to 2.5).
  1 charge with a **60 s recharge**, at most 1 standing (`Data Containers/PlaceSwitchActionSO.cs:88,104,110,116,130`).
  A ball threading it pays energy plus a prism dais (a super-shielded sun core at Mass 5).
- Nucleus Seeding (Space): balls in the membrane.
- Ball Forge (omni crystal): a crystal at full energy becomes a ball.

**Prefab AI:** `abilities: []`, `ScarabAIBoostPolicy` (dash cooldown 2 s).

**In the escort today:** chases hearts and does nothing on contact. It never dashes and never places a ring.

**Change: `ScarabRingRole`.**
- (a) Plate: `Tick` calls `TryAutopilotDash(toHeart)` when the heart is within `plateRange`, as in
  BroadsideController.cs:313-317. This alone makes it a fauna killer.
- (b) Rings: see section 4 for placement. Fly to the chosen site, and when `IsLinedUpOnSite` (a copy of
  `TollwayController.IsLinedUpOnFreeAnchor`, 544-565) passes, press A (as in Tollway 490-530).
- (c) Purpose: a ring's dais is fresh own-domain mass beside the Ark's route (decoy and graze bait). A super-shielded
  sun core is armour fauna cannot eat. Put rings on the threat-to-Ark line at `ringStandoff`.

## 3. Evolvable parameter vectors

Register each gene through `GeneRegistry.Register("ArkEscort.<Vessel>", new GeneSpec(name, min, max, default))`
(Utility/AITraining/Core/GeneRegistry.cs:33). The genome lives on a new `ArkEscortGenomeSO` (one per vessel,
`Assets/Resources/ArkEscort/<Vessel>Genome.asset`), read by the role. The sim passes a genome path with a new
`-arkEscortGenome` argument (ArkEscortSim.cs, parsed beside `-arkEscortSim`).

Suggested fitness, from `ArkEscortReport`: `HullMinFraction`, legs reached, kills per leg, `ThreatSeconds`,
`EscortNear/EscortSamples`, and a penalty for own-hull damage. Add an `ArkFriendlyFire` counter by listening on the
prism-destroyed channel, filtered to Ark prisms with an own-domain destroyer. Use several seeds per genome; the
doc's section 4 shows 20 points of noise per run.

Common to all five (they plug into `ArkEscortPilot` and `ArkEscortRun`):

| Gene | Range | Plugs into |
|---|---|---|
| guardRadius | 400-1400 | `ArkEscortPilot.GuardRadius` (15), threat gathering |
| retargetSeconds | 0.2-2.0 | `RetargetSeconds` (17) |
| retargetMargin | 0-0.5 | new hysteresis in `RankThreats` |
| threatW_dist / threatW_closing | 0-1 / 0-1 | the threat score |
| stationRadius | 120-600 | `StationRadius` (18), the idle station |
| leashFactor | 1.0-3.0 | `EscortLeashFactor` (ArkEscortRun.cs:32), per role |
| arkLaneMargin | 10-120 | `ArkEscortSafety.LaneClear` hull padding |

There are 8 common genes, counting the two threat weights as one. Each vessel takes **"the common 6"**
(guardRadius, retargetMargin, threatW_closing, stationRadius, leashFactor, arkLaneMargin) and its own genes below.
That gives 11-16 genes per vessel. Freeze `retargetSeconds` and `threatW_dist` at their defaults first; for the
Sparrow, drop `leadSeconds` too to stay at 15.

Per vessel, as `gene [range] -> where it plugs in`:

- **Sparrow** (the common 6, plus 10 more):
  - `standoff [60-300]` -> lead-point offset; `leadSeconds [0-1.5]` -> lead pursuit.
  - `rocketMinRange [40-120]`, `rocketMaxRange [120-320]`, `rocketAimDeg [1-15]` -> `HoldBoostAIPolicySO` 38-42
    (the _ArkEscort variant).
  - `gunAimDeg [2-20]`, `gunRange [100-260]` -> the FullAuto `Tick` gate.
  - `sectorDeg [60-180]` -> seat bearing split; `nucleusSweepWhenThreatsBelow [0-4]` -> the (d) trigger.
  - Boost `alignDegrees [5-30]` -> `AIBoostPolicySO`.
- **Squirrel** (the common 6, plus 8 more):
  - `joustMargin [1-60 u/s]` -> the joust gate (the asset's `speedMargin` is 1); `joustMaxTurnDeg [15-120]`.
  - `skimEnergyGoal [0-1]` -> run-up exit; `cultivateWhenThreatsBelow [0-6]` -> mode switch.
  - `pacifyMargin [1.0-2.0]` -> own/hostile nucleus volume; `weaveRadiusFrac [0.2-0.9]` -> nucleus spiral.
  - `nucleusLeashExtra [0-1500]` -> `LeashOverride`; `ringOnRunup [0/1]` -> the tube press.
- **Dolphin** (the common 6, plus 5 more):
  - `reachUse [0.1-1.0]` -> crystal filter; `approachAlignDeg [5-60]` -> crystal choice.
  - `minEnergyToBlast [0.2-1.0]` (against `spendAtCharge01` 0.95); `coneArkClearance [0-300]` -> cone veto.
  - `skimWakeOffset [20-200]` -> energy banking; `ChargeBoostAIPolicySO.maxHoldOffSeconds [1-8]`.
- **Serpent** (the common 6, plus 5 more):
  - `wallStandoff [150-600]` -> W; `wallLateralSpread [0-200]` -> multi-wall offset.
  - `holdSeconds [0-12]` -> stance hold; `useStance [0/1]` -> cloak or stance.
  - `crystalDetourMax [0-600]` -> crystal shielding runs.
- **Scarab** (the common 6, plus 6 more):
  - `plateRange [60-260]` -> `TryAutopilotDash` gate (Broadside ships 160); `ringStandoff [100-500]` -> site scoring.
  - `siteMaxAge [1-20 s]`, `siteReach [30-150]` -> the section 4 resolver.
  - `ringAimTolerance [30-120]` -> `IsLinedUpOnSite`; `dashCooldownSeconds [1-4]` -> `ScarabAIBoostPolicy`.

## 4. Code facts that were asked for

### Squirrel trail prism spacing vs speed (Vessel/VesselPrismController.cs)
- The spawn loop (318-343) lays one pair per iteration, then waits `delay = clamp(wavelength x warp / Speed, 0, 3)` s
  (`ApplyBoostSpawnDelay` is not overridden for the Squirrel). Along-track spacing is therefore **constant at
  `wavelength`**, independent of speed, for every speed from `wavelength/3` up.
  - Below `wavelength/3` u/s the 3 s clamp makes spacing `3 x Speed`.
  - Below 3 u/s nothing is laid (the gate at 323). Nothing is laid while the vessel is attached, or for 2 s after spawn
    (`startDelay`).
- The wait is a `UniTask.Delay`, which is at least one frame, so spacing is really `max(wavelength, Speed x frameDt)`.
  The delay uses scaled time, so in the **sim at time scale 3** a frame advances `3/fps` game-seconds. At 60 fps the
  trail opens gaps above about **140 u/s** (7 / 0.05). Live at 1x, the same happens above about 420 u/s.
  Escort-sim trail density for a boosted Squirrel is therefore lower than in play.
- Squirrel values (Assets/_Prefabs/Spacevessels/Squirrel.prefab 950-963):
  - `initialWavelength 7`, `minWavelength 5`, `Gap 18.5`: two rails, each laid at plus or minus 9.25 u (`Gap x 0.5`).
  - `BaseScale (20, 0.75, 5.5)`, `maxBlockScale 5`, trail volume a fixed 1.35, `driftShieldsTrail 1`.
- Drift (`DriftTrailActionExecutor` 65 and 79, every 0.05 s) calls `SetDotProduct(dot(course, forward))` (286-289):
  - `wavelength = max(5, 7 x |dot|)`, so a hard drift packs prisms at 5 u.
  - `ZScaler = max(1, 5 x (1 - |dot|))`, so drifted prisms are up to 5x longer.
  - Drift prisms are shielded.

### Scarab goal (switch) placement vs crystal locations
- The ring centre is `ship + course x placementDistance (150, flat)` (Executors/PlaceSwitchActionExecutor.cs:207-210).
- It **snaps onto a living PLANT's heart** (flora only, via `FloraHeartRegistry.NearestToSegment`) that lies within
  `anchorReach 70` of the **segment** from ship to centre and is not already occupied (another switch within 1 u)
  (R_VesselActions/ScarabSwitchAnchors.cs:56-68, 95-111; executor 215-217).
- With no plant in reach it **places free**, 150 u ahead.
- Fauna hearts, free elemental crystals and omni crystals are **never** anchors today.
- A mode may veto or move the placement with the static `PlaceSwitchActionExecutor.PlacementResolver` (executor 71-93,
  223-229; the Tollway installs one at TollwayController.cs:179 and clears it at 191). The resolver must be a pure
  function of replicated state, because the press re-executes on every peer.
- To realise "only on active or inactive elemental crystal locations, e.g. where a fauna just swam":
  - New `ArkEscortCrystalSites` (Controller/Voyages): a ring buffer, sampled every 0.5 s, of (a) live fauna heart
    positions and (b) free crystal positions from `cell.RuntimeData.CellItems`, each with a timestamp, kept for
    `siteMaxAge`. Flora hearts count as active sites.
  - `ArkEscortRun.SetSail` installs `PlacementResolver = ArkEscortCrystalSites.Resolve`. It accepts `anchored`, or
    snaps to the nearest site within `siteReach` of the segment, and otherwise refuses (the press costs nothing).
    `Finish` and `OnDestroy` clear it.
  - Caveat: fauna and free-crystal positions are per-peer, not replicated, so this resolver is safe in the single-host
    sim and solo play but can desync a ring in a party. Restrict party play to `anchored`, or replicate the site list.

## 5. Known bugs and likely causes

1. **Grizzly+Urchin+Falcon deploys one AI wingmate.** The sim seats the local pilot in seat 0 (`SavedSquad`
   `MySeat` 0), so Grizzly is local and Urchin and Falcon are wingmates. `Assets/_Prefabs/Spacevessels/Falcon.prefab`
   (guid `633ae27c43a2006459b22e655df7ab4f`) is in `DefaultNetworkPrefabs.asset` but **not** in
   `Assets/_SO_Assets/Vessel Prefab Container.asset` (`_shipPrefabs` has 22 entries; Falcon, Termite and Shrike-era
   gaps are noted in ServerPlayerVesselInitializerWithAI.cs:506). `SpawnAiCompanionAsync` therefore logs "No prefab for
   vessel type Falcon - companion not released" and returns (Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs:413-417).
   - Fix: refuse to sail, or swap the hull, when `TryGetShipPrefab(slot.Vessel, out _, false)` fails in
     `ArkEscortRun.Deploy` (210-225), and log the seat. The same code is in `VoyageRun.cs:256`.
   - A local Falcon would fail `RequestSwap` the same way.
   - Registering Falcon is a separate design call: it has no AI kit or element map row.
2. **Hull-joust assumption** (finding 1), ArkEscortPilot.cs:55-61. This is the main reason the Dolphin, Serpent and
   Scarab add little; pass 8b's "one Sparrow = three hulls" fits it.
3. **Stale AI cell data** (finding 2): no `RetargetCell` on a leg change.
4. **Leash teleport at 1260 u** (finding 3) blocks nucleus play and any forward station.
5. **Friendly fire on the hull** (finding 4): sim seats carry no element, so no L5 safeties; blind weapon timers.
6. **The Serpent wall can steal the Ark's hull** (SerpentWallAssembler.cs:365-377: no Ark exclusion, and own-domain
   prisms are allowed).
7. **Seat collapse:** `_threats[Mathf.Min(_seat, count-1)]` sends seats 1 and 2 onto the same creature when there are
   one or two threats, and there is no hysteresis, so the target flaps on every 0.5 s re-sort.
8. **The Dolphin's external target kills its weapon** (the same lesson as Bends and Rampage, BendsController.cs:54-61).
9. **The Scarab has no autopilot triggers in the escort** (no `TryAutopilotDash`, no switch press).
10. **Threat geometry:** `CountThreats` measures distance to the Ark's **centre** within 900 u. A long Ark's stern
    grazers can be out of range while bow creatures that are not grazing count. Measure to the nearest hull prism or
    hull segment instead.
11. **Documented:** d9 at 2.6x population lost the D3D12 device (populations are now capped). One sweep stopped on a
    lobby error. Runs are single noisy samples.

## 6. Suggested order
1. Shared plumbing: `RetargetCell`, the leash override, the lane-clear check, the hysteresis, the Falcon guard, and
   the Serpent's Ark guard.
2. `SparrowDestroyerRole` and its `_ArkEscort` rocket policy; evolve three Sparrows at d4.
3. Squirrel, then Dolphin.
4. Scarab and Serpent after a human escort with each (section 1).
