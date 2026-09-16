# Cleave — Technical Documentation

> **Naming.** `GameModes.Cleave = 39` is the code/data/enum identity. The mode has been renamed
> TWICE — `Ribcage` → `PeelTheCage` (2026-09) → `Cleave` (immediately after, when three of its four
> intensities stopped being cages). Enum VALUES are pinned forever, so both historical names
> resolve to `Cleave` in ONE hop in `GameModeRenameMigration`; a chained map would strand the
> oldest saves on a name nothing reads. Asset GUID seeds inside
> `Tools/Build/author_cleave_assets.py` still say `Ribcage` **on purpose** — a seed is identity,
> not a name.

## Overview

Cleave is the **Rhino-only slicing race**. Domains race to be first to **destroy 2,000 hostile
prisms** with the sword, and the arena *is* the score — cutting it apart and winning are the same
act.

**Intensity is WHICH PLACE you cut, not how much of it there is.** The four intensities are four
unrelated arenas with four different verbs, built by four different generators:

| i | arena | the verb | prisms | volume | danger | far reach |
|---|---|---|---:|---:|---:|---:|
| 1 | **The Panes** | commit to a line | 11,021 | 2,806,755 | 170 | 372 |
| 2 | **The Swell** | read the grain | 13,738 | 2,433,470 | 461 | 349 |
| 3 | **The Cage** | peel inward | 14,731 | 2,919,695 | 428 | 375 |
| 4 | **The Twistbands** | roll the blade | 16,423 | 4,058,255 | 228 | 357 |

This replaced a ladder that was **2 / 3 / 4 / 5 nested shells** — the same arena four times, and
the reason the mode was renamed. The three-rind cage is the only rung kept, and it is kept
**byte-identical**: same generator, same seed, same 14,731 prisms, same count thresholds.

> **Player-facing unit is PRISMS, never "bars" or "plates".** Every number a player reads counts
> PRISMS — the scoreboard, the reveal, and any toast copy. Two words for one counter reads as two
> counters.

**One axis.** Destruction is the race: `HostilePrismsDestroyed`, the same platform stat Rampage
runs on and the same 2,000 target. Scoring mass is everything that is not your own team's laid
trail — the arena (environment mass, non-roster owner ⇒ hostile whatever colour it wears) and
rival trails. Your own and your teammates' trails never score, so there is no lay-and-smash
farming loop.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCleave.unity`
- **GameMode enum**: `GameModes.Cleave = 39`
- **Controller**: `CleaveController : MultiplayerDomainGamesController` — structural sibling of
  `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner detection in
  `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus progress milestones and the AI
- **Scoring**: `CleaveScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`; golf-timed)
- **Turn monitor**: `CleavePrismTurnMonitor` → `EndConditionOverridesSO.GetCleavePrismTarget()`
  (default **2000**, FrogletTools ▸ Game Modes ▸ End Game Conditions — never a per-scene field)
- **Domains**: `MinDomainsAllowed = 2`, `MaxDomainsAllowed = 3`; players **2–4** with AI backfill
- **Vessels**: **Rhino only**, enforced in two places (see "Vessel lock")

## The four arenas

Each is a `CellEnvironmentSpawnableBase` subclass under
`_Scripts/Controller/Environment/MiniGameObjects/`. They share nothing but the envelope and the
prism palette; each one exists to ask the sword a different question.

### 1 · The Panes — `SpawnablePanes`

Nine flat slabs cutting the cell at nine authored angles, each a **corduroy of parallel ribs** with
its own grain direction. Planks overlap along a rib (step 15 under length 17) and ribs sit 21 apart
across it, so a pane is something you fly *through* as much as *at*.

It is intensity 1 because a plane is the most forgiving surface in the mode. A shell curves away
from you — you cross it perpendicular and you are through, or you run along it and it bends out
from under the blade. A plane does neither: line up with it and the mass stays where you left it
for the full 720 units of its diameter. The pilot's first discovery is the one the whole mode is
built on — **a sword rewards commitment to a line**.

- **Panes are OFFSET, and not all by the same amount** (−0.55…+0.5 of the radius). Nine planes
  through the origin would pile every intersection into one knot at the middle and leave the rest
  of the ball empty.
- **Mullions are the prize.** Where two panes cross, a beam is laid along the intersection: the
  densest *and* straightest run of mass in the arena, so the best cut available is to find one and
  fly it. The line is exact analytic geometry (the closest point on the line of intersection to the
  cell centre, then one Pythagoras for the chord), not a search.
- **Rims carry the only traps** — the frame around each pane's disc, which is exactly what you clip
  when you misjudge a pass.

### 2 · The Swell — `SpawnableSwell`

Seven great **corrugated sheets** stacked through the cell at seven angles, each rolling on its own
wavelength (78 → 240, a 3× span) with a weaker second swell running *along* the ridges so the
troughs themselves rise and fall.

Where the panes teach commitment, the swell teaches the thing that makes commitment interesting:
**a surface has a GRAIN, and the line only pays if you read it.** Fly a trough and the deck stays
level under the blade for its whole length — the longest uninterrupted cut in the mode. Fly the
same sheet ninety degrees off and you climb and drop through every ridge, clipping a handful of
prisms per crest and nothing between. Same sheet, same speed, several times the score.

Four things make that legible rather than merely true:

- **The grain is PAINTED.** Gold on the crests, Jade in the troughs, Blue on the flanks — the
  corrugation reads as colour banding from across the arena, before the relief is visible. (Scoring
  does not care: every non-roster prism is hostile to everyone, in any colour.)
- **The crests are the traps.** The only danger prisms here ride the ridge lines, so the grain is
  worth reading twice — the trough is both the richest cut and the safe one.
- **Sheets have an UNDERSIDE.** A zero-thickness surface is invisible edge-on and a blade can cross
  the plane of it without touching anything. A half-density reef hangs 13 below each sheet with its
  planks turned crosswise.
- **Sheets FRAY rather than ending.** The void threshold ramps up past 78% of a sheet's radius, so
  a sheet dissolves into open water instead of stopping at a hard rim. The panes are framed slabs
  and say so; the swell is weather.

### 3 · The Cage — `SpawnableRibcage` (the kept arena)

Three concentric hollow rinds of prism bone at radius **360 / 295 / 230** — meridian ribs, latitude
hoops, a diagonal through every cell, joints at every crossing, two polar crowns. Unchanged from
the mode that was named after it.

- **The openings are TRIANGLES.** Every rib × hoop cell carries one diagonal with an alternating
  lean, so the weave reads as a truss rather than as rounded bubbles.
- **It tightens inward.** `DensityStep` (1.05) compounds with the shrinking radius: cells run
  **94u → 74u → 56u**. The last layer is the hardest to slip through, not the easiest.
- **Every inner rind is TILTED onto its own axis** (`ShellTilts`, pole axes ≥34° apart), because a
  latitude-hoop sphere is densest at its poles and stacked caps would collapse the match into
  "everyone drills the top".
- **No free corridor** — `ShellLonOffsets` phases each rind so the gaps never line up radially.

### 4 · The Twistbands — `SpawnableTwistbands`

Three interlocked **Möbius ribbons** on the three coordinate planes, each carrying a solid plated
deck, a crosswise keel beneath it and a chunky cornice along its edge.

The other three arenas are made of surfaces that hold still — a pane is a plane, a swell ripples but
keeps its plane, a shell curves the same way everywhere. A Möbius band does something none of them
do: **its surface rotates about its own direction of travel as you fly it.** Hold a blade against
the deck and the deck turns out from under it, so the cut only continues if the pilot keeps rolling
the sword to match — which is exactly the axis the Rhino's triggers drive (RT−LT is yaw *and* roll,
`RHINO_SHIELD_SWIPE.md`). This is the one arena that asks for the swordsmanship rather than the
line, which is why it is intensity 4.

It is also the hardest to read: the bands carry an ODD number of half twists (1 / 3 / 5) and are
therefore genuinely one-sided, so flying a lap returns you to your own starting patch upside down —
mass you already cut is now above you. There is no global up and no silhouette to peel inward
through; you navigate by the ribbon.

- **The band frame is `SpawnableOurobor`'s, deliberately unchanged** — the width direction rotates
  `TwistRate · u` out of the loop plane (that IS the twist), and `AlongSurface` is the exact partial
  derivative rather than the loop tangent, so a plate near an edge is square to the surface it is
  actually on. What differs is what rides on it: Ourobor grows countryside on a band you LAND on;
  this lays a deck you CUT.
- **Lanes are painted ACROSS the ribbon**, so the deck reads as a road — and because the band is
  one-sided, the lane order MIRRORS after a lap. The arena's joke is something the player can see.
- **The cornice is ONE curve and needs `u` to run 0…4π to close.** That is the band's own proof of
  one-sidedness (after 2π the `+HalfWidth` edge has become the `−HalfWidth` edge), and it carries
  every trap in the arena — the edge is what you clip when the roll does not keep up. Halving that
  range would draw half an edge and leave the other half bare, which is the shape of bug that looks
  like a content gap.

## Nothing is shielded, and the reason is the AI

Every prism in every Cleave arena is `PrismKind.Plain` except the sparse `PrismKind.Danger` traps.
Nothing is `Shielded` and nothing is `SuperShielded`. The old doc gave one reason — a super-shielded
prism is fully invulnerable to `Prism.Damage`, so enough of them could put the destruction target
out of reach. There is a second reason, and it is the stronger one:

> **A super-shielded prism can only be popped by an ENERGIZED blade; energizing requires holding
> the both-triggers chop stance; and `AIPilot` never pulls a trigger.** (`RHINO_ENERGY_SWORD.md`
> states this outright: *"AI never pulls triggers, so AI Rhinos never energize"*.) Shielded mass is
> therefore mass an **all-AI domain can never score against**, in a mode whose entire score is
> destroying it — the Tollway rule, that an AI which cannot play is a defect.

This is asserted, not just documented: `cleave_budget.verify` fails the build if any arena emits a
shielded prism of either tier. If the mode ever wants hardened mass, it needs an AI that can
energize first.

## The shared envelope

All four arenas are built to one radius — `SliceArenaGeometry.OuterRadius` (**360**) — because
three systems are sized against it and none of them can be told which intensity is running:

```
arena 360  <  AI station 468 (360 × 1.3)  <  spawn ring 576  <  membrane 1200
```

- `CleaveController` parks its AI stations at `OuterRadius × AiStationStandoff`. `AIPilot` has no
  arrive-and-stop behaviour, so a station *inside* the mass is a point the AI orbits from within
  forever — the "the AI just stays inside" defect, twice.
- The scene's `spawnRingRadiusFloor` puts players outside all of it (this cell has **no nucleus**,
  so the computed ring would otherwise collapse to the cell centre).

The ordering is asserted in `cleave_budget.verify`, and it is asserted on the prism's **far corner**
rather than on its lay point: a lay at exactly 360 still puts geometry outside 360. The measured
worst case is **375** (the cage's rim), comfortably inside the 468 station radius.

## How the numbers are measured

**`Tools/Build/cleave_arena_harness` compiles the SHIPPED arena generators and runs them.** Its
`run.sh` hands `csc` the four real `Spawnable*.cs` files from `Assets/` — they are not copied —
alongside a faithful shim of the `UnityEngine` and `CellEnvironmentSpawnableBase` surface they sit
on, and runs the result. (No `.csproj`: the repo gitignores `*.csproj` because Unity generates its
own, so a project file here would be untracked and the harness would not survive a clone. Same
shape as `Tools/Build/regatta_course_harness`.) It emits `cleave_arena_measurements.json`:
prism counts, exact volume, per-kind and per-domain breakdowns, and the far-corner reach.

Three of the four arenas cull prisms with value noise, so an analytic model would have to
re-implement that noise — and the float path into it — to be exact, and would silently become an
ESTIMATE the first time either drifted. Running the real code has no drift surface at all.

**The measurement is hash-guarded.** Every source that could move a count (the four generators, the
shared envelope, `PaintingStrokeToolkit.cs` for the noise, and the harness's own shims) is hashed
into the JSON, and `cleave_budget.load()` refuses to answer from a measurement whose sources have
moved. Re-measure with:

```
bash Tools/Build/cleave_arena_harness/run.sh
```

**Faithfulness is proven by a control:** the harness reproduces the cage's shipped **14,731** prisms
and its shipped count thresholds (15431 / 15231 / 18331 / 17731) exactly.

### The predecessor model was wrong by exactly 2×, and it mattered

`ribcage_budget.py` computed the per-prism jitter volume factor as
`((1.2)**4 - (0.8)**4) / (4*0.2)` = **2.08**. `Jit(s, 0.2)` draws `k ~ U(0.8, 1.2)`, so
`E[k³] = (1.2⁴ − 0.8⁴) / (4 × 0.4) =` **1.04** — the divisor used the *half-width* of the range
instead of its width. The file's own comment said 1.04.

Every shipped volume threshold was therefore ~2× the arena's real volume, and
**volume is the spine**: `CellPhaseThresholds.Compute` steps phase on volume and uses count only as
a Frenzy backstop. The cell's live volume sat permanently below `RestlessEnterVolume`, so the
authored ladder described a cell twice as heavy as the one that exists and never moved. The
measured baselines fix it.

⚠ **`Tools/Build/wildlife_cage_budget.py` still carries the identical expression** and is
deliberately NOT touched here — that is another mode's tuning, and correcting it makes Wildlife
Liberation's cell reach Restless/Frenzy *earlier*, which gates fauna release and so is a real
balance change rather than a pure bug fix. It wants its own pass.

## Intensity

The platform already has exactly one way for a cell to vary by intensity, and this mode uses it
rather than inventing a second:

```
Cell.AssignConfig                                     [Cell.cs]
  CellTypeChoiceOptions.IntensityWise
    → index = Clamp(gameData.SelectedIntensity - 1, 0, CellConfigs.Count - 1)
    → Cleave Cell Config 1..4, in that order
        → EnvironmentPrefab = SpawnablePanes / Swell / Ribcage / Twistbands
        → PhaseThresholds   = THAT arena's own MEASURED baseline
```

Each intensity needs its OWN `CellConfigDataSO` because `PhaseThresholds` must ride its own
baseline — the arenas run 11,021…16,423 prisms and 2.43M…4.06M volume, so one shared threshold
block would put three of the four cells in the wrong phase from frame one.

**Prism count is monotone across the ladder** (asserted). Nothing about four unrelated arenas forces
that, but a ladder whose size wandered up and down would make "intensity" mean nothing to a player
choosing a rung. **Volume is deliberately NOT monotone** — the swell's planks are smaller than the
panes' — and that is fine precisely because every cell carries its own thresholds.

## Collider budget

One box collider per prism, so the arena *is* the collider count: **11,021 at intensity 1 rising to
16,423 at intensity 4**, plus nothing else (no fauna, no flora in this cell).

**The whole ladder got lighter at the top**: the heaviest arena was 20,153 prisms and is now 16,423,
an **18.5% cut** to the worst case. `cleave_budget` asserts the heaviest arena never exceeds the
20,153 the mode has already shipped, so the ladder can only ever get lighter than a number that
already had a product decision behind it.

That said, intensity 1 is still ~7× the masterplan's ≤1,500 per-cell target and intensity 4 ~11×.
This remains the branch's headline performance risk. Mitigations are the standing ones (collider-LOD
by phase, no new physics queries — scoring rides the StatsManager SOAP channel and the AI aims
analytically), and the mode's whole verb actively removes colliders as the match runs. **Measure on
device before tuning.** The bluntest dial per arena is its step size (`RibStep`/`PlankStep`,
`PlateStepAlong`/`PlateStepAcross`, `DensityStep`); re-run the harness and the generator after any
change.

## The pipeline (zero bespoke tracking)

The stat was already plumbed platform-wide (Rampage runs on it); the mode only picks it and reads
it twice — once to end the turn, once to drive the milestones.

```
Rhino cuts a prism (one hit - plain prism)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed → HostilePrismsDestroyed++   (arena mass is non-roster ⇒ hostile;
                                                          your own team's trail is filtered out)
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ CleavePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached   [server]
  ├─ CleaveController.SampleProgress → leader + milestone rungs           [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team buff
              │  turn end
              ▼
CleaveController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

## Vessel lock

**Rhino only** (`ArcadeGameCleave.Vessels` has one entry), enforced in **two** places because one
was not enough:

1. `GameDataSO.SyncFromArcadeGame` clamps `selectedVesselClass` into the game's allowed set. This
   covers the machine that pressed Start, on every route (modal, rematch, Maelstrom chain).
2. `ServerPlayerVesselInitializer.ResolveSpawnVesselType` re-clamps **server-side at spawn**. This
   is the one that matters in multiplayer: `Player.NetDefaultVesselType` is an OWNER-write
   NetworkVariable that each client sets from its OWN local config and from the menu's
   vessel-changer toy, so a client walked in still wearing the hull it last flew — and
   `SyncFromArcadeGame` never runs on a client, while the config ClientRpc lands *after* the spawn.
   Symptom: **the client flew a Dolphin in Rhino-only Cleave** while the AI (whose class comes from
   the scene's `aiInitializeDatas`) correctly spawned Rhinos. The server is the only authority that
   sees every player's request and the mode's rules together, so the clamp belongs there — the same
   principle as never writing domain state from client code.

## Progress milestones

At a quarter and a half of the win target, the **leading** domain crosses a rung:
`CleaveController.SampleProgress` (server, every `progressSampleSeconds` = 0.5 s) →
`AnnounceMilestone_ClientRpc` → a `GameToastSituation` post plus `HapticController.PlayAlert()` on
every peer (~1.2 s of hard rattling — the game's **third** haptic feel, and the only thing that
fires it; see `Docs/HAPTICS.md`).

These are **pure feedback — they change no game state**, so a missed or late sample costs a toast,
never a rule. Rungs ride the leader's *own* progress rather than a cross-domain total so they land
at a fixed point in the race. A lead change after the first milestone posts `CleaveLeaderChanged`.

The two rung situations are `CleaveQuarterCut` / `CleaveHalfCut` — renamed from `…QuarterPeeled` /
`…HalfPeeled` with this branch, because "peeled" described ONE of the four arenas (you do not peel
a wave sheet). All three renames of these two values have been free for the same reason: no
`GameToastConfigSO` authors them yet, so nothing serialized points at any old name. Toast copy is
still unauthored, so **right now the shake IS the milestone feedback**.

## Spawning outside the arena

Players start on the computed cell spawn ring (`CellSpawnFormation`, all facing the cell), NOT on
authored transforms: the donor scene's four points sat at ±50, deep inside the arena.

**The formation is `EquatorialRing`, not the default `Symmetric`** — everyone evenly spaced on one
horizontal great circle. This was a fairness requirement for the cage (a latitude-hoop sphere is
densest at its poles, so a tetrahedral spread would drop two of four players onto the hard cap) and
it remains the right default for the other three, none of which is uniform about its poles either.

The ring normally measures off the cell's nucleus radius, and this cell deliberately has none — so
it would collapse to the cell centre. `spawnRingRadiusFloor` (**576**) gives the ring a floor for
exactly this case: a cell whose "core" is a structure rather than a nucleus. The number is owned by
`cleave_budget.SPAWN_RING`, which asserts it sits outside the AI stations and inside the membrane,
and the generator writes it into the scene from there.

## AI

**Every AI station is OUTSIDE the arena. That is the whole fix.** `AIPilot` has no arrive-and-stop
behaviour — it steers at `_targetPosition` forever and flies through on arrival — so *any* target
inside the arena becomes a point the AI loops around from within.

One station per strike, always at `SliceArenaGeometry.OuterRadius × 1.3`. Stations walk a
golden-angle spiral, so successive stations are ~137° apart and **the chord between them passes
close to the centre** — a full crossing of the ball. The loitering happens outside; the damage
happens on the transit.

**This needs no per-arena code, and that is a property of the chord rather than luck**: a line
through the middle of the ball cuts panes, sheets, shells and ribbons alike. Every AI is phased onto
its own arc so a full lobby spreads around the sphere, and every 4th strike is a RAID on
`Cell.GetExplosionTarget` (the densest mass hostile to its domain, i.e. opponents' trails), offset
by seat so they never all raid at once.

**Invariant for anyone re-tuning this:** `SliceArenaGeometry.AiStationStandoff` must stay **> 1**.

⚠ **An AI Rhino cannot energize its blade** (no triggers), which is fine here because nothing in any
arena is super-shielded — but it is the constraint that makes "no shielded mass" a rule rather than
a preference. See above.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.cleavePrismTarget`, 0 = default **2000**) — the number of hostile prisms a
domain must DESTROY to win, the same target Rampage races to. The field was `ribcagePrismTarget`
until this branch; both it and its Build twin were renamed with the mode.

> **⚠ Pacing flag — 2,000 is inherited, and it is now inherited across an arena change as well as a
> prism-kind change.** It was set when every bar was a two-hit shielded prism in a 14,977-prism
> cage. The bars are one-hit now, and three of the four arenas are new geometry nobody has flown.
> 2,000 is **18%** of intensity 1's 11,021 prisms and **12%** of intensity 4's 16,423. Expect
> matches to run short. This has not been playtested. It is one editor field, and the milestones
> follow it automatically; a per-intensity target would need a small change to
> `CleavePrismTurnMonitor`.

Comeback rate is `0.01`, so a quarter-of-target deficit (500) buys 5 element levels — comfortably
over the one-whole-level floor the arcade recipe requires.

## The fauna removal (2026-08)

The mode used to run a **fauna ladder**: a brood was penned inside the cage, the cell's controlling
domain was pinned to the race leader (`Cell.SetModeControlOverride`) so the brood hatched in the
leader's colours, and the untouched legacy herbivore diet turned it loose on every trailing team.
Fauna were **removed from the level on request**, so:

| removed | kept |
|---|---|
| The five fauna config assets and the spawn profile's `SupportedFaunas` | Every platform capability the ladder was built on |
| `ApplyStage`, `PublishLeader_ClientRpc`, `PublishRelease_ClientRpc`, the stage constants | `Cell.SetModeControlOverride` / `ModePhaseFloor` / `FaunaReleaseTier` / `FaunaContainmentRadius` / `ContainmentIntruderFrenzy` / `HasPreyInsideFaunaContainment` |
| `SpawnableRibcage.ContainmentRadius` | `SpawnProfileSO.InitialFaunaReleaseTier`, `FaunaConfigurationSO.ReleaseTier`, the batched fauna seeding, the shielded-grid fix |

The kept items are general, documented platform capabilities with no Cleave dependency — several
now have **no caller**, which is accepted for the same reason `ScoringMetric.PrismsRemaining` is
kept: churning a shared, serialized surface twice costs more than an unused-but-documented API.

## Shared-code touchpoints

| Site | Change |
|---|---|
| `GameModes` | `Cleave = 39` (was `PeelTheCage`, was `Ribcage`) |
| `GameModeRenameMigration` | `Ribcage` → `Cleave` and `PeelTheCage` → `Cleave`, each in ONE hop |
| `GameToastSituation` | `CleaveQuarterCut = 50`, `CleaveHalfCut = 51`, `CleaveLeaderChanged = 52` |
| `SliceArenaGeometry` | **new** — the one envelope all four arenas and the AI are built to |
| `SpawnablePanes` / `SpawnableSwell` / `SpawnableTwistbands` | **new** arena generators |
| `SpawnableRibcage` | reads `SliceArenaGeometry.OuterRadius`; its `ShellRadius` const is retired (the controller now reads the shared envelope) |
| `Cell` | `SetModeControlOverride` (+ live-swarm re-colour), `ModePhaseFloor`, `FaunaReleaseTier`, fauna containment, `NotifyBlockShieldStateChanged`, shielded mass excluded from the targeting grids |
| `HapticController` | `PlayAlert()` — the third feel, gate extended per `Docs/HAPTICS.md` |
| `GameDataSO` | `SyncFromArcadeGame` clamps `selectedVesselClass` into `SO_ArcadeGame.Vessels` |
| `ServerPlayerVesselInitializer` | `spawnRingRadiusFloor` — lets the computed ring serve a cell whose core is a STRUCTURE rather than a nucleus |
| `EndConditionOverridesSO` (+ window + asset) | `cleavePrismTarget` live/build/getter, default 2000 |
| `ElementalComebackSystem` | `GameModes.Cleave` shares Rampage's `ScoreDifferenceSource.PrismsDestroyed` case |

### The one cross-mode behaviour change: shielded mass leaves the targeting grids

`Cell.AddBlock`'s own comment already stated the rule — *"fauna must never be led to mass they
cannot eat"* — and applied it only to nucleus-interior mass. `Docs/ECOSYSTEM.md` §16.2 then removed
shielded prisms from every herbivore's **diet**, but they stayed in the **grids**, so density
centroids kept steering swarms onto mass the creatures had just been told they could not eat.

Shielded prisms are now excluded from the targeting grids at `AddBlock`, and
`NotifyBlockShieldStateChanged` re-files a prism when a shield engages or is shed. It strictly
*reduces* grid work and adds no query. **It affects two other modes and both are improvements** —
Skim Race's super-shielded track and Astro League's super-shielded edge lining no longer pull fauna
steering. Note it has **no bearing on Cleave itself**, which has no shielded mass and no fauna; it
is kept because it is a genuine platform fix.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameCleave.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/CleaveScoringRule.asset` |
| Cell configs (4) | `_SO_Assets/Cell Configs/Cleave Cell/Cleave Cell Config {1..4}.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset` |
| Arena prefabs (4) | `_Prefabs/Spawnables/Spawnable{Panes,Swell,Ribcage,Twistbands}.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameCleave.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`cleavePrismTarget`) |
| Measurements | `Tools/Build/cleave_arena_measurements.json` (generated; hash-guarded) |

Every asset above is authored by `Tools/Build/author_cleave_assets.py` — deterministic GUIDs,
idempotent, validates before writing. **Re-tune there and re-run** rather than hand-editing the
YAML. Its `--check` is a **real diff against disk**, not a dry run: the previous version only
re-ran its in-memory validation and printed "no files written", which passed whatever the assets
actually said.

**The Rampage scene clone is a spent one-shot and now STANDS DOWN.** Once `MinigameCleave.unity`
exists the generator patches the blocks it owns (the Cell's config list, the spawn ring, the
controller's field names) and leaves the rest alone. It only clones from Rampage when the scene is
missing. This is the `author_dogfight_assets.py` trap avoided deliberately: an `assert` on a donor
that has moved on aborts the script and takes every check below it with it, which is how four
sibling mode generators came to validate nothing.

**Re-tuning order** (any geometry change):

```
bash Tools/Build/cleave_arena_harness/run.sh   # re-measure the shipped generators
python3 Tools/Build/cleave_budget.py                    # inspect the ladder + thresholds
python3 Tools/Build/author_cleave_assets.py             # re-author the assets
python3 Tools/Build/author_cleave_assets.py --check     # must pass
```

## In-editor verification (authored headless — NOT yet run)

Nothing below has been executed. The arenas were measured by compiling and running their real
generators, which proves what they EMIT; it proves nothing about how any of it looks or plays.

1. **Open** `MinigameCleave.unity`. Every script reference resolves (no "Missing (Mono Script)"),
   the controller's inspector shows `rule` = CleaveScoringRule, the milestone fractions 0.25 / 0.5
   and **`aiArenaRadiusOverride`** (renamed this branch — if the inspector shows a stray
   `aiCageRadiusOverride`, the scene was not re-authored), and the **Cell shows four configs with
   Cell Type Choice = Intensity Wise**.
2. **Intensity picks a DIFFERENT PLACE.** Launch each of 1–4 in turn. You should get angled slabs,
   then corrugated sheets, then three nested shells, then twisted ribbons — four arenas that look
   nothing like each other. *This is the headline check*: if two intensities look alike, the Cell is
   not on `IntensityWise` or the configs are listed out of order.
3. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should report
   **11,021 / 13,738 / 14,731 / 16,423** prisms. If it disagrees, the harness and the editor have
   drifted — re-run the harness and investigate before shipping.
4. **Panes — the mullions.** Find a pane-pair intersection: it should be a straight, visibly denser
   beam. Flying one end to the other should be the best single cut in the arena.
5. **Swell — the grain is legible.** From a distance the sheets should read as gold/jade banding.
   Fly a trough with the blade laid flat, then fly the same sheet across the grain, and confirm the
   first is worth several times the second.
6. **Swell — crests punish.** Clipping a ridge should full-stop you, debuff all four elements for
   4 s and reset boost. Troughs should be clean.
7. **Cage — unchanged.** Three rinds at 360 / 295 / 230, triangular openings, tightening inward,
   each inner rind tilted onto its own axis, no free radial corridor. This rung is supposed to be
   exactly what it was before the branch.
8. **Twistbands — the roll.** Fly a band holding the blade against the deck: keeping contact should
   require continuously rolling the sword. Fly a full lap and confirm you arrive back at your start
   inverted, and that the lane colours have mirrored.
9. **Twistbands — the cornice closes.** Follow one band's edge rail all the way round: it should be
   a single continuous curve that takes TWO laps to return to its start. A rail that stops halfway
   means the 0…4π sweep was shortened.
10. **Every prism is one hit.** Nothing anywhere should shed a shield or show an octahedron.
11. **No fauna.** Nothing should hatch, at any intensity, at any point in the match.
12. **Rhino only — SOLO.** Pick a different vessel in an earlier game, then launch Cleave: you
    should spawn a Rhino, with a `clamping selected vessel` line in the log.
13. **Rhino only — MULTIPLAYER (the regression that shipped once).** Have the CLIENT fly a Dolphin
    in the menu (vessel-changer toy), then have the host launch Cleave. The client must spawn a
    **Rhino**, with a `does not allow Dolphin; spawning Rhino instead` warning on the host. Then
    return to the menu and confirm the client can pick a Dolphin again.
14. **Spawn outside, on the equator.** All four players start on ONE horizontal circle ~576u out,
    90° apart, facing the arena, with the whole thing visible ahead. Also check Crystal Capture
    still spawns on its sphere (tetrahedral) — that scene must be unchanged.
15. **Everyone starts at 0.** In a real lobby, check every score panel reads 0 the instant the
    countdown ends — including after a rematch and after a previous game in the same session.
16. **Smashing scores; laying does not.** The HUD domain sum should rise as you cut and not at all
    from laying trail. Shatter one of your OWN team's trail prisms — the sum must not move; a
    rival's trail must.
17. **Milestones.** At **500** destroyed by the leading domain the device should shake hard for
    ~1.2 s; again at **1,000**.
18. **Win + scoreboard.** First domain to **2,000** ends the turn; winners show a time, losers
    "N Prisms Left". Replay (scene reload) resets the milestones.
19. **Pacing.** Time each intensity end to end — see the pacing flag. Most likely thing to need a
    change.
20. **AI stays outside, in every arena.** Watch an AI Rhino for a minute at intensity 1 AND at
    intensity 4: it should orbit outside and cut on transits. If it settles inside, the standoff
    has been set ≤ 1.
21. **Cloud save survives the rename.** Sign in with an account that has Cleave progress from
    before this branch: unlocks, quest completion, max unlocked intensity and bests must all still
    be there. This is what `GameModeRenameMigration` exists for, and the failure mode is silent.
22. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro League**: fauna
    should behave normally and should no longer park against the super-shielded track / edge lining.
23. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool, at intensity 4.

## Known limitations / follow-ups

- **Nothing has been run in the editor.** The arenas are measured, not seen. Every claim about how
  they LOOK — that the mullions read as beams, that the swell's banding is legible at range, that
  the twistbands' roll is a manageable ask rather than an infuriating one — is a design intention
  awaiting a playtest.
- **The 2,000 target is unmeasured for all four arenas** — see the pacing flag.
- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors a definition, so they are silently skipped (which is how a mode opts
  out). Author `GameToastConfig_Cleave.asset` with `{0}`=domain, `{1}`=prisms destroyed,
  `{2}`=target to make them visible.
- **`wildlife_cage_budget.py` still carries the doubled jitter factor** — see above. Out of scope
  here, but it is a live tuning defect in Wildlife Liberation's cell.
- **No objective-arrow provider**: like Rampage, `MiniGameHUD.CreateObjectiveProviderForGameMode`
  has no Cleave case — the arena surrounds you, so there is no single point to aim at.
- **No UGS stats reporter yet**, and no dedicated end-game controller — the shared scoreboard
  handles it.
- **Danger placement is a first pass in all four arenas** (pane rims, swell crests, twistband
  cornices; the cage's per-rib walk is unchanged). If they read as noise rather than as traps,
  cluster them instead — one constant per arena.
- **The harness needs a dotnet 8 SDK.** The measurement JSON is committed so nothing routine requires it,
  and the hash guard makes a stale measurement loud rather than silent — but re-tuning an arena
  does require a .NET SDK.
- **Several kept platform APIs now have no caller** (the fauna-containment family), documented above.
