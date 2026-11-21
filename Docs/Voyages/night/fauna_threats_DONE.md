# Fauna threats for the Ark escort — DONE (2026-10-31)

Branch `claude/voyage-fauna-threats` (worktree `D:\Voyage\wt-b`), three commits on top of `44aedd874`:

1. `feat(ecology): per-config species data override + LightFauna variant speeds`
2. `feat(fauna): PlateLatch - a forager Boid rides moving mass it bit`
3. `feat(escort): per-world fauna threat assets for the five escort worlds`

Not touched (the parallel Ark agent's files): `Ark.cs`, `ArkwayRun.cs`, `ArkEscortRun.cs`, `Prism.cs`, `Cell.cs`,
`Fauna.cs`. No shared asset under `_SO_Assets/Lifeforms`, `Light Fauna Data`, `Substrate Fauna/*Species`,
`Builder Colonies` was edited; every per-world number lives in a copy under that cell's `Cell Configs/` folder.

## 0. The one mechanism everything rides: `FaunaConfigurationSO.SpeciesDataOverride`

`Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs` — a `ScriptableObject` slot. A config copy names
its OWN species data asset (a `LightFaunaDataSO`, `WormColonyConfigSO`, `SubstrateSpeciesSO` or `BuilderColonyConfigSO`);
the creature class swaps the prefab's asset for it. No prefab variants (the brief's "editor step" is gone).

- `ISpeciesDataOverride` (`FloraAndFauna/ISpeciesDataOverride.cs`): `BindSpeciesData(FaunaConfigurationSO)`.
  Implemented by `LightFauna`, `WormFauna`, `SubstrateFauna`, `BuilderColonyFauna`.
- Bound in `CellLifeSpawnerBase.SpawnFaunaWithDomain` BEFORE `pop.Initialize(host)` — the one Instantiate every
  producer reaches (both spawners, the preview arena, Tandava severing), so a colony's chain / a substrate's block
  claim / a builder's seeding already see the world's data. `LightFauna` also re-resolves it from `SourceConfig` at
  `ApplyVariantTuning` and on its first behaviour tick, so OFFSPRING (`Fauna.SpawnOffspring` → Initialize →
  AssignLineage, no spawner hook) inherit the lineage's data.
- `LightFauna` now honours `FaunaVariantTuning.MinSpeed` / `MaxSpeed` / `FeedingClusterRadius` (`MinSpeed`/`MaxSpeed`
  properties replace the four `data.minSpeed/maxSpeed` reads), as `Boid` already did. Note the ecology trap: a config
  with `SpreadElements` + a palette applies the PALETTE sibling's Variant, not its own — which is why the per-world
  speeds below go through `SpeciesDataOverride`, not Variant.

Brief claims verified: `LightFaunaDataSO` is prefab-level (shared by every config of the species) — true;
`Prism.Trail` is set for hull plates (`PrismTrailBuilder.ConfigureLaid` → `AssignTrail`) — true, but it is ALSO set
for every environment lay, so "Trail-owned" cannot identify the hull (see T2); `Cell.FaunaReleaseTier` exists
(`Cell.cs:845`) and the Boneyard / Wildlife profiles already seed it at 0 — true; the shark data asset does not
serialize its hunt fields (class defaults 20/10 apply) — true; `MassSharkFaunaDataSO.territoryRadius` etc. likewise.
Substrate species and builder configs are referenced only from their prefabs, which are referenced only by the Swarm
cell's configs — true (grep by guid).

## 1. Per world

### W1 Garland Shallows — "the Wake-Runners"
- Threat: a FAST QuadFish school (22–40 u/s instead of 16–28) that keeps up with the Ark and reaches the stern plates
  its wake leads them to; the Garland shark culls the school.
- Intended counter: Dolphin crystal blasts into the clustered school at the stern; Squirrel nourishes once the world is
  flipped; Serpent walls across the wake line.
- Changed: new `Cell Configs/Garland Cell/Garland QuadFish Light Fauna Data.asset` (+meta, guid
  `30e9a3b8e3ba4c49b509cb933c62fcc8`) = QuadFish data with `minSpeed 22`, `maxSpeed 40`, every other field written
  explicitly; `Garland QuadFish Fauna Config Data.asset` → `SpeciesDataOverride` set.
- Counts / colliders: QuadFish floor 4 / cap 8 (4 fin prisms + heart) = 32 body colliders; Brittlestar cap 6 × 11
  spindles' prisms; Shark cap 2. Unchanged by this work; well under 1,500 with the Ark's 108 hull plates.
- Tunables: `Garland QuadFish Light Fauna Data.asset` → `minSpeed` [12, 30], `maxSpeed` [20, 55], `consumeRadius`
  [40, 100], `feedingClusterRadius` [10, 30], `maxClusterBites` [2, 8]; `Garland QuadFish Fauna Config Data.asset` →
  `PopulationSize` [2, 12], `MaxLivePopulation` [4, 24] (move with the floor), `FeedsPerOffspring` [2, 12],
  `ReproductionCooldownSeconds` [8, 40]; `Garland Shark Fauna Config Data.asset` → `MaxLivePopulation` [1, 3].

### W2 Scarab Reef — "Piranha plate-eaters" (the medium item)
- Threat: the Charge piranha school (45–70 u/s, all-danger single body prism) LATCHES onto a plate that is moving under
  its mouth and chews the ring of plates around its grip; a latched fish must be pulled off or shot off.
- Intended counter: Serpent walls (own-domain mass out-counts the hull in the density grid, diverting the school);
  Sparrow guns pop each rider's one body prism; flying a vessel through the latched school scares them off
  (`PlateLatchScareRadius`); Scarab goal rings route the Ark inside the squad's laid mass.
- Changed: `FloraAndFauna/PlateLatch.cs` (+meta) — a runtime-added component (no prefab carries it) that remembers a
  grip plate and the body's seat on it, eases the creature's TRANSFORM to the seat each frame (never a HealthPrism
  position), faces the grip, and releases with a fling (leech numbers: FlingSpeed 90, 3 s relatch cooldown).
  `Boid.cs`: the behaviour tick records where the chosen feed target was; when the mouth reaches it and the target has
  drifted ≥ 0.35 u since (MOVING mass — the Ark hull, any towed plate; still mass is eaten as before) the boid latches
  instead of biting; while riding it takes `LatchedMouthful` every `consumeHoldSeconds` of the edible plates within
  `feedingClusterRadius` of the grip (never the grip), and lets go when the ride is over, the grip is gone, the ring is
  stripped (zero bites), a vessel is inside the scare radius (one `OverlapSphereNonAlloc` on the tick, not per frame),
  or it is leashed; `OnDeath` drops the grip. `FaunaConfigurationSO`: `PlateLatchSeconds` (0 = off, every other
  biome), `PlateLatchFlingSpeed`, `PlateLatchScareRadius`. `Scarab Scramble Piranha Fauna Config Data.asset`:
  8 / 90 / 60.
- Why "moved under it" and not "Trail-owned": `Prism.Trail` is set on every environment lay too, so it would have made
  piranha latch onto flora; motion is the honest, non-Ark-specific test (no Ark seek — the plate is still found by the
  ordinary densest-mass goal). Also keeps the drone (non-forager) path untouched.
- Counts / colliders: Piranha floor 8 / cap 22, 1 body prism each = 22; Tadpole cap 8; Brittlestar cap 4 × 11.
  Unchanged.
- Tunables: `Scarab Scramble Piranha Fauna Config Data.asset` → `PlateLatchSeconds` [3, 20], `PlateLatchScareRadius`
  [0, 120], `PlateLatchFlingSpeed` [40, 140], `Variant.MinSpeed` [30, 60], `Variant.MaxSpeed` [50, 90],
  `Variant.FeedingClusterRadius` [15, 40], `PopulationSize` [4, 16], `MaxLivePopulation` [10, 40],
  `FeedsPerOffspring` [2, 8], `ReproductionCooldownSeconds` [2, 10], `CenterFocusBias` [0, 0.6].

### W3 The Boneyard — "the Scavenger tide" (a wall problem)
- Threat: a dense, SLOW (10–16 u/s), short-reach (consume 40, 4 bites a mouthful) QuadFish tide that only grows when
  it actually eats (2 offspring per 20 feeds) — fenceable, instead of a 150–300 grazer vacuum that stripped the Ark
  whatever the escort did (ARK_ESCORT.md pass 8b).
- Intended counter: Serpent walls (shielded mass is not food and is excluded from the steering grids — a Mass-shielded
  wall is a true fence for a slow school); Scarab rings to route the Ark; Dolphin blasts where the tide bunches at the
  fence.
- Changed: new `Cell Configs/Boneyard Cell/Boneyard Scavenger Light Fauna Data.asset` (+meta, guid
  `241dd4a2a91a454e96ec8cc8d261bf5f`): `consumeRadius 40`, `minSpeed 10`, `maxSpeed 16`, `maxClusterBites 4`;
  `Boneyard Scavenger 1-4.asset` → `SpeciesDataOverride`, `OffspringPerBirth 2`, `FeedsPerOffspring 20` (was 20/1).
- Counts / colliders (unchanged caps): rung 1 floor 60 / cap 150 → 600 fin prisms; rung 4 floor 120 / cap 300 → 1,200
  fin prisms + 300 hearts' trigger colliders, plus the Ark's 108: at the ceiling of the 1,500 gate. `ArkEscortSO.
  maxPopulation 2` × tier Population ≤ 1.4 × Threat 1.0 scales the floor and cap together through
  `Cell.RuntimePopulationScale`, so d9 on rung 4 can reach the cap. Do not raise these caps; lower them if the slow
  tide now survives longer than the fast one did.
- Tunables: `Boneyard Scavenger Light Fauna Data.asset` → `consumeRadius` [25, 81], `minSpeed` [6, 16], `maxSpeed`
  [10, 28], `maxClusterBites` [2, 8], `feedingClusterRadius` [10, 30]; `Boneyard Scavenger N.asset` →
  `PopulationSize` [30, 120], `MaxLivePopulation` [60, 300], `FeedsPerOffspring` [8, 30], `OffspringPerBirth` [1, 3],
  `ReproductionCooldownSeconds` [6, 30].

### W4 The Swarm — "the Nucleus fortress"
- Threat: the fortress colony cements a wider, thicker shell (ShellRadius 60, 64 workers) around the nucleus band the
  Ark must cross; the pack hunters no longer hunt locusts — their ring forms on VESSELS (prey off, WHunt 0.6 solitary /
  3 gregarious, AggrBase 0.9); leeches hold on longer and drain harder (ClingMax 10, SipWeight 0.4).
- Intended counter: Squirrel jousts the shell's hearts and out-masses the nucleus claim; Sparrow pops leeches (the
  proxy exists to be rammed); Dolphin blasts the ring when it closes (`CloseR` 350).
- Changed: new under `Cell Configs/Swarm Cell/`: `Swarm Pack Hunter Species.asset` (guid
  `bcd3e3c7180743c0a59bf6cd12cbc1ed`), `Swarm Leech Species.asset` (`05bee3b7429b453aab89216fae103dbf`),
  `Swarm Fortress Colony Config.asset` (`f0cbde43f1e2458a960337f9823caf55`), each a full copy of the shared asset
  with only the named fields changed; wired through `SpeciesDataOverride` on `Substrate Fauna/Substrate Pack Hunter
  Fauna Config Data.asset`, `Substrate Fauna/Substrate Leech Fauna Config Data.asset` (both referenced only by the
  Swarm spawn profile) and `Swarm Cell/Swarm Fortress Builder Fauna Config Data.asset`.
- Counts / colliders: data populations — pack 6/7 with `maxProxies 7`, leech 48/64 with its proxy cap, fortress
  `MaxProxies 24` (unchanged): the collider cost is the proxy budget, not the headcount. Shell prisms are mass the
  colony lays (more with 64 workers; `MaxWorkers` is the knob to pull back).
- Tunables: `Swarm Pack Hunter Species.asset` → `species.AggrBase` [0.4, 1], `species.Solitary.WHunt` [0.2, 1],
  `species.Gregarious.WHunt` [1.5, 4], `species.CloseR` [200, 450], `species.RingHoldSeconds` [2, 10],
  `species.N0`/`Capacity` [4, 9] (proxies ≤ 7); `Swarm Leech Species.asset` → `species.ClingMax` [4, 14],
  `species.SipWeight` [0.2, 0.6], `species.GripLoss` [0.8, 2.5], `species.SipS` [0.8, 2.5]; `Swarm Fortress Colony
  Config.asset` → `ShellRadius` [40, 80], `MaxWorkers` [32, 80], `CementK` [0.3, 1], `AlarmRadius` [80, 160],
  `StrikeAt` [0.4, 0.8].

### W5 The Maw — "the Kaiju and its court"
- Threat: the worm colonies hunt the ESCORTS from further (AggroRadius 400) and more often (hunt 18 s interval / 14 s
  window) while the grazer court eats the Ark; the sharks are ALWAYS hunting (huntIntervalSeconds 0) so they thin the
  court — unless the squad kills the sharks first.
- Intended counter: Sparrow shoots segment body prisms from range; Squirrel jousts segment hearts (each a full
  lifeform); Serpent walls split the court from the kaiju band; leave the sharks alive.
- Changed: new under `Cell Configs/Wildlife Liberation Cell/`: `Wildlife Worm Colony Config.asset` (guid
  `70ae2cc1b24745e2a39bd4ccc57b959d`; `AggroRadius 400`, `HuntIntervalSeconds 18`, `HuntDurationSeconds 14`) wired
  into `Wildlife WormColony 1-4.asset`; `Wildlife Shark Light Fauna Data.asset` (`0fb432e79f834153b1b98e8946ca71cc`;
  shark data with every field explicit and `huntIntervalSeconds 0`) wired into `Wildlife Shark 1-4.asset`.
  `WormFauna.BindSpeciesData` swaps the colony config before `BuildChain`, so chain numbers would follow too (left
  identical here).
- Counts / colliders (unchanged, pre-existing): rung 4 caps QuadFish 893 (×4), Brittlestar 228 (×11), Shark 68 (×3),
  Clawfish 77 (×4), WormColony 9 (×≤16 segments) — far above 1,500 at the authored cap; the escort relies on
  `ArkEscortSO.maxPopulation 2` and the population tiers never filling them. Flagging it: this world's caps, not
  this change, are the budget risk.
- Tunables: `Wildlife Worm Colony Config.asset` → `AggroRadius` [220, 600], `HuntIntervalSeconds` [12, 30],
  `HuntDurationSeconds` [6, 18], `LungeSpeed` [50, 100], `StrikeRange` [60, 140], `TelegraphSeconds` [0.8, 2];
  `Wildlife Shark Light Fauna Data.asset` → `huntIntervalSeconds` [0, 30], `huntDurationSeconds` [5, 15],
  `territoryRadius` [0, 900], `attackRange` [10, 30]; `Wildlife WormColony N.asset` → `PopulationSize` [2, 6],
  `MaxLivePopulation` [3, 9]; `Wildlife Shark N.asset` → `PopulationSize` [8, 40], `MaxLivePopulation` [16, 68].

## 2. Designed, not built

- **Staged scavenger waves (Boneyard).** Both spawners already honour `FaunaConfigurationSO.ReleaseTier` against
  `Cell.FaunaReleaseTier` (seeded from `SpawnProfileSO.InitialFaunaReleaseTier`, 0 on the Boneyard profiles). Design:
  split each rung into two configs (`Boneyard Scavenger N` at tier 0 with half the floor/cap, `Boneyard Scavenger N
  Second Wave` at tier 1 with the rest, both in the rung's spawn profile) and have `ArkEscortRun` set
  `cell.FaunaReleaseTier = 1` when the Ark passes the cell's midpoint (or when hull < 70%). ~15 lines in
  `ArkEscortRun.cs` (off-limits here) + 4 config copies.
- **Leech latching onto the Ark (Swarm).** `SubstrateCore` lists only vessels as cling hosts (`SubstrateArms.cs`
  vessel list / `SubstrateCellHost`). Design: `SubstrateCellHost` registers the Ark root as a pseudo-vessel entry
  (position + hull-length radius, no `IVesselStatus`), so the Burst kernel's `ClingMax`/`Sip` path treats the hull as
  a host; a sip on the Ark should call the parallel agent's `Fauna.Bite` helper on the nearest live hull plate. Needs
  `Ark.cs` to expose its root/radius (off-limits) — ~40 lines total.
- **Turned fauna / shepherd mode (T6).** `ArkEscortPilot.Target` switches from hunting to `Fauna.Nourish` once
  `cell.ControllingDomain == squad domain`; report the flip in `ArkEscortReport`. ~30 lines, deferred because it sits
  beside the parallel agent's `ArkEscortRun.cs` work.
- **Expensive, written only:** a LightFauna predator that hunts vessels (new diet branch + network sync + heart-seat
  gate); pack-hunter targeting outside the substrate pen; new species; the GA harness (`-arkEscortGenome` JSON that
  writes the fields in §1 before `ArkEscortSimLauncher.Run`).

## 3. Invariants (ecology skill §2) — touched and kept

Continuity: the latch eases in (exponential seat), the release is a velocity, nothing teleports; no imposed death: no
timer culls (the latch ride ending is a behaviour, not a death); no domain asymmetry / no Ark seek: the latch test is
"the plate moved", the goal is still the densest-mass grid; mass conserved / one heart each: untouched; volume is the
spine: shielded plates are still never a latch or a meal (`IsEdibleForForager`); endogenous selection: all tuning is
offline config; collider budget: no cap raised, counts stated above; the §65 element law: untouched (the piranha stays
Charge, all-danger body).

## 4. Verification

- Static: the five `Tools/Build/check_*.py` gates (see the compile section appended below).
- Compile: Unity 6000.3.17f1 batchmode on `D:\Voyage\wt-b` (see below).
- Not verified (needs the editor / a sim run): the latch seat looks right on a moving hull; the scare radius pulls a
  school off; the Boneyard tide is slower to strip the Ark; the pack ring forms on vessels with `PreyName ''`
  (the kernel's `WHunt` reads vessels when there is no prey — confirm in the Swarm cell); `ArkEscortSim` numbers per
  world at d3/d4 before and after.

## 5. Compile / gate results

- `Tools/Build/check_conditional_compilation.py` OK (3056 files), `check_enum_member_references.py` OK,
  `check_switch_label_collisions.py` OK, `check_using_directives.py` OK (10 files), `check_self_referential_locals.py` OK.
- Unity 6000.3.17f1 `-batchmode -quit -nographics -projectPath D:\Voyage\wt-b` → exit 0, "Exiting batchmode
  successfully", 0 × `error CS`, no missing-script / meta / guid import errors (`D:\VoyageLogs\compile_wt-b.log`).
- Pushed: `origin/claude/voyage-fauna-threats` (no PR).

# Round 2 (2026-10-31, after merging origin/claude/voyage-night)

Branch `claude/voyage-fauna-threats`, merge `18acc10d5` (clean; LightFauna's run-tuning override and my data override
both kept) + commits `1d7f7de45` (code), `1f601f46f` (escort-owned worlds) and the verbose sim flag. Compile clean
(0 `error CS`, `D:\VoyageLogs\compile_wt-b_r2.log`), five static gates OK.

## Precedence: fauna.* run overrides win over SpeciesDataOverride

Spawner path: `BindSpeciesData` (world data) -> `Initialize` -> `ApplyRuntimeTuningFrom` makes the run copy FROM the
world data -> `AssignLineage` re-applies it after the variant. Offspring path: `LightFauna.BindSpeciesData` itself
re-derives the run copy (`FaunaRuntimeTuning.ResolveLightData`) when the cell carries a tuning, so a bind that lands
after the tuning (an offspring's lineage) still ends on the run's numbers. Worm: the override swaps `config` before
`Initialize`; `ResolveWormConfig` then copies from it.

## The escort now OWNS its worlds (`Assets/_SO_Assets/Cell Configs/Ark Escort Cell/`)

Every escort world's cell config is also a mode's arena (Garland/Swarm: Menu_Main; Scarab: Scarab Scramble; Boneyard
1-4: Broadside/DogFight/Dustup/Salvo/GrizzlyCharge; Wildlife 1-4: Wildlife Liberation + Undertow), so round 1's edits to
the shared fauna configs were reverted and the threats live on escort-only copies: 11 `Escort * Cell Config`, 11
`Escort * Spawn Profile`, the changed fauna configs (`Escort Garland QuadFish`, `Escort Scarab Piranha`,
`Escort Boneyard Scavenger N Wave 1/2`, `Escort Swarm Pack Hunter/Leech/Fortress Builder`, `Escort Maw Shark N /
WormColony N`) and the 7 species data assets (`Escort ... Light Fauna Data / Species / Colony Config`), all fresh guids;
`Toys/ArkEscort.asset` legs and ladders point at the copies. Shared assets are byte-identical to the merge.

## Designed-only items, now built

- **Staged scavenger waves** (Boneyard): `Escort Boneyard Scavenger N Wave 1` (ReleaseTier 0, half floor/cap) +
  `Wave 2` (ReleaseTier 1, the other half) on `Escort Boneyard Spawn Profile N` (InitialFaunaReleaseTier 0).
  `ArkEscortSO.secondWaveAt` (0.6; param `escort.secondWaveAt`, 0 = off): `ArkEscortRun.Metrics.TryReleaseWave` on the
  1 Hz sample sets `cell.FaunaReleaseTier = 1` once the Ark is inside the current world within that share of the
  membrane radius; both spawners then seed tier 1 on their next tick. Any world whose profile starts at tier 0 stages;
  a profile at int.MaxValue is never touched. Leg field `WaveReleasedAt`.
- **Leeches on the Ark** (Swarm): the Ark is a CLING-ONLY substrate pilot (`SubstratePilot.ClingOnly`; `Ark.Live`
  registry; `SubstrateCellHost.SenseVessels` adds every Ark inside the cell, radius 40). Only a species with `ClingMax`
  sees it (kernel `NearestPilot(..., seeClingOnly: k.Cling != 0)`, core `NearestHost` for the latch, tick job skips it
  for proxies), so no hunter, ring, mobber or leviathan is drawn to it. A rider's sip (`SubstrateFauna.Sip`) with no
  vessel victim resolves `host.ArkById` and bites the hull plate nearest the rider's seat (within 60 u) through
  `Fauna.BiteMass` from a `SipMouth` transform at the seat; `SubstrateFauna.AuthoredArmourBite` = the species'
  `SipWeight`, so a leech gnaws armour at 0.4 points a sip. Leg field `ArkSips`.
- **PlateLatch through the plate-eating path**: `Boid.LatchedMouthful` bites through `BiteMass` (a gnaw counts as a
  bite and keeps the rider on; the prefab's `shieldBite 0.25 / superShieldBite 0.1` now apply to latched piranha).
- Telemetry: `Latches`, `WormLunges`, `ArkSips`, `WaveReleasedAt` per leg and run totals (`PlateLatch.Latches`,
  `WormFauna.Lunges`, `SubstrateFauna.ArkSips` session counters, diffed per leg); the summary line carries them.
  `-arkEscortVerbose` (sim flag; `run_sim.ps1 -Extra "-arkEscortVerbose"`) puts the ToyBox channel's end reasons in the log.

## Sim verification (Sparrow+Squirrel+Dolphin, time scale 3, `D:\VoyageLogs\threats_*.jsonl`)

| run | result | hull min | Garland | Scarab | Boneyard | Swarm | Maw |
|---|---|---|---|---|---|---|---|
| d3, pacify 0.6 (default) | Survived | 51% | pacified 39 s, 0 plates | pacified 39 s, 0 creature plates, 0 latches | pacified 48 s, wave at 874 s(abs), 0 plates | pacified 126 s, 0 sips, 8 kills on Ark | NOT pacified: 108>55>63, **-101 plates (97 by light fauna)**, peak 168 threats, wave released |
| d6, `escort.pacifyShare 0` | Survived | 22% | 0 plates, 14 kills on Ark, peak 6 | **108>24>34, -111 plates (109 by piranha), 52 latches** | 34>29>27, -88 (+81 regrown), wave at 159 s, peak 100 | 27>26>108 (+88 regrown), -7, 0 sips, peak 97 | never entered (see below) |

What showed up, per world:
- **Scarab (latches)**: yes - 52 latches and 109 plates to the piranha at d6; the Ark's worst world (24 plates left).
  At d3 pacification turned the school at 39 s before it reached the hull.
- **Boneyard (waves)**: the second wave released 159 s into the leg (d6) and in both runs; the slow tide took 88
  plates at d6 while the reactor regrew 81 - a sustained, fenceable pressure rather than a strip.
- **Maw (worm aggro, plate loss)**: at d3 the Maw was the only world that bit (-101, 97 by light fauna, peak 168), so
  it is distinct; **0 worm lunges** in 169 s - the colonies (2-3 at x0.54) never had a vessel inside 400 u in a hunt
  window. Recommend `AggroRadius` 600 / `HuntIntervalSeconds` 12 on `Escort Wildlife Worm Colony Config` for the
  next pass. At d6 the leg was never entered (below).
- **Swarm (leech sips)**: 0 sips in both runs. The leech band is 470-620 u and the Ark crawls through the core; the
  riders' kernel sees the Ark (verified by code path, not by a sip) but none reached it. Suggest a sip test with
  `fauna.*` off and the leech band widened on `Escort Swarm Leech Fauna Config` (BandOuterRadius 900) before judging it.
- **Garland (wake-runners)**: 0 plates lost in both runs; the fast school is killed (14 kills on the Ark at d6) before
  it strips anything. Garland is the warm-up world as intended.
- Worlds differ: Scarab and Boneyard are the plate worlds at d6, the Maw at d3, Garland is clean, the Swarm regrows.

## Issues found (not fixed here)

1. **Second run in one editor session is Abandoned** after 6-26 s under way (0 s in cells; both sims, d6 and d3 alike).
   The baseline (`sim_baseline_sparrow3b.log`, 9 consecutive runs) did not do this, so it arrived with voyage-night's
   or my changes; the end reason is only on the verbose channel, hence `-arkEscortVerbose` (a 2 x d1 diagnostic run is
   recorded below). Until fixed, drive the farm one run per editor launch.
2. **`MissingReferenceException: Crystal` in `CellConveyor.SpawnCoreCrystal` -> `StandCell` -> `AdvancePastTarget`**
   (`CellConveyor.cs:524`, `CellRuntimeDataSO.AddCrystalToList`) while standing The Maw at d6: the route ended after
   the Swarm and leg 5 was never entered (its 680 kills were booked to leg 5 with no entry). Not seen in the other
   logs; a destroyed crystal in the shared runtime list. Needs the conveyor owner's eye.
3. Pacification at the default 0.6 fires at 39-48 s in four worlds (also noted by integration): with it on, only the
   Maw ever threatens; the per-world threats are measurable only with `escort.pacifyShare` 0 or a much higher line.

## Diagnostic run (2 x d1, `escort.pacifyShare 0`, `-arkEscortVerbose`; `threats_multirun.jsonl`)

Both runs Survived back to back (93% / 85% hull): the second-run abort is NOT deterministic - it hit the d3-after-d6 and
d6-after-d3 pairs (heavier worlds) and not d1-after-d1. Scarab latched again (5 / 11 latches, 62 / 64 plates to the
piranha, every plate regrown at d1), the Maw released its tier-1 "wave" (its profile also starts at tier 0; harmless,
no tier-1 species there), Swarm/Garland/Maw lost nothing to creatures.

Corridor findings the conveyor's owner should look at (verbose log `D:\VoyageLogs\sim_threats_multirun.log`):
- **The Boneyard was never stood at d1** in either run (verbose "Traversal cell stood" lists Garland, Scarab Scramble,
  Swarm, Wildlife Liberation only; leg 3 `EnteredAt 0`), while the SAME asset (`Escort Boneyard Cell Config 1`) stood
  and was crossed at d3 (`threats_check`), and `Escort Boneyard Cell Config 3` at d6. So the escort reaches 4 worlds
  at d1 and 5 at d3/d6 - `CellConveyor.NextConfig` / `TraversalWorlds` is where to look (the route indexes into
  `_worlds`, filled once per corridor).
- **The Maw was not stood at d6** after the `Crystal` MissingReferenceException in `CellConveyor.SpawnCoreCrystal`
  (`StandCell` -> `AdvancePastTarget`); the route then ended at the Swarm and the run read "Survived".
- `AIPilot.UpdateCellContent` (`AIPilot.cs:646`) throws `MissingReferenceException: Crystal` repeatedly (15 in the d1
  pair) - the escort roles' pilot reads a destroyed crystal's transform. Not mine; noting it because it is the loudest
  thing in every escort log now.

## How to read a run for the threats

`python3 Tools/Build/ark_escort_report.py <jsonl>` (unchanged), or straight from the JSON: per leg `Latches`,
`WormLunges`, `ArkSips`, `WaveReleasedAt` (Time.time; -1 none), `PlatesByCreature`, `PlateKillers`; run `Latches`,
`WormLunges`, `ArkSips`, `WavesReleased`, `SecondWaveAt`. Run with `-Params` `{"escort.pacifyShare": 0}` to see the
threats at all below d7, and `-Extra "-arkEscortVerbose"` for the end reasons and the stood cells.
