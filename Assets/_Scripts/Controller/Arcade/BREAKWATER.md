# Breakwater — Technical Documentation

> **Naming.** `GameModes.Breakwater = 48` is the code/data/enum identity, and the player-facing
> `DisplayName` on `ArcadeGameBreakwater.asset` is **"Breakwater"** too. A breakwater is a barrier
> you have to get past to reach harbour, and the mode is fifteen of them in a row.

> **Status.** The whole mode is **committed** across three commits - the arena model and the pure
> geometry, the runtime, then the assets, the scene, the generator and this document.
> **Nothing here has been run in the Unity editor.** Every number below comes from
> `Tools/Build/breakwater_arena.py`'s own output or from a file that was read, quoted rather than
> retyped, and the C# was compiled and EXECUTED outside the editor against Unity-type stubs - but
> a stub is not the engine. See *In-editor verification* for what that does and does not buy, and
> note in particular that the scene still carries the Salvo donor's two `GlobalObjectIdHash`
> values and needs one open-and-save before it is flown in a real lobby.

## Overview

Breakwater is the **Sparrow-only station race**. Fourteen ordered **stations** hang on a walk
through the cell, every pilot flies the **same course in order**, and the first **DOMAIN** whose
**lead runner** threads the last one wins.

A station is a switch with a wall in front of it. Each one is a shallow **dish** of plates that
flares back toward the pilot, its throat welded shut by a triple-rake weave of **danger** bars,
with an 18-unit **eye** at the centre ringed by a twelve-block **keystone collar**. You close on
it with two rockets in the bay and make one choice:

| verb | what it costs | what it buys |
|---|---|---|
| **fire** | half the missile tank (50 hostile prisms' worth of ammunition) | a 50-unit spherical hole, cut wherever you aimed |
| **saw** | your speed — turret stance stops you dead | the weave ground open with free gun rounds |
| **thread** | nothing at all | a 1.46× hull clearance with danger bars a hull-width away |

**The walls you shoot ARE the ammunition.** `ammoPerPrism` is `0.01` on `Sparrow.prefab` and
`SkyBurstGunAction.asset`'s `ammoCost` is `0.5` against a tank of 1, so **50 hostile prisms buy a
rocket and the bay holds two** — a station's dish roughly funds the next door, and a clean thread
banks a rocket for a station you cannot read. The economy closes with no pickup anywhere on the
course.

What actually **scores** is none of the three verbs: it is the `RaceGateRing` at the port radius.
The verbs are three **prices for the same crossing**, which is why the mode needs no rule saying a
sawn station counts as much as a shot one.

**Key architectural facts:**

- **GameMode enum**: `GameModes.Breakwater = 48` (`Drumfire = 47` was the previous highest; 7 and
  31 stay reserved forever). `EnumIntegrityTests.GameModes_HasExpectedMemberCount` 46 → 47 in the
  same commit, plus `[TestCase(GameModes.Breakwater, 48)]`.
- **Controller**: `BreakwaterController : MultiplayerDomainGamesController` — 1 round / 1 turn,
  `HasEndGame = false`, `UseGolfRules = true`, `UseSceneReloadForReplay = true`, server winner
  detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`; plus the course roll, its
  broadcast, the arena stand-up and the crossing loop.
- **Course generator**: `BreakwaterCourse` — pure, deterministic, no `UnityEngine.Random`, no
  `System.Random`, no `Time`, no scene access. A deliberate **fork** of `SwitchbackCourse`.
- **Station geometry**: `BreakwaterStationBuilder` — **closed form, zero random draws**.
- **Arena**: `SpawnableBreakwater : CellEnvironmentSpawnableBase` — the stations plus the shoals,
  laid as 106 separate trails.
- **Turn monitor**: `BreakwaterStationTurnMonitor` — resolves the station count from
  `EndConditionOverridesSO.GetBreakwaterStationTarget()` (default **14**, FrogletTools ▸ Game
  Modes ▸ End Game Conditions — never a per-scene field), syncs via NetworkVariable →
  `GameDataSO.SwitchTargetCount`.
- **Scoring**: `ScoringMetric.SwitchesThreaded` (**9**, reused), golf-timed, folded
  `BestByDomain`. `BreakwaterScoringRule.asset` will be a **second asset** on the existing
  `SwitchbackScoringRuleSO` — zero new scoring code.
- **Objective arrow**: `BreakwaterObjectiveProvider`, wired in `MiniGameHUD.ResolveObjectiveProvider`.
- **Comeback**: `ScoreDifferenceSource.SwitchesThreaded` (**8**, reused), rate **0.35** in the model
  (a quarter-of-target deficit = 3.5 stations → **2.45** element levels).
- **Vessels**: **Sparrow only.** **Players**: 2–4 with AI backfill (intended; the card that
  declares it is unwritten).

**Only one new enum value lands in the whole branch.** Everything else about scoring is reused —
see *One metric, reused*.

---

## The correction that the design rests on

The mode was designed around a claim about the Sparrow's skyburst that **turned out to be false**,
and finding that out changed which prefab cuts the door and removed the branch's only stated
platform prerequisite. Both are recorded here because they are worth more than the feature.

### 1. `AOERadialBlocks` lays a CONE SHELL, not a starburst — and there is no bore

The winning pitch rested on the skyburst's radial blocks laying *"a 12-ray starburst with a
40-unit clear bore"* — i.e. that the rocket itself punched a hole through a wall and left a
readable star of prisms around it. Read against
`Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs`, that is not what it does:

```csharp
private void CreateRay(int rayIndex, Trail trail)
{
    float angleStep = 360f / Mathf.Max(1, numberOfRays);   // computed…
    for (int b = 0; b < blocksPerRay; b++)
    {
        float radius = Random.Range(minRadius, maxRadius);
        …
        float randomRot = Random.Range(0f, 360f);
        Quaternion spreadRot = Quaternion.AngleAxis(spreadDeg, axis);
        Quaternion aroundRay = Quaternion.AngleAxis(randomRot, rayDirection);
        Vector3 spreadDir    = aroundRay * spreadRot * rayDirection;
```

`angleStep` is **computed and never used**, and `rayIndex` never reaches the direction — it only
reaches the block's id string. Every block is placed at a **fixed `raySpread` off the forward
axis** at a **random azimuth**, at a **random radius** in `[minRadius, maxRadius]`. So on
`AOEConicSkyBurst.prefab`'s authored values — `numberOfRays: 12`, `blocksPerRay: 6`,
`raySpread: 20`, `minRadius: 20`, `maxRadius: 80`, `baseBlockScale: {3, 3, 10}`, `shielded: 1`,
`ExplosionDelay: 0` + `SecondaryExplosionDelay: 0.11`, one ray per frame — a skyburst deposits
**72 shielded prisms scattered over a 20° forward cone shell, in the shooter's own domain, over
twelve frames starting 0.11 s after detonation**. It is a **cairn**: a monument to a rocket
somebody fired here. There is **no bore**, and it destroys nothing (`ExplodeAsync` opens with
`DisableConicExplosion()`).

**The door is cut by a different prefab entirely.** `SparrowSkyBurstProjectileImpactContainer` →
`DetonateEndEffect.asset` lists **two** AOE prefabs, `AOEConicSkyBurst.prefab` and
`AOEExplosion.prefab`, and it is the second — plain and **spherical** — that destroys mass.
`ProjectileDetonatorSO` computes `targetScale = Lerp(MinScale, MaxScale, Clamp01(proj.Charge))`
over the asset's authored `minExplosionScale: 100` / `maxExplosionScale: 170`, and
`MaxScale` is a **diameter** for the spherical blast, so:

| CHARGE | blast radius |
|---|---|
| resting | **50 u** |
| 10 | **85 u** |
| 15 (overcharge) | **85 u** — `Mathf.Clamp01` |

(`SPARROW_SKYBURST_BAY.md` states the same band independently: *"the existing prism blast is
radius 50–85 u depending on CHARGE"*.)

**That correction is what makes the intensity ladder honest, and it runs both ways.** The port
radius ladder straddles the **resting** blast on purpose:

- **The hard end is pinned UNDER it.** Intensity 4's port is **42** against a resting radius of
  50, so a pilot with **no upgrade at all** clears a whole plug with one rocket. This closes the
  trap `WILDLIFE_LIBERATION.md` records from the other side: a core promise must never be gated
  behind an element level, because **the comeback system hands element levels to whoever is
  LOSING**.
- **The easy end is pinned OVER it.** Intensity 1's port is **72**, wider than the resting blast,
  so one rocket cannot take the whole plug and **where** you cut is a real choice — cut on the eye
  and you have widened the thread; cut on the rim and you have opened a door off the racing line.
  That choice exists only because `72 > 50`.

`BreakwaterCourseTests.ThePortLadderStraddlesTheRestingBlastRadius` asserts both ends, and
`breakwater_arena.py` prints them:

```
  tightest port 42 vs resting-Charge blast 50 -> one rocket clears the whole plug at I4: True
  widest port 72 vs resting-Charge blast 50 -> aim CHOICE exists at I1: True
```

The cairn is kept, unmodified, as the **social layer**: 72 shielded prisms in the shooter's colour
sitting where a rival spent a rocket. Nothing in this mode reads them.

### 2. The "mandatory platform prerequisite" that was not one

The spec named one branch-blocking gate: flip `shielded: 1 → 0` on `AOEConicSkyBurst.prefab`,
because the cairn would otherwise mint roughly 4,032 always-on convex `MeshCollider`s over a
match and blow the cell's collider budget before a station was laid.

**Checked against the shipped code, it would not.** `shieldMeshCollider.enabled = true` appears
**nowhere in the codebase** — grep finds the field declared on both `PrismOctahedronShield` and
`PrismStellatedOctahedronShield` and assigned `= false` at four sites, never `true`. That is
exactly what CLAUDE.md already states: *"a shield swaps the MESH and the mass, never the
collider."* A shielded prism carries the same LOD-cullable `BoxCollider` an unshielded one does.

So **no shipped prefab was touched**, no other mode was perturbed, and the branch's stated go/no-go
never existed. *A go/no-go gate is worth verifying before you pay for it* — the cost of checking
was one grep; the cost of paying would have been a platform-wide change to a weapon four modes
already ship.

**The reason never to shield a Breakwater plug is different, and it is not contingent on
anything.** A shield engages the **circumscribing octahedron** and reaches `1.5 × leafSize`
(`Docs/ECOSYSTEM.md` §35). On a 12-unit rake pitch with a 3-unit bar cross-section, that fuses the
weave into a solid tube and deletes both the saw and the thread — two of the three verbs the mode
is built on. Toughness here is bought with **more bars or a narrower port**, never with a tier.
`SpawnableBreakwater`'s class remarks say so, and deliberately do **not** cite the collider, so
the rule does not evaporate the day that field is wired up.

---

## One metric, reused

**A station is a switch threaded in order.** That is not an analogy — it is the same fact
`ScoringMetric.SwitchesThreaded = 9` already records, so the entire scoring layer is reused
verbatim:

| reused wholesale | what it does here |
|---|---|
| `ScoringMetric.SwitchesThreaded = 9` | the stat, the score, the progress bar, the next-station index, the report token |
| `IRoundStats.SwitchesThreaded` + its NetworkVariable and mirror | replication, with nothing added |
| `SwitchThreadScoring.Credit(stats, index)` | the whole validation: `index != stats.SwitchesThreaded` rejects a client claiming station 13 from the start line **and** a duplicate of one already paid |
| `Player.ReportSwitchThreaded_ServerRpc(int)` | the client→server half of owner-detects/server-records |
| `GameDataSO.SwitchTargetCount` | the goal row's target |
| `ScoringMetrics.BestByDomain` + `ScoringRuleSO.DomainValue` | the lead-runner fold, on the one seam all five domain readers go through |
| `ScoreDifferenceSource.SwitchesThreaded = 8` | the comeback source (one added `case` in `DefaultSourceFor`) |
| `ObjectiveIconSet.asset` metric 9 | icon `eae5dbed618cd04cc66a6089b7c2d10d`, label **"Thread switches"** — already present, **no asset edit** |
| `ModeControlsLibrary.asset` metric 9 | the launch-panel objective icon — already present, **no asset edit** |
| `SwitchbackScoringRuleSO` | a **second asset**, not a second script |

**"THREAD SWITCHES 3/14" is literally correct, not a compromise.** The goal-stack row is keyed on
the `ScoringMetric`, never on the game mode (`Docs/GAME_MODE_TOPBAR.md` §2), so a new mode picking
an existing metric gets a correct goal line for free — and here the shipped label happens to be
the true description of what a Breakwater pilot does. The mode contributes **one** new enum value
in the entire branch: `GameModes.Breakwater = 48`.

**What is NOT shared, and why not.** Two classes are structural clones rather than reuses, and
that is a measured conclusion:

- `SwitchbackGateTurnMonitor` names `GetSwitchbackGateTarget()` and
  `FindFirstObjectByType<SwitchbackController>()`. Both would have to become parameters, which is
  a shared base class whose only members are *which override key* and *which controller type* — a
  generic seam holding two constants, in exchange for a second file both modes must be read
  through.
- `SwitchbackObjectiveProvider` is hard-bound to `SwitchbackController`.
- `BreakwaterCourse` is a deliberate **fork** of `SwitchbackCourse` (see *The walk*).

### The gate ring was PROMOTED, not forked

`SwitchbackGateRing` became **`RaceGateRing`** and moved to
`_Scripts/Controller/Arcade/Racing/`. `git mv` on the `.cs` **and** its `.cs.meta`, so the guid is
unchanged at `5c7998f7c6ab3b62b2368c80a15e7413` — a rename must never move a guid, and
`author_switchback_assets.py` already anticipated the folder meta and its guid, so the committed
folder meta and the generator agree.

**The signature is primitives, and that IS the promotion:**

```csharp
public void Build(int index, Vector3 position, Vector3 axis, float radius,
                  ThemeManagerDataContainerSO theme, float bloomSeconds);
```

It used to take `in SwitchbackGate`, so a second race could not raise a ring without first
constructing the first mode's course record. **Hoisting a shared gate struct was considered and
rejected**: a Switchback gate carries one radius for the whole course while a Breakwater station
carries a per-station port radius plus the dish, plug and collar hanging off it — so a common
struct would be a third type existing only to be passed, which every future race would have to
convert into. Forking the file was the other alternative and loses for the reason the class now
states: the drawn ring **is** the trigger radius (the switch law), and a copy is a second place for
that to be broken with nothing comparing the two.

`SwitchThreadScoring` stayed where it is — it is already a shared-safe static.

---

## The walk

`BreakwaterCourse.Generate(seed, settings)` is a constructive backtracking walk. Two properties
hold **by construction**, and both are asserted over 400 seeds × 4 intensities in
`BreakwaterCourseTests`:

| property | how it is guaranteed |
|---|---|
| **Turn cap** — no corner sharper than `MaxTurnDegrees` | The heading only advances when a station is PLACED, and every proposal — including the one that steers away from the shell wall — passes through `ClampTurn`. When there is no legal escape the walk **backtracks**. |
| **Presentation cap** — no plug ever stands edge-on to the line you arrive on | A station faces the **flow bisector** of its corner, already half the turn off each leg. The jitter is spent from what is LEFT of the cap: `presentation ≤ halfTurn + jitter ≤ MaxPresentDegrees`. |

The tempting shortcut for the first is to let the heading rotate between failed attempts. It is
wrong: two 60° rotations compose into a 120° hairpin between two **placed** stations.

The presentation cap matters more here than it does in a bare gate race, and for a reason that is
about the *arena* rather than about flying: a plug standing edge-on is not a hard station, it is
one whose **eye cannot be threaded** and whose **dish shields its own plug from the blast**. Two of
the three verbs would be gone.

Three details of the stream are part of the contract rather than implementation:

- **The step is drawn BEFORE the deflection**, because the model draws it in that order. Swapping
  two individually-correct draws still ships a different course for every seed.
- **The cone angle is `max × sqrt(u)`, never `max × u`.** A cone's area grows with the angle, so a
  linear draw crowds every deflection near zero and the course comes out nearly straight. Same
  shape as the fauna-band fix in `Docs/ECOSYSTEM.md` — a uniform draw in a radial coordinate is
  not a uniform dispersal.
- **`Deflect` has no early-out for a zero angle**, and the one caller that can pass a small one
  guards it at the call site at the same `0.01` threshold the model uses. An early-out would make
  the number of draws a function of the *argument*, so one degenerate corner would shift every
  subsequent draw.

### `MinSeparation` is DERIVED, and it must stay below the minimum leg

```csharp
public static float MinSeparationFor(float portRadius, float minStep) =>
    Mathf.Min(0.9f * minStep, 4f * portRadius);
```

Four port radii is *"the two mouths are clearly separate places"* — closer and a pilot cannot tell
which ring is theirs, and the ordered-station rule stops reading as a course. But the value can
never reach the minimum leg, because **`TooClose` tests a candidate against every placed station
INCLUDING its immediate predecessor**. A separation above the shortest leg rejects most of the
step range before the walk has considered any geometry, and the walk starves.

The spec authored **420** against a **260** minimum leg. That is not a tuning miss, it is a
generator that mostly cannot generate. Derived from the two numbers it is genuinely a function of,
it lands well clear:

```
Derived minimum separation (must stay BELOW the minimum leg)
  I1: sep   270.0  min leg   300.0   OK
  I2: sep   240.0  min leg   300.0   OK
  I3: sep   200.0  min leg   290.0   OK
  I4: sep   168.0  min leg   275.0   OK
```

`BreakwaterCourseTests.TheSeparationFloorStaysBelowTheShortestLeg` asserts the ordering rather than
the values.

### The ladder is ONE DIAL, and intensity 1 is the anchor

Intensity 1 plays well, so it is **pinned**, and every other level is derived from it. The
hardening runs through a single number — the **turn cap** — because that one dial moves both halves
of what makes a course hard. Decompose a leg of length `L` turning `θ` off the previous heading:

```
along-track = L · cos θ            across-track = L · sin θ
```

Raising `θ` alone **decreases the first and increases the second**, which is exactly the shape that
stops a course being a series of gentle sweeps: the next door is barely ahead of you and well off
to the side, so you cannot fly it flat — you have to roll and strafe onto it.

| | port | legs | turn cap | along-track @cap | across-track @cap |
|---|---|---|---|---|---|
| **I1** (pinned) | 72 | 300–460 | **45°** | 269 | 269 |
| I2 | 60 | 300–433 | **55°** | 210 | 300 |
| I3 | 50 | 300–407 | **65°** | 149 | 320 |
| I4 | 42 | 300–380 | **75°** | 88 | 328 |

Along-track collapses **3.1×** (269 → 88) while across-track opens **1.2×** (269 → 328). The
maximum leg comes down with it so the hardest course is also the **densest** — more time in
corners, less in straights — but the *minimum* leg is deliberately **pinned at 300 on every
level**, and that is what pays for the whole hardening.

**Flyability stops being measured and becomes guaranteed.** The Dubins condition is
`leg > 2R·sin(turn)`, and `sin` caps at 1 — so **`2R` = 260.2 is the hard ceiling of that
requirement at *any* turn angle whatsoever**. A minimum leg of 300 clears it outright, which means
the turn cap can be raised as far as the presentation cap allows without ever re-checking whether a
corner is flyable. The previous ladder shortened legs *as* it tightened corners and had to measure
its way to safety; this one is safe by construction, and the `Dubins violations` row below can only
ever read 0. `EveryMinimumLegClearsTwiceTheTurningRadius` asserts it per intensity, so a future
retune that drops a minimum leg under 260.2 fails rather than quietly handing the turn cap back its
teeth.

**What the first cut got wrong, kept because the trap is general:** intensity 1 originally ran the
LONGEST legs with the TIGHTEST turn cap — the gentlest corners at the easiest level — and inside a
660-unit-thick shell a long leg with little turn available walks into the wall and cannot come
back. **21% of seeds failed to generate.** Long legs and tight corners are the same constraint
pulling opposite ways, which is why the shipped ladder never trades them off: it moves one dial and
holds the other still. Every row below is swept, and the sweep is the authority:

```
  I1  (port 72, legs 300-460, turn cap 45, present cap 50)
    generation failures      : 0 / 400
    worst corner             : 45.0 deg (cap 45)
    worst presentation       : 44.3 deg (cap 50)
    closest two stations     : 270.1 u (derived floor 270.0)
    shortest leg             : 302.2 u
    Dubins violations        : 0 (leg <= 2R.sin(turn) at R=130.1)
    MAX stations inside LOD  : 2 (radius 200 u)
    air at nearest spawn pad : 72.0 u (rejection floor 49.3)
    worst JOIN corner        : 66.0 deg (cap-exempt; Dubins needs 237.8 u, shortest leg is 302.2)
    start gate presentation  : 48.1 deg, spread across pads 0.0000
    start gate pad distances : spread 0.0000 u
    clear eye radius         : 18.000 u (1.46 x hull)

  I2  (port 60, legs 300-433, turn cap 55, present cap 54)
    generation failures      : 0 / 400
    worst corner             : 55.0 deg (cap 55)
    worst presentation       : 53.6 deg (cap 54)
    closest two stations     : 243.0 u (derived floor 240.0)
    shortest leg             : 300.0 u
    Dubins violations        : 0 (leg <= 2R.sin(turn) at R=130.1)
    MAX stations inside LOD  : 2 (radius 200 u)
    air at nearest spawn pad : 56.8 u (rejection floor 49.3)
    worst JOIN corner        : 64.3 deg (cap-exempt; Dubins needs 234.5 u, shortest leg is 300.0)
    start gate presentation  : 48.7 deg, spread across pads 0.0000
    start gate pad distances : spread 0.0000 u
    clear eye radius         : 18.000 u (1.46 x hull)

  I3  (port 50, legs 300-407, turn cap 65, present cap 58)
    generation failures      : 0 / 400
    worst corner             : 65.0 deg (cap 65)
    worst presentation       : 57.9 deg (cap 58)
    closest two stations     : 229.8 u (derived floor 200.0)
    shortest leg             : 300.1 u
    Dubins violations        : 0 (leg <= 2R.sin(turn) at R=130.1)
    MAX stations inside LOD  : 2 (radius 200 u)
    air at nearest spawn pad : 51.5 u (rejection floor 49.3)
    worst JOIN corner        : 66.2 deg (cap-exempt; Dubins needs 238.0 u, shortest leg is 300.1)
    start gate presentation  : 48.7 deg, spread across pads 0.0000
    start gate pad distances : spread 0.0000 u
    clear eye radius         : 18.000 u (1.46 x hull)

  I4  (port 42, legs 300-380, turn cap 75, present cap 62)
    generation failures      : 0 / 400
    worst corner             : 74.9 deg (cap 75)
    worst presentation       : 61.8 deg (cap 62)
    closest two stations     : 263.4 u (derived floor 168.0)
    shortest leg             : 300.0 u
    Dubins violations        : 0 (leg <= 2R.sin(turn) at R=130.1)
    MAX stations inside LOD  : 2 (radius 200 u)
    air at nearest spawn pad : 51.3 u (rejection floor 49.3)
    worst JOIN corner        : 66.9 deg (cap-exempt; Dubins needs 239.4 u, shortest leg is 300.0)
    start gate presentation  : 48.8 deg, spread across pads 0.0000
    start gate pad distances : spread 0.0000 u
    clear eye radius         : 18.000 u (1.46 x hull)
```

### Flyability is a Dubins condition, checked at the transient ceiling

A vessel at speed `v` with turn rate `ω` cannot fly tighter than `R = v/ω`, so a corner is
holdable only when the leg exceeds the chord that circle needs: `leg > 2R·sin(turn)`
(`AI_ORBIT_BREAK.md`). It is checked at the state a racer is **least able to correct in** — full
throttle, boosting, at the top of the overcharge band:

```
The Sparrow's turning circle (R = v / omega)
state                                      v (u/s)     omega     R (u)
mouse cruise (XDiff 0.5, no boost)            22.5     82.25      15.7
mouse boost (XDiff 0.5)                       72.5     87.25      47.6
pad cruise (XDiff 0.75, no boost)             28.8     82.88      19.9
pad boost, Time rest (XDiff 0.75)            103.8     90.38      65.8
pad boost, Time 10 (comeback)                145.9     94.59      88.4
transient ceiling (XDiff 1, Time 15)         235.0    103.50     130.1
```

`v = XDiff × DefaultThrottleScaler × boost × Mult(Time) + MinimumSpeed`;
`ω = RotationThrottleScaler × v + PitchScaler`. **Every one of those is read off `Sparrow.prefab`,
not off `VesselTransformer`** — the class's field initializers are `DefaultThrottleScaler 50`,
`PitchScaler 130`, `RotationThrottleScaler 0` and the prefab overrides all three
(`25 / 80 / 0.1`, with `DefaultMinimumSpeed 10` and `boostMultiplier 5`). Re-deriving from the
class defaults yields a different ship and a silently wrong clearance — the general trap CLAUDE.md
records: *a number read off a field initializer is not the number the game runs on.*
`BreakwaterCourseTests` rebuilds `R` from those five constants rather than pasting `130.1`, so a
vessel retune surfaces as a course that no longer clears instead of a stale constant that still
passes.

**The mouse rows are in the table for a reason and are not used by the proof.** The one-thumb
mouse scheme pins `XDiff` at the neutral 0.5 (`ONE_THUMB_MOUSE_CONTROLS.md`), so a desktop Sparrow
pilot flies the top two rows and a pad pilot the next two. The proof uses the ceiling, which is the
strictest.

### Reseed before shortening

The residual failure rate at `AttemptsPerStation = 32` is about **0.1% per seed**. Switchback's
back-off is to **halve** its gate count, and at that mode's rate it is a defensible trade. Here it
is not: halving answers a one-in-a-thousand roll by shipping **that one match** a race half the
length of every other — a difference the players in it can see and cannot explain.

`BreakwaterController.GenerateAndBroadcastCourse` therefore runs two phases:

1. **Reseed, keeping the full count** — `BreakwaterCourse.ReseedAttempts = 3`, which takes the
   failure rate to roughly 1e-9. Each retry logs a warning naming the shell, the step range and the
   separation.
2. **Only once every reseed has failed: shorten**, halving toward a floor of 2, then log a warning
   that says the match is shorter than the mode is authored for and names the two knobs to change.
   Generation must never return empty — that would leave the match with no rings, no arena, no
   scoring and no turn end.

At two stations it gives up and logs an **error** with every number that has to move. A shell
genuinely too tight for the requested count is a **configuration** fault, and both knobs that
cause it are authorable in the shipped editor (the overrides window's station target; the
controller's shell fields), so it has to degrade rather than hang.

**The reseed chain is DERIVED, not re-rolled** — `DeriveNextSeed(seed) = seed * 1664525 +
1013904223`. A pinned `courseSeed` must reproduce the same course every time *including the roll
that failed*, or a reported course cannot be reproduced.

`Generate` deliberately does **not** reseed itself: a generator that silently retried would make
its own failure rate unobservable, and the caller is the only party that knows which seed it may
legitimately move to.

---

## The station, in full

Everything below is emitted by `BreakwaterStationBuilder.Build(in station, emit)` — **closed form,
zero random draws**. That buys two things nothing else does:

1. `Tools/Build/breakwater_arena.py` **mirrors** the file exactly, so the cell's `PhaseThresholds`,
   the collider budget and the prism-clamp proof are statements about the arena that actually
   ships rather than estimates of it. The moment a draw appears here, every one of those numbers
   becomes a guess.
2. A station is rebuilt from a **broadcast pose** on every peer, so it is identical on every
   machine without replicating a prism, a seed, or a stream position.

The one source of variety, the dish's plate jitter, is a **hash of `(ring, plate)`** —
deliberately carrying **no station index**, so all fifteen jitter alike and the model (which has
no notion of a station index) can reproduce the emitted scales exactly.

### The plug — the only part that bites

Three **rakes** at 0°, 60° and 120° within the port plane, each a family of parallel lines at
perpendicular offsets `±(k + 0.5) × 12` while `(k + 0.5) × 12 < port − 3`.

**The half-pitch offset is load-bearing.** At `k = 0` the nearest line stands 6 units off centre,
so **no line of any rake passes through the centre** and the thread exists at every rake bearing by
construction rather than by a special case carved for it. Three rakes at 60° is the coarsest weave
with no straight-line gap wider than the pitch at any bearing — two rakes leave a lattice of
diamond holes a pilot can cheat through off-axis.

Each line is clipped to the annulus `[18, port]`. A line whose **bar body** comes closer to centre
than the eye radius is **split into two runs**, one either side of the hole — that split is what
actually cuts the eye out of the weave. A run is then divided into **equal** bars no longer than 62
units, `(3, 3, L)`, `PrismKind.Danger`. Equal rather than "as many full-length bars as fit plus a
remainder", because a stub at the rim costs the same collider as a full bar and reads as damage
rather than as structure.

#### The clip is against the bar's NEAR EDGE, not its centreline

**This shipped wrong, and the reason it is worth reading is that every constant involved was
individually correct.** A bar is `BarCross` = 3 wide, so a line whose *centre* stands exactly 18
units off the axis still puts 1.5 units of prism inside the hole. The first cut tested the
centreline — `if (d < eye)` — and `(1 + 0.5) × 12 = 18.0` is `EyeRadius` **exactly**, so for the
`k = 1` line that test was a clean, deliberate-looking **false**: the line was emitted as one
unsplit chord straight across the plug, and six bar bodies straddled **16.5 … 19.5** at every
station, at every intensity. The keystone collar's inner faces sit at exactly 18, so **the station
advertised a mouth 1.5 units wider than it had** — 16.5 clear, `1.34 ×` the hull rather than the
documented `1.46 ×`.

The fix is one line: clip against `d − BarCross / 2` and take the eye's half-chord *there*, which
puts the nearest **corner** of the nearest bar at exactly `EyeRadius`. It costs intensity 1 six
extra bars (78 → 84, the `k = 1` line now splitting into two runs that each need two) and every
intensity ~92 units of bar length, which is why the `PhaseThresholds` moved with it.

Two things generalise:

- **An exact tangency in a clipping test is a warning, not a reassurance.** The old bullet here
  read the `18 = 18` coincidence as a *virtue* ("a clean false rather than a coin-flip at a
  tolerance") — the two numbers being exactly representable made the wrong branch perfectly
  deterministic instead of intermittently wrong, which is worse, because it looks decided.
- **A named constant is an INPUT to the arithmetic, not a claim about what the geometry emits.**
  `EyeRadius = 18` was true of the clipping input and false of every prism laid. `ThePlugLeavesThe
  WholeEyeClear` therefore measures the **emitted boxes** — the largest disc in the port plane no
  prism shadow intrudes on — and `TheCollarsInnerFaceLandsOnTheEye` measures the collar the same
  way, so the rim a pilot lines up on and the hole they fly through are proved to be the same
  circle. Both were run against a negative control that restores the centreline clip and reports
  16.5 at all four intensities.

One more geometric fact worth carrying:

- **The longest bar the ladder produces is 58.79 u** (intensity 4, the 30-unit offset line),
  against the 62 cut and a `PrismScaleAnimator` clamp at 100. It is far inside the clamp, but the
  clamp is the thing that would swallow an overshoot **in silence** (the environment lay path
  writes `TargetScale` directly and never calls `AdmitTargetScale`), which is why the model checks
  every emitted axis rather than trusting the authored range.

### The keystone collar — the aim point

Twelve 8-unit cubes on a ring at `CollarRadius`, 30° apart, in the port plane, `PrismKind.Plain`.

`CollarRadius` is **derived, never authored**: `EyeRadius + CollarCube × 0.5 = 22`, so the blocks'
inner faces **are** the eye's rim and narrowing the eye can never leave the collar floating off it.
Each block is posed with local Z along the flow and local Y radial, so its half-extent falls on the
radius and the rim a pilot threads is a real flat surface rather than an implied circle.

**It is sized to be unmissable.** `12 × 8 / (2π × 22) = 69%` of the rim's circumference, so a
rocket aimed at the eye and a little off axis still clips a block and detonates where the pilot
meant it to rather than sailing through and arming on nothing.

**Deliberately Plain, not Danger.** The collar is what you brush when you very nearly thread the
eye, and clipping the rim of the hole you were aiming at should be an ordinary prism hit — while a
wild miss lands in the weave, which is Danger and hurts. A dangerous collar would punish the good
attempt and the bad one identically and delete the difference between them.

### The dish — the landmark and the bank

Concentric rings of `(7, 7, 1.5)` plates on a cone shell whose rim is pinned to the port and whose
mouth opens **back toward the incoming leg**, `PrismKind.Plain`.

The half-angle is 22° **from the axis**, so the shell makes 22° with the flow. On a cone, radius
grows linearly with distance from the apex, so a ring of radius `r` sits at
`offset = −Axis × (r − PortRadius) / tan(22°)` = `2.4751 × (r − PortRadius)`. **The sign inverts
silently** — a dish built along `+Axis` is a shell the pilot never sees, hiding behind the plug and
adding nothing but prisms and colliders — which is why the derivation is written out in the source.

Plates lie **tangent** to the shell: with generator `g = −cos(a)·Axis + sin(a)·radial`, the outward
normal is `n = sin(a)·Axis + cos(a)·radial` (`n·g` is identically zero), and posing local Z along
`n` puts the 1.5-unit thickness across the shell. A ring's plate count is
`round(2πr / 14)` — the same pitch as the radial spacing, so plates tile roughly square at every
radius and both the ring count and the plate count fall out of the port radius with nothing
authored. `Mathf.RoundToInt` is round-half-to-**even**, matching the model's `round()`; a
hand-rolled `(int)(x + 0.5f)` would break the mirror at a half.

Measured, per intensity:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| nominal rim (`1.75 × port`) | 126.0 | 105.0 | 87.5 | 73.5 |
| ring radii | 72, 86, 100, 114 | 60, 74, 88, 102 | 50, 64, 78 | 42, 56, 70 |
| plates per ring | 32, 39, 45, 51 | 27, 33, 39, 46 | 22, 29, 35 | 19, 25, 31 |
| axial depth of the horn | 104.0 | 104.0 | 69.3 | 69.3 |

Note the **outermost ring lands short of the nominal rim** (114 against 126 at intensity 1),
because rings step by 14 from the port rather than being distributed to the rim.
`BreakwaterStationBuilder.DishRadius` returns the nominal 126, and the shoal clearance reads it —
so the rubble keeps clear of a rim slightly wider than the one that was built. Conservative in the
safe direction, and one expression with two readers so the horn and the rubble cannot overlap by
drift.

**The jitter is one factor for all three axes**, so a plate's aspect — its identity as a thin slab
— is exact at every draw, where per-axis jitter would turn some plates into splinters and others
into blocks. `k` is mean-zero per axis but `k³` is **not**, so the dish comes out about 3.2%
heavier than nominal; the model sums the real draws rather than pricing it at nominal, which is why
the thresholds below describe this arena exactly.

**What the three parts teach, in the order a pilot meets them.** The dish is a horn you cannot miss
and cannot be hurt by — it says *the station is here, and it is pointed at you* from a long way
out, and it is also the ammunition. The collar is the aim point. The plug is the only part that
bites. **The tight thread is forgiving and the sloppy one is not.**

### The shoals — the rubble between

Six clusters of seven 4-unit cubes strung along each of the thirteen legs, `PrismKind.Plain`,
40–110 units off the flight line.

**It is the mode's ammunition, not scenery.** A pilot who missed — who threaded when they should
have fired, or sawed and fell behind — can mine it without leaving the course. The 40–110 band is
the whole trade: near enough to be worth a detour, far enough that nobody meets it by accident.
Every cluster is Plain deliberately: this mode's danger is concentrated entirely in the weave, a
place the pilot chose to fly into having looked at it.

**The placement window is DERIVED, not sampled.** A cluster at `(s along, w lateral)` clears both
endpoint stations iff `s² + w² ≥ clear²` and `(L−s)² + w² ≥ clear²`, so a lateral offset admits any
`s` at all only when `w² ≥ clear² − (L/2)²`. Drawing `w` from that floor upward and `s` from the
window it opens makes the endpoint clearances hold **by construction**, leaving the retry loop only
non-adjacent stations and other legs to answer. Measured over the real walk: a naive uniform draw
over `(s ∈ [0,L], w ∈ [40,110])` clears both endpoints only **13.5%** of the time at intensity 1,
so twelve proposals would have sent about one cluster in six to the fallback. With the window
derived, **37,440 clusters over 120 seeds × 4 intensities took the fallback zero times**, and the
tightest window the derivation ever opened was **9.46 units** of lateral freedom, on intensity 1's
shortest legs — exactly where the arithmetic said it would bind.

**The count is EXACT and must stay so.** The model prices the shoals at `legs × 6 × 7 = 546` prisms
with **no rejection term**, so a cluster that cannot find a legal spot is **placed at its best
candidate** rather than dropped. Degrading a clearance by a few units is invisible in play;
dropping clusters would make the authored thresholds describe an arena heavier than the one that
shipped — the model measuring a world that does not exist, which is the one failure this whole
family of closed-form builders exists to prevent.

Clusters are spread in **azimuth** around the leg, one per sixth, so six can share the tight `s`
window that intensity 1's shortest leg offers and still stand a full chord apart. Nothing
interpenetrates at any rotation, and it is arithmetic rather than luck: a rotated 4-cube occupies a
sphere of radius 3.46 so two need 6.93 units between centres; centre-to-shell is at least 8;
shell-to-shell the six phyllotaxis directions have a minimum unit chord of 1.2599, i.e. 10.08 units
at the radius floor, and the worst mixed pair (one at 8, one at 11.2, 78.5° apart) still stands
12.4 apart. Both margins survive the whole 1.0–1.4 factor range.

### One trail per structure

The base class lays an environment as **one** trail. That is wrong here for the reason the
Switchyard already records: `PrismscapeTopology.DimensionOf` reads the authored `Trail.Dimension`,
so a dish (a cone **shell**) and a plug (a woven **solid**) sharing one trail would route a rider
onto the wrong ride.

`SpawnableBreakwater` therefore lays **120 segments** — 15 stations × 2 (`DISH` =
`PrismscapeDimension.Surface`, `THROAT` = `Volume`) plus **15** legs × 6 clusters (`Volume`).
Fifteen, not fourteen: a circuit has a leg leaving *every* station, the closing one included, and
walking consecutive pairs to `Count - 1` skips it — see `BreakwaterCourseSettings.NextStation`.

**Nobody rides a Breakwater station today** — it is a Sparrow-only mode — which is exactly why the
declaration has to be right now: an honest dimension costs one enum value at lay time, and a
dishonest one is a bug that waits for the first vessel that can attach.

**The parts are sorted by GEOMETRY, not by emit order.** The builder happens to emit outside-in,
but its own documentation says nothing depends on that, and slicing the lay list on an assumed
order is precisely the dependency that survives review and breaks the day somebody reorders three
lines for a nicer reveal. So the builder's output is buffered and walked twice. **The
discriminator has a chasm rather than a cliff**: Danger is the weave; among the Plain prisms the
collar sits at 22 from the axis and the dish's innermost ring at the port radius (42 at the
tightest intensity, 72 at the widest), so the midpoint separates them by **10 units either way at
the worst case and 25 at the best** — the shape the Switchyard's `BurrMatchRadius` settled on after
a quantize-to-whole-units key was rejected for having a boundary a float could land on.

Segments are laid **sequentially inside one `BeginArenaBuild` bracket**, never concurrently: the
lay budget is a shared per-frame counter, so 106 concurrent lays would still place only a frame's
worth of prisms while each first requested its own 256-prism async clone batch — the whole arena
cloned in one frame, the exact spike the budget exists to prevent. The bracket also holds the
arena-ready gate closed across the gaps *between* segments, where an absence-of-activity check
would misread the pause as "arena done" and drop the connecting screen onto a half-built course.

---

## Intensity is the DOOR and the COURSE, not the arena's size

Station **count** is constant at every level, because it is the end-game target and is authored in
one place — so a match is the same length at all four and they are comparable. Same reasoning as
Rampage, where the forest is identical at all four and only the pressure changes.

**`R_port` is THE axis, and it moves two things with one number.** A smaller port means a smaller
dish (×1.75), so **the hardest course is also the poorest ammo bank** — fewer prisms in reach means
fewer of the 50 that buy the next rocket. It is a measured ladder rather than a formula because
both of its ends are pinned by shipped facts (see *The correction*) and the interesting property is
where it crosses between them.

```
Per-station geometry
                                I1          I2          I3          I4
R_port                          72          60          50          42
plug lines/rake                  7           6           5           4
plug bars                       78          54          42          30
longest bar                     58          57          54          59
dish rings                       4           4           3           3
dish plates                    167         145          86          75
collar blocks                   12          12          12          12
prisms/station                 257         211         140         117
volume/station              53,984      40,927      28,598      21,928

Arena totals (15 stations + shoals)
arena prisms                 4,575       3,795       2,730       2,385
arena volume               837,677     641,833     456,896     356,842
x nominal (16/prism)          11.9        10.9        10.9         9.8

Shoals: 546 prisms / 34,944 volume (constant at every intensity)
```

> **Reading that table:** the row printed as **`plug lines/rake`** is `runs ÷ 2`, not the number of
> lines. The real line count per rake is **12 / 10 / 8 / 6** (6 / 5 / 4 / 3 offsets, each ±). It is
> a **display-only** label bug in `station_totals` — `bars`, `volume` and the clamp proof all come
> from `plug_runs`/`plug_bars` directly and are correct. Fix the label, not the geometry.

And the course rows, which run the **opposite** way (see *The ladder is measured*):

| intensity | port | legs | max corner | axis jitter | presentation cap | separation floor |
|---|---|---|---|---|---|---|
| 1 | 72 | 300–460 | 45° | 22° | 50° | 270.0 |
| 2 | 60 | 300–450 | 50° | 28° | 54° | 240.0 |
| 3 | 50 | 290–440 | 55° | 34° | 58° | 200.0 |
| 4 | 42 | 275–420 | 60° | 40° | 62° | 168.0 |

**The eye is a CONSTANT across the ladder**, 18 units, `1.46 ×` the hull radius. That is the whole
of the third verb, and holding it fixed while the port narrows is what makes the ladder say
something: at intensity 1 threading is the miser's option beside a wide easy door, and at intensity
4 it is very nearly the only gap left. Nothing about the eye had to change to say that.

```
Bounds that make the intensity axis honest
  tightest port 42 vs hull radius 12.32 -> 3.41x clearance
  eye 18 vs hull radius 12.32 -> 1.46x clearance
```

---

## The start is provably fair

Pilots spawn on an **equatorial ring** around the cell (`CellSpawnFormation.EquatorialRing`, which
the scene must author), and the **start gate sits on that ring's POLE with its axis ALONG the pole**.
So every pad is the same distance from it *and* sees it at the same angle — measured spread
**0.0000 on both**. Under a Symmetric (tetrahedral) formation no such point exists.

That matters more here than in a plain gate race: whoever arrives first also gets the **undamaged
plug** and the choice of how to open it. `BreakwaterCourseTests.TheStartGateSitsOnTheSpawnFormationPole`
asserts the axis and the shell, and `EverySpawnPadSeesTheStartGateIdentically` asserts both halves
of the fairness claim. Changing the scene's spawn formation to Symmetric breaks the argument.

**The gate's DISTANCE along that axis is solved, never authored** — it is wherever the axis is
exactly one chord from the circuit's entry station, so there is no number here to tune or to drift.
An authored `firstStationDistance` used to live on the controller and is retired; *a config that
cannot affect anything is worse than absent.* Why the gate sits **off** the circuit at all rather
than being its first station is a structural result about circles inside a shell, and is recorded
under "A start gate and a circuit, flown twice".

### …and no station may swallow a spawn pad

**The spawn ring is INSIDE the course shell** — pads at 480 against a 420–1080 walk — and until it
was measured, nothing in the generator knew the ring existed. The walk's only placement tests were
the shell and `TooClose` (station-to-station), so a station could and did land on a pad: measured
over 400 seeds × 4 intensities × 2/3/4 seats, **7 of 14,400 pad-cases put a pilot inside a
station's structure** and 23 more put one within a hull radius of it. That pilot starts the match
embedded in Danger prisms, with no counterplay and nothing on screen to explain it.

`BreakwaterCourse.ReachesSpawnPad` rejects any candidate whose station would reach a pad, using
`StationReach(port)` — the bounding sphere of the station's geometry, whose farthest point is the
dish rim (`DishRatio × port` out in the port plane and `(DishRatio − 1) × port / tan 22°` behind
it) — plus `DefaultSpawnPadClearance`, **four hull radii**. Three choices in that sentence:

- **A bounding SPHERE, deliberately coarse.** The number only ever *rejects*, so erring outward
  costs the walk a little freedom and can never let prism near a pad. Measured, it costs the walk
  **nothing at all**: 0 generation failures over 800 seeds × 4 intensities at every clearance from
  0 to 60.
- **Four hull radii, not one.** A pilot spawns *facing* the cell and needs room to see the wall and
  turn, not merely to not be inside it. The shipped sweep leaves **52.4 u** of air at the tightest
  pad, against the 49.28 floor.
- **The pad set is the UNION over 2, 3 and 4 seats** — `{0°, 90°, 120°, 180°, 240°, 270°}` — rather
  than the live roster. The course is generated once and broadcast once, so keying it on the seat
  count would let a seat added between those two moments invalidate the geometry every peer already
  holds. *A generator that reads a number the network can still change has to be re-run when it
  does; taking the union means it never has to be.*

The shoals get the same treatment from the other side: `SpawnableBreakwater.SetSpawnPads` feeds
`ClusterMargin`, which is a **scoring** term with a best-margin fallback rather than a rejection —
so it can never drop a cluster, and the arena's prism count stays exactly the number the
`PhaseThresholds` were measured against.

---

## The course travels; the seed does not

The server generates the course and **broadcasts the geometry**: six floats per station interleaved
into one `float[]`, plus **one shared port radius** (the ladder gives every station in a course the
same port, so sending it per station would be fifteen copies of one number). Fifteen stations is
**340 bytes**, and it is the entire wire cost of the arena — everything else is closed form from
those poses.

A shared seed would have worked, and `BreakwaterCourse` is deterministic on purpose — but it would
rest on `Mathf.Sin`/`Acos` agreeing to the last bit across Mono and IL2CPP, and a single flipped
branch inside the walk yields a **completely different** course rather than a slightly different
one. The seed is kept only so a reported course can be reproduced.

A late-joining client pulls it with `RequestCourse_ServerRpc` → a targeted `SyncCourse_ClientRpc`,
mirroring `MultiplayerMiniGameControllerBase`'s config pull. `ApplyCourse` is idempotent, because
the server applies its own copy before broadcasting and a client that both received the broadcast
and answered its own pull must not build the course twice.

**Ordering is load-bearing on both paths.** `base.OnNetworkSpawn()` has already sent the config
sync (server) or asked for it (client), and every message here travels on the **same
NetworkObject**, so NGO delivers the intensity before the course on either route. `BuildArena`
spawns at the local `Intensity`, and a client that built its arena before the config landed would
build a **different arena than the host for the whole match** — the sticky-intensity race the base
class's own pull comment records. Moving the generate/request call above `base.OnNetworkSpawn`
would reintroduce it.

**The course is generated about the ORIGIN and then offset to the cell**, once, before the
broadcast — so peers receive world positions, the generator stays a pure function of its shell, and
moving or nesting the Cell keeps the course on the arena. The fairness argument depends on that
offset.

**The connecting panel holds through the whole thing.** The arena is rolled per match, so at scene
start there is nothing for the arena-ready gate to observe and it would release the moment it
opened — releasing pilots into an empty cell, ahead of several thousand prisms materialising around
them. `OnNetworkSpawn` opens a `PrismTrailBuilder.BeginArenaBuild()` bracket that `ApplyCourse`
closes after the rings stand and the lay is in flight; despawn and a failed generation close it
too, so it can never wedge on a build that will not happen. **The hand-off has no gap**: `Spawn`
generates synchronously and reaches `PrismTrailBuilder.LayBudgetedAsync`, whose active-lay counter
increments **before its first await**, so the gate is already held by the lay itself in the same
frame.

---

## Detection: owner-detects / server-records

```
BreakwaterController.Update()                       [EVERY peer]
  └─ for each player where IPlayer.IsNetworkOwner   ← host: its human + ALL AI
     │                                                client: its own human only
     ├─ sample position, reject an implausible single-frame step
     ├─ test the segment against THAT PILOT'S next ring only
     └─ crossed?
         ├─ IsServer  → SwitchThreadScoring.Credit(stats, index)     [direct]
         └─ else      → Player.ReportSwitchThreaded_ServerRpc(index) [validated server-side]
```

**`IsNetworkOwner`, never `IsLocalUser`** — the host owns every AI Player, and the narrower test
would silently never advance one. That is the gate The Bends records for combat hits, reached from
the other direction.

**Segment crossing, never a trigger volume.** `RaceGateRing.CrossedMouth` is a plane test with a
lateral bound, the same math `ScarabSwitch` and `AstroLeagueGoal` use. It is **direction-agnostic**
— a station threaded backwards is still threaded, which is the honest reading for a race, since you
still had to fly there.

**The implausible-step reject is `maxPlausibleSpeed × Δt × 2 + 5`.** Doubled so one dropped frame
does not read as a teleport, offset so a stationary vessel's numerical jitter is never near the
bound. The authored 400 u/s is 1.7× the hull's measured transient ceiling of 235, so nothing a
pilot can *fly* is rejected. Above it a segment is not flight — a respawn or an eject sweeps a
straight line across the arena that would cross several stations' planes at once, and **because the
count IS the next index, crediting one of them would also skip the station in front of the pilot**.

**The optimistic counter** (`PilotRun.Optimistic`) exists because on a client the authoritative
count lags by a round trip, during which a boosted pilot can reach the next station. It reconciles
both ways: it adopts the server's value when that catches up or overtakes, and falls **back** to it
when a report goes unacknowledged for `reportResyncSeconds` (3 s), so a dropped or rejected report
cannot strand a pilot testing a station they will never be credited for.

**Flying with no course is silent by construction** — the pilot is simply never credited — so it
announces itself: one error, once, naming the missed broadcast and the unanswered pull.

---

## Which station is MINE — two halves, both per-viewer

Fourteen identical dishes scattered through a cell is a course you have to be told the ORDER of,
and the arena makes it *worse* rather than better: a dish is a big obvious landmark that says
nothing about whose turn it is, and every pilot's next station looks exactly like every other
pilot's.

1. **The objective arrow** (`BreakwaterObjectiveProvider`) — says which *direction* to fly when the
   station is off screen. One array lookup: the controller already indexes its rings by station
   number and the pilot's progress **is** that index.
2. **The ring goes LIME** (`RaceGateRing.SetIsNextForLocalPilot` → `ToySwitchSignal.Next`, the
   platform's free-pickup CTA colour) — says *this one* once several dishes are in frame.

Both read the pilot's live `SwitchesThreaded`, so they cannot point at different stations. Both are
**local only and nothing is replicated**: every peer builds its own copy of the course, so a
`RaceGateRing` already belongs to exactly one viewer. `LightLocalNextStation` is driven from the
pilot's live progress rather than from the crossing event, so it is correct after a rollback, after
a late course arrival, and for a client whose report is in flight.

**Repainting the next station in the pilot's DOMAIN colour was the obvious alternative and is wrong
three times over**: it spends the switch vocabulary's reserved domain colour on something that
hands nobody a domain; it makes two pilots flying side by side see different worlds; and worse here
than in Switchback, a Breakwater station is a 257-prism landmark rather than a bare ring, so a
domain-coloured one would read as **mass somebody owns** in an arena where every prism is
deliberately `Domains.Blue` and hostile to everyone.

---

## Everything is `Domains.Blue`

`StatsManager.IsFriendlyEnvironmentPrism` is **domain-only** (`SparrowMissileFuzeTests` pins it:
own domain friendly, another domain hostile, *"neutral mass is hostile to everyone — Blue is the
no-team sentinel"*). So a Blue arena is hostile to every pilot, and **every pilot's rounds pay
ammunition on every door**. Painting a station in a playable colour would mean one team's rounds
paid no ammo on it while everyone else's did.

**And worse: it would make the station unopenable by that domain.** The Sparrow's **Charge-5**
upgrade spares own-domain mass — an upgrade **the comeback system hands to whoever is LOSING**.
That is the trap `WILDLIFE_LIBERATION.md` records (a creature kill that borrowed the friendly-fire
flag switched itself off for the pilot sharing the swarm's colour, in the one mode scored on
killing creatures), and here it is closed **by construction**: no call site in `SpawnableBreakwater`
can pass a domain, because none of them takes one.

**No flora and no fauna, and that is a rule rather than a saving.** This cell has no nucleus, so
herbivores eat opposing-domain mass — and the whole arena is Blue. A food web would **graze the
doors open on its own**, which is imposed death of the mode's central object, arriving through a
system that is otherwise exactly right.

---

## Collider budget

**Zero always-on mesh colliders are authored anywhere.** Every prism is `Plain` or `Danger`, both
of which ride the LOD-cullable `BoxCollider` that `PrismColliderLodManager` reclaims outside its
`lodRadiusMeters = 200` radius. The fifteen switch rings carry **no collider at all**.

The rest is **measured off the real walk**, because it is a claim about how many stations fall
inside the LOD radius at once — a property of how the walk **folds**, not of the authored
separation:

```
Collider budget (MEASURED worst case, not asserted)
                                    I1          I2          I3          I4
stations in radius                   2           2           2           2
active prism colliders             568         464         322         276
against band                     1,500       1,500       1,500       1,500

  Zero ALWAYS-ON mesh colliders are authored: every prism is Plain or Danger, both
  LOD-cullable. The 15 switch rings carry no collider at all.
```

**The count is not monotonic in intensity**, and that is the point of measuring it: a lower
intensity has more prisms per station but a course that folds less tightly. A number derived from
the authored separation would have got this backwards.

---

## Phase thresholds

Volume is the spine; count is the rare frenzy/perf backstop. Deltas over the **measured** baseline,
with exits 10% under their enters so a trail-caused Frenzy always releases with the arena intact:

```
Phase thresholds (volume is the spine; count is the backstop)
  I1: baseline    837,677   RestlessEnter    957,677   FrenzyEnter  1,137,677
  I2: baseline    641,833   RestlessEnter    761,833   FrenzyEnter    941,833
  I3: baseline    456,896   RestlessEnter    576,896   FrenzyEnter    756,896
  I4: baseline    356,842   RestlessEnter    476,842   FrenzyEnter    656,842
```

Exits are `baseline + 108,000` and `baseline + 270,000`; counts ride `+700 / +500 / +3,600 /
+3,000`.

This arena is **9.8×–11.9× nominal volume per prism** (a 62-unit danger bar is 558 volume against
the nominal 16), which is exactly the case CLAUDE.md records for Rampage: *a cell whose prisms are
not nominal must author its volume ladder, never inherit the `count × 16` derivation.* Inheriting
it here would put the ladder an order of magnitude low and pin the cell at Frenzy from the first
frame.

**Confirm in the editor before trusting these**: FrogletTools ▸ Ecology ▸ **Measure Cell
Environment Baselines**. If the measurer disagrees with this table, the C# and the model have
drifted — fix both, do not paper over it in the asset.

---

## The model is a MIRROR, and it was proved against the C#

`Tools/Build/breakwater_arena.py` reproduces `BreakwaterCourse`'s xorshift32 walk and
`BreakwaterStationBuilder`'s `Hash01` **bit for bit**, in the same consumption order. That is not
tidiness — it is the only thing that makes the thresholds, the collider budget and the clamp proof
statements about the arena that **actually ships**.

It was **proved, not asserted**: the shipped C# was compiled against Unity-type stubs and diffed
against the model, giving **160 courses position-identical** (worst delta **0.002 u**, float32
rounding against the model's doubles) and every station's prism count and per-prism volume matching
to **0.0000%** on all four intensities — the last of which only became true once the model was
taught to mirror `Hash01` exactly rather than pricing the dish at its nominal volume.

The geometry is `float` on one side and `double` on the other, so a candidate sitting within float
epsilon of the shell wall or the separation floor could in principle be classified differently. The
sweep's tightest observed margin is **0.9 units against a 168-unit floor** — five orders of
magnitude clear.

`BreakwaterCourseTests` is the C# half of the same proof: **49 cases, 0 failures**, executed
offline, over the model's **own seed schedule** (`seed × 7919 + intensity`, not 1..400). Sweeping a
different 400 would be a different sample — at the ~0.1% residual failure rate a fresh set carries
~0.4 expected failures over 1600 courses, so a test that invented its own seeds would be flaky for
a reason that has nothing to do with the code. Both sets were checked and both are clean, but only
one is the set the model's claims are about, and the sweep's tightest margin lives in it.

---

## AI

`ArmRacers` installs a per-pilot `AIPilot.SetExternalTargetProvider` at `OnCountdownTimerEnded`
(server only). Each AI is pointed at its own next station as **two waypoints**:

- far out (beyond `aiCommitDistance`, 240) → a point **behind** the station on its own axis
  (`aiApproachLead`, 280), which lines the approach up with the port;
- inside it → a point **beyond** the station (`aiThroughDistance`, 200), which flies the pilot
  through.

`AIPilot` has no arrive-and-stop behaviour — it steers at its target forever and passes through on
arrival — so handing it the port's centre produces a pilot orbiting the mouth, the defect both
PeelTheCage and Dog Fight record. 240 is under the shortest leg the ladder produces (275), so a
pilot always spends the head of a leg lining up rather than arriving already committed; 280 is
2.15× the tightest turning circle at the transient ceiling, so the approach leg is flyable from any
bearing; 200 clears the widest dish rim the ladder builds (126).

Which side is "behind" is **latched** when the station changes, not recomputed — a pilot that
drifts just past the plane without threading would otherwise see the sides swap and swing away
(Dog Fight's break-off lesson).

### The AI opens its own doors with NO new AI code

An AI Sparrow's own prefab already authors FullAuto and SkyBurst on its `AIPilot.abilities` list,
and the platform's `ConfigureAIPilot` is all that is needed to start them. A round leaves along its
muzzle container's forward — the **nose** — and the two waypoints above hold that nose on the plug
for the entire run-in. So it is pointed at the station because it is *flying* to the station, and
whatever it fires goes down the axis into the weave. No aim solver, no "should I shoot this" rule.

**That rests on one authored field: `Sparrow.prefab`'s AIPilot has `drift: 0`.** Drift is the one
state in which `AIPilot` frees the nose from the steer direction
(`ResolveDriftLookDirection`) so a committed vessel can aim somewhere else — a Dolphin wants
exactly that, and this hull must not have it, or an AI would spend its run-in pointed at a mass
cluster off to one side and fire every round into empty space. **Turning drift on for the Sparrow
would silently take this mode's whole no-code AI gunnery with it.**

**And when it fires nothing, it flies straight down that axis — which is the EYE.** Both aim points
sit on the station's axis, and the axis passes through the eye by construction (the rakes are
offset half a pitch precisely so no line crosses the centre). At 18 units against a 12.32-unit hull
that is 1.46× clearance, so an AI that never opens a single door **still threads every station it
reaches**. *The pre-open eye is the designed FLOOR on AI competence* — the mode cannot produce an
AI that is stuck, only one that is slow.

### The crystal detour

Installing an external target provider **replaces** `AIPilot`'s own crystal seeking outright — the
trap `RAMPAGE.md` records — so without a detour an AI would fly past every crystal in the cell and
never level an element. In a mode whose comeback buff **is** element levels, an AI that cannot
level is one the comeback cannot reach.

The detour is bounded by a **distance budget, never a radius**: a crystal is taken only when going
through it costs less than `aiCrystalDetourSlack` (200 u) of extra flying between here and the next
station, so it can never pull a pilot off the course. It is **re-tested every frame** rather than
latched — unlike the approach side, where a latch is what stops an oscillation — because "on the
way" is a cheap monotone test that stops holding the instant the pilot is past the crystal, so the
detour cannot become an orbit. The registry scan is throttled to `aiCrystalScanSeconds` (0.5 s) and
filters to `Crystal.CrystalManager != null`, the filter `RampageObjectiveProvider` records:
`Crystal.Active` also holds every lifeform heart the food web drops. **This cell carries no food
web, so the filter buys nothing here today** — it is kept because the registry is global and a
heart dropped in another cell must never become a target.

Steering only: no ability, throttle or weapon is touched, and the provider is per-pilot, so nothing
leaks into another mode.

**Known cost, left alone deliberately:** `AIPilot.UseAbilityCoroutine` fires an ability on a plain
duration/cooldown timer with **no range test**, so an AI's SkyBurst goes off on schedule whether or
not a station is in front of it — expect a wasted rocket on a long leg. That is a property of the
platform's AI ability driver, not of this mode, and a range gate here would be a second, mode-local
ability driver.

---

## Scoring and the end condition

**A domain's progress is its LEAD RUNNER, not its sum.** Every pilot flies the same course, so
summing teammates would give a two-pilot domain twice the course, cross the target at half the
stations, and beat a one-pilot domain that had actually flown further. `SwitchbackScoringRuleSO`
already overrides `DomainValue` to `ScoringMetrics.BestByDomain`, and because that override is on
the ONE seam every domain reader goes through, the end condition, the "remaining" readout, the
placement order, the winner resolution and the HUD's own domain boxes all move together.

Its mirror is `RemainingForPlayer`, which the rule also already overrides: a **pilot's** own
readouts must not show the domain fold, or a trailing teammate's goal row would read the ace's
"12/14" while their objective arrow pointed at station 4.

The design consequence is deliberate: **a teammate never adds to your score.** Team play is
interference — and in a Sparrow mode the ammunition is on the course by construction.

### A start gate and a circuit, flown twice

The course is a **start gate** plus a **closed fourteen-station circuit**, flown twice — **29
crossings**. After the last station a pilot continues *forward* into the first one; the start gate
is threaded once and never again, so a lap adds `stations − 1` rather than `stations`.

**It replaced an out-and-back**, which re-flew the same stations reversed and play-tested exactly as
it reads: being sent back through the rings you came.

**The start gate is what makes a circuit fair, and it is not decoration.** Fairness here is "pilots
spawn on an `EquatorialRing`, the first gate sits on that ring's pole, so every pad is equidistant".
Make that first gate the first gate of a closed **loop** instead and the approach is *axial* while a
closed loop's tangent at an axial point is *perpendicular*. Measured over 400 seeds × 4 intensities,
presentation at that gate ran **12.8–90.0°** with up to **73.5° of spread across pads**: one pilot
gets a 14° face-on approach and another 90° edge-on to the same ring. That is worse than the
out-and-back it would replace.

It is **structural, not tuning**. Inside the 420…1080 shell no circle can cross the polar axis at
radius ≥ 420 with a near-axial tangent, because `c + R ≤ 1080`, `R² − c² ≥ 420²` and `c/R ≥ 0.866`
are jointly unsatisfiable. So the start gate sits *off* the circuit, on the axis, with its axis
**along the pole** — which makes every pad equidistant **and** face-on: measured spread **0.0000 on
both**, strictly fairer than the old rule, which equalised distance only.
`EverySpawnPadSeesTheStartGateIdentically` asserts both halves.

### The circuit is CONSTRUCTED, not searched

The walk could not be steered home — a closing walk failed **55–76% of seeds**, and that is the
vessel's own turning circle and the membrane deciding it between them. So the loop is not searched
for, it is built closed, and every constraint becomes an analytic bound.

**1. A zigzag ring hits an exact turn angle in closed form.** For `P_i = R(cos tᵢ, sin tᵢ) ± z·axis`
with N even (so the zigzag closes), consecutive legs alternate `sᵢ ± 2z·axis`, giving

```
cos(turn) = (|s|² cos φ − 4z²) / (|s|² + 4z²),        φ = 2π/N
```

and solving it for a target chord and turn yields **the mode's intensity dial directly**:

```
|s| = chord · sqrt((1 + cos T) / (1 + cos φ))     <- ALONG track
2z  = sqrt(chord² − |s|²)                        <- ACROSS track
```

Verified: every corner lands on `T` to 1e-6. Raising the turn cap *collapses* the along-track
component and *opens* the across-track one, which is exactly the rolled, strafing entry the ladder
is built to ask for.

**2. Wander is LOW-FREQUENCY** (harmonics k = 1, 2 in radius and out-of-plane). A smooth deformation
moves neighbouring stations *together*, so it changes the loop's outline a lot while barely moving
adjacent spacing. Per-station jitter does the opposite: it had to be shrunk to ~20% of nominal to
fit the chord band, which made every course look like every other one. With low-frequency wander the
radius spread *within* one course is 31–342 u and the loop radius across seeds spans 531–1006.

**3. The amplitude shrinks until the caps hold**, and at amplitude 0 the loop is a regular zigzag
ring — legal by construction. **So the shrink always terminates.** That is what replaces rejection
sampling, and it is why the sweep has no failure rate to report: 0 failures in 1,600 courses.

**4. The rotational degrees of freedom are SPENT, not randomised.** Two put the entry station where
a polar start gate is exactly one chord away; the third spins the loop so its tangent there already
points down the entry leg. Randomising them was the first cut and is why the entry leg was almost
never in the chord band — the start gate has to sit on the axis, so its distance from the loop is
not free. Two discrete choices (which station to enter on, and the entry offset factor) are
*searched*, and the score is **quantised to 0.1°** before comparison so float noise cannot flip
which branch wins — the offline model and the shipped C# would otherwise be able to disagree about
a whole course over 1e-4 of a degree, and the model is what proves the C#.

**The one cost, stated plainly.** The **merge** from the start gate onto the circuit is a hard
corner: **66.9° worst, ~61° mean**. It is exempt from the turn cap — which describes the circuit —
and bounded only by Dubins, which the 300-unit minimum leg guarantees at *any* angle:
`2R·sin(66.9°) = 239.4 < 300`. It happens once per race and reads as a racing start: launch, thread
the gate, hook onto the racing line.

**One replicated int still carries the whole race.** That is the property Switchback established
and the thing laps most threatened. `BreakwaterCourseSettings.RingForCrossing` folds the crossing
count into a ring (crossing 0 is the start gate; everything after it walks the circuit forward and
wraps, so the fold is a plain modulo where it used to be a zigzag), so
`IRoundStats.SwitchesThreaded` is *still* simultaneously the score, the progress bar, the token the
server validates against **and** the index of the ring to test this frame. No per-lap state exists.

⚠ **The fold is for choosing which ring to TEST; the token that travels is the CROSSING.** The
server validates a report with `gateIndex != stats.SwitchesThreaded`, and that counter counts
crossings — so from lap 2 on the ring index and the crossing diverge, and reporting the *ring*
would have every lap-2 report rejected as a duplicate of one already paid. `RingIndexFor` is the
single place the fold is applied, read by the objective arrow, the local next-ring highlight and
the AI's waypoint provider alike, because three copies of `SwitchesThreaded % something` is one
edit away from an arrow and a lit ring naming different stations.

**End condition** is authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**, and
laps split the one number that used to do two jobs:

| authored | means | default |
|---|---|---|
| `breakwaterStationTarget` | stations **laid** — this is arena mass, and moves the PhaseThresholds | 14 |
| `breakwaterLaps` | how many times the course is flown — costs **no** extra arena | 2 |
| `GetBreakwaterCrossingTarget()` | *derived*: what a pilot must **thread** | **27** |

The race target is derived rather than authored, so it can never ask for a crossing the course
cannot offer.

**But the COURSE is the authority, not the override.** Generation backs off when a shell is too
tight, and ~0.1% of seeds fail outright, so the laid count can legitimately come in under the
authored one. A target naming a station the course does not contain is unreachable, and **an
unreachable target is a match that cannot end**: every pilot threads every station that exists,
nobody satisfies `IsObjectiveReached`, and the turn runs forever with **no clock to catch it** —
this mode races to a count and authors no time monitor. So `BreakwaterStationTurnMonitor` reads
`BreakwaterController.CrossingTarget` — the laid count already folded over the authored laps, so
the monitor and the detector cannot re-derive the laps arithmetic differently — and falls back to
the override only before the course exists, warning when the two differ.

**Publishing the target is load-bearing, not cosmetic.** `MiniGameHUD.RefreshGoalStack` draws
nothing — silently, no warning, no placeholder row — when the target is 0, so a monitor that
computed the number correctly and never wrote it back would ship a mode whose objective readout is
simply **absent**, on every client, with the race working perfectly. That reads as "the goal stack
is not implemented for this mode", which is why the write is duplicated on both the server branch
and the late-start client branch.

`TurnMonitor.PublishesSecondsRemaining` is **inherited false, not overridden**, and the absence is
the decision: the payload is a COUNT, and answering true makes `MiniGameHUD` draw the **clock** row
— `m:ss`, no glyph, no target — over a number that is neither seconds nor a time.

**`EndConditionOverrides.asset` is authored with `breakwaterStationTarget: 14` and its `…Build`
twin, and it is the one place this mode would have survived NOT being** — the general trap is that
a missing YAML key deserializes to the **type** default rather than the field initializer, but here
the getter treats 0 as *use the default* and returns 14 anyway. The key is authored regardless, so
the window reads a real 14 instead of "14 (default)" and the build snapshot has something to
restore.

---

## Assets

Every asset is authored by **`Tools/Build/author_breakwater_assets.py`** (32 files, deterministic
GUIDs, idempotent, validated in memory before anything is written). **Re-tune there and re-run**
rather than hand-editing YAML.

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameBreakwater.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/BreakwaterScoringRule.asset` (`metric: 9`, `golfRules: 1`) |
| Scene | `_Scenes/Multiplayer Scenes/MinigameBreakwater.unity` (+ `EditorBuildSettings`) |
| Cell configs ×4 + spawn profile | `_SO_Assets/Cell Configs/Breakwater Cell/` |
| Arena prefab | `_Prefabs/Spawnables/SpawnableBreakwater.prefab` |
| Script + folder `.meta` ×8 | `Arcade/Breakwater/`, `Arcade/Breakwater.meta`, the turn monitor, the spawnable, the tests |
| Roster registration | `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes` |
| This document's `.meta` | `BREAKWATER.md.meta`, guid `91e4140a69805c65a0b54a9be6950766` = `md5("CosmicShore/doc/BREAKWATER.md")` |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` |
| Goal-stack icon + label | `Assets/Resources/ObjectiveIconSet.asset` metric 9, "Thread switches" — **already present, no edit** |
| Launch-panel objective icon | `Assets/Resources/ModeControlsLibrary.asset` metric 9 — **already present, no edit** |
| Arena model | `Tools/Build/breakwater_arena.py` — **committed**, and imported by the generator |

The card reads `Mode: 48`, `IsMultiplayer: 1`, `GolfScoring: 1`, `SceneName: MinigameBreakwater`,
one `Vessels` entry (**Sparrow**), players **2–4**, domains **2–3**, intensities **1–4**,
`ComebackRatePerScoreDeficit: 0.35`. The spawn profile authors `SupportedFloras: []` and no fauna;
every cell config authors `NucleusPrefab: {fileID: 0}` and `EnvironmentPrefab: {fileID: 0}` — **the
arena is not an authored environment, because the course is rolled per match**, so the controller
stands it up itself.

### The generator is the only green one in the repo

```
$ python3 Tools/Build/author_breakwater_assets.py --check
Validation passed (32 files).
  scene: shipped (read-only)
  stations 14  comeback 0.35 (2.36 levels at a quarter-of-target deficit)  sense radius 1250
  I1: port 72   4,575 prisms     837,677 volume  ->  Restless    957,677  Frenzy  1,137,677
  I2: port 60   3,795 prisms     641,833 volume  ->  Restless    761,833  Frenzy    941,833
  I3: port 50   2,730 prisms     456,896 volume  ->  Restless    576,896  Frenzy    756,896
  I4: port 42   2,385 prisms     356,842 volume  ->  Restless    476,842  Frenzy    656,842
--check: no files written; all 32 files match what this script authors.
```

Those rows are **imported from `breakwater_arena.py`, not retyped** — which is the whole point:
the cell's `PhaseThresholds` are derived from the same arithmetic that builds the arena, so the two
cannot drift. (Spot-checked against the shipped `Breakwater Cell Config 1.asset`: `RestlessEnter
5275` = 4,575 + 700, `FrenzyExitVolume 1,107,677` = 837,677 + 270,000.)

**Measured against its siblings, it is the only mode generator in this repo whose `--check`
passes.** All five were run:

```
author_switchback_assets --check   exit 1
author_drumfire_assets   --check   exit 1
author_salvo_assets      --check   exit 1
author_hijack_assets     --check   exit 1
author_breakwater_assets --check   exit 0
```

Drumfire's is the failure mode to avoid and the reason there was no green baseline to copy: a spent
one-shot `assert` sitting **above** the validation section (`donor crystal block not found`), which
fires once the donor scene moves on **and takes every check below it with it** — so its `--check`
proves nothing at all while still looking like a gate. This one raises nothing: every failure
appends to an `errors` list, the run reports all of them at once, and it writes nothing.

**The scene clone STANDS DOWN rather than asserting.** Its output says so explicitly —
`scene: shipped (read-only)` — because a measured re-run of `author_switchback_assets.py` rewrote
`MinigameSwitchback.unity` and changed a NetworkObject's `GlobalObjectIdHash`
(2537121143 → 99744438), a silent cross-peer scene-sync break. Unity owns those values once the
scene is committed, so the wiring checks gate the file rather than a byte diff. To re-bootstrap
from the donor, delete the scene and re-run.

**One prefab, not four.** Every intensity ships the same `SpawnableBreakwater`: station geometry is
a closed-form function of the per-station `PortRadius` the **controller** supplies, and the
prefab's only serialized fields are shoal tuning, which the model prices identically at all four
levels. Four variants would be four byte-identical assets differing in nothing — four places for
one number to drift.

Two further traps it respects, both of which have bitten this repo:

1. **`SpawnProfileSO`'s template omits every non-zero default**, and Unity fills a missing key with
   the **type** default rather than the field initializer — so every field is authored explicitly.
2. `CallToActionTargetType` and `PreviewClip` are **retired keys** the old templates still author;
   neither appears in `ArcadeGameBreakwater.asset` (verified: zero occurrences). Emitting either is
   what makes `author_salvo_assets.py` and `author_hijack_assets.py` red today.

And two things the SCENE has to get right, both verified in the authored file:

- **`differenceSource: 8`** (`SwitchesThreaded`). `ElementalComebackSystem.EnsureExists` **respects
  a scene-authored instance**, so a stale donor value is not corrected at runtime — which is why
  three sibling scenes currently read a stat their pilots do not move.
- **`spawnFormation: 1`** (`EquatorialRing`) with **`spawnRingRadiusFloor: 480`**, the model's own
  `SPAWN_RING_RADIUS`. Without both, the fairness argument above is void: station 1 sits on the
  ring's pole, and under `Symmetric` (0) there is no point equidistant from every spawn.

---

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Breakwater = 48`, plus the tripwire comment 46 → 47 |
| `EnumIntegrityTests` | member-count tripwire 46 → 47, `[TestCase(GameModes.Breakwater, 48)]` |
| `SwitchbackGateRing` → `RaceGateRing` | `git mv` into `Arcade/Racing/` (guid unchanged), primitive `Build` signature |
| `SwitchbackController` | re-pointed at `RaceGateRing`, call site passes position/axis/radius |
| `author_switchback_assets.py` | `RaceGateRing`'s path + the `Racing.meta` folder guid, seeded from the **old** name so the shipped `.cs.meta` guid is preserved |
| `ElementalComebackSystem` | `case GameModes.Breakwater:` falls through to `ScoreDifferenceSource.SwitchesThreaded` |
| `MiniGameHUD` | `GameModes.Breakwater` → `BreakwaterObjectiveProvider` |
| `EndConditionOverridesSO` | `breakwaterStationTarget` live/build fields, `GetBreakwaterStationTarget()`, `DefaultBreakwaterStationTarget = 14`, and the four sync/compare paths |
| `EndConditionOverridesWindow` | the field, the resolved-value row, the build-snapshot row, the help text |

**No new metric, no new stat, no new RPC, no new scoring code, no impact effects, no vessel edits,
no prefab edits.** The mode is a composition of shipped systems plus a course generator, a station
builder and an arena.

---

## In-editor verification (authored headless — NOT YET RUN)

**Nothing below has been run, and the mode has never been played.** The scene now exists on disk,
so it can be opened — but no part of this list has been executed, and no claim anywhere in this
document about how the mode FEELS is an observation. What *has* been done is offline and is stated
exactly:

- Every file was **compiled for real** (Roslyn against signature-faithful stubs transcribed from
  the repo, each with a negative control proving the harness bites), and the controller was then
  re-compiled against the **real** promoted `RaceGateRing` and the **real** course generator
  together — the cross-file binding no single-file compile could check.
- Every base member `SpawnableBreakwater` calls was checked against the shipped
  `CellEnvironmentSpawnableBase` / `SpawnableBase` / `PrismTrailBuilder` rather than assumed.
- `BreakwaterCourseTests` was **executed**: 49 cases, 0 failures, with 8 mutation controls
  confirming each gate fires. Its four newest cases — the two that measure the eye off the
  **emitted prisms** and the two that hold the spawn-pad clearance — were proved separately:
  `DistanceFromStationAxis` was compiled and run against known controls (a unit box at 10 reports
  9.5; a box on the axis reports 0), reproduces the Python model's independent convex-shadow
  measurement to 1e-4 at all four intensities, and reports **16.5** when the near-edge clip is
  reverted.
- `Tools/Build/breakwater_arena.py` passes all of its proofs.
- `check_conditional_compilation.py` and `check_enum_member_references.py` are clean.
- `author_breakwater_assets.py --check` **exits 0** over all 32 authored files, with the cell's
  `PhaseThresholds` imported from the arena model rather than retyped.

Run this in order:

1. **Open** `MinigameBreakwater.unity`. Every script reference resolves; the controller's inspector
   shows `rule` = BreakwaterScoringRule, `cellData` = the cell's Runtime Cell Data, `arenaPrefab` =
   SpawnableBreakwater, and the course/AI/detection fields at their authored values. The spawner
   shows `spawnFormation` = **EquatorialRing**. The cell authors **no nucleus**, **no flora**, **no
   fauna**, and its `PhaseThresholds` match the table above for the selected intensity.
2. **THE ARENA EXISTS — the load-bearing check.** Enter play. The connecting panel holds through
   generation and the prism lay; when it releases, fifteen dishes hang between 420 and 1080, each
   facing a different way, each with a blue ring at its port and a woven danger plug in its throat.
   Station 1 is directly "above" the cell centre and every pilot is the same distance from it.
   The console shows `[Breakwater] Course seed …: 15 stations, intensity N, port radius …`.
   **Look around from a standing start before touching the stick**: no station, dish or shoal
   cluster is anywhere near the spawn pads — the walk rejects any station within its own bounding
   sphere plus four hull radii of one, and the measured worst case leaves 52 units of air.
3. **The three verbs.** (a) **Fire** a skyburst at a plug: a spherical hole appears — at intensity 4
   the whole plug goes with one rocket at resting Charge; at intensity 1 it does not, and where you
   aimed matters. (b) **Saw**: turret stance grinds the weave open. (c) **Thread**: fly the eye
   without touching a bar. All three then cross the ring and all three score the same 1.
4. **Ammunition closes the loop.** Start with a full bay. Open a door, then count: destroying ~50
   hostile prisms should refill one rocket, and the Charge card's gauge should climb toward it. A
   station's dish alone should roughly fund the next door.
5. **THREADING SCORES, and only in order.** Cross station 1: the goal row ticks **1/14** and reads
   "THREAD SWITCHES"; the arrow and the lime ring move to station 2. Cross station 3 without
   threading 2 — nothing happens. Go back through 2, then 3: both count.
6. **Backwards counts.** Thread a station from the far side: it still counts.
7. **Missing the mouth does not count.** Fly past a ring just outside its rim: no tick.
8. **Danger bites.** Clip a plug bar: full-stop slow, all-element debuff, boost reset. Clip a
   **collar** block instead: an ordinary prism hit, no debuff.
9. **Client detection.** In a real lobby, the CLIENT threads stations and its own count rises on
   both machines. Reverse it.
10. **The lead-runner fold.** Two pilots on one domain. A threads 5, B threads 0: the domain's HUD
    box reads **5**. B then threads 3: the box still reads 5, not 8. Each pilot's own goal row shows
    their own count.
11. **Win + scoreboard.** First domain to put ONE pilot through station 14 ends the turn; winners
    show "VICTORY" + course time. Replay (scene reload) resets everything to 0/14 **and rolls a new
    course**.
12. **AI flies the course and opens its own doors.** AI Sparrows thread stations in order rather
    than orbiting a ring, and should be seen firing down a station's axis on the run-in. An AI that
    fires nothing must still thread the eye — if one gets **stuck** at a plug, the two-waypoint aim
    or `drift` is wrong, not the geometry.
13. **Intensity.** Compare 1 and 4: the ports are visibly tighter and the course visibly twistier at
    4, and the station count is 15 at both. One rocket takes a whole plug at 4 and does not at 1.
14. **Comeback.** Let one domain fall ~4 stations behind: the trailing pilots' element flowers fill
    ~2.5 levels.
15. **Collider budget.** With the profiler open, fly the tightest fold of the course at intensity 4
    and confirm active prism colliders stay near the measured 568 rather than climbing with the
    whole arena.
16. **Baselines.** FrogletTools ▸ Ecology ▸ **Measure Cell Environment Baselines** must agree with
    the *Phase thresholds* table. Disagreement means the C# and the model have drifted.
17. **The circuit closes.** Thread the last station and confirm the objective arrow points at the FIRST circuit station, not back the way you came, and that the start gate is never re-offered.
18. **Regression — Switchback unchanged.** Launch Switchback: twenty rings, the same gate race,
    lime next-gate highlight, AI flying the course. `RaceGateRing` is shared now, so a Breakwater
    change can break it.

---

## Known limitations / follow-ups

- **The scene needs one open-and-save in the editor before it is flown.** A YAML clone cannot
  compute a `GlobalObjectIdHash`, so `MinigameBreakwater.unity` carries the Salvo donor's two
  values verbatim. They are distinct **from each other**, which is the condition that actually
  makes `NetworkSceneManager.PopulateScenePlacedObjects` throw, and the two scenes are never
  loaded together — but every other shipped scene carries its own pair, and Unity re-mints these
  the first time the scene is saved. Do that and commit the result. The generator's
  `SCENE_ALREADY_SHIPPED` guard is what makes it safe: once the scene exists it is registered
  read-only and validated by named wiring assertions, so no re-run can ever put the donor's
  values back.

- **The mode has never been run.** Not once, in any form. Everything above is geometry, arithmetic
  and headless proof — the C# was compiled and executed outside the editor against Unity-type
  stubs, which is a real check of the arithmetic and no check at all of the engine.

- ~~**The plug's rakes were clipped on their centreline, so the eye was 16.5 and not 18.**~~
  **FIXED** — see "The clip is against the bar's NEAR EDGE" above. Measured off the emitted prisms
  at all four intensities, the weave and the collar now both stand at exactly **18.0000**
  (`1.461 ×` the hull), against a negative control that restores the old test and reports 16.5.
  Cost: +6 bars at intensity 1 and −92 u of bar length everywhere, so every `PhaseThreshold` was
  re-derived.

- ~~**Nothing kept a station off a spawn pad.**~~ **FIXED** — see "…and no station may swallow a
  spawn pad" above. 7 of 14,400 measured pad-cases put a pilot inside station structure; the walk
  now rejects any candidate whose bounding sphere plus four hull radii reaches a pad, at zero cost
  to the generation rate, and the shoals honour the same pads through `ClusterMargin`.

- ~~**A total generation failure hung every client's connecting panel.**~~ **FIXED** — every peer
  opens a `PrismTrailBuilder.BeginArenaBuild` bracket in `OnNetworkSpawn`, and the give-up branch
  released only the **server's**, so each client held its panel until the 180-second stall cap with
  the one explaining error on the host console. `CourseUnavailable_ClientRpc` now tells every peer,
  which is what makes the branch degrade *loudly* rather than *silently on three machines*.
  General shape: **a bracket opened on every peer has to be closed on every peer, including on the
  path where the thing it was waiting for never happens.**

- ~~**The reused scoring rule says "Gates", not "Stations".**~~ **FIXED.**
  `SwitchbackScoringRuleSO` hardcoded `"{n} Gates Left"`, `"{n} Gates"` and `"GATES LEFT"`, so a
  Breakwater scoreboard would have said *gates* about a course of *stations*. It now carries a
  serialized **`unitNoun`** and `BreakwaterScoringRule.asset` authors `Station`.
  **The fallback lives in CODE, not in the field initializer, and that is the load-bearing part:**
  Unity fills a key a serialized asset does not carry with the TYPE default — an empty string —
  never with the initializer, so the shipped `SwitchbackScoringRule.asset` (authored before the
  field existed) would otherwise have started rendering `"3 Left"` and `" LEFT"`. *A new
  serialized field is a silent regression on every asset already on disk unless the reader treats
  ABSENT as a real case.* That is the `SpawnProfileSO` trap the ecology notes record, reached from
  the scoring side. The **goal row** was never affected — it is keyed on the metric, and the
  shipped label "Thread switches" is literally true of both modes.

- **`SparrowHullRadius = 12.32` is not re-derivable from anything in the tree.** Three files state
  it as measured (`BreakwaterCourse`, `breakwater_arena.py`, and the test's prose) and **no script
  reproduces it** — unlike Switchback's `DolphinHullRadius = 2.86`, whose derivation is at least
  written out in `SWITCHBACK.md`. It cannot be checked from `Sparrow.prefab` alone either: that
  file carries exactly **one** `BoxCollider` and **one** `SphereCollider` of its own (the latter a
  `DummySkimmer`), so the hull colliders live inside nested prefab instances and a measurement has
  to resolve those. Every "the eye is threadable" claim in this document rests on that constant. A
  `Tools/Build/measure_vessel_hull_radius.py` covering the whole fleet would settle it once.

- ~~**The model's `plug lines/rake` row is mislabelled**~~ **FIXED** — it printed `runs ÷ 2` where
  the real per-rake line count is 12 / 10 / 8 / 6. Display only (every derived number is computed
  from `plug_runs` directly), but a table that cannot be read literally is a table that gets
  quoted wrongly later.

- ~~**`EmitBarRun`'s comment names the wrong ladder maximum.**~~ **FIXED** — it said *"58.5 units
  (intensity 1, the 42-unit offset line)"*, which is intensity 1's maximum rather than the
  ladder's; it now names **58.79** at intensity 4 (the 30-unit offset line). Both were always
  inside the 62 cut and nowhere near the clamp, so nothing was wrong — the comment was.

- **15 stations is unmeasured.** Chosen as the number the arena model sizes everything else against,
  not from a playtest. It is one editor field, but it also sizes the arena — raising it adds mass
  and moves every `PhaseThreshold` with it.

- **The comeback rate 0.35 is arithmetic, not a playtest.** `0.25 × 27 × 0.35 = 2.36` levels at a
  quarter-of-target deficit. The generator **asserts** it in both directions (`require(_quarter >=
  1.0)` and `<= 5.0`) rather than trusting the number, because `bonusLevels = deficit × rate` makes
  the rate a function of the **target** — the trap `DOGFIGHT.md`, `BENDS.md`,
  `WILDLIFE_LIBERATION.md` and `SWITCHBACK.md` have each recorded independently, on four different
  modes — **and it fired here.** Two laps took the target 14 → 27, which at the shipped 0.7 would
  have handed a quarter-of-target deficit **4.7 element levels**, nearly half the sustained band,
  for being a quarter behind. Halving it to 0.35 holds the 2.4 the mode was tuned at and lands on
  Dog Fight's curve, the nearest sibling by structure. *Changing how long a race is retunes its
  comeback whether you meant to or not.*
  Re-targeting the mode means re-deriving the rate — and the assert is what makes that impossible
  to forget.

- **No toasts.** No `GameToastConfigSO`, so no "STATION 12/14" or lead-change announcement. Rampage,
  Dog Fight, The Bends, Salvo and Switchback all ship this way.

- **No mode preview.** No `ModePreview_Breakwater.asset`, so the arcade card falls back to its
  `CardBackground` and hides Test Flight — the same state Salvo and Switchback ship in. Worth
  authoring here, since the arena is the thing worth looking at; note the preview's satellite arena
  has no controller to roll a course, so it would need a canned one.

- **Adding a card is adding a ROW.** Menu_Main's arcade grid is authored at 3 × 4 and
  `EnsureGridCapacity` / `FitScrollContent` / `ReportUnreachableCards` now handle the growth
  generally — but a fourteenth roster mode should still be checked against
  `ArcadeGridCapacityTests` and the on-screen report, per `SWITCHBACK.md`'s long record of that bug.

- **`AIPilot.UseAbilityCoroutine` has no range test**, so an AI wastes rockets on long legs (above).
  A platform-level "is anything in front of me" gate would help this mode, Dog Fight and Salvo at
  once.

- **The station rings are not in the Codex.** They are neither flora, fauna, crystal nor toy, so no
  kingdom currently fits them — the same gap `SWITCHBACK.md` records.

- **`ServerPlayerVesselInitializer.EnsureSpawnPosesReady` has a latent null-deref** when
  `spawnRingRadiusFloor > 0` and the cell has not resolved. Not touched here; do not lower the floor
  to 0 in this mode's scene without reading it.

- **Nobody has flown this.** Every claim about *feel* above — that a dish reads as a landmark, that
  the collar makes a rocket forgiving, that threading is frightening at intensity 4 — is a design
  intention supported by geometry, not an observation. The first playtest is entitled to disagree
  with any of it, and the three dials it should reach for first are the **eye radius** (the whole of
  the third verb), the **port ladder** (the whole of the first), and `shoalOffsetMin`/`Max` (the
  ammunition budget between doors).
