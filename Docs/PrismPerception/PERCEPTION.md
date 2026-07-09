# Prism Constructs & Perception

**Branch:** `claude/prism-constructs-perception-my7fg7` · **Status:** proof-of-concept / exploration

> *A printed page fools the eye into a full colour space using three inks in a dot matrix. Our board
> has three domains and one extra dimension to spend. What can be **tricked into perception** — colour,
> value, form, image, motion — purely by how prisms are arranged in 3D?*

This is the exploration that followed the Gaussian-splat reference work: instead of *reconstructing*
splats, it asks what the three **domains** can *synthesise* by arrangement alone. It composes existing
fundamentals only — **prisms are the dots, domain is the ink** — and never adds a colour channel, a
decay, or a timer.

---

## The pivotal finding: the domains glow near-CMY

The single most important fact, and it is not obvious from the code: **a prism does not glow in its
team colour.** The block shader is emissive with an HDR `_BrightColor` key, and those values (in
`_SO_Assets/Color Palettes/OriginalColorSetSO.asset`, `*.InsideBlockColor`) are:

| Domain | `InsideBlockColor` (linear HDR) | Reads as | ≈ print primary |
|---|---|---|---|
| **Jade** | `(0, 0.588, 1.135)` | **azure-cyan** | C |
| **Ruby** | `(0.549, 0, 1.498)` | **violet / magenta** | M |
| **Gold** | `(1.498, 0.668, 0.089)` | **amber** | Y |

*(The `TrailHighlightColor`s are an even cleaner cyan / magenta / amber.)*

So the three domains are, physically, a **near-CMY print triad** — but rendered in **additive light**
(HDR emission + URP Bloom on a black ground), i.e. **partitive** mixing, exactly like pointillism or a
halftone viewed at distance. The user's analogy is not a metaphor; it is literally what the hardware does.
This is *why* dithering the three domains reaches colours none of them own.

> Consequence: verify every swatch against a **Bloom-enabled URP scene**, never against the `Domains`
> enum names. `Jade == green` is a naming convention, not the emission.

---

## The gamut, measured honestly

Additive mixing of three primaries reaches the **triangle** they span in chromaticity; density/coverage
sets value. Computed from the real primaries above:

- **Size:** the reachable triangle is **≈ 19 % of the sRGB gamut** — a *compact* wedge, not the rainbow.
- **Spans:** azure-cyan → violet → amber. **Reachable:** blues, teals, periwinkles, purples, plums,
  ambers, and the desaturated mixes between them. (The hero swatch defaults to **periwinkle blue** —
  a hue *none of the three domains own*, made from azure + violet.)
- **Outside the wedge:** saturated **pure green** (there is no green primary — only cyan and amber
  flank it) and **pure spectral red**; both desaturate as you push for them.
- **No true neutral white:** an equal mix lands at a **cool lavender** (D65 sits just outside the
  triangle), so any "grey" reads faintly cold — the mirror image of a warm-ink print set.

> ⚠️ Earlier analysis in this exploration assumed a warm *green/red/gold* triad and concluded a "warm
> wedge, no blues, warm-khaki neutral." That was computed for the wrong primaries. With the **real**
> emission the bias is **cool**, blues/purples are the easy colours, and **green** is the hard one.

---

## Five axes you can encode

| Axis | Knob | Perceived as |
|---|---|---|
| **Ratio** | domain mix per region (dither) | hue & saturation (partitive) |
| **Density** | prism count / size (coverage `k`) | value / luminance, at constant hue |
| **Arrangement** | where prisms sit | form, depth, a continuous surface |
| **Orientation** | which domain face points where | view-dependent colour (no 2D analogue) |
| **Time** | flicker a prism between domains | mixing in time (PWM) — same gamut, zero grain |

---

## The demo tour (interactive bench)

`Docs/PrismPerception/prism-perception-lab.html` — a self-contained interactive optical bench (Canvas
2D, additive `lighter` compositing = the real partitive physics, no external libraries). Eight stations,
simplest → most mind-bending:

0. **Partitive Gamut** — drag a target in the domain triangle; a 3D dot field retunes its dither and
   fuses to that hue. The thesis image.
1. **Value from binary dots** — one domain, density-modulated into a smooth Lambert-shaded sphere.
2. **Surface from a dot cloud** — a trefoil knot as sparse points reading as a continuous glowing
   surface (connect-the-dots → splat).
3. **Full-colour image from three** — 3D Floyd–Steinberg error-diffusion reproduces a picture; up close
   it's grain, at range it fuses.
4. **View-dependent colour** *(3D-only)* — angularly-gated prisms show a Jade, a Ruby, and a Gold word
   from three viewpoints in the same volume.
5. **Moiré depth & motion** — two detuned domain lattices beat into fringes that glide as you orbit;
   motion from a static construct.
6. **Temporal (PWM) mix** *(beyond spatial grain)* — a flickering panel fuses to the same colour as a
   spatial-dither panel, with no dot texture.
7. **Sightline** *(3D-only)* — an anamorphic cloud that collapses into a crest from **one** viewpoint
   and scatters to noise from every other.

Two more, considered and *not* built as stations (honesty): **Impossible Blue** (simultaneous-contrast
nudges a patch to a hue outside the wedge — but only in the eye, it does not survive a screenshot) is
noted in the Limits section; **Chameleon Totem** is the multi-band generalisation of station 4.

---

## Correctness gotchas (do not skip)

- **Mix in LINEAR light.** Decode sRGB → linear before averaging/diffusing error, or dark-value mixing
  is wrong. (`Mathf.GammaToLinearSpace`.)
- **Reachability test before every dither.** Solve for the barycentric weights; if any is negative the
  target is out of the wedge — **gamut-map first** (project to the nearest edge), never feed a negative
  area fraction to the dither. The POC generator does this via a coarse simplex search that *clamps* by
  construction.
- **A perceptually-even value ramp is cubic** in coverage (`k = ((L*+16)/116)³`), not linear.
- **3D dither must be isotropic** (3D void-and-cluster / serpentine error-diffusion on all axes) or it
  shows worms/gradients when orbited off its optimised axis.
- **Fusion is distance-bound** (< ~1 arc-minute subtense). Every "resolves into an image" claim is
  range-bound; too close, it is honestly just dots.
- **Perceptual tricks don't survive a screenshot or a colour-picker** — they live in the viewer's eye,
  are a few ΔE, and evaporate when the inducing surround leaves the field of view. Never ship one as
  emitted colour.
- **Scene prerequisites:** a `ThemeManager` + `ThemeManagerDataContainer.asset` must exist (else
  `Prism.ChangeTeam` throws), **and a URP Bloom volume is required** — the HDR emission is what makes
  the additive mix read at all.

---

## In-engine generator

`Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs` — a real `MonoBehaviour` that
builds these constructs through the **canonical** prism path (`PrismTrailBuilder.LayBatched` →
`LayOne`: Instantiate → `ChangeTeam` → pose → `Initialize` → bloom-in). It lays ordinary
`SpawnablePrism` prisms, one `Domains` per prism, and lets the domain emission + Bloom do the mixing —
no new primitive, no decay, no timer (continuity + mass-conservation laws hold).

**Setup:** add the component to an empty GameObject in a scene that has a `ThemeManager` and a URP Bloom
volume, assign `_Prefabs/Trails/SpawnablePrism.prefab`, pick a mode, press **Build** (context menu) or
enable *Build On Start*.

| Mode | What it lays |
|---|---|
| **PartitiveVolume** | fills a Sphere/Box/Disc with a blue-noise dither of the three domains solved (in linear light, gamut-clamped) to a chosen **target colour** |
| **SplatSurface** | a trefoil-knot point cloud that reads as a continuous glowing surface, colour-swept along its length |

**Collider budget:** every prism carries a trigger collider, so a field is bounded by the same per-cell
budget as any trail. Keep `count` in the low thousands, keep `LayBatched` (spawn never spikes a frame),
and treat a dense field as you would any dense prismscape.

---

## How this composes with the fundamentals

Per `CLAUDE.md` — *favour emergent systems, don't cheat emergence*:

- **Prisms / Prismscapes** — these constructs *are* prismscapes at a particular density. No new
  structure type; the "dots" are the existing primitive.
- **Domain** — the only colour source is Jade/Ruby/Gold material sets. The expanded gamut is **emergent
  from arrangement**, never a hand-picked shade or a fourth colour.
- **Mass** — a construct is a fixed stock of conserved prisms: it blooms in, holds, and is removed only
  by an active force. Nothing here needs decay or a cull timer.
- **Continuity of existence** — prisms bloom in via `Initialize`; `LayBatched` spreads the spawn so
  nothing pops.

It is a lens on prisms already in the game, not a bypass of them.
