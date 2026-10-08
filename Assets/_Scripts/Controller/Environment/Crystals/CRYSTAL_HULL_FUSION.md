# Crystal → hull fusion

**The crystal's faces come off it and mate with the hull.**

The generic elemental capture (`CrystalCaptureConfigSO`) snatches the crystal, sucks it into the
hull centre, shrinks it to nothing and sprays a husk into the wake. A fusion replaces that, per
**(vessel, element) pair**, with the crystal visibly becoming part of the vessel. It is the hull
counterpart of the Squirrel's omni morph (`R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md`, the omni
cage's panels landing on the boost ring's eight shields) and reads the crystal the same way:
**panels fly, filler is absorbed.**

1. **Peel** (0.16 s) — each of the crystal's solids folds into its outer face, and the 60 faces
   lift off the crystal, where it was taken.
2. **Flight** (0.42 s) — every face flies to its own patch of the hull, swinging out over the patch
   and coming straight DOWN onto it. Faces nearest the hull land first. Colour carries from the
   pickup's lime to the pilot's domain crystal pair.
3. **Mate** (0.30 s) — the faces lie ON the hull's surface, bent to its shape and wearing its
   normals, flare, and the charge discharge runs continuously round each face's outline. The
   pickup sound plays here.
4. **Dissolve** (0.30 s) — they sink a hair into the skin and dissolve.

**First and only pair today: Squirrel × Charge.** Every other pair plays the generic capture,
unchanged. Adding a pair is one entry in `Resources/CrystalHullFusionConfig`.

---

## 0. History — why the first cut looked like nothing changed

The first push (`c6e2c698`) moved whole prisms rigidly onto the hull. In play it looked "very
similar to before", and the reason was not the design: **it never ran.** The charge crystal draws
`CrystalEdgeArcMeshBaker`'s twin, which the baker uploads with `markNoLongerReadable: true`. The
fusion checked `isReadable` on the drawn mesh, refused every charge crystal, warned ONCE, and fell
back to the generic capture — which is, by construction, exactly what the pilot saw before.

- Fixed by `CrystalEdgeArcMeshBaker.TryGetReadable`: a readable twin re-baked from the cached
  source, identical channel for channel.
- Lesson: **a fallback to the old effect is indistinguishable from the old effect.** When "it looks
  the same" comes back, check the console for the one-time refusal before tuning anything.

The same playtest note named the target look: *faces come off and mate with the vessel model*, like
the omni → octahedra morph. So the second cut moved from rigid prisms to faces.

**The second cut ran, and was too slow to see.** Playtest: *"it lagged so much the effect was over by
the time the frame caught up."* Every face point was projected onto the hull ON THE MAIN THREAD,
during the pickup, 8 faces a frame through the peel — measured afterwards on the real meshes at
**80 ms warm / 222 ms cold even in .NET** (more in the Editor's Mono), plus a per-pickup hull bake,
surface grid and ~3k native `Transform` calls a frame. The third cut (this one) moves all of it:

| Stage | Before | Now (warm .NET, real meshes) |
|---|---|---|
| face cut + hull layout | main thread, every pickup | **worker thread, once per (hull mesh, crystal mesh)**, ~100 ms, started at vessel spawn |
| per pickup | bake + layout + 60 faces × 51 points projected | **2 ms**: start poses, 60×60 assignment, clone a prototype mesh |
| per frame | 3,060 `TransformPoint` + 16.7k vertices + `RecalculateBounds` | **0.25 ms**: one matrix read per bone, managed maths, 10.4k vertices |

Lesson: **an effect that does its expensive work in the window it animates in is invisible
however correct it is** — the expense eats the window. Anything that depends only on assets
(here: two meshes) is a cache, built before it is needed.

## 1. What flies: panels and filler

`CrystalHullFusionGeometry.BuildPanels`: a solid is a connected piece of the mesh (welded by
position); a face is a solid's triangles joined across welded edges while within 20° of each other
(120 of the charge crystal's 300 side quads are non-planar by 5.2°; its sharpest real crease is
57.5°). Each solid's outermost face is its **panel**; the rest is **filler**, folded into the panel
during the peel. Measured with the shipped code on the charge FBX: **60 panels, every one a
pentagon**, radial alignment ≥ 0.9994.

## 2. Where each face lands

- **Contact:** the outermost outward-facing hull vertex in the direction the crystal came from, in
  the hull's normalised space.
- **Patches:** the other 59 are farthest-point sampled over the outward-facing skin, so they sit at
  near-uniform spacing.
- **Match:** each face's target direction is its crystal radial reflected through the contact
  (`WrapDirection`), and faces take patches by the optimal assignment (Hungarian, cost `1 − cos`).

| Measured on the shipped Squirrel FBX | patch gap | face → patch turn (cos), worst / median |
|---|---|---|
| direction straight onto the hull (first attempt) | **0.00** — 9 of 60 on a taken spot, piled on the wing tips | — |
| spread + greedy match | 0.46 | **−0.94** (a face crossing the whole hull) / 0.96 |
| spread + optimal match (shipped) | 0.46–0.48 | **+0.07 – +0.19** / 0.89 |

## 3. How a face lands ON the hull

A flat three-triangle pentagon cannot lie on the Squirrel: its corners sat a **median 0.31 patch
radii off the skin**, and snapping corners to hull vertices left 36% unsnapped (the hull is coarse
low-poly in its flat areas). So the drawn face is **rebuilt as a subdivided fan**
(`FusionTemplate`, 3 levels: 31 points and 45 triangles per pentagon). Every point is laid in the
patch's tangent plane at the patch's size, keeping the face's twist from the crystal, then
**projected onto the closest point of the hull's own triangles** (`HullSurface` — triangles binned
by their bounding boxes, so a big low-poly triangle is found from every cell it spans), wearing the
hull's interpolated normal there.

Shipped code on the real meshes: **1,860 of 1,860 points projected** (3,060/3,060 at the earlier 4
levels), median 0.15 patch radii from laid to surface. The surface query grows its search one ring
of cells at a time and stops as soon as nothing outside can be closer. The subdivided face keeps the charge discharge: each
sub-triangle carries the baker's channel contract with only the outline's segments marked as bolt
edges.

## 4. The faces ride the bones — and the layout is built once, off the main thread

The Squirrel hull is skinned and puppeteered. The hull is baked at collection; each point is pinned
to the bone that dominates the hull vertex nearest it, against a snapshot of the bones at the bake,
and follows that bone live. A face on a wing stays on the wing while it flaps. Needs the hull mesh
CPU-readable: this branch sets `isReadable: 1` on `SquirrelVessel_CosmicShoresTest1.fbx`.

Where the faces land depends only on the two meshes, so it is a `HullLayout` built **once per
(hull mesh, crystal mesh)**: patches farthest-point spread from the hull's top, and on each patch
ONE landed grid (face 0's pentagon, scaled), every point projected and pinned to its bone. Point `k`
of a patch is where point `k` of ANY face lands — the charge crystal's 60 faces are one pentagon cut
by one template, and the build refuses (named) a crystal whose faces differ. A pickup only matches
faces to patches.

It is built on a **worker thread** (`Task.Run`) from plain arrays the main thread captured — the
hull bake, the bone matrices at the bake, the crystal's vertex channels — and touches no
`UnityEngine.Object`. The main thread never awaits it; it polls a volatile `Ready`, so none of the
UniTask main-thread hazards in `Docs/THREADING.md` apply. `VesselAnimation.Initialize` calls
`CrystalHullFusion.Prewarm`, so the layout is building from the moment a listed vessel spawns. A
pickup that beats it plays the generic capture (verbose line on the `CrystalMorph` channel).

## 5. Hook and retirement

`ElementalCrystalImpactor.RunCapture` → `TryFuseOntoHull`: if the config lists
`(vesselStatus.VesselType, crystal element)` and the pair's layout is ready, `CrystalHullFusion.Begin`
clones the prototype mesh, matches faces to patches, hides the crystal's renderers and draws frame 0
the same frame. The crystal stays alive,
hidden, until the **mate**, when it moves to the contact, plays its pickup sound via
`Crystal.Explode(SuppressHusk = true)` and leaves the cell. Scoring and the element level land at
contact, before any of this. The fusion is pure photons.

Every refusal falls back to the generic capture and **warns once per reason**: no hull renderer, an
empty bake, a crystal with no readable model, a layout the worker could not build. A layout still
building is not a fault: that pickup plays the generic capture with a verbose line.

## 6. Why CPU and not the omni morph's shader stamp

- the target **moves** (a skinned hull at flight speed, bone by bone) — a target stamped into a UV
  channel would be stale the frame after it was written;
- the charge shader already spends TEXCOORD1–3 on its discharge;
- it is a **one-shot per pickup** (~1.2 s, ~10.4k vertices a frame), not a standing per-prism cost.

## 7. Files

| File | Role |
|---|---|
| `Controller/Environment/Crystals/CrystalHullFusion.cs` | runtime: prewarm + worker layout cache, per-pickup match, per-frame pose, material |
| `Utility/CrystalHullFusionGeometry.cs` | pure: panels, template, contact, patches, assignment, wrap, hull surface |
| `ScriptableObjects/CrystalHullFusionConfigSO.cs` | per-(vessel, element) entries + beat timing |
| `Resources/CrystalHullFusionConfig.asset` | the opt-in: Squirrel × Charge |
| `ImpactEffects/Impactors/ElementalCrystalImpactor.cs` | `TryFuseOntoHull` / `RetireIntoFusion` |
| `Utility/CrystalEdgeArcMeshBaker.cs` | `TryGetReadable` (the drawn charge mesh is unreadable) |
| `Environment/FlowField/Crystal.cs` | `TryGetDomainCrystalColors` |
| `_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx.meta` | `isReadable: 1` |
| `Tests/Editor/CrystalHullFusionGeometryTests.cs` | panels, template, surface, layout, patches, assignment, wrap — also RUNS headless |
| `Controller/Animation/VesselAnimation.cs` | `Initialize` → `CrystalHullFusion.Prewarm` |
| `Environment/FlowField/CrystalEdgeArcs.cs` | `PlateCorners` (so a prefab reader asks for the same twin) |
| `Tests/Editor/CrystalHullFusionConfigTests.cs` | beats, slow motion, shipped opt-in |
| `Tools/Build/crystal_morph_harness/` | now also builds and runs the geometry suite (stub extended) |

## 8. Tuning knobs (`Resources/CrystalHullFusionConfig`, per entry)

| Knob | Shipped | What it does |
|---|---|---|
| `peelSeconds` / `flightSeconds` / `mateSeconds` / `dissolveSeconds` | 0.16 / 0.42 / 0.30 / 0.30 | the four beats (1.18 s; generic capture is 0.44) |
| `playbackScale` | 1 | **slow motion for inspection** — 10 to watch faces land one by one; a test refuses it shipped above 1 |
| `peelDistance` | 0.45 | how far faces lift off the crystal, crystal radii |
| `flightBow` | 0.9 | how far out over its patch a face swings before coming down, hull mean half-extents |
| `flightStagger` | 0.45 | nearest-first travelling landing; 0 = all at once |
| `spotConeDegrees` | 12 | cone the contact is searched in |
| `tileFill` | 1.15 | face size vs the gap to its neighbour (1 = just touching) |
| `surfaceLift` | 0.04 | gap to the skin, face radii (anti z-fight) |
| `flareGain` | 2.6 | brightness on landing (hue kept) |
| `arcBoost` / `mateArcDuty` | 2 / 0 | discharge intensity × and silence while mated |
| `sinkDepth` | 0.12 | face radii sunk by the end |
| `convergeToDomainColour` | on | lime pickup → pilot's domain crystal pair over the flight |

## 9. Cost

Measured with the shipped geometry on the real meshes (warm .NET; the Editor's Mono is slower, so
read the ratios):

- **Vessel spawn (main thread, once per pair):** one `BakeMesh`, the bone weights read, arrays
  copied — a few ms.
- **Worker (once per pair):** face cut ~50 ms + hull layout ~50 ms.
- **Pickup (main thread):** ~2 ms — 10.4k start positions, 60×60 Hungarian, a prototype clone.
- **Frame (main thread):** ~0.25 ms of maths + one 10.4k-vertex upload, for ~1.2 s.

`ProfilerMarker`s: `CrystalHullFusion.Prewarm`, `.Begin`, `.Frame`. If a pickup still hitches,
those three name the culprit.

A fast "nearest vertices, then their triangles" query was tried for the projection and rejected:
it missed 6–11% of points and landed the rest up to 0.95 patch radii off on the Squirrel's coarse
areas. Once the layout is cached and off-thread, exactness costs nothing anyone waits for.

## 10. Verification status

- **Compiles** against real Unity 6000.0 references, player and editor configs
  (`Tools/Build/unity_refcompile`; the first cut's run was negative-controlled with a planted
  missing member).
- **Runs headless:** `bash Tools/Build/crystal_morph_harness/run.sh` — 42/42 (21 upstream + 21
  fusion, incl. the layout builder). Three planted defects each fail it: every template edge marked a bolt, the facing filter
  removed, face normals inverted.
- **Shipped geometry on the real meshes** (scratch driver over the FBX exports): numbers in §1–§3.
- `AssignMinCost` matches brute force on 300 random matrices.
- **Not yet seen in the editor.**

### In-editor verification

1. Squirrel, skim a **charge** crystal. The frame must NOT hitch; turn on the Profiler and look
   for `CrystalHullFusion.Begin` / `.Frame` if it does. Expect: the crystal's prisms fold into their outer
   pentagons, which lift off and fly to the hull, come down onto it and lie ON it — bent over its
   curves, on top, underside and wings — crackling round their outlines, then sink in. ~1.2 s.
   Domain colour, not lime. Pickup SFX as they land. No husk spray.
2. **If it looks like the old capture, check the console first** for a `[CrystalHullFusion]`
   warning — every refusal falls back to exactly the old effect (§0).
3. To study it, set `playbackScale` to 10 on the asset (and back to 1 before committing — a test
   enforces it).
4. Pitch/yaw hard mid-fusion: wing faces stay on the wings.
5. Squirrel + mass/space/time crystal, and any other vessel + charge crystal: old capture, unchanged.
6. **FrogletTools > Toolbox > Logging > CrystalMorph** → one `[CrystalHullFusion]` line per pickup
   (faces, points, patch radius, domain colour read).
7. Run `CrystalHullFusionGeometryTests` + `CrystalHullFusionConfigTests` (edit mode).

## 11. Follow-ups / known limitations

- **Faces at a wing edge fold onto it.** Laid points past the edge project onto the edge line;
  measured outline-edge stretch ranges 0.06–1.35 (median 0.79). A patch picker that keeps away from
  silhouette edges would fix it.
- **The hull's charge blend shape glides while the faces lie on it** (the level-up lands at
  contact), so faces sit on the pre-glide surface — a few percent of a Squirrel vertex's travel.
- **The face's crackle pattern changes on the peel's first frame** — the subdivided outline's
  segments carry their own seeds, not the crystal's — and frame 0 is otherwise the crystal exactly.
- **One fusion per pickup, no pooling**: a burst of pickups is a burst of 10.4k-vertex meshes.
- **The layout is read off the hull as it was when the vessel SPAWNED** (its element shapes at
  spawn levels). A hull whose charge shape has grown since sits a few percent off the cached skin.
- **Prewarm runs only from `VesselAnimation.Initialize`.** Overrides that do not call the base
  (`ButterflyAnimation`, `ScarabAnimation`, `UrchinAnimation`, `RiptideAnimation`,
  `SparrowAnimationController` — unchecked) would build on their first pickup instead; irrelevant
  until one of them gets an entry.
- **Two opt-in mechanisms for crystal retirements now exist** (§3.5 row from the 2026-10-08
  reorient): the omni morph uses a per-vessel container slot
  (`VesselImpactorDataContainerSO.OmniCrystalRetirement`), this uses a Resources table keyed by
  (vessel, element), because elemental collection runs on the skimmer path, which has no such
  slot. Worth unifying once a second hull or element shows which shape generalises.
- **Other pairs.** The mechanism is generic over any crystal that splits into solids: the omni cage
  is 122; Time is 30 blocks but animated (it would need the animated pose sampled first); Space is
  one connected body and needs its own idea.
