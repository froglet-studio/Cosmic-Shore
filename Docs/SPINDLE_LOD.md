# Spindle LOD — the braid, the drawing-together, the thread

**Owner:** Garrett (spindles, ecology look). **Status (2026-10-20, round 2):** built on `cece/gallant-keller-6mr1u4`.
Two styles on one set of meshes (`SpindleLodProfileSO.style`, live-switchable): **COVERT** (§2, the first round:
the LOD hides itself) and **OVERT** (§7, the default since round 2: the LOD IS the effect, in lockstep with the
prisms' own depth ramp, through the shared `IncorporealDepth.hlsl`). Compiled (Unity reference compile, Amoebius),
the shader contracts proven by compiling the shipped HLSL (12 mutants caught), the runtime run in Amoebius, the look
previewed offline from the shipped shader code (§7.3). The graphs have still not been through Unity's importer.

**Round 1's play bug, fixed.** Garrett, Lattice cell, in flight: one gyroid showed its limbs' near AND far forms
at once (the far thread as bright straight "stars" through every prism, braids around them). Cause: the system
tiered by `Camera.main`, which in flight is Bootstrap's MainCamera-tagged menu camera and never follows the vessel
(the depth prisms froze from the same mistake; `CameraManager.ViewCamera` exists for it). Limbs near the vessel were
classified by their distance from a fixed point and handed the far mesh, while the shader blended for the real
camera. Fixed by tiering from `CameraManager.ViewCamera`.

## 1. Why

Spindles are the skeleton made beautiful: every limb of every plant is a wire, and the game leans into that
instead of hiding it. Up close they are the best thing in a cell. Far away they "don't look like much at all",
and there are thousands of them at distances where they are barely seen (Garrett, 2026-10-20). Performance is the
driver; a seamless transition is the requirement; a better far look is the stretch.

### What a spindle is, measured

Every flora branch prefab (`Branch`, `GyroidBranch`, `AssemblyBranch`, `QuasicrystalBranch`) and the tadpole draw
ONE mesh, `BezierCurve.001` in `Assets/_Models/Fauna/bonita.fbx` (`Tools/Build/author_spindle_lod_meshes.py
--report` re-measures all of this):

- **A braid, not a tube.** Three strands of triangular cross-section, 25 rings each, spiralling round a STRAIGHT
  centreline (the mean of the three ring centres is on the z axis to 0.014 of a unit). 216 quads, 432 triangles.
- Each strand tapers 0.015 → 0.151 → 0.013 units; the three spread to 0.54 apart, pinch to 0.17 at ring 12 and
  splay to 0.69 at the tip. The braid's width (its **girth**) is 1.40 object units; a flora branch is ~15 units long.
- **SpindleGraph's lace works at the scale of whole strands**: UV0 runs u along a strand's length and v round its
  three faces, and the Voronoi is ~1.5 cells per strand. So the lace stays legible into the mid distance; the strands
  themselves do not.

### Why the far ones cost so much and look like so little

- A 0.3-unit strand is one pixel wide at ~280 units (1080p, 60° FOV). Beyond that each spindle is 432 triangles of
  sub-pixel geometry, which rasterises as broken, sparkling dots — and tiny triangles are the most expensive kind a
  GPU draws.
- SpindleGraph's own distance term (`_Distance` = 2,000,000, so ~1,414 units) raises the alpha exponent with the
  squared distance, alpha-clipping the braid away by ~1,000–1,400 units — after every vertex and fragment was paid for.
- Every limb is its own GameObject renderer (lever L1 in `Docs/PERFORMANCE_OPTIMIZATION.md`): culled, sorted (it is
  transparent) and submitted one at a time.

### Why the 2026-10-17 sub-pixel LOD hid nothing

It measured a limb's **bounding sphere** on screen (2–16 px) and hid limbs under 0.75 px. A spindle is ~15 units
long and ~1.4 across, so its sphere is big at the very distance its three strands are each a fraction of a pixel.
**A wire is judged by its thickness, never by its length or its bounding sphere.** Every number below is in
**girths**: camera distance over the braid's world-space width. That unit means the same picture on a gyroid
branch, a lattice-stretched limb (a stretch along the length leaves the girth alone) and a tadpole body.

## 2. The tiers (the COVERT style; OVERT is §7)

| tier | girths (default) | mesh | shader | draws |
|---|---|---|---|---|
| **near** | < 100 | the authored braid, 432 tris | **SpindleGraph, untouched** | its own renderer, exactly as before |
| **mid** | 100 – 420 | `SpindleBraidMid`: the braid at every third ring, 144 tris | `SpindleLodMidGraph` | its own renderer |
| **far** | 420 – 1,900 | `SpindleThreadFar`: one faceted thread on the centreline, 24 tris | `SpindleLodFarGraph` | **one instanced call per material** while still |
| **culled** | > 1,900 | — | — | nothing |

At the reference display a flora branch's girth is 1.40 units, so: near to ~140 units, mid to ~590, far to ~2,660.
Today's braid has faded out by ~1,400; the thread stays as a glowing line for longer and costs a fraction of it.

### What happens inside the mid tier (the seamless part)

Everything that MOVES happens on the GPU, per pixel and per frame, as a pure function of the camera
(`Assets/_Graphics/Materials/Graphs/SpindleLod.hlsl`):

1. **Below `convergeStart` (110)** the mid graph returns SpindleGraph's lace exactly; the only difference from the
   near tier is the decimated mesh, whose error (0.05 units) is under a quarter-pixel at the swap.
2. **From `convergeStart` to `convergeEnd` (= midEnd × (1 − hysteresis) = 386)** the three strands **draw
   together** onto their centreline (a geomorph: TEXCOORD1 is each ring's step to the centreline) while the lace
   **closes** into the thread's shading on the same schedule. The braid becomes one thread as you pull away.
3. **The width hold ramps in on that same schedule.** Past the width where a strand would go sub-pixel it is drawn
   at `minThreadPixels` (1.25) by pushing each vertex out along its own radial arm (TEXCOORD2), and its alpha pays
   for the extra width (coverage = true width / drawn width). It never starts before convergence: the near tier
   cannot hold a width, and a hold the near tier lacks would show at the swap (the clang harness caught exactly that).
4. Past `convergeEnd` the mid braid IS the far thread's construction, vertex for vertex and pixel for pixel.

### The CPU only swaps where both sides draw the same picture

`SpindleLodSystem` moves a limb outward past a boundary and inward only once it is back inside boundary ×
(1 − hysteresis). Each band sits where the two tiers are identical:

- near ↔ mid: [92, 100], below `convergeStart` — both draw the plain lace.
- mid ↔ far: [386, 420], past `convergeEnd` — both draw the converged thread.
- far ↔ culled: the inward edge is `farFadeEnd` itself — the thread is switched back on only where its alpha is
  still zero, and fades in from there.

`SpindleLodProfileSO.BandsAreSane` refuses a profile that breaks this order, and `SpindleLodTests` holds it.

### The far look

The skeleton glows instead of vanishing. The thread is a **faceted triangular volume** (never a sheet: the
"Nothing in the world looks flat" rule), shaded between the material's dull and bright colours by how squarely
each facet faces the camera (`threadBase`, `threadFacet`), brightening toward `threadGlow` (1.35, reaches bloom)
as it recedes, held at a visible width so it reads as a continuous line rather than dotted sparkle, and fading
out between `farFadeStart` and `farFadeEnd`. From afar a plant reads as its constellation of bonds. Every one of
these is a dial on the profile — **the look is Garrett's call** (see the open decision on the task board).

## 3. The pieces

| piece | what it is |
|---|---|
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/SpindleLodSystem.cs` | The runtime. ONE Burst pass per tick (`tickIntervalSeconds`, 0.2) over a read-only `TransformAccessArray` (the `PrismColliderLodManager` shape): pose, girth distance, tier under hysteresis, moved-or-still. Main-thread work is O(changed limbs), budgeted (`maxTransitionsPerFrame`). Still far limbs: renderer off, drawn by `Graphics.RenderMeshInstanced` in calls of 1,023. Created on the first registration; outlives scene loads. Kill switch: the profile's `lodEnabled`. |
| `Spindle.cs` | Registers each renderer whose mesh the profile knows (end of `Start`, once posed), hands it back before any death, keeps a fading limb out of the batch (its condense/evaporate is a per-renderer MPB stamp), and keeps the tier's material on fade settle. Life, death, ordered wither and the tree are untouched. |
| `Assets/Resources/SpindleLodProfile.asset` / `SpindleLodProfileSO` | The tuning half (bands, look, cadence) is the designer's and is written once. The measured half (templates, LOD meshes, girth, strand radius, the imported bounds Unity must produce) is owned by the generator. |
| `Assets/_Models/Spindles/` | `SpindleBraidMid.asset`, `SpindleThreadFar.asset`, baked by `Tools/Build/author_spindle_lod_meshes.py` (`--check`, `--report`, `--self-test`). |
| `SpindleLodMidGraph` / `SpindleLodFarGraph` | Generated FROM SpindleGraph by `Tools/Shaders/wire_spindle_lod.py` (`--check` is the drift gate: edit SpindleGraph, re-run it). SpindleGraph itself is not touched — it also draws BranchingMembrane and the Sparrow's exhaust projectile. |
| `SpindleLodMid.mat` / `SpindleLodFar.mat` | Template materials: SpindleMaterial with the shader swapped. They exist so the BUILD keeps the shader variants the runtime clones use (`_ALPHATEST_ON`, `_SURFACE_TYPE_TRANSPARENT`, instancing). Runtime LOD materials clone them and copy each limb's phase-variant colours, sway and phase. |
| `Tools/Shaders/verify_spindle_lod.py` | Compiles the shipped `SpindleLod.hlsl` + `IncorporealDepth.hlsl` with clang and proves: pass-through when unset, the near→mid and mid→far hand-offs in both styles, the width hold and its coverage, the fades; for OVERT, the opening against an independent transcription of SpreadSubGraph at every distance, exact rest up close, the danger magnitudes, world units under scale, the root-anchored fan, motion only while open. `--self-test`: 12 mutants, all caught. |
| `Assets/_Graphics/Materials/Graphs/IncorporealDepth.hlsl`, `Resources/IncorporealDepth`, `Utility/IncorporealDepth.cs` | The shared depth effect of the incorporeal parts (§7), its tuning, and its publisher. |
| `Tools/Shaders/render_spindle_lod_preview.py` | The offline preview from the shipped shader code (§7.3). |
| `Assets/_Scripts/Tests/Editor/SpindleLodTests.cs` | Tier hysteresis, band order, the cull edge, and `ShippedProfile_IsSaneAndMeasured`, which checks the LOD meshes against the braid Unity actually imported. |

**Telemetry.** `renderers` (HUD console) appends `spindle LOD: N limbs - near · mid · far own + instanced ·
culled`, because an instanced far limb has no enabled renderer and the census alone would undercount the family.
`[ECOSIM]` (EcosystemPerfProbe) carries `spindles=near/mid/farOwn/farInstanced/culled`. Markers:
`SpindleLod.Tick`, `SpindleLod.Drain`, `SpindleLod.Draw`.

**The import guard.** The generator reproduces Unity's FBX import (`bakeAxisConversion: 0`: x negated, winding
reversed, ×0.01) and records the bounds that predicts for the source mesh. At runtime each mesh set compares them
with the imported mesh's real bounds (readable on a non-readable mesh; the braid's centre sits off the x axis, so a
sign error shows) and disables itself with a warning if they differ, rather than drawing a thread beside its branch.
Amoebius's own FBX importer passes the same check.

## 4. Measured

- Mesh: 432 → 144 (mid) → 24 (far) triangles per limb; the instanced far tier has no renderer.
- Amoebius, Menu_Main (Garland), headless, seed 7, 1,800 frames: 910 limbs registered, **865 (95%) drawn by the
  instanced far batch**, 24 mid, 21 far on their own renderer (fading or just posed), 0 culled; the tier counters
  matched a same-frame recount of the books on every sample. Those proportions are this camera's; Amoebius's
  milliseconds are .NET's, so the frame-time win needs a Unity capture.

## 5. What is not verified

- **No Unity editor has opened the branch.** The two graphs are JSON generated from SpindleGraph (validated
  structurally, custom-function signatures checked by `check_shadergraph_custom_function_signatures.py`) but have not
  been through the Shader Graph importer. If one imports magenta, every limb past nearEnd shows it: set `lodEnabled`
  off on the profile and report it.
- **The look has not been seen.** Transitions are proven equal in the shader maths; whether the far thread is the
  improvement it is meant to be is a judgement only Garrett can make in the editor.
- **The frame-time win is unmeasured in Unity.** A/B: an S2 Lattice capture with `lodEnabled` on, one with it off.
- **Amoebius draws the two LOD graphs untranslated** until its GLSL library carries `SpindleLodVertex_float`,
  `SpindleLodSurface_float` and `SpindleLodThread_float` (the engine translates file-based Custom Functions from a
  hand-ported library), and the two graph guids are mapped to the Spindle family. Shombith's area; on the board.

## 6. Known limits

- Tiering follows `CameraManager.ViewCamera` (the camera the player looks through). A second camera (the Third Eye,
  a minimap) sees that camera's meshes; the shader still blends for its own distance.
- The instanced batch is culled by one box per material, not per limb; instances off screen still run 24-triangle
  vertex work. Per-instance culling is the next step if a capture asks for it.
- A limb that moves never batches (a creature's body would lag its creature). Tadpoles and other moving users of
  the braid mesh still get the mid and far meshes on their own renderers.
- The ordered wither, the tree links, the sway constants handed to prisms (`PrismSway`) and the death fades are
  unchanged; a fading limb leaves the batch for its fade.

## 7. OVERT: the LOD as an effect, in lockstep with the prisms

Garrett, round 2: *"explore an overt approach ... instead of trying to disguise the LOD, lean in and make it into a
really cool effect that signals this mass is still alive because it has spindles moving through it. Its depth
effects should be even stronger than the base prisms and on par with the danger prisms."* And: *"the model LOD
should work in lockstep with a sqrdistance shader effect that ranges like the prisms. Then we can use the same
shader effects on the shards and switches, membranes, and nucleus to give a connection between the incorporeal
parts of the hypersea."*

### 7.1 The prisms' depth effect, measured

Traced out of BlockGraph (`DistanceSpreadAndColors` + `SpreadSubGraph`; `Docs/SWARM_FAUNA.md` §15 traced the same):
`norm = |objectOrigin − camera|² / _SqrDistance` (100,000 on every prism material: the ramp ends at ~316 units),
`far = _Spread × (50, 35, 20)`, `eff = norm > 1 ? far : lerp(−7, far, norm)`, `eff = max(eff, _Spread)`, and each
face moves `eff / objectScale` along its normal. Base prisms (`PrismMaterial`, spread 0.1) open (5, 3.5, 2) units;
**danger** prisms (`DangerBlockMaterial`, the theme's `OriginalMaterialSet` tier, spread 0.09/0.29/0.8) open
(4.5, 10.2, 16). The incorporeal parts READ the danger tier (`Resources/IncorporealDepth.tierMaterialSet`), so a
prism retune moves them too, and `strength` (1) scales it ("on par with danger"; above 1, stronger).

### 7.2 What the spindle does with it

- **The same ramp, less the resting spread**: `opening = eff − spread`, so up close the opening is exactly zero and
  the braid is exactly SpindleGraph's. The first axis starts opening at norm 0.339 (~184 units for the danger tier);
  everything is fully open at norm 1 (~316 units).
- **The braid's parts are its STRANDS, and they FAN.** Each strand moves along its own splay (TEXCOORD3: the way its
  tip already leans off the centreline), anchored at the root and reaching the danger face travel (15.2 units) at
  the tip. The limb blooms like a whisk opening and stays a bond to the node it grows from.
- **Alive**: a wave travels root to tip (`flowWaves` crests, `flowSpeed` crests/s), breathing the fan ±`flowAmplitude`
  and lighting a pulse on its crest (`pulseGlow`), both in proportion to how open the limb is (a closed limb is
  still).
- **Lockstep tiers**, in the prisms' norm: near → mid at the ramp's first opening (both tiers closed inside the
  hysteresis band below it); mid → far at 1 / (1 − hysteresis), so the far tier's inward edge is norm 1 (both fully
  open inside the band above it); the lace closes into the thread shading on the opening's own schedule. The far
  tier draws the SAME 144-triangle braid the mid tier does. A fully opened strand travels 15 units, so the two
  tiers must open identically: the 5-ring alternative (72 tris) already sits 0.19 units off the 9-ring braid
  before opening, and its normals would then carry its strands visibly elsewhere. Faded out between norm 16 and 36 (~1,265–1,900 units), then culled.
- **Culling bounds** grow by `IncorporealDepth.MaxOpening` (the danger face travel × (1 + flowAmplitude), ~20.5
  units) on mid/far renderers and the instanced batch, so an opened limb just off screen never blinks out.

**What was tried first and rejected (measured in the preview, §7.3):** the literal prism rule on the braid's 216
small faces, with its per-axis anisotropy, shredded every limb into a hairball - the skeleton gone. Then a rigid
part along each strand's radial direction: that direction turns with the spiral, so parted strands curled into
coils, and the roots left their node. The root-anchored fan along a fixed splay is what keeps the wireframe read.

### 7.3 Seeing it without Unity

`python3 Tools/Shaders/render_spindle_lod_preview.py OUT --style overt|covert` compiles the SHIPPED
`SpindleLod.hlsl` + `IncorporealDepth.hlsl` + `SpindleSway.hlsl` with clang, runs them on the real mid braid posed
as the Branch prefab poses it (a star of 8 limbs, like one lattice node), and writes a sheet (five points on the ramp:
the shape framed large with the width hold off, and below it the true size on a 1080p screen with the hold on) and a
two-second animation of the open star. Colour is the far tier's thread shading (the lace's Voronoi is a Shader Graph
node with no HLSL here) and the raster has no bloom: judge shape and motion, not grading.

### 7.4 One effect for every incorporeal part

`IncorporealDepth.hlsl` is written for adoption: include it from a graph's Custom Function and choose the form
that fits what the surface is made of.

| surface | form | why |
|---|---|---|
| snow shards | `IncorporealDepthOpen` (per face, prism-exact) | faceted solids with a few big split-normal faces |
| toy switch rings | `IncorporealDepthOpen` | the ring's segments are faceted |
| membrane | `IncorporealDepthPart` along each panel's outward direction | parting panels, not shredding triangles |
| nucleus | `IncorporealDepthPart` per shell | the same |
| spindles | `IncorporealDepthPart` along each strand's splay, root-anchored | §7.2 |

Then `IncorporealDepthGlow` in the fragment and a per-surface Strength. The ramp, the tier and the wave are global
(`Utility/IncorporealDepth.Publish`), so every part opens at the same distances, as hard as danger mass, with one
wave, which is the connection between the incorporeal parts. Nothing but the spindles is wired yet
(T-20261020-3b39).
