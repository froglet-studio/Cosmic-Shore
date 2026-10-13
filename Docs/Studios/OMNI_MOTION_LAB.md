# Omni Motion Lab

A browser lab for the next round of the omni crystal's element effects. It covers four asks:

1. **Mass:** the triangle prisms seal fully once they cross into the camera's visible hemisphere.
2. **Space:** the squares make two moves per loop instead of three. The two triangle steps become one smooth move.
3. **Time:** the rhombus flips are timed into the squares' stops, so squares and rhombi never move at once.
4. **Charge:** a gallery of 17 effects for the pentagons. Each plays on the omni and on the elemental Charge crystal, so you can see Charge brought into parity with the other three.

The lab has styles, sliders for each ask, a beat timeline and a side-by-side omni / Charge crystal view. A "compare every Charge effect" grid is on both subjects. There is also a headless collision scorecard and a shared decision log.

- **Page (source):** `Docs/Studios/OmniMotionLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/7Q7uzP6Xc3dUWQ2QHky3uh (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Data:** the same block as the Omni Combine Lab, stamped into both pages by `python3 Tools/Build/omni_combine_lab_assets.py`. `--check` fails if either page is stale. The omni prism thickness is imported from `author_omni_crystal_triangles.DEPTH_THICKNESS`.
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/OmniMotionLab.html`
- **Hooks:** `window.__lab.runFullBatch()`, `runChargeBatch()`, `clipTimeline(p)`, `clipSelfTest()`, `beatsOf(p)`, `squaresAt(p, t)`, `wavesAt(p, t)`, `sealOf(p, D)`

## Round 1 (2026-10-13)

### What the shipped data actually is (measured on load, `SQFIT`)

The Space squares (`OmniSpaceSquares.asset` ← `CrystalsRaw_V2.blend`, Action.008) have three keys over a 4.8 s loop: 1.56 s, 1.6 s and 1.6 s, back to back, each a cosine ease. So the squares stop three times a loop and are moving 97% of the time.

| Key | What it is, geometrically | Worst miss |
|---|---|---|
| `SquareRotateAroundPentagon` | a **rigid 72° turn** about a pentagon's five-fold axis, through the crystal centre | 1.8e-5 |
| `SquareOverTri_Step1` + `Step2` | together, a **rigid 120° turn** about a triangle's three-fold axis, through the centre | 2.9e-5 |
| `SquareOverTri_Step1` alone | that turn **halfway (60°), lifted 0.19 off the shell** (12% of the radius) | 1.2e-5 |

A blend shape moves every corner in a straight line, so each turn is played as a chord. **Mid-move, a square shrinks to 0.83 of its size (pentagon key) or 0.87 (each triangle step), and dips toward the axis.** That is the shipped look today.

The Time wave (`TimeCrystalFlipWaveProfile.asset`) runs every 2 s, with its rings moving for 1.8 s of it. So shipped, the squares and the rhombi move at the same time **87% of the time**.

### New on the page

- **Triangles tab:** *Seal at the horizon* (lab) and *Horizon margin*. The seal depth becomes `plateRadius / cameraDistance − margin` each frame, which is the edge of the visible hemisphere under perspective (slightly past 90°). The other tab rows are the shipped profile values and the material's face split and thickness. The ratchet is `DepthRatchet.Step`, ported line for line.
- **Cadence tab:** *Cadence* has three settings:
  - 0, shipped clocks;
  - 1, interleaved beats: pentagon turn | Time wave | triangle turn | Time wave, so one wave plays in each square stop, from a new start vertex each time;
  - 2, a control: two moves with Time on its own clock.

  The tab also has *Gap* between beats (below 0 the beats overlap; this is the planted control), *Time wave speed*, and an optional **Charge beat**.
- **Squares tab:** durations, ease, and the two paths.
  - *Pentagon path*: the art key (chord), or a rigid 72° turn with an optional lift.
  - *Triangle path*: Step1 → Step2 under ONE ease (no stop, but a corner at Step1); the keys overlapped (rounds the corner, no new frames); or **one rigid 120° turn lifted on a sine, which passes exactly through the art file's Step1 pose** (lift × the art's).
- **Charge tab + Charge gallery:** 17 effects plus Off, in six families:

  | Family | Effects |
  |---|---|
  | surface / shader only | Edge crawl (ships), Corona, Ripple |
  | pentagon motion | Capacitor, Stepper clicks, Dynamo, Hum |
  | wedges (the pentagon as five wedges) | Iris, Bloom, Lightning rod |
  | bolts between pentagons | Chain lightning, Spark gaps, Strike, Arc web |
  | field | Dipole field, Shockwave, Tesla core |

  Each has a "Needs" list: shader, bones, wedge mesh, bolts, sprites. Shared knobs: rate, motion, light, bolts, stagger, and wedges on the omni.
- **Subject:** omni, Charge crystal, or both side by side, with the same effect on each. **Compare every Charge effect** shows all 18 at once on either subject; double-click a cell to pick it.
- **Beat timeline** under the stage: Space, Time and Charge lanes. Amber marks every moment Space and Time move together.
- **Ship it tab:** what the current settings would take in the game, per element.

### Scorecard

`runFullBatch()` uses the omni under an orbiting camera (20°/s, pitch ±20°, 6.5 radii) for 14 s at 12 Hz. Overlap and stops are timed at 120 Hz over whole loops. It is deterministic (two runs are byte-identical).

| Style | Overlap % | Stops / loop | Loop s | Min square size | Raised in view % | Visible clips % | Clips by pair (% frames / deepest % R) |
|---|---|---|---|---|---|---|---|
| **Shipped today** | 86.9 | 3 | 4.8 | 0.845 | 100 | 66.7 | Space×Time 53.6/2.3; Mass×Space 43.5/2.2; Charge×Space 14.9/2 |
| Proposal: the brief, as keys | 0 | 2 | 8.16 | 0.846 | 0 | 28.6 | Space×Time 17.9/1.8; Mass×Space 12.5/2; Charge×Space 9.5/2 |
| **Proposal: rigid arcs that hop clear** | **0** | **2** | 8.16 | **1** | **0** | **0** | none |
| Proposal: rigid arcs on the shell | 0 | 2 | 8.16 | 1 | 0 | 22 | Space×Time 16.1/1.9; Mass×Space 4.2/1.6; Space×Space 3/1.2 |
| Proposal: rounded keys | 0 | 2 | 8.16 | 0.768 | 0 | 25 | Space×Time 22.6/1.8; Mass×Space 10.1/2.2; Charge×Space 9.5/2 |
| Proposal: four beats | 0 | 2 | 9.71 | 1 | 0 | 0 | none |
| Control: two moves, Time on its own clock | **42.9** | 2 | 8.16 | 0.846 | 0 | 32.1 | Space×Time 32.1/2.2 … |
| Control: beats overlap (gap −0.5) | **33** | 2 | 5.56 | 0.845 | 0 | 31 | Space×Time 22.6/2.1 … |

Both controls fail as they must. The clip detector's own control, `clipSelfTest()`, finds nothing at rest (0 pairs excluded) and names a square pushed half-way into its neighbour.

### Found

1. **The squares' moves cut into still neighbours. Scheduling alone cannot stop it.**
   - `clipTimeline()` on the rigid-arc proposal shows every Space×Time clip happening while the squares move and the rhombi are still. A Time wave on its own clips nothing.
   - The art's keys clip as well. The pentagon chord dips into the pentagon (Charge×Space) and into the rhombi. The triangle steps cut the rhombi and the triangles.
   - So "never both at once" removes the overlap (86.9% → 0) but leaves visible clips at 28.6% of frames.
   - **What removes them is a rigid turn that hops.** The sweep below counts VISIBLE clipping plate-pair frames in the move's own beat, with the scorecard's camera. It is coarse at 15 Hz, then 60 Hz near the edge (`clipTimeline`).

   | Pentagon turn lift (× R) | 0 | 0.05 | 0.07 | 0.09 | 0.12 | 0.13 | **0.14** | 0.15 |
   |---|---|---|---|---|---|---|---|---|
   | 15 Hz | 562 | 564 | 358 | 154 | 76 | | | 0 |
   | 60 Hz | | | | | | 39 | **0** | 0 |

   | Triangle turn lift (× the art's 0.19) | 0 | 0.5 | 1 | 1.5 | 1.8 | 2.0 | 2.1 | **2.2** |
   |---|---|---|---|---|---|---|---|---|
   | 15 Hz | 1309 | 1187 | 202 | 6 | | 0 | | |
   | 60 Hz | | | | | 15 | 3 | 3 | **0** |

   The proposal ships 0.15 and 2.3, a margin over the measured edge.
2. **The last triangle-turn clips were raised far-side prisms at the limb.** The prisms on the far side stand at the tops of their columns (2× the radius). A square hopping over a triangle near the silhouette can graze one. That depends on the camera. At 2.3× the scorecard's orbit sees none; another camera path might.
3. **Seal at the horizon works:** raised prisms on the visible side drop from 100% of frames to 0. At 6.5 radii, the seal depth is 0.146 − margin 0.05 = 0.096 (shipped: 1.0).
   - The profile is shared with the Mass crystal, so it would get the same rule with its own plate radius.
   - The gameplay camera's distance varies, so the seal depth must be computed per frame from `CameraManager.ViewCamera`.
4. **Two Charge effects only clip while the squares turn.**
   - On the hop proposal, Iris (5.6%) and Spark gaps (6.1%) meet a hopping square during the pentagon turn. Both already rise as they open or lean; without that rise they clipped 71% and 50% of frames.
   - **With the Charge beat on, all 17 effects clip 0%.** That is the case for "four beats": Space, Time and Charge take turns, and Mass answers the camera.

   `runChargeBatch` on the hop proposal, 12 s at 15 Hz (with the beat: 19.4 s, two cycles):

   | Effect | Family | Charge clips % | with its own beat | Travel % R | Bolts / frame | Peak light | Needs |
   |---|---|---|---|---|---|---|---|
   | Off | control | 0 | 0 | 0 | 0 | 0 | — |
   | Edge crawl (ships) | surface | 0 | 0 | 0 | 118 | 0 | shader (ships) |
   | Capacitor | motion | 0 | 0 | 7.8 | 3.6 | 3.5 | bones, shader, bolts |
   | Stepper clicks | motion | 0 | 0 | 47 (a turn) | 25 | 1.4 | bones, bolts |
   | Dynamo | motion | 0 | 0 | 47 (a turn) | 60 | 0.7 | bones, bolts |
   | Iris | wedges | 5.6 | 0 | 13 | 40 | 0.25 | wedge mesh, bones, sprites, bolts |
   | Bloom | wedges | 0 | 0 | 18 | 0 | 0.25 | wedge mesh, bones, sprites |
   | Lightning rod | wedges | 0 | 0 | 13 | 5 | 0.25 | wedge mesh, bones, sprites, bolts |
   | Chain lightning | bolts | 0 | 0 | 6 | 3 | 3.2 | bolts, bones |
   | Spark gaps | bolts | 6.1 | 0 | 18 | 1.1 | 0.6 | bolts, bones |
   | Dipole field | field | 0 | 0 | 8.4 | 2.5 | 2.8 | bones, shader, bolts |
   | Shockwave | field | 0 | 0 | 8 | 1.4 | 3 | bones, shader, bolts |
   | Hum | motion | 0 | 0 | 1.3 | 128 | 0.8 | bones, shader, bolts |
   | Corona | surface | 0 | 0 | 0 | 0 | 0.7 | sprites, shader |
   | Ripple | surface | 0 | 0 | 0 | 0 | 0 | shader |
   | Strike | bolts | 0 | 0 | 5 | 9.6 | 3.2 | bolts, bones |
   | Arc web | bolts | 0 | 0 | 2 | 12.5 | 0.25 | bolts |
   | Tesla core | field | 0 | 0 | 4 | 1.6 | 1.4 | sprites, bolts, bones |

### Checked

- `verify_lab.cjs` PASS on desktop and phone. Screenshots read: the split view, both compare grids and eight single effects.
- `runFullBatch()` is byte-identical across two runs. The fits are shown on the About tab: 1.8e-5 / 2.9e-5 / 1.2e-5.
- `clipSelfTest()`: the rest pose is clean and a planted overlap is found.
- `omni_combine_lab_assets.py --check` OK (2 labs) and `--self-test` PASS. `author_omni_combine_asset.py --check` OK: the block gained `depth.omni.thickness` and the converter's data is unchanged.

### Decision needed (designer)

- Which square path: **rigid arcs that hop** (0 clips, needs in-between frames re-baked on the two keys), or the art file's keys (no new frames, still clips)?
- Gap between beats, and the triangle turn's length (2.4 s proposed).
- Whether Charge gets its own beat.
- **Which Charge effect, or effects.** This round is about the look; cost comes next.

### Not modelled

- The shading approximates the Fresnel shaders, and there is no bloom.
- The prisms are the art omni's triangle plates thickened inward, not the shipped `OmniCrystalTriangles` mesh.
- The collision test checks corners and edge midpoints against convex plates, so an edge crossing an edge is missed. "Visible" treats the body as a sphere.
- The scorecard uses one camera orbit at a fixed distance.
- No sound, capture, LOD or frame cost.
- The Charge crystal and the omni's wedges are the art file's meshes.
- Nothing here is verified in Unity, and no C# changed this round.
