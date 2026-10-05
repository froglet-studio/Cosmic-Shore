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
- **Wearers** (§10) founded in the 400-465 u band, Charge hearts: the inner gap between the nucleus (392) and the
  Inner swarm band. The hearts roam the whole cell after that.

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
bash Tools/Build/builders_harness/run.sh            # all; or: fortress | thieves | wearers | exp
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
  - **`BuilderDeath.Stomach`** (round 11-9, `Docs/SWARM_FAUNA.md` §25) records that stomach at the moment of death, in
    all three cores. A birth in the SAME step can re-use the dead slot and overwrite `Stomach[slot]`; a ledger that read
    the array after the step booked the newborn's stomach instead. The showcase cell's first run had 9 such re-uses in
    2 minutes and a 231.8-volume builder residual; booking from the record closes it to 0 (showcase harness U4, with a
    negative control).

## 9. Files

- `Assets/_Scripts/Controller/Environment/FloraAndFauna/Builders/`: `BuilderCore.cs`, `BuilderColonyCore.cs`,
  `ThiefNestCore.cs`, `BuilderRegistry.cs`, `BuilderPrismWorld.cs`, `BuilderColonyConfigSO.cs`,
  `BuilderColonyFauna.cs`, with metas.
- `Swarm/SwarmMemberRenderer.cs`: additive `Upload` / `HideSlot` overloads over plain arrays.
- Collider ceiling with round 11b merged: 18 hearts + 960 swarm + 78 substrate + 84 builder proxies = **1140** /
  1200. With the threat grove (+20) and the wearer's proxies (+32, §10.4): **1,192 / 1,200**.
- `Tools/Build/builders_harness/`: `Arena.cs`, `Program.cs`, `run.sh`.
- `Tools/Build/swarm_glue_typecheck/`: the Builders files are added to the list, with stub additions.
- `Tools/Build/author_builders.py`, plus the hooks in `author_swarm_fauna.py`.
- Assets: the two prefabs, the two configs, the two cell species configs, and the Swarm cell spawn profile.

## 10. The WEARER: a creature made of what it took (round 11-10)

The showpiece of the research (PORT.md §4, `wearers.py`, DISCOVERIES "Species 4 - wearers" and round 2 "satiation
moult"). Small HEARTS steal prisms, mostly a pilot's trail, and WEAR them as a body. Each theft makes a body bigger.
Bodies that touch FUSE into one creature. Past a size, the thing skulking in your wake turns round, REARS (the body
contracts: the 1 s telegraph) and LUNGES with its body's prisms turned dangerous. Ram it and you strip your prisms back.
Hurt it fast and it MOULTS, shedding its outer layer back to whoever it was stolen from.

### 10.1 What runs

- `WearerCore.cs` is pure C# (no UnityEngine). It is the research's `Wearers` with the v3 contact rule and body cap.
  - **Body growth.** A stolen prism attaches to a free lattice site (s = 6 u) touching the body, with weight
    `nb^-alpha x exp(-(d - dmin) / 0.5)`: it sticks near where it touched. The frontier is kept incrementally.
  - **Phases.**
    - Thief: steal, preferring trail (x0.15 on squared distance), never within 90 u of a ship. It flees inside 70 u
      and skulks in the wake beyond 160 u.
    - At `HuntAt` (60) worn prisms, it becomes a hunter: approach (intercept), rear (1 s, squash 0.7), lunge
      (2.2 x 1.8 speed, 0.9 s), recover (2.5 s).
  - **Contact.** Every sensed vessel rams. A vessel strips each worn prism it touches BACK to its own domain (it falls
    loose; nothing is destroyed). A heart with fewer than 3 body prisms around it is exposed and dies to a touch;
    a proxied heart dies on the platform's own contact path instead (`PlatformBody`).
- **Satiation moult** (research v3). A body over 150 prisms (only while thief or approaching) sheds its outermost down
  to 90 prisms.
  - The shed prisms become a static LAIR: a built structure in `BuilderRegistry` that never moves and is never loot.
  - The first 10 volume of the shed prisms is EATEN and pays for a newborn heart (production gating, not a cull).
- **Hurt moult** (GAME). Losing a sixth of the body inside a 2 s decaying window (strips, weapons, a pilot's steal)
  sheds 30% of what is left `GiveBack` to its previous owners. The creature then slinks off as a thief.
- **Stomach.** A hungry heart eats the outermost prism of its own body (`Consume`). It starves only when its stomach is
  empty. There is no clock: an unfed heart dies between `0.6 x 30 / 0.01` and `30 / 0.01` s (W6).
- **One crystal per heart.** Hearts are ordinary colony members (`BuilderColonyFauna`, species `Wearers = 2`): GPU or
  prism-entity bodies, proxies near a vessel, every death through a proxy's sealed `Die`. The worn body is NOT
  lifeform tissue: it is stolen mass. When a leader dies, its body falls loose (still the colony's domain) and its
  riders split off.
- **The one predicate.** A heart steals a prism only if it is alive, unshielded, not its own domain, unclaimed and
  `Loose`. `Loose` excludes built, carried and worn prisms, living tissue and grove tissue
  (`ThreatGrove.IsGroveTissue`, merged from 9261700ee). A worn prism that becomes shielded, changes domain or dies
  leaves the body that tick.

### 10.2 The body as ONE thing (`BuilderPrismWorld : IWearWorld`)

- `Wear(h, creature, local)`:
  - the prism leaves the carry list;
  - `BuilderRegistry.MarkCarried` (no other colony takes it);
  - it is parented under its creature's container (a root GameObject, never the anchor's child, so tearing the anchor
    down never takes stolen prisms with it) at the next `PoseBody`, keeping its own rotation and scale.
- `PoseBody` (once per creature per tick) records the target pose.
- Per FRAME (`Animate` → `PoseWorn`):
  - each container gets ONE `SetPositionAndRotation` (interpolated). The worn prisms' colliders ride the hierarchy:
    no per-prism transform write.
  - Every worn prism's world matrix is `container x cached local TRS` (no per-prism transform read). They all go out
    in ONE `PrismRenderService.SetTransformsBatch` and ONE `PrismSpatialIndex.UpdatePositionsBatch` (Burst; only an
    8 u bucket crossing pays a hash-map remove and add).
- Per-prism transform writes happen only:
  - when the body's squash changes by more than 0.01 (rear, lunge, recover: PORT's per-phase notify);
  - on wear;
  - on unwear (back to its original parent, `NotifyPositionChanged`).
- `SetBodyDanger` is event-driven, twice per lunge. It runs `MakeDangerous` on every worn prism, and back again by
  clearing `IsDangerous`, restoring the speed debuff and calling `DeactivateShields` (the plain look).
- Fusion moves a prism between bodies with no unwear.

### 10.3 Proof (`bash Tools/Build/builders_harness/run.sh wearers`, `WearArena.cs`)

Setup: the research's run_mixed cell (1,500 prisms; a racer and a wanderer laying trail at 0.25 s / 6 volume; plus a
ramming hunter in W1b). There are 16 founders, the GAME colony. Each run is 5 min at 10 Hz, over 4 seeds.

| Test | Result | Research |
|---|---|---|
| W1 body from stolen mass | every seed past 60 prisms (max 120-180); 69% of worn steals were trail; 59 fusions | trail 0.74-0.86 |
| W1 hunts | 107 lunges, 48 hits; **2.40 hits/min**; telegraph exactly 1.00 s | wanderer 2.1-2.56/min, 1.15-1.42 s |
| W1 budget | never more than `WornCap` 300 worn prisms | |
| W1b ramming hunter | strips 353, 13 hurt moults, kills all 16 hearts in 5 min (3.2/min) | 13.3 kills/min vs 40 hearts |
| W2 cost (sim, 10 Hz) | prism writes 987/s, rebuckets 901/s, container writes 17/s, queries 1.8/s; core 0.013 ms/step, p95 0.063 ms | 2,085-2,529; 1,700-2,800; 31-44; 12-13 |
| W3 satiation moult (cap 50) | 3-4 moults a run, lairs of 85-119 prisms, a birth per moult | 1-3 births, lair 106-266 |
| W4 hurt moult returns mass | body 50 → 9 stripped + 12 shed, all alive, loose and in the pilot's domain; live volume unchanged | |
| W5 exposed heart | a bare heart touched by a ramming vessel dies (one death) | |
| W6 starvation | deaths 2027-2939 s against stomachs 1800-3000 s | |
| W7 shields | 0 shielded prisms worn or moved | |
| Mass audit | 0 in every run | 0 |

### 10.4 Budget, stated honestly

- **Colliders.** The worn body adds **0**: every worn prism is the platform's own prism with its own collider.
  - The hearts' proxies add `2 x 16 = 32` (MaxProxies 16).
  - Engaged worst case: 18 hearts + 1,038 swarm and substrate + 116 builder (48 fortress, 36 thieves, 32 wearer) =
    **1,172**; with the threat grove's +20, **1,192 / 1,200**.
  - `author_swarm_fauna.py` gates this sum.
- **Moving prisms vs PORT.md's 2,085-2,529 prism writes/s.**
  - The research cost was one managed position write per worn prism per 10 Hz tick. The harness measures 987/s in
    the GAME colony: 16 founders, bodies capped at 150 and 300 worn per colony, against the research's 40 hearts.
  - In the game, the per-prism work per FRAME is one matrix multiply, plus one entry in each of two batched Burst
    writes: at most 300 x 60 fps = **18,000 batched entries/s** in 2 calls a frame.
  - Managed transform writes are about 1 per creature per frame. Squash writes (≤ 300 per lunge cycle frame) happen
    only in rear, lunge and recover.
- **Unmeasured: physics.** Moving a parent of static colliders makes Unity re-sync every moved collider's pose to
  PhysX each frame the body moves: at most 300 static-collider moves a frame. A kinematic `Rigidbody` on the container
  would make that one compound actor move. It was NOT taken: it would make every worn prism a kinematic trigger that
  fires against the cell's static prisms, a gameplay change nobody has measured. QA-SWARM-ROUND11-10 profiles it.
- **The ladder** (item 1 of this round). Builder member bodies are `BindVirtualMass` entries, so `LiveVolume` counts
  them. `author_builders.body_volume()` puts every colony's member cap x |1.4 x 0.9 x 2.6| into
  `author_swarm_fauna.ladder()`, beside the substrate's bodies: 294.8 volume. FrenzyEnterVolume moves from
  448,000 to 449,000.
  - Worn prisms, walls, hoards and lairs are the cell's own mass changing hands, so they are not added.

### 10.5 What is NOT proved

- Nothing has run in the editor (QA-SWARM-ROUND11-10).
- Physics cost of moving static colliders under a container.
- Whether `MakeDangerous` / `DeactivateShields` on a stolen trail prism reads right in every theme.
- A pooled prism returned while worn: the pool re-parents it. The core drops it the tick `Alive` reads false. A
  pooled prism that still reads alive for a tick is posed one extra frame.
- Every sensed vessel counts as ramming, so a lunge that lands also strips its own body a little (a hit costs the
  creature). The research's wanderer did not ram.
- A relentless ramming hunter wipes the 16 founders out inside 5 minutes (W1b). That is the counterplay working;
  whether it is too easy is a playtest question.

### 10.6 Files

- `Builders/WearerCore.cs` (new, meta owned by `author_builders.py`);
- `BuilderPrismWorld.cs` (`IWearWorld`);
- `BuilderColonyFauna.cs` (`Wearers` species);
- `BuilderColonyConfigSO.cs` (`Wearers = 2`, the wearer fields, `ToWearerParams`);
- `Tools/Build/builders_harness/{WearArena.cs, Program.cs W1-W7, run.sh}`;
- `swarm_glue_typecheck` (WearerCore + stubs);
- `author_builders.py` (the species, `body_volume()`);
- `author_swarm_fauna.py` (the ladder);
- assets: `BuilderWearer.prefab`, `WearerConfig.asset`, `Swarm Wearer Builder Fauna Config Data.asset`, the spawn
  profile entry, and the Swarm Cell Config ladder.

