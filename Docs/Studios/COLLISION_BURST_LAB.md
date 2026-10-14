# Collision Burst Lab

The four level-1 **collision bursts** (shatter, bloom, unravel, glitter — `Docs/PRISM_MOTION.md` §3a,
`Docs/PRISM_ANIMATION.md` §4.11) side by side with the **ordinary prism explosion**, running the
shipped shader code in WebGL2, with a measured squint scorecard and a shared decision log.

- **Live page:** https://claude.ai/artifact/F47yG1UjBkK662rEnhk7m9 (decision log: `decisions`)
- **Page (generated, never edit by hand):** `Docs/Studios/PrismCollisionBurstLab.html`
- **Source:** `Tools/Build/prism_collision_burst_lab/lab.src.html`; `build.py` stamps the shipped
  numbers and shader code into it (`--check` fails when the page is stale).
- **Parity gate:** `Tools/Build/prism_collision_burst_lab/parity.py`

The question the lab exists to answer is the brief's: **spectacular when you look at it, the ordinary
explosion when you squint.** One column per part of that: size, centre, fade, colour, and the RMS of the
blurred difference ("squint"), each against the explosion of the same prism at the same moment.

## What the page reads (it is a reader, never an authority)

`build.py` copies, at build time, from the assets — every value on the page names its source:

| From | What |
|---|---|
| `Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl` | the whole burst — translated mechanically to GLSL (types, `frac`/`lerp`, comments), not re-written |
| `PrismOcclusionCorridor.hlsl` | `PrismErosionFade_float`, its hash and `PRISM_EROSION_*` constants (the explosion's fade) |
| RotateFacesAlongAxis / TangentSlider subgraphs | the explosion's face pivot (asserted −0.5 / 0.5 / 0.5 from the graph's node slots) |
| `ExplodingBlockMaterial.mat`, `PrismExplosion.prefab`, `PrismExplosion.cs` | `_ExplosiveRotation` 0.0169, `_ExplosiveSpead` 0.05, `_Spread` (0.18, 0.1, 0.1), speed 10–33.33, 7.5 s life |
| `Prism.asset` | the explosion's 72-vertex wedge mesh, byte for byte |
| `PrismMotionConfig.asset`, `PrismMotion.cs` | burst dials, kind weights, the 64/48 and 256/192 ladder, debris restitution ⅓ and limit 200 |
| `PrismCollisionBurstMaterial.mat`, `OriginalColorSetSO` | the burst's look dials; Jade/Ruby/Gold/Blue inside/outside colours |

The burst mesh is `HighPolyPrismMesh.GetUnwelded(10)` rebuilt on the page in float32 (`Math.fround`), as
C# builds it.

## Round 1 — 2026-10-14 (`cece/dreamy-pascal-7xxx1m`)

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
