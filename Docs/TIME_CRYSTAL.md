# Time Crystal — a procedural flip wave that starts from a new vertex every loop

The Time crystal is 30 rigid golden-rhombus plates on an icosidodecahedral shell (the exploded faces of a
rhombic triacontahedron). Every 2 s a wave runs across it from one five-fold vertex to the opposite one:
ring by ring, each plate turns 180° about one of its own diagonals. The crystal itself never turns. Each
wave starts from a different vertex than the one before, out of the 12.

It used to be an Animator playing the artist's take, plus a component (`TimeCrystalVertexHop`) that
snapped the whole model to a random symmetry of the shell each loop, so the take's one fixed start vertex
appeared to wander. Since 2026-10-08 the wave is **procedural**: no Animator, no clip, no snap. The take is
now the reference the procedural wave is generated from and tested against.

| Piece | Where |
|---|---|
| Model | `Assets/_Models/TimeCrystalExport.fbx` — skinned, one bone per plate, **Read/Write enabled** (the runtime measures the plates) |
| Reference take | `TimeCrystalArmature\|TimeSequenceAnimFinal.001` in the same FBX (frames 0–50 at 25 fps, one key per frame, linear). Played by nothing; read by the generator and the parity test. |
| Driver | `Assets/_Scripts/Controller/Environment/Crystals/CrystalFlipWave.cs` (on the crystal ROOT, poses the model's bones) |
| Profile | `FlipWaveProfileSO` → `Assets/_SO_Assets/Environment/TimeCrystalFlipWaveProfile.asset` (**generated**) |
| Math | `Assets/_Scripts/Utility/FlipWave.cs` (plates, plan, pose), `FlipWaveRig.cs` (bones), `IcosahedralSymmetry.cs` (frame) |
| Generator + source proof | `python3 Tools/Build/author_time_crystal_flip_wave.py` (`--check`, `--self-test`) |
| Tests | `Assets/_Scripts/Tests/Editor/CrystalFlipWaveTests.cs` — including **FBX parity** |
| Consumers | `CrystalTime.prefab` and its variant `CrystalTimeDandruff.prefab` (`ElementalCrystalSet.time`). `OldCrystalTime.prefab` is legacy and still tumbles. |

## 1. What the take actually is — measured, not described

`author_time_crystal_flip_wave.py` asserts each of these from the FBX. Re-run it after any re-export.

1. **The rest mesh is icosahedrally symmetric** (worst vertex mismatch under the group generators 4e-7),
   so a wave from any vertex looks like the authored one.
2. **Both ends of the take are the bind pose as a shape** (worst 7.5e-7). A plate turned 180° about a
   diagonal is the same plate, so the wave can snap from "all flipped" back to rest at the loop wrap. Note
   that it is the same SHAPE, not the same vertices: the take's frame 0 has many bones already turned 180°,
   so every per-frame measurement is taken against frame 0's own pose.
3. **Rings and cadence.** The plates flip in rings by height along the start axis: **5 / 5 / 10 / 5 / 5**.
   Ring *k* starts on frame 5(*k*+1) (0.2 s + 0.2 s·*k*) and every ring flips in 25 frames (1.0 s), so the
   last ring lands exactly on the loop end. Rings 1–4 share **one** curve (a symmetric ease). Ring 0, the
   **lead ring** round the start vertex, has its own faster-in curve plus a **squash to 0.90** and a
   **pinch**: its centroid dips 0.048 of a plate's reach inward and slides 0.080 toward the start vertex
   mid-flip. A plate's reach is half its long diagonal.
4. **Axis and sense.** Each plate turns about whichever of its two diagonals is **more perpendicular to the
   wave's direction across it**. For the polar rings that is exactly perpendicular, and for the equator
   ring it is the diagonal 58° off. The plate's outer face **rolls WITH the wave**, away from the start
   vertex. The predicted signed axis matches every block to 0.00°.

**FBX artefacts the fit ignores.** Seven blocks miss a key at the start or end of their flip. Two
equator blocks (`89`, `Bone.002`) start a frame late. Some blocks creep about 0.03° per frame across a key
gap before their ring starts, or for the last 0.12° at the end. The shared curves are fitted from the
on-cadence blocks only. All 30 are then held to the proof budget below.

## 2. How it runs

- **Frame.** The FBX declares a Z-up axis system, and how Unity signs the converted axes decides which of
  the icosahedron's two coordinate-aligned orientations the model lands in. So nothing about the frame is
  authored. `FlipWaveRig` skins every vertex into the model root's local space at rest and measures each
  plate (centroid, radial, diagonals, reach). Each plate's outward direction is a two-fold axis, and
  exactly one orientation puts every one of them 31.72° from two vertices
  (`IcosahedralSymmetry.TryResolveFromTwoFoldDirections`, tolerance 2°). That yields the 12 start
  vertices. No bone names are involved, so the old `leadBlockBones` list is gone.
- **Plan.** For each of the 12 vertices, `FlipWave.Plan` assigns every plate its ring, signed axis and
  toward-the-start direction (§1.3–1.4). The plates and the 12 plans are measured once **per mesh** and
  shared by every instance of the prefab.
- **Pose.** Each frame the profile is sampled once **per ring**, and each bone whose ring sample changed
  gets one `SetLocalPositionAndRotation`, plus a `localScale` write only while the lead ring squashes.
  Rings at rest cost nothing. A crystal whose renderer is not visible does no work at all. Its pose is a
  pure function of its clock, so it is right again the frame after it comes back into view. That is the
  same one-frame lag as Animator culling.
- **Clock.** Scaled `Time.time` from `OnEnable`, so a re-pooled crystal restarts at rest on a fresh
  wave. At each wrap the next vertex is drawn uniformly from the other 11 by a per-instance
  `System.Random`, never `UnityEngine.Random`, which is seeded for deterministic content.

Cost versus what it replaced: the Animator (which also ran **off screen**, at `m_CullingMode: 0`) and its
clip evaluation are gone. In their place is at most 30 bone writes per visible crystal per frame, and
fewer most of the time. The skinned mesh renders exactly as before. This has not been profiled yet.
Profile a scene with many Time crystals (`CrystalFlipWave.LateUpdate` has a `ProfilerMarker`) before
deciding whether it needs to move to a job or the GPU.

## 3. Proof

| What | How | Result |
|---|---|---|
| The procedural rule rebuilds the take | generator, Python, all 240 verts × 51 frames + 50 midpoints | worst **0.42 %** of the shell radius (the source's own Euler-key noise) |
| The SHIPPED C# rebuilds the take | Roslyn harness running `FlipWave`/`FlipWaveRig`/`FlipWaveProfileSO`/`IcosahedralSymmetry` unmodified against the FBX and the generated asset | worst **0.42 %**; seam (end of every wave vs rest) 4.6e-7 |
| …under every axis conversion an importer could apply | same harness, all **48** signed axis permutations (mirrors included) built as transforms above the armature | 48/48 resolve, worst 0.42 % |
| Each part of the rule matters | negative controls: axis sign, wrong diagonal, no pinch, no squash, stagger ±1 frame, unreadable mesh, soft skin, 5°-tilted frame | every one fails loudly |
| **FBX parity in Unity** | `CrystalFlipWaveTests.ProceduralWave_MatchesTheImportedTake_FrameByFrame`: Unity's own import of the take vs the rig + shipped profile on the real model, every frame and midpoint | tolerance 1.5 % of the radius (Unity's keyframe reduction is set to 0.5°); **runs only in the Editor** |

## 4. Rules

- **Never put an Animator back on this model, and never put a rotation driver on it.** An Animator would
  fight the wave for the bones every frame. A tumble (`JustRotate`) would turn the shell between waves and
  make each wave start read as a jump. The edit-mode test fails on either.
- **The profile asset is generated.** To change the Time crystal's motion, change the take and re-run
  `author_time_crystal_flip_wave.py`. To make a *different* motion, author a copy of the asset;
  `--check` fails on a hand edit of the generated one.
- **The mesh stays Read/Write enabled.** Without it a player has no vertex data and the crystal holds
  still, with one console error naming the cause.
- **The root's rotation is not this component's.** The capture flourish
  (`ElementalCrystalImpactor.RunCapture`) and the spent-husk spawn own it. The wave writes only bone poses.
- **A re-export that changes the plates, the skinning or the frame breaks the wave loudly**, with one
  console error saying what was refused: an unreadable mesh, a non-rigid skin, a plate that is not a thin
  tangent slab, or directions off the two-fold axes. Re-run the generator, then the parity test.

## 5. The Omni crystal's rhombi (next)

The Omni crystal's body (`OmniCrystalExport1_8-21-25.fbx`) is an exploded polyhedron of 122 plates. Of
those, **30 are rhombi**, which is Time's shape family. The rest are 20 triangles (Mass), 12 pentagons
(Charge) and 60 boxes. Giving the 30 rhombi this wave reuses `FlipWave`, `FlipWaveProfileSO` and the frame
resolution unchanged, provided the rhombi sit on the shell's two-fold axes, which still has to be
measured. What differs:

- **Diagonal ratio 0.597**, against the Time crystal's golden 0.618. The rules pick a diagonal by its
  angle to the wave, not by its length, so this is fine. The profile's pinch is in units of each plate's
  own reach, so the shared profile scales with the plate.
- **The rhombi are part of a static body mesh** drawn by several overlays (`OmniCrystalTriangles`,
  `OmniCrystalChargeEdges`, team materials). They have to be split into something with one transform
  per plate before anything can turn them. The two options are a skinned plate rig baked by a generator,
  which suits `FlipWaveRig` as it stands, or separate plates. Decide by draw-call cost, because the
  Omni's overlays multiply it.

## 6. Open rows (seen while building this, not fixed here)

- **`OldCrystalTime.prefab` looks dead.** Its guid `cb1d516a59d28e64d87b8dcc2abaf7b9` appears in no
  `.unity`, `.prefab` or `.asset` file (2026-10-08). It sits under `_Prefabs/`, not `Resources/`, and
  no code names it. It still carries a `JustRotate` tumble. It is a deletion candidate, but re-prove
  it unreferenced (including any `Resources.Load` or Addressables path) at the time of deletion, not
  from this note. *Report, not fix.*
- **`JustRotate.cs:15` `if (direction == null)` can never fire.** `Vector3` is a struct, so the
  fallback to `Vector3.forward` for an unset direction is dead code. A `JustRotate` left at the
  default zero direction rotates nothing, silently. *Inconsistency: a one-line fix in its own
  change.*
- **Retired, not open:** the shell's 0.00027 off-centre offset no longer matters. Every plate now turns
  about its own measured centroid, and the shell centre is the mean of those centroids.
