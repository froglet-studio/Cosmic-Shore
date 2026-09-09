# Skein — the Urchin cable race (`GameModes.Skein = 48`)

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
grind speed and `TickCarriedSpeed` bleeds it at a constant `detachSpeedDecayRate` 12 u/s² toward
cruise — **833 u above cruise over 8.33 s**. The longest stroke in this arena is 420 u, half the
budget.

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
**backwards up the course at 150 u/s**, because `TrailFollower.Attach` seeds `Backward` from
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

**Intensity is the RAIL COUNT: N = 5 / 6 / 7 / 9.** Everything else is identical at all four
levels — the knot, the shells, the lay, the gate count, the gate mouth, the prism, the spacing,
the spawn ring — so the arena's silhouette, its hollow core, its launch geometry and its fairness
argument never move.

| I | N | in/out | prisms | volume | trails | worst turn | worst miss |
|---|---|---|---|---|---|---|---|
| 1 | 5 | 3/2 | 5,369–6,586 | 1.55 M | 56 | 4.47° | 11.96 u |
| 2 | 6 | 3/3 | 6,777 | 1.95 M | 65 | 4.45° | 11.97 u |
| 3 | 7 | 4/3 | 7,536–8,205 | 2.36 M | 65 | 4.41° | 12.00 u |
| 4 | 9 | 5/4 | 10,003–10,652 | 3.07 M | 88 | 4.43° | 12.00 u |

Prism counts sit inside the shipped band (Hijack 2,772–9,930; Peel the Cage 10,620–20,153;
Drumfire 28,350). **Volume does not**, and that is deliberate — see § 8.

**Both ends of the ladder are derived.** `N_min = 5`: at `n_out = 2` the outer shell offers two
lanes and a gate on it is a coin flip. `N_max = 9`: the tightest same-shell separation
`2·a_in·sin(π/n_in)` = **64.7 u** must exceed the 40 u gate mouth with margin (38%), or a ring
becomes threadable from the wrong lane.

---

## 7. The prism — the expensive, thin-margin decision

**`prismScale = (6, 6, 8)`, spacing 8.0 u**, overriding the Track Projector's own `(3,3,6)`.

`VesselAttachPrismEffectSO` is dispatched from a PhysX trigger (`ImpactorBase.OnTriggerEnter`,
enter-only, no sweep, no shell tier for plain prisms), sampled once per `FixedUpdate` — and the
project's `Fixed Timestep` is exactly **0.04 s** with `m_AutoSyncTransforms: 0` — while
`VesselTransformer.Update → MoveShip` **teleports** via `transform.position +=`. The effective
sample step is `speed × max(frameTime, 0.04)` = **6.00 u at the 150 u/s a launch carries.**

A trigger fires on overlap, so the catch window is the Minkowski sum of the prism's collider
cross-section and the hull's extent along travel. Measured: **3.46–3.53 u for a 3-wide rail**
against a 6.00 u step — so **~41% of perpendicular re-attaches MISS at any frame rate ≥ 25 fps**,
frame-rate- and phase-dependently, with nothing logged. The pilot just flies through the rail.

`(6, 6, 8)` gives a window of 6.46–6.61 u > 6.00 — **an 8–10% margin, and only at ≥ 25 fps**. At
20 fps the cross-section would need to be ≥ 7.1.

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
| The Slip ghost is inert | **REFUTED** | The ghost works, and the 65 u shell gap is **conservative**: carried speed decays at only 12 u/s², so 0.6 s of ghost covers ~88 u. 65 books a 26% margin. Do not raise it. |
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
the grind at 150 rather than collapsing to 30 u/s). Off-rail it flies at its own next ring and
*through* it, because `AIPilot` has no arrive-and-stop behaviour. The provider is cleared at
teardown — Switchback ships without that and leaks its closure across a scene-reload replay.

**Still unverified in the editor:** whether that AI actually races, and everything about how the
mode plays. Nothing here has been run in Unity.
