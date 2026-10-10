# Omni Shepard Lab

A browser lab for designing the omni crystal's **Shepard triangles**: the shells of triangle plates that
stream in toward the crystal and land on its body, forever. Sliders for how many shells are in
flight, how many triangles each carries, where they launch, where they land, how fast they travel,
and how they are coloured. Round 3 added 18 alternative styles, a random style generator, a
compare grid and shared likes. A trip chart, a scorecard and a "what it takes to ship" export go with it.

- **Page (source):** `Docs/Studios/OmniShepardLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/NqWi3Ej4vxU3poSjp2HqG9 (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Shipped numbers + meshes:** baked in by `Tools/Build/omni_shepard_lab_assets.py`
  (`--check` fails when the assets move and the page has not been rebaked; `--self-test`)
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/OmniShepardLab.html`
- **Round-to-round parity at the shipped settings:** `Tools/Build/omni_shepard_lab_parity.cjs <old.html> <new.html>`

## How the effect works (measured from the assets)

`Crystal.prefab` carries three `OmniCrystalTriangles` instances, each with its own
`OmniShepardTriangles {0,1,2}.mat` on `OmniShepardFresnelShader`. The shader scales the whole
20-plate triangle mesh about the crystal centre by `s`, where `s` sweeps `_Start → _Stop` once per
`_Period`, and draws it with `alpha = (1.05 − s) × _Opacity`.

| Layer | `_Start` | `_Stop` | `_Period` |
|---|---|---|---|
| OmniShepardTriangles 0 | 1.000 | 0.833 | 3 s |
| OmniShepardTriangles 1 | 0.833 | 0.667 | 3 s |
| OmniShepardTriangles 2 | 0.667 | 0.500 | 3 s |

The three bands are contiguous and share one clock, so when the period wraps, each layer jumps to
exactly where the layer outside it just was. One triangle seems to fly all the way in over
3 × 3 s = 9 s (the thick "baton" line on the trip chart). The one jump that would show, the innermost
layer vanishing at alpha 0.55, happens at `_Stop` 0.5, which puts every plate 0.17 u inside the body
face behind it (plate centroid 1.562 local × 0.892 × 0.5 × 10 = 6.97 u; body face 7.14 u), so the
opaque body hides it.

A fourth shell, `OmniShepardTrianglesRim`, does not scale (`_ScaleDistance` 0) and draws at alpha
0.02–0.07: a faint static outer skin.

## Round 3 (2026-10-10): many more looks

Feedback: "people don't like it, give me many more options". The decision log was empty, so this
round widens the space instead of tuning one look: a new effect model, 18 named styles, an endless
seeded random supply, a 3 × 3 compare grid, and shared likes and saves so the team can converge.

**What changed**
- **One generalised model.** Every shell copy loops over the whole trip and each triangle plate
  gets its own clock, path, spin and size. At the shipped knobs it collapses to exactly the three
  staggered material bands the game draws. Pixel parity against round 2: at most 10 of ~85k lit
  pixels differ by more than 2/255 (`omni_shepard_lab_parity.cjs`). The negative control (Swirl
  270°) changes 26k–80k pixels.
- **About 20 new knobs** in Stream / Motion / Shape / Light: direction (in / out / breathe), hover
  before the dive, stagger and its pattern (random, pole wave, sweep, spiral), swirl, tumble,
  scatter, shrink-with-distance, size at launch, thickness, fade style, blend (glass / glow),
  brightness, hue drift, landing flash, body pulse, trails.
- **Every knob says what it costs to ship:** a chip on each slider and each style reads
  *material values* (edit .mat / .prefab), *needs mesh*, or *needs shader*. The **Ship it** tab
  writes the material values, or the recipe plus what would have to be built.
- **Styles tab:** 18 styles, *Compare 1–9* / *10–18* / *9 random* / *Surprise me*, *Like*
  (shared through the page's `votes` collection), *Save to team gallery* (`styles` collection), and
  a *Most liked* ranking. Click a compare cell to open it in the editor.

**Scorecard** (seed 7, from `runBatch()`; "hidden" = that end of the loop cannot be seen to pop):

| Style | Ships as | Shells | Tris drawn | Trip (s) | Launch (u) | Seen from (u) | Speed in → land (u/s) | Visible tris (avg) | Launch hidden | Landing hidden |
|---|---|---|---|---|---|---|---|---|---|---|
| Shipped | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 57.5 | yes | yes |
| Swirl | shader | 4 | 640 | 9 | 6.8 | 6.8 | 1.14 → 0.34 | 71.7 | yes | yes |
| Tumbling shards | shader | 3 | 480 | 7.5 | 6.8 | 6.8 | 0.93 → 0.93 | 57.3 | yes | yes |
| Meteor rain | shader | 2 | 1280 | 2.4 | 6.8 | 6.78 | 2.9 → 2.9 | 38.1 | yes | yes |
| Pole wave | shader | 2 | 320 | 5 | 6.8 | 6.8 | 1.39 → 1.39 | 38.2 | yes | yes |
| Lighthouse | shader | 1 | 160 | 4 | 6.8 | 6.8 | 1.74 → 1.74 | 19.2 | yes | yes |
| Breathe | shader | 2 | 320 | 8 | 6.8 | 6.76 | 2.16 → 2.16 | 38.3 | yes | yes |
| Emitter | shader | 4 | 640 | 6 | 6.8 | 6.8 | 1.16 → 1.16 | 76.7 | yes | yes |
| Implosion | shader | 1 | 160 | 3.5 | 11.68 | 11.68 | 7.52 → 7.52 | 19.6 | yes | yes |
| Glow stream | shader | 5 | 800 | 8 | 6.8 | 6.8 | 0.87 → 0.87 | 95.4 | yes | yes |
| Sparkle | shader | 4 | 192 | 4 | 6.8 | 6.76 | 1.74 → 1.74 | 22.8 | yes | yes |
| Assemble | shader | 2 | 320 | 6 | 10.98 | 10.98 | 2.79 → 0.93 | 37.5 | yes | yes |
| Heartbeat | shader | 2 | 320 | 2.8 | 6.8 | 6.8 | 6.49 → 1.81 | 37.5 | yes | yes |
| Petals | shader | 3 | 480 | 10.5 | 6.8 | 6.76 | 0.66 → 0.66 | 57.1 | yes | yes |
| Vortex | shader | 3 | 1920 | 3.6 | 6.8 | 6.8 | 1.94 → 1.94 | 57.5 | yes | yes |
| Slabs | shader | 4 | 640 | 8 | 6.8 | 6.8 | 0.87 → 0.87 | 76.7 | yes | yes |
| Dense stream | material | 6 | 960 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 115 | yes | yes |
| Glass contrast | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 57.5 | yes | yes |
| One shell (baseline) | material | 1 | 160 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 19.2 | yes | yes |
| Lands short (control) | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.62 → 0.62 | 60 | yes | **no** |

**Found**
1. **Only three of the 18 looks ship as material values** (Shipped, Dense stream, Glass contrast).
   Everything that moves triangles independently (stagger, swirl, tumble, scatter, hover, glow,
   trails) needs an `OmniShepardFresnelShader` extension. That extension would ALSO collapse the
   crystal's three layer materials into one material with a copy index.
2. **The scorecard caught two of my own styles popping.** Breathe faded in but never out, so it
   vanished at full opacity at the far point (fixed: the envelope now fades at both ends for
   breathe). Implosion's halo was born at alpha 0.10 (fixed: fade line 1.40, birth alpha 0.05).
   All 18 now hide both ends.
3. Additive "Glow" styles brighten wherever triangles overlap the body. Without bloom the lab
   understates how they would read in game.

**Decision needed (designer / team):** open the page, use Compare, and Like what you would ship.
The next round starts from the *Most liked* list and the decision log. If the winner needs the
shader, that is the build ticket. If not, the Ship it tab has the material values.

**Checked:** `verify_lab.cjs` PASS (desktop + phone); `omni_shepard_lab_assets.py --check` OK;
pixel parity with round 2 at the shipped settings, plus its negative control; compare grids of
styles 1–9, 10–18 and random 1–9 read as screenshots; the scorecard's *Lands short* control still
fails.

## Round 2 (2026-10-10): the shells were never drawn

Reported by the designer on the live page: "zero shells, just the main crystal, and weird
artifacts in the center from misdrawn vertices". Both were lab bugs; the game was not involved.

**Found**
1. **No shell ever rendered in round 1.** The shell shader declared `uS` in both stages, at
   `highp` in the vertex shader (the GLSL ES default) and `mediump` in the fragment shader. WebGL
   refuses to link a program whose shared uniform precisions differ, so every shell draw was
   skipped. Chromium reports that as a console WARNING, and `verify_lab.cjs` failed only on errors,
   so round 1 passed. Its "both screenshots were read" check was also wrong: the crystal in them was
   the body alone. Fix: `precision highp float` in both fragment shaders, and `program()` now throws
   on a failed link, which the page logs as a console error.
2. **The body's centre drew wrong.** The page derived its own face normals and flipped each one
   "outward" by testing it against the face centroid. The body is 30 hollow, bevelled rhombus plates:
   122 inner faces point at the centre and 480 wall faces point sideways (measured from the FBX). The
   flip turned the inner faces around, and on the walls the sign of a near-zero dot product picked
   bright or dark at random. Fix: the baker now carries the FBX's authored per-corner normals
   (`LayerElementNormal`, `ByPolygonVertex` / `IndexToDirect`; Unity imports them because
   `normalImportMode: 0`), and the page uses them as they are.

**Checked**
- `verify_lab.cjs` now fails on WebGL `INVALID_*` / link warnings. Its `--self-test` has a fifth
  plant (a draw with an unlinked program), and all five plants are named.
- Negative controls on this lab: the round-1 page from git now FAILS the gate ("program not linked").
  The fixed page with only the shell precision reverted fails with "Precisions of uniform 'uS'
  differ". The fixed page PASSES.
- Screenshots read at 30 u (shells streaming in around the body), with the body hidden (shells
  only), and at 16 u with the shells off (body plates, walls and gaps clean; no stray triangles).
- `omni_shepard_lab_assets.py --check` OK, and `--self-test` 6/6.
- The round-1 scorecard is unchanged. It is computed from the meshes and the formula and never
  depended on the draw.

## Round 1 (2026-10-10)

**What changed:** the lab exists. Sliders (Shells / Motion / Look / View tabs), six presets, a live
readout, the trip chart, a scorecard, the material-value export, and the decision log.

**Every knob maps to an asset field or says it doesn't:**

| Slider | Field | Ships without code? |
|---|---|---|
| Shells in flight | count of `OmniShepardTriangles` layers on `Crystal.prefab` | Yes, but each new layer is a prefab instance + active/inactive material pair + `crystalModels` entry |
| Triangles per shell | — | No: needs a mesh variant of `OmniCrystalTriangles.asset` (the export lists which plates) |
| Launch height / Landing scale | outermost `_Start` / innermost `_Stop` | Yes |
| Shell size | `OmniCrystalTriangles.prefab` `m_LocalScale` | Yes |
| Period | `_Period` | Yes |
| Landing ease | unequal `_Start`/`_Stop` bands per layer | Yes: the bands stay contiguous, so the shipped shader runs it seamlessly |
| Opacity, Rim power, Face forward | `_Opacity`, `_RimPower`, `_FaceForward` | Yes |
| Fade line | the `1.05` constant in `OmniShepardFresnelShader` | No: shader edit, and it also moves the rim shell and the Mass crystal |

**Scorecard** (seed 7; produced by `window.__lab.runBatch()` / `metrics()` on the page, measured
2026-10-10 through `verify_lab.cjs` and a Playwright probe):

| Variant | Shells | Launch (u above body) | Seen from (u) | Trip (s) | Speed in → land (u/s) | Spacing (u) | Shells seen | Land gap (u) | Landing hidden |
|---|---|---|---|---|---|---|---|---|---|
| Shipped | 3 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 2.32 | 2.86 | −0.17 | yes |
| One shell (baseline) | 1 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 6.97 | 0.95 | −0.17 | yes |
| Lands short (control, `_Stop` 0.6) | 3 | 6.80 | 6.80 | 9 | 0.62 → 0.62 | 1.86 | 3.00 | +1.22 | **no** |
| Dense stream (6 shells, 1.5 s) | 6 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 1.16 | 5.72 | −0.17 | yes |
| Settle in (ease 2, 5 shells, 1.8 s) | 5 | 6.80 | 6.80 | 9 | 1.39 → 0.15 | 2.51 → 0.28 | 3.95 | −0.17 | yes |
| Launch 1.6 (fade unchanged) | 3 | 15.16 | **7.36** | 9 | 1.70 → 1.70 | 5.11 | 1.41 | −0.17 | yes |
| Launch 1.6, fade 1.65 | 3 | 15.16 | 15.16 | 9 | 1.70 → 1.70 | 5.11 | 2.93 | −0.17 | yes |
| Shell size 1.2 | 3 | 11.61 | 11.61 | 9 | 1.04 → 1.04 | 3.12 | 3.00 | +2.23 | **no** |

Landing scale sweep (everything else shipped): `_Stop` 0.45 → gap −0.87 u (hidden), 0.52 → +0.11 u
(hidden, inside the 0.15 u tolerance), 0.55 → +0.53 u (visible pop).

**Checked**
- `verify_lab.cjs`: PASS (no console errors, desktop 1600 × 900 fits with no scroll or clipping,
  iPhone 13 has no sideways scroll and reads as a phone, manual clock advances, `runBatch` is
  deterministic). Stage is WebGL, so the blank check is skipped; both screenshots were read.
- The baked block matches the assets: `omni_shepard_lab_assets.py --check` OK; `--self-test` passes
  its six checks, including two planted defects (a non-Shepard material, a marker named in prose).
- The body mesh's frame matches the triangle mesh's: all 48 signed axis permutations of the FBX were
  scored by how evenly the plates land on the body at `_Stop` 0.5; the identity is the best fit
  (spread 0.011 local units), so the page uses the FBX vertices as-is.
- The negative control reads as a failure: **Lands short** reports a visible pop.

**Found**
1. **Launch height is capped by the shader, not the material.** Above `s ≈ 1.04` a triangle's alpha
   is below the 0.01 clip, so raising `_Start` alone adds invisible travel: at 1.6 the triangles are
   still first seen 7.36 u above the body (shipped: 6.80) while flying 2.2× faster and spaced 2.2×
   wider. A visibly higher launch needs the fade constant raised, which is a shader edit shared with
   the rim shell and the Mass crystal.
2. **Shell size moves the landing too.** `m_LocalScale` 1.2 raises the launch to 11.6 u but lands
   2.2 u short of the body, so the hand-off becomes a visible pop. Raise the launch with `_Start`
   (and the fade line), not with the prefab scale.
3. **The landing window is narrow:** `_Stop` between about 0.45 and 0.52 hides the hand-off; 0.55
   already pops.
4. **Easing is free.** Unequal bands keep the hand-off seamless on the shipped shader, so "fast in,
   settle onto the body" needs only material values (and enough layers to look smooth).

**Decision needed (designer)**
- How dense and how fast should the stream be (shells × period)? Shipped is 3 shells, 9 s trip.
- Should the triangles launch from higher up? If yes, that is a shader edit (Found 1).
- Fewer triangles per shell (a sparser, shard-like stream) needs a new mesh; is that wanted?

Record the choice in the page's **Decisions** tab; the next round starts by reading that log.

## What this lab does not model

Bloom and the rest of URP post-processing; blending happens in sRGB with a 2.2 gamma approximation.
Transparent sort order (Unity sorts these shells by object origin, the same point for all of them).
ThemeManager domain recolours (only the active and inactive materials). The game camera's FOV, the
vessel, the crystal's collect/explode materials, FadeIn on respawn. Frame cost on a real GPU.

## Graduation

Material values come from the **Export** tab. Edit both the active and the `Inactive` material of
each layer. More than three layers means new `OmniShepardTriangles (n)` instances on
`Crystal.prefab` with their own material pairs and `crystalModels` entries; fewer means removing
them. After any asset change, rebake the lab (`python3 Tools/Build/omni_shepard_lab_assets.py`) so
its "shipped" column follows the assets.
