# Silence Vignette Lab

The lab where the team picks how a **silenced pilot's screen** looks (`Docs/ElementalAbilitySystem/SILENCE.md` §5).

- **Live page:** https://claude.ai/artifact/Hequw1VLdwXi2ztrDeuXQC (private to Garrett until shared from its Share menu)
- **Source:** `Docs/Studios/SilenceVignetteLab.html`. The repo copy is the source; republish from it to the same URL.
- **Game side:** `Assets/_Scripts/Controller/FX/SilenceVignetteMask.cs` (the shape), `SilenceScreenFeedback.cs` (the overlay), and `Assets/Resources/VesselSilenceConfig.asset` (every number, under *Vignette*).
- **Parity:** `bash Tools/Build/silence_vignette_parity/run.sh` checks that the lab's mask twin matches the C# mask and that the lab's `SHIPPED` block matches the asset.

## The question, as columns

The question: what should the danger-coloured edge look like so that a silenced pilot **sees it for sure**, while it still fits the game's art direction? The scorecard has one column per part of that question:

| Column | What it measures | Want |
|---|---|---|
| Edge · space | How much the edge changes the picture on open space, the Dog Fight case. This is the OKLab colour difference ×100, averaged over the outer band of a 480×270 frame. | high: it is seen |
| Edge · bright | The same over a bright, prism-filled scene | high: it is still seen |
| Edge · danger | The same over a field of danger prisms, which are the same red | high: it does not vanish into them |
| Centre | The same over the middle 30% of the frame | low: play stays clear |
| Red · space | The red added over the edge band on open space | high enough to say "danger" and not just "dark" |

Each column is averaged over seeds 1 to 3 at full strength, measured at the breath's peak. Breathing is not scored and is judged by eye.

## Round 1 (2026-10-21)

**What changed.**
- The first cut drove URP's Vignette in the danger colour at 0.26. Garrett did not see it in two Dog Fight runs. URP's Vignette *multiplies* the frame by its colour, and the arena's sky is (0, 0, 0.1), so red times near-black is near-black (L-STU-125).
- The game now draws the edge as an **alpha-blended overlay**: a ScreenSpaceOverlay canvas sorted under the HUD, with a baked mask texture tinted with the palette's danger signal colour. It **breathes** while the silence holds and **flares** on every hit that extends it.
- URP's Vignette stays only as an optional black darkening under the edge.
- The shipped values are **overtuned on purpose** until this lab picks a look:
  - strength 0.75, with the edge starting at 0.55 of the way to the top;
  - rim 0.35, breathing 25% at 0.8 Hz;
  - hit flare 0.35, darkening 0.3.

**Scorecard.** Produced by the lab's `runBatch` (button *Score the looks*) and re-run by `verify_lab.cjs` on this commit:

| Look | Edge · space | Edge · bright | Edge · danger | Centre | Red · space |
|---|---|---|---|---|---|
| Off (control) | 0 | 0 | 0 | 0 | 0 |
| Round 0: URP red multiply (the invisible cut) | 2.78 | 5.01 | 2.92 | 0.62 | 0.045 |
| **Shipped (overtuned)** | **45.1** | 30.6 | 44.34 | 1.59 | 0.797 |
| Ember (subtle) | 26.45 | 17.44 | 26.01 | 0.49 | 0.432 |
| Faceted window | 40.98 | 26.83 | 40.32 | 1.08 | 0.716 |
| Prism rim | 13.35 | 9.75 | 13.4 | 1.08 | 0.216 |
| Hard frame | 55.18 | 38.06 | 54.27 | 5.34 | 1.005 |
| Breathing | 45.1 | 30.6 | 44.34 | 1.59 | 0.797 |

- Breathing scores the same as Shipped because it differs only in its motion.
- The OKLab just-noticeable difference is about 2 in these units. Round 0's 2.8 on open space matches what Garrett reported: nobody saw it.
- Every overlay look reads at 5 to 20 times that, and all but Hard frame keep the centre under 1.6.

**Checked.**
- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/SilenceVignetteLab.html` passes:
  - no console errors;
  - no page scroll at 1600×900, and no sideways scroll on an iPhone 13;
  - every `__lab` hook works with no arguments;
  - every `SPEC` key is in `SHIPPED` and in range;
  - the manual clock advances;
  - `runBatch` gives the same result within one page load and across two loads.
- The two screenshots were looked at. On desktop the shipped edge is plainly red over open space.
- The page runs its own yardstick check: Off must read 0, Round 0 must read under 4, and Shipped must read more than 10 times Round 0. It passes.
- `bash Tools/Build/silence_vignette_parity/run.sh` passes:
  - 21 cases and 5214 values; the worst |js − cs| is 4e-7;
  - all 24 `SHIPPED` fields equal the asset;
  - four planted controls are each caught: feather doubled, facet phase flipped, breathing inverted, and an asset value moved.
- `SilenceVignetteMaskTests` (edit mode) pins the shape's contract:
  - the centre is clear and the corners are full;
  - what roundness 0 and 1 mean;
  - a flat-topped polygon, falling back to the soft shape below three facets;
  - the rim line drawn with no fill;
  - the breathing's depth;
  - opacity as weight × strength plus the flare.
- The decision log is wired: `ArtifactData list decisions` answered and returned an empty collection.

**Found.**
- The URP Vignette path could never have worked for this colour on this sky, whatever the intensity.
- The Amoebius run that "proved" it only proved that the driver reached weight 1.0 (L-STU-125).
- The Amoebius renderer draws neither the URP vignette nor this overlay's look, so the look is still an editor check.

**Decision needed (designer).** Which look ships, and at what strength:
1. Fly the stage with *Rival firing*, switch looks, and change scenes.
2. Tune any slider; a changed value is underlined against the shipped one.
3. Log the pick in **Decisions**.
4. **To Unity** gives the asset lines.

The next round reads the log first.

## What the lab does NOT model

- The scenes are stand-ins: flat 2D trails and ships in the shipped palette, with no bloom, tonemapping, motion or HUD. Bloom in the game will lift the HDR danger colour, and the lab does not show that.
- The darkening uses URP 17's `ApplyVignette` formula on the stand-in frame, not the real profile.
- The silence timeline follows the game's rules (a hit's worth, the onset shake against the per-hit shake, the flare and the fades). The rival's hit rate is a lab knob (`labHitsPerSecond`), not a measured AI.
- The camera shake is not drawn. Its numbers are listed for reference only.
- Frame cost is not measured. The overlay is one full-screen alpha quad, about 256×144 texels, re-baked only when the aspect ratio or the shape changes.
