# Prism Motion — the fundamental, and its collision rule

Garrett, 2026-10-14: *"the rhino and stoat have introduced prism movement. prism movement should be a
fundamental with consistent expectations. one of them should be that when a moving prism collides with
another prism both prism should be destroyed."*

The fundamental's entry (canonical term **Prism movement**) is in
`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` § "The fundamentals". This file is the design and the
verification record. The code is one static class, `PrismMotion`
(`Assets/_Scripts/Controller/Prisms/PrismMotion.cs`).

## 1. What a moving prism is

A prism is **moving** when a FORCE displaces it from where it rests. Two forces do today:

| Mover | What moves the prism | Shape of the move | Path |
|---|---|---|---|
| The Rhino's hull plough (`RhinoPloughPrismEffectSO`, `RHINO_PLOUGH.md`) | a hull ram shoves it sideways out of the furrow | ONE step, made the frame of the ram (the visual slides on the prism clock afterwards) | one-shot: `PrismMotion.TrySweep` |
| Every black hole's gravity field (`BlackHoleGravityField`, `BLACK_HOLE.md` §3) — the Stoat's dipole, the drift pair, the crystal wormhole | the holes' pull, integrated every frame | one step per frame, per body (up to 6,000 bodies) | batch: `PrismMotion.ContactJob` (Burst) |

**Not a moving prism**: one POSED by its owner every frame — a creature's body (`HealthPrism`, the
fauna movers), a builder setting a stone (`BuilderPrismWorld`), a growing plant (`AssembledFlora`), an
assembler bonding a wall (`WallAssembler`, `GyroidAssembler`), a projectile carrying its payload
(`FullAutoBlockShootActionExecutor`). Those prisms are the shape of their owner, not mass a force
displaced, so they do not run the rule. They are valid TARGETS of it.

## 2. The expectations

1. **A moving prism that runs into another prism destroys BOTH.** Through `Prism.Damage(devastate:
   true)` (`PrismMotion.Annihilate`): an ordinary shield is not an answer, the debris leaves along the
   closing velocity (restitution 1/3, ceiling 200 u/s — the ram's numbers, so a prism thrown into a
   prism comes apart like one a hull hit at that speed), and both deaths go through the ordinary
   destruction path (stats, cell sums, the index).
2. **"Runs into" = the contact spheres go from apart to touching over the step**
   (`PrismMotion.EntersContact`). Both prisms are taken as moving linearly over the step (a static
   target is a zero step), so two bodies falling side by side in lockstep never meet, and a pair
   already touching when the step began is **at rest**, not a collision — a trail's neighbours are
   not annihilated by being pulled together. The test is symmetric.
3. **A contact sphere is the sphere of the prism's own volume** (`PrismMotion.ContactRadius`), the
   quantity the cells already weigh — capped at `MaxContactRadius` (6 u, ~900 u³) so the broad phase
   stays bounded. One definition for both paths.
4. **Super-shielded mass is the one survivor.** It is indestructible: `Prism.Damage`'s own gate makes
   it deflect, and only the mover is destroyed. No force moves it in the first place (the plough
   jiggles it, the gravity field never admits it).
5. **Scored to the force behind the motion**: the Rhino's pilot for a plough throw; the hole's owner
   (`BlackHole.CaptureOwnerName` / `CaptureDomain`) for a body, read off the hole nearest the body,
   or the anonymous `"Black Hole"` in `Domains.Blue` for an ownerless hole — exactly what that hole's
   capture is scored to. This keeps the rule inside **Mass is conserved**: the collision is the act
   of a vessel or a field, never a passive decay.
6. **The move is final the frame it is made** (the movers contract, `PRISM_ANIMATION.md` §3.6): the
   transform, the collider, the spatial index and the shell agree, so a moving prism is always tested
   where it actually is, and later movers in the same frame see it there.

## 3. How each path keeps it

**One-shot (`TrySweep`).** Before the plough commits a shove from `from` to `to`, it asks for the
first prism the step runs into: `PrismSpatialIndex.QuerySegment` (real prisms) and
`QuerySegmentVirtualIds` (data-driven swarm members) gather centres within `rSelf + MaxContactRadius`
of the path, each is refined with `EntersContact` against its stored position, and the earliest wins
(a virtual member is materialised only if it is the winner). A hit stops the prism at the contact
point and annihilates the pair; a clear path completes the shove and its slide.

**Batch (`ContactJob`).** After `IntegrateJob`, a fourth Burst job runs one body per index: it walks
the index's 8 u buckets around its step (reach `rSelf + MaxContactRadius`), reads every live entry's
stored position and live volume (`PrismSpatialIndex.TryGetMotionView` — `_spatial`, `_cellData`,
`_buckets`, read-only), and for an entry that is itself a body (`MapSlotsJob`'s slot → body map) uses
that body's own step instead. It emits each body's earliest contact (≤ 1 per body, so the hit list
never overflows). It completes before the batch index write, because the stored positions it reads
ARE every body's pre-step positions. On the main thread `ApplyContacts` annihilates each pair before
`ApplyVerdicts`, which then drops every destroyed body from the ledger — so a collision outranks a
capture or a throat transit on the same frame. A pair both bodies reported is annihilated once
(`Damage` is idempotent on a destroyed prism).

## 4. Stated limits

- **Spheres, not boxes.** A long, thin prism meets the world as a sphere of its volume: it can pass
  closer along its length than its box would, and meet sooner across its width. Chosen because the
  batch path must test thousands of bodies per frame in Burst against prisms whose orientation the
  index does not carry, and both paths must answer the same question. Exact oriented-box contact
  (`ShieldShellMath.BoxOverlapsBox`) is the upgrade if play shows the difference.
- **A throat transit is a teleport, not a sweep** (`ApplyVerdicts` moves the body source-side after
  the contact pass) — it does not test the path it skips, as nothing physical crosses it.
- **Per peer.** Both movers already run on every peer from local state (the plough as the ram did,
  the gravity field as the wormhole toll does), so two peers can disagree on a collision at the edge
  of a contact — the same stance as those systems' own verdicts.
- **Cost (batch).** Up to 6,000 bodies × the buckets their reach spans (8–27 at 8 u buckets), in
  parallel. Not yet profiled in the editor — see §6.

## 5. Files

| File | Role |
|---|---|
| `_Scripts/Controller/Prisms/PrismMotion.cs` | The rule: `ContactRadius`, `EntersContact`, `Annihilate`, `TrySweep`, `ContactJob`, `MapSlotsJob` |
| `_Scripts/Controller/Managers/PrismSpatialIndex.cs` | `StoredPosition`, `ContactVolume`, `TryGetMotionView` (region "Prism motion") |
| `_Scripts/Controller/Environment/BlackHole/BlackHoleGravityField.cs` | `PrevPoints` out of the integration, `ScheduleContacts`, `ApplyContacts`, `CollisionsTotal` |
| `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs` | `TrySweep` before the shove |
| `_Scripts/Tests/Editor/PrismMotionTests.cs` | The contact sphere, first touch, stop-short, pass-wide, at-rest, separating, two movers, lockstep, symmetry |

## 6. In-editor verification

No `unity` binary was available in the authoring session. Both configs of the headless reference
compile (`Tools/Build/unity_refcompile`) are clean; nothing has been seen in play.

1. **Plough into a wall.** Freestyle (Menu lava lamp). Lay a trail, then ram a second trail that runs
   parallel to it a few units to its side, so the plough throws prisms toward the first. Each thrown
   prism that reaches the first trail must stop there and BOTH must explode; one thrown into open
   space slides out and survives, as before.
2. **Plough into super-shield.** Same, with a super-shielded wall as the target: the wall jiggles and
   survives, the thrown prism explodes.
3. **Dipole into a field.** Stoat in a dense prism field: lay a pair. Prisms falling in from different
   directions that meet must explode in pairs on the way in; a trail falling in as a ribbon must NOT
   chain-explode from its own neighbours (they start touching = at rest).
4. **Scoring.** In an arcade mode with a destroyed-prism score, both kinds of collision count for the
   Rhino's / the Stoat's pilot.
5. **Cost.** `prof` with a Stoat pair over a full body budget (~6,000): the `ContactJob` must be a small
   fraction of `IntegrateJob` in the Timeline. If it is not, lower `MaxContactRadius` first.
