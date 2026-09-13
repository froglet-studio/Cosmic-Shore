# Skein — the Urchin cable race (`GameModes.Skein = 51`)

**Urchin-only. First DOMAIN whose LEAD RUNNER threads the last ring wins.**

A trefoil-knot cable hangs in the cell, wrapped in two shells of rails: an **inner shell at 55u**
that is nearly straight and therefore fast, and an **outer shell at 120u** that corkscrews and
therefore is not. **5 to 9 rails** by intensity, each cut into open segments whose ends are
**aimed** — run a rail off its tip and its own tangent throws you onto a live rail somewhere
else. Twenty-four ordered rings are threaded in sequence: two are wide collars that swallow the
whole cable (the start and the finish, so nobody wins or loses for the lane they were in) and the
other twenty-two sit on **one specific rail**, so the question is never *can you thread it* but
*can you be on that rail when you get there*.

> **Status: the ARENA GEOMETRY is built and proven; nothing downstream of it is written yet.**
> See § "What is and is not verified" at the bottom before building on this. It is the honest
> statement, not a formality — several of the claims this design was originally drafted against
> turned out to be wrong, and they were only caught by measuring.

---

## 1. Why this mode exists

The Urchin's verbs are **ATTACH** (the 1D rail grind, `TrailFollower`), **CONVERT** (riding a
rival's prism steals it, riding your own grows it), **LAUNCH** (running off an open ribbon's end
carries the grind speed into free flight) and the **chain-spike cascade**. `Hijack(46)` built an
arena that rewards the *steal*. This one builds an arena that rewards the *ride*.

**Riding beats flying 2.62×, and that number is the vessel's own.** Nothing here is authored to
make the rails attractive:

| | course speed | vs flying |
|---|---|---|
| flying the gate polyline | 52.6 spine-u/s | 1.00× |
| grinding the **inner** shell | **137.8** spine-u/s | **2.62×** |
| grinding the **outer** shell | 76.4 spine-u/s | 1.45× |
| crawling a rival's colour | 9.2 spine-u/s | 0.17× |

`TrailFollower.FriendlyTerrainSpeed` is 150 and `HostileTerrainSpeed` is 10 on `Urchin.prefab`;
`VesselTransformer.DefaultThrottleScaler` is 50 and the Urchin has **no boost action in its map**.
So a friendly grind is 3× the vessel's free-flight ceiling before the shell's arc-length penalty,
and a third of every lane is somebody else's colour and rides at 0.17× flying — *the only thing
slower than not riding at all*. The spike tap fired ahead of a paint boundary is the single
largest speed swing a pilot controls.

**And the launches are free travel on top.** `CarrySpeedIntoFreeFlight` hands free flight the
grind speed and `TickCarriedSpeed` bleeds it at a constant `detachSpeedDecayRate` 36 u/s² toward
cruise — **295 u/s of excess spent over 8.2 s, worth ~1,208 u of travel above cruise**. The
longest stroke in this arena is 420 u, a third of the budget.

---

## 2. The spine — a knot, not a walk

**A (2,3) torus knot**, `R = 560`, `r = 200`, closed form, no RNG:

```
C(t) = ((R + r·cos 3t)·cos 2t, (R + r·cos 3t)·sin 2t, r·sin 3t),   t ∈ [0, 2π)
```

**Three facts hold EXACTLY**, and `skein_budget.prove_spine` asserts all three rather than
trusting them:

1. **`|C'(t)| = √(9r² + 4(R + r·cos3t)²)`** — the cross terms in `|C'|²` cancel identically, so
   arc length is a clean 1-D integral. **Measured L = 8,029.9 u**, `|C'| ∈ [937.2, 1634.1]`.
2. **The minimum non-local self-distance is exactly `2r` = 400 u, at every `t`.** The two passes
   at a shared azimuth are `t` and `t+π`, where `cos3t` flips sign and `cos2t` does not, so the
   separation is `√((2r cos3t)² + (2r sin3t)²) = 2r` identically. **Measured 400.000000.**
3. **`u(t)·C'(t) ≡ 0`** for the torus outward normal — **measured max 2.4e-16**, i.e. exact to
   float precision.

**This is why the spine is a knot and not a wandering walk.** A wandering spine must *prove* it
does not pass near itself, and at this cable diameter it cannot: a tube of the required radius
around a curve this long needs ~82% packing of the available shell, which is why the wandering
candidate measured 6.4 u closest non-adjacent approach and listed the antiparallel fold as its own
fatal flaw. A pilot whose ribbon ends at a fold catches a rail going the wrong way and is flung
**backwards up the course at 300 u/s**, because `TrailFollower.Attach` seeds `Backward` from
`dot(Course, HeadingAt) < 0`. Here there is nothing to reject, no redraw budget, and **no seed
that can generate an unplayable arena.**

**Frame: the torus surface frame.** Frenet is wrong — its normal flips 180° through an inflection
and tears the braid. Parallel transport is wrong — it does not close on a loop, so every strand
kinks at the seam. The torus frame is 2π-periodic by construction, so an integer twist closes a
strand exactly. Its price is a measured **geodesic torsion of −2.7323 turns per lap**, which is
why the twist numbers below are *solved against the measurement* rather than authored.

---

## 3. The two shells

A strand is `S_k(s) = C(s) + a·(cos θ·u(s) + sin θ·v(s))`, `θ = φ_k + s/λ`.

**The twist is SOLVED, not authored** — `solve_twists` takes the largest integer twist that still
satisfies both derived bounds, because more twist is more crossings and a more legible braid:

| | a | w | λ | ψ | f = 1/cos ψ | course speed |
|---|---|---|---|---|---|---|
| inner | 55 | **10** | 127.80 | 23.29° | 1.0887 | 137.8 spine-u/s |
| outer | 120 | **18** | 71.00 | 59.39° | 1.9638 | 76.4 spine-u/s |

- **`ψ_in ≤ 25°`** — the inner shell must be the fast lane by a readable margin; above 25° the two
  shells' speeds converge and the mode's central choice evaporates.
- **`f_out ≤ 2.0`** — an outer lane must stay at least 1.4× faster than *flying* it, or the shell
  stops being a road and becomes a punishment. `150/(1.4 × 52.6) = 2.038`.

**BOTH SHELLS WIND THE SAME WAY, and that is load-bearing.** Counter-winding halves the crossing
period, which is the tempting reason to do it, and it makes the inner/outer tangent dot
`(λ_in·λ_out − a_in·a_out)`, which goes **negative** at any practical lay: a launch from one shell
onto a counter-wound strand of the other lands at more than 90° to it, `Attach` seeds `Backward`,
and the pilot is carried back up the course at grind speed. Same-handed the numerator is
**+15,674** rather than +2,474 — positive at every lay, by construction. `prove_winding` asserts
both branches so the *reason* survives the decision.

---

## 4. The flare — and what it actually does

**An outer rail's tangent aims at a live strand in 0 of 384 sampled indices.** That is not a bug;
it is the launch theorem. For a strand on a frame whose derivatives have only `T` and mutual
components, `S'` has **no radial component, exactly, everywhere** — so a pilot who grinds to a
break and does not steer flies a tangent line to the cylinder of radius `a`, and radius along that
line is `√(a² + ℓ²)`, **monotone increasing in both directions**. *Out is free; in is impossible.*

So over the **215 u** of spine either side of an outer break, the strand's radius eases to the
inner shell on a smoothstep. **`FLARE_SPINE` is DERIVED and pinned from both sides**, with about
40 u of slack:

| flare | per-prism turn | |
|---|---|---|
| 65 (the first design) | **16.81°** | 3.5× over budget |
| 150 | 5.90° | over |
| 200 | 4.52° | ok |
| **215 (shipped)** | **4.41–4.47°** | ok, budget **4.80°** |
| 300 | 86.55° | adjacent flares **overlap** |

- **lower bound** — the per-prism turn through the flare must stay inside the pilot's sustained
  budget, `TURN_RATE × PRISM_SPACING / GRIND = 90 × 8 / 150 = 4.80°`.
- **upper bound** — `2 × FLARE_SPINE < SEGMENT_SPINE`, or adjacent flares collide.

**Note what the flare actually DOES, because it is not what it looks like.** With a symmetric
smoothstep V the radial derivative is **zero at the vertex**, so the break does not "dive inward"
at all. The flare brings the outer rail **down to the inner shell**, and it then launches exactly
like an inner rail — the ordinary outward throw. That is why a flared outer break's measured
arrival angles (15–29°) match an inner break's rather than being steeper. "Out is free, in must be
bought" survives intact; what buys it is 215 u of authored descent.

**The flare is symmetric, and that is a correctness fix rather than a flourish.** A one-sided
flare leaves the radius snapping 55 → 120 the instant past the break — a 65 u discontinuity in the
underlying curve. The ride never traverses it (it is the break gap), but every measurement taken
over the strand does, and it reads as a **~91° per-prism turn that no flare length can shift**.
*That invariance under the parameter is the tell that a measured number is a discontinuity rather
than a curvature.*

---

## 5. What the model caught that the design did not

Every item here passed design review and was found only by measuring. This is the argument for
`Tools/Build/skein_budget.py` existing at all.

| finding | how it presented |
|---|---|
| Outer rails cannot aim without a flare | 0 of 384 indices — the launch theorem, not a tuning miss |
| The 65 u flare is unrideable | 16.81°/prism against a 4.80° budget |
| A one-sided flare is a discontinuity | ~91°/prism, *invariant* under flare length |
| The gate walk fell into a **cycle** | gates 11–13, 16–18 and 21–23 laid on top of each other |
| One lead-in starves the walk | 22 of 24 gates once per-seed jitter moved the breaks |
| Painting by construction is not balanced | 37.5 / 29.5 / 33.0 % → rebalanced to 33.3 ± 0.2 |
| **Three of five proof gates could not fail** | see below |

### The one that matters most

A negative control — deliberately breaking a number and confirming the proof fails **by name** —
found that **two assertions were circular**. They asserted a generator against the very tolerance
it accepts against, so loosening the trim loosened the proof with it: `END_AIM_RADIUS` 12 → 60
still printed `ALL PROOFS PASSED` over an arena full of launches that aim at nothing.

> **General rule: a proof must be stated against a constant the thing under test cannot move.
> Asserting a generator against its own tolerance is a tautology wearing a gate's clothes.**

The independent-bounds block (`MAX_LAUNCH_MISS`, `MAX_ARRIVAL_ANGLE`, `HARD_BACKWARD_ANGLE`,
`MIN_GATE_SEPARATION`, `MIN_SHELL_GAP`, `MIN_LOBE_CLEARANCE`) exists for exactly that reason. Two
further gates were simply **missing** — nothing asserted that the shells stay apart, and nothing
asserted that the cable *fits inside the knot's own self-clearance* (`prove_spine` asserts the
self-distance is `2r`, which stays true at any `r`, so at `r = 120` the theorem held and the lobes
interpenetrated). **All seven probes now fire by name.**

---

## 6. The ladder

**Intensity is how much of the race NAMES A CURVE.** The knot, the radius band, the lay, the ring
count, the segment length, the prism, the spacing and the spawn ring are identical at all four
levels, so the arena's silhouette, its hollow core, its launch geometry and its fairness argument
never move. Three things climb together, and they are one idea rather than three dials — how many
lanes there are to read, how many rings PIN the pilot to one of them, and how far apart the rings
therefore sit.

| I | N | pinned | collars | laps | ring gap | ride | prisms | volume | trails | worst turn | worst miss | next ring on screen |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 5 | 0 | 24 @ 190 u | 1 | 349 u | ~32 s | 4,361 | 1.26 M | 11 | 1.89° | 11.31 u | **100.0 %** |
| 2 | 6 | 7 | 17 @ 180 u | 2 | 698 u | ~63 s | 4,820 | 1.39 M | 18 | 1.90° | 11.35 u | 58.3 % |
| 3 | 7 | 11 | 13 @ 165 u | 4 | 1,396 u | ~127 s | 5,083 | 1.46 M | 25 | 1.90° | 11.37 u | 3.2 % |
| 4 | 9 | 22 | 2 @ 150 u | 6 | 2,095 u | ~190 s | 6,699 | 1.93 M | 32 | 1.90° | 11.87 u | 0.0 % |

Prism counts sit inside the shipped band (Hijack 2,772–9,930; Peel the Cage 10,620–20,153).
**Volume does not**, and that is deliberate — see § 8.

**A ring is one of two things.** A **PINNED** ring sits on one named strand — a seeded draw that is
never the previous pinned ring's strand — so reaching it means getting onto that curve, which means
riding to a break and taking its aimed launch. A **COLLAR** is centred on the spine and wider than
the cable, so every strand passes inside it and whichever curve the cable has put you on threads it.
`PinStride` says how often the march pins: 1 pins every ring (intensity 4, the shipped arena), 0
pins none (intensity 1).

**The spacing follows from that, which is why the two move together.** A pinned ring costs a whole
transfer — one rideable run plus the longest launch, 1,650 u — and a collar costs nothing, so the
window is measured **pin to pin, not ring to ring**. Collars in between are free, and that is
exactly what lets the low rungs pack their rings close enough to see the next one while still
leaving room for the transfers they do ask for. At intensity 4 every ring is a pin and the two
readings are the same number, so that rung's bound is unchanged.

**Intensity 1's promise is that the next ring is already on screen as you thread this one**, so the
objective arrow is decoration. It is proven, not hoped for: `prove_next_ring_visible` measures the
angle from the pilot's direction of travel to the NEAREST EDGE of the next ring's mouth against the
**32.5°** half-frame a riding pilot actually sees, and asserts 100 % at that rung. `main()` then
asserts the ORDERING — the fraction may never RISE with intensity — because what makes the two ends
a ladder rather than two settings is that nothing in between reverses.

Both ends of the strand count are derived. `N_min = 5`: below five strands the phase spread is too
coarse to cover the radius band at every station, so the cable has radial holes. `N_max = 9`: the
closest strand pair is **32.6 u** there, which must clear both the 24 u ride-envelope floor and
twice the 9 u MASS-5 shield reach; a tenth strand takes it under the armour bound and two lanes
fuse.

---

## 7. The prism — the expensive, thin-margin decision

**`prismScale = (6, 6, 8)`, spacing 8.0 u**, overriding the Track Projector's own `(3,3,6)`.

`VesselAttachPrismEffectSO` is dispatched from a PhysX trigger (`ImpactorBase.OnTriggerEnter`,
enter-only, no sweep, no shell tier for plain prisms), sampled once per `FixedUpdate` — and the
project's `Fixed Timestep` is exactly **0.04 s** with `m_AutoSyncTransforms: 0` — while
`VesselTransformer.Update → MoveShip` **teleports** via `transform.position +=`. So the vessel
jumps `speed × 0.04` between two chances to be noticed, and above some speed it steps clean over
a rail.

A trigger fires on overlap, so the catch window is the Minkowski sum of the prism's collider
cross-section and the hull's extent along travel — measured **3.46–3.53 u on a 3-wide rail** and
**6.46–6.61 u on a 6-wide**, i.e. ~0.5 u of hull either way. Consecutive prisms' envelopes touch
(8.0 u of extent against 8.0 u of spacing), so a rail is one continuous **tube** of radius
`(cross-section + 0.5)/2`, and a ray crossing it at angle θ to its axis is inside for `2R/sin θ`.

> **⚠ THE MARGIN IS NEGATIVE AT THE SHIPPED SPEEDS, AND THIS SECTION USED TO SAY OTHERWISE.**
> `(6, 6, 8)` was chosen here to clear a 6.00 u step — the step at the **150 u/s** the grind ran
> at when this was written. The rail speed then doubled and the launch gained a 1.2× kick in the
> *next* round, and nothing re-derived the prism: a launch now leaves at **360 u/s**, meets its
> target rail after 504–900 u of glide (`END_AIM_MIN`/`MAX`, both authored as TIMES), and is
> still doing **255–306 u/s** when it gets there — a step of **10.2–12.2 u** against a chord of
> **7.5 u at the 60° arrival cap** and **8.6 u at the median 48.5° arrival**.
>
> At most one sample can land inside a chord shorter than the step, and whether it does is a
> phase coin toss, so `P(latch) = min(1, chord/step)` is exact. Measured per intensity by
> `skein_budget.py`'s `measure_attach_latch`: **86.6% / 85.0% / 81.4% / 82.1%** — so roughly
> **one aimed launch in six slips past the rail it was aimed at** and the pilot flies on, with
> nothing logged. It is survivable (the pilot is still gliding, still pointed at the cable, and
> catches it on a later pass as the carry decays) and it is *the mode's signature move landing
> five times in six*.
>
> **It cannot be bought off with a fatter prism**, which is why the number is REPORTED rather
> than gated: closing it needs a cross-section near **8**, and `prove_shield_clearance` asserts
> the ceiling — at MASS 5 a rail's armour reaches `1.5 × leafSize`, so an 8-wide rail reaches
> 12 u under armour against the 24 u closest strand pair, i.e. two lanes' armour exactly
> touching and "which rail am I on" losing its answer. The measured trade, if it is ever wanted:
> `(7,7,8)` buys **86.9%** for 1.36× the prism volume, `(8,8,8)` buys **93.5%** for 1.78× and
> spends the whole armour budget.
>
> The general rule, and the reason the number now lives in the model instead of in this
> paragraph: **a dimension chosen to clear a speed is a function of that speed, and nothing
> fails when the speed moves — the prose simply goes on describing the old vessel.**
> `prove_vessel_mirror` now re-reads `FriendlyTerrainSpeed`, `HostileTerrainSpeed`,
> `DefaultThrottleScaler`, `detachSpeedDecayRate` and `endLaunchSpeedKick` out of
> `Urchin.prefab` and `GunVesselTransformer.cs` on every run, so the model can no longer assume
> a vessel the project does not ship.

> **The cheap structural fix already exists and this mode does not take it.**
> `ImpactorBase.AcceptImpacteeFromSweep` is the platform's swept-dispatch seam, complete with the
> `IsSweepDispatch` flag that stops the trigger path double-firing. Its only user today is
> `Projectile` (`sweptVesselDetection` / `sweptPrismDetection`), added for *exactly* this failure —
> CLAUDE.md records it as *"a fixed-timestep trigger is a SAMPLE, not a test, and a fast enough
> projectile is invisible to it."* A **vessel-side twin** (a capsule OVERLAP over the Update
> segment, never a cast — a cast ignores colliders it starts inside, which is precisely the
> hull-is-already-on-the-rail case) is the right long-term answer, would let this arena go back to
> `(3,3,6)` at a fifth of the volume, and would fix Hijack's re-attach at the same time. It is a
> fleet-wide platform change and is recorded here as a follow-up, not taken.

---

## 8. Collider budget

**Every prism is `PrismKind.Plain` — zero always-on mesh colliders are authored.** The active
count is bounded by `PrismColliderLodManager`'s 200 u radius rather than by the population. Note
that this stays true even where mass becomes shielded: a shield swaps the mesh and the mass, never
the collider (verified, and audited).

**Armour is not free GEOMETRICALLY, though, and that is a gate rather than a note.** A shield
engages the circumscribing octahedron at `CIRCUMSCRIBING_SCALE = 3` on the box *half*-extents —
reaching **1.5 × leafSize**, i.e. **9 u laterally** on a `(6,6,8)` prism. Any rail here can come up
armoured mid-match, because a MASS-5 pilot shields their own colour by riding it. If two lanes were
closer than twice that reach their armour would meet and the two rails would become one mass the
ride cannot tell apart — which would silently destroy the address system the whole mode rests on,
since a ring is centred on ONE rail and *"which rail am I on"* has to have an answer. Measured:
tightest same-shell separation **64.7 u** at N=9 against **18 u** of combined reach — **46.7 u
clear** — and the shell gap 65 u likewise. `prove_shield_clearance` asserts all three (lane-to-lane,
shell-to-shell, and that a gate mouth clears its own rail's armour), and fires when broken.
Consecutive armoured prisms *along* a rail do interpenetrate; that is one rail, and it is fine.

**Volume is 1.55–3.07 M**, against Drumfire's 1.37 M and Hijack's 0.15–0.54 M. That is the direct
price of the tunnelling-safe prism: `(6,6,8)` is **288 per prism against `(3,3,6)`'s 54 — 5.33×,
and 18× the nominal 16**. CLAUDE.md's locked rule therefore applies with full force:

> **a cell whose prisms are not nominal must author its volume ladder, never inherit the
> `count × 16` derivation.**

The inherited derivation would be **~17× too low** and would pin the cell at Frenzy from frame
one. `PhaseThresholds` must be authored per intensity from `CellEnvironmentBaselineMeasurer` and
the offline model. Nothing in this mode reads phase (no flora, no fauna), so the only consequence
of getting it wrong is collider-LOD-by-phase — which is precisely the system this arena leans on
hardest.

---

## 9. What is deliberately NOT in this mode

**NO FOOD WEB.** In a nucleus-less cell herbivores eat *opposing-domain* mass, and the leader's
colour is by definition the most abundant as they convert lanes under themselves — so a swarm
would preferentially graze whatever the **trailing** team had just painted. That is an
anti-comeback current in a mode whose entire tempo is contested ownership. There is a second
reason Hijack does not have: fauna are client-local (`CellNetworkSync` — every peer runs its own
spawner off local `Random` rolls), so a food web would chew the **track** differently on every
machine, and a race whose track differs per peer is unacceptable in a way a heist's is not.

**NO NUCLEUS.** Nothing here reads `DominantDomain`, every prism is meant to change hands, and a
nucleus would drop a fauna sanctuary and the crystal-respawn volume into the knot's hollow core —
which is a lane. `CrystalManager.noNucleusSpawnRadius` must therefore be authored, or every omni
crystal falls through to its own `SphereRadius` and spawns on the arena's exact centre (Dog
Fight's recorded defect).

**NO 2D SURFACE OR VOLUME RIDE.** Every segment declares `PrismscapeDimension.Trail`, so
`BlockscapeFollower` never engages and the marble roll never fires. This is the deliberate
division of labour with Hijack: that mode's burrs are `Volume` and it is the mode that rewards the
roll; this one rewards the grind. A solid here would give the pilot somewhere to *stop*, and a
race with a pond in it stops being a race.

**NO PER-LAUNCH BONUS AND NO AIRTIME MULTIPLIER.** The launch pays through geometry (the trim aims
it at a live rail), physics (it carries 833 u above cruise) and economy (only riding banks spike
ammo). Scoring the *record* of a manoeuvre rather than its effect is the scripted-outcome cheat.

**NO NEW SCORING METRIC.** Metric 9 (`SwitchesThreaded`) is reused wholesale, and with it
`ScoreDifferenceSource.SwitchesThreaded` (8), `IRoundStats.SwitchesThreaded`,
`Player.ReportSwitchThreaded_ServerRpc`, `SwitchThreadScoring.Credit` and the `BestByDomain` fold.
The mode claims **one** new enum member in the whole project.

**NO `Domains.Blue` IN THE MASS.** A two-domain lobby finds the third colour's third of every
strand hostile to both sides and evenly distributed — symmetric unclaimed loot. Blue stays
reserved for the neutral gate rings.

---

## 10. What is and is not verified

**Built and proven offline** (`python3 Tools/Build/skein_budget.py`, exit 0, all seven negative
controls fire by name):
the spine's three exact identities · the shells' derived twist · the flare's two-sided bound ·
the trim's four acceptance conditions · the gate walk's separation · paint balance to ±0.2% ·
per-prism turn · gap ratio · coincident-prism exclusion · nesting · lobe clearance.

**Verified against the shipped code by adversarial reading** (not by running the editor):
`Trail.Project` rides a **uniform Catmull-Rom** through block centres — so curvature does *not*
bound a rideable rail, and the real per-prism constraint is the pilot's turn rate; the corrected
gap rule is **5×** interior and **6.79×** one-sided at a rail end (the plan's 7× was one-sided
algebra applied to the interior case); coincident prisms cause the walk to **HANG**, not to
produce a NaN, which is worse and is why the generator asserts a minimum gap; and the tunnelling
measurement in § 7.

**Settled by adversarial verification against the shipped code** — seven claims, each read by an
independent verifier required to quote `file:line`, then each verdict handed to a **second reader
told to refute it**. All seven verdicts survived the audit; two claims were REFUTED and four
corrected. Where a verdict below says REFUTED, it means *the claim was wrong and the code says
otherwise*.

| claim | verdict | what it changed |
|---|---|---|
| Attach is a fixed-step trigger; `(3,3,6)` tunnels | **CONFIRMED** | **41.2% of perpendicular re-attaches miss** at ≥25 fps, 52.9% at 20 fps. `(6,6,8)` clears it by 8–10% and only above 25 fps; below that the cross-section would need ≥ 7.1. Kept. |
| `Trail.Project` rides a uniform Catmull-Rom | **PARTIALLY** | Curvature does **not** bound a rideable rail. The gap rule is **5×** interior / **6.79×** one-sided at a rail end, not the 7× the design carried. The model asserts 4:1, inside both. |
| A shielded prism costs an always-on collider | **REFUTED** | **CLAUDE.md was right and the survey was wrong.** A shield swaps the mesh and the mass, never the collider, and shielded prisms stay LOD-reclaimable — so shielded mass costs **zero** always-on colliders. The audit closed both of the verifier's residual uncertainties in its favour (no `MeshCollider` exists in any scene; the one suspicious prefab is referenced by nothing and carries neither shield component). What is **not** free is the GEOMETRY — see the shield-clearance gate below. |
| The Slip ghost is inert | **REFUTED** | The ghost works, and the 65 u shell gap is **conservative**: carried speed decays at only 36 u/s² from a 360 u/s launch, so 0.6 s of ghost covers ~210 u. 65 books a 3.2x margin. Do not raise it. |
| A launch leaves along the nose, not the rail | **PARTIALLY** | **The aim is bounded by the pilot's ATTITUDE error, not by the rail.** Lateral miss = `d·sin(nose error)` — 68 u over 200 u at 20°. So *"grind to the end and don't steer"* is a claim about a pilot who is actually pointing down the rail, and the 12 u trim tolerance is the geometry's contribution only. |
| `AIPilot`'s orbit break fires while attached | **PARTIALLY** | **Do NOT ship the one-clause `IsAttached` exemption** — it fixes the less likely of two failures. Fix it **mode-side**, as Hijack already does: hand `SetExternalTargetProvider` a lead point *ahead on the vessel's own strand*. The range then falls every frame (which resets `OrbitDetector` before it can sweep 540°) **and** the bearing stays near the tangent (so `LookingAtCrystal` holds and `ram: 1` keeps `XDiff` at 1). **One mode-side closure buys both — no platform change.** |
| `Attach` seeds `Backward` past 90° | **PARTIALLY** | The flip threshold is **asymmetric** — Forward→Backward at 110.49°, Backward→Forward at 69.51°, a 40.97° dead band — and a wrong-way attach costs **164 u** to recover from a cruise fly-in, **184 u** from a grind-speed transfer. Adds an **authoring rule the design was missing**: each rail's prisms must be laid in index order **along the race direction**, or `Backward` has no relation to "wrong way" and the whole hazard analysis is undefined. And the 60° cap is margin against the **coin flip** — at 90° `Dot(Course, heading) ≈ 0` and its sign is float noise — not against a clean threshold. |

**Still NOT verified, and load-bearing:**
- **The AI Urchin end to end.** The orbit-break verdict removes the platform change but not the
  risk: `ram: 1` still fires only while `LookingAtCrystal`, and the mode-side lead point is the
  thing that has to hold it. Untested in the editor. Ship it wrong and there is no AI backfill,
  which for a 2–4 player party mode means no solo mode.
- **Everything about how it plays.** Match length, readability of a two-shell braid at speed, and
  whether 22 strand-gates over ~2 laps is the right course length are all unmeasured.

**Written and machine-verified:** `SkeinCourse.cs` — the generator, transcribed from the model and
**compiled and RUN outside Unity** against stubbed `Vector3`/`Mathf` (Mono `mcs`). Compiling it
caught two real defects that reading did not: target-typed `new(...)` (legal in Unity's C# 9,
unparseable here) and — the one that mattered — **`seed * 2654435761` silently overflowing `int`**,
which is a *determinism* bug rather than a compile nicety, since the offline model computes the
same product in arbitrary precision and masks to 32 bits. Both sides now wrap identically.

Measured over 60 seeds × 4 intensities: **231/240 lay a full 24-ring course first try**, and all 9
that did not **recovered inside 4 re-rolls**. `TryGenerate` returning false is a designed path; the
controller must re-roll rather than ship a short course, because a target naming a ring that does
not exist is a match that cannot end.

**The mode is wired end to end and appears in the arcade.** `SkeinController` (reads the cable's
own rings rather than deriving a second course, broadcasts the geometry, runs the one-segment-test
detection loop with the optimistic/resync reconcile), `SpawnableSkein` (one OPEN Trail per rail,
laid sequentially inside one arena-build bracket, prisms in index order ALONG the race direction),
`SkeinScoringRuleSO` (inherits Switchback's fold wholesale), `SkeinRingTurnMonitor`,
`SkeinObjectiveProvider` + its `MiniGameHUD` case, the `EndConditionOverridesSO` key (8 sites), and
`Tools/Build/author_skein_assets.py` — which authors the four cell configs, the four spawnable
prefab variants, the spawn profile, the scoring rule, the arcade card, the scene, and the three
registration edits (build settings, the live `OrganicRematchGames` roster, and
`ProgressionConfig.alwaysUnlockedModes` — without that last one the card renders, reports
interactable, passes a raycast and **opens nothing**).

The generator is compiled and run outside Unity; the authoring script is `--check` clean and
idempotent (34/34 unchanged on a second run); 7,402 project guids scanned with zero collisions.

**One expected first-open diff:** the cloned scene carries the DONOR's in-scene
`GlobalObjectIdHash` values. Unity recomputes them the first time `MinigameSkein.unity` is opened
and saved — commit that diff. It is harmless meanwhile (two game scenes are never loaded at once,
and NGO indexes in-scene objects by `(hash, sceneHandle)`), but `MinigameSwitchback` carries
distinct values only because a human opened it, and Hijack shipped with PeelTheCage's.

**The AI is wired, mode-side, with no platform change.** `SkeinController.ArmRacers` installs one
`SetExternalTargetProvider` closure per bot. While ATTACHED it aims a lead point down the pilot's
OWN rail, and that single choice is what the orbit-break verdict called for: the range then falls
every frame (which resets `OrbitDetector` before it can sweep past its threshold — an outer strand
turns thousands of degrees per lap, where Hijack's 20° arcs never could) **and** the bearing stays
near the tangent (which holds `LookingAtCrystal`, so the authored `ram: 1` keeps `XDiff` at 1 and
the grind at 300 rather than collapsing to 60 u/s). Off-rail it flies at its own next ring and
*through* it, because `AIPilot` has no arrive-and-stop behaviour. The provider is cleared at
teardown — Switchback ships without that and leaks its closure across a scene-reload replay.

**Still unverified in the editor:** whether that AI actually races, and everything about how the
mode plays. Nothing here has been run in Unity.

### 10.1 What the editor caught that nothing here could — and the two gates that answer it

Two compile errors reached the editor on this branch, one per round trip:

| error | file | cause |
|---|---|---|
| `CS0246: 'GameDataSO' could not be found` | `SkeinScoringRuleSO.cs` | missing `using CosmicShore.Utility;` (`SpawnableSkein.cs` had the same defect for `CSDebug`) |
| `CS0165: use of unassigned local variable 'seed'` | `SpawnableSkein.cs(96)` | `int seed = cableSeed != 0 ? cableSeed : (seed != 0 ? seed : DefaultSeed);` — the local **shadows** the inherited `SpawnableBase.seed`, so the inner name binds to the local under construction |

Both are trivial for a real compiler and **structurally invisible to every check this repo can run
offline**: there are no Unity managed assemblies here, and Roslyn abandons class-body binding when
the base type lives in the `Assembly-CSharp` monolith (`Docs/ASSEMBLY_SPLIT.md`), so a `mcs`/dotnet
pass over these files reports nothing about their bodies. `SkeinCourse.cs` compiles and *runs*
outside Unity only because it is pure math with no Unity base type — the other five files cannot
follow it.

The second one is the more interesting failure. **CS0165 is the lucky half of that bug**: the
compiler complains only because the local happens to be unassigned at that point. Had the
initializer been well-defined, C# would have said *nothing at all* and the code would silently
have read the local's default instead of the serialized field it was reaching for. The fix routes
it through the `CableSeed` property, whose own doc comment already argued against exactly this —
*"two derivations of one course is exactly the drift this avoids."* One expression, one place.

Both classes are detectable **on syntax alone**, which is the whole point:

- `Tools/Build/check_using_directives.py` — an unqualified first-party type with no `using` that
  can reach it. Scoped to changed files: it must guess whether an unqualified name is first-party,
  and project-wide that guess produces 143 false positives (`Key`, `Direction`, `Frame`, `Stats`
  also exist in UnityEngine and NUnit).
- `Tools/Build/check_self_referential_locals.py` — a local declarator naming itself in its own
  initializer. Needs no type resolution at all, so **`--all` is clean over the whole tree** (0
  findings across 1,849 files) and it is safe in CI.

Writing the second gate is itself the cautionary tale. A first cut produced **~100 findings, every
one false**, in three shapes: an optional parameter default (`void F(int n = 1)`, whose
"initializer" runs on into the method body), a keyword in the type position (`else offset = offset
/ d;` is a re-assignment, not a declaration), and an object-initializer property (`new Foo {
options = development }` names *Foo's* property). All three are now self-test cases, alongside the
real bug as a negative control. **A gate nobody has watched fail is a gate nobody should trust —
and a gate that cries wolf a hundred times is one nobody reads.**

The general rule, extending CLAUDE.md's *"a check that cannot resolve a type cannot see errors
about that type"*: **the defect classes that survive an unresolvable compile are exactly the ones
decidable on syntax, so gate those and say plainly that the rest needs the editor.** What remains
editor-only here is every error that needs a symbol table — a member that does not exist, an
override whose signature drifted, an argument type mismatch. Those were checked by hand against
their declarations for all five gameplay files (every override, every interface implementation,
every cross-system call); that is a hand check, not a gate, and it does not scale past this branch.


## 11. The breathing cable — what the first playtest changed

Three things came back from flying it, and the second and third are one design.

**The rings and the objective arrow never appeared, on any peer, in any match.**
`SkeinController` polled `FindAnyObjectByType<SpawnableSkein>` and it returned null forever —
because `SpawnableBase.Spawn()` **does not instantiate itself**. It `new GameObject(name)`s a
plain container and lays prisms into it, so the `SpawnableSkein` that ran the generation is the
PREFAB ASSET and no such component ever exists in the scene. The cable built (it is prisms), and
everything that depended on finding the component silently did not: no rings, no arrow, then a
30 s timeout and an error nobody was watching for. The arena now resolves the way `Cell` resolves
its own garden — `cell.Config.EnvironmentPrefab is SpawnableSkein` — off a serialized `arenaCell`
whose scene reference already existed. That also fixed a second latent bug in the same lines: the
world origin is `cell.transform.position`, because `Cell` parents the container at
`localPosition = zero`; the prefab's own transform is an asset and never moves.

> **General rule.** A `SpawnableBase` is a GENERATOR, not a scene object. Anything that needs its
> generated data reads it off the **config**, never off a scene search — and its output is
> positioned by the **Cell**, not by the prefab's transform.

**Launches were landing 60 u away — 0.40 s of free flight — so a launch read as shooting straight
into the next segment.** Two causes, both fixed: `END_AIM_MIN` was an authored 60 u, and the trim
*preferred the nearest* qualifying landing, so the generator systematically produced the shortest
legal hop. The floor is now DERIVED as a time (`LAUNCH_DECISION_SECONDS × GRIND_FRIENDLY` =
1.4 × 150 = 210 u) and the trim takes the **furthest** landing inside the window. Measured across
all four intensities the shortest gap is now **210–218 u (1.40–1.45 s)** and the longest 418–420 u
(2.79 s), comfortably inside the 833 u the vessel glides above cruise.

**The two shells are retired, and so is the flare.** Each strand now rides a radius that
oscillates with its own phase:

```
a_k(s) = A_MID + A_SWING · sin(2π·RADIAL_CYCLES·s/L + φ_k)      = 90 + 45·sin(2π·3·s/L + φ_k)
```

The two-shell cable answered *"which lane am I on"* with a property of the LANE, so the answer
never changed while you rode it. A breathing radius makes it a property of **when**: every strand
spends part of the lap as the direct inner path and part spiralling out, and the phases are spread
(`φ_k = 2πk/N`) so at every station the N radii still sample the whole band — the full radial
coverage the shells bought, kept. Riding the inward phase is **1.315× shorter** than the outward
one, which is the reason to change strands; the 1.4 s launch window is the time to do it in.

**The theorem that makes it safe: `φ_k` is the radial phase AND the angular phase.** With one
shared twist the `s/λ` term cancels between any two strands, so their angular separation
`D = φ_k − φ_j` is **constant in s** — and the radial phase separation is that same `D`, so the
pair of radii traces one ellipse rather than roaming the whole box. Separation is then the law of
cosines in ONE variable, `d(ψ)² = a_j² + a_k² − 2a_j a_k cos D`, with nothing about the spine in
it — which is why the bound holds at every station of every seed, proven once rather than
re-measured per course. Give the radius an independent phase and all of that is gone.

Measured: closest pair **63.0 / 51.0 / 42.9 / 32.6 u** at N = 5/6/7/9, against a 24 u ride-envelope
floor and an 18 u MASS-5 armour-fusing bound.

**That immediately caught a real defect the old cable hid: the 40 u gate mouth was WIDER than the
32.6 u strand separation at N=9**, so a ring was threadable by a pilot riding the neighbouring
strand — which destroys the one-rail addressing the whole ordered-gate contract rests on. The
mouth is now DERIVED (`min(40, 0.85 × closest pair)` → 40 / 40 / 36.5 / 27.7), so a denser cable
wears smaller rings automatically. It costs no new plumbing: `SkeinGate` already replicates its own
`Radius`, and the ring is drawn at that radius by the switch law.

Retiring the flare also bought a large margin back: the worst per-prism turn fell from **4.49° to
1.90°** against the pilot's 4.80° budget, because the flare was the tightest curvature in the
arena. Prism counts fell with it (10,652 → 9,023 at I4), and the cell ladders are re-authored from
the model.

### The negative controls are RUNNABLE now

They were run by hand against the two-shell cable and recorded in comments — and the rewrite
retired half the constants they perturbed, so they were stale prose. `skein_budget.py --controls`
breaks one thing at a time and requires **the named proof** to object:

| control | must object |
|---|---|
| `A_MID 90 → 55` (band [10,100], lobes still clear) | `prove_strand_separation` |
| `MOUTH_SEPARATION_FRACTION 0.85 → 1.4` | the wrong-lane gate assertion |
| `LAUNCH_DECISION_SECONDS 1.4 → 4.0` | the gate walk starves |
| `r 200 → 120` | `prove_cable_fits` |
| `RADIAL_CYCLES 3 → 3.5` | `prove_strand_closes` |
| `PRISM_SCALE 6 → 40` | `prove_shield_clearance` |

Two things it caught about itself, both worth keeping. The first cut raised `A_SWING` to 80, which
breaks the cable's **lobe** clearance before it breaks strand separation — so the control passed
while proving nothing about the theorem it named. And once each control had to name its proof, the
runner was found to run the proofs in a **different order than `main`**, attributing a collapsed
cable to shield clearance instead of separation. *A control that accepts "something objected"
cannot tell a load-bearing proof from a redundant one, and a control suite that reorders the proofs
tests a sequence the build never executes.*

`prove_strand_closes` is new and fills a gap the old file had: closure at `s = L` was documented
from the start and never asserted. A non-integer in either periodic term leaves a step
discontinuity that reads as a ~90° per-prism turn — a rideability failure rather than the closure
bug it is, which is the same misdiagnosis the one-sided flare cost.

### Parity with the C#, stated honestly

The C# generator compiles and runs outside Unity (Mono `mcs -langversion:latest`) and produces 24
gates, balanced paint and ≥200 u gate separation at all 16 (intensity × seed) combinations tested.
It is **not bit-identical to the model**: rails match in 13 of 16, and prism counts run 0.5–0.8%
higher because the chord-accumulating sampler takes ~16,000 steps in `float` where Python uses
`double`. That is expected and does not matter, for the reason SKEIN.md already records — **the
course TRAVELS, the seed does not**. The server broadcasts the geometry, so no client ever
re-derives it. What must agree is the CONTRACT, and it does; the one value that is a closed form
rather than an accumulation — the derived gate mouth — agrees exactly (40.00 / 40.00 / 36.48 /
27.74 on both sides).

**Still unverified in the editor:** everything about how it now plays. The bug fix means the rings
and the arrow should appear for the first time, so the mode has effectively not been play-tested
at all yet.


## 12. The merge that moved the ground, and the second playtest pass

Bleeding-edge landed 84 commits under this branch, and three of them changed what Skein *is*.

**`Skein = 48` collided with `Tollway = 48`.** Upstream shipped Tollway at 48 and Headlong at 49
while this branch was in flight — the exact parallel-branch enum collision `DRUMFIRE.md` records,
where git merges two additions cleanly into a file carrying one number twice. Skein moved to
**50**, and the member-count tripwire went 47 → 48 (IDs 0..50, with 7, 31 and **47** reserved —
upstream retired Drumfire). The enum is proven duplicate-free rather than eyeballed.

**The gate-race platform was extracted, and Skein was 400 lines of it.** `GateRaceController` now
owns the broadcast, the rings, the crossing detection, the optimistic report/reconcile round trip
and the final scores; `SwitchbackGateRing` → `RaceGateRing`, `SwitchbackGateTurnMonitor` →
`RaceGateTurnMonitor`, `SwitchbackScoringRuleSO` → `GateRaceScoringRuleSO`,
`SwitchbackObjectiveProvider` → `RaceGateObjectiveProvider`. Four of the five types Skein depended
on were gone. `SkeinController` was rebuilt as a subclass and went **490 → 131 lines**; Skein's own
turn monitor and objective provider are deleted outright, because the platform's are mode-generic.

That rebuild also fixed the arena adoption properly. The platform calls `BuildCourse` **once**, on
the server, at `OnNetworkSpawn` — which does not suit polling for a cell environment that builds
behind `InitDelayMs`. It does not have to: the cable's SETTINGS and SEED are authored fields on the
prefab asset, readable immediately, so the controller re-runs the identical deterministic
generation (same base seed, same 7919 stride, same six attempts) and lands on the same cable with
nothing to wait for and nothing to communicate.

**One platform change was needed and is the honest kind.** `GateRaceController.TryOverrideAim` is a
new virtual, consulted before the gate logic: Skein is the first gate race flown by RIDING, and
while attached the aim point is the RAIL, not the ring — aiming at a ring while riding leaves the
range roughly constant as the cable corkscrews, `OrbitDetector` trips on swept angle without
progress, and the pilot drops from 150 to 30 u/s mid-grind. Off-rail it returns false and the
platform's ordinary gate aiming (including its crystal detour) takes over, which is exactly right:
then the pilot *is* flying.

### The three playtest changes

**The gaps are 15× bigger — five missing prisms became seventy-five (40 u → 600 u).** At 40 u a
pilot sailed over the hole and re-attached to the SAME strand, so a break was a cosmetic stutter
rather than a decision. The aimed landing window (210–420 u) now sits entirely INSIDE the gap: the
transfer is reachable, the self-bridge is not.

That is **proven**, not assumed — `prove_no_self_bridge` measures every launch ray against the
pilot's own strand past the break (the trim's clearance test skips the self strand by
construction, since a ray leaves along it). Measured: a launch comes no closer than **224–292 u**
to its own strand, against a 24 u floor; at the old 40 u gap the proof fails. Writing it exposed a
bug in the proof itself, caught by the negative control: it scanned from `END_AIM_MIN` (210 u),
where any strand has already curved clear of its own tangent, so it reported "no bridge" for
*every* gap including the bridgeable one. The scan starts just past the muzzle now.

**The 600 u gap forced a coupling into the open: the rideable RUN is the authored quantity, not the
period.** At the old `SEGMENT_SPINE = 450` the holes were longer than the segments and consecutive
holes overlapped, so a strand became mostly missing (9,023 → 5,272 prisms at I4 before this was
caught). `SEGMENT_RUN = 450` is authored, `SEGMENT_SPINE = run + gap` is derived.

**The rings now MARCH down the course; the random part is which strand each sits on.** The old walk
chased BREAKS — hopping from one aimed transfer to the next, preferring those ahead of a cursor but
*falling back to those behind it* — so the ring order could run BACKWARD along the spine. A race
whose next objective is behind you is not a course. Ring *k* now sits at spine arc `k × SPACING`
with `SPACING = GATE_LAPS × L / (mid + 1)`, which closes exactly on the finish collar, so the race
is a whole number of laps (3) and the sequence IS the course the bundle follows. Which strand
carries a ring is a seeded draw that is never the previous ring's strand, so **every ring is a
strand change**.

The march cannot starve, because it asks nothing of the breaks — which is why the walk needed the
backward fallback in the first place.

`prove_gate_spacing` asserts there is time to make that change: spacing ≥ `SEGMENT_RUN +
END_AIM_MAX` = 870 u, against the shipped 1047 u. **It is worth being exact about why that is the
run and not the period**: the break gap is never ridden across — it is the reason the pilot is
flying, so it is already inside the launch. Counting it twice made the bound 600 u too pessimistic,
enough to have forced the ring count down by a third for no reason.

### The seed sweep, and why it exists

The model proved ONE seed. The C# generator's four-seed run then caught two defects the
single-seed proof structurally could not see, on the very pass that introduced them: a gate
separation of **179.9 u** against a 200 u floor (two rings threadable in one pass — the trefoil
passes close to itself, so even spacing along the SPINE is not even spacing in SPACE), and a
**2.5%** paint imbalance.

Both are fixed — separation is enforced during the march on the STRAND, because the strand is the
free variable and only a last-resort arc nudge disturbs the even march — and `skein_budget.py` now
re-runs **every** assertion across four extra seeds × four intensities. Worst gate separation over
the sweep is **206.1 u** in the model and **206.1 u** in the C#.

The paint tolerance moved 1% → **2%**, and that is a consequence rather than a loosened standard:
paint is assigned per SEGMENT, and the 15× gap took the count from ~40 short segments to ~21 long
ones, one of which can be a fifth of a strand's mass. Measured worst over 20 pairs is 1.36%; 1% is
not reachable at this granularity and asserting it would fail on seeds nobody had run.

> **General rule.** A generator that takes a seed must be PROVEN over seeds. A proof at seed 0 is a
> proof about one match, and the failures it misses are the ones a player finds.

Negative controls are up to **eight**, each naming the proof that must object. Two of them changed
meaning during this pass and the strict check caught both: the launch-window perturbation used to
surface as a starving walk (a march cannot starve, so it now surfaces at the launch floor, where it
always belonged), and the gap perturbation exposed the scan-window bug above.

**Still unverified in the editor:** everything about how it plays. The rings and the arrow have
still never been seen.

---

## 13. The first playtest: no rings, and an arrow pointing at a crystal

Both halves of that report, and they are two different bugs that happen to look like one.

### 13.1 The arrow — an enum MOVE follows references, and no serialized integer

`ArcadeGameSkein.asset` carried `Mode: 48`. The mode is **50**.

When the merge collided `Skein = 48` with upstream's `Tollway = 48`, Skein moved to 50 and every
C# reference followed it, because they are references — `GameModes.Skein`. The card's `Mode` is a
serialized **int**. It did not follow, and nothing said so:

* `check_enum_member_references.py` asks whether a NAME exists. `Mode: 48` contains no name.
* `check_switch_label_collisions.py` reads C#. The card is YAML.
* `author_skein_assets.py --check` was **green**, because the generator emitted `Mode: 48` too.
  The asset and the thing that authors the asset agreed with each other about the wrong number.

So `MiniGameHUD.CreateObjectiveProviderForGameMode` matched `case GameModes.Tollway`, and
`TollwayObjectiveProvider` falls back to the cell's crystals when no free flora heart is standing.
The Skein cell authors no flora. The arrow pointed at the crystal — correctly, for a mode this
was not.

> **General rule.** Renaming or renumbering an enum member is a compile-time event for code and a
> **silent** one for data. Every serialized integer that encoded the old value has to be swept by
> hand, and a generator that owns such an asset is a *second* place the change has to land — where
> `--check` will happily prove the two copies of the wrong number still match.

Fixed in three places that must move together: the asset, `author_skein_assets.py`'s emit, and its
`register_progression()` (which was still trying to unlock 48 — already present as Tollway, so it
would have reported "already registered" forever while Skein stayed locked).

### 13.2 The rings — `Cell.Config` is a LATCH, not a description

`SkeinController.BuildCourse` read `cell.Config` and got null, on every match.

`Cell.Config` is `runtime.Config`, and `runtime.Config` is written by `AssignConfig`, which runs
from `Cell.Initialize`, which runs on `OnInitializeGame` behind `InitDelayMs` — **1000 ms**.
`GateRaceController.OnNetworkSpawn` runs at t≈0. So the read was a full second early, every time.

This is the *second* cut of this bug with the same symptom and a different cause. The first read
the scene for a `SpawnableSkein` component that `SpawnableBase.Spawn()` never instantiates; the
fix moved to the config, which answers the *object* question correctly and the *timing* question
not at all. One never-resolves for another.

The platform already had the shape of the answer and only for one property:
`ExpectedNucleusWorldRadius` exists precisely because `NucleusWorldRadius` reads 0 during the
spawn chain. **The same split was missing for the config itself**, so every consumer that needs it
early has to invent one. `Cell.ExpectedConfig` is that twin — the config this cell has, or the one
it *will* choose:

* It **answers or declines; it never guesses.** A `Random` multi-config cell returns null rather
  than rolling, because an unlatched roll is a *different* roll from the one `AssignConfig` will
  make, and a confident wrong answer is worse than a null.
* A client that cannot yet know its intensity returns null too. That costs nothing here: the
  course is built on the SERVER and BROADCAST, so a client receives what the server derived
  instead of deriving it — the same split `CrystalManager.IntensityScaled` records.
* It is deliberately **silent**. It is a prediction, not a decision, so the misauthored-config
  warnings stay on `AssignConfig`, which is asked once.

**`ExpectedNucleusWorldRadius` was deliberately NOT routed through it.** It could be — it would
stop returning 0 for multi-config cells, which is arguably the bug it was written for — but it
would then move the spawn ring outward in the **twelve** shipped modes whose cells are
`IntensityWise` and whose scenes set `arrangeSpawnPointsAroundCell`. Its own summary states the 0
as the contract and its callers are written against it. That is a play-tested change to modes this
branch was not asked to touch; it is left as a known, stated gap.

### 13.3 The platform half: one attempt is not a slow build, it is a race that never runs again

`GateRaceController` called `BuildCourse` **exactly once**, at `OnNetworkSpawn`, and logged a
failure if it returned null. For Switchback and Headlong that is correct — their courses are pure
geometry, answerable on that frame or never. Skein is the first subclass whose course is a
property of the **scene**, and for it a single attempt turns "not ready yet" into "no rings, no
scoring, no turn end, for the whole match."

The platform now retries every frame until it works or `courseBuildTimeoutSeconds` (6 s) expires.
Three details are each load-bearing:

* **The seed is drawn once**, in `BeginCourseGeneration`, not per attempt — or the course would be
  a function of how many frames the cell took to answer, and two runs of the same build would
  differ for no reason a player could see.
* **The retry ticks above `Update`'s guards.** The course is built while the turn has *not*
  started (that is what the arena-build announcement is holding the connecting panel for), so a
  retry gated on `IsTurnRunning` would never run at all.
* **A retried method must not log.** `BuildCourse` runs every frame now, so a `LogError` inside it
  is a per-frame path. A subclass sets `CourseFailureDetail` instead, and the platform prints it
  once, if and only if the window closes.

The ordering that makes the retry safe was already there: `RaceGateTurnMonitor.StartMonitor` reads
`AuthoritativeGateCount` at TURN start, and the turn cannot start while the arena-build bracket is
open — which the controller holds until the course lands or the retry gives up.

> **General rule.** When a one-shot read of scene state fails, ask whether the caller gets a second
> chance. If it does not, the failure is not "sometimes slow", it is permanent — and it will
> present as a feature that has never once worked rather than as a race.

---

## 14. The second playtest pass: a faster Urchin, a lit start collar, and a start line

### 14.1 The vessel got faster, and this arena is authored in the pilot's TIMES

The Urchin's rail speed doubled (150 → 300) and its cruise rose 30% (50 → 65), with a **1.2×
kick** off the end of a ribbon. Every one of those is a vessel change, and all of them landed
here, because what a pilot experiences in a cable is *how long* things take.

Three constants were already written as `seconds × speed` and re-derived themselves. Three were
written as distances and each had to be found by hand:

| | was | now | why |
|---|---|---|---|
| `END_AIM_MIN` | 210 | **504** | derived — 1.4 s of decision, now at the LAUNCH speed (300 × 1.2) |
| `MIN_SEGMENT_SPINE` | 300 | **600** | derived — 2.0 s, "a rail, not a bump" |
| `END_AIM_MAX` | 420 | **900** | was a literal, now `LAUNCH_MAX_SECONDS × LAUNCH_SPEED` |
| `SEGMENT_RUN` | 450 | **750** | was a literal, now 2.5 s of riding |
| `BREAK_GAP` | 600 | **900** | was a literal, now 3.0 s of flight |
| `GATE_LAPS` | 3 | **6** | a ring's spacing must cover one run plus the longest launch |

`END_AIM_MAX` is the one that mattered: held at 420 while `END_AIM_MIN` moved out to 504, the
launch window would have **inverted** — a 1.4 s .. 2.8 s decision collapsed past a single
distance, with the generator quietly finding nothing to aim at.

> **General rule.** Anything in a generator that describes what the PILOT does is a time.
> Anything that describes what the GEOMETRY is — a clearance, a radius, a prism — is a distance.
> Write each as what it is and a vessel retune re-derives the first kind for free; write a time
> as a distance and it silently goes on describing the old vessel.

The race is the same LENGTH in seconds it always was: six laps of the spine at 300 u/s is exactly
three at 150. Prism counts land within 1% of the old arena (I4 6,699 against 6,648), because the
run/period ratio is preserved — so the phase ladder moved by a few percent rather than being
re-authored.

**`END_AIM_MAX` also has a MEASURED ceiling, and it is the one window edge that does.** A longer
ray has more chances to graze the strand it left, and `prove_no_self_bridge` puts the cliff
between 936 u and 1008 u: at 1008 a launch passes **15.7 u** from its own strand against the 24 u
floor, and the pilot can bridge the hole instead of changing strands — the mechanic, gone. 792 u
through 900 u all measure the same 74.6 u worst case, so the shipped value sits on a plateau
rather than against the cliff. A swept search also found that **widening `BREAK_GAP` does not
monotonically help**: where the strand has curved back to by the time a ray gets there is a
geometric accident, not a function of the gap. The gap is what measured best, not what the story
predicted.

### 14.2 The control harness was running against different constants than the build

Adding derived constants exposed a defect in `--controls` that had been latent: `_apply` carried
its own hand-written copy of the import-time derivations, and that copy went stale. It re-derived
`END_AIM_MIN` off the GRIND speed after the launch kick had moved it onto the LAUNCH speed, so
every negative control silently ran against a 420 u window instead of 504.

The symptom was one control firing the **wrong proof**: a perturbed gate MOUTH objected to *paint
balance*, because a different aim window changes which cuts become breaks, which changes the
segment set, which changes the paint. The suite still said "ALL FIRE" for the other seven.

There is now ONE `_derive()`, called at import and again by the harness, and a control that pins
a derived constant by hand (`BREAK_GAP = 40`) keeps its value while everything downstream
re-derives from it.

> **General rule.** A re-derivation list that duplicates the import-time derivations is a second
> place every derived constant has to be added, and nothing fails when you forget. The same
> reasoning retired the literal `52.6` (the flying reference speed, `CRUISE / CHORD_ARC`), which
> appeared in four places and would have gone on describing a 50 u/s vessel forever.

### 14.3 The start/finish collar could not go lime, because there were two of them

Reported as "the start end ring should be the call to action colour when it is the ring we are
intended to go through — the other rings were working great", which is exactly right and is not a
colour bug.

`WalkGates` opens on the spine collar at arc 0 and **closes on the same point** ("the two collars
share a point - safe by construction, since ordered gates make the finish uncrossable until its
turn"). Built naively that is two coincident 150 u rings. Lighting the start lime therefore left
its neutral twin drawn in the same place, and which one the renderer picked was a coin toss.

Fixed on the PLATFORM rather than in Skein, because it is a property of any course that visits a
point twice: `GateRaceController.FindCoincidentRing` hands a gate the earlier gate's ring, and
`RaceGateRing` draws one and forwards the other's highlight. Both gates keep their own crossing
test and their own place in the order, so the FINISH is still markable — which is the one gate a
race most needs to point at. Headlong never lands here: a lapped circuit stores one ring per
index and wraps the count.

### 14.4 Everyone starts on the collar now, not 1120 u away facing a knot

The platform's cell ring puts pilots on a great circle 1120 u out, all facing the CENTRE. In a
cable arena that means facing a trefoil rather than facing the thing they have to fly through
first. Switchback solves the same problem by putting gate 1 on its spawn ring's pole and Headlong
by rotating its whole circuit there; neither works here, because Skein's rings sit on rails the
arena builds and the two would have to rotate together.

So the pilots move instead — which is also the only arrangement that can be CLOSE as well as
fair. `CellSpawnFormation.BuildFacingRing` stands them on a 90 u ring 220 u behind the collar, all
aimed through it: every slot is the same `sqrt(220² + 90²)` from the gate, the ring's phase is
derived from the collar's own axis rather than authored, and 90 u is well inside the collar's 150 u
mouth so **everyone threads the first gate by flying straight forward**. The race begins at the
first real decision instead of at a steering test.

The delivery is the part worth reusing. A mode controller writing `SetSpawnPoses` itself is a race
it loses about as often as it wins — vessels spawn at 200 ms and AI at `OnNetworkSpawn`, and two
scene NetworkBehaviours' spawn order is undefined. The new `IPlayerSpawnLine` is asked by
`ServerPlayerVesselInitializer` at the moment it needs the poses, so there is no ordering to get
wrong; the price is that the answer must be derivable from authored data alone, before the cell
has latched a config and long before any course exists.

Skein can pay it: `SkeinCourse.StartPose` reads the spine's closed form at arc 0, and the spine is
a pure function of the knot's two radii — no seed, no intensity, no cable. Only `StrandCount`
varies per setting, and the collar is on the SPINE.

> **General rule.** When a mode needs to know something during the SPAWN CHAIN, the question to
> ask is not "how do I get there first" but "what part of this is answerable from authored data".
> If none of it is, the spawn chain is the wrong place for it.

---

## 15. The intensity ladder: "no hunting for the next ring"

The ask, verbatim: *"lets keep intensity 4 where it is at, but intensity 1 should be rings in an
easy sequence. no hunting for the next ring. the next ring should be visible from each ring as you
fly through the previous ring. so the indicator is effectively never used."*

Before this pass intensity was **only** the strand count. The gate walk was identical at all four
settings — 24 rings, six laps, every ring on a randomly drawn strand that was never the previous
ring's — so intensity 1 and intensity 4 asked the pilot for exactly the same thing and only the
lane count differed.

### 15.1 The measurement that chose the design

The first instinct was "put consecutive rings on the SAME strand at low intensity, so you just keep
riding". Measured, that is not what makes a ring findable. The off-heading angle from the pilot's
direction of travel to the next ring, over 80 stations × every strand:

| spine gap | same strand (med / max) | different strand (med / max) |
|---|---|---|
| 200 u | 15.6° / 26.7° | 50.7° / 67.4° |
| 400 u | 28.3° / 51.8° | 42.7° / 68.9° |
| 750 u | 44.0° / 84.2° | 41.3° / 84.7° |
| 1,650 u | 79.5° / 127.5° | 81.3° / 128.8° |
| 2,095 u (shipped) | 98.5° / 147.6° | 96.4° / 154.4° |

**Past ~600 u the two columns are the same number.** Which strand a ring is on stops mattering
entirely, because the knot has curved further than any lane offset. The shipped 2,095 u puts the
next ring a median **98° off the pilot's heading** — behind their shoulder. *That* is the hunting,
and it is a property of the SPACING, not of the strand draw.

Below ~400 u "same strand" does win — and it cannot be built. A strand is rideable for 750 u and
then missing for 900, so **a strand is live only 45 % of the arc**; more than half the time "the
same strand" is a hole, and the aimed launch that carries the pilot out of that hole deliberately
points at a *different* curve (`prove_no_self_bridge` exists to guarantee it). A same-strand walk
would be asking the pilot to stay somewhere the cable is actively throwing them off.

### 15.2 The collar

So the low rungs do not name a curve at all. A **collar** is a ring centred on the spine and wider
than the cable, so every strand passes inside it: whichever curve the cable has put you on threads
it. The start and finish rings were already collars — this pass makes the mid rings collars too, in
a proportion that is the ladder.

Its radius is bounded at both ends and neither bound is a taste (`prove_collar_band`):

* **floor** — wider than the cable's outward extreme (`A_MAX` = 135 u), or a pilot riding an
  outward phase flies straight past it. Nothing proved this before, because the only collars were
  the two a pilot is *placed* on.
* **ceiling** — `collar + 60 ≤ 2r − A_MAX` = **205 u**. The knot passes itself at exactly `2r` =
  400 u, the cable reaches 135 u either side, and 60 u is the lobe clearance this file already
  requires. Past that a collar starts enclosing a *different* stretch of cable, and a ring you can
  thread from somewhere else in the course is not an ordered gate.

That ceiling is load-bearing in a second way: it is what stops the ladder answering a visibility
failure by simply growing the ring forever. The honest lever is the spacing; this is the wall the
other one hits.

### 15.3 What "on screen" means, in numbers this repo already ships

`GraphicsSettingsData.DefaultFieldOfView` is 90 and `SpeedTunnelConfig.fovDrop` is 25, and the
tunnel is **saturated** during a grind (`maxEffectSpeed` 280 against the Urchin's 300 u/s rail), so
a riding pilot sees **65° vertically**. Half of that — **32.5°** — is the bound.

Vertical on purpose: at 16:9 the horizontal half-angle is 48.6°, so bounding the vertical one bounds
both however the offset happens to point. It is conservative twice over, because it also assumes the
pilot's nose lies exactly along their course when in fact they steer — a ring at 32° is one they are
already turning toward.

The reading is the angle to the **nearest edge** of the next ring's mouth, not to its centre. These
rings are not points: a 190 u collar at 340 u spans 58°, so its centre can sit outside the frame
while the pilot is flying straight into it. Scoring that as "off screen" would reject the very
geometry that makes intensity 1 easy.

Measured at intensity 1: **100 % on screen at every seed**, worst case 31.2° against the 32.5°
frame, median 8.8° from dead centre, smallest subtense 25°. The worst case is stable across seeds
(30.5–31.6°) because the collars sit on the spine and only the strand phases move — the pilot that
produces it is one riding the outward phase, corkscrewing 43.5° off the spine.

### 15.4 What it cost, stated plainly

**The race gets shorter as it gets easier: ~32 s / ~63 s / ~127 s / ~190 s of riding.** Intensity 1
is one lap of the cable. That falls straight out of the promise — 24 rings must close on the finish
collar, so the gap is `laps · L / 23`, and one lap is the tightest gap the ring count allows.

The alternative was a per-intensity ring TARGET (48–70 rings over more laps, which the geometry
would also satisfy). It was rejected on the sibling mode's own argument: Switchback keeps its gate
count constant *because the count is the end-game target, read both by the monitor and by the
controller, so the course and the number counting it cannot drift*. Buying race length with a
second per-intensity number in `EndConditionOverridesSO` is not worth reopening that.

### 15.5 The rules this pass leaves behind

> **A course's readability is a property of its SPACING against the arena's curvature, not of which
> lane the next objective is in.** Two candidate fixes that both sound like "make it easier" —
> keeping the pilot on one rail, and moving the ring to a nearer lane — measured as *the same
> number* past 600 u of a knot that turns 360° in 8,030.

> **When a rung of a ladder promises something, assert it AT THAT RUNG and assert the ORDERING
> everywhere else.** A bound every rung must pass is a bound the hardest rung sets, which is the
> opposite of a ladder. `prove_next_ring_visible` asserts 100 % only where `pin_stride = 0`;
> `prove_visibility_ladder` asserts non-increasing across the four.

> **A window that some rings need and others do not is measured between the rings that need it.**
> The transfer bound was ring-to-ring only because every ring used to be a pin. Restating it pin to
> pin changed nothing at intensity 4 and is what made the whole ladder possible.

> **A control harness truncates.** Two new controls read as WRONG PROOF OBJECTED purely because the
> phrase identifying the proof sat past the 96-character cut. The fix is in the assertion messages:
> *put the identifying phrase first*.

> **A per-intensity table is the shape that drifts** — four numbers in a row, three of them
> plausible. `prove_csharp_mirror` parses `SkeinCourse.cs`'s own `ForIntensity` arrays and asserts
> they equal the model's `LADDER`. It is deliberately **not** in the negative-control sequence: the
> controls perturb `LADDER` on purpose, so it would object to every one of them and mask the proof
> each was aiming at. What guards *it* is the assert on each regex match — a parser that silently
> finds nothing reads exactly like a clean file, which is the failure mode a mirror check actually
> has. Watched to fail three ways: a drifted number, a field that stopped being an array, and a
> missing checkout (which reports "unchecked" rather than passing).

### 15.6 Intensity 4 is byte-identical

Every number in its row — 6,699 prisms, 327.3 u minimum gate separation, the paint shares, the
launch misses — reproduces the pre-pass output exactly. `PinStride = 1` makes `k % 1 != 0` false at
every ring, so the collar branch is never taken and consumes no RNG; the seeded draw sequence, and
therefore the course, is untouched.
