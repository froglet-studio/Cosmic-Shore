# Prism Motion — the fundamental, and its collision rule

Garrett, 2026-10-14: *"the rhino and stoat have introduced prism movement. prism movement should be a
fundamental with consistent expectations. one of them should be that when a moving prism collides with
another prism both prism should be destroyed."* — and, the same day: *"use boxes when moving prism count
is low, switch to spheres when moving many prisms."* — and: *"make a variety of prisms collisions that
use the ultra high res prism used by slicing. these should look spectacular when looked at but
undistinguished from the normal explosion when squinting. when we can afford it use those for level one
and turn it off on level 2. your setting at 256 (192) can define the transition to level three."*

Garrett, 2026-10-27: *"change the fundamental movement interaction of prisms (effecting all shielded prism
movement), so shielded prisms bounce off each other and other prisms instead of being destroyed on collision."*
That is expectation 1a below.

The fundamental's entry (canonical term **Prism movement**) is in
`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` § "The fundamentals". This file is the design and the
verification record. The code is one static class, `PrismMotion`
(`Assets/_Scripts/Controller/Prisms/PrismMotion.cs`).

## 1. What a moving prism is

A prism is **moving** when a FORCE displaces it from where it rests. Three forces do today:

| Mover | What moves the prism | Shape of the move | Path |
|---|---|---|---|
| The Rhino's hull plough (`RhinoPloughPrismEffectSO`, `RHINO_PLOUGH.md`) | a hull ram shoves it sideways out of the furrow | ONE step, made the frame of the ram (the visual slides on the prism clock afterwards) | one-shot: `PrismMotion.TrySweep` |
| The Jacks' net (`JacksNet.DragPrisms`, `R_VesselActions/JACKS.md` §1.1; Garrett, 2026-10-25: "lets have prisms move and be dragged by the net. they should only be destroyed when they collide with other prisms") | the net between the two flying fish carries what its mouth crossed | one step per frame, per dragged prism (up to `netPrismCap`, 48); a plant's prism is plucked first (`HealthPrism.LeaveAsSkeleton`: free cell mass, no longer posed by its plant), so it is a moving prism and not a posed one | the box path: `PrismMotion.TryFirstContact` with the net's own prisms as movers of the same step (`MoverSteps`), so the catch never breaks itself; level by `NextLevel` over the count |
| Every black hole's gravity field (`BlackHoleGravityField`, `BLACK_HOLE.md` §3) — the Stoat's dipole, the drift pair, the crystal wormhole | the holes' pull, integrated every frame | one step per frame, per body (up to 6,000 bodies) | climbs the LADDER (§2.3) with its body count: boxes (`PrismMotion.TryFirstContact`, main thread) at levels 1–2, spheres (`PrismMotion.ContactJob`, Burst) at level 3 |

**Not a moving prism**: one POSED by its owner every frame — a creature's body (`HealthPrism`, the
fauna movers), a builder setting a stone (`BuilderPrismWorld`), a growing plant (`AssembledFlora`), an
assembler bonding a wall (`WallAssembler`, `GyroidAssembler`), a projectile carrying its payload
(`FullAutoBlockShootActionExecutor`). Those prisms are the shape of their owner, not mass a force
displaced, so they do not run the rule. They are valid TARGETS of it.

## 2. The expectations

1. **A moving prism that runs into another prism destroys BOTH.** Through `Prism.DamageByCollision`
   (`Prism.Damage(devastate: true)` plus the level's look; `PrismMotion.Annihilate`): an ordinary shield is not an answer, the debris leaves along the
   closing velocity (restitution 1/3, ceiling 200 u/s — the ram's numbers, so a prism thrown into a
   prism comes apart like one a hull hit at that speed), and both deaths go through the ordinary
   destruction path (stats, cell sums, the index).
1a. **Shielded prisms bounce** (Garrett, 2026-10-27; **LOCKED** 2026-10-28, the rule every mover builds on, the Meerkat's
   soliton first; `PrismBounce`, `PrismMotion.Bounces`). A collision a
   SHIELDED prism is part of, as the mover or as the prism it runs into, destroys neither: the movers rebound
   off the contact, keeping `shieldBounceRestitution` (0.6, `Resources/PrismMotionConfig`) of the closing speed.
   A shielded mover bounces off super-shielded mass too; an unshielded one still goes against it (4).

   | Mover | Prism it runs into | Outcome |
   |---|---|---|
   | plain | plain | both destroyed (1) |
   | shielded | plain | bounce |
   | plain | shielded | bounce |
   | shielded | shielded | bounce |
   | plain | super-shielded | the mover is destroyed (4) |
   | shielded | super-shielded | bounce |

   Each mover rebounds its own way, since it owns the prism's motion:
   - **The gravity field:** the body's velocity rebounds off the contact normal (from the struck prism's centre to
     the mover's). Against another body the impulse is shared equally, equal and opposite, so momentum is kept;
     against a prism that is not a body, the mover takes all of it and the other keeps still
     (`BlackHoleGravityField.Bounce`, `BouncesTotal`).
   - **The Rhino plough:** the thrown prism stops at the contact and the rest of the throw rebounds off it, mirrored
     and shortened by the restitution (`PrismBounce.Rebound`). A second prism in the way of the rebound stops it
     there: a bounce again, or both go if neither of that pair is shielded.
   - **The Jacks net:** the net poses its prisms, so a shielded collision holds the dragged prism against the other
     at the contact instead of through it. Neither goes, and the net keeps it.
2. **"Runs into" = the contact shapes go from apart to touching over the step**
   (`PrismMotion.EntersContact`, a box overload and a sphere overload). Both prisms are taken as
   translating linearly over the step (a static target is a zero step; no mover turns a prism), so two
   bodies falling side by side in lockstep never meet, and a pair already touching when the step began
   is **at rest**, not a collision — a trail's neighbours are not annihilated by being pulled together.
   Both tests are symmetric.
3. **What a mover can afford follows how many prisms it moves — the LADDER** (`PrismMotionLevel`,
   `PrismMotion.NextLevel`, `Resources/PrismMotionConfig`). The rule is the same on every rung; the
   precision and the look change:

   | Level | Moving prisms | Contact | Look |
   |---|---|---|---|
   | **1 Showcase** | ≤ `showcaseMaxMovers` (64) | exact oriented **boxes** | a **collision burst** on the ultra-high-res prism (§2.7), while its budget has room; else the explosion |
   | **2 Plain** | ≤ `boxMaxMovers` (256) | exact oriented **boxes** | the ordinary explosion |
   | **3 Bulk** | above | the **spheres** of the prisms' volumes, in Burst | the ordinary explosion |

   Each line is crossed upward at its value and back downward only at three quarters of it (64 → 48,
   256 → 192), so a count hovering at a line does not flip every frame. The plough moves one prism per
   ram, so it is always level 1 (`PrismMotion.SingleMoverLevel`); the gravity field climbs with its body
   count. `showcaseMaxMovers` 0 turns level 1 off; `boxMaxMovers` 0 forces level 3.
   - **Box** (exact): the prism's own authored box collider through its live transform
     (`PrismMotion.TryGetBox`), swept by the separating-axis test. Two translating boxes keep the same
     15 axes all step, so each axis's overlap is one interval of t and the boxes overlap exactly where
     all 15 do — the first touch is exact, not sampled. Main thread, per mover: the broad phase is
     `QuerySegment` within the mover's half-diagonal + `MaxBoxReach` (12 u) of its path. A virtual swarm
     member (no transform until materialised) is an axis-aligned cube of its contact radius.
   - **Sphere** (cheap): the sphere of the prism's own volume (`PrismMotion.ContactRadius`), the
     quantity the cells already weigh, capped at `MaxContactRadius` (6 u, ~900 u³) to bound the broad
     phase. Thousands of bodies at once, in Burst.
4. **Super-shielded mass is never destroyed.** It is indestructible: `Prism.Damage`'s own gate makes
   it deflect, and an unshielded mover is destroyed against it (a shielded one bounces, 1a). No force moves it in the first place (the plough
   jiggles it, the gravity field never admits it).
5. **Scored to the force behind the motion**: the Rhino's pilot for a plough throw; the hole's owner
   (`BlackHole.CaptureOwnerName` / `CaptureDomain`) for a body, read off the hole nearest the body,
   or the anonymous `"Black Hole"` in `Domains.Blue` for an ownerless hole — exactly what that hole's
   capture is scored to. This keeps the rule inside **Mass is conserved**: the collision is the act
   of a vessel or a field, never a passive decay.
6. **The move is final the frame it is made** (the movers contract, `PRISM_ANIMATION.md` §3.6): the
   transform, the collider, the spatial index and the shell agree, so a moving prism is always tested
   where it actually is, and later movers in the same frame see it there.
7. **The look at level 1 — spectacular up close** (§3a): the destruction detail level the viewer's
   distance picks (the explosion, then the silver gnomon cut at four depths, every fragment bursting out
   on its own along the hit), game-wide. (Rounds 1–8 also asked it to read as the explosion when squinted
   at; the designer retired that on 2026-10-16.)

## 3. How each path keeps it

**One-shot (`TrySweep`, boxes).** Before the plough commits a shove from `from` to `to`, it asks
`TryFirstContact` for the first prism the step runs into: `PrismSpatialIndex.QuerySegment` (real
prisms) and `QuerySegmentVirtualIds` (data-driven swarm members) gather centres within reach of the
path, each is refined box against box, and the earliest wins (a virtual member is materialised only if
it is the winner). A hit stops the prism at the contact point and annihilates the pair; a clear path
completes the shove and its slide.

**Batch, few bodies (`BoxContacts`, boxes).** After the poses are written back and before the batch
index write, each body that moved runs `TryFirstContact` from its start box (its transform's box minus
its step); another body is met along its own step (`PrismMotion.MoverSteps`, over the slot → body map).

**Batch, many bodies (`ContactJob`, spheres).** After `IntegrateJob`, a fourth Burst job runs one body
per index: it walks
the index's 8 u buckets around its step (reach `rSelf + MaxContactRadius`), reads every live entry's
stored position and live volume (`PrismSpatialIndex.TryGetMotionView` — `_spatial`, `_cellData`,
`_buckets`, read-only), and for an entry that is itself a body (`MapSlotsJob`'s slot → body map) uses
that body's own step instead. It emits each body's earliest contact (≤ 1 per body, so the hit list
never overflows). It completes before the batch index write, because the stored positions it reads
ARE every body's pre-step positions. On the main thread `ApplyContacts` annihilates each pair before
`ApplyVerdicts`, which then drops every destroyed body from the ledger — so a collision outranks a
capture or a throat transit on the same frame. A pair both bodies reported is annihilated once
(`Damage` is idempotent on a destroyed prism).

## 3a. The look — the destruction detail levels (level 1)

A prism destroyed by a level-1 collision is drawn at the **destruction detail level** the viewer's
distance picks, like every other prism death in the game (the detail levels are game-wide since
2026-10-14 — `Docs/PRISM_ANIMATION.md` §4.12, `PrismInteractionLod`, `Resources/PrismInteractionLodConfig`).
Destruction is one spectrum, coarse to fine, by the prism's projected diameter over the screen height:

| Level | Size on screen | Up close |
|---|---|---|
| **Explosion** | under `shatterMinScreen` (5%), off screen, or over budget | the ordinary 24-wedge explosion |
| **Cut level 1 / 2 / 3 / 4** (round 7: the silver gnomon cut, 192 / 504 / 1,320 / 3,456 fragments, each spinning a little off its neighbours by a gradient noise; the Sierpinski cut below stays selectable) | from 5% / 10% / 16% / 24% | the explosion's own wedges (depth 0) cut deeper: each corner triangle splits at its edge midpoints, the middle stays whole — 96 / 312 / 960 / 2,904 fragments of a self-similar range of sizes; a shallower depth is the deeper with its finest corners merged. Every fragment moves by the EXPLOSION's own law (round 5, kind `WEDGE`): its response to the impact velocity, its two spin axes, its speed-capped spin rate, its slide and its erosion fade, as a wedge scaled to the fragment |

The AMOUNT grades it too: once the shared budget is half full every new death is one level coarser, at 80%
two (`PrismInteractionLodConfig`).

No white accents (the designer, 2026-10-14): the only contrast is the brighter inside colour on the
back faces once the surface opens. Both finer levels share the explosion's ENVELOPE — its flight
(`PrismDebris.ResolveExplosionFlight`, linear `v·t`), its life (`PrismExplosion.DefaultDuration`, 7.5 s),
its palette, a prism-sized cloud (`3.5 + 1.5·spread` radii, spread capped at 1), and its linear fade — so
squinted at, each is meant to read as the explosion.

**Round 9 (the designer, 2026-10-16): the squint is retired.** The silver gnomon cut's range of sizes lets a
far-away player take the small fragments as unseen, so a level no longer has to read as the explosion squinted at:
every fragment bursts out on its own direction, filling the sphere, the small ones flung farthest and gone first
(the FRAGMENT kind, `Docs/PRISM_ANIMATION.md` §4.12; measured in the Collision Burst Lab's Look scorecard). The
squint measurements below are history.

**Measured, round 5:** with the explosion's law, every depth is the explosion's size (0.99–1.02) and fade
(0.05–0.07 of it, closer than two explosions); the squint, 0.21–0.22 against the 0.097 band, is held off by the
brighter inside colour alone (0.13–0.14 without it). The earlier rounds' text follows.

**Measured (the Collision Burst Lab, `Docs/Studios/COLLISION_BURST_LAB.md`,
https://claude.ai/artifact/F47yG1UjBkK662rEnhk7m9):** with the white gone, the glitter's colour difference
from the explosion fell (0.187 → 0.109), but neither level yet squints as the explosion on rendered,
blurred pixels (shatter 0.346, glitter 0.303 against a 0.097 two-explosion band; a never-breaking prism
scores 0.315). What remains is shape — the cloud is ~0.79 of the explosion's size and its last pieces go
sooner. Round 1's proposal (carry the detail on the explosion's own wedges) stays in the lab's Archive.

Above level 1 (many movers) a collision asks for the plain explosion (`PrismEventData.PlainDeath`), as
the ladder always said. **Budget**: `maxLiveBursts` 48 entities and `maxLiveTriangles` 80,000 triangles,
destruction and the steal's sheds together, zero colliders, one batch per mesh; a level that does not fit
steps down, and past every level the ordinary effect.

## 4. Stated limits

- **Spheres at level 3.** Above `boxMaxMovers` a long, thin prism meets the world as
  the sphere of its volume: it can pass closer along its length than its box would, and meet sooner
  across its width. The index carries no orientation, so a Burst pass over thousands of bodies cannot
  test boxes; raise the budget if play shows the difference and the main-thread cost allows it.
- **Boxes are the prism's own box, not its shield.** A shielded prism's shell (the octahedron the hull
  meets) is wider than its box; the collision rule uses the box for both.
- **A box target whose half-diagonal exceeds `MaxBoxReach` (12 u)** can be missed by the box path's
  broad phase when its centre is far from the path.
- **A throat transit is a teleport, not a sweep** (`ApplyVerdicts` moves the body source-side after
  the contact pass) — it does not test the path it skips, as nothing physical crosses it.
- **Per peer.** Both movers already run on every peer from local state (the plough as the ram did,
  the gravity field as the wormhole toll does), so two peers can disagree on a collision at the edge
  of a contact — the same stance as those systems' own verdicts.
- **The detail level is photons only and per peer.** Like the explosion it replaces, each peer draws its
  own, picked by that peer's own camera — two peers may draw one death at different levels, never a
  different outcome.
- **"When we can afford it" is a budget, not a frame-time measurement.** Level 1 is a mover-count
  ceiling plus a live-burst ceiling and a size-on-screen line; none reads the frame time. If profiling
  shows a burst wave costing frames, lower `maxLiveBursts` (`PrismInteractionLodConfig`) or
  `showcaseMaxMovers`.
- **Cost.** Box path: up to 256 bodies × one managed segment query + a 15-axis test per candidate, on
  the main thread. Sphere path: up to 6,000 bodies × the buckets their reach spans (8–27 at 8 u
  buckets), in parallel. Neither yet profiled in the editor — see §6.

## 5. Files

| File | Role |
|---|---|
| `_Scripts/Controller/Prisms/PrismBounce.cs`, `_Scripts/Tests/Editor/PrismBounceTests.cs` | Shielded prisms bounce (1a): who bounces, the contact normal, the shared impulse, the plough's rebound |
| `_Scripts/Controller/Prisms/PrismMotion.cs` | The rule: `Annihilate`, `Bounces`, `BounceRestitution`; boxes — `Box`, `TryGetBox`, `EntersContact(Box…)`, `TryFirstContact`, `MoverSteps`, `TrySweep`; spheres — `ContactRadius`, `EntersContact(float3…)`, `ContactJob`, `MapSlotsJob` |
| `_Scripts/Controller/Managers/PrismSpatialIndex.cs` | `StoredPosition`, `ContactVolume`, `TryGetMotionView` (region "Prism motion") |
| `_Scripts/Controller/Environment/BlackHole/BlackHoleGravityField.cs` | `PrevPoints` out of the integration, `NextLevel` (the ladder), `BoxContacts`, `ScheduleContacts`, `ApplyContacts`, `CollisionsTotal` |
| `_Scripts/ScriptableObjects/PrismMotionConfigSO.cs`, `Resources/PrismMotionConfig.asset` | The ladder (`showcaseMaxMovers` 64, `boxMaxMovers` 256) |
| `_Scripts/ScriptableObjects/PrismInteractionLodConfigSO.cs`, `Resources/PrismInteractionLodConfig.asset` | The game-wide detail levels: the size-on-screen lines, the budget, the shape, the shed weights (`Docs/PRISM_ANIMATION.md` §4.12) |
| `_Scripts/Utility/Effects/PrismInteractionLod.cs` | Picks the level: destruction (explosion / shatter / glitter) and stealing (grade / shed) |
| `_Scripts/Utility/Effects/PrismCollisionBurst.cs` | The carrier: culling bounds, stamp, drain, retire (destruction and sheds on separate expiry queues) |
| `_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl` / `.shader`, `_Graphics/Materials/PrismCollisionBurstMaterial.mat` | The five kinds, the two fade curves, the URP passes |
| `_Scripts/Utility/Foundation/HighPolyPrismMesh.cs` | `GetUnwelded` (TEXCOORD2 = triangle centroid + random); `GetSierpinski` (the destruction's cut at a depth); `GetShed` (the union-jack cut the sheds draw) |
| `_Scripts/Utility/Foundation/PrismSierpinskiTiling.cs` | The destruction's Sierpinski cut by recursion depth — pure, held to the lab's port and to `Prism.asset` (depth 0) by `parity.py` (.NET) |
| `_Scripts/Utility/Effects/PrismDebris.cs` | `ResolveExplosionFlight` — the explosion's flight, shared |
| `_Scripts/Controller/Vessel/Prism.cs`, `PrismEventChannelWithReturnSO.cs`, `PrismFactory.cs` | `DamageByCollision` → `PrismEventData.PlainDeath` (above level 1) → the factory's slice, detail level, explosion |
| `Tools/Shaders/verify_prism_collision_burst.py` | Executes the shipped HLSL (12 properties, 5 negative controls, the shipped grade executed) and glslang-compiles every pass |
| `_Scripts/Tests/Editor/PrismCollisionBurstTests.cs` | The ladder and its hysteresis, the envelope and overshoot vs the HLSL, the bounds, the shed life and skin, the shed weights, the size-on-screen pick, the unwelded mesh, the shipped configs and wiring, no white accents |
| `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs` | `TrySweep` before the shove |
| `_Scripts/Tests/Editor/PrismMotionTests.cs` | Spheres and boxes: first touch, stop-short, pass-wide, at-rest, separating, two movers, lockstep, symmetry; a long slab its sphere misses; a gap only the edge axes see |

## 6. In-editor verification

No `unity` binary was available in the authoring session. Both configs of the headless reference
compile (`Tools/Build/unity_refcompile`) are clean, `verify_prism_collision_burst.py` passes with every
negative control firing (eleven as of the lab's round 10), and nothing has been seen in play — whether
the bursts READ as spectacular, and how strongly the hit should shape them, is the playtest's answer.

1. **Plough into a wall.** Freestyle (Menu lava lamp). Lay a trail, then ram a second trail that runs
   parallel to it a few units to its side, so the plough throws prisms toward the first. Each thrown
   prism that reaches the first trail must stop there and BOTH must explode; one thrown into open
   space slides out and survives, as before.
2. **Plough into super-shield.** Same, with a super-shielded wall as the target: the wall jiggles and
   survives, the thrown prism explodes.
3. **Dipole into a field, both shapes.** Run it once in a sparse field (few bodies: boxes) and once in a
   dense one (past 256 bodies: spheres; the `BlackHole` verbose channel reports the body count). Stoat:
   lay a pair. Prisms falling in from different
   directions that meet must explode in pairs on the way in; a trail falling in as a ribbon must NOT
   chain-explode from its own neighbours (they start touching = at rest).
4. **Scoring.** In an arcade mode with a destroyed-prism score, both kinds of collision count for the
   Rhino's / the Stoat's pilot.
5. **Cost.** `prof` with a Stoat pair at ~250 bodies (boxes: the main-thread `BoxContacts` must stay well
   under a millisecond — if not, lower `boxMaxMovers`) and over a full body budget (~6,000,
   spheres: `ContactJob` must be a small fraction of `IntegrateJob` — if not, lower `MaxContactRadius`).
6. **The detail levels, up close.** Plough a few prisms into a trail (level 1) with the camera close:
   each comes apart into silver triangles and gnomons of many sizes that burst out along the hit, tumble
   and shrink away, the smallest first — no white anywhere. Back the camera off: the same collision
   explodes plainly.
   No magenta (a rejected shader), no pop on the first frame, nothing culled while still on screen.
7. **The flight.** Side by side — a plough collision and a sword-free hull ram of a stationary prism by
   another vessel (the ordinary explosion): both clouds travel the same way, along the hit. (Round 9
   retired the squint — a detail level no longer has to read as the explosion — so size and fade may
   differ; round 10's lab measures the lean along the hit, `Docs/Studios/COLLISION_BURST_LAB.md`.)
8. **The ladder.** Dense Stoat field: past 64 bodies collisions explode plainly (level 2), past 256 the
   contact goes to spheres (level 3); coming back down, level 2 returns at 192 and level 1 at 48. Fill
   the burst budget (48 live) with many level-1 collisions at once: the overflow explodes plainly, never
   vanishes.
