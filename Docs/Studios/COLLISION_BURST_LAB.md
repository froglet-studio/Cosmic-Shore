# Collision Burst Lab

The prism's **interaction detail levels** (`Docs/PRISM_ANIMATION.md` §4.11) side by side with the ordinary
effects they stand in for, running the shipped shader code in WebGL2, with a measured squint scorecard and
a shared decision log: **destruction** (explosion → shatter → glitter) and **stealing** (the grade → a
bloom, unravel or flake shed). Round 1 looked at the four level-1 collision bursts alone.

- **Live page:** https://claude.ai/artifact/F47yG1UjBkK662rEnhk7m9 (decision log: `decisions`)
- **Page (generated, never edit by hand):** `Docs/Studios/PrismCollisionBurstLab.html`
- **Source:** `Tools/Build/prism_collision_burst_lab/lab.src.html`; `build.py` stamps the shipped
  numbers and shader code into it (`--check` fails when the page is stale).
- **Parity gate:** `Tools/Build/prism_collision_burst_lab/parity.py`

The question the lab exists to answer is the brief's: **spectacular when you look at it, the ordinary
effect when you squint** — the explosion for a destruction, the grade for a steal. One column per part of
that: size, centre, fade, colour, and the RMS of the blurred difference ("squint"), each against the
ordinary effect on the same prism at the same moment.

## What the page reads (it is a reader, never an authority)

`build.py` copies, at build time, from the assets — every value on the page names its source:

| From | What |
|---|---|
| `Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl` | the whole burst — translated mechanically to GLSL (types, `frac`/`lerp`, comments), not re-written |
| `PrismOcclusionCorridor.hlsl` | `PrismErosionFade_float`, its hash and `PRISM_EROSION_*` constants (the explosion's fade) |
| RotateFacesAlongAxis / TangentSlider subgraphs | the explosion's face pivot (asserted −0.5 / 0.5 / 0.5 from the graph's node slots) |
| `ExplodingBlockMaterial.mat`, `PrismExplosion.prefab`, `PrismExplosion.cs` | `_ExplosiveRotation` 0.0169, `_ExplosiveSpead` 0.05, `_Spread` (0.18, 0.1, 0.1), speed 10–33.33, 7.5 s life |
| `Prism.asset` | the explosion's 72-vertex wedge mesh, byte for byte |
| `PrismInteractionLodConfig.asset` | the size-on-screen lines, the budget, the shape, the shed weights and skin offset |
| `PrismClockAnimation.hlsl`, `MaterialPropertyAnimator.cs` | the steal's grade, `PrismColorLerp_float` (translated), and its 0.8 s length |
| `PrismMotionConfig.asset`, `PrismMotion.cs` | the 64/48 and 256/192 ladder, debris restitution ⅓ and limit 200 |
| `PrismCollisionBurstMaterial.mat`, `OriginalColorSetSO` | the look dials; Jade/Ruby/Gold/Blue inside/outside colours |

The burst mesh is `HighPolyPrismMesh.GetUnwelded(10)` rebuilt on the page in float32 (`Math.fround`), as
C# builds it.

## Round 2 — 2026-10-14 (`cece/dreamy-pascal-7xxx1m`): the detail levels

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
