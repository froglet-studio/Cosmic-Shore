# Collision Burst Lab

The prism's **interaction detail levels** (`Docs/PRISM_ANIMATION.md` §4.11) side by side with the ordinary
effects they stand in for, running the shipped shader code in WebGL2, with a measured look scorecard and
a shared decision log: **destruction** (the explosion → the silver gnomon cut, every fragment bursting out on its own)
and **stealing** (the grade → the roll). Round 1 looked at the four level-1 collision bursts alone.

- **Live page:** https://claude.ai/artifact/F47yG1UjBkK662rEnhk7m9 (decision log: `decisions`) — round 11
- **Page (generated, never edit by hand):** `Docs/Studios/PrismCollisionBurstLab.html`
- **Source:** `Tools/Build/prism_collision_burst_lab/lab.src.html`; `build.py` stamps the shipped
  numbers and shader code into it (`--check` fails when the page is stale).
- **Parity gate:** `Tools/Build/prism_collision_burst_lab/parity.py`

The question the lab exists to answer was the brief's: **spectacular when you look at it, the ordinary
effect when you squint**. Since round 9 the squint is retired (the designer: the silver cut's range of sizes lets a
far-away player take the small fragments as unseen) and the question is **does it look awesome**: the Look scorecard
measures each level (fragments, size range, how evenly the cloud fills the sphere, how big, how fast it tumbles, how
many neighbours move in step, when it thins). Rounds 1–8 below measured the squint.

## What the page reads (it is a reader, never an authority)

`build.py` copies, at build time, from the assets — every value on the page names its source:

| From | What |
|---|---|
| `Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl` | the whole burst — translated mechanically to GLSL (types, `frac`/`lerp`, comments), not re-written |
| `PrismOcclusionCorridor.hlsl` | `PrismErosionFade_float`, its hash and `PRISM_EROSION_*` constants (the explosion's fade) |
| RotateFacesAlongAxis / TangentSlider subgraphs | the explosion's face pivot (asserted −0.5 / 0.5 / 0.5 from the graph's node slots, and asserted equal to the HLSL's `PRISM_BURST_PIVOT_BIAS` / `SLIDE_BIAS` / `SLIDER_GAIN`: the page's explosion runs the HLSL's `PrismBurstExplosionMap`) |
| `ExplodingBlockMaterial.mat`, `PrismExplosion.prefab`, `PrismExplosion.cs` | `_ExplosiveRotation` 0.0169, `_ExplosiveSpead` 0.05, `_Spread` (0.18, 0.1, 0.1), speed 10–33.33, 7.5 s life |
| `Prism.asset` | the explosion's 72-vertex wedge mesh, byte for byte |
| `PrismInteractionLodConfig.asset` | the size-on-screen lines, the budget, the shape, the shed weights and skin offset, the bloom's rings, segments and peel noise |
| `PrismClockAnimation.hlsl`, `MaterialPropertyAnimator.cs` | the steal's grade, `PrismColorLerp_float` (translated), and its 0.8 s length |
| `PrismMotionConfig.asset`, `PrismMotion.cs` | the 64/48 and 256/192 ladder, debris restitution ⅓ and limit 200 |
| `PrismCollisionBurstMaterial.mat`, `OriginalColorSetSO` | the look dials; Jade/Ruby/Gold/Blue inside/outside colours |

The burst mesh is `HighPolyPrismMesh.GetUnwelded(10)` rebuilt on the page in float32 (`Math.fround`), as
C# builds it.

## Round 11 — 2026-10-17 (`cece/dreamy-pascal-7xxx1m`): the explosion IS level 0; the performance pass

**Your decision applied** (the designer, logged as *Fragment look*, then: *"I like the new decision i just sent in. it
communicates the explosion a bit better, but it seems like it was reflected in the levels not the explosion. lets see
what we can do about unifying them and ensure that this is the performance optimized version we are pushing for both
the explosion and the roll effect for the steal."*).

### Why the decision missed the explosion

Level 0 was the explosion's own Shader Graph (`ExplodingBlockMaterial`); every Fragments-tab dial drives the burst
shader, so it could only ever move levels 1-4.

### What changed

- **The explosion is level 0 of the cut** (`explosionAsFragments`, on): every death the finer levels do not take — far
  away, off screen, PLAIN (prism movement above ladder level 1) or over the close-up budget — is the FRAGMENT kind on
  the cut's depth 0, the explosion's own 24 wedges (`PrismInteractionLod.MeshOfLevel(0)`), so one law and one set of
  dials move every destruction. It rides the same carrier (one entity, 24 triangles, as the explosion did) on its OWN
  live queue, capped by `maxLiveExplosions` (4,096) and kept out of the detail budget, so a field-wide wipe neither
  crowds the close-up levels nor is refused by them. Any refusal — the cap, the render service down, a device without
  Entities Graphics (where the in-flight WebGL MeshRenderer path takes over) — falls back to the explosion's graph.
- **One program per kind** (`#pragma multi_compile_local _ _BURST_FRAGMENT _BURST_ROLL`): the game's destructions draw
  a vertex program with ONLY the fragment law and the steal's roll one with ONLY the roll — none of the five lab-only
  kinds' code or registers, no per-vertex kind branch. `PrismCollisionBurst` keeps three runtime materials (every-kind
  for the lab-only sheds, fragment, roll) and batches by (mesh, program). The lab still runs the every-kind program.
- **The roll draws the welded grid** (`HighPolyPrismMesh.Get(n)` for kind ROLL, `PrismInteractionLod.MeshOf`): its pose
  reads only per-vertex data and its piece is per face, so it needs no torn triangles. The welded grid transforms
  (n + 1)² vertices a face instead of 6n²: 3,750 against 20,736 at 24 subdivisions (5.5 ×), the same triangles, every
  vertex bit-identical (`TheRollDrawsTheWeldedGrid`).
- **Adopted from the decision**: fragment burst 0.1, drift 0.06, lift 2, aim 0.95, speed gain 1.65, travel noise 0.48;
  the roll's fine line 0.24; the burst material's Fresnel 4 and inside glow 1.2; depth lines 0.05 / 0.315 / 0.52 and
  level 4 at **0.6** (the decision put levels 2 and 3 past level 4's 0.24, which the config's sanity check refuses —
  level 4 moved to the top of the range, so the 3,456-fragment cut is the rarest). **Not adopted**: the bloom /
  unravel / flake weights (the steal stays the roll alone, as asked), their shapes (bloom rings, curls, ribbons, shards
  — dead in the game; 7 ribbons would also break the shed meshes' fit), the grade length 2.2 s
  (`MaterialPropertyAnimator.DefaultTransitionSeconds` is every prism colour change in the game — a decision of its own),
  and the archived proposals (overshoot, shed lift).

### Look scorecard (shipped settings, impact v (20, 0, 0))

| | Fragments | Evenness | Cloud at 2 s (R) | In step | Lean along v (R) | Half gone | All gone |
|---|---|---|---|---|---|---|---|
| *Explosion graph (level 0 before this round)* | 24 | 0.323 | 1.78 | — | 0.00 | — | erodes to 6.38 s |
| **Level 0 · the explosion** | 24 | 0.556 | 0.66 | 0% | **0.45** | 5.40 s | 7.10 s |
| Level 1 | 192 | 0.658 | 1.90 | 0% | 1.04 | 4.60 s | 6.63 s |
| Level 4 | 3,456 | 0.674 | 3.25 | 0.3% | 1.83 | 3.30 s | 5.53 s |
| *control: a face leaves as one* | | **0.476** — fires | | | | | |
| *control: velocity reversed / ignored* | | | | | **−1.82 / −0.01** — fire | | |
| *control: noise off* | | | | **100%** — fires | | | |

The decision leans the cloud hard along the hit, so evenness is about 0.6-0.67 against round 9's 0.8; the harness's
floor moved from 0.70 to **0.50**, between the shipped burst and the grouped control (0.42-0.48), which is what the
floor is for. At a 20 u/s hit the level-0 cloud is SMALLER than the old explosion's (0.66 R against 1.78 R at 2 s:
burst 0.1 and drift 0.06 are small until the speed boost, 4.85 × at the clamp).

### Checked

`verify_prism_collision_burst.py` PASS — F1-F10 at the adopted shape (reach use 0.998, spin 0.949 of its cap, smaller
gone first in every pair, evenness 0.659 over the 0.50 floor, lean ≥ 2.78 R at 30 u/s, mirrored within 0%), 11 controls
fire, and glslang compiles 42 programs (3 passes × the every-kind, fragment-only and roll-only programs, plain and
DOTS-instanced, × 2 stages). `parity.py` PASS (129,600 burst and 10,008 fragment vertices, 0 outliers; 7 controls
fire). `verify_lab.cjs` PASS (screenshots read: level 0 fans its 24 wedges up along a (−29, 97, −43) hit as levels 1-4
do). `unity_refcompile` player 0 errors; editor 0 in this branch's files (3 in `AuthenticationServiceFacade.cs`,
identical on a clean `bleeding-edge` worktree — upstream). **Not run:** the editor — **NOT editor-verified**
(checklist item 11).

### Decision needed (designer)

- The level-0 cloud's size at a gentle hit (0.66 R against the old explosion's 1.78 R) — the Fragments tab's burst and
  drift move it for every level at once.
- The grade length (2.2 s in your decision): every prism colour change in the game, not only the steal.

## Round 10 — 2026-10-16 (`cece/dreamy-pascal-7xxx1m`): the velocity, shown and measured

**Your report** (the designer): *"they still don't seem to be accepting the velocity vector. you should give me a 3d
input vector in the lab that i can use to confirm you are applying the velocity correctly. in game it looked like the
velocity was not getting accepted at all."*

### What was found

The velocity DOES reach the burst — traced end to end: `PrismFactory.SpawnExplosion` hands `data.Velocity` to
`PrismInteractionLod.TryDestruction` exactly as it hands it to the explosion; `PrismCollisionBurst.TryRequestDestruction`
resolves it through the explosion's own `PrismDebris.ResolveExplosionFlight` and stamps it in `_SliceDrift`
(`SpawnSliceDebrisBatch` writes it per entity); `BurstVert` reads it as the flight (`posWS += v t`), the lean and the
second spin axis. What the hit barely changed was the fragments' OWN motion: a 0.35 lean, and a burst the same size
for a 10 u/s hit and a 33 u/s one — where the explosion's spread grows with its speed. And the lab's camera rode with
the flight, so the flight itself never showed on the page.

### What changed

- **A harder hit bursts wider and leans more along it** (`PrismBurstFragmentBoost`): the fragments' distance and their
  lean × (1 + `fragmentSpeedGain` 0.5 × (flight speed ÷ `fragmentSpeedRef` 10 u/s − 1)) — 1 at the explosion's slowest
  debris, 2 at 30 u/s. `fragmentAim` 0.35 → 0.5. The bounds count it (`FragmentReach(…, flightSpeed)`).
- **The lab's Velocity tab**: the impact velocity X / Y / Z the death is handed (`PrismEventData.Velocity`), resolved
  exactly as the explosion resolves it; a **yellow arrow** per cloud on the stage is the flight; **Camera rides the
  flight** 0 holds the camera still so every cloud — the explosion's and the fragments' — visibly flies along the
  arrow; the HUD prints impact v → flight, the boost and the lean.
- **The Look scorecard's velocity check**: *Lean along v* (the cloud's mean displacement at 2 s with the flight taken
  out, along the hit — a flight not applied would read −|v|·t ÷ R) and *Across v* (≈ 0), with two controls that must
  fail: the velocity reversed (must lean the other way) and the harness's `PRISM_BURST_FRAGMENT_VELOCITY_OFF` compiled
  into a page probe (must not lean).

### Look scorecard (shipped settings, impact v (20, 0, 0) → flight 20 u/s, boost 1.5)

| | Evenness | Cloud at 2 s (R) | In step | Lean along v (R) | Across v (R) | Half gone |
|---|---|---|---|---|---|---|
| *Explosion (the baseline)* | 0.323 | 1.78 | — | 0.00 | 0.00 | — |
| Level 1 | 0.759 | 4.26 | 0% | 1.09 | 0.03 | 4.60 s |
| Level 2 | 0.786 | 5.27 | 0.9% | 1.34 | 0.03 | 4.17 s |
| Level 3 | 0.796 | 6.26 | 0.5% | 1.60 | 0.04 | 3.70 s |
| Level 4 | 0.800 | 7.21 | 0.3% | **1.87** | 0.04 | 3.30 s |
| *control: velocity reversed* | 0.793 | 7.21 | 0.3% | **−1.93** — fires | 0.05 | |
| *control: velocity ignored* | 0.816 | 4.81 | 0.5% | **−0.03** — fires | 0.03 | |
| *control: noise off* | 0.803 | | **100%** — fires | | | |
| *control: a face leaves as one* | **0.543** — fires | | | | | |

### Checked

`verify_prism_collision_burst.py` PASS — new **F10**: for random prisms and directions the burst leans at least 2.58 R
along a 30 u/s hit at 2 s, reversing the hit mirrors it within 4%, and a 3 × harder hit bursts the boost's 2 × wider
(within 0%); its control (`PRISM_BURST_FRAGMENT_VELOCITY_OFF`, the pose ignores the hit) FIRED — 11 controls, all
fire; F1–F9 still pass at the new shape (reach use 0.998, spin 0.997 of its cap, evenness 0.794). `parity.py` PASS —
the page's FRAGMENT pose vs the HLSL executed with random velocities and boosts, 10,008 vertices, worst 3.7e-4.
`verify_lab.cjs` PASS (screenshots read: a 25 u/s upward hit with the camera held still sends every cloud up along its
arrow). `build.py --check` OK; `unity_refcompile` player and editor 0 errors; `check_console_logging.py` 0.
**Not run:** the editor — **NOT editor-verified**. In the game, look first at the burst material's `_FragmentFade.w`
(0.5) and `_FragmentNoise.w` (10) and the config's `fragmentSpeedGain` / `fragmentSpeedRef` (checklist item 10).

### Decision needed (designer)

How strongly the hit should shape the burst — the Velocity tab to set a hit, the Fragments tab's *Aim* and *Speed gain*
to set how much it leans and widens.

## Round 9 — 2026-10-16 (`cece/dreamy-pascal-7xxx1m`): the squint is retired; every fragment bursts out on its own

**Your decision applied** (the designer): *"i see that the squint test was steering things to do exactly what i am
trying to fix now. by using the silver gnomen cut we unsure that there are still a nice size range such that from far
away a player could assume that the small ones are going unseen. thus we do not need to do the squint test anymore.
this will allow you to create a more uniform effect instead of keeping things grouped so as to maintain squint parity
across levels. now I just want it to look awesome"*. The shared decision log was empty at the start of the round.

### What changed, in the game and in the lab

The WEDGE kind (every fragment riding its root wedge's slide and spin, so the cloud stayed grouped in 24 sheets) is
gone. Kind 5 is now **FRAGMENT** (`PrismBurstFragmentPose`, world space), tuned by fourteen new config fields
(`PrismInteractionLodConfig` `fragment*`, the lab's new **Fragments** tab):

- **Its own direction**: its face's radial × how far out on the face it sits (0 at the centre, √2 at a corner) plus
  the face normal × `fragmentLift` (1) plus the hit × `fragmentAim` (0.35) — with lift 1 that is the cube seen from its
  own centre, every face filling its own sixth of the sphere whatever the prism's proportions.
- **Its own distance**: R × (`fragmentBurst` 0.5 × (1 − e^(−t/τ)) + `fragmentDrift` 0.2 × t), × (1 + `fragmentSizeSpeed`
  0.8 × its size level): the big chunks stay near the core and the fine debris is flung farthest, so the cloud is
  FILLED, not a hollow inflated box (the first tuning, burst 1.2 / size-speed 0.3, read as one).
- **Its own spin**: about its direction and cross(velocity, N) — the explosion's velocity response — at the
  explosion's own speed-capped angle (`_ExplosiveRotation`, copied at runtime) × min(1 + 0.25 × level, 1.6).
- **Its own end**: it shrinks away to its centre (no dissolve) over the last 35% of its own life, the smallest gone at
  45% of the explosion's life and the largest at 92% — every fragment gone by 95%, so the entity retires on nothing.
- **The noise stays**: the gradient noise varies both spin angles (0.15), the distance and the direction (0.25), so
  neighbours move alike and never in step.

The CPU bounds (`PrismCollisionBurst.FragmentReach`) are the HLSL's `PrismBurstFragmentReach`, proved by the harness.
`_ExplosionSpread`, `_WedgeNoise` and the erosion's UV are gone from the shader (the mesh keeps the explosion's UV0
layout). The **squint scorecard and the LOD-step test are retired**; the **Look scorecard** measures each level on the
page's own vertex stage through transform feedback; the Squint button is gone.

### Look scorecard (shipped settings, `verify_lab.cjs` batch)

| | Fragments | Size range | Spread evenness | Cloud at 2 s (R) | Tumble (rad/s) | In step | Half gone | All gone |
|---|---|---|---|---|---|---|---|---|
| *Explosion (the baseline)* | 24 | 1 | 0.323 | 1.78 | 0.73 | — | — | erodes to 6.38 s |
| Level 1 | 192 | 4.5 × | 0.777 | 2.84 | 0.58 | 0% | 4.60 s | 6.63 s |
| Level 2 | 504 | 8.2 × | 0.793 | 3.52 | 0.63 | 0.9% | 4.17 s | 6.23 s |
| Level 3 | 1,320 | 15.2 × | 0.806 | 4.17 | 0.66 | 0.5% | 3.70 s | 5.90 s |
| Level 4 | 3,456 | 28.1 × | **0.811** | 4.81 | 0.67 | 0.5% | 3.30 s | 5.53 s |
| *control: noise off* | 3,456 | | 0.816 | | | **100%** — fires | | |
| *control: a face leaves as one* (`PRISM_BURST_FRAGMENT_FACE_ONLY` compiled into the page) | 3,456 | | **0.529** — fires | | | | | |

Spread evenness is the entropy of the fragments' travel directions over 256 equal bins of the sphere, as a share of
the most there can be: the explosion's six slide directions cap it at 0.32. Finer levels thin faster (more small
fragments, which leave first) — from far away the small ones simply go unseen.

### Found

- **The harness never tested the silver cut's motion in rounds 7 and 8.** `run_wedge_tests` picked the mesh with
  `cut.name[1] == 'i'`, true for both "sierpinski" and "silver", so every "silver" case built a Sierpinski mesh (the
  round-8 W9 slide test called `silver(7)` directly and was real). The new F tests compare names exactly and assert
  each mesh's vertex count (72 × the per-wedge count) before testing it.
- **`centroid` is an HLSL interpolation keyword**: a parameter of that name compiled in the C++ harness and failed
  glslang's HLSL front end (and would have failed Unity's). Renamed `middle`.
- **One angle off every face is six hollow cones**: a unit radial plus a constant lift sent every fragment of a face
  off at the same angle (evenness 0.714 in the harness); weighting the radial by how far out the fragment sits filled
  each face's sixth of the sphere (0.828).
- **A 64-bin evenness cannot tell six cones from a sphere** (0.787 against 0.726 for lift 3): 256 bins (about 7°
  each) separate them.

### Checked

`verify_prism_collision_burst.py` PASS — F1–F9 on both cuts at depths 0–10, the noise off and on, the shipped shape
and every slider at the end of its range, random rotations and non-uniform scales: the first frame is the prism; every
fragment rigid up to its shrink (worst 3.3e-8 of the reach); unit normals; inside `PrismBurstFragmentReach` (worst use
0.985); never spinning faster than 2 × the explosion's angle × spinCap × (1 + the noise) (worst 0.95 of it); all gone by
95% of the life; the smaller gone first in 492,312 of 492,312 pairs a size level apart; 5 of 2,592 same-size
neighbours travelling in step; evenness 0.826 (floor 0.70). X: the explosion map still equals RotateFacesAlongAxis'
closed form (2.5e-6). Controls: the noise off and a face leaving as one FIRED, the explosion's pivot and unscaled
turn FIRED — 10 in all; glslang compiles every pass. `parity.py` PASS — the page's FRAGMENT pose against
`PrismBurstFragmentPose` executed, 10,008 vertices over 7 cases (both cuts, noise on and off, random shapes, random
rotations), worst 3.2e-4; 7 controls FIRED (incl. the burst doubled). `verify_lab.cjs` PASS (desktop and phone
screenshots read); `build.py --check` OK; `unity_refcompile` player and editor 0 errors; `check_console_logging.py` 0.
**Not run:** the editor — **NOT editor-verified** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

### Decision needed (designer)

How it should FEEL in motion — the Fragments tab holds every dial (burst, drift, lift, aim, how much the smaller fly
farther and spin faster, when the smallest and largest are gone, the shrink, the noise). The numbers above are a first
tuning chosen from still frames.

## Round 8 — 2026-10-15 (`cece/dreamy-pascal-7xxx1m`): the noise moves the slides too — superseded by round 9 (the FRAGMENT kind)

**Your decision applied** (the designer): *"so the perlin noise helped with the rotations now use it for the
displacements too"*.

### What changed

`PrismBurstWedgeSlide`: a fragment's slide is its wedge's tangent T with its length × (1 + `wedgeSlideNoise` (0.15) ×
a third field of the same gradient noise), plus half that amplitude across the wedge (cross(N, T)) and off the face
(N) from two more fields — sampled at the fragment's centroid like the spin's, so each fragment stays rigid,
neighbours travel nearly together, and none in step. `PrismBurstExplosionMap` takes the slide's direction (the
explosion passes T, unchanged); the slide's amplitude rides `_WedgeNoise.z`. A slide is at most 1 + 2 × the amplitude
of the explosion's, and `WedgeReach` (CPU bounds) and `PrismBurstWedgeReach` count it.

### Scorecard (shipped settings)

| | Size | Centre | Fade | Colour | Squint |
|---|---|---|---|---|---|
| Silver level 1 | 0.931 | 0.269 | 0.028 | 0.114 | 0.157 |
| Silver level 2 | 0.868 | 0.235 | 0.044 | 0.112 | **0.141** |
| Silver level 3 | 0.846 | 0.236 | 0.052 | 0.114 | **0.144** |
| Silver level 4 | 0.816 | 0.180 | 0.065 | 0.112 | **0.132** |
| *the band: the explosion, another flight* | 1.132 | 0.208 | 0.099 | 0.012 | 0.097 |

Levels 2–4 now **squint as the explosion** — inside 1.5 × the band — WITH the inside colour (round 7: 0.152–0.167);
level 1 is close (0.157). The cloud is a little smaller (0.82–0.93 of the explosion's coverage) — the fragments of a
wedge no longer stack in one sheet. LOD steps: 0.33 / 0.45 / 0.64 / 0.90 of their bands, all unnoticeable.

### Checked

`verify_prism_collision_burst.py` PASS — W9: no neighbouring fragment pair of one wedge slides in perfect step
(0 of 15,840; without the noise all of them), the slide's steepest slope 1.4 a unit, the longest 1.15 of the
explosion's (bound 1.30); every pose inside the widened reach (worst use 0.902); a new control (the slide noise off)
FIRED — 11 in all. (The roll test's own share read 97.8% this run against 98.5%: the new test's random draws moved
the roll's random prisms and cameras; the roll is unchanged.) `parity.py` PASS (the wedge with both noises on);
`verify_lab.cjs` PASS; `build.py --check` OK; `unity_refcompile` player + editor 0 errors. **Not run:** the editor.

### Decision needed (designer)

The slide noise amplitude (0.15) — a slider beside the spin noise on the page (Kinds tab).

## Round 7 — 2026-10-15 (`cece/dreamy-pascal-7xxx1m`): the silver gnomon cut, the spin noise, and the ROLL

**Your decisions applied** (the designer, in the session): *"lets add a small amount of perlin noise. just enough so
we break up the perfectly synchronized triangle rotations from adjacent triangle with the same normal. we can also use
this silver ratio gnomen as a way to get a range of triangle sizes. the continuity of the subtle perlin noise will
hopefully keep the beautiful order together while giving just enough asymmetry"* — and for stealing: *"lets use a
camera trick like we do for the mass crystal effect. the peeling can curl into a tight roll and roll along and around
the surface until the prism occludes it. it can always peel away from the camera's perspective and around the surfaces
the camera is facing. then the rolls don't need to dissolve they just move out of sight by being occluded from their
own prism."*

### What changed, in the game and in the lab

- **The silver gnomon cut** (`PrismSilverGnomonTiling`, `HighPolyPrismMesh.GetSilverGnomon`, config
  `destructionCut = SilverGnomon`, `silverDepths` 4 / 6 / 8 / 10). Your figure's silver triangle (45°, 67.5°, 67.5°)
  and gnomon (45°, 22.5°, 112.5°) each cut exactly into one of each — the triangle at (√2 − 1) of its far leg (the
  smaller triangle 2·sin 22.5° ≈ 0.765 of it), the gnomon where its silver part's legs are equal — taking √2 − 1 and
  2 − √2 of the parent's area. The explosion's right-isosceles wedge cuts into one of each too (its 90° corner is
  67.5° + 22.5°), so depth 0 is still the explosion. The recursion gives the child of the OTHER shape one less budget
  and the child of the SAME shape (the larger, the whirl's continuation) two less: Fibonacci counts (24 × 8 / 21 / 55
  / 144 fragments at the four levels) with a wide range of sizes (lengths spanning 4.5× / 8.2× / 15× / 28×; the
  Sierpinski depth 4 spans 8×), every piece one of the two shapes — no slivers — every depth nested in the next.
  Each fragment's size LEVEL (log2 of its wedge's size over its own) rides TEXCOORD2.z as before; the Sierpinski cut
  stays selectable.
- **The spin noise**: each fragment's two spin angles are its root wedge's × (1 + `wedgeSpinNoise` (0.15) × a 3D
  gradient (Perlin) noise at its centroid, `wedgeNoiseFrequency` 3 cycles a prism width, seeded per death),
  `PrismBurstWedgeSpin`. Continuous (steepest slope 3.2 a unit), so neighbours turn almost alike and the order holds;
  never equal (with the noise on, 0.5% of neighbouring fragment pairs remain in step, against 100% without).
- **The ROLL** (kind 6, `PrismBurstRoll`; `rollWeight` 1, every other shed weight 0). Each face's skin rolls up like a
  carpet AWAY from the camera — along the face's in-plane axis on which the camera is furthest off the centre — into
  an Archimedean spiral (inner radius `rollRadius` 0.05 prism radii, `rollGap` 0.01 between turns, both never finer
  than the shed mesh allows, so a turn stays round and turns never cross), its outer turn tangent to the face. It
  crosses the face over the grade's length (eased in, then rolling at a constant speed); the skin winds top side in,
  so the roll shows the NEW colour. Then it rolls over the far edge onto the side face — which faces away from the
  camera by that choice of axis — and down it as far as this camera needs to lose it (x · (L + c) > h · 2R), or, when
  that face is too short, round the next edge onto the face opposite its own; it STOPS at the first place that hides
  it (rolling on would bring it out past the prism's far end for a camera looking along the prism). Its ends tuck in
  as it goes over, by the strip a sideways camera could see past the corner. It never dissolves where it is seen —
  only in the last 15% of its life, a roll its prism could not hide. The camera position at the steal and the mesh's
  subdivision ride `_SliceAxis`; the life is the grade × 1.6.

### Scorecards (shipped settings)

**Destruction against the explosion** (band 0.097, held whole 0.315):

| | Size | Centre | Fade | Colour | Squint |
|---|---|---|---|---|---|
| Silver level 1 (192 fragments) | 0.941 | 0.158 | 0.039 | 0.116 | 0.167 |
| Silver level 2 (504) | 0.892 | 0.219 | 0.035 | 0.117 | 0.154 |
| Silver level 3 (1,320) | 0.876 | 0.147 | 0.046 | 0.120 | 0.152 |
| Silver level 4 (3,456) | 0.874 | 0.173 | 0.045 | 0.125 | 0.153 |
| Sierpinski 4 as wedges (round 6) | 0.903 | 0.228 | 0.054 | 0.119 | 0.166 |

The silver cut squints a little closer than round 6's Sierpinski (0.152–0.167 against 0.166–0.184) and fades closer
than two explosions are to each other (0.035–0.046 against 0.099); the colour (the inside colour) is still the gap.

**Stealing against the grade** (band 0.033, colour snapped 0.158): the roll 0.239 (12 subdivisions) / 0.209 (24),
"reads as different"; round 5's bloom 0.116 / 0.121. The roll is a WIPE that hides itself, not a blend: squinted at,
the old colour leaves the face from the camera's side instead of fading everywhere at once, and the new-colour roll
stands proud of the face while it crosses (size 1.18–1.21). It is a different kind of steal from the grade by design.

**Off the camera** (the harness, 200 random slabs 1:5 at most, cameras 4–20 radii out from anywhere): by 70% of the
roll's tail, **98.5%** of the camera-facing skin's vertices are hidden behind their own prism; the rest are grazing
views, left to the last-15% fade.

**LOD steps** (1,080 px): silver level 4 → 3 0.35, 3 → 2 0.51, 2 → 1 0.71, 1 → the explosion 1.05; the roll 24 → 18
0.09, 18 → 12 0.48, 12 → the grade 1.40 (its band: the same steal 10% of the grade later) — all unnoticeable.

### Found

1. **The silver dissection fits the explosion's wedges exactly**, and its two shapes refine each other — a two-tile
   substitution, so every depth is nested and sliver-free (the C# and the page's port agree value for value as float32).
2. **A roll that keeps rolling un-hides itself.** Rolling on along the opposite face carried it past the prism's far
   end for a camera looking along a long slab (87.7% hidden); stopping at the first hiding place: 98.5%.
3. **A roll's ends leak past the corner** for a sideways camera (an end cap lies in the plane of the side face; the
   strip behind it, as wide as the roll stands off the prism times the camera's slope, is visible). The tuck fixes it.
4. **The burst hash names shatter shards in steps of 1/1024** (float precision on large inputs), so two shards can
   share a name; the harness's rigidity test treated a shared name as one shard and failed once the random sequence
   moved. The test now identifies a shard by its Voronoi cell. Harmless in the game (only the shard's dissolve order
   shares the value) — the shatter is the shader's fallback, drawn by no ladder.
5. **A step band from a law without randomness collapses** — again (L-STU-70): the roll follows the camera, not a seed.

### Checked

- `verify_prism_collision_burst.py` — **PASS**: the roll (first frame, unit normals, envelope, isometry and normal
  derivative on its face, two-sided at its contact line, the 97% hiding test, no early dissolve); the gradient noise
  (sd 0.305, 0.03% clamped); the wedge on both cuts, noise off (W1–W7) and on (W8: within 2 × amplitude of its
  wedge's spin, out of step with its neighbours); 10 controls FIRED, two new (noiseless spin; the roll toward the
  camera).
- `parity.py` — **PASS**: the burst with the roll (129,600 vertices, 0 outliers); the wedge on both cuts with the
  noise (10,008 vertices); depth 0 vs the explosion's graph; the silver cut vs the C# compiled, depths 0–12, as
  float32; the silver meshes (area 6, the C#'s counts); six controls FIRED (one new: the next silver depth).
- The silver tiling's angles, counts, wedge membership and nesting run under .NET: 0 failures.
- `verify_lab.cjs` PASS (screenshots read); `build.py --check` OK; `unity_refcompile` player + editor — 0 errors in
  project code; `check_console_logging.py` 0 problems. **Not run:** the Unity editor (`PrismCollisionBurstTests`,
  new: the silver cut's shapes, counts, nesting and mesh; the roll's kind, tail and pick; the config) — filed in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

### Decision needed (designer)

1. **The roll's camera**: stamped at the steal (shipped) — a fast camera move during the 1.3 s could show a roll on
   its way round; the live camera would follow but could flip a roll's direction mid-way. Play it and say.
2. **The roll against the grade**: it does not squint as the grade (0.21–0.24); it is the camera trick instead. Keep
   it, or keep the grade at the farther shed lines and roll only close up.
3. **The spin noise** (0.15 at 3 cycles a prism width) and **the silver depths** (4 / 6 / 8 / 10) — sliders on the page.
4. **The inside colour on destruction fragments** (still open from round 6).

## Round 6 — 2026-10-15 (`cece/dreamy-pascal-7xxx1m`): every fragment spins on its ROOT wedge's axes — superseded where round 7 differs (the cut, the noise, the shed)

**Your report applied** (the designer, after trying round 5 in the game): *"the velocity vector should be using a
cross product with the normal to determine rotation axis etc. like the explosion shader"*.

### What was wrong

The cross product was already there — `PrismBurstExplosionMap` turns about cross(velocity, N) with the raw world
velocity and the object-space normal, exactly as `ExplodingBlockGraph` feeds RotateFacesAlongAxis (traced edge by
edge: the graph's `Velocity` property goes straight into the subgraph's Cross Product with the object-space Normal
Vector; `PrismDebris` and the burst stamp the same resolved flight). What differed was the OTHER axis and the slide:
round 5 gave each fragment its own tangent, the direction from its face centre to its centroid, so every fragment
turned about a different first axis and slid in a different radial direction — a starburst in which the
velocity-driven spin read as random. The explosion has four tangents a face, one per wedge.

### What changed

- `PrismBurstWedgeTangent`: a fragment takes its ROOT wedge's tangent — the face's in-plane object axis, signed, along
  which its centroid lies furthest from the face centre (Prism.asset's tangent for that wedge). So every fragment turns
  about its wedge's own two axes — the tangent and cross(velocity, N) — by the explosion's angle, and slides with its
  wedge, about its own centre: the pieces of a wedge spin in step as the explosion spins that wedge, and are sent the
  way it is sent. Depth 0 is unchanged (a wedge's centroid direction IS its tangent).
- The harness proves it (W7): every vertex of every fragment at depths 0–3 has its root wedge's explosion normal
  (worst 1.8e-7); a new control (the round-5 radial tangent) fires on 82,944 checks.

### Scorecards (shipped settings)

| | Size | Centre | Fade | Colour | Squint | Without the inside colour |
|---|---|---|---|---|---|---|
| Depth 1 | 1.002 | 0.108 | 0.062 | 0.116 | 0.184 | 0.053 |
| Depth 2 | 0.962 | 0.180 | 0.059 | 0.117 | 0.176 | 0.058 |
| Depth 3 | 0.934 | 0.136 | 0.060 | 0.119 | 0.172 | 0.058 |
| Depth 4 | 0.918 | 0.190 | 0.055 | 0.119 | 0.173 | 0.064 |
| *the band: the explosion, another flight* | 1.132 | 0.208 | 0.099 | 0.012 | 0.097 | |

The centre is now inside the band (0.11–0.19, was 0.19–0.38) and the squint fell from 0.21–0.22 to 0.17–0.18. Drawn
without the inside colour (a scratch copy, not shipped) every depth now squints CLOSER to the explosion than two
explosions are to each other (0.053–0.064 against 0.097). LOD steps: depth 4 → 3 0.34, 3 → 2 0.49, 2 → 1 0.67,
1 → the explosion 1.06 of their bands — all unnoticeable.

### Checked

`verify_prism_collision_burst.py` PASS (8 controls fire); `parity.py` PASS (wedge depths 0–3 vs the HLSL executed,
depth 0 vs the explosion's graph, UVs); `verify_lab.cjs` PASS; `build.py --check` OK. The change is HLSL only; the C#
diff is comments. **Not run:** the Unity editor.

### Decision needed (designer)

1. **The inside colour on destruction fragments** — the gap is now ALL colour: 0.17–0.18 with it, 0.05–0.06 without.

## Round 5 — 2026-10-15 (`cece/dreamy-pascal-7xxx1m`): every depth moves as the explosion; the bloom alone, cut finer — superseded where round 6 differs (each fragment's tangent)

**Your decisions applied** (the designer, in the session): *"lets use just the bloom for stealing and we can have
more noise in how it peels so it looks different each time. the lets have it peal from more cuts so one peal curls
tighter and moves less far off the surface, so it is less obtrusive while skimming close. for destruction the
explosion is tuned. it accepts velocity vectors that send fragments in the right direction spinning the right way.
it limits how fast things rotate so they don't flicker etc. Each depth should adopt the fine tuning that was put
into the base explosion."*

### What changed, in the game and in the lab

- **Every destruction fragment moves by the explosion's own law** — a new burst kind, `WEDGE` (5), in
  `PrismCollisionBurst.hlsl`: `PrismBurstExplosionMap` is RotateFacesAlongAxis' closed form (W = 0) in object space —
  turn about the piece's tangent T, then about cross(velocity, N), each by amount × `_ExplosiveRotation`, in the
  object-SCALED frame as the graph does it; slide out along T by amount × `_ExplosiveSpead`; amount = the explosion's
  clamped speed × t, so the spin rate is capped exactly where the explosion caps it. `PrismBurstWedgePose` applies it
  to a Sierpinski fragment as a SCALED WEDGE: T = the direction from its face centre to its centroid (a wedge's own
  tangent at level 0), pivot = its centroid plus the explosion's pivot-to-centroid offset × 2^−level. At level 0 it
  IS the explosion. The flight is the explosion's (stamped velocity × t, as before).
- **The explosion's dials are single-sourced**: `PrismCollisionBurst.TryGetExplosionTuning` copies `_Spread`,
  `_ExplosiveRotation` and `_ExplosiveSpead` from the explosion pool's live material onto the burst material
  (`_ExplosionSpread`, `_ExplosionDials`) at runtime; the shader's defaults equal `ExplodingBlockMaterial.mat`'s (a test).
- **The explosion's fade too**: a `WEDGE` fades on `PrismErosionFade_float` over UV0 at opacity 1 − t/life, the
  explosion's fragment stage exactly — and every fragment of the cut now wears its wedge's EXPLOSION UV
  (`PrismSierpinskiTiling.WedgeUV`: `Prism.asset` gives every wedge the same UV triangle, (0,0) (1,0) (0.5,0.5)), so
  depth 0 fades exactly as the explosion and every depth on its pattern. The Sierpinski mesh's TEXCOORD2.z is now the
  fragment's level.
- **Culling bounds** for a wedge: `WedgeReach` = 3R + (2·o + amount·ES·gain)·maxScale, o the pivot's largest offset
  off the centroid — a proved bound (the harness asserts every pose inside it; worst use 0.898).
- **Stealing is the bloom only** (`unravelWeight` and `flakeWeight` ship at 0; both stay in the shader and in the
  Archive tab). The bloom is cut finer: each quarter into `bloomRings` (3) rings from the cube edge inward and
  `bloomSegments` (3) segments along it; each piece hinges on its own outer edge and is a ring's depth long, so the
  same `bloomTurns` curls it three times tighter and its lift (∝ its length) is a third. **`peelNoise`** (0.6) gives
  each piece its own delay, curl (0.4–1.6×) and lift (0.3–1.7×); the rings peel from the edge inward.
- **A new seed every steal**: `StealSeed` = the pose's seed moved on by the clock (× the golden ratio), so the same
  prism stolen twice peels two ways. In the lab, every Replay is a new steal.
- `IsSane` requires every shed mesh to be a multiple of 2 × rings and of segments (`BloomCutsFit`), so no piece
  straddles a triangle.
- **The lab**: the Destruction depths draw the `WEDGE` kind; the explosion and the wedge both run the HLSL's
  `PrismBurstExplosionMap` (the hand-written GLSL closed form is gone). The Stealing line-up is the grade, one petal
  a quarter (rings 1, segments 1, noise 0), the bloom, and the bloom on the next steal; sliders for rings, segments
  and peel noise (Kinds tab). The Archive tab adds round 4's glitter depth and round 4's three sheds.

### Scorecards (shipped settings)

**Squint against the explosion** (band 0.097, held whole 0.315):

| | Size | Centre | Fade | Colour | Squint |
|---|---|---|---|---|---|
| Depth 1 | 1.015 | 0.186 | 0.066 | 0.111 | 0.213 |
| Depth 2 | 1.002 | 0.278 | 0.074 | 0.104 | 0.213 |
| Depth 3 | 0.992 | 0.301 | 0.054 | 0.109 | 0.215 |
| Depth 4 | 1.019 | 0.378 | 0.063 | 0.109 | 0.224 |
| Depth 4 as round 4's glitter | 0.821 | 0.486 | 0.188 | 0.099 | 0.306 |
| *the band: the explosion, another flight* | 1.132 | 0.208 | 0.099 | 0.012 | 0.097 |

Size and fade are now the explosion's (the fade closer than two explosions are to each other). **What is left is the
colour**: the burst's back faces wear the brighter inside colour (the designer's rule, round 2), the explosion's do
not. Drawn without it (a scratch copy of the page, not shipped), depths 1–4 squint at 0.130 / 0.138 / 0.138 / 0.139 —
inside 1.5 × the band: **"squints as the explosion"**.

**LOD steps** (a 1,080 px screen; the band is now the deeper depth on another FLIGHT — the wedge and the explosion
have no per-prism random, so another prism on the same flight draws the same death):

| Step | At | Prism px | Band | Step | Step ÷ band |
|---|---|---|---|---|---|
| Depth 4 → 3 | 24% | 259 | 0.512 | 0.350 | 0.68 |
| Depth 3 → 2 | 16% | 173 | 0.501 | 0.449 | 0.90 |
| Depth 2 → 1 | 10% | 108 | 0.490 | 0.561 | 1.14 |
| Depth 1 → the explosion | 5% | 54 | 0.468 | 0.559 | 1.19 |
| Shed 24 → 18 | 28% | 302 | 0.455 | 0.066 | 0.15 |
| Shed 18 → 12 | 16% | 173 | 0.420 | 0.109 | 0.26 |
| Shed 12 → the grade | 8% | 86 | 0.353 | 0.390 | 1.10 |

Every step unnoticeable (≤ 1.5 × its band).

**Sheds against the grade** (band 0.033, colour snapped 0.158): the bloom 0.116 (12) / 0.121 (24), against one petal a
quarter 0.177 / 0.175 — the finer cut is closer to the grade (fade 0.065 against 0.172; size 1.12 against 1.25).

**Off the surface** (the lab's 4 × 0.6 × 2 slab, the shed's course, three seeds; the HLSL executed): the highest any
piece rises off its own face fell from 1.660 u (0.74 prism radii) for one petal a quarter to **0.629 u (0.28)**; the
mean from 0.344 u to **0.116 u**.

### Found

1. **The motion gap round 4 named was the gap.** With the explosion's law, size went from 0.72–0.82 to 0.99–1.02 and
   the fade from 0.15–0.19 to 0.05–0.07; the squint fell from 0.30–0.32 to 0.21–0.22.
2. **The remaining destruction gap is the inside colour, measured.** It is the round-2 rule (the brighter inside is
   the contrast); keeping it costs the squint ~0.08. A designer's call (below).
3. **The explosion's UVs are per wedge, not per face.** `Prism.asset` gives all 24 wedges the same UV triangle, so the
   explosion's erosion wipes each wedge on its own; the cut's face coordinates would have faded on a different
   pattern on half the faces (parity's new control: 36 of 72 vertices differ). Fixed by `WedgeUV`.
4. **A destruction's LOD band was measuring nothing.** With a law that has no per-prism random, "the same level on
   another prism" draws the identical death (band 0.001–0.003, ratios in the hundreds). The band is now another flight.
5. **A tighter curl breaks a fixed chord tolerance.** The harness's isometry check assumed a chord within 2e-3 of its
   arc; three times the curvature put it at 3e-3. It now proves isometry by convergence (halving the step quarters
   the shortfall), as it already proved the normal.
6. **`centroid` is an HLSL keyword** (an interpolation modifier): a parameter of that name compiles in C++ and fails
   in glslang. Renamed.

### Checked

- `verify_prism_collision_burst.py` — **PASS**: the wedge at depths 0–3 — amount 0 is the prism; level 0 equals the
  harness's own Rodrigues statement of the graph's closed form (worst 1.1e-6 radii); rigid in the world; unit normals;
  inside `PrismBurstWedgeReach` (worst use 0.898); no fragment turns faster than the explosion's 2 × amount × rotation
  (worst 1.000 of it) — plus the bloom's rings and noise through every earlier test; seven controls FIRED, two new
  (pivot bias 0; the turn in the unscaled frame); glslang compiles every pass.
- `parity.py` — **PASS**: burst 108,000 vertices (rings / segments 1, 2, 5, random noise) 0 outliers; explosion 720
  vertices vs the subgraph evaluated; **wedge** 4,248 vertices at depths 0–3 vs `PrismBurstWedgePose` executed, and depth 0's
  144 vertices vs RotateFacesAlongAxis evaluated on `Prism.asset` triangle by triangle (worst 3.2e-4); depth 0's UV0 =
  `Prism.asset`'s, 72 of 72; the cut vs the C# compiled; five controls FIRED (next depth, next kind, wedge and explosion
  with ExplosiveRotation ×2, face coordinates as UV0).
- `verify_lab.cjs` PASS (screenshots read); `build.py --check` OK; `unity_refcompile` player + editor — 0 errors in
  project code; `check_console_logging.py` 0 problems.
- **Not run:** the Unity editor, so `PrismCollisionBurstTests` (new: the wedge kind and constants, the reach, the
  explosion's dials, the steal seed, the explosion UVs, bloom-only config) — filed in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

### Not modelled (also true in the game's burst)

- The explosion's outer graph also slides the whole prism along `TangentSlider` before the face map, and reduces the
  spread with distance ("Distance Spread And Colors"); the wedge kind, like the lab's explosion, uses the face map
  alone.

### Decision needed (designer)

1. **The inside colour on destruction fragments**: keep it (the contrast; squint 0.21–0.22, "close") or let the
   wedge's back faces wear the explosion's shading (squint 0.13–0.14, "squints as the explosion").
2. **The bloom's cut**: 3 rings × 3 segments and noise 0.6 shipped; the sliders are live on the page (the shed meshes
   must stay multiples of 2 × rings and of segments: with the shipped 12 / 18 / 24, rings 1 or 3 and segments 1, 2,
   3 or 6 fit; any other value needs other mesh sizes, or `IsSane` refuses and every steal grades).
3. **Retire the unravel and the flake** from the shader, or keep them for a later use (shipped at weight 0).

## Round 4 — 2026-10-15 (`cece/dreamy-pascal-7xxx1m`): the Sierpinski cut — superseded where round 5 differs (every depth now moves as the explosion; stealing is the bloom only)

**Your decision applied** (the designer, in the session): *"a serpinski inspired cut would give a nice
distribution and range that you could scale with recursion depth without the need for shatter and using the
base explosion as the first depth."*

### What changed, in the game and in the lab

- **Destruction is one cut, scaled by recursion depth** (`PrismSierpinskiTiling`, `HighPolyPrismMesh.GetSierpinski`).
  Depth 0 is the explosion's own cut — four isoceles wedges a face fanned from its centre — and the game draws it
  as the ordinary explosion. Each deeper depth splits every corner triangle at its edge midpoints: the MIDDLE stays
  whole (the gasket's hole, here a fragment) and the three CORNERS recurse. 24 × 1 / 4 / 13 / 40 / 121 triangles at
  depths 0–4 (364 at 5). Per face at depth 4: 4 middles of a sixteenth of the face, 12 of 1/64, 36 of 1/256 and 432
  corners of 1/1024 — a self-similar range of sizes. Each depth is the next with its finest corners merged into
  their parent, so every depth is the whole prism at rest.
- **The ladder**: explosion (depth 0) → depth 1 (≥ 5% of the screen height) → 2 (≥ 10%) → 3 (≥ 16%) → 4 (≥ 24%),
  `depthMinScreen` in `PrismInteractionLodConfig` (an array: its length is the deepest cut, up to 5), coarser by
  the amount as before. The **shatter** and round 3's **quadtree glitter** left the ladder (`PrismGlitterTiling` and
  `GetGlitter` deleted); the shader is unchanged — each Sierpinski triangle is a glitter shard about its own
  centroid, and the shatter kind stays in the shader as its fallback for an unknown kind.
- **The lab**: the Destruction line-up is the explosion and depths 1–4; round 3's ladder is in the Archive tab.

### Scorecards (shipped settings)

**Squint against the explosion** (band 0.097, held whole 0.315): depth 1 0.320, depth 2 0.309, depth 3 0.303, depth 4
0.306 — closer than round 3's shatter (0.346) and quadtree glitter (0.318–0.322), still not the band; depths 2–4 now
score below the never-breaking prism ("close").

**LOD steps** at each line's true pixel size (a 1,080 px screen; band = the deeper depth on another prism):

| Step | At | Prism px | Band | Step | Step ÷ band |
|---|---|---|---|---|---|
| Depth 4 → 3 | 24% | 259 | 0.923 | 0.511 | 0.55 |
| Depth 3 → 2 | 16% | 173 | 0.915 | 0.663 | 0.72 |
| Depth 2 → 1 | 10% | 108 | 0.919 | 0.733 | 0.80 |
| Depth 1 → the explosion | 5% | 54 | 0.879 | 0.864 | 0.98 |

Sheds unchanged from round 3. The same limit applies: a destruction band is two different deaths, near its ceiling.

### Found

1. **The explosion IS depth 0, exactly.** Depth 0 of the cut, placed on the six faces, is the same 24 triangles as
   `Prism.asset` (parity.py) — so the ladder's first rung needs no new effect at all.
2. **The first depth is uniform.** Depth 1 is 16 equal triangles a face (the middle and the three corners of a wedge
   are the same size); the range of sizes starts at depth 2. If the first rung should already vary, the split could
   keep a corner whole as well as the middle — a design call.
3. **The motion is still the glitter's, not the explosion's.** Every depth's shards drift and spin about their own
   centroids; the explosion's wedges slide and turn on its graph. That — not the cut — is what keeps every depth from
   the squint band (size 0.79–0.82). Carrying each shard on its root wedge's explosion motion (round 1's archived
   proposal) would close it, now that every shard has a root wedge.

### Checked

- `parity.py` — **PASS**: the cut against `PrismSierpinskiTiling.cs` compiled with .NET, 2,172 face triangles over
  depths 0–5, 0 differ; depth 0 = Prism.asset's 24 wedges; the page's depth meshes cover the surface (area 6, the C#'s
  counts); shed meshes 0 straddles; the burst (108,000 vertices, 0 outliers) and the explosion; controls FIRED (the
  next depth's cut 6/6, next kind 76.7%, ExplosiveRotation ×2 100%).
- The cut's properties run under .NET (every depth covers the face once, counter-clockwise; every deeper triangle in
  exactly one shallower): 0 failures.
- `verify_prism_collision_burst.py` PASS (shader unchanged); `verify_lab.cjs` PASS (screenshots read);
  `build.py --check` OK; `unity_refcompile` player + editor — 0 errors in project code.
- **Not run:** the Unity editor, so `PrismCollisionBurstTests` — filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

### Decision needed (designer)

1. **The depth lines** (5% / 10% / 16% / 24%) and the **deepest cut** (4 = 2,904 fragments; 5 = 8,736 is available).
2. **Depth 1's uniformity** — keep it, or vary the first split.
3. **Carry the shards on the explosion's motion?** The cut now gives every fragment a root wedge; riding it would make
   every depth move as the explosion does, with the shard detail on top.

## Round 3 — 2026-10-14 (`cece/dreamy-pascal-7xxx1m`): glitter sizes, shed resolutions, the amount — superseded where round 4 differs (the shatter and the quadtree glitter left the ladder)

**Your decisions applied** (the designer, in the session): *"lets have glitter create a range of fragment
sizes that are triangles of different sizes. then from far away it will look like we just aren't seeing the
small ones. then you can grade the effect with more LOD based on distance and amount."* and *"the steal
effects are looking great, but these should have even higher resolution versions that also grade LOD."*

### What changed, in the game and in the lab

- **Glitter in three sizes** (`PrismGlitterTiling`, `HighPolyPrismMesh.GetGlitter`): each face is a 16 × 16
  grid cut by a quadtree — 4-blocks that may split into 2-blocks that may split into cells (60% each), every
  block two right triangles across a diagonal its hash picks — so the shards are triangles with legs of 1/4,
  1/8 and 1/16 of a face. The levels are NESTED: GLITTER LARGE is the sixteen 4-blocks a face (192
  triangles), MEDIUM the tree cut at 2 (528), FULL the whole tree (1,386). A coarser level is the finer with its
  small fragments merged back into the triangle they came from, so every level is the whole prism at rest (no
  holes, no pop) and, far away, reads as the finer one with the small fragments too small to see. A far
  glitter draws a seventh of the triangles a near one does.
- **Sheds at three resolutions** (`HighPolyPrismMesh.GetShed`): 12, 18 and 24 subdivisions (1,728 / 3,888 /
  6,912 triangles), multiples of 6 so the unravel's ribbons (now 6 a face, was 5) and the flake's scales fall
  on whole triangles. The shed meshes are cut UNION-JACK: the off-diagonal quadrants' cells take the other
  diagonal, so both face diagonals are triangle edges and the bloom's petals tear along straight lines — on
  the old uniform cut one of the two diagonals was a staircase, plain to see close up at 24.
- **Graded by distance AND amount** (`PrismInteractionLod`): destruction explosion → shatter → glitter large →
  medium → full at 5% / 12% / 17% / 24% of the screen height; stealing grade → shed 12 → 18 → 24 at 8% / 16% /
  28%. The AMOUNT: once the budget (entities, or the new TRIANGLE budget of 80,000) is half full every new event
  is one level coarser, at 80% two; a level whose mesh does not fit steps down until one does, then the
  ordinary effect. Each mesh is its own spawn batch.
- **The lab**: the Destruction line-up is the whole spectrum (explosion, shatter, glitter large, medium, full);
  the Stealing view draws its sheds at the resolution the size and amount pick (or one you force, Sheds tab);
  a "Budget already full" slider shows the amount at work; the Levels tab holds every line. **Score the LOD
  steps** renders each pair of neighbouring levels at the true pixel size of the line between them.

### Scorecards (shipped settings)

**Squint, destruction against the explosion** (band 0.097, held whole 0.315): shatter 0.346; glitter large
0.322, medium 0.319, full 0.318. **The three glitter levels squint the same** — merging keeps the cover.
**Squint, sheds against the grade** (band 0.033, snapped 0.158): at 12 and at 24 subdivisions bloom 0.213 /
0.215, unravel 0.222 / 0.223, flake 0.118 / 0.117 — **resolution does not change the squint**.

**LOD steps** (`__lab.runSteps()`; a 1,080 px screen; the band is the finer level on another prism):

| Step | At | Prism px | Band | Step | Step ÷ band |
|---|---|---|---|---|---|
| Glitter full → medium | 24% | 259 | 0.974 | 0.489 | 0.50 |
| Glitter medium → large | 17% | 184 | 0.977 | 0.624 | 0.64 |
| Glitter large → shatter | 12% | 130 | 0.973 | 1.141 | 1.17 |
| Shatter → explosion | 5% | 54 | 0.938 | 0.884 | 0.94 |
| Shed 24 → 18 (bloom) | 28% | 302 | 0.492 | 0.035 | **0.07** |
| Shed 18 → 12 (bloom) | 16% | 173 | 0.462 | 0.058 | **0.13** |
| Shed 12 (bloom) → grade | 8% | 86 | 0.412 | 0.536 | 1.30 |

Every step is within 1.5× its band. Read it with its limit: a destruction band is two DIFFERENT deaths of one
level, which differ almost completely up close (~0.97), so for destruction "within the band" only says a level
change is no more different than another death. The strong results are the glitter's merges (half to two
thirds of the band: a merged glitter differs from the full one less than two full glitters differ) and the
shed resolutions (a fourteenth and an eighth of the band: the extra resolution is invisible at the size where it
switches on — and visible only as smoother curls up close).

### Found

1. **A uniform triangulation staircases one face diagonal.** Every cell cut the same way puts the main diagonal
   on triangle edges and the anti-diagonal across them, so a bloom petal's border on that side was a staircase —
   at 24 subdivisions, up close, exactly where the finest shed is drawn. Fixed in the shed meshes (union-jack);
   the shatter's mesh keeps the slice's cut, which its Voronoi does not care about.
2. **Holes would have popped.** Dropping the small glitter fragments outright (the first reading of "not seeing
   the small ones") leaves the prism with holes on its first frame. Merging them into their parents is the same
   picture far away with nothing missing.
3. **A 24-subdivision shed is 6,912 triangles**, more than the old per-entity worst case (1,200), so an entity
   count alone could no longer bound the cost: the budget is now triangles too, and the levels step down to fit.

### Checked

- `python3 Tools/Build/prism_collision_burst_lab/parity.py` — **PASS**: the burst on 108,000 vertices (0 past
  tolerance), the explosion; **the page's glitter tiling against `PrismGlitterTiling.cs` compiled with the
  .NET SDK — 1,053 blocks, 0 faces differ**; the page's glitter meshes cover the surface (area 6, the C#'s
  triangle counts) and its shed meshes have 0 triangles across either diagonal; controls FIRED (next face's
  tiling 18/18, next kind 76.7%, ExplosiveRotation ×2 100%).
- The tiling's own properties run under .NET (every level tiles every face once; every fine block in exactly one
  coarse block): 0 failures. The union-jack rule checked for n = 10, 12, 18, 24 (0 straddles; odd n cannot avoid
  its centre cell, and every shed n is a multiple of 6).
- `python3 Tools/Shaders/verify_prism_collision_burst.py` — PASS (unchanged shader). `verify_lab.cjs` — PASS.
  `build.py --check` — OK. `unity_refcompile` player + editor — 0 errors in project code.
- **Not run:** the Unity editor, so `PrismCollisionBurstTests` (now also the tiling, the glitter and shed meshes,
  the ladders and the amount) — filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

### Decision needed (designer)

1. **The lines**: glitter large / medium / full at 12% / 17% / 24%, sheds 12 / 18 / 24 at 8% / 16% / 28% — zoom
   the stage and watch the ◆.
2. **The amount**: one level coarser at half the budget, two at 80% — and is 80,000 triangles the right budget?
3. **The glitter's odds** (60% split at each level) set how many small fragments there are; the round-2
   decisions on the sheds' silhouette and destruction's shape gap stay open.

## Round 2 — 2026-10-14 (`cece/dreamy-pascal-7xxx1m`): the detail levels — superseded where round 3 differs

**Your decisions applied** (the designer, in the session; the shared log was empty): no white edge on the
dissolves or the glitter — the brighter inside colour is the contrast; shatter, glitter and the explosion
are one spectrum for destruction; bloom and unravel shed for stealing, revealing the new colour inside; a
level of detail for these fundamental interactions across the game; other shedding effects that look like
the grade from a distance. Round 1's geometry is kept ("good geometrically"); its carried proposal moved
to the Archive tab.

### What changed, in the game and in the lab

- **The shader** (`PrismCollisionBurst.hlsl` / `.shader`): the white accents are gone (`_HotColor`, ember,
  glint, crack flash, `_BurstFade`); every kind's back faces wear the inside colour; a fifth kind, **FLAKE**
  (scales tipping off in a wave down the long axis); a second fade curve, the **grade's smoothstep**, for
  sheds; the inside colour as a stamp so a shed's inside is the NEW colour.
- **The game** (`PrismInteractionLod`, `PrismInteractionLodConfigSO`): every explosion death picks
  explosion / shatter / glitter by the prism's size on screen (5% / 15% of the screen height); every steal
  picks the grade or a shed (from 8%), and an accepted shed snaps the prism to its new colour under a skin
  in its old colours. One budget (48), past it the ordinary effect. `PrismMotionConfig` keeps only the
  ladder; a collision above level 1 asks for the plain explosion.
- **The lab**: three views — **Destruction** (explosion, shatter, glitter on one flight), **Stealing** (the
  grade beside the three sheds, a "stolen by" domain), **Collision** (drawn at the level the camera
  distance picks). The HUD says the prism's size on screen and what the game would draw there; the ◆
  marks it, and moves as you zoom. Tabs: Levels, Shape, Kinds, Sheds, Look, Scene, Proposal, Archive.

### Scorecard (shipped settings)

`__lab.runBatch()` (Score the squint; reproduced by `verify_lab.cjs`). The squint now blurs all three
colour channels, not luminance alone, because a steal is a change of HUE. Each table has a band and a
negative control.

**Destruction, against the explosion** (15 times over 7.25 s):

| | size | centre | fade | colour | **squint** | verdict |
|---|---|---|---|---|---|---|
| Explosion, another prism (band) | 1.13 | 0.21 | 0.10 | 0.012 | **0.097** | the band |
| Prism held whole (negative) | 0.12 | 0.53 | 0.59 | 0.002 | **0.315** | fails, as it must |
| Shatter | 0.79 | 0.50 | 0.23 | 0.115 | **0.346** | reads as different |
| Glitter | 0.80 | 0.47 | 0.20 | 0.109 | **0.303** | close |

**Sheds, against the grade** (jade stolen by ruby, 15 times over the 0.8 s grade):

| | size | centre | fade | colour | **squint** | verdict |
|---|---|---|---|---|---|---|
| Grade, 10% late (band) | 1.00 | 0 | 0 | 0.015 | **0.033** | the band |
| Colour snapped (negative) | 1.00 | 0 | 0 | 0.076 | **0.158** | fails, as it must |
| Bloom | 1.32 | 0.03 | 0.24 | 0.045 | **0.214** | reads as different |
| Unravel | 1.35 | 0.06 | 0.25 | 0.048 | **0.220** | reads as different |
| Flake | 1.10 | 0.04 | 0.04 | 0.032 | **0.120** | close |

**The shed PROPOSAL** (lab-only dials, not in the game: a shed's lift and curl as a share of the shared
spread and turns):

| lift × / curl × | Bloom | Unravel | Flake |
|---|---|---|---|
| 1 / 1 (shipped) | 0.214 | 0.220 | 0.120 |
| 0.2 / 0.5 | 0.112 | 0.186 | 0.109 |
| 0 / 0.35 | 0.081 | 0.166 | 0.104 |
| 0 / 0.2 | 0.069 | 0.138 | 0.104 |

### Found

1. **No white helped the glitter's colour, not the shape.** The glitter's colour difference from the
   explosion went 0.187 → 0.109 (the same measure in both rounds; the squint blur changed from luminance to
   three channels, so round-1 and round-2 squint numbers are not comparable). What still separates both
   destruction levels from the explosion is SHAPE: the cloud is ~0.79 of the explosion's screen and its
   pieces go sooner (fade gap 0.20–0.23).
2. **A shed that leaves the silhouette cannot look like a grade.** Bloom and unravel, at the destruction's
   spread and curl, grow the prism's silhouette by a third and score further from the grade than an instant
   colour snap does. Kept on the prism (no lift, a third of the curl) the bloom reaches 0.069 — still above
   the strict band (0.05), well under the snap.
3. **The flake reads as a WIPE.** Its coverage follows the grade's curve exactly (it is the same
   smoothstep), but the old colour leaves from one end to the other, not everywhere at once; no dial moved
   it below 0.104. Whether a wipe is acceptable as "the grade from a distance" is a design call.
4. **The unravel's ribbons run the long axis**, so on a 4 : 0.6 : 2 slab they are long levers: even at a
   fifth of the curl they stick out (size 1.19, squint 0.138).
5. **A harness tolerance broke on a draw it had never seen.** Adding the flake shifted the shader harness's
   random sequence, and the unravel's isometry check failed at 2.2e-3 against a fixed 2e-3: float32 noise of
   40-unit coordinates over a 5e-4 step. Fixed by giving the check the same noise floor its sibling normal
   check had (the shader was right).

### Checked

- `python3 Tools/Shaders/verify_prism_collision_burst.py` — **PASS**: 5 kinds, 0 failures; negative controls
  FIRED: overshoot, uniform value, envelope, cheap normal, **linear shed** (a shed off the grade's curve);
  the shed's progress equals the executed shipped `PrismColorLerp_float` to 1e-4 at three grade lengths;
  glslang compiles every pass.
- `python3 Tools/Build/prism_collision_burst_lab/parity.py` — **PASS**: 108,000 vertices over 30 cases
  (5 kinds, sheds on the grade's curve), worst 5.16e-4 radii / 1.91e-4 normal, 0 past tolerance; the
  explosion 720 vertices, worst 4.59e-4; both negative controls FIRED (76.7%, 100%).
- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/PrismCollisionBurstLab.html` — **PASS**;
  desktop and phone screenshots read.
- `build.py --check` — OK. `bash Tools/Build/unity_refcompile/run.sh` — player and editor configs: 0 errors
  in project code. `check_console_logging.py`, `check_conditional_compilation.py` — clean.
- **Not run:** the Unity editor (no `unity` binary in this session) and so `PrismCollisionBurstTests` —
  filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

### Decision needed (designer)

1. **The sheds:** keep bloom and unravel as they are (spectacular, but they read as their own effect),
   or graduate the shed-only lift and curl (a per-shed stamp channel; bloom at lift 0 / curl 0.2 ≈ 0.07)?
   Is the flake's wipe "the grade from a distance"?
2. **The destruction spectrum's lines:** 5% (shatter) and 15% (glitter) of the screen height, 8% for a
   shed — zoom the stage and watch the ◆.
3. **Destruction's remaining gap is shape** (size 0.79, pieces gone sooner). Close it (spread, a later
   fade), or accept that a close-up death reads as a richer explosion.

## Round 1 — 2026-10-14 (`cece/dreamy-pascal-7xxx1m`) — superseded by round 2

### What it shows

- **Line-up:** the explosion and the four kinds, five prisms struck at once, flying the same flight
  (closing speed 60 → 20 u/s after the ⅓ restitution); the camera rides with them. Scrub, rate, replay
  (each replay is a new seed, so new shards).
- **Collision:** a mover runs into a resting prism; each picks its kind from its own pose, as the game does.
- **Squint** (header): blurs the stage, ~0.4 prism radius.
- **Carry on the explosion (proposal):** the burst's detail riding the explosion's own wedges instead of
  its own envelope. Labelled a proposal; not in the shipped shader.
- **Ladder** tab: the level for any mover count, with the hysteresis traced.
- Settings tabs for every burst and look dial; a changed value is underlined and the header counts the diffs.

### Scorecard (shipped settings)

Measured with **Score the squint** (`__lab.runBatch()`, reproduced by `verify_lab.cjs`): each candidate
drawn alone at 120×80, 15 times through its life, blurred, against the explosion at the same moment;
three prisms per kind. Band = the explosion of another prism (how much two explosions differ).

| | size | centre | fade | colour | **squint** | verdict |
|---|---|---|---|---|---|---|
| Explosion, another prism (band) | 1.13 | 0.21 | 0.10 | 0.012 | **0.082** | the band |
| Prism held whole (negative control) | 0.12 | 0.53 | 0.59 | 0.002 | **0.261** | fails, as it must |
| Shatter | 0.79 | 0.50 | 0.23 | 0.110 | **0.329** | reads as different |
| Bloom | 0.91 | 0.39 | 0.16 | 0.198 | **0.467** | reads as different |
| Unravel | 0.69 | 0.40 | 0.22 | 0.181 | **0.325** | reads as different |
| Glitter | 0.80 | 0.47 | 0.20 | 0.187 | **0.341** | reads as different |
| Shatter · carried (proposal) | 0.85 | 0.57 | 0.21 | 0.113 | 0.238 | close |
| Bloom · carried (proposal) | 0.88 | 0.48 | 0.16 | 0.189 | 0.245 | close |
| Unravel · carried (proposal) | 0.77 | 0.57 | 0.20 | 0.183 | 0.235 | close |
| Glitter · carried (proposal) | 0.86 | 0.38 | 0.15 | 0.177 | 0.252 | close |

"Squints as the explosion" = squint ≤ 1.5 × the band (0.123).

**With the accents tamed** (emberWhite 0, emberGlow 1, glint 0, heatSeconds 0.05, innerGlow 0, overshoot
0.176 so the fade ends with the explosion's) the shipped bursts measure 0.196–0.208 and the carried
ones 0.145–0.178; adding spread 0.25, carried Glitter reaches **0.123 — the only candidate that passes**
(Shatter 0.125, Bloom 0.140, Unravel 0.175). Colour drops from 0.11–0.20 to ~0.05 in both.

### Found

1. **The shipped bursts do NOT squint as the explosion.** Every kind scores 4–6× the band and reads as a
   different effect — worse than a prism that never breaks at all (0.261). Three causes, each measured:
   **colour** (the white-hot cracks, embers, glints and inner glow brighten the cloud: 0.11–0.20 against
   0.012), **size** (the burst covers 0.69–0.91 of the explosion's screen), and **fade end** (the
   explosion's last wedges linger; the burst's fade is linear to its own end).
2. **The docs' description of the explosion was wrong.** `PRISM_ANIMATION.md` §4.9 said the explosion's
   four wedges per face "spin in place". The shipped subgraph slides the pivot outward along the face
   tangent by `A·ExplosiveSpread/2` (A = speed·t) — linearly with age — while the wedge turns by
   `A·ExplosiveRotation`. The burst envelope was designed against the remembered description; the lab
   baseline is the shipped graph, evaluated. Corrected in §4.9.
3. **Three portability defects in the shipped `PrismCollisionBurst.hlsl`,** found by the parity gate
   running it on the GPU, fixed in the HLSL (the C++ verifier still passes, all four controls firing):
   - **Bloom petal ties:** a triangle centroid exactly on a face diagonal (|u| = |v|) picked its petal by
     float noise, ~80 vertices per case ~1 radius off. Now `ax >= ay - PRISM_BURST_PETAL_TIE` (1e-4).
   - **Unravel at small age:** `(1 − cos a)/κ` cancels in float32 (2.2e-3 off). Now `2 sin²(a/2)/κ`.
   - **Glitter centroids:** the lab first built them in doubles; C# builds them in float. Both now float.

### Checked

- `python3 Tools/Build/prism_collision_burst_lab/parity.py` — **PASS**. The page's burst vertex shader,
  read back by WebGL2 transform feedback, against C++ executing the shipped HLSL on the same mesh:
  86,400 vertices over 24 cases (4 kinds), worst 4.65e-4 radii / 1.91e-4 normal, **0 past tolerance (no
  outliers allowed)**. The explosion against the shipped subgraph evaluated per vertex: 720 vertices over
  10 cases, worst 7.34e-4. Negative controls FIRED: the next kind (75% of vertices differ), ExplosiveRotation ×2 (100%).
- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/PrismCollisionBurstLab.html` — **PASS**
  (desktop 1600×900 and iPhone 13; screenshots read: no overlap, line-up framed at every seed).
- `python3 Tools/Build/prism_collision_burst_lab/build.py --check` — OK.
- `python3 Tools/Shaders/verify_prism_collision_burst.py` — PASS after the HLSL fixes.
- The squint scorecard says NO: the held-whole prism fails (0.261), the band is 0.082.
- Found while verifying: the line-up camera ignored the seed's offset, so after any Replay the stage
  was empty. Fixed before publishing.

### Not modelled (also on the page, About tab)

Bloom/HDR and tonemapping; the explosion's own shader colouring (approximated with the burst's skin
shading so colour differences are the accents, not the lab); the occlusion corridor; contact detection
(the collision view touches at the x-extents); material tiers (one domain pair at a time); float32
clock precision late in a session and phone GPUs. Nothing here has run in the Unity editor.

### Decision needed (designer)

1. **Graduate the "carried on the explosion's wedges" proposal?** It closes about a third of the gap
   alone (0.33–0.47 → 0.24–0.25) and keeps every kind's character up close.
2. **Tame the accents?** The white-hot cracks, embers, glints and inner glow are what make the bursts
   spectacular AND what make them read differently squinting (colour Δ 0.11–0.20 → ~0.05 without them).
   Options: lower them, make them briefer, or accept that level 1 reads differently.
3. **End the fade with the explosion's** (overshoot 0.176 on the page)?

Record a pick with **Decisions** on the page; the next round opens by reading it.
