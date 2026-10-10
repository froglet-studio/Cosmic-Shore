# Omni Shepard Lab

A browser lab for tuning the omni crystal's **Shepard triangles**: the shells of triangle plates that
stream in toward the crystal and land on its body, forever. Sliders for how many shells are in
flight, how many triangles each carries, where they launch, where they land, how fast they travel,
and how they are coloured. A trip chart, a scorecard and an export of the material values go with it.

- **Page (source):** `Docs/Studios/OmniShepardLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/NqWi3Ej4vxU3poSjp2HqG9 (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Shipped numbers + meshes:** baked in by `Tools/Build/omni_shepard_lab_assets.py`
  (`--check` fails when the assets move and the page has not been rebaked; `--self-test`)
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/OmniShepardLab.html`

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
