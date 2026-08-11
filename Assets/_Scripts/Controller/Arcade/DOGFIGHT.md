# Dog Fight — Technical Documentation

> **Naming.** `GameModes.DogFight = 41` is the code/data/enum identity. The player-facing
> `DisplayName` on `ArcadeGameDogFight.asset` is **"Dog Fight"** too — no split today, but if
> one is ever wanted, change the DisplayName only (the Tournament/"Maelstrom" and
> Ribcage/"Peel the Cage" precedent). Do not rename the enum, the controller, the scene, or
> this file.

## Overview

Dog Fight is the **Sparrow-only gun duel**. Two to four pilots hunt each other through the
**Boneyard** — a wrecked world of hollow hulks, leaning spires and rubble canyons built for
close encounters and hiding places. A **bullet hit scores 1**, a **missile hit scores 50**
(direct strike *or* caught in the blast), and the first **DOMAIN** to the point target
(default **500**) wins.

**One axis, and it is gunnery.** The scored stat is `IRoundStats.CombatPoints` — a weighted sum
of landed vessel-vs-vessel hits. Nothing else scores: not the wreckage, not crystals, not
wildlife. **A pilot who spends the match demolishing scenery loses to one who spends it
shooting people**, and that is the whole design.

**It is the platform's first mode whose score comes from vessel-vs-vessel combat.** Every other
multiplayer mode races prisms (Rampage, Ribcage), crystals (Skim Race, Scurry), goals (Astro
League), fauna waves (Brood Rush) or fauna kills (Wildlife Liberation). Landing a shot on
another *pilot* had no scoreboard anywhere before this.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity` (single unified scene,
  cloned from Rampage's skeleton — no separate singleplayer variant; solo play is a party of
  one + AI backfill)
- **GameMode enum**: `GameModes.DogFight = 41`
- **Controller**: `DogFightController : MultiplayerDomainGamesController` — a structural sibling
  of `RampageController` / `WildlifeLiberationController` (1 round / 1 turn, `HasEndGame=false`,
  server winner detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus
  progress milestones and the AI dogfighters
- **Scoring**: `DogFightScoringRuleSO` (`metric = ScoringMetric.CombatPoints`; golf-timed) — the
  winning domain's pilots score their finish time, everyone else the `GolfScoreSentinels`
  sentinel (displayed "N Points Left")
- **Turn monitor**: `DogFightPointTurnMonitor` — resolves the target from
  `EndConditionOverridesSO.GetDogFightPointTarget()` (default **500**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
  `GameDataSO.CombatPointTargetCount`
- **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2`, `MaxDomainsAllowed = 3`
- **Vessels**: **Sparrow only** — enforced by the three platform layers Wildlife Liberation put
  in place (see "Sparrow-only" below)
- **Config**: `_SO_Assets/Games/ArcadeGameDogFight.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## Why it is a TEAM race and not a free-for-all

This is the design decision everything else hangs off, and it is forced by the impact layer, not
chosen for flavour.

`Projectile.DisallowImpactOnVessel` refuses **own-domain** contact, and `ExplosionImpactor`
skips own-domain vessels unless the blast is running friendly fire. **Teammates therefore cannot
shoot each other at all.** With four players and only three playable domains
(Jade / Ruby / Gold), two of them must share a colour — and in a free-for-all those two would be
unable to fight, which is not a scoring quirk but a pair of players with no legal targets.

So domains **are** the sides here:

| players | domains | shape |
|---|---|---|
| 2 | 2 | 1v1 |
| 3 | 3 | three-way |
| 4 | 2 | 2v2 |
| 4 | 3 | 2v1v1 |

Points pool per domain and `MinDomainsAllowed = 2` makes a one-domain lobby unlaunchable. The
alternative — a per-mode "everyone is an opponent" flag threaded through the projectile and
blast paths — was considered and rejected: it would make friendly fire a *mode* property of a
system that currently has exactly one rule, which is the kind of carve-out CLAUDE.md's
Universality section warns about.

> Contrast Wildlife Liberation, the platform's free-for-all: its prey is **fauna**, so
> teammates sharing a domain costs nothing. A dogfight cannot borrow that shape.

## The pipeline (zero bespoke tracking)

```
Sparrow lands a shot on an OPPOSING vessel
  ├─ direct hit   → ProjectileImpactor.AcceptImpactee → VesselCombatHitByProjectileEffectSO
  └─ blast        → ExplosionImpactor.AcceptImpactee  → VesselCombatHitByExplosionEffectSO
        │  (both claim the SAME VesselCombatHitLatch window per shooter/victim/class)
        ▼
GameDataSO.OnCombatHitLanded.Raise(CombatHitStats)          [shooter's machine only]
        ▼
StatsManager.CombatHitLanded
        ├─ [server]  credit directly
        └─ [client]  Player.ReportCombatHit_ServerRpc()  (projectiles are LOCAL objects -
                     see "Multiplayer" below; without this only the host could score)
        ▼
CombatHitScoring.Credit  →  BulletHitsLanded++ / MissileHitsLanded++          [server]
                            CombatPoints += rule.PointsForCombatHit(class)
        ▼
ScoringMetrics.Read(stats, CombatPoints)
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ DogFightPointTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached      [server]
  ├─ DogFightController.SampleProgress → leading domain + milestones           [server]
  └─ ElementalComebackSystem (source CombatPoints, per-DOMAIN) → trailing-side buff
        │  turn end
        ▼
DogFightController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

## Where the point VALUES live, and why

The platform counts landed hits as **raw facts** (`BulletHitsLanded` / `MissileHitsLanded`) and
has no opinion about what one is worth. `DogFightScoringRuleSO` says a bullet is 1 and a rocket
is 50, through the new `ScoringRuleSO.PointsForCombatHit` virtual (default **0** — every other
mode counts gunnery and scores none of it).

`CombatHitScoring.Credit` applies that weighting **once, server-side, at the instant of the
hit**, and banks the result in `IRoundStats.CombatPoints`. That is deliberate: it keeps
`ScoringMetric.CombatPoints` a plain cumulative int, so it sums by domain, drives the HUD, feeds
the comeback system and orders the scoreboard through exactly the same shared machinery as
crystals or prisms — **with no weighting table anywhere in the metric reader**. Deriving the
score at read time instead would have meant teaching `ScoringMetrics.Read` about a per-mode
config, which is the coupling this avoids.

The two raw counts are kept **as well as** the total because in a gun duel they are the
interesting half of the story: 300 bullets and 6 rockets are the same score, and the scoreboard's
secondary line says so (`"312 pts · 64×● 5×◆"`).

## Multiplayer: how a client's hit reaches the server

**This is the one place the mode needed real networking, and it needed it because of the
PROJECTILES, not the scoring.**

A prism sits at the same world position on the host and on every client, so when a client rams
one the server's own physics sees the same collision with the same attribution and
`StatsManager` records it server-side — which is why Rampage and Ribcage need no RPC at all.

**Projectiles are not like that.** A bullet or a skyburst is a pooled **local** object spawned by
whichever machine's gun fired it: no `NetworkObject`, no RPCs, no replication. A shot a client
just landed does not exist on the server at all. Recorded server-only, **only the host could ever
score.**

So `StatsManager.CombatHitLanded` has a client branch — the second one in that class, after the
fauna path, and for the same underlying reason:

```
server (host + every AI, which is server-owned)      → credit RoundStats directly
client, and shooterName == this machine's player     → Player.ReportCombatHit_ServerRpc(class)
                                                          → server credits THAT Player's stats
```

**Identity comes from RPC ownership, not from the name string**: `RequireOwnership` is the
default and the server credits the RoundStats of the `Player` object the RPC arrived on, so a
client can only ever credit itself. The hit class travels as an int and is re-validated
server-side rather than trusted.

If an AI's gun happens to also fire on a client, that client sees the name mismatch and drops
the hit; the server's own copy is the one that counts. No configuration makes that
double-count.

> A client can spam the RPC to inflate its own score. So can it spam the joust and fauna-kill
> RPCs. Anti-cheat is out of scope for the party-game layer; noted so nobody assumes otherwise.

## The latch: why one rocket cannot score twice

`VesselCombatHitLatch` deduplicates by **(shooter, victim, weapon class)** over a short window
(0.5 s for missiles, 0.05 s for bullets). Two independent sources of double-counting make it
necessary, and they are why the projectile effect and the explosion effect share **one** latch
rather than each carrying its own:

1. **A rocket scores through two code paths for one shot.** A skyburst that hits a vessel
   directly *detonates on impact* (`VesselSpinBySkyBurstProjectileEffectSO.detonateOnHit`), so
   the direct hit fires from `ProjectileImpactor` and the blast fires again from
   `ExplosionImpactor` a fraction of a second later. One missile, two events — and at 50 points
   each that is not a rounding error.
2. **A hull is more than one collider.** The Squirrel carries two box colliders and the Manta a
   body per wing, so a single blast sphere raises `OnTriggerEnter` once per pair.
   `VesselImpactor` already latches *crystals* for exactly this reason.

The window is therefore also an anti-spam floor: two genuinely different rockets landing on the
same pilot inside 0.5 s score once. That is intended — a dogfight should reward two hits a
second apart, not a shotgun of simultaneous detonations.

The generator **asserts** the two missile effects carry the same non-zero cooldown, because
splitting them silently reinstates the double-count.

## The platform change: the skyburst's blast can now touch a pilot

`AOEConicSkyBurst.prefab`'s `ExplosionImpactor` shipped with `explosionImpactorDataContainer:
{fileID: 0}` — a **null container** — so `AcceptImpactee`'s vessel branch returned immediately
for every pilot it engulfed. **A Sparrow rocket has never done anything to a pilot it did not
hit dead-on.** This branch gives it `SkyBurstExplosionImpactorDataContainer.asset`, which is
what makes "caught in the missile's blast radius" a real event.

Scoped to the **conic** prefab deliberately. The same detonation also spawns the shared
`AOEExplosion.prefab`, which the Manta's crystal path uses; hanging a "missile hit" on that
would label a crystal blast as gunnery in every mode. The conic burst is the big one (scale
100–170) and is Sparrow-only, so it is the honest place for this.

**It affects every other mode**, and in one direction only: a skyburst blast can now run vessel
effects. Today the container holds *only* the scoring effect, so outside Dog Fight the observable
change is that `BulletHitsLanded` / `MissileHitsLanded` start accumulating (worth 0 points
everywhere else). Verify in-editor rather than assuming — checklist item 11.

## The Boneyard (the arena)

`SpawnableBoneyard : CellEnvironmentSpawnableBase`, seed 41, deterministic per seed like every
cell environment. Built from the same vocabulary as `SpawnableAtlantis` — the intensity-4 Scurry
world this was asked to be inspired by — and aimed at the opposite feeling. **Atlantis is a
drowned garden-city that grew; the Boneyard is what is left after one fell.**

Every family exists to answer one question: *where can a pilot hide, and how does the other pilot
find them?* A dogfight in open space is a jousting match — two vessels see each other from 800
units out and converge head-on forever. So the arena breaks sightlines at three scales:

| family | role |
|---|---|
| **Fallen hulks** | Colossal hull sections, ribbed and **hollow**, plated over ~half their circumference so the torn-open side is a way IN. **The hiding places** — the only spots in the arena where a pilot can be genuinely invisible, and a hunter has to commit to entering. |
| **Shattered spires** | Snapped, leaning towers. Cover you fly *around*, at the scale of a single turn, with overhangs to duck under. |
| **Skeleton frames** | Open girder cages. **See-through cover**: stops a rocket, not a sightline — a pilot can watch a missile eat a girder. Also roofs over a patch of the warren. |
| **Broken overpasses** | Arcs with a **collapsed span**. The gap is the feature: shoot through it, dive through it, or misjudge it. |
| **Crust** | A shallow paraboloid bowl of tilted slabs, deliberately porous (~14 u spacing against ~9 u plates) — ground you can drop *through*. |
| **Rubble / ash** | Floor litter and suspended fallout. Neither stops anyone; both fill the volume with parallax, which is what makes a fast pass *read* as fast. Ash is skimmable, so cutting the drift also feeds. |
| **The reactor** | One unmissable centre landmark ("meet me at the reactor") — the only super-shielded mass in the arena, ringed with danger, so the most visible point is also the worst place to sit still. |

**Altitude is the arena's central trade.** The crust is a bowl and all the cover stands on it, so
the lower half of the volume is a warren and the upper half is open sky. Down low you are hidden,
slow, and constantly nearly hitting things; up high you are fast and can see everything — and so
can everyone else. Nothing enforces this; it falls out of the geometry.

**Intensity ramps the DENSITY OF COVER and nothing else** — more wrecks, tighter warrens, shorter
sightlines — through the four prefab variants' structure counts plus the base `density` knob. The
arena **radius is fixed at 520 at every intensity**, for the same reason Ribcage and the wildlife
cages fix theirs: it is what the spawn shell, the AI's fallback aim point and the silhouette are
all defined against.

Analytic budget (`Tools/Build/boneyard_budget.py`; confirm with FrogletTools ▸ Ecology ▸ Measure
Cell Environment Baselines):

| intensity | density | hulks | spires | frames | overpasses | prisms | volume | danger |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.75 | 6 | 9 | 4 | 3 | **12,746** | 850,965 | 61 |
| 2 | 0.90 | 8 | 12 | 5 | 4 | **16,100** | 1,074,639 | 81 |
| 3 | 1.05 | 10 | 15 | 6 | 5 | **19,460** | 1,298,664 | 104 |
| 4 | 1.20 | 13 | 19 | 8 | 7 | **24,097** | 1,600,003 | 139 |

Comparable to Ribcage (10,620 → 20,153) and Wildlife Liberation (9,206 → 13,956 of cage), and
**far cheaper than Atlantis** (~69k, itself flagged as un-profiled). That was a deliberate
choice: this arena carries four Sparrows' worth of projectile and AOE traffic on top of the
structure, which no other mode does.

**Every bar is a one-hit plain prism** except the sparse danger and the rationed armour:

- **Danger** (61–139) rides only the **torn end ribs** of hulks and the reactor's hot inner ribs
  — telegraphed by the geometry rather than hidden in it. Contact costs the standard danger
  punishment (volume-independent full-stop slow, 4 s all-element debuff, boost reset).
- **Shielded / super-shielded** is the reactor core ring (24) plus one beacon per spire — **33–43
  always-on convex mesh colliders, 0.18–0.26 % of the structure**. Beacons are shielded rather
  than plain so they *survive* a match: a landmark a stray rocket can delete is not a landmark.
  **Do not armour the wreckage** — a shielded hulk would be both un-shootable cover and a few
  thousand permanent mesh colliders.

**`boneyard_budget.py` is a MIRROR, not an estimate.** The generator imports it, so the arena and
its PhaseThresholds cannot drift. That is only possible because the C# was written for it: every
per-unit count is a `const`, the only count-affecting rejections are deterministic (the reactor's
blow-out wedge, the overpass's collapsed span) and are reproduced exactly, and
`spawnClearRadius` is **0** on every variant so `Emit`'s clearance rejection never fires. Re-tune
**there** and re-run the generator rather than hand-editing YAML.

### The scavengers

`SupportedFaunas` holds one species: QuadFish, banded to nothing (the whole cell), 60→120 at seed
and capped 150→300 by intensity. **Deliberately light.** They are atmosphere — scavengers picking
over the wreck — and a crystal source for elemental progression when nobody is in your sights.
They are **not** a second heavy system: every fauna body prism is a *mover* that re-buckets in
`PrismSpatialIndex` each frame, and this arena is already carrying the structure above plus heavy
projectile traffic. Killing one scores **nothing** (Dog Fight reads `CombatPoints`, not
`LifeformsKilled`).

**No flora** — the rooms are meant to read as wreckage, not overgrowth.

## Spawning

Players spawn on a **sphere at r = 700** (`CellSpawnFormation.Formation.Symmetric`), outside the
wreck field (520) and inside the membrane (1200), all facing the cell — so every pilot's opening
move is to fly in.

Symmetric rather than Ribcage's `EquatorialRing` because **a dogfight arena has no meaningful
"up"**: the crust is a bowl, not a floor with a ceiling, so there is no pole to be unfair about,
and a spherical spread means the opening merge comes from every direction instead of everyone
converging on one plane.

The cell has **no nucleus**, so the ring has nothing to measure off and uses
`spawnRingRadiusFloor`. (No nucleus is itself deliberate: the Boneyard's centre is a *structure*,
not a territorial claim, and a node-control zone would be a second silent objective nobody is
playing for.)

## AI dogfighters

**The AI's guns need no wiring.** The Sparrow prefab's `AIPilot` already runs `FullAutoAction`
(3 s on / 0.8 s off) and `SkyBurstGunAction` (2 s / 5 s) on their own cooldowns, so an AI with an
opponent in front of it is already shooting. What `DogFightController.ArmDogfighters` decides is
what *"in front of it"* means.

- **Lead pursuit, not pure pursuit.** `AIPilot` has no arrive-and-stop behaviour — it steers at
  its target forever and flies through on arrival — so an AI aimed at an opponent's *current*
  position permanently trails them and only ever fires where they were. The aim point is
  `aiLeadSeconds` (0.6) ahead along the quarry's own course.
- **Break off on the merge.** Inside `aiBreakOffDistance` (120) the aim point flips to a spot
  *beyond* the quarry, so the AI commits to an overshoot and comes back around instead of
  grinding hull-to-hull. Without it, "steer at the enemy forever" degenerates into a ramming
  contest neither pilot can shoot their way out of — the same class of mistake as Wildlife
  Liberation's AI orbiting a cage wall, and the exact inverse of Rampage, where ramming **is**
  the scoring verb.
- **Quarry selection** re-runs every `aiRetargetSeconds` (1.5) and takes the nearest live
  opponent, so a pilot who flies into a brawl is picked up by whoever is closest rather than
  every AI converging on one victim. Between samples the AI keeps flying lead pursuit on the
  pilot it already chose (the provider is sampled every frame, so the aim point tracks a live
  position even though the *choice* is slow).

`ServerPlayerVesselInitializerWithAI` also adds Dog Fight to its `shouldSeekPlayers` set, so an
AI that somehow never receives an external provider still hunts pilots rather than crystals.

## Progress milestones

At a quarter and a half of the point target, the **leading domain** crosses a rung:
`SampleProgress` (server, every 0.5 s) → `AnnounceMilestone_ClientRpc` → a `GameToastSituation`
post plus `HapticController.PlayAlert()` on every peer. A lead change after the first milestone
posts `DogFightLeadChanged`.

These are **pure feedback — they change no game state**, so a missed or late sample costs a toast,
never a rule. Toast copy is unauthored today, so **right now the shake IS the milestone feedback**
(same state as Ribcage and Wildlife Liberation).

## Everyone starts at zero

Ribcage shipped a bug where some players began a match on a non-zero score. `RoundStats` lives on
the **persistent** Player NetworkObject and survives every scene load, so a missed reset carries
the previous game's stats straight in. Four layers here:

1. **`ServerPlayerVesselInitializer`** calls `player.PrepareForNewScene()` unconditionally, once
   per player, on the processing path (the platform fix).
2. **`DogFightController.OnNetworkSpawn`** sweeps all three combat stats to 0 (server only).
3. **`OnCountdownTimerEnded`** sweeps again — the last moment before anyone can score, by which
   point a late joiner is on the roster.
4. **`VesselCombatHitLatch.Clear()`** on spawn *and* on replay, on **every** peer — `Time.time`
   keeps running across a scene load, so a fast rematch could otherwise inherit a claimed window
   and silently eat the first hit of the new match.

`IRoundStats.Cleanup()` zeroes `BulletHitsLanded`, `MissileHitsLanded` and `CombatPoints`, and
`IRoundStatsCleanupTests` asserts all three — **anything added to `IRoundStats` must be zeroed
there and asserted there.**

## Sparrow-only

Enforced in **three** places, all reading the single `Vessels` entry on
`ArcadeGameDogFight.asset`. This is not belt-and-braces for its own sake — Ribcage shipped with
two of these and a client still flew a Dolphin:

1. **`GameDataSO.SyncFromArcadeGame`** clamps `selectedVesselClass` on the machine that pressed
   Start, on every route (modal, rematch, Tournament chain).
2. **`ServerPlayerVesselInitializer.ResolveSpawnVesselType`** re-clamps **server-side at spawn**.
   This is the one that matters in multiplayer: `Player.NetDefaultVesselType` is an OWNER-write
   NetworkVariable each client sets from its own local config and the menu's vessel-changer toy,
   so a client walks in still wearing the hull it last flew.
3. **`ServerPlayerVesselInitializerWithAI`** clamps the **AI's** class too. The scene also
   authors Sparrow directly, so the clamp should never have to fire.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.dogFightPointTarget`, 0 = default **500**) — the points **one domain**
must bank. Live/Build split + build auto-restore work like every other mode. The milestone rungs
are fractions of it (0.25 / 0.5), so they land at 125 and 250.

> **⚠ Pacing flag — 500 has not been playtested.** It is the number that was asked for, not a
> measured one, and the two routes to it are wildly different lengths: **500 bullet hits** at the
> full-auto's ~5 shots/s (assuming a generous hit rate) is a long grind, while **10 rockets** is
> very short. Expect real matches to sit somewhere between, and expect the *ratio* (1 vs 50) to
> be the more interesting dial than the target — a rocket is currently worth fifty bullets, which
> makes the skyburst the whole game if its hit rate is anywhere near a tenth of the cannon's.
> Both values and the target are single editor fields (`DogFightScoringRule.asset` for the
> values, the End Game Conditions window for the target).

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameDogFight.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/DogFightScoringRule.asset` |
| Combat-hit SOAP channel | `_SO_Assets/Event Channels/Event_CombatHitStats.asset` |
| Bullet scoring effect | `_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset` |
| Missile direct-hit effect | `_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissile.asset` |
| Missile blast effect | `_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset` |
| Skyburst explosion container | `_SO_Assets/Effects/Effect Containers/Explosion Containers/SkyBurstExplosionImpactorDataContainer.asset` |
| Cell configs (4) | `_SO_Assets/Cell Configs/Boneyard Cell/Boneyard Cell Config {1..4}.asset` |
| Spawn profiles (4) | `_SO_Assets/Cell Configs/Boneyard Cell/Boneyard Spawn Profile {1..4}.asset` |
| Scavenger configs (4) | `_SO_Assets/Cell Configs/Boneyard Cell/Boneyard Scavenger {1..4}.asset` |
| Arena prefabs (4) | `_Prefabs/Spawnables/SpawnableBoneyard{1..4}.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameDogFight.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`dogFightPointTarget`) |

Every asset above is authored by `Tools/Build/author_dogfight_assets.py` — deterministic GUIDs,
idempotent, validates before writing. **Re-tune there and re-run** rather than hand-editing the
YAML. `Tools/Build/boneyard_budget.py` is the arena's analytic model and the generator
**imports** it, so the wreckage and the PhaseThresholds cannot drift apart.

The generator also makes **three edits outside the mode**, and they are the load-bearing wiring
for the whole feature — without them Dog Fight has a scoring rule and nothing that raises it:
the bullet effect onto `SparrowFullAutoProjectileImpactContainer`, the missile effect onto
`SparrowSkyBurstProjectileImpactContainer`, and the new explosion container onto
`AOEConicSkyBurst.prefab`. All three are asserted before anything is written.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `DogFight = 41` |
| `CombatHitClass` | new enum (`Bullet` / `Missile`) |
| `IRoundStats` / `RoundStats` | `BulletHitsLanded`, `MissileHitsLanded`, `CombatPoints` (+ events, + server-write NetworkVariables, + `Cleanup`, + `ClearEventSubscriptions`) |
| `ScoringMetric` / `ScoringMetrics.Read` | `CombatPoints = 8` |
| `ScoringRuleSO` | `PointsForCombatHit` virtual — a mode's opinion of what a landed hit is worth (0 everywhere else) |
| `CombatHitScoring` | the ONE place a hit becomes numbers, shared by the server path and the client RPC |
| `VesselCombatHitLatch` | shared (shooter, victim, class) dedup — see "The latch" |
| `VesselCombatHitByProjectileEffectSO` | new impact effect: a direct hit landed |
| `VesselCombatHitByExplosionEffectSO` | new impact effect: an opponent caught in a blast |
| `ExplosionImpactor` | `SourceVessel` accessor (null for an anonymous blast) |
| `CombatHitStats` + `ScriptableEventCombatHitStats` + `EventListenerCombatHitStats` | new SOAP payload type |
| `GameDataSO` | `OnCombatHitLanded` channel + `CombatPointTargetCount` |
| `StatsManager` | `CombatHitLanded(CombatHitStats)` + a code-side SOAP subscription, and the class's SECOND client branch (see "Multiplayer") |
| `Player` | `ReportCombatHit_ServerRpc(int)` — owner-side hit report; identity comes from RPC ownership |
| `ElementalComebackSystem` | `ScoreDifferenceSource.CombatPoints` (per-DOMAIN) |
| `EndConditionOverridesSO` (+ window + asset) | `dogFightPointTarget` live/build/getter, default 500 |
| `GameToastSituation` | `DogFightQuarterDown = 57`, `DogFightHalfDown = 58`, `DogFightLeadChanged = 59` |
| `ServerPlayerVesselInitializerWithAI` | Dog Fight added to the `shouldSeekPlayers` modes |
| `AOEConicSkyBurst.prefab` | given the explosion container it never had — a skyburst BLAST can now reach a pilot |
| `IRoundStatsCleanupTests` | asserts the three new stats zero |

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameDogFight.unity`. Every script reference resolves (no "Missing (Mono
   Script)"), the controller's inspector shows `rule` = DogFightScoringRule, the milestone
   fractions 0.25 / 0.5, and the AI fields 1.5 / 0.6 / 120; the **Cell shows four configs with
   Cell Type Choice = Intensity Wise**.
2. **The arena builds.** Launch at intensity 1: a bowl of crust with 6 hulks, 9 leaning spires,
   4 girder cages, 3 broken overpasses, and the reactor at the centre. If you see one structure
   or an empty bowl, the Cell is not on `IntensityWise` or the configs are out of order.
3. **Hulks are hollow and enterable.** Fly INTO one through its torn-open side and sit there.
   Confirm the ribs leave gaps you can slip between, and that you cannot be seen from outside.
   **This is the headline check** — if the hulks read as solid tubes, the plating arc is wrong.
4. **Intensity changes the density of cover.** Relaunch at intensity 4: visibly more wrecks,
   shorter sightlines, and a noticeably busier crust. Same arena radius.
5. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should
   report **12,746 / 16,100 / 19,460 / 24,097** prisms for intensities 1–4. If it disagrees, the
   generator and `boneyard_budget.py` have drifted — fix both.
6. **Spawn outside, on a sphere.** All players start ~700 u out, spread over a sphere, facing the
   arena with the whole thing visible ahead. Nobody starts inside it. Also check Crystal Capture
   still spawns on its sphere and Ribcage on its own ring — those scenes must be unchanged.
7. **BULLETS SCORE — the load-bearing check.** Shoot an opponent with the full-auto: your score
   should tick **+1 per hit**, and shooting a hulk, the crust, or a scavenger should move it by
   **nothing**.
8. **MISSILES SCORE 50, ONCE.** Hit an opponent dead-on with a skyburst: **+50, not +100** (the
   direct hit and its own blast both fire — the latch is what makes it one). Then detonate one
   *near* an opponent without touching them: also **+50**. This is the pair of checks the whole
   latch exists for.
9. **A CLIENT'S HITS SCORE.** In a real lobby (host + at least one client), have the CLIENT do
   all the shooting for 30 s. Their score must rise on **both** machines. If it rises only on the
   client, the `ReportCombatHit_ServerRpc` path is broken — and note the reverse test is not
   equivalent, because the host records directly.
10. **Teammates score nothing.** In a 2v2, shoot a teammate: no damage, no points, and the
    scoreboard does not move. Splash one with a rocket: same.
11. **Regression — the skyburst blast.** Play **Wildlife Liberation** and freestyle: a skyburst
    that engulfs another vessel should now be *possible* but should still do nothing visible
    beyond the existing spin, since the container holds only the scoring effect. Confirm the
    Manta's crystal blast (shared `AOEExplosion.prefab`) is unchanged.
12. **Everyone starts at 0.** In a real multiplayer lobby, every score panel reads 0 at the
    countdown — **including after a rematch and after playing a previous game in the same
    session.**
13. **Win + scoreboard.** First domain to the target ends the turn; the winning side shows a
    time, everyone else "N Points Left", and the secondary line reads `N pts · X×● Y×◆`. Confirm
    a *teammate* of the winner DOES get the winner's time (this is a team race — the opposite of
    the Wildlife Liberation check). Replay (scene reload) resets the milestones and the counters.
14. **Milestones.** When the leading domain reaches **125** points the device should shake hard
    for ~1.2 s; again at **250**. Nothing else should change.
15. **AI dogfights.** Watch an AI Sparrow for a minute: it should chase a pilot, *shoot*,
    overshoot on the merge, and come back around. If it grinds hull-to-hull, `aiBreakOffDistance`
    is not being applied; if it circles empty space, the quarry search found nothing.
16. **Danger is where it looks dangerous.** Ram a hulk's torn end rib and the reactor's inner
    shell — some should full-stop you, debuff all four elements for 4 s and reset boost. Ram a
    spire or the crust — one hit, no penalty.
17. **Pacing.** Time a full match at intensity 1 end to end — see the pacing flag under "End
    condition". Note separately how many points came from bullets vs rockets.
18. **Collider + frame telemetry** on device via DiagnosticsHUD / the Benchmark tool, at
    intensity 4 with 4 players (the worst case: 24,097 structure prisms + up to 300 scavenger
    body prisms + four Sparrows' projectile and AOE traffic).

## Known limitations / follow-ups

- **500 is unmeasured, and so is the 1:50 ratio** — see the pacing flag.
- **Hits are not replicated as FEELING, only as score.** The victim's spin / debuff runs on the
  shooter's machine (projectiles are local), so a pilot being shot does not see themselves get
  knocked about the way the shooter does. That is pre-existing behaviour for every Sparrow
  weapon, not something this branch introduced, but a dogfight is the first mode where it
  matters — a `ClientRpc` broadcast of the confirmed hit (the joust's
  `NetworkVesselImpactor.ExecuteJoust_ClientRpc` shape) is the clean fix and is deliberately out
  of scope here.
- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors definitions, so they are silently skipped (which is how a mode opts
  out). Author a `GameToastConfig_DogFight.asset` with `{0}`=domain, `{1}`=points, `{2}`=target
  to make them visible.
- **No objective-arrow provider**: like Rampage, Ribcage and Wildlife Liberation,
  `MiniGameHUD.CreateObjectiveProviderForGameMode` has no case — your objective is another
  pilot, so there is no fixed point to aim at. An arrow to the nearest opponent would be a
  genuinely good addition and is the most obvious follow-up.
- **No UGS stats reporter yet** (a "most missile hits" leaderboard is a clean follow-up), and no
  dedicated end-game controller — the shared scoreboard handles it.
- **A 4-player / 3-domain lobby is 2v1v1**, which is not balanced. The lobby allows it because
  the domain count is the host's choice; if it plays badly, pin `MaxDomainsAllowed` to 2 and let
  4 players always be 2v2.
- **The scavengers are not banded.** They roam the whole cell, including the open space outside
  the wreck field. Penning them to the warren (`FaunaConfigurationSO.BandInner/BandOuterRadius`,
  the Wildlife Liberation capability) would concentrate them in the cover, which may or may not
  be better — untested.
