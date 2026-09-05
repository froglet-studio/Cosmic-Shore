# Dog Fight — Technical Documentation

> **Naming.** `GameModes.DogFight = 41` is the code/data/enum identity. The player-facing
> `DisplayName` on `ArcadeGameDogFight.asset` is **"Dog Fight"** too — no split today, but if
> one is ever wanted, change the DisplayName only (the Maelstrom/"Maelstrom" and
> PeelTheCage/"Peel the Cage" precedent). Do not rename the enum, the controller, the scene, or
> this file.

## Overview

Dog Fight is the **Sparrow-only gun duel**. Two to four pilots hunt each other through the
**Boneyard** — a wrecked world of hollow hulks, leaning spires and rubble canyons built for
close encounters and hiding places. A **bullet hit scores 1**, a **missile hit scores 50**
(direct strike *or* caught in the blast), and the first **DOMAIN** to the point target
(default **90**) wins.

**One axis, and it is gunnery.** The scored stat is `IRoundStats.CombatPoints` — a weighted sum
of landed vessel-vs-vessel hits. Nothing else scores: not the wreckage, not crystals, not
wildlife. **A pilot who spends the match demolishing scenery loses to one who spends it
shooting people**, and that is the whole design.

**It is the platform's first mode whose score comes from vessel-vs-vessel combat.** Every other
multiplayer mode races prisms (Rampage, PeelTheCage), crystals (Skim Race, Scurry), goals (Astro
League), fauna waves (Brood Rush) or fauna kills (Wildlife Liberation) — every one of them a
per-DOMAIN sum, and this mode is no exception. Landing a shot on another *pilot* had no
scoreboard anywhere before this.

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
  `EndConditionOverridesSO.GetDogFightPointTarget()` (default **90**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
  `GameDataSO.CombatPointTargetCount`
- **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2`, `MaxDomainsAllowed = 3`
- **Vessels**: **Sparrow only** — enforced by the three platform layers Wildlife Liberation put
  in place (see "Sparrow-only" below)
- **Config**: `_SO_Assets/Games/ArcadeGameDogFight.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)
- **Objective marker**: `DogFightObjectiveProvider` — the off-screen arrow points at the nearest
  vessel you can actually shoot (see below)
- **Crystals**: **four** omni crystals on platform-normal settings (with an authored
  `noNucleusSpawnRadius`, see below) **plus** elemental pickups scattered by `DogFightController`
- **Comeback**: `ScoreDifferenceSource.CombatPoints`, rate **0.12** (see below)
- **Environment**: `SpawnableBoneyard` at all four intensities, 9,043 → 34,654 prisms

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

> **This is now settled platform doctrine, not a Dog Fight opinion.** Wildlife Liberation
> shipped a per-PLAYER winner and it was **reverted** for the same arithmetic: four seats against
> three domains means a full lobby always has teammates, and an individual winner bypasses every
> domain surface (winner banner, HUD panels, scoreboard ordering, `ResolvePlacementOrder`). Dog
> Fight has that constraint *and* an impact layer that refuses own-domain contact, so it is the
> stronger case of the two. See CLAUDE.md's `WildlifeLiberation(40)` entry — "Do not re-derive
> it".

## The pipeline (zero bespoke tracking)

```
Sparrow lands a shot on an OPPOSING vessel
  ├─ direct hit   → ProjectileImpactor.AcceptImpactee → VesselCombatHitByProjectileEffectSO
  │                 (bullet, turret-stance prism round, or a rocket's centre-punch)
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

**Both of the Sparrow's fire modes count as "bullet".** Full-auto rounds and turret-stance prism
rounds are the same weapon class — one direct projectile hit — so
`SparrowPrismProjectileImpactContainer` carries the same `VesselCombatHitByBullet` effect as
`SparrowFullAutoProjectileImpactContainer`, and its container already carried the same two victim
effects (spin + skimmer shrink). Only the **missile** is worth more, and only because a missile
is a different proposition.

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
`StatsManager` records it server-side — which is why Rampage and PeelTheCage need no RPC at all.

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

## The skyburst launches from the missile bay (2026-08)

The rocket no longer materializes at a floating gun point: the press opens the Sparrow's
animated missile bay (right bay first, left bay second) and the projectile — now the model's
own missile, not the wedge polyhedron — spawns **0.2 s later at the live bay bone's pose**
(`SkyBurstGunAction.launchDelaySeconds`; `FireGunActionExecutor` cancels a pending launch on
turn end or vessel teardown, with ammo staying spent). For this mode that means ~0.2 s of
fire-to-impact latency on the 50-point weapon; the scoring path, cooldown latch, hit sphere,
and blast are untouched. Mechanics + tuning:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.

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

## The missile got a proximity fuze (2026-09) — this mode gets faster

A skyburst now detonates when an opposing vessel comes within **20× its own hit radius** (~76 u at
resting MASS) rather than only on contact. A missile hit is worth 50 points here and the mode runs
to 90, so the practical effect is that the EXISTING blast — the conic/sphere pair, radius up to 85 —
routinely catches a pilot the rocket would previously have flown past. Expect shorter matches until
this is retuned.

**Scoring is unchanged, deliberately.** The new warhead blast (which debuffs pilots and kills
wildlife) carries **no** `VesselCombatHitByExplosionEffectSO`, so it does not add a second
50-point event; a rocket still scores once, through its direct hit or the conic blast, sharing one
`VesselCombatHitLatch` window. The lever if this proves too fast is
`proximityFuzeRadiusMultiplier` on `SkyBurstProjectile.prefab`. Full mechanics:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.

Note the Sparrow's omni crystals also changed meaning: they no longer refill the missile tank
(prism destruction does that now) and instead grant 8 s of elemental-debuff immunity. That does not
touch this mode's scoring — `VesselCombatHitByMissile*` runs with `requireDebuffableVictim: false`,
so a warded pilot is still fully scoreable.

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

### Scatter — how the wreckage is distributed

**A boneyard is patchy, and it goes all the way out.** The first pass got this wrong in three
specific ways, and `ScatterPlanar` fixes each one:

1. **Equal-area radii.** Drawing a placement radius uniformly puts far more wreckage per unit of
   *area* near the middle — density falls off as 1/r — which is exactly the "it's all in the
   centre" look. Every radius is now an equal-area draw, and it runs over the **playable
   annulus** (`coreClearRadius` → 0.92 R) rather than the whole disc: spreading equal-area over
   the full disc sounds right and is not, because the innermost anchor lands inside the clearing,
   its whole field gets shoved back out by the clamp, and the rim is left bare.
2. **Debris FIELDS, not a sprinkle.** Structures cluster onto `debrisFields` (7) anchors with
   open lanes between them, and the families **interleave** across those anchors, so a field is a
   mixed tangle of hulk and spire and girder rather than one family's private island.
3. **The centre is empty.** Nothing is placed inside `coreClearRadius` (120), and the reactor
   landmark was moved **off-centre** — a landmark in the middle of a radially symmetric arena
   orients nobody (every bearing off it looks the same) while planting a monolith exactly where
   the field most needs to read as open.

Measured over the shipped structure counts, splitting the arena into three equal-**area** rings:

| version | inner / mid / outer |
|---|---|
| ideal (perfectly even by area) | 33% / 33% / 33% |
| original (uniform radius, centred reactor) | ~58% / 25% / 17% |
| equal-area over the full disc | ~51% / 38% / 11% |
| **shipped** (equal-area over the annulus) | **36–44% / 33–45% / 17–24%** |

The residual inner lean is deliberate-ish and left alone: the very rim being a little sparser
reads naturally for a wreck field, and players spawn *outside* at r=700 and fly in through it.

**Roughly `driftFraction` (40%) of every family never landed.** Drifters hang in the volume above
the crust at any attitude, so there is no single ground plane and a fight can climb *through*
wreckage instead of leaving it. The crust itself now buckles in two octaves — the coarse one
±110 against a bowl that only rises 130 across its whole radius — which breaks the paraboloid
into **shelves** rather than one smooth dish. A smooth dish is a funnel: it points every sightline
and every drifting pilot at the middle.

**Altitude is still a trade.** Most of the heavy cover rests on the shelves, so low is a warren
and high is comparatively open — but the drifters mean high is not *empty*. Nothing enforces
this; it falls out of the geometry.

**Intensity ramps the DENSITY OF COVER and nothing else** — more wrecks, tighter warrens, shorter
sightlines — through the four prefab variants' structure counts plus the base `density` knob. The
arena **radius is fixed at 520 at every intensity**, for the same reason PeelTheCage and the wildlife
cages fix theirs: it is what the spawn shell, the AI's fallback aim point and the silhouette are
all defined against.

### The intensity ladder is SPREAD, deliberately

All four intensities fly the same Boneyard. Intensity 4 briefly flew Scurry's `SpawnableAtlantis`
instead ("make dog fight intensity 4 have the same environment as scurry 4"); the playtest read
after that was the opposite — the Boneyard is right at every level, and what was wrong was that
the levels sat too close together (1 → 4 spanned only **1.9×**, so picking one barely changed the
match). Intensity 4 came back to the Boneyard and the ladder was widened to **3.8×**:

| intensity | density | hulks | spires | frames | overpasses | prisms | volume | danger | step |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.55 | 4 | 6 | 3 | 2 | **9,043** | 601,491 | 43 | — |
| 2 | 0.90 | 8 | 12 | 5 | 4 | **16,100** | 1,074,639 | 81 | +78 % |
| 3 | 1.30 | 13 | 19 | 8 | 7 | **24,807** | 1,652,305 | 139 | +54 % |
| 4 | 1.75 | 19 | 27 | 11 | 10 | **34,654** | 2,314,319 | 203 | +40 % |

**Intensity 2 is byte-for-byte where it was.** The arena was tuned at that level, so the other
three moved around it rather than the whole ladder shifting.

The generator enforces the spread: `author_dogfight_assets.py` fails if any step adds less than
25 % more cover than the level below it, so "intensity" can never quietly become a rounding error
again. Analytic budget from `Tools/Build/boneyard_budget.py` — an exact mirror of the C#, not an
estimate; confirm with FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines.

The top end is the same order as the freestyle cell environments (34–41k), and **half** of
Atlantis (~69k, itself flagged as un-profiled and ~2.8× the largest profiled cohort). That
headroom is deliberate: this arena carries four Sparrows' worth of projectile and AOE traffic on
top of the structure, which no other mode does. PeelTheCage runs 10,620 → 20,153 and Wildlife
Liberation 9,206 → 13,956 of cage, so intensity 4 is now the heaviest party-game arena — **soak it
on device**, and if it will not hold, the scavenger cap (`SCAVENGER_CAP[3]`) is the cheapest thing
to pull before the structure counts.

**Every bar is a one-hit plain prism** except the sparse danger and the rationed armour:

- **Danger** (43–203) rides only the **torn end ribs** of hulks and the reactor's hot inner ribs
  — telegraphed by the geometry rather than hidden in it. Contact costs the standard danger
  punishment (volume-independent full-stop slow, 4 s all-element debuff, boost reset).

  > **The full-stop slow did not exist here until 2026-08-15.** The Sparrow's
  > `SparrowImpactorDataContainer` carried no `VesselChangeSpeedByPrismEffectSO`, so the *only*
  > vessel this mode flies took no speed penalty from any prism — danger ribs included. The
  > danger punishment was really only the debuff and the input mute. `SparrowVesselChangeSpeedByPrism`
  > is now wired on the Squirrel's numbers, which makes this paragraph true and has a second
  > consequence the mode wants: **the wreckage is now terrain.** A normal Boneyard prism is
  > environment-owned (`Domains.Blue`, hostile to everyone, so the own-domain skip never applies)
  > and at `massScaling 0.1` against `maxSlowStrength 0.5` anything of volume ≥ 5 saturates — so
  > clipping a hulk halves your throttle for a second and recovers linearly. Flying the canyons
  > cleanly is now a skill the arena rewards rather than a line you can ignore. Worth a look in
  > the first playtest: it makes cover genuinely costly to hug, which is the point, but it also
  > slows disengages through debris — if it over-punishes, `maxSlowStrength` on that asset is the
  > dial, and moving it un-shares the fleet's collision read.
- **Shielded / super-shielded** is the reactor core ring (24) plus one beacon per spire — **30–51
  always-on convex mesh colliders, 0.15–0.33 % of the structure**. Beacons are shielded rather
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

Symmetric rather than PeelTheCage's `EquatorialRing` because **a dogfight arena has no meaningful
"up"**: the crust is a bowl, not a floor with a ceiling, so there is no pole to be unfair about,
and a spherical spread means the opening merge comes from every direction instead of everyone
converging on one plane.

The cell has **no nucleus**, so the ring has nothing to measure off and uses
`spawnRingRadiusFloor`. (No nucleus is itself deliberate: the Boneyard's centre is a *structure*,
not a territorial claim, and a node-control zone would be a second silent objective nobody is
playing for.)

## The turret muzzles — why turret fire scored nothing

Wiring the scoring effect into `SparrowPrismProjectileImpactContainer` was necessary and not
sufficient. Turret shots still did no damage and scored no points, and the cause was not in the
scoring path at all:

**The Sparrow carries two pairs of gun transforms, one per fire mode, and they had drifted 13.8
units apart.**

| executor | fire mode | `LeftGun` / `RightGun` local position |
|---|---|---|
| `FullAutoActionExecutor` | bullets | `(±3.2, 0.4, `**`1.30`**`)` |
| `FullAutoBlockActionExecutor` | turret prism rounds | `(±3.0, 0.4, `**`15.13`**`)` |

A shot is **born at its muzzle**, so every turret round spawned 15 units ahead of the nose and the
first 15 units of its path simply did not exist. This mode is built for close passes through a
wreck field, so the enemy is routinely *inside* that gap — the round appeared already past them
and hit nothing, no matter how correctly the scoring was wired. Playtest, and exactly right:
*"maybe because the point of origin of bullets for the sparrow is too far away from the model."*

Both pairs are bare `Transform`s — no renderer, no VFX, no children — so the position is purely
where the shot starts. The turret's pair is moved onto the bullets' position, which is also the
documented rule for this weapon (`SPARROW_TURRET_STANCE.md`: *"a turret shot **is** a bullet —
you just see a prism flying"*).

**Range is unaffected.** The executor computes `anchor = muzzle + forward × range`, so moving the
muzzle back moves the anchor back with it; the path length is identical and nothing needs
retuning. The prism now visibly emerges from the gun barrels instead of materialising ahead of
the ship.

The generator asserts this on every run — four gun transforms on the bullets' position, and no
transform left at `z = 15.13`. It is authored on a **shared vessel prefab**, so a silent drift
here breaks the Sparrow in every mode, not just this one.

## AI dogfighters

**The AI's guns need no wiring.** The Sparrow prefab's `AIPilot` already runs `FullAutoAction`
(3 s on / 0.8 s off) and `SkyBurstGunAction` (2 s / 5 s) on their own cooldowns, so an AI with an
opponent in front of it is already shooting. What `DogFightController.ArmDogfighters` decides is
what *"in front of it"* means.

- **Lead pursuit, not pure pursuit.** `AIPilot` has no arrive-and-stop behaviour — it steers at
  its target forever and flies through on arrival — so an AI aimed at an opponent's *current*
  position permanently trails them and only ever fires where they were. The aim point is
  `aiLeadSeconds` (0.6) ahead along the quarry's own course.
- **A COMMITTED break-off, latched at the merge.** Inside `aiBreakOffDistance` (120) the AI
  switches to an `Extend` phase, latches one escape point — straight through the quarry and out
  `aiExtendDistanceMultiplier` (3) × the break-off distance beyond it — and flies *that fixed
  point* until it arrives or `aiMaxExtendSeconds` (4) expires. Only then does it look for a
  quarry again.

  > **This is a rewrite of a version that did not work, and the reason is worth keeping.** The
  > first attempt had no state: it aimed at the quarry, and inside the break-off radius aimed at
  > a point derived from the *current* geometry instead. That point is recomputed every frame, so
  > the instant the AI slipped past its target the vector to it flipped and the "escape" point
  > landed back behind the AI — it turned straight round. Two ships welded together, grinding in
  > a circle. Playtest: *"the AI always try to be close to the player but not run away a bit."*
  > **A break-off has to be a decision the pilot commits to, not a function of where the enemy is
  > this instant** — an escape vector the target can steer is not an escape vector.

- **Separation is what makes the missiles visible.** The skyburst was always on the AI's ability
  list and always fired, on its own timer, whether or not anyone was in front of it. Welded to a
  target at zero range a rocket has no room to fly and its blast has nowhere useful to land, so
  the missiles read as absent. With a real extend the AI comes back in from a few hundred units
  with the target ahead — the geometry a skyburst is for. Turret stance (`ModeSwitchingFire`,
  2 s every 12 s) gets the same benefit. **No weapon or ability wiring was changed**; the loop
  drives `SetExternalTargetProvider` and nothing else, so it cannot leak into another mode.

  Without any of this, "steer at the enemy forever" degenerates into a ramming contest neither
  pilot can shoot their way out of — the same class of mistake as Wildlife Liberation's AI
  orbiting a cage wall, and the exact inverse of Rampage, where ramming **is** the scoring verb.
- **Quarry selection** re-runs every `aiRetargetSeconds` (1.5) and takes the nearest live
  opponent, so a pilot who flies into a brawl is picked up by whoever is closest rather than
  every AI converging on one victim. Between samples the AI keeps flying lead pursuit on the
  pilot it already chose (the provider is sampled every frame, so the aim point tracks a live
  position even though the *choice* is slow).

`ServerPlayerVesselInitializerWithAI` also adds Dog Fight to its `shouldSeekPlayers` set, so an
AI that somehow never receives an external provider still hunts pilots rather than crystals.

## The objective marker

The HUD's off-screen objective arrow (`ObjectiveIndicator`) is auto-created by `MiniGameHUD`
whenever the scene sets `autoCreateObjectiveIndicator` — which the Dog Fight scene inherits — so
the only wiring is a case in `CreateObjectiveProviderForGameMode`.

`DogFightObjectiveProvider` points at the **nearest vessel on a different domain**. It is
deliberately *not* `JoustObjectiveProvider`, which takes the nearest other player regardless of
colour: that is right for Joust, where overtaking a teammate buffs them, and wrong here, where
teammates cannot be damaged at all — in a 2v2 the Joust provider would spend the match pointing
at the one vessel in the arena worth nothing. The domain check is the entire difference between
the two files.

**Nearest**, rather than "whoever is winning" or "whoever is hunting me", on purpose: in an arena
built to break sightlines, the question the arrow has to answer is *"which way is the fight"*
when a hulk has just swallowed your target. Anything cleverer would fight the player's own read
of the situation instead of restoring the one thing the Boneyard takes away.

## Crystals — the omni PLUS a scattered elemental layer

Dog Fight scores **only** gunnery, so crystals are pure elemental progression: worth nothing on
the scoreboard, but a real reason to fly the wreckage between engagements — see the comeback
section below for why Mass in particular pays here.

### The omni crystal runs exactly as it does everywhere else

`crystalCountMode: 0`, `fixedCrystalCount: **4**`, `spawnOnClientReady: 1`. Crystals are a
platform fundamental; a mode that switches one off is a mode where a whole economy silently does
nothing.

**Four, not one.** A single crystal in a 520-unit arena is a needle nobody detours for; four means
there is usually one worth breaking off toward, which is the entire point of having them in a mode
where crystals score nothing. Scurry reaches a similar density by a different route
(`PlayerCountPlusExtra` + 5, i.e. **nine** in a full lobby) — too many here, so this stays on
`FixedCount`.

The scene authors **one** thing the donor did not: **`noNucleusSpawnRadius: 420`**. This is the
whole fix for the bug that made the omni crystal read as an Astro League ball —
`CrystalManager.GetAnchorlessSpawnRadius` falls back to the cell's **nucleus** radius, the
Boneyard has no nucleus *by design*, so it fell through to the crystal's own `SphereRadius` (a few
units) and every spawn landed on the arena's exact centre. A large faceted sphere pinned to the
middle of a gunnery arena reads as *the objective*.

420 puts it inside the wreck field (520) so it hides among the hulks, and inside the spawn shell
(700) so it is never behind a pilot's back at the countdown. Each respawn draws a new point, so it
is a thing you go and find. The field exists for exactly this case — deleting the crystal was
never the fix.

### The elemental layer on top

**`DogFightController.SpawnElementalCrystals`** scatters 14 more — all four elements, equal-volume
through a shell (cube root of a uniform draw) so they spread evenly rather than bunching at the
middle. These are the mode's comeback loop made physical: a trailing pilot is already being
buffed, and Mass stretches the Sparrow's fired prisms, so this is the same reward available to
anyone willing to fly for it.

Two implementation notes, both forced rather than chosen:

- **Runtime provisioning.** The four standalone elemental prefabs (`ElementalCrystalSetSO`)
  deliberately carry *no* collection components — lifeform prefabs author them as overrides — so
  they are scenery until something wires an `ElementalCrystalImpactor` + `ImpactCollider` onto
  them. That recipe is the platform's, not this mode's: it is exactly what the Wanderway's
  `Microscene.MintElementalCrystal` does, down to the collection effects coming off the set.
- **In the controller, not the environment.** The arena is authored per intensity (four
  `SpawnableBoneyard` variants) and pickups have nothing to do with how much wreckage there is, so
  the controller is the one path that covers every intensity from one seed.

> **Known limitation — the crystals are LOCAL.** Placement is deterministic (fixed seed + fixed
> count), so every peer lays the *same* crystals in the *same* places with no network message —
> but **collection is per-peer**: each pilot collects their own copy. That is the standing caveat
> the Wanderway's crystals and the whole fauna simulation already carry
> (`Docs/ECOSYSTEM.md` §7 caveat 4), and it is tolerable here **only because crystals score
> nothing in this mode**. If they ever do, this must become server-authoritative.

## Comeback — all four elements, sized to a 90-point race

`ElementalComebackSystem` runs here on `ScoreDifferenceSource.CombatPoints`, per **domain** like
every other team source: a pilot's deficit is their side's deficit behind the leading colour.

**All four elements rise together.** That is platform law, not a Dog Fight choice —
CLAUDE.md and `ElementalComebackSystem`'s own summary: *"ALL FOUR elements rise EQUALLY … the
per-vessel/per-element weights are retired (equal-elements is the law)."* So
`ComebackRatePerScoreDeficit` is the entire tuning surface, and a Mass-only weighting would be a
fundamentals change requiring sign-off, not a mode setting.

That said, **Mass is the element this mode's buff is actually felt through**, because of what the
Sparrow does with each one: Space scales muzzle speed, Time and Charge pay in their own
currencies, and **Mass is the only one wired to the guns' output** — it stretches the fired prisms
(`SPARROW_TURRET_STANCE.md`). So the equal-elements law and the playtest ask ("more mass for the
player behind") do not actually conflict: all four rise, and the one that changes how your shots
behave is Mass.

**Mass now grows the HIT VOLUME too, not just the silhouette.** It always stretched the prism's
z-axis, but the flying collider was a fixed sphere (`collisionDiameter` 1.65 / `shieldedCollisionDiameter`
2.475), so a Mass-buffed pilot fired visibly bigger rounds that connected exactly as often as
before — the buff was a cosmetic. `FullAutoBlockShootActionExecutor.FireOne` now scales the hit
diameter by **√multiplier**: the prism grows on one axis and the hit volume is a sphere, so the
square root keeps the sphere inside the silhouette it stands in for. At Mass 10 (multiplier 2.5)
the prism is 2.5× longer and the sphere 1.58× wider.

**The rate is a function of the target.** `bonusLevels = deficit × rate`, so a rate only means
anything next to the scale of deficits the mode produces. This shipped at **0.004**, which was
scaled for the original 500-point target and never rescaled when the target changed — a whole
rocket behind (50 points) bought 0.2 of a level, i.e. nothing. It is now **0.12**, against a
**90**-point target:

| deficit | bonus levels | in words |
|---:|---:|---|
| 22.5 (¼ of target) | 2.7 | a couple of exchanges behind |
| 50 (one rocket) | 6.0 | one missile behind |
| 90 (shutout) | 10.8 → **capped at 10** | `ResourceSystem.SustainedCeiling` |

That puts it on the same footing as the other party games (Rampage: a quarter-of-target deficit is
worth ~5 levels). The generator **fails the build** if a quarter-of-target deficit ever buys less
than one whole element level, so the rate and the target cannot drift apart again the way they
already did once.

## Progress milestones

At a quarter and a half of the point target, the **leading domain** crosses a rung:
`SampleProgress` (server, every 0.5 s) → `AnnounceMilestone_ClientRpc` → a `GameToastSituation`
post plus `HapticController.PlayAlert()` on every peer. A lead change after the first milestone
posts `DogFightLeadChanged`.

These are **pure feedback — they change no game state**, so a missed or late sample costs a toast,
never a rule. Toast copy is unauthored today, so **right now the shake IS the milestone feedback**
(same state as PeelTheCage and Wildlife Liberation).

## Everyone starts at zero

PeelTheCage shipped a bug where some players began a match on a non-zero score. `RoundStats` lives on
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
`ArcadeGameDogFight.asset`. This is not belt-and-braces for its own sake — PeelTheCage shipped with
two of these and a client still flew a Dolphin:

1. **`GameDataSO.SyncFromArcadeGame`** clamps `selectedVesselClass` on the machine that pressed
   Start, on every route (modal, rematch, Maelstrom chain).
2. **`ServerPlayerVesselInitializer.ResolveSpawnVesselType`** re-clamps **server-side at spawn**.
   This is the one that matters in multiplayer: `Player.NetDefaultVesselType` is an OWNER-write
   NetworkVariable each client sets from its own local config and the menu's vessel-changer toy,
   so a client walks in still wearing the hull it last flew.
3. **`ServerPlayerVesselInitializerWithAI`** clamps the **AI's** class too. The scene also
   authors Sparrow directly, so the clamp should never have to fire.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.dogFightPointTarget`, 0 = default **90**) — the points **one domain**
must bank. Live/Build split + build auto-restore work like every other mode. The milestone rungs
are fractions of it (0.25 / 0.5), so they land at **22** and **45**.

At 90, the two routes are **90 bullet hits** or **2 rockets** (or any mix), which makes the
skyburst decisive rather than incidental — landing one rocket is worth more than half the race.
Whether that ratio is right is the open question; the target and both point values are single
editor fields (`DogFightScoringRule.asset` for the values). The milestone rungs move with the
target automatically, since they are fractions of it.

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
the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
`SparrowPrismProjectileImpactContainer` (turret stance), the missile effect onto
`SparrowSkyBurstProjectileImpactContainer`, and the new explosion container onto
`AOEConicSkyBurst.prefab`. All four are asserted before anything is written.

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
| `EndConditionOverridesSO` (+ window + asset) | `dogFightPointTarget` live/build/getter, default 90 |
| `GameToastSituation` | `DogFightQuarterDown = 57`, `DogFightHalfDown = 58`, `DogFightLeadChanged = 59` |
| `ServerPlayerVesselInitializerWithAI` | Dog Fight added to the `shouldSeekPlayers` modes |
| `MiniGameHUD` | `CreateObjectiveProviderForGameMode` case for Dog Fight |
| `DogFightObjectiveProvider` | new: nearest OPPOSING vessel (the Joust provider ignores domain) |
| `ElementalCrystalSetSO` | `RandomElementFrom(System.Random)` — a seeded pick, so a scatter can be reproduced identically on every peer |
| `AOEConicSkyBurst.prefab` | given the explosion container it never had — a skyburst BLAST can now reach a pilot |
| `IRoundStatsCleanupTests` | asserts the three new stats zero |

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameDogFight.unity`. Every script reference resolves (no "Missing (Mono
   Script)"), the controller's inspector shows `rule` = DogFightScoringRule, the milestone
   fractions 0.25 / 0.5, and the AI fields 1.5 / 0.6 / 120 / 3 / 4; the **Cell shows four configs
   with Cell Type Choice = Intensity Wise**.
2. **The arena builds.** Launch at intensity 1: a bowl of crust with 4 hulks, 6 leaning spires,
   4 girder cages, 3 broken overpasses, and the reactor at the centre. If you see one structure
   or an empty bowl, the Cell is not on `IntensityWise` or the configs are out of order.
3. **Hulks are hollow and enterable.** Fly INTO one through its torn-open side and sit there.
   Confirm the ribs leave gaps you can slip between, and that you cannot be seen from outside.
   **This is the headline check** — if the hulks read as solid tubes, the plating arc is wrong.
4. **THE LEVELS MUST FEEL DIFFERENT — the intensity check.** Launch intensity 1 and intensity 4
   back to back. 1 should read as an *open field* with knots of wreckage you have to fly to,
   where a fleeing Sparrow struggles to break line of sight; 4 should read as a *maze* you can
   lose someone in within a second. Same arena radius, ~3.8× the cover. If 1 and 4 feel like the
   same match at slightly different densities, the ladder is still too tight.
4b. **Every intensity is the Boneyard.** Intensity 4 is *not* Atlantis (it was, briefly) — if you
   see Scurry's drowned garden-city with its world-tree and terraces, the intensity-4 config is
   pointing at the wrong prefab. Watch the frame rate here specifically: 34,654 prisms is the
   heaviest arena of any party game.
5. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should
   report **9,043 / 16,100 / 24,807 / 34,654** prisms for intensities 1–4. If it disagrees, the
   generator and `boneyard_budget.py` have drifted — fix both.
6. **Spawn outside, on a sphere.** All players start ~700 u out, spread over a sphere, facing the
   arena with the whole thing visible ahead. Nobody starts inside it. Also check Crystal Capture
   still spawns on its sphere and PeelTheCage on its own ring — those scenes must be unchanged.
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
    a *teammate* of the winner DOES get the winner's time — teammates pool, so they share the
    win. Replay (scene reload) resets the milestones and the counters.
14. **Milestones.** When the leading domain reaches **125** points the device should shake hard
    for ~1.2 s; again at **250**. Nothing else should change.
15. **AI DOGFIGHTS — it must LEAVE.** Watch an AI Sparrow for a minute. The loop should read as
    *close → pass → run out a long way → turn → come back in*, with a visible gap between passes.
    If it stays glued to you circling, the extend is not committing (check that
    `aiExtendDistanceMultiplier` / `aiMaxExtendSeconds` reached the scene). If it circles empty
    space, the quarry search found nothing.
15b. **AI MISSILES.** On the run back in, the AI should be launching skybursts at standoff range —
    that is the geometry they were always missing, not new wiring. If you still never see one,
    the problem is `SkyBurstGunAction`'s ammo, not the steering.
16. **SCATTER — the arena must not read as centred.** From the spawn shell, the wreck field
    should look *patchy and spread to the rim*: knots of structure with open lanes between them,
    an obviously empty middle, and the reactor off to one side rather than dead ahead. Fly the
    rim: there should be real cover out there, not a bare edge. Fly high: drifting wrecks should
    hang above the crust at odd angles, so the upper volume is not empty sky.
17. **Crystals: FOUR omni, and they move.** Four big faceted spheres, none at the arena centre —
    scattered out among the wreckage inside r≈420. Collect one and confirm the respawn lands
    somewhere *else*. If they spawn stacked dead centre, the scene has lost its
    `noNucleusSpawnRadius`. Alongside them, ~14 small elemental crystals spread through the
    arena in all four colours, each skimmable for an element level; confirm they appear at
    **every** intensity and that two peers see them in the SAME places.
17b. **COMEBACK — Mass in particular.** Let one domain get ~50 points ahead, then check the
    trailing pilot's element flowers: all four should be visibly filled (≈6 levels of bonus).
    Switch that pilot to **turret stance** and watch the fired prisms — they should be noticeably
    longer than the leader's. Close the gap and the buff should drain back. If the flowers barely
    move, `ComebackRatePerScoreDeficit` is not reaching the vessel.
17c. **TURRET PRISMS DAMAGE AND SCORE — the regression check.** In turret stance, land a prism
    round on an opponent: it must **do damage** and read **+1**, exactly like a bullet. Do this at
    CLOSE range specifically (inside ~15 units) — that is the case that was completely dead before
    the muzzle fix. Shooting wreckage with it still scores nothing.
17d. **Mass grows what you HIT WITH.** With Mass buffed, turret rounds should be both visibly
    longer *and* easier to land. If they look bigger but feel identical to aim, the hit diameter
    has stopped riding the multiplier.
18. **The objective arrow points at an ENEMY.** In a 2v2, confirm the marker tracks an opposing
    pilot and never your wingman, and that it re-targets when your quarry disappears behind a
    hulk.
19. **Danger is where it looks dangerous.** Ram a hulk's torn end rib and the reactor's inner
    shell — some should full-stop you, debuff all four elements for 4 s and reset boost. Ram a
    spire or the crust — one hit, no penalty.
20. **Pacing.** Time a full match at intensity 1 end to end — see the pacing flag under "End
    condition". Note separately how many points came from bullets vs rockets.
21. **Collider + frame telemetry** on device via DiagnosticsHUD / the Benchmark tool, at
    intensity 4 with 4 players (the worst case: 24,097 structure prisms + up to 300 scavenger
    body prisms + four Sparrows' projectile and AOE traffic).

## Known limitations / follow-ups

- **90 is unmeasured, and so is the 1:50 ratio** — at this target a single rocket is **56%** of
  a domain's whole race, which is either the drama of the mode or its flaw. The target has now
  moved 500 → 120 → 90 without a measured match behind any of them. See the pacing flag under
  "End condition".
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
- **No UGS stats reporter yet** (a "most missile hits" leaderboard is a clean follow-up), and no
  dedicated end-game controller — the shared scoreboard handles it.
- **A 4-player / 3-domain lobby is 2v1v1**, which is not balanced. The lobby allows it because
  the domain count is the host's choice; if it plays badly, pin `MaxDomainsAllowed` to 2 and let
  4 players always be 2v2.
- **The scavengers are not banded.** They roam the whole cell, including the open space outside
  the wreck field. Penning them to the warren (`FaunaConfigurationSO.BandInner/BandOuterRadius`,
  the Wildlife Liberation capability) would concentrate them in the cover, which may or may not
  be better — untested.
