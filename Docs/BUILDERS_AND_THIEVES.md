# Builders and Thieves: creatures that steal prisms and build with them (Round 11e)

Linked from `Docs/SWARM_FAUNA.md` §23. The research is on the research branch: `Tools/Ecology/builders`
(`PORT.md`, `run_fortress.py`, `run_defend_vs_mend.py`, `test_rules.py`) and `bestiary/species/thief.py`.
QA: `Docs/QA/QA_BACKLOG.md` QA-SWARM-ROUND11-5.

This round adds two species. Both take prisms that already exist and give them a new owner:

- **The fortress colony.** Workers steal loose mass and wall their brood chamber in with it. When a wall is cut, it
  knits shut. A colony's nest is its peaceful phase: the same rule, before anyone has attacked it.
- **The thief nest.** Magpies nest on a plant, tail a passing ship, snatch its warm wake and fly it home to a
  visible hoard.

Neither species creates or destroys a prism. Every brick and every hoarded prism was a pilot's trail prism, a
skeleton or loose cell mass. It keeps its own collider, its own spatial-index entry and its own render entity, so
**a structure adds no colliders** (the net impact is 0).

## 1. Numbers (research defaults, shipped)

| | Value | Source |
|---|---|---|
| Fortress workers | 48 founders, cap 48, 70 u/s, sense 220 u, 1 in 4 re-forage a step | `run_fortress.py` |
| Lattice | spacing 8 u, 33³ sites; template shell `Rc 40, w 7`; cement K 0.6; nucleate 0.02 | `PORT.md` §1 |
| Mending | alarm + gap (both), gap gain 2.5, alarm gain 1, scar gain 3 | round 1 / round 2 |
| Defence | alarm radius 110 u, strike at 0.6, **defender caste 0.3** | round 3 (`run_defend_vs_mend.py`) |
| Thieves | **6 founders**, cap 18, free 150 u/s, **laden 75 u/s**, warm wake 1.5 s, spot 700 u, scout 400 u | `thief.py` (+ GAME founders) |
| Stomachs (GAME) | worker 40 / 0.02 per s; thief 20 / 0.02 per s active, **0.004 per s roosting** | this round |

**The defender caste.** The 6.4 s "a cut wall knits shut" headline is round 1's, before workers defended. With
round 3's defence code, every idle worker answering the alarm slows repair to t50 17.8 s. A 30% caste brings it
back to 5.6 s. The shipped `DefendCaste = 0.3` is that recommended fortress, and the dial between "mends" and
"stings".

## 2. Shared colony machinery (`FloraAndFauna/Builders/`)

### 2.1 Pure cores (no UnityEngine; headless-tested)

| File | Role |
|---|---|
| `BuilderCore.cs` | `IBuilderWorld` (the platform verbs a colony may use, on integer mass handles), `BuilderVessel`, `BuilderDeath`, `BuilderRng`, `BuilderStomachParams`, `BuilderMath`. |
| `BuilderColonyCore.cs` | The fortress. Struct-of-arrays workers (the `SwarmSortCore` shape), a dense 33³ lattice with `occ`, `q`, `cement`, `alarm` and `scar` fields, and the one predicate. |
| `ThiefNestCore.cs` | The thief nest: claim book, tail, snatch, homing, hoard, larder, roost and torpor. |

**The one predicate** (`BuilderColonyCore.IsStealableForMe`, also the thieves' `Wanted`). A prism qualifies when all
of these hold:

- it is live;
- it is **not shielded or super-shielded** (shielded mass is never a target or food);
- it is not the colony's own domain;
- it is not built by ANY colony, and not carried by anyone (`BuilderRegistry`);
- it is not living tissue (a flora or fauna body prism);
- it is inside the forage band.

**Pickup** is `Prism.Steal(colonyName, colonyDomain, superSteal: false)`.

**Carry** writes the transform and calls `NotifyPositionChanged` (the mover contract), interpolated every frame
between 10 Hz ticks. The carried prism keeps its collider. A vessel that rams a carrier, or its prism, kills the
worker, which drops its heart crystal, and the prism falls loose where it is.

**Deposit** follows claim-before-place:

1. `PrismSpatialIndex.TryReserve(site, 3.6)`.
2. The FINAL pose is written in the same frame, so collider, index and volume are final immediately.
3. The 0.35 s hop from the grip is a clock-stamped flight: `PrismRenderService.StampFlight`, with velocity
   `(site - grip)·π/(2T)`, `EncapsulateBoundsPoint` for the muzzle, and `ClearFlightStamp` on arrival.
4. `ReleaseReservation`.

This is animation, not live data.

**Upkeep.** A built prism that dies or changes domain (rammed, shot, stolen back by a pilot) frees its site and
raises alarm there. Laden workers climb the alarm gradient, and the gap rule fills surrounded holes first.

### 2.2 Ownership: `BuilderRegistry`

A static registry keyed by `(colonyId, Vector3Int site)`, like `SchwarzPTileRegistry` and
`GyroidOctagonRegistry`. A fortress keys bricks by lattice coordinate; a hoard keys prisms by a negative id. It
also holds a set of the prisms in someone's grip. Because there is ONE registry, colonies never strip each other's
structures and never fight over a prism in mid-air. When a colony is destroyed, `ReleaseColony` leaves its walls
and hoard standing as ordinary loose mass (nothing pops).

### 2.3 The game world: `BuilderPrismWorld`

`IBuilderWorld` over `PrismSpatialIndex.QuerySphere` (real prisms only; virtual entries never appear there).

- **Handles.** A handle is a table slot keyed by `(Prism, prismProperties.TimeCreated)`. A pooled prism that
  returns as new mass gets a new handle.
- **Dead handles.** A dead, unpinned handle is quarantined for 6 s before reuse, so no core still aims at it.
- **Eating** is `Prism.Consume(mouth, colonyDomain, colonyName, byCreature: true)`.
- **Recapture.** `Steal` back to whoever it was taken from.
- **Raid.** `Steal` to the raiding vessel's name and domain.

### 2.4 The anchor: `BuilderColonyFauna : Fauna`

The anchor is heartless. Like the swarm, it is a population, not an animal, so `Predated` returns false. The
species config's element names the members' hearts. Each colony ticks at 10 Hz in `Update`. Its parts:

- **Member bodies are prism render entities (round 11a §19.2).** This is the substrate's path, through the swarm's
  `SwarmEntityLedger`. The body is created in one `CreateBatch`, wears its tier's material in the colony's domain
  (from the theme's painted set), and is restyled with `SetLooksBatch` when a striker turns danger. Each frame,
  `SwarmBodyPose` computes the matrices and one `SetTransformsBatch` writes them.
  - Hearts stay on the swarm's instanced `SwarmMemberRenderer` (`DrawBodies` off), through a new additive
    `Upload(instances, heartIdx, heartStart, heartCount)` overload.
  - When the service is off or a material is missing, the instanced body draw returns.
- **Members are index entries (round 11a).** While a member is only data, its body is a `PrismSpatialIndex`
  virtual entry, kept by the swarm's own `SwarmEntryLedger`. The entry is registered at birth, pushed in bulk
  each tick, suspended while a proxy body is real, and released at death. It is bound to the cell's volume sum
  (`Cell.BindVirtualMass`). The colony is an `IVirtualFaunaOwner`, so weapons, the AOE pass and predators
  (`VirtualFauna.NearestPrey` / `PreyInReach`) find and materialise a far member like any swarm member.
- **Proxies.** A member within `EngageRadius` of a vessel gets a real `SwarmTadpoleFauna` proxy (heart + one body
  prism) through `Bind(cell, swarm: null, ...)`. Fortress colonies hold at most 24 proxies, thief nests 18.
- **Every death goes through a proxy's sealed `Die`.**
  - Contact is split. A member with a READY proxy (`PlatformBody[k]`) takes a vessel's contact through its real
    body: its danger plate stings, and its body prism breaks the platform's way. A member without a proxy, or one
    whose carried prism is rammed, is killed by the core's distance test, which materialises a proxy and calls
    `Jousted(vesselName)`.
  - If the joust is refused (newborn grace), the proxy withers instead (`Starve`).
  - An empty stomach withers (`Starve`).
  - A death the platform runs on a proxy (weapon, joust, predator) is polled every frame and reported to the core
    as `BuilderDeath.PlatformKill`. A knocked-down thief's prism then goes back to its pilot.
  - Every member drops exactly one crystal (its own heart). Nothing pops.
- **Colour.** The colony wears the domain the cell spawned it in (the controlling domain). Its walls and hoard are
  stolen history, so a cell's one-colour re-colour does not repaint it (`AcceptsTeamRecolour => false`).
- **Vessels** are sensed by `Physics.OverlapSphereNonAlloc` with `NonPrismOverlapMask`, to `IVesselStatus`.
- **Profiling.** `ProfilerMarker`s: `BuilderColonyFauna.Tick`, `.Tick.SenseVessels`, `.Tick.Step`, `.Tick.Proxies`,
  `.Tick.Upload` and `.Frame`. One `CSDebug.LogVerbose(CSLogChannel.Ecology)` line is written at founding.

## 3. Ecology laws, as built

| Law | How |
|---|---|
| Mass conserved | No timers, TTLs or cullers. The only exits are eating (`Consume`, through the food web) and the platform's own weapons and rams. Harness audit: `created - live - eaten - destroyed = 0` in every run. |
| No imposed death | Death comes from vessels, weapons, predators, or an **empty stomach**, never a clock. B6: unfed workers die exactly when their stomachs empty (1212-1998 s against a predicted 1200-2000 s). |
| One crystal per lifeform | Each member carries its own heart, released by the sealed `Fauna.Die` on its proxy. |
| Controlling-domain spawn | The anchor's `domain` comes from the cell spawner. Nothing here prescribes a colour. |
| Shielded mass is never food or a target | The predicate, plus a second guard in `BuilderPrismWorld.Steal`. B3 and T5 assert 0, and a negative control (a world that hides shields) is caught. |
| Collider budget | Structures add 0. Proxies are 2 colliders each, only near vessels: 24 + 18 → **84**. The Swarm cell's ceiling check (`author_swarm_fauna.py`) now counts them: 1140 worst case (with round 11b's substrate) against a ceiling of 1200. |
| Stakes | A striking fortress worker is a danger-tier body prism. An opposing-domain pilot who hits it burns petals (round 10's rule). The telegraph is the guard screen forming between the colony and the vessel before the strike. |

**Worker heart budget.** The colony is heartless. A fortress holds at most 48 hearts and a nest 18. They are
GPU-drawn with no collider until a proxy forms (at most 24 or 18 at once).

## 4. The thieves' opening-transient fix (the living cell's failure)

In the living cell, 18 thieves seeded at full strength starved during the opening flora crash, and 3 of 4 seeds
were extinct by about 35 min. Three model changes fix it without adding a dial:

1. **Founded small, then bloom.** The nest starts with 6 thieves. Births are paid from the stomach
   (`BirthCost 8`), and the cap of 18 is only a backstop.
2. **The hoard is the larder.** A hungry thief at the nest eats a hoarded prism. Its food is stolen trail, not the
   flora the herbivore boom strips.
3. **Roosting torpor.** With no ship in sight, a thief roosts at 0.004/s (one-fifth of its active rate), so an
   empty opening costs almost nothing.

## 5. Counterplay

| Against | Do |
|---|---|
| Fortress | Cut the wall. Workers re-steal **your own trail** from the breach (95% of repairs come from the cutter's trail). Kill workers to slow it, or keep cutting the same line: scar tissue grows the wall thicker there (B4). |
| Thieves | **Turn back.** A laden thief flies at half its free speed, so you always catch it, and knocking it down returns its prism to you. **Weave** so your wake is not where they expect it. Or **raid the hoard**: all your stolen mass sits in one place, and a vessel touching a hoarded prism takes it. |

## 6. Where they live (`Tools/Build/author_builders.py`, `--check`)

They live in the Swarm cell, off the swarms' bands (Inner 470-620, Middle 690-840, Outer 910-1080):

- **Fortress colony** in the 845-905 u band, Mass hearts.
- **Thief nest** in the 1085-1140 u band, Space hearts. It perches on the nearest living plant within its scout
  reach, which is the Outer band's Space flora.

`author_builders.py` owns these files:

- the Builders script metas;
- the two anchor prefabs (`BuilderFortressColony.prefab`, `BuilderThiefNest.prefab`);
- `Assets/_SO_Assets/Builder Colonies/{FortressColonyConfig,ThiefNestConfig}.asset`, written from
  `BuilderColonyConfigSO`'s own C# defaults plus per-species overrides;
- the two species configs in the Swarm cell.

`author_swarm_fauna.py` lists the species configs in the cell's spawn profile, counts their proxies in its collider
ceiling, and leaves them out of its stale-file sweep.

## 7. Proof (headless)

Run with a private `TMPDIR`. Shared `/tmp` races with other workers' harnesses.

```
export DOTNET_ROOT=/usr/lib/dotnet TMPDIR=<private dir>
bash Tools/Build/builders_harness/run.sh            # all; or: fortress | thieves | exp
bash Tools/Build/swarm_glue_typecheck/run.sh        # cores + glue against the stubs (netstandard2.1, C# 9)
python3 Tools/Build/author_builders.py --check
python3 Tools/Build/author_swarm_fauna.py --check
```

`builders_harness` is a C# port of the research arena (`Arena.cs`, an `IBuilderWorld` over arrays) that drives the
SAME core files the game compiles. Its asserted results:

| Test | Result | Research |
|---|---|---|
| B1 wound knit, alarm + gap, t50 | **5.5 s** (≤ 8) | 6.4 (r1) / 5.6 (r3 caste) |
| B1 ordinary building, t50 | 23.8 s (≥ 15); none/both 4.4x (≥ 3) | 39.5; 6.2x |
| B1 alarm only / gap only | 10.0 / 16.6 s | 6.8 / 15.8 |
| B1 every worker defends (caste off) | 15.3 s (> 1.5x the caste's) | 17.8 vs 5.6 |
| B1 repair from the cutter's trail | 95% (≥ 85%) | 98% |
| B1 cost | 0.079 ms/step mean, 0.22 ms p95 (48 workers, 10 Hz); 183 carried-prism writes/s; 51 queries/s | |
| B2 game colony (stomachs, births, `TryReserve`) | t50 5.7 s, no starvation in a fed cell, audit 0 | |
| B3 shields | 0 shielded prisms taken; the negative control is caught (14 taken) | 33 in the control |
| B4 scar tissue on one cut line | +58 vs +33 without | 65→113 vs 65→86 |
| B5 a wall breached by stealing | re-filled, t90 6.2 s | 12.5 |
| B6 starvation is an empty stomach | deaths at 1212-1998 s against a predicted 1200-2000 s | |
| T1 steals/min vs a wanderer | 24.0 (15-90) | 39 |
| T1 snatch window | every claim ≤ 1.50 s old | 1.5 s |
| T1 laden speed | max **75.0 u/s** (half of 150) | |
| T2 knock-down returns the prism | yes; recaptured 8.3 a run; unit test (domain and volume back) | 1.9 |
| T3 cold ablation | 11.2 vs 24.0 steals/min | 10.9 vs 39 |
| T4 empty 10 min opening | 0 starved | |
| T4 founded nest vs full colony, first minute | 39% (≤ 60%); blooms (births); rate grows | |
| T4 45 min, a ship 2 min in every 6 | no seed extinct (6/8/6/9 alive) | 3 of 4 extinct |
| T5 shielded trail | 0 snatched | |
| Mass audit | 0 in every run | 0 |
| Thief cost | 0.012 ms/step | |

## 8. What is NOT proved

- Nothing here has run in the Unity editor. The glue is type-checked against hand-copied stubs, which catch a
  wrong member or signature, not a changed one or runtime behaviour. See QA-SWARM-ROUND11-5.
- **Platform interplay.** The harness does not model:
  - whether a vessel's contact with a member's proxy body prism kills it in every vessel class (the swarm relies on
    the same path);
  - what a vessel's own collision does to a carried or built prism (explode, steal, pass through) in each vessel
    class;
  - whether `StampFlight` reads well at 0.35 s;
  - the proxy materialise budget under a dense AOE.
- **The cell spawner** is assumed to place each anchor inside its band, in the controlling domain.
- **Thief nest placement.** Perching on a plant is a placement, not a binding. If the plant dies, the nest stays
  where it is.
- **Unstated volume.** Food in a member's stomach (eaten volume not yet spent) is not stated to the cell's volume
  sum. Round 11a retired `SetVirtualVolume`, and the swarm does not state its stomach either.

## 9. Files

- `Assets/_Scripts/Controller/Environment/FloraAndFauna/Builders/`: `BuilderCore.cs`, `BuilderColonyCore.cs`,
  `ThiefNestCore.cs`, `BuilderRegistry.cs`, `BuilderPrismWorld.cs`, `BuilderColonyConfigSO.cs`,
  `BuilderColonyFauna.cs`, with metas.
- `Swarm/SwarmMemberRenderer.cs`: additive `Upload` / `HideSlot` overloads over plain arrays.
- Collider ceiling with round 11b merged: 18 hearts + 960 swarm + 78 substrate + 84 builder proxies = **1140** /
  1200.
- `Tools/Build/builders_harness/`: `Arena.cs`, `Program.cs`, `run.sh`.
- `Tools/Build/swarm_glue_typecheck/`: the Builders files are added to the list, with stub additions.
- `Tools/Build/author_builders.py`, plus the hooks in `author_swarm_fauna.py`.
- Assets: the two prefabs, the two configs, the two cell species configs, and the Swarm cell spawn profile.
