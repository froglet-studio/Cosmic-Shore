# Fauna threats for the Ark escort — research brief

Repo: `D:\Voyage\wt-c` (read-only survey, 2026-10-31). Paths below are repo-relative. `F/` =
`Assets/_Scripts/Controller/Environment/FloraAndFauna/`, `SO/` = `Assets/_SO_Assets/`.

## 0. How fauna reach the Ark today (the mechanism every threat rides)

- No fauna code knows the word "Ark" (grep `Ark` in `F/*.cs` and `Cell.cs` hits only comments). The hull is
  **ordinary opposing-domain prism mass** laid by `Ark.BuildHullLays` (`Assets/_Scripts/Controller/Environment/Ark.cs:315`):
  108 socket prisms, 67 plain / 18 danger / 16 shielded / 7 super-shielded (`Docs/ECOSYSTEM.md:7510`). Herbivores eat it
  through `LightFauna.IsEdibleForHerbivore` (`F/LightFauna.cs:840`) -> `Fauna.IsPreyForMe` (`F/Fauna.cs:140`) ->
  `Cell.IsPreyForHerbivore` (`Cell.cs:564`: nucleus cell = anything outside the nucleus; else `preyDomain != faunaDomain`).
- Shielded plates are inedible outside a Frenzy and super-shielded never (`Fauna.IsInedibleShield`, `F/Fauna.cs:892`;
  `Cell.ShieldedMassIsFood`, `Cell.cs:3426`). This is why the sim's hull "bottoms out near 20%" and the escort needed a
  `BreachHull` line (`Docs/Voyages/ARK_ESCORT.md` §2).
- Steering never targets the Ark: grazers head for `Cell.GetDensestRegionAnyDomain` / `GetExplosionTarget` by PRISM
  COUNT (`F/LightFauna.cs:489-560`, `F/Fauna.cs:1920`), so the Ark's bites are "chance encounters" unless its WAKE leads
  them in (`Docs/ECOSYSTEM.md:7636-7665`). Weighting the Ark directly was ruled a scripted threat (§0).
- Hostility = the world's controlling domain opposes the squad (`ArkEscortRun.cs:184-186`,
  `Cell.SetReplicatedStartingController`); fauna spawn in that colour only. Difficulty = `Cell.RuntimePopulationScale`
  (`Cell.cs:2520`) x the leg's `Threat`, capped by `ArkEscortSO.maxPopulation`.
- Predators (`FaunaDiet.Predator`) eat only herbivore FAUNA (`F/LightFauna.cs:568-585`, `1054-1130`); vessels are
  only separation obstacles to a LightFauna (`F/LightFauna.cs:624-650`). The only wildlife that hunt VESSELS are the
  worm colony (`WormFauna.FindNearestVessel`, `F/WormFauna.cs:695-711`, `AggroRadius` 220 / `StrikeRange` 90 / lunge),
  the substrate bestiary (`F/Substrate/SubstrateArms.cs:23`, pounce/peck/latch/gulp) and the NCA lizard
  (`NcaCreatureConfigSO.VesselSenseRadius` 120, `BoltSpeed`, `VesselBiteReachVoxels`).
- Kills: a vessel through the HEART jousts (`Fauna.Jousted`, `F/Fauna.cs:719` -> `Predated` -> sealed `Die`,
  `F/Fauna.cs:1640`); shooting every body `HealthPrism` kills via `OnBodyPrismExploded` (`F/Fauna.cs:1353`). Every
  death drops exactly one elemental crystal (`LifeFormCrystal`). `ArkEscortPilot.Target` already flies at
  `LivingHeart` (`Assets/_Scripts/Controller/Voyages/ArkEscortPilot.cs:55-60`).

## 1. Live roster (species on the five escort worlds)

| Species | Class / prefab | Movement | Targeting & eating | Reproduction | Killed by | Escort worlds (configs) |
|---|---|---|---|---|---|---|
| QuadFish | `LightFauna`, `Assets/_Prefabs/FloraAndFauna/QuadFish.prefab` (diet 0, starvation 30) | flocking steer + GPU sway + `QuadFishSwimDriver`; `QuadFishFaunaDataSO` min/max 16/28 | herbivore: densest-region goal, bite = target + `feedingClusterRadius` 24.3 (max 8) inside `consumeRadius` 81 facing <25 deg (`LightFauna.UpdateFeeding`, `:863`) | config `FeedsPerOffspring` | heart joust; 4 fin prisms | Garland (4/8), **Boneyard Scavenger 1-4 (60-120 floor / 150-300 cap)**, Wildlife QuadFish 1-4 (383/893) |
| Brittlestar | `LightFauna`, `Assets/_Models/Fauna/MassBrittlestarFauna.prefab` (diet 0, starv 30) | Animator + DampedTransform arms; data 15/25 | herbivore, consume 55, cluster 16.6 | as above | heart; 11 spindles' prisms | Garland (3/6), Scarab (2/4), Wildlife 1-4 (99/228) |
| Shark | `LightFauna`, `MassSharkFauna.prefab` (**diet 1 Predator**, starv 45) | armature + `SharkJawDriver`; 25/35, pursuit x1.5, agility 2.5 | hunts nearest live herbivore in hunt pulses (data defaults: `huntInterval` 20 / `huntDuration` 10, `territoryRadius` 600, anchor 400 - not serialized in `SO/Light Fauna Data/MassSharkFaunaDataSO.asset`, so class defaults apply); `attackRange` 12.6 at the mouth | per kill | heart; 3 spindles' prisms | Garland (1/2), Wildlife Shark 1-4 (32/68) |
| Clawfish | `LightFauna`, `Clawfish.prefab` (diet 0) | GPU sway; 18/32 | herbivore, consume 59, cluster 19.7 | config | heart; 4 fluke ribs | Wildlife Clawfish 1-4 (34/77) |
| Tadpole / "Piranha" | `Boid`, `TadPoleFauna.prefab` (forager 1, starv 90) | boid flock (cohesion/align/sep), `huntSpeedMultiplier` 10 while hunting mass | forager: `GetDensestRegionAnyDomain` + `CenterFocusBias` (`F/Boid.cs:216-228`); mouthful path | config | heart; 1 body prism | Scarab Tadpole (Space, 4/8, 10-15 u/s), Scarab **Piranha** (Charge, 8/22, 45-70 u/s, cooldown 4 s, cadence 0.6) |
| Worm colony | `WormFauna` root + `WormSegmentFauna` members, `WormColony.prefab` (diet 1) | follow-the-leader slither; `WormColonyConfigSO` cruise 18, turn 40 deg/s, kaiju 4.97 | **hunts vessels** (aggro 220, telegraph 1.2 s, lunge 70, tail whip 74.5), bites fauna (`FaunaBiteRange` 56, `MaxBitesPerTick` 6), grows a segment per `Cell.CurrentFaunaSpawnPeriod` | segment production, `FeedsPerOffspring` 0 | every segment has a heart; segment prisms re-link the chain | Wildlife WormColony 1-4 (5/9) |
| Swarm (whale / pufferfish / jellyfish / dragonfly) | `SwarmFauna` (data members, proxies near vessels), `SwarmFaunaConfigSO` | GPU-drawn, `Cruise` 0.35, `TurnPerStep` | eats flora volume as eggs (`BiteRadius` 7, `BitersPerStep` 12); reacts to vessels (`DangerEnter` 0.45, `HuntEnter` 0.2, `PuffWindup`) | `LayRate`/`LayMax`, `EggVolume`, `CrossElementCost` | member proxy heart; `MaxProxies` 160 | Swarm Inner Mass / Middle Charge / Outer Space (bands 470-620 / 690-840 / 910-1080) |
| Substrate bestiary | `SubstrateFauna` host + `SubstrateAgentFauna` proxies, `SubstrateSpeciesSO` (`F/Substrate/SubstrateSpecies.cs:76-216`) | Burst kernel, per-species `Speed`/`Burst`/`Turn` | vessel-centric: pack ring (`RingRoles` 7, `Sense` 900, `WPrey` 1.5 on locusts), mobber dive (`WStrike` 4, 160 u/s), leech latch (`ClingMax` 6, sip 1.5 s, flung at hard turns), stampede bull (`WStrike` 3, 170), leviathan gulp (`GulpR` 220, 115 u/s), harrier (prey shoal), lurker creep, siege | `BirthStock`/`Stock0` | agent heart + one body prism per proxy | Swarm cell only (10 configs, bands 470-1080) |
| Builders (Fortress / Thief Nest / Wearer) | `BuilderColonyFauna`, `BuilderColonyConfigSO` | workers 70 u/s, thieves 150 | lay a lattice shell (`ShellRadius` 40, `CementK`), mend on alarm (`AlarmRadius` 110, `StrikeAt` 0.6), thieves steal prisms (`SpotRange` 700, `Territory` 900) | `WorkerBirthCost` 16 | proxy hearts; shell prisms are mass | Swarm cell (bands 400-465 / 845-905 / 1085-1140) |
| NCA lizard | `NcaCreatureFauna`, `NcaCreatureConfigSO` | learned NCA body, swim 14 / bolt 38 | bolts at vessels within 120 u, bites (`VesselBiteCooldown` 0.8), eats mass (`MouthRadius` 9, `FeedInterval` 1.2) | - | hit prism + heart | Swarm cell (Middle band) |

Spawn profiles: `SO/Cell Configs/<cell>/… Spawn Profile*.asset` (`SupportedFaunas`, `BaseFaunaSpawnTime` 30 s; Wildlife 20 s).
Garland = Brittlestar+QuadFish+Shark; Scarab = Tadpole+Brittlestar+Piranha; Boneyard N = Scavenger N (QuadFish only);
Swarm = 3 swarms + 10 substrate + 3 builders + lizard; Wildlife N = QuadFish/Brittlestar/Shark/WormColony/Clawfish N
(the four Wildlife fauna configs are numerically identical; the ladder differs in flora/cages, not fauna numbers).
All LightFauna/Boid configs on these worlds use `SpreadElements 1` (element rolled per spawn from `SO/Lifeforms/*`).

## 2. Tunable knobs = candidate parameter vector

Per **species config** (`FaunaConfigurationSO`, `Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs`):
`InitialSpawnCount`/`PopulationSize` (floor, 1-400), `MaxLivePopulation` (cap; move with floor, ecology skill §4.6),
`FeedsPerOffspring` (0 = sterile, 3-20 shipped), `OffspringPerBirth` (1-3), `ReproductionCooldownSeconds` (4-60),
`ReleaseTier` (staged release, needs a mode to set `Cell.FaunaReleaseTier`), `BandInnerRadius`/`BandOuterRadius`
(0-1180; spatial diet+steer pen), `CenterFocusBias` (0-1), `Element` (1 Charge / 2 Mass / 3 Space / 4 Time, decides
armour: Mass shielded body, Charge all-danger body, `Docs/ECOSYSTEM.md` §65), `SpreadElements`, `ElementPalette`.
Per-variant (`FaunaVariantTuning`, `Enabled` required): `BaseBodyScale` (0.4-1.6), `BodyPrismScale`, `StarvationSeconds`
(30-120), `Forager`, `CohesionRadius` (20-100), `BehaviorUpdateRate` (0.6-3), `SeparationRadius`, `FeedingClusterRadius`,
`GoalWeight` (0.3-5), `MinSpeed`/`MaxSpeed` (10-70). Boid-only fields; LightFauna reads only Body/Starvation/Material.

Per **LightFauna species** (`LightFaunaDataSO`, `F/LightFaunaDataSO.cs`; four assets in `SO/Light Fauna Data/`):
`detectionRadius` (60-100), `separationRadius` (39-97), `consumeRadius` (33-81), `behaviorUpdateRate` (2),
`separationWeight` (8-20), `goalWeight` (1.5-2), `minSpeed`/`maxSpeed` (15-35), `rotationLerpSpeed` (5-7),
`feedingFacingAngle` (25), `consumeHoldSeconds` (2), `feedingClusterRadius` (10-24), `maxClusterBites` (8),
`feedingBrakeSharpness`; predator: `pursuitSpeedMultiplier` (1.5), `pursuitAgility` (2.5), `attackRange` (12-30),
`territoryRadius` (600; 0 = cell-wide), `territoryAnchorDistance` (400), `huntIntervalSeconds` (20; 0 = always),
`huntDurationSeconds` (10); `witherRingInterval`. Shared by every config of the species: a per-world change needs a
new data asset + prefab variant, or a per-config override (see §4).
Prefab-level on `Fauna` (`F/Fauna.cs:22-34, 296-355`): `diet`, `predationImmunitySeconds` (6), `starvationSeconds`,
`stomachCapacity` (-1 = 16), `goalUpdateInterval` (5) and `goalUpdateIntervalByAggression` {1, .55, .25},
`goalOrbitRadius` (55-60). Hard-coded aggression multipliers `F/LightFauna.cs:387-389`: cadence {1,.55,.25}, consume
radius {1,1.4,1.8}, speed {1,1.25,1.6} - indexed by `Cell.AggressionLevel` (phase Calm/Restless/Frenzy).

Per **worm colony** (`WormColonyConfigSO`, `SO/Lifeforms/WormColonyConfig.asset`): `SpawnSegmentCount` 8, `KaijuScale`
4.97, `MaxSegmentsPerWorm` 16, `CruiseSpeed` 18, `TurnDegreesPerSecond` 40, `MouthRadius` 46, `FaunaBiteRange` 56,
`MaxBitesPerTick` 6, `AggroRadius` 220, `StrikeRange` 90, `PursuitSpeedMultiplier` 1.45, `HuntInterval/Duration` 26/12,
`TelegraphSeconds` 1.2, `LungeSpeed` 70, `RecoverSeconds` 2.5, `TailWhipRadius` 74.5, `SpeedByAggression`.
Per **substrate species** (`SubstrateSpeciesSO.species`, ~120 fields, `F/Substrate/SubstrateSpecies.cs:76-216`): the
headline ones are `N0`/`Capacity`, `Solitary`/`Gregarious` regimes (Speed, Burst, Turn, WHunt, WRing, RingR, Comfort),
`Sense`, `AggrBase`, `BiteR`, `StarveS`, `PreyName`/`WPrey`, `WStrike`/`StrikeSpeed`/`RampS`, `ClingMax`/`GripLoss`/
`FlingSpeed`/`SipS`/`SipWeight` (leech), `GulpR`/`GulpSpeed` (leviathan), `RingRoles`, `QUp`/`QDown` (quorum).
Per **swarm** (`SwarmFaunaConfigSO`): `SeedMembers` 24, `BiteRadius` 7, `BitersPerStep` 12, `EggVolume`, `LayRate`/`LayMax`,
`StarvationSeconds` 90, `Cruise`, `TurnPerStep`, `WanderReach` 300, `DangerEnter/Exit`, `HuntEnter`, `HuntWindupSeconds`,
`MaxProxies` 160, `EngageRadius` 160. Per **builders**: `Founders`/`MaxWorkers` 48, `WorkerSpeed` 70, `ShellRadius` 40,
`AlarmRadius` 110, `StrikeAt` 0.6, `DefendCaste` 0.3, `ThiefFounders`/`MaxThieves`, `ThiefSpeed` 150, `SpotRange` 700.
Per **escort** (`ArkEscortSO`): leg `Threat`, `Hostile`, `HostileFrom`, `Ladder`; tier `Population`, `RegrowSeconds`,
`HostileShare`, `ArkSpeed`, `BreachHull`; `maxPopulation`.

Suggested evolvable vector per world (keep it <= ~25 genes): per species {floor, cap, FeedsPerOffspring, cooldown,
min/max speed, consumeRadius or BiteR, feedingClusterRadius, band outer, element} + predator {huntInterval, huntDuration,
territoryRadius, attackRange} + worm {AggroRadius, LungeSpeed, HuntInterval} + leg Threat. Fitness = hull at exit and
escort kills from `ArkEscortSim` JSON (`Tools/Build/ark_escort_report.py`). Collider gate: body prisms per creature x cap.

## 3. Signature threats, one per world

**T1 Garland Shallows - "the Wake-Runners" (Scarab/Garland grazer school + 1 shark).** Threat: fast QuadFish schools
chase the Ark's wake (the only thing that leads grazers to the hull, `Docs/ECOSYSTEM.md:7636`) and strip plates from the
stern; the shark culls the school so it never over-breeds. Counter: **Dolphin** omni/team crystal blasts the clustered
school at the stern; **Squirrel** nourishes own-domain creatures (`Fauna.Nourish`, `F/Fauna.cs:750`) once the cell is
flipped. Code: none required beyond config - raise QuadFish `MaxSpeed` (28 -> 40) via a Garland-only
`LightFaunaDataSO` + prefab variant, or add `FaunaVariantTuning.MinSpeed/MaxSpeed` reading to
`LightFauna.ApplyVariantTuning` (`F/LightFauna.cs:116`, ~10 lines: scale `data.minSpeed/maxSpeed` into private fields).

**T2 Scarab Reef - "Piranha plate-eaters" (Charge Piranha tadpoles, latch-on).** Threat: the 45-70 u/s Charge piranha
(all-danger body, §65) swarm latches onto the hull and chews one plate ring; latched piranha must be PULLED off.
Counter: **Serpent** walls (laid own-domain mass is food that out-counts the hull in the density grid, so the school
diverts) and **Sparrow** guns pop the single body prism of each latched fish; Scarab rings as goals keep the Ark's path
inside the squad's laid mass. Code (medium): new `PlateLatch` component on `Boid`: when the faced edible prism is a
`Trail`-owned hull prism, parent-follow it (`TrailFollower`-style, `Trail.OnOldestRemoved` rule) for `latchSeconds`
while biting on `consumeHoldSeconds`; released by body-prism hit, by a vessel pass within `VesselSenseMask`
(`F/Fauna.cs:945`), or by the leash. Reuse the leech's grip/fling numbers as defaults (`GripLoss` 1.6, `FlingSpeed` 90).

**T3 The Boneyard - "the Scavenger tide" (Boneyard Scavenger 1-4, QuadFish 60-120 floor).** Today it strips the Ark
regardless of the escort (`ARK_ESCORT.md` pass 8b) because 150-300 grazers per cap barely follow the population scale
and nothing sorts them. Make it a **wall problem**: the tide is a dense, slow, breeding school with a short
`consumeRadius`, so it can be physically fenced. Counter: **Serpent** walls (shielded prisms are not food and are
excluded from steering grids, `Cell.AddBlock`; a Mass-shielded wall is a true fence) and **Scarab** goal rings to route
the Ark. Code (cheap): Boneyard-only `LightFaunaDataSO` (consume 81 -> 40, speed 16/28 -> 10/16, `maxClusterBites` 8 -> 4)
+ `OffspringPerBirth` 2 with `FeedsPerOffspring` 20 so the tide only grows when it actually eats. Optional medium:
honour `ReleaseTier` so the tide releases in two waves (`Cell.FaunaReleaseTier` set by `ArkEscortRun` on entry).

**T4 The Swarm - "the Nucleus fortress" (Fortress builders + pack hunters + leeches).** Threat: builders cement a shell
around the nucleus the Ark must reach, the pack ring stalks the escort ships (`WPrey` on locusts today), leeches latch on
hulls and sip. Counter: **Squirrel** jousts the shell's hearts and out-masses the nucleus claim (volume is the spine,
`Cell.IsPreyForHerbivore` sanctuary); **Sparrow** pops leeches (its proxy "exists to be rammed"); **Dolphin** blasts the
pack ring when it closes (`CloseR` 350). Code (cheap): `Substrate Pack Hunter Species.asset` `PreyName` -> `""` and raise
`WHunt`/`AggrBase` so the ring forms on vessels; `Substrate Leech Species` `ClingMax` 6 -> 10, `SipWeight` 0.25 -> 0.4;
builders `ShellRadius` 40 -> 60, `MaxWorkers` 48 -> 64 (collider gate: proxies only, `MaxProxies` 24). Medium: let the
leech's `Attach` target the Ark's hull prisms too - `SubstrateCore` only lists vessels (`SubstrateArms.cs:108`); add the
Ark root as a pseudo-vessel entry (radius from `Ark` hull length) in `SubstrateCellHost` (~40 lines).

**T5 The Maw - "the Kaiju and its court" (Wildlife worm colonies + sharks + plate-eaters).** Threat: worm colonies hunt
the ESCORTS (`AggroRadius` 220, lunge 70) while the grazer court eats the Ark; killing a worm segment frees its heart but
the chain re-links. Counter: **Sparrow** shoots segments' body prisms from range; **Squirrel** jousts segment hearts
(each is a full lifeform); walls split the court from the kaiju band. Code (cheap): `WormColonyConfig` `AggroRadius`
220 -> 400 and `HuntInterval/Duration` 26/12 -> 18/14 on a Maw-only config asset (prefab `config` field, so a prefab variant
or a `WormColonyConfigSO` reference on the `FaunaConfigurationSO`, medium ~20 lines); Wildlife Shark `huntIntervalSeconds`
0 on a Maw data asset so predators thin the court unless the squad feeds them.

**T6 (any pacified world) - "Turned fauna".** When the squad flips a hostile world (lays more mass than the controller),
`Cell.SetModeControlOverride`/controller change re-colours the LIVE swarm into the squad's domain (design principles:
one fauna colour per cell, re-coloured live). Those creatures then never eat the Ark (own-domain hull is not prey in a
nucleus-less cell) and can be shepherded by Squirrel `Nourish`. Already in the platform; expose it to the escort by
reporting the flip in `ArkEscortReport` and letting the Squirrel's AI seat (`ArkEscortPilot`) switch from hunting to
nourishing once `cell.ControllingDomain == squad` (~30 lines in `ArkEscortPilot.Target`).

## 4. Cost

| Change | Cost |
|---|---|
| Per-world speed/consume/bite via new `LightFaunaDataSO` assets + prefab variants; config floors/caps/breeding; worm config numbers; substrate species numbers; builder numbers | **cheap** (config/asset only; editor step to make prefab variants) |
| `LightFauna.ApplyVariantTuning` reading `MinSpeed/MaxSpeed/FeedingClusterRadius` (so the EXISTING config fields drive LightFauna per world, no prefab variants) | **medium-small** (~15 lines, `F/LightFauna.cs:116`) |
| `FaunaConfigurationSO.WormConfigOverride` -> `WormFauna.config` at `Initialize` | medium (~20 lines) |
| `PlateLatch` on `Boid`/`LightFauna` (T2) | **medium** (new component ~120 lines; must write only rotation/position of the CREATURE, never a `HealthPrism.localPosition`; respects continuity and no-cull) |
| Substrate leech latching onto the Ark (T4) | medium (~40 lines in `SubstrateCellHost`, Burst kernel sees it as a vessel) |
| Escort pilot shepherd mode after a flip (T6) | medium (~30 lines) |
| Staged `ReleaseTier` waves driven by `ArkEscortRun` | medium (~15 lines; both spawners already honour the tier) |
| Pack-hunter escort-targeting beyond the substrate pen; a LightFauna predator that hunts VESSELS; new species | **expensive** (new diet branch in `LightFauna`, network sync, heart seat gate, collider budget restatement) |
| A GA harness driving these over `ArkEscortSim` runs (one Unity batch run ~minutes per escort at timescale 3) | expensive in wall-clock, cheap in code: `ArkEscortSimLauncher` already takes squads/difficulty; add a `-arkEscortGenome` JSON that writes the SO fields before `Run` |

## 5. Locked invariants any change must respect (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`, `.claude/skills/ecology/SKILL.md` §2)

1. **Continuity of existence**: nothing pops; latch/release, waves and deaths wither, bloom, suction or fade.
2. **No imposed death**: no lifespans, timers or culls - population is bounded by consumption + starvation; gating
   PRODUCTION (`ReleaseTier`, population scale) is allowed, culling to meet it is not.
3. **No domain asymmetry**: fauna spawn in ONE colour, the controlling domain's; no "attack the Ark" weighting
   (`Docs/ECOSYSTEM.md:7650`: "not to weight the Ark (that is a scripted threat)"). Threat pressure must come from food-web
   mechanics (wake, density, pens, diet), never a hard-coded Ark seek. Stealing/turning a creature only through
   `Fauna.StealInto` / the controller re-colour.
4. **Mass is conserved, every lifeform drops one heart** (`LifeFormCrystal`); a colony is a population, every member has a
   heart; hearts are one size (`ElementalCrystalSetSO.CrystalWorldScale` 3.55) - scale the BODY, never the heart.
5. **Volume is the spine**: phase/aggression comes from `Cell.LiveVolume`; shielded mass is never a steering target and
   super-shielded mass is never food; shielded is food only in Frenzy (`Fauna.IsInedibleShield`). Do not make Ark plates
   edible by special-casing.
6. **Endogenous selection only**: no lifeform level, no scripted fitness in-game; co-evolution happens OFFLINE on config
   assets (our GA writes SOs), which is permitted - the game itself must not score and reward creatures.
7. **Territorial permanence** (don't cull the dominant canopy) and **the collider budget** (Masterplan §4, target
   <= ~1,500 active prism colliders per cell): state body prisms x cap per world before raising any floor/cap; prefer data
   populations (swarm/substrate proxies) for big counts.
8. Element law (§65): Mass body = shielded, Charge body = all danger, Time breeds faster, Space out-spans; a species'
   element choice changes its diet exposure (an armoured plant is not food) - re-check every penned grazer's food.
9. Per-cell population modifiers resolve on the `Cell` (`ResolveFaunaCap`/`IsFaunaAtCap`), and all FOUR producers
   (two spawners, `Fauna.TryReproduce`, Microscene) must agree; a shared `SO/Lifeforms` asset edited for one world edits
   every mode using it - make per-world copies under the cell's `Cell Configs/` folder.
