# Time Crystal — a still crystal whose flip wave hops between vertices

The Time crystal is 30 rigid blocks on an icosidodecahedral shell. Its one animation flips every
block 180° in a wave that runs from one five-fold vertex to the opposite vertex, and loops every 2 s.
It does **not** tumble. Each time the loop wraps, the model snaps in a single frame to a random
rotation of the icosahedral group. The shape at that moment is congruent under every one of those
rotations, so the player sees no turn. They only see the next wave start from another of the
12 vertices, never the same one twice in a row.

| Piece | Where |
|---|---|
| Model + take | `Assets/_Models/TimeCrystalExport.fbx`, take `TimeCrystalArmature\|TimeSequenceAnimFinal.001` (frames 0–50 at 25 fps), Animator `CrystalTimeAnimController` |
| Driver | `Assets/_Scripts/Controller/Environment/Crystals/TimeCrystalVertexHop.cs` (on the crystal ROOT, rotates the model CHILD) |
| Group math | `Assets/_Scripts/Utility/IcosahedralSymmetry.cs` |
| Source-model proof (READER) | `python3 Tools/Build/measure_time_crystal_wave.py` (`--self-test`) |
| Tests | `Assets/_Scripts/Tests/Editor/TimeCrystalVertexHopTests.cs` |
| Consumers | `CrystalTime.prefab` and its variant `CrystalTimeDandruff.prefab` (`ElementalCrystalSet.time`). `OldCrystalTime.prefab` is legacy and still tumbles. |

## 1. Why the snap is invisible — the four facts it rests on

`measure_time_crystal_wave.py` asserts each of these from the FBX itself. Re-run it after any
re-export.

1. **The rest mesh is icosahedrally symmetric**, with its two-fold axes on the coordinate axes. The
   worst vertex mismatch under the group generators is 4e-7.
2. **The loop seam is the bind pose.** At t = 0 and t = 2 s every block sits exactly on its bind
   vertices (worst 7e-7). A block flips 180° about an axis that maps it onto itself, so the end of
   the loop is the same shape as the start.
3. **The wave runs pole to pole, with a still window at the seam.** Flip order correlates with
   height along the start axis (r = 0.97). No block moves more than 1° for the first 0.24 s, so the
   snap has a window to land in, not a single frame. Two blocks (`89`, `Bone.002`) ease in from
   t = 0 at about 0.02° per frame, which is not visible.
4. **The first ring's bones name the start vertex.** The five blocks that flip first are
   `Bone.019/020/029/030/031`. Their bone heads sit on the pentagon around the start vertex, so
   their rest centroid lies exactly on it. They are the prefab's `leadBlockBones`, and the tool
   fails if the two lists disagree.

## 2. Why the frame is measured at runtime, not authored

The FBX declares a Z-up axis system (`UpAxis 2`, `FrontAxis +1`, `CoordAxis −1`). Unity converts it
by permuting and negating axes, and which way it signs them decides which of the icosahedron's two
coordinate-aligned orientations the model ends up in. Those orientations are cyclic permutations of
(0, ±1, ±φ) and of (0, ±φ, ±1), 90° apart. Their rotations are not symmetries of each other, so a
hard-coded group would be wrong half the time. No asset in the repo anchors this axis system: the
validated `(−x, z, −y)` mapping in `Docs/VESSEL_TAIL_AND_JETS.md` is for Y-up files. The mesh is
not Read/Write either (`isReadable: 0`), so the runtime cannot inspect its vertices.

So in `Awake` the component resolves the five lead bones by name, takes their centroid in the model
root's local space, and snaps it onto the nearest of the 24 five-fold directions of both
orientations, with a 2° tolerance. The measured offset is 0°, and the nearest axis of the wrong
orientation is 26.6° away. That single snap fixes both the start vertex and the orientation, and the
group is generated from it. Offline, this resolver was run on the FBX's real bone heads and mesh
under all 48 signed axis permutations. All 48 recovered the true start vertex, and all 60 snaps
left the mesh congruent. A tilted frame, the wrong bone ring and a missing bone are each refused.
A refusal logs an error and disables the hop, and the crystal keeps animating in place.

## 3. Rules

- **Never put a rotation driver on this crystal's model again.** A tumble (`JustRotate`) makes the
  snap visible, because it rotates the shape between symmetric poses. The edit-mode test fails on
  one.
- **The root's rotation is not this component's.** The capture flourish
  (`ElementalCrystalImpactor.RunCapture`) and the spent-husk spawn own it. The hop writes only the
  model child's `localRotation`.
- **Random picks use a per-instance `System.Random`, not `UnityEngine.Random`.** The global stream
  is seeded for deterministic content (the SkimRace track, `GunSpreadMath`), and a per-frame visual
  must not consume from it.
- **A re-export that renames bones, changes the take, or moves the wave breaks the hop loudly**, with
  one console error naming the problem. Re-run `measure_time_crystal_wave.py` and update
  `leadBlockBones` from its output.

## 4. Open rows (seen while building this, not fixed here)

- **`OldCrystalTime.prefab` looks dead.** Its guid `cb1d516a59d28e64d87b8dcc2abaf7b9` appears in no
  `.unity`, `.prefab` or `.asset` file (2026-10-08). It sits under `_Prefabs/`, not `Resources/`, and
  no code names it. It still carries a `JustRotate` tumble. It is a deletion candidate, but re-prove
  it unreferenced (including any `Resources.Load` or Addressables path) at the time of deletion, not
  from this note. *Report, not fix.*
- **`JustRotate.cs:15` `if (direction == null)` can never fire.** `Vector3` is a struct, so the
  fallback to `Vector3.forward` for an unset direction is dead code. A `JustRotate` left at the
  default zero direction rotates nothing, silently. *Inconsistency: a one-line fix in its own
  change.*
- **Known and accepted:** the shell is centred 0.00027 file units off the model origin along the
  file's Z axis, and the hop rotates about the origin. Each snap therefore shifts the shell by at
  most 0.00054 local units (about 0.001 world units at the prefab's 2.13 scale), well below
  visibility. Not debt. If a future model has a real offset, rotate about its centre.
