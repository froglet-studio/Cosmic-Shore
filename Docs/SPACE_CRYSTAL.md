# Space Crystal — mesh, spin, and why its normals are authored by a tool

The space crystal is 60 rigid blocks arranged with icosahedral symmetry. It idles by **spinning**:
groups of blocks rotate about a 5-fold axis (5 blocks per group, 72°) or a 3-fold axis (3 blocks
per group, 120°). Every block lands in another block's slot, so the end pose is congruent to the
start, and the cycle can snap back to rest without a visible pop.

| Piece | Where |
|---|---|
| Artist source (Blender export, 4 shape keys) | `Assets/_Models/SpaceCrystalExport1_7-17-25.fbx` |
| Shipped mesh (what every space crystal renders) | `Assets/_Models/spacecrystalanim.fbx`, mesh fileID `-5993354799466719267` |
| Generator (source to shipped, with correct normals) | `Tools/Build/author_space_crystal_mesh.py` (`--check`, `--report`) |
| Driver | `Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs` |
| Consumers | `CrystalSpace`, `ActiveCrystalSpace`, `SpaceDandruff`, and the heart crystals on `GyroidFlora`, `TadPoleFauna`, `MassSharkFauna` and `MassBrittlestarFauna` (11 renderers, one mesh). The flora prefabs nest `CrystalSpace`. |

## 1. The shape-key design

Each spin is two keys, **stacked**:

```
phase 0.0 → 0.5   1stHalfSpin  0 → 100
phase 0.5 → 1.0   2ndHalfSpin  0 → 100   (1stHalfSpin held at 100)
phase 1.0         both → 0 on the same frame   (congruent pose: invisible)
pause, then the other spin (5-point, 3-point, 5-point, …)
```

Measured from the export: each block is a near-rigid 36° / 60° rotation at the half pose, and
`base + 1st + 2nd` is a permutation of the base vertices to 3e-6. The vertex motion is the
artist's and is shipped unchanged.

## 2. Why the normals were wrong (every previous attempt)

Three faults stack. Only the first is visible at a glance:

1. **Stacking.** Unity blends normals as `n0 + Σ wᵢ·Δnᵢ`. Every exporter, and Unity's own
   *Calculate* mode, derives each key's `Δn` against the **base** mesh. Rotations don't add, so
   while the 2nd half runs on top of the 1st, the summed normal is wrong. It is off by up to
   **19.7° (5-point)** and **42.5° (3-point)**, and the reset pops by exactly that much. Blender
   can't fix this at export: its FBX exporter writes all-zero shape normals. Importing those gives
   41.7° / 118° errors, because the normals never move at all.
2. **Hard edges vs. per-control-point shape normals.** FBX stores shape normals per control
   point. Each corner of a block is shared by 3 faces with 3 different normals, so a single delta
   can't be right for all three.
3. **Linear interpolation inside one key.** Even a correct end normal is lerped linearly. That
   misses the true face normal mid-key by up to 2.6° / 8.3°.

## 3. What the generator writes

`python3 Tools/Build/author_space_crystal_mesh.py --report` prints this table (max error against
the true face normal, in degrees):

```
spin           scheme                   1st half 2nd half
5PointRotate   Unity Calculate (before)     2.58    19.65
               this tool, 4 frames/key *     0.18     0.18
3PointRotate   Unity Calculate (before)     8.31    42.47
               this tool, 4 frames/key *     0.61     0.61
```

- **Unwelded mesh.** One control point per polygon corner (1440), so a per-control-point shape
  normal *is* a per-face normal.
- **2nd-half normals are relative to the 1st-half end pose.** The sum Unity computes is then
  exact, and the end normal equals the base normal of whichever face now occupies that slot:
  0.0005° at the reset, so there is no pop. **Consequence: a 2nd-half key is only correct with its
  1st-half key at 100.** The animator always drives it that way.
- **4 in-between frames per key** (25/50/75/100), each with exact normals. Every in-between
  *position* lies on the original straight line, so playback motion is identical to the source.
- **Axis and scale.** The geometry is converted into the target file's axis system (a proper
  rotation, so chirality is kept) and scaled ×82.7 so the outer radius equals the old mesh's
  (74.40 raw). Every prefab's scale and collider still fit, and the heart-seat measurement
  matches the old mesh to 0.001.
- **Identity kept.** The file keeps its guid, its `space` Model name and its
  `Deltoidal hexecontahedron (1)` Geometry name, so Unity's name-derived mesh fileID is unchanged
  and no prefab needed editing.
- **Import setting.** `spacecrystalanim.fbx.meta` sets `blendShapeNormalImportMode: 0`, so Unity
  imports the file's normals. *Calculate* would throw them away and recompute the wrong ones.
  `--check` asserts this.

Independent check: assimp reads back every frame's normal equal to the expected value within
0.003°, positions on the line within 4e-5. **Use the per-quad (Newell) normal as the reference,
never a single triangle's.** The artist's half poses twist some quads by up to 14° (the blocks
are near-rigid, not rigid). The shading uses one normal per quad, so a triangle-based reference
reports false errors of 6–25°.

## 4. The material: `_spread` is off for the block mesh

Both space crystal materials are on `CrystalGraph`. Its vertex stage moves every vertex along its
own **object-space normal** by `_spread × (cos t + 1.5)`. On the old mesh (60 separate kites)
that made the shell breathe. On 60 closed blocks it pulls each block's six faces apart by
0.075–0.375 mesh units, against block edges of 0.07–0.32. So `SpaceCrystalMaterial` and
`ActiveSpaceCrystalMaterial` now author `_spread: 0`: the model encodes its own spread, the same
call `KEY_SYSTEMS_AND_CONVENTIONS.md` records for the Charge crystal.

`SpaceCrystalMaterial` was also used by two time-crystal prefabs on other meshes
(`CrystalTimeDandruff`, `OldCrystalTime`). They now point at **`TimeDandruffCrystalMaterial`**, a
material *variant* of `SpaceCrystalMaterial` whose only override is `_spread: 0.15`. Their look
is unchanged, and colour edits to the parent still reach them.

The heart crystals on fauna use `BlueMassCrystalMaterial` (`ShepardGraph`). At rest its vertex
stage is a uniform scale pulse about the origin, which keeps blocks intact, so it is unchanged.

If the breathing is wanted back, make it **radial** (a uniform scale, like `ShepardGraph`), never
along face normals.

## 5. Updating the model

### 5a. You changed the shape keys or the blocks in Blender (normal path, no Unity steps)

1. Keep the key **names** (`5PointRotate-1stHalfSpin`, `5PointRotate-2ndHalfSpin`,
   `3PointRotate-1stHalfSpin`, `3PointRotate-2ndHalfSpin`), each 2nd-half key **relative to Basis**
   (the default), and the design rule: `Basis + 1st + 2nd` must land every vertex on another
   vertex. The generator asserts that and refuses otherwise.
2. Keep the blocks **Shade Flat** (the generator refuses smooth source normals). Then
   *File ▸ Export ▸ FBX*: *Limit to ▸ Selected Objects*, *Object Types* = Mesh only,
   *Geometry ▸ Smoothing* = Normals Only, *Apply Modifiers* on, *Triangulate Faces* **off** (the
   tool expects quads), and *Shape Keys* exported (the default). Axis and scale settings don't
   matter: the generator reads the file's declared axes and rescales to the shipped radius.
   Overwrite `Assets/_Models/SpaceCrystalExport1_7-17-25.fbx`.
3. `python3 Tools/Build/author_space_crystal_mesh.py` — then commit both FBX files.

The normals Blender writes for shape keys are ignored. That's the point: the tool computes them.

### 5b. The superior alternative, only if you want RIGID blocks: bones instead of shape keys

Shape keys move each vertex in a straight line. That's why the 3-point spin's blocks squash to
**73%** of their size mid-key and twist up to 14° at the half pose. If that should read as a true
spin, rig it instead. Skinning rotates normals exactly, with no tool step at all:

1. Separate the mesh by loose parts (*Edit Mode ▸ P ▸ By Loose Parts*), then *Join* them back.
   Or keep one object and use vertex groups: one group per block, `Block_00` … `Block_59`.
2. Add an Armature with **one bone per block**, head at the crystal's origin, named after the
   block's vertex group. Parent the mesh with *Armature Deform ▸ With Empty Groups*, then assign
   each block's vertices to its own group at weight 1.
3. Animate two actions, each one spin: every bone rotates about **its group's axis** (5-fold for
   `Spin5`, 3-fold for `Spin3`) by 72° / 120° over the clip, using rotation mode *Axis Angle* or
   *Quaternion*, never Euler. Every block lands in a neighbour's slot, so the last frame equals
   the first frame's pose and the clip loops.
4. Export with *Armature* + *Mesh*, *Add Leaf Bones* off, *Bake Animation* on. In Unity,
   SpaceCrystalAnimator would then swap from blend-shape weights to an Animator playing the two
   clips. That is a code change, so ask for it.

Cost: 60 skinned bones per crystal against 2 active blend-shape channels. Only worth it if the
squash is a visible problem.

## 6. Verification status

- **Offline (done):** generator `--check` green, and negative-controlled: a corrupted byte fails
  it, and so does `blendShapeNormalImportMode: 1`. assimp re-read with normals matching to 0.003°.
  `SpaceCrystalAnimator` compiled and run in a Roslyn harness, with blend-shape names resolved in
  shuffled order with a deformer prefix. The 2nd half is never non-zero without its 1st half at
  100, and collect lands on the end pose then shrinks to zero. A mutant that swaps the halves
  fails the harness. `Tools/Build/unity_refcompile` gives **0 errors in project code**.
- **In editor (NOT yet run — the Unity CLI was unavailable in the session that made this):**
  1. Reimport `spacecrystalanim.fbx`. The inspector should list 4 blend shapes with no import
     warnings.
  2. Open `CrystalSpace.prefab` and scrub the 4 weights in the 1st→2nd order. The shading should
     track the faces, and zeroing both at the end should not pop.
  3. Play any mode with space crystals. Watch the spin alternate 5-point / 3-point, and collect one
     (it should finish its spin and shrink).
  4. `CrystalTimeDandruff` (via `PrismManagers`) should look unchanged.
