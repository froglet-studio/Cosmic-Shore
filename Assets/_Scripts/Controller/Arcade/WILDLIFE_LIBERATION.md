# Wildlife Liberation — Technical Documentation

> **Naming.** `GameModes.WildlifeLiberation = 40` is the code/data/enum identity. The
> player-facing `DisplayName` on `ArcadeGameWildlifeLiberation.asset` is **"Wildlife
> Liberation"** too — no split today, but if one is ever wanted, change the DisplayName only
> (the Tournament/"Maelstrom" and Ribcage/"Peel the Cage" precedent). Do not rename the enum,
> the controller, the scene, or this file.

## Overview

Wildlife Liberation is the **Sparrow-only hunt**. Three concentric cages at **1050 / 600 /
200** pen three tiers of wildlife, with a very wide empty room between each pair. Break in and
shoot; the **first PLAYER** to the kill target (default **500**) wins.

**One axis, and it is the ecology.** The scored stat is `IRoundStats.LifeformsKilled` — an
*attributed creature death*. Nothing else scores: not cage prisms, not rival trails, not
crystals. A creature that starves, or that a shark eats, credits **nobody**.

**This is the platform's first free-for-all race.** Every other multiplayer mode here (Skim
Race, Joust, Scurry, Rampage, Ribcage, Brood Rush, Astro League) resolves a winning **domain**
from a per-domain sum. This one resolves a winning **player**, because with four hunters in one
cage, pooling two of them would let somebody win off a teammate's kills. The domain sums are
still computed, synced and shown on the in-game HUD — they are a secondary "how is my colour
doing" readout, not the win condition. See "Per-player scoring" below.

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
  golf-timed) — the winning hunter's `Score` is their finish time, everyone else the
  `GolfScoreSentinels` sentinel (displayed "N Kills Left")
- **Turn monitor**: `WildlifeKillTurnMonitor` — resolves the target from
  `EndConditionOverridesSO.GetWildlifeKillTarget()` (default **500**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
  `GameDataSO.LifeformTargetCount`
- **Players**: **1–4** with AI backfill. `MinDomainsAllowed = 1`, `MaxDomainsAllowed = 3`
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
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels (secondary)
  ├─ WildlifeKillTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached       [server]
  ├─ WildlifeLiberationController.SampleProgress → leading hunter + milestones [server]
  └─ ElementalComebackSystem (source LifeformsKilled, PER-PLAYER) → trailing-hunter buff
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

> **Known limitation (inherited, not introduced).** Because fauna diverge per peer, each hunter
> is shooting their *own* copy of the swarm. Populations are statistically identical (same
> configs, same seed floors, same rooms) but not the same creatures, so two players cannot race
> for the same kill. That is `Docs/ECOSYSTEM.md` §7 caveat 4 — "not yet fair for competitive
> play" — and the honest fix is server-authoritative fauna, which is a platform project, not a
> mode feature. It is also why the win condition being **per-player** rather than per-domain is
> more than a design preference here: a shared team total across diverging simulations would be
> harder to reason about than four independent hunts.

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

## Per-species containment bands (the pens)

`Cell.FaunaContainmentRadius` — Ribcage's brood pen — is **one radius, whole cell**. Three
nested cages need three pens, so the capability is generalized to an **annulus authored per
species**: `FaunaConfigurationSO.BandInnerRadius` / `BandOuterRadius`, honoured by

- `Fauna.Goal`'s setter (the one point every goal writer passes through — `ResolveGoal`, Boid's
  override, LightFauna's half-dozen direct writes, the spawner's initial goal, and
  `TryReproduce`'s inheritance), which composes the cell pen first and then the band;
- `Fauna.IsPreyForMe`, the shared edibility predicate every grazer now routes through
  (`LightFauna.IsEdibleForHerbivore`, `WormFauna.IsEdiblePrism`, `Boid.IsEdibleForForager`);
- `RandomLifeSpawner.BandGoal`, so a wave **hatches** inside its room rather than swimming home
  through mass it may not eat.

Same contract as the cell pen and for the same reason (`Docs/ECOSYSTEM.md` §22): **a spatial
DIET + STEERING rule, never a wall.** Nothing is teleported, no collider is added, nothing is
culled for crossing a boundary. A creature can still drift out on its own momentum — it simply
has no reason to and nothing to eat there. `0 = no band` is the default and what every shipped
biome authors, so nothing else changes. Offspring inherit their parent's band for free (they
bind the same config).

**The bands are why the cage can be cheap.** Each band stops **60u short of its own walls**
(`SpawnableWildlifeCage.BandWallClearance`), so a creature's jail is outside its band and
therefore not food. Without that, herbivores would eat two thirds of their own cage (the bars
are painted across the domain triad, and the legacy diet eats opposing-domain mass) and the
alternative — shielding the bars — would swap ~9,000 LOD-cullable BoxColliders for always-on
convex MeshColliders. **Do not shield the cage.**

| room | band | wall |
|---|---|---|
| outer | 660 .. 990 | 1050 |
| middle | 260 .. 540 | 600 |
| core | 0 .. 140 | 200 |

Authored from **one source**: `SpawnableWildlifeCage.BandInner/BandOuter` in C#, mirrored by
`wildlife_cage_budget.band_inner/band_outer`, which is what the asset generator writes into the
fauna configs. The generator refuses to write a band that is not strictly inside its room.

## The jail

`SpawnableWildlifeCage : CellEnvironmentSpawnableBase`, seed 40, deterministic per seed like
every cell environment. **This is not Ribcage.** Ribcage is a layered orange whose bone *is* the
score — dense, tight, five rinds. This is a sparse lattice of long bars with big triangular
openings, so the arena reads as mostly empty space: here the prisms are only the walls.

- **Three shells, always**, at a **fixed** 1050 / 600 / 200. The shell count is deliberately
  **not** the intensity dial: each shell walls in a tier of wildlife, so dropping one would
  delete a third of the game rather than make it easier.
- **Enormous radial gaps** — 450u between the outer and middle cages, 400u between the middle
  and the core. Each room is a place you fly *into*, not a rind you pass through.
- **The openings are TRIANGLES, from a GEODESIC** (subdivided icosahedron), not latitude hoops.
  That is a fairness property: a latitude sphere is densest at its poles, which is why Ribcage
  must tilt every rind onto its own axis so nobody drills the top. A geodesic has no poles —
  every approach meets the same weave — so this cage needs no tilt table at all.
- **Intensity ramps the SHAPE and the WEAVE.** Intensities 1–2 are three geodesic spheres;
  3 swaps the outer cage for a **BOX** (square rail grid, heavy corner posts — "the boxing
  ring"), 4 makes the middle one a box too. The core stays geodesic at every intensity so the
  innermost room keeps the "cell" read. A box is a genuinely different problem: its flat faces
  mean an approach is either square-on at a long dense wall or into a corner where three walls
  converge.
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
| 2 | geodesic f6 | geodesic f5 | geodesic f4 | **11,456** | 82 | 210 / 144 / 60u |
| 3 | **box** f13 | geodesic f6 | geodesic f5 | **11,680** | 85 | 93 / 120 / 48u |
| 4 | **box** f14 | **box** f13 | geodesic f6 | **12,870** | 99 | 87 / 53 / 40u |

> **The box frequencies are much higher than the geodesic ones and that is not a typo.** A cube
> face grid at frequency *f* contributes 12*f*² segments against a geodesic's 30*f*², and the box
> is smaller (corners on the radius ⇒ faces at 0.577·r). Matching frequencies would make the
> *harder* intensities lighter **and** more open than the easy ones. The values come from the
> measured table in `wildlife_cage_budget.py`. Re-tune **there** and re-run the generator, never
> by eye.

## The wildlife (the objective)

One `FaunaConfigurationSO` per **(species, room, intensity)** — the spawner runs one loop per
config — banded to its room and scaled by intensity ("later intensities will have more fauna").

| species | room | seed | cap (I1) | level | body prisms ea. |
|---|---|---:|---:|---:|---:|
| Tadpole | outer | 180 | 260 | 1 | 1 |
| QuadFish | outer | 90 | 130 | 1 | 1 |
| Brittlestar | outer | 14 | 20 | 1 | 10 |
| Brittlestar | middle | 26 | 40 | 2 | 10 |
| QuadFish | middle | 20 | 30 | 3 | 1 |
| Shark (predator) | middle | 10 | 16 | 2 | 11 |
| Shark (predator) | core | 6 | 10 | 5 | 11 |
| Worm Colony (kaiju) | core | 3 | 5 | 3 | ~26 |

Populations scale ×1.0 / ×1.2 / ×1.45 / ×1.7 with intensity:

| intensity | creatures (seed) | creatures (cap) | body prisms (cap) |
|---|---:|---:|---:|
| 1 | 349 | 511 | 1,436 |
| 2 | 419 | 613 | 1,721 |
| 3 | 505 | 740 | 2,068 |
| 4 | 593 | 868 | 2,426 |

`PopulationSize` is a **seed floor**, not a population: the spawner only tops a species back up
to it (bootstrap + extinction recovery). Everything above comes from reproduction and is bounded
by starvation — the food web, not a timer (`Docs/ECOSYSTEM.md` §6). `MaxLivePopulation` is the
performance backstop, which is why the **cap** column is what the collider budget is sized
against.

**`FaunaFoodFloor` is 0 (always produce)**, deliberately: the cage is not edible (bands), so a
prey-gated spawner would never bootstrap the jail. Creatures then feed on whatever a player lays
inside their room — which is the mode's risk/reward: fly in to shoot and your trail becomes
their dinner.

**No flora.** `SupportedFloras` is empty; the rooms are meant to read as empty space with
wildlife in them.

### Collider-budget impact — read this before tuning anything else

| intensity | cage prisms | fauna body prisms (cap) | total |
|---|---:|---:|---:|
| 1 | 9,206 | 1,436 | **10,642** |
| 2 | 11,456 | 1,721 | **13,177** |
| 3 | 11,680 | 2,068 | **13,748** |
| 4 | 12,870 | 2,426 | **15,296** |

Comparable to Ribcage (10,620 → 20,153) in raw collider count — but **the fauna half is far more
expensive per collider than the cage half**, and that is this branch's headline performance risk:

- **Every fauna body prism is a MOVER.** It re-buckets in `PrismSpatialIndex` as the creature
  swims (`Fauna.NotifyBodyPrismsMoved`), where a cage prism is registered once and never moves.
- **Every creature runs a behaviour coroutine** — 349 to 593 of them at seed, up to 868 at cap.
  This is the number to watch, not the prism count.
- **This is ~7× the masterplan's ≤1,500-per-cell fauna-prism target** and roughly **6× the
  creature count of any shipped biome.** It is an explicit product decision ("very heavy",
  requested 2026-08), not an accident of the roster.

**Measure on device before tuning.** Dials, in order of bluntness: `POPULATION_SCALE` and the
`ROSTER` caps in `wildlife_cage_budget.py` (the creature count — start here), then the cage's
`SHELL_PLANS` frequencies, then `BAR_STEP`. Re-run **both** Python tools after any change.

## Per-player scoring

`WildlifeLiberationScoringRuleSO` overrides exactly what the free-for-all needs and nothing
else:

| member | behaviour |
|---|---|
| `IsObjectiveReached` | scans **players**, not domains |
| `ResolveWinningHunter` | the leading individual; ties break by name (ordinal), so every peer agrees |
| `ResolveWinner` | the winning player's **domain** — only used to colour the banner |
| `RemainingForPlayer` | **your own** deficit (new virtual on `ScoringRuleSO`; defaults to the domain deficit everywhere else) |
| `Remaining(domain)` | a domain's **best** hunter's deficit, not the sum — summing would make a two-player domain look further from winning than a one-player domain level with it |
| `AssignScores` | finish time to the winning **player**; everyone else a sentinel encoding their own remaining kills |

`ElementalComebackSystem` gains `ScoreDifferenceSource.LifeformsKilled`, the one **per-player**
source: the leader is the leading individual and a player's value is their own kills, so a
hunter trailing the top hunter gets the buff even when their colour is ahead.

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

At a quarter and a half of the kill target, the **leading hunter** crosses a rung:
`SampleProgress` (server, every 0.5 s) → `AnnounceMilestone_ClientRpc` → a `GameToastSituation`
post plus `HapticController.PlayAlert()` on every peer. A lead change after the first milestone
posts `WildlifeLeadChanged`.

These are **pure feedback — they change no game state**, so a missed or late sample costs a
toast, never a rule. Toast copy is unauthored today, so **right now the shake IS the milestone
feedback** (same state as Ribcage).

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.wildlifeKillTarget`, 0 = default **500**) — the number of creatures
**one player** must kill. Live/Build split + build auto-restore work like every other mode. The
milestone rungs are fractions of it (0.25 / 0.5).

> **⚠ Pacing flag — 500 has not been playtested.** It is the number that was asked for, not a
> measured one. Intensity 1 holds 349 creatures at seed and re-seeds every 20 s, so 500 kills
> across four hunters is reachable, but whether it lands at two minutes or ten is unknown. It is
> one editor field, and the milestones follow it automatically.

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
(geometry **and** bands **and** roster) and the generator **imports** it, so the walls, the pens
and the PhaseThresholds cannot drift apart.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `WildlifeLiberation = 40` |
| `Fauna` | `OnBodyPrismExploded` default = **die when the last body prism is destroyed** (platform-wide); `ReportKill` publishes attributed deaths; per-species band (`IsInsideBand` / `ClampToBand` / `IsPreyForMe`); `StarvationKiller` constant |
| `WormSegmentFauna` | override thinned to its `_dead` guard — the kill rule moved to the base |
| `LightFauna` / `WormFauna` / `Boid` | edibility routed through `Fauna.IsPreyForMe` (adds the band to the existing cell rule) |
| `FaunaConfigurationSO` | `BandInnerRadius` / `BandOuterRadius` |
| `RandomLifeSpawner` | `BandGoal` — a banded species hatches inside its own room |
| `CellRuntimeDataSO` | `OnFaunaKilled` (`ScriptableEventString`) |
| `StatsManager` | `LifeformKilled(string)` + a code-side SOAP subscription, and the class's ONLY client branch (see "Multiplayer") |
| `Player` | `ReportFaunaKill_ServerRpc()` — owner-side kill report; identity comes from RPC ownership |
| `IRoundStats` / `RoundStats` | `LifeformsKilled` (+ event, + server-write NetworkVariable, + `Cleanup`) |
| `ScoringMetric` / `ScoringMetrics.Read` | `LifeformsKilled = 7` |
| `ScoringRuleSO` | `RemainingForPlayer` virtual — a per-player readout for a free-for-all |
| `GameDataSO` | `LifeformTargetCount` |
| `ElementalComebackSystem` | `ScoreDifferenceSource.LifeformsKilled` — the one per-PLAYER source |
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
9. **The tiers stay in their rooms.** Fly a full lap of each room. The outer room should be
   thick with tadpoles/quadfish plus a few brittlestars; the middle noticeably bigger creatures
   and sharks; the core the kaiju. **Nothing should be swimming between rooms**, and nothing
   should be chewing on a cage bar.
10. **Sparrow only — SOLO.** Pick a different vessel in an earlier game, then launch this: you
    should spawn a Sparrow, with a `clamping selected vessel` line in the log.
11. **Sparrow only — MULTIPLAYER (the Ribcage regression).** Have the CLIENT fly a Dolphin in the
    menu (vessel-changer toy), then have the host launch. The client must spawn a **Sparrow**,
    with a `does not allow Dolphin; spawning Sparrow instead` warning on the host, and every AI
    must be a Sparrow too. Then return to the menu and confirm the client can pick a Dolphin
    again — the restriction must not leak out of the game scene.
12. **A CLIENT'S KILLS SCORE.** In a real lobby (host + at least one client), have the CLIENT
    do all the killing for 30 s. Their counter must rise on **both** machines. If it rises only
    on the client, the `ReportFaunaKill_ServerRpc` path is broken — and note the reverse test is
    not equivalent, because the host records directly.
13. **Everyone starts at 0 (the other Ribcage regression).** In a real multiplayer lobby (host +
    at least one client), check every score panel reads 0 at the countdown — **including after a
    rematch and after playing a previous game in the same session.**
14. **Win + scoreboard.** First player to the target ends the turn; the winner shows a time,
    everyone else "N Kills Left". Confirm a *teammate* of the winner does **not** get the
    winner's time. Replay (scene reload) resets the milestones and the counters.
15. **Milestones.** When the leading hunter reaches **125** kills the device should shake hard
    for ~1.2 s; again at **250**. Nothing else should change.
16. **AI hunts.** Watch an AI Sparrow for a minute: it should work one room, actually shoot
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
