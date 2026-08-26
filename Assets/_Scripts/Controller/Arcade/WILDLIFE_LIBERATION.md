# Wildlife Liberation — Technical Documentation

> **Naming.** `GameModes.WildlifeLiberation = 40` is the code/data/enum identity. The
> player-facing `DisplayName` on `ArcadeGameWildlifeLiberation.asset` is **"Wildlife
> Liberation"** too — no split today, but if one is ever wanted, change the DisplayName only
> (the Tournament/"Maelstrom" and Ribcage/"Peel the Cage" precedent). Do not rename the enum,
> the controller, the scene, or this file.

## Overview

Wildlife Liberation is the **Sparrow-only hunt**. Three concentric cages at **1050 / 600 /
200** divide the arena into rooms with a very wide empty gap between each pair, and the
wildlife — every tier of it, swarm to kaiju — roams **all of it** on one shared band, including
the open water outside the outer cage where the players spawn. Hunt it down; the **first DOMAIN**
to the summed kill target (default **30**) wins.

**One axis, and it is the ecology.** The scored stat is `IRoundStats.LifeformsKilled` — an
*attributed creature death*. Nothing else scores: not cage prisms, not rival trails, not
crystals. A creature that starves, or that a shark eats, credits **nobody**.

**It is a domain race, like every other multiplayer mode here** (Skim Race, Joust, Scurry,
Rampage, Ribcage, Brood Rush, Astro League): the winning domain is the one whose players'
kills sum to the target first.

> **A FREE-FOR-ALL variant (first player to the target) shipped here briefly and was reverted.
> Do not re-derive it.** The mode seats up to **four** players but the platform has only
> **three** playable domains (`GameDataSO.ActiveDomains` = Jade / Ruby / Gold; Blue is the "no
> team" sentinel), so a four-player lobby *always* has two players sharing a colour. A
> per-individual winner therefore bypasses the domain machinery every other mode runs on — the
> winner banner, the domain HUD panels, the scoreboard's team ordering and
> `ResolvePlacementOrder` all speak in domains, and a mode that answers "a player won" leaves
> every one of them describing something that is not the result. Teammates sharing a total is
> the intended shape here, not a defect to design around.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity` (single
  unified scene, cloned from Rampage's skeleton — no separate singleplayer variant; solo play is
  a party of one + AI backfill)
- **GameMode enum**: `GameModes.WildlifeLiberation = 40`
- **Controller**: `WildlifeLiberationController : MultiplayerDomainGamesController` — a
  structural sibling of `RampageController` / `RibcageController` (1 round / 1 turn,
  `HasEndGame=false`, server winner detection in `OnTurnEndedCustom`, snapshot
  `SyncFinalScores_ClientRpc`), plus progress milestones and the AI hunters
- **Scoring**: `WildlifeLiberationScoringRuleSO` (`metric = ScoringMetric.LifeformsKilled`;
  golf-timed) — the winning **domain's** players `Score` = finish time, everyone else the
  `GolfScoreSentinels` sentinel encoding their team's deficit (displayed "N Kills Left")
- **Turn monitor**: `WildlifeKillTurnMonitor` — resolves the **per-domain** target from
  `EndConditionOverridesSO.GetWildlifeKillTarget()` (default **30**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
  `GameDataSO.LifeformTargetCount`
- **Players**: **1–4** with AI backfill. `MinDomainsAllowed = 2` (a domain race needs two
  colours), `MaxDomainsAllowed = 3` — so a full lobby always has one domain of two
- **Vessels**: **Sparrow only** — see "Sparrow-only" below, which is enforced in three places
- **Config**: `_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## The pipeline (zero bespoke tracking)

```
Sparrow shoots a creature's last body prism
  └─ HealthPrism.Explode → Fauna.OnBodyPrismExploded → sealed Fauna.Die(killerName)
        ├─ crystal drops                              (mass conserved)
        ├─ body withers / suctions out                (continuity of existence)
        └─ CellRuntimeDataSO.OnFaunaKilled.Raise(killerName)   [player-attributed only]
              ▼
StatsManager.LifeformKilled
        ├─ [server]  credit directly
        └─ [client]  Player.ReportFaunaKill_ServerRpc()  (fauna are client-local -
                     see "Multiplayer" below; without this only the host could score)
              ▼
IRoundStats.LifeformsKilled++                                                 [server]
              ▼
ScoringMetrics.Read(stats, LifeformsKilled)
  ├─ SumByDomain → MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ WildlifeKillTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached       [server]
  ├─ WildlifeLiberationController.SampleProgress → leading domain + milestones [server]
  └─ ElementalComebackSystem (source LifeformsKilled) → trailing-domain buff
              │  turn end
              ▼
WildlifeLiberationController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

## Multiplayer: how a client's kill reaches the server

**This is the one place the mode needed real networking, and it needed it because of the
ecology, not the scoring.**

Every other stat in the game originates from something that exists identically on every peer. A
prism sits at the same world position on the host and on every client, so when a client rams
one, the server's own physics sees the same collision with the same attribution and
`StatsManager` records it server-side — which is why Rampage and Ribcage need no RPC at all.

**Fauna are not like that.** They have no `NetworkObject`; every peer simulates its own swarm
and the populations diverge (`Docs/ECOSYSTEM.md` §7 caveat 4). A creature a client just shot may
not exist on the server at all. Recorded server-only, **only the host could ever score a kill.**

So `StatsManager.LifeformKilled` has a client branch — the only one in that class:

```
server (host + every AI, which is server-owned)   → credit RoundStats directly
client, and killerName == this machine's player   → Player.ReportFaunaKill_ServerRpc()
                                                       → server credits THAT Player's stats
```

This is the same **owner detects → server records** round-trip `NetworkVesselImpactor` uses for
jousts. **Identity comes from RPC ownership, not from the name string**: `RequireOwnership` is
the default and the server credits the RoundStats of the `Player` object the RPC arrived on, so
a client can only ever credit itself.

> **Transitional limitation — fauna network sync is in flight on a separate branch.** Until it
> lands, fauna diverge per peer: each hunter shoots their *own* copy of the swarm. Populations
> are statistically identical (same configs, same seed floors, same rooms) but not the same
> creatures, so two players cannot race for the same kill (`Docs/ECOSYSTEM.md` §7 caveat 4).
>
> That has one consequence worth knowing while it lasts: teammates on one domain are shooting
> two different local swarms, so a two-player domain's total is **two independent hunts added
> together**, not one swarm hunted twice — it converges on the target roughly twice as fast as a
> solo domain. In a 4-player lobby (one domain of two, two of one) that is a real asymmetry.
>
> **Both go away when the sync branch merges**, and nothing here needs to change for that: this
> mode reads `IRoundStats.LifeformsKilled` through the ordinary scoring path, and the RPC below
> is an owner-reports-to-server round-trip that stays correct whether or not the creature also
> exists on the server. Re-measure the kill target after the merge, though — the doubled-up
> domain stops getting its head start, so matches will run longer.

> A client can spam the RPC to inflate its own count. So can it spam the joust RPC. Anti-cheat
> is out of scope for the party-game layer; noted so nobody assumes otherwise.

## The platform change: fauna are now shootable

**Before this mode, no creature in the game could be killed by shooting it.** Destroying a
fauna's body prisms just removed prisms and left the creature swimming; the only kill paths were
starvation, predation, and the crystal joust (`VesselWitherLifeformByCrystalEffectSO` →
`Fauna.Predated`). `WormSegmentFauna` was the sole exception — it implemented
`OnBodyPrismExploded` and died when its body was stripped. So a whole vessel class whose verbs
*are* guns and missiles could not kill wildlife at all.

That rule is now the **`Fauna` base behaviour**: when a creature's last body prism is destroyed
it dies through the sealed `Fauna.Die`, which drops its elemental crystal (mass conserved) and
withers / suctions the remains out (continuity of existence) exactly like every other death.

**This is inside the conserved-mass law, not an exception to it.** There is no timer, no
lifespan, no cull: a creature nobody shoots still only ever dies to starvation or predation. An
active force removing mass is precisely what the law permits (CLAUDE.md ▸ "Mass is conserved").

Invariants checked against this change:

| invariant | status |
|---|---|
| Continuity of existence | **Held.** `Boid.OnDeath` / `LightFauna.OnDeath` already wither or suction; both skip already-destroyed prisms, so a shot creature's remaining structure still leaves visibly. |
| No imposed death | **Held.** No clock added. The only new sink is a player's guns. |
| Starvation = wither-to-crystal | **Held.** The kill path routes through the same sealed `Die`. |
| Every lifeform drops one elemental crystal | **Held.** `Die` drops it before `OnDeath` runs. |
| No domain asymmetry | **Held.** Nothing here reads domain. |
| Volume is the spine | **Untouched.** |
| Territorial permanence | **Untouched.** |
| Collider budget | **Stated below.** |

**It affects every other mode**, and in every case as an improvement: wildlife in Skim Race,
Brood Rush, freestyle and the Wanderway are now killable by any vessel that can destroy a prism.
Verify those in-editor (checklist item 12) rather than assuming.

`WormSegmentFauna` keeps a thin override purely for its own `_dead` guard, which covers the
colony-initiated deaths (`WitherAway`, a split's shed) the base guard cannot see.

## The roam band (one band, every species)

`Cell.FaunaContainmentRadius` — Ribcage's brood pen — is **one radius, whole cell**.
`FaunaConfigurationSO.BandInnerRadius` / `BandOuterRadius` generalizes it to an **annulus
authored per species**. This mode used that to stack three pens, one tier of wildlife per room;
it now authors **one band, shared by every species**: **0 .. 1180** — the whole arena, core to
just inside the 1200u membrane (`SpawnableWildlifeCage.RoamInner` / `RoamOuter`).

> **The three-tier pen was replaced on request (2026-08):** *"all the faunas are set up like the
> big ones are concentrated in the center — make all the faunas disperse everywhere, do not need
> the layer-by-layer fauna structure."* Locking a tier into a room read as three stacked
> aquariums around a boss room: the fight converged wherever a player broke in, and the apex
> creatures were only ever findable at one radius. Mixing every tier through one volume is what
> makes this a **hunt** — what you meet next is a roll, not a radius.

The band is honoured at the same three chokepoints as before; none of that machinery changed,
which is the payoff of having built the pen as a capability rather than three special cases:

- `Fauna.Goal`'s setter (the one point every goal writer passes through — `ResolveGoal`, Boid's
  override, LightFauna's direct writes, the spawner's initial goal, and `TryReproduce`'s
  inheritance), which composes the cell pen first and then the band;
- `Fauna.IsPreyForMe`, the shared edibility predicate every grazer routes through
  (`LightFauna.IsEdibleForHerbivore`, `WormFauna.IsEdiblePrism`, `Boid.IsEdibleForForager`);
- **`CellLifeSpawnerBase.SpawnFaunaBanded`**, so a banded species **hatches inside its band,
  scattered across it** — an independent random direction and radius per creature, for both its
  spawn position and its initial goal.

  **This lives on the BASE, and that is the whole lesson of the bug it fixed.** The placement was
  first written into `RandomLifeSpawner` — and never ran. `Cell.StartSpawnerForMode` picks
  `IntensityWiseLifeSpawner` whenever the cell is on `CellTypeChoiceOptions.IntensityWise`, which
  is also the *only* way to vary a cell by intensity. This mode needs per-intensity cages, so it
  gets the intensity spawner whether it wanted it or not, and that spawner passed **no spawn
  position at all** with a goal of the cell crystal. Every creature in the biome was born at the
  centre and immediately swam back to it. Both spawners now go through one call.

Same contract as the cell pen and for the same reason (`Docs/ECOSYSTEM.md` §22): **a spatial
DIET + STEERING rule, never a wall.** Nothing is teleported, no collider is added, nothing is
culled for crossing a boundary. A creature can still drift out on its own momentum — it simply
has no reason to and nothing to eat there. At 0..1180 the band's only real remaining job is
keeping creatures off the membrane. `0 = no band` is the default and what every *other* shipped
biome authors, so nothing else in the game is affected. Offspring inherit their parent's band for
free (they bind the same config).

### ⚠ The draw had to be fixed with it, and it was half the reported bug

`CellLifeSpawnerBase.RandomPointInBand` drew `Random.Range(inner, outer)` — uniform in RADIUS,
which gives every radial shell the same headcount while a shell's space grows as r². Measured
over 200k samples on a 0..1180 band:

| quarter of the arena's VOLUME (inner → outer) | uniform-in-radius | volume-uniform |
|---|---:|---:|
| 1st (r ≤ 743) | **63.1%** | 25.2% |
| 2nd (743–936) | 16.3% | 24.9% |
| 3rd (936–1072) | 11.6% | 24.8% |
| 4th (1072–1180) | 9.1% | 25.2% |

So *"the big ones are concentrated in the centre"* was partly the pens and partly the draw, and
widening the band alone would have made the clumping **worse** — the wider the band, the harder
the r² error bites. `RandomBandRadius` now draws the cube root of a uniform sample between the
cubed walls. It was invisible until now because every band ever authored was a thin annulus
(660..990 moves its mean radius 2.6%; 1090..1180 moves it 0.1%), and no other biome authors a
band at all. **Same finding `Docs/ECOSYSTEM.md` §27 records for flora planting** — a species
disperses in a volume-uniform BAND, never on a shell — reached independently on the fauna side,
which is the tell that it is a property of spheres rather than of either system.

### ⚠ What the pens were silently buying: the cage is now food

**The old bands were why the cage could be cheap.** Each stopped **60u short of its own walls**,
so a creature's jail was outside its band and therefore not food. One arena-wide band puts all
three cages inside it, and this cell has **no nucleus**, so the legacy diet applies: herbivores
eat opposing-domain mass, the bars are painted across the domain triad, and **the cage is
grazeable and erodes as a match runs.**

That is accepted deliberately — it is the food web working, in a mode whose entire subject is the
ecology — and two things in the same pass cut how far it goes: the kill target dropped **250 →
30** (an ~8× shorter match) and the population dropped **15%**.

**Do not answer it by shielding the bars.** A shield reaches **1.5 × `leafSize`**
(`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`, `Docs/ECOSYSTEM.md` §35), so a 26u bar laid
every 34u would fuse this sparse lattice into a solid tube — and every bar would stop being a
one-hit prism, which is the break-in the mode is built on. If playtest says the erosion is too
fast, the levers in order are: raise `RoamInner` off 0, then cut `POPULATION_SCALE`.

**General rule this leaves behind: when you remove a constraint, find what it was silently
buying.** A pen that looked like a steering rule was also a diet rule, and the diet rule was also
a collider-budget device.

### The rooms still exist — as architecture

| room | interior | bounded by |
|---|---|---|
| open water | 1090 .. 1180 | outside the 1050 cage, inside the 1200 membrane |
| outer | 660 .. 990 | the 1050 and 600 cages |
| middle | 260 .. 540 | the 600 and 200 cages |
| core | 0 .. 140 | the 200 cage |

`SpawnableWildlifeCage.RoomInner` / `RoomOuter` still describe these, because the three cages
still divide the arena into them — but they are **cage architecture, not a fauna pen**. Their one
live consumer is the AI hunters' patrol, which steps through the rooms to sweep the arena at
every radius. The player spawn ring is at **1150**, inside the open water, so there is something
to shoot from the moment you spawn and breaking into a cage is a choice rather than the only way
to score.

Authored from **one source**: `SpawnableWildlifeCage.RoamInner/RoamOuter` in C#, mirrored by
`wildlife_cage_budget.ROAM_INNER/ROAM_OUTER`, which is what the asset generator writes into the
fauna configs — and the generator **fails** if the two disagree, if any species carries a
different band from its neighbours (which is how a "tier" would creep back in), or if a band
fails to reach past the outer cage into the water the players spawn in.

## The jail

`SpawnableWildlifeCage : CellEnvironmentSpawnableBase`, seed 40, deterministic per seed like
every cell environment. **This is not Ribcage.** Ribcage is a layered orange whose bone *is* the
score — dense, tight, five rinds. This is a sparse lattice of long bars with big triangular
openings, so the arena reads as mostly empty space: here the prisms are only the walls.

- **Three shells, always**, at a **fixed** 1050 / 600 / 200. The shell count is deliberately
  **not** the intensity dial: each shell is a ROOM you break into, so dropping one would delete a
  third of the game rather than make it easier. (It was written when each shell also *penned* a
  tier of wildlife; the pens are gone and the rooms are the reason now.)
- **Enormous radial gaps** — 450u between the outer and middle cages, 400u between the middle
  and the core. Each room is a place you fly *into*, not a rind you pass through.
- **The openings are TRIANGLES, from a GEODESIC** (subdivided icosahedron), not latitude hoops.
  That is a fairness property: a latitude sphere is densest at its poles, which is why Ribcage
  must tilt every rind onto its own axis so nobody drills the top. A geodesic has no poles —
  every approach meets the same weave — so this cage needs no tilt table at all.
- **Intensity ramps the SHAPE and the WEAVE — and nothing else.** The wildlife roster is
  identical at every intensity (see below), so this table carries the *entire* difficulty curve.
  Intensities 1–2 are three geodesic spheres that tighten; 3 swaps the outer cage for a **BOX**
  (square rail grid, heavy corner posts — "the boxing ring"); 4 is **three nested boxes** at the
  tightest weave in the mode. A box is a genuinely different problem: its flat faces mean an
  approach is either square-on at a long dense wall or into a corner where three walls converge.
- **Every bar is a ONE-hit PLAIN prism** except the danger traps. Nothing is `Shielded` or
  `SuperShielded` — see the bands section for why.
- **50–99 danger traps, in the CORE cage only.** The innermost room holds the biggest, hardest
  creatures; salting its walls is what makes "just ram your way in" a bad plan *there
  specifically*. Contact costs the standard danger punishment (volume-independent full-stop
  slow, 4 s all-element debuff, boost reset) — a Sparrow that loses its speed inside the core
  room is in real trouble.

Analytic budget (`Tools/Build/wildlife_cage_budget.py`; confirm with FrogletTools ▸ Ecology ▸
Measure Cell Environment Baselines):

| intensity | outer | middle | core | prisms | danger | openings (o/m/c) |
|---|---|---|---|---:|---:|---|
| 1 | geodesic f5 | geodesic f4 | geodesic f3 | **9,206** | 50 | 251 / 179 / 79u |
| 2 | geodesic f7 | geodesic f5 | geodesic f4 | **12,696** | 82 | 180 / 144 / 60u |
| 3 | **box** f14 | geodesic f7 | geodesic f5 | **13,244** | 85 | 87 / 103 / 48u |
| 4 | **box** f18 | **box** f18 | **box** f12 | **13,956** | 158 | 67 / 38 / 19u |

> **The box frequencies are much higher than the geodesic ones and that is not a typo.** A cube
> face grid at frequency *f* contributes 12*f*² segments against a geodesic's 30*f*², and the box
> is smaller (corners on the radius ⇒ faces at 0.577·r). Matching frequencies would make the
> *harder* intensities lighter **and** more open than the easy ones. The values come from the
> measured table in `wildlife_cage_budget.py`. Re-tune **there** and re-run the generator, never
> by eye — the rounding in the per-segment prism walk is not monotonic in frequency, so an
> eyeballed bump can easily make a cage *lighter*.

## The wildlife (the objective)

**One `FaunaConfigurationSO` per (species, intensity)** — four species, four intensities, and
the spawner runs one loop per config. Every one carries the **same** band (0..1180, the whole
arena) and every one runs `SpreadElements` over its species' four canonical element assets, so a
species' variety is its ELEMENT.

| species | seed | cap | body prisms ea. | prisms at cap |
|---|---:|---:|---:|---:|
| QuadFish | 383 | 893 | 1 | 893 |
| Brittlestar | 99 | 228 | 10 | 2,280 |
| Shark (predator) | 32 | 68 | 11 | 748 |
| Worm Colony (kaiju) | 5 | 9 | ~26 | 234 |
| **total** | **519** | **1,198** | | **4,155 prisms at cap** |

**The roster has been merged TWICE, and both merges were arithmetic.** It started as
`species × room` — the pens gave this table a `room` column, so a species living in two rooms
needed two configs (8 per intensity). Replacing the three pens with one arena-wide band collapsed
those into `species × level` (6 per intensity). Then **`Docs/ECOSYSTEM.md` §39 retired lifeform
levels**, and `species × level` collapsed into **`species` (4 per intensity)**. Populations were
preserved exactly through both — 610 seed / 1,409 cap before `POPULATION_SCALE`, 519 / 1,198 /
4,155 body prisms after it, identical to the six-row table row for row in total (`prisms` was
already equal across a species' rows, so nothing is lost in the arithmetic).

**What the second merge COST, stated plainly: the size tiers are gone.** This mode was built on
"a very heavy swarm of small creatures, much bigger ones, and the biggest and toughest", and
`InitialLevel` was how that read — a level-5 shark among level-2 ones, a level-2 brittlestar
among level-1 ones. There is no level any more, so **the four species are now told apart by
species and by element, not by size**: a shark is a shark, and every shark in the arena is the
same size. The species themselves still span an order of magnitude (a 1-prism QuadFish against a
~26-prism worm colony), so the swarm-to-kaiju read the mode is named for survives; what is gone
is the *within-species* size mix, and with it the "that one's a big one" moment inside a school.
If that mix is wanted back, the honest lever is a per-element `FaunaVariantTuning.BaseBodyScale`
on the species' four canonical assets — an element that is genuinely a bigger animal — not a
level axis. Note this also makes the heart a species constant: a shark heart is 4.60 world scale
and a worm segment's 2.28, per element and for life (`Docs/ECOSYSTEM.md` §39.2), so **a shark
kill pays double a worm segment's** — which is the size-reads-as-reward the tiers used to give,
moved from within a species to between them.

**No tadpoles** (removed on request, 2026-08). QuadFish inherits the swarm role — also a
1-prism body, so the headcount survives, but the tadpoles were carrying a large share of it and
redistributing that share across species that are not all 1-prism raised the body-prism total
from 3,161 to 4,896. That is the cost of the swap and it is paid in the collider table below.

**15% off the whole roster** (requested 2026-08, alongside the dispersal): `POPULATION_SCALE` is
`[0.85, 0.85, 0.85, 0.85]`, applied to seed **and** cap so it actually binds — a scalar that
moves only the floor is clamped away by `MaxLivePopulation` and reads as doing nothing
(`Docs/ECOSYSTEM.md` §29). 610 → 519 at seed, 1,409 → 1,198 at cap, 4,896 → 4,155 body prisms.
It is deliberately the *dial* rather than twelve edited numbers, so the cut is one line to
revisit and the authored roster still reads as the play-tested one.

**The roster is otherwise identical at every intensity** (requested 2026-08: *"keep around 600
rising to 1400 at all intensities — the later levels can have more complexity"*, now scaled).
The whole intensity ramp lives in the cage's `SHELL_PLANS` instead. **Do not read a uniform
scale as "unused".**

**The seed→cap gap is wide on purpose.** 519 → 1,198 means well over half the eventual
population is *born in play*: the spawner only tops each species back up to its floor, so
everything above it is reproduction, bounded by starvation and the caps. The swarm visibly
thickens as a match runs and thins where hunters have been working — the food web doing the
shaping, not a spawner curve.

`PopulationSize` is a **seed floor**, not a population: the spawner only tops a species back up
to it (bootstrap + extinction recovery). Everything above comes from reproduction and is bounded
by starvation — the food web, not a timer (`Docs/ECOSYSTEM.md` §6). `MaxLivePopulation` is the
performance backstop, which is why the **cap** column is what the collider budget is sized
against.

**`FaunaFoodFloor` is 0 (always produce)**, deliberately: it was set when the cage was inedible
(the pens) and a prey-gated spawner would never have bootstrapped the jail. With the roam band
the cage IS food, so the floor is no longer load-bearing — it is left at 0 because "always
produce" is still what this mode wants. Creatures also feed on whatever a player lays, which is
the mode's risk/reward: linger to shoot and your trail becomes their dinner.

**No flora.** `SupportedFloras` is empty; the rooms are meant to read as empty space with
wildlife moving through them.

### Collider-budget impact — read this before tuning anything else

| intensity | cage prisms | fauna body prisms (cap) | total | was |
|---|---:|---:|---:|---:|
| 1 | 9,206 | 4,155 | **13,361** | 14,102 |
| 2 | 12,696 | 4,155 | **16,851** | 17,592 |
| 3 | 13,244 | 4,155 | **17,399** | 18,140 |
| 4 | 13,956 | 4,155 | **18,111** | 18,852 |

**The 15% cut moves the budget in the right direction and does not change its shape** — the cage
is untouched, so the saving is 741 movers per intensity. The cage half will now also *shrink
during a match*, because the roam band made the bars grazeable; do not treat the cage column as
a floor.

Comparable to Ribcage (10,620 → 20,153) in raw collider count — but **the fauna half is far more
expensive per collider than the cage half**, and that is this branch's headline performance risk:

- **Every fauna body prism is a MOVER.** It re-buckets in `PrismSpatialIndex` as the creature
  swims (`Fauna.NotifyBodyPrismsMoved`), where a cage prism is registered once and never moves.
- **Every creature runs a behaviour coroutine** — **519 at seed, up to 1,198 at the caps.** This
  is the number to watch, not the prism count.
- **This is still well over the masterplan's ≤1,500-per-cell fauna-prism target** and many times
  more creatures than any shipped biome. It is an explicit product decision ("very heavy…",
  requested 2026-08), trimmed 15% in the dispersal pass, not an accident of the roster.
- **Bootstrap cost:** 519 creatures, seeded one per `InitialSpawnCount` step per species loop
  with a frame yield between each. Expect a visible fill-in during the countdown rather than a
  hitch.

**Measure on device before tuning.** Dials, in order of bluntness: `POPULATION_SCALE` and the
`ROSTER` caps in `wildlife_cage_budget.py` (the creature count — start here), then the cage's
`SHELL_PLANS` frequencies, then `BAR_STEP`. Re-run **both** Python tools after any change.

## Scoring

`WildlifeLiberationScoringRuleSO` is deliberately **thin**. It picks the metric
(`LifeformsKilled`) and the target (`GameDataSO.LifeformTargetCount`) and inherits the rest of
`ScoringRuleSO`'s domain behaviour — `ResolveWinner` (highest domain sum, ties by
`ActiveDomains` order so every machine agrees), `Remaining` (the domain's deficit) and
`ResolvePlacementOrder` are all used unchanged. Only `IsObjectiveReached`, `AssignScores` and
the two presentation methods are its own, and each has the same shape as
`RibcageScoringRuleSO`'s.

| member | behaviour |
|---|---|
| `IsObjectiveReached` | first active domain whose summed kills reach the target |
| `ResolveWinner` | inherited — highest domain sum |
| `Remaining(domain)` | inherited — `target − SumByDomain` |
| `AssignScores` | winning domain's players get the finish time; everyone else a sentinel encoding *their team's* deficit, so losing teammates tie |
| `BuildResults` | golf order is team-major by construction; individual kills order teammates, name is the final tiebreak |

`ElementalComebackSystem` uses `ScoreDifferenceSource.LifeformsKilled`, domain-aggregated like
every other source — a player's deficit is their team's deficit against the leading colour.

## Sparrow-only

Enforced in **three** places, all reading the single `Vessels` entry on
`ArcadeGameWildlifeLiberation.asset`. This is not belt-and-braces for its own sake — Ribcage
shipped with two of these and a client still flew a Dolphin:

1. **`GameDataSO.SyncFromArcadeGame`** clamps `selectedVesselClass` on the machine that pressed
   Start, on every route (modal, rematch, Tournament chain).
2. **`ServerPlayerVesselInitializer.ResolveSpawnVesselType`** re-clamps **server-side at spawn**.
   This is the one that matters in multiplayer: `Player.NetDefaultVesselType` is an OWNER-write
   NetworkVariable that each client sets from its OWN local config and from the menu's
   vessel-changer toy, so a client walks in still wearing the hull it last flew — and
   `SyncFromArcadeGame` never runs on a client, while the config ClientRpc lands *after* the
   spawn.
3. **`ServerPlayerVesselInitializerWithAI`** now clamps the **AI's** class too (new on this
   branch). The AI's vessel comes from the scene's `aiInitializeDatas` or a captain roll,
   neither of which knows the mode's rules, so a mis-authored scene could field opponents in an
   illegal hull. The scene also authors Sparrow directly, so the clamp should never have to
   fire.

## Everyone starts at zero

Ribcage shipped a bug where some players began a match with a non-zero score.
`RoundStats` lives on the **persistent** Player NetworkObject and survives every scene load, so
a missed reset carries the previous game's stats straight in. Three layers here:

1. **`ServerPlayerVesselInitializer`** calls `player.PrepareForNewScene()` unconditionally, once
   per player, on the **processing** path (the platform fix — it used to live inside the
   *finder*, on its fallback branch only, so whether a player started at zero depended on which
   lookup branch happened to find them).
2. **`WildlifeLiberationController.OnNetworkSpawn`** sweeps `LifeformsKilled` to 0 (server only).
3. **`OnCountdownTimerEnded`** sweeps again — the last moment before anyone can score, by which
   point a late joiner is on the roster.

`IRoundStats.Cleanup()` zeroes `LifeformsKilled`, and `IRoundStatsCleanupTests` asserts it —
**anything added to `IRoundStats` must be zeroed there and asserted there.**

## AI hunters

**Every AI waypoint is INSIDE a room. That is the whole rule, and it is the exact inverse of
Ribcage's.** `AIPilot` has no arrive-and-stop behaviour — it steers at its target forever and
flies through on arrival — so a target's placement decides where the AI *lives*. Ribcage wants
its AI outside the bone (damage happens on the transit), so its stations sit beyond the shell.
Here the prey is inside the rooms, so a waypoint on a wall would make the AI orbit the wall and
never hunt: every patrol waypoint is placed at the **middle of a room's radial band**.

Each AI works one room for 4 waypoints and then steps inward, cycling outer → middle → core →
outer, phased by seat so a full lobby spreads through the jail. Waypoints walk a golden-angle
spiral (~137° apart) so it keeps finding un-hunted wildlife. **Every other waypoint is a HUNT**
on `Cell.GetExplosionTarget` — the densest mass hostile to its domain, which in an arena whose
mass is mostly creature bodies resolves to a swarm. No bespoke targeting exists; that is the
same density query the ecology already runs.

## Progress milestones

At a quarter and a half of the kill target, the **leading domain** crosses a rung:
`SampleProgress` (server, every 0.5 s) → `AnnounceMilestone_ClientRpc` → a `GameToastSituation`
post plus `HapticController.PlayAlert()` on every peer. A lead change after the first milestone
posts `WildlifeLeadChanged`.

Rungs ride the leader's *own* progress rather than a cross-domain total, so they land at a fixed
point in the race rather than at a point a busy lobby reaches several times faster.

These are **pure feedback — they change no game state**, so a missed or late sample costs a
toast, never a rule. Toast copy is unauthored today, so **right now the shake IS the milestone
feedback** (same state as Ribcage).

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.wildlifeKillTarget`, 0 = default **30**) — the number of creatures a
**domain** must kill between them. Live/Build split + build auto-restore work like every other mode. The
milestone rungs are fractions of it (0.25 / 0.5).

> **30, down from 250 (requested 2026-08).** Every intensity holds ~519 creatures at seed and
> breeds toward ~1,198, so 30 is a small fraction of the standing population — this is now a
> **short, punchy** match rather than a long grind, and with the wildlife dispersed everywhere a
> hunter can start scoring from the spawn ring without breaking into anything. Milestones land at
> **8** (0.25) and **15** (0.5), and they follow the field automatically.
>
> **Neither number is playtested.** Three things bear on it. (1) The target moved ~8×, so the
> pacing question is now "is 30 too *quick*", not too slow. (2) While fauna are still
> client-local, a two-player domain reaches it faster than a solo one (see the multiplayer note
> above) — so time a **4-player** match, not a solo one. (3) When the fauna-sync branch merges
> that head start disappears and matches get longer. It is one editor field.
>
> It also bounds the cage erosion the roam band introduced: a shorter match is less grazing.

### ⚠ The comeback rate moved with it — the third outing of a recorded trap

`ElementalComebackSystem` computes `bonusLevels = deficit × ComebackRatePerScoreDeficit`, so
**the rate is a function of the target** and re-targeting a mode silently disarms it. Dog Fight
recorded the trap; The Bends hit it 20× harder and added a build-time assert. This mode is the
third case, and it was **already latent at bring-up**: the card inherited Rampage's `0.01`
against a target 8× smaller, so a quarter-of-target deficit only ever bought 0.625 of a level.
250 → 30 would have taken that to **0.075** — a comeback system that does nothing at all.

Rate is now **0.35**, so a quarter-of-target deficit (7.5 kills) buys **2.6** levels. That is Dog
Fight's curve (90 × 0.12 = 2.7), which is the nearest sibling by structure — same vessel, same
"many small increments" race. Where the shipped family sits:

| mode | target | rate | levels at ¼-target deficit |
|---|---:|---:|---:|
| Rampage / Ribcage | 2000 | 0.01 | 5.0 |
| Bends | 3 | 4.0 | 3.0 |
| **Wildlife Liberation** | **30** | **0.35** | **2.6** |
| Dog Fight | 90 | 0.12 | 2.7 |
| Scarab Scramble | 10 | 0.5 | 1.25 |

`author_wildlife_liberation_assets.py` now **fails the build** if a quarter-of-target deficit
stops buying one whole element level, so the next target change cannot repeat it.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/WildlifeLiberationScoringRule.asset` |
| Fauna-kill SOAP channel | `_SO_Assets/Event Channels/Event_FaunaKilled.asset` |
| Cell configs (4) | `_SO_Assets/Cell Configs/Wildlife Liberation Cell/Wildlife Liberation Cell Config {1..4}.asset` |
| Spawn profiles (4) | `_SO_Assets/Cell Configs/Wildlife Liberation Cell/Wildlife Spawn Profile {1..4}.asset` |
| Fauna configs (32) | `_SO_Assets/Cell Configs/Wildlife Liberation Cell/Wildlife {Outer,Middle,Core} {Species} {1..4}.asset` |
| Cage prefabs (4) | `_Prefabs/Spawnables/SpawnableWildlifeCage{1..4}.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`wildlifeKillTarget`) |

Every asset above is authored by `Tools/Build/author_wildlife_liberation_assets.py` —
deterministic GUIDs, idempotent, validates before writing. **Re-tune there and re-run** rather
than hand-editing the YAML. `Tools/Build/wildlife_cage_budget.py` is the arena's analytic model
(geometry **and** the roam band **and** roster) and the generator **imports** it, so the walls,
the band and the PhaseThresholds cannot drift apart.

> **The generator has TWO modes, and a retune uses the second one.**
> `--population` re-authors only the layer that gets retuned — the fauna configs, the spawn
> profiles, the cell configs and the kill target — and skips the one-shot **bring-up** sections
> that clone the donor `MinigameRampage.unity` and register the arcade card. Use it for every
> roster, band or target change.
>
> That donor has moved on since bring-up, so a **full** run asserts out (`controller field block
> not found in donor scene`) — and even if it matched, re-cloning would overwrite this mode's
> scene with a fresh copy of somebody else's. A generator that authors both a one-time scene and
> a re-tunable data layer has to be able to run just the second half. Re-enabling the full path
> means re-syncing those `OLD_FIELDS` / `OLD_CELL` anchors against the current Rampage scene
> first.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `WildlifeLiberation = 40` |
| `Fauna` | `OnBodyPrismExploded` default = **die when the last body prism is destroyed** (platform-wide); `ReportKill` publishes attributed deaths; per-species band (`IsInsideBand` / `ClampToBand` / `IsPreyForMe`); `StarvationKiller` constant |
| `WormSegmentFauna` | override thinned to its `_dead` guard — the kill rule moved to the base |
| `LightFauna` / `WormFauna` / `Boid` | edibility routed through `Fauna.IsPreyForMe` (adds the band to the existing cell rule) |
| `FaunaConfigurationSO` | `BandInnerRadius` / `BandOuterRadius` |
| `CellLifeSpawnerBase` | `SpawnFaunaBanded` / `RandomPointInBand` / `ClampToBand` / `IsBanded` — banded placement hoisted onto the BASE so both spawners share it; **`RandomBandRadius`** draws the radius VOLUME-uniformly (cube root between the cubed walls) instead of uniform-in-radius, which crowded the inner wall — see "The draw had to be fixed with it" |
| `SpawnableWildlifeCage` | `BandInner`/`BandOuter` → **`RoomInner`/`RoomOuter`** (cage architecture, consumed only by the AI patrol) + `BandWallClearance` → `RoomWallClearance`; new **`RoamInner`/`RoamOuter`** = the one fauna band |
| `IntensityWiseLifeSpawner` | routed through `SpawnFaunaBanded` (it previously passed no spawn position, so everything spawned at the cell centre) + now honours `MaxLivePopulation` |
| `RandomLifeSpawner` | routed through the same shared call |
| `CellRuntimeDataSO` | `OnFaunaKilled` (`ScriptableEventString`) |
| `StatsManager` | `LifeformKilled(string)` + a code-side SOAP subscription, and the class's ONLY client branch (see "Multiplayer") |
| `Player` | `ReportFaunaKill_ServerRpc()` — owner-side kill report; identity comes from RPC ownership |
| `IRoundStats` / `RoundStats` | `LifeformsKilled` (+ event, + server-write NetworkVariable, + `Cleanup`) |
| `ScoringMetric` / `ScoringMetrics.Read` | `LifeformsKilled = 7` |
| `GameDataSO` | `LifeformTargetCount` |
| `ElementalComebackSystem` | `ScoreDifferenceSource.LifeformsKilled`, domain-aggregated like every other source |
| `EndConditionOverridesSO` (+ window + asset) | `wildlifeKillTarget` live/build/getter, default 500 |
| `GameToastSituation` | `WildlifeHuntQuarter = 53`, `WildlifeHuntHalf = 54`, `WildlifeLeadChanged = 55`, `WildlifeCoreBreached = 56` |
| `ServerPlayerVesselInitializerWithAI` | clamps the AI's vessel class into the mode's allowed set |
| `IRoundStatsCleanupTests` | asserts the new stat zeroes |

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameWildlifeLiberation.unity`. Every script reference resolves (no "Missing
   (Mono Script)"), the controller's inspector shows `rule` = WildlifeLiberationScoringRule and
   the milestone fractions 0.25 / 0.5, and the **Cell shows four configs with Cell Type Choice =
   Intensity Wise**.
2. **Three cages, very far apart.** Launch at intensity 1: three concentric cages at 1050 / 600
   / 200 with big empty rooms between them. This is the headline check — if you see one cage or
   the layers look adjacent, the Cell is not on `IntensityWise` or the configs are out of order.
3. **Openings are TRIANGLES** on every cage at intensity 1–2, with no dense polar cap anywhere
   (fly a full orbit — the weave should look the same from every angle).
4. **Intensity changes the SHAPE.** Relaunch at intensity 3 → the **outer** cage is a BOX with
   square openings and heavy corner posts. Intensity 4 → the **middle** one is a box too, and
   the core is visibly the tightest weave (40u).
5. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should
   report **9,206 / 11,456 / 11,680 / 12,870** prisms for intensities 1–4. If it disagrees, the
   generator and `wildlife_cage_budget.py` have drifted — fix both.
6. **Spawn outside, on the equator.** All players start on ONE horizontal circle ~1150u out,
   facing the jail, with the whole thing visible ahead. Nobody starts inside it. Also check
   Crystal Capture still spawns on its sphere (tetrahedral) and Ribcage on its own ring — those
   scenes must be unchanged.
7. **THE KILL PATH — the load-bearing check.** Shoot a tadpole (1 body prism, so one hit): it
   should **die** — wither/suction out and drop an elemental crystal — not keep swimming. Then a
   brittlestar (10 prisms): it should take ten and die on the last one. Your kill counter should
   tick once per creature, not once per prism.
8. **Only YOUR kills count.** Watch a shark eat a tadpole, and watch one starve. Neither should
   move any score. Shoot a cage bar — no score either.
9. **THE HEADLINE CHECK — every tier is everywhere, and the density is even.** Fly a full lap of
   each room. You should meet quadfish, brittlestars, sharks **and** the worm colony in all four,
   including the open water you spawn in — **no room is a boss room any more**. Above all, look
   down the middle: **there must be no clump at the centre of the arena.** That is the failure
   this pass exists to fix and it had two causes, so check both — if creatures are stacked at the
   centre, either `SpawnFaunaBanded`/`RandomPointInBand` is not being reached at all (everything
   defaults to `host.transform.position`), or the volume-uniform draw has been reverted to
   `Random.Range(inner, outer)`, which alone puts 63% of the population in the innermost
   quarter-volume.
9b. **Population grows.** Note the rough headcount at the countdown (~519) and again three
   minutes in: it should be visibly denser, heading toward ~1,198 — that is reproduction. If it
   is flat, the species are hitting their caps immediately or nothing is feeding.
9c. **⚠ NEW — watch the cage erode, and judge whether it is too fast.** The roam band made the
   bars food (see "What the pens were silently buying"). Some grazing is expected and correct.
   Note roughly how open the **core** cage looks at the countdown and at the end of a full match:
   if it has been chewed open enough that the innermost room stops reading as a room, the levers
   are `SpawnableWildlifeCage.RoamInner` (raise it off 0) then `POPULATION_SCALE` — **never a
   shield on the bars**, which would fuse the lattice and cost the one-hit break-in.
10. **Sparrow only — SOLO.** Pick a different vessel in an earlier game, then launch this: you
    should spawn a Sparrow, with a `clamping selected vessel` line in the log.
11. **Sparrow only — MULTIPLAYER (the Ribcage regression).** Have the CLIENT fly a Dolphin in the
    menu (vessel-changer toy), then have the host launch. The client must spawn a **Sparrow**,
    with a `does not allow Dolphin; spawning Sparrow instead` warning on the host, and every AI
    must be a Sparrow too. Then return to the menu and confirm the client can pick a Dolphin
    again — the restriction must not leak out of the game scene.
12. **A CLIENT'S KILLS SCORE.** In a real lobby (host + at least one client), have the CLIENT
    do all the killing for 30 s. Their counter — and their DOMAIN's panel — must rise on **both**
    machines. If it rises only on the client, the `ReportFaunaKill_ServerRpc` path is broken —
    and note the reverse test is not equivalent, because the host records directly.
13. **Everyone starts at 0 (the other Ribcage regression).** In a real multiplayer lobby (host +
    at least one client), check every score panel reads 0 at the countdown — **including after a
    rematch and after playing a previous game in the same session.**
14. **Win + scoreboard.** First player to the target ends the turn; the winner shows a time,
    everyone else "N Kills Left". Confirm a *teammate* of the winner does **not** get the
    winner's time. Replay (scene reload) resets the milestones and the counters.
15. **Milestones.** When the leading domain reaches **8** kills the device should shake hard
    for ~1.2 s; again at **15**. Nothing else should change. (These are 0.25 / 0.5 of the 30
    target — they move with it.)
16. **AI hunts.** Watch an AI Sparrow for a minute: it should sweep one room, actually shoot
    creatures, then move inward. If it orbits a wall without hunting, a waypoint has been moved
    onto a shell.
17. **Danger traps are core-only.** Ram a bar on the outer and middle cages — one hit, no
    penalty. Ram one in the core — some bars there should full-stop you, debuff all four
    elements for 4 s and reset boost.
18. **Regression — the platform kill path.** Play **Skim Race** (intensity 3), **Brood Rush**,
    and Menu_Main freestyle. Wildlife should behave normally, and should now be killable by
    shooting/ramming its body prisms in those modes too. Confirm nothing dies that did not die
    before *without* being hit.
19. **Regression — the band change.** Confirm every unbanded biome is unchanged (bands default to
    0 = off): Blob/Menu_Main fauna should roam the whole cell as before.
20. **Collider + creature telemetry** on device via DiagnosticsHUD / the Benchmark tool, at
    intensity 4 (the worst case: 15,296 colliders, 868 creatures). **This is the number most
    likely to force a change.**
21. **Pacing.** Time intensity 1 end to end — see the pacing flag under "End condition".

## Known limitations / follow-ups

- **The Clawfish is deliberately not in the roster.** Its prefab carries **no `HealthPrism` at
  all**, so it has no body to shoot and cannot be killed by a Sparrow. Putting un-scoreable
  creatures in a hunt would read as a bug. Giving it a body is a prefab change and would let it
  join the outer room.
- **500 is unmeasured** — see the pacing flag.
- **Toast copy is unauthored.** The four `GameToastSituation` values exist but no
  `GameToastConfigSO` authors definitions, so they are silently skipped (which is how a mode opts
  out). Author a `GameToastConfig_WildlifeLiberation.asset` with `{0}`=hunter, `{1}`=kills,
  `{2}`=target to make them visible. `WildlifeCoreBreached` has **no publisher yet** — it is
  reserved for a "somebody got into the core" callout.
- **Cage radii do not vary with intensity.** "Bigger cages at later intensities" was interpreted
  as *denser and boxier*, because the outer radius is what the spawn ring, the AI aim points and
  the arena silhouette are all defined against (the same reason Ribcage fixes its outer radius).
  Growing the inner two shells at high intensity is a one-line change to `SHELL_RADII` if the
  tighter rooms are wanted.
- **No objective-arrow provider**: like Rampage and Ribcage,
  `MiniGameHUD.CreateObjectiveProviderForGameMode` has no case — the wildlife is all around you,
  so there is no single point to aim at.
- **No UGS stats reporter yet** (a "most creatures killed" leaderboard is a clean follow-up), and
  no dedicated end-game controller — the shared scoreboard handles it.
- **The worm colony's segments are not individually banded.** Only the colony brain binds a
  `FaunaConfigurationSO`, so its segments follow the head rather than being penned themselves.
  In practice the head's band keeps the whole colony in the core; a colony that grew a very long
  tail could trail a segment past the wall.
- **Very heavy is very heavy.** See the collider-budget section. If intensity 4 will not hold
  frame rate on device, lower `POPULATION_SCALE` in `wildlife_cage_budget.py` first — the
  creature count, not the cage, is the cost.
