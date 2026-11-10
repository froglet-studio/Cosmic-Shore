# Membrane wobble from ten numbers (genome-lab mining, prototype)

Status: prototype on branch `claude/genome-membrane-loop`, 2026-11-10. Not switched on anywhere: the shipped
`CapsuleMembrane.prefab` still plays its baked asset until someone flips one field. Came out of the Amoebius genome
lab's Unity mining pass (the project that searches tiny procedural "genomes" against game footage); this is its
"procedural compression" case: an asset that was already a pure function of a few numbers, stored as megabytes.

## What it is

The cell membrane (`CapsuleMembrane`, 2,562 instanced capsules on a radius-1200 icosphere) wobbles from a loop that
the inspector's Bake button precomputes into `Assets/_SO_Assets/Cell Data/CapsuleMembraneAnimation.asset`:
60 keyframes x 2,562 rotations. That loop is a deterministic function of the ten-field `BakeSignature`
(subdivisions, radius, seed, two jitters, noise frequency and amplitude, pulse speed, loop length, keyframe rate).

`CapsuleMembraneLoop` runs that function at runtime instead:

- **On demand.** A keyframe is generated the first time playback needs it, and the keyframe after next is warmed a
  third at a time per matrix update (`Warm`), so no update pays for a whole keyframe after the first two. After one
  loop (12 s) every keyframe exists and generation stops.
- **Shared.** One loop per distinct signature, process-wide; the 92 cell configs that use the membrane share it, as
  they share the asset today.
- **Identical.** The bake button now writes this same loop into the asset (`BakeAnimationInto` calls it), with the
  original expression order kept, so baked and generated agree to float precision. `CapsuleMembraneLoopTests`
  compares the generated loop with the shipped asset capsule by capsule.
- **Per-cell variety for free.** `CapsuleMembrane.ApplyWobble(seed, frequency, amplitude, pulseSpeed)` gives one
  membrane its own wobble with no bake and no asset (a "membrane genome"). A baked pipeline would need one 3.4 MB
  asset per variant.

## Numbers

| | Baked asset (today) | Generated |
|---|---|---|
| On disk / in build | 3,400,965 B (measured; base64 floats, compresses poorly) | the 10 inputs already on the prefab, 0 B extra |
| Managed memory while playing | the serialized base64 string (~3.3 M chars, ~6.6 MB as UTF-16) + 2.46 MB of `Quaternion[]`, plus ~4.9 MB of transient decode buffers (inferred from `CapsuleMembraneAnimationSO.Decode`) | 2.46 MB of `Quaternion[]` once all 60 keyframes exist; nothing else |
| Load | decode 2.46 MB on first access | two keyframes (5,124 capsule rotations) when the first membrane wakes |
| Per matrix update (20 Hz) | interpolate 2,562 rotations | the same, plus at most 854 rotations of warming during the first 12 s |
| A new wobble | re-bake, a new 3.4 MB asset | `ApplyWobble(...)` |

Not measured: the CPU time of one keyframe on a phone. Each capsule is three `Mathf.PerlinNoise` calls, one
`Quaternion.Euler`, two quaternion-vector products and one `LookRotation`, the same work as one capsule of the old
per-frame fallback; a third of a keyframe is 854 of those. Profile `CapsuleMembrane.UpdateMatrices` (the existing
`ProfilerMarker`) on a low-tier phone before switching the prefab.

## How to switch it on

1. On `Assets/_Prefabs/Environment/CapsuleMembrane.prefab`, set **Animation Source** to `GenerateFromSettings`.
2. Play a cell; the inspector says "Generated from settings - no bake needed".
3. Once satisfied, clear the prefab's **Animation Preset** and delete `CapsuleMembraneAnimation.asset` (3.4 MB out of
   the build). Keep the bake button: it still writes the same loop for anyone who wants a baked copy.

## Files

- `Assets/_Scripts/Game/Environment/CapsuleMembraneLoop.cs` (new): the generator, cache, icosphere and base layout
  (moved out of `CapsuleMembrane`, unchanged).
- `Assets/_Scripts/Game/Environment/CapsuleMembrane.cs`: `animationSource` field, generated playback path,
  `ApplyWobble`; the bake now goes through the loop.
- `Assets/_Scripts/Game/Environment/CapsuleMembraneAnimationSO.cs`: `BakeSignature` is `IEquatable` (cache key).
- `Assets/_Scripts/Editor/CapsuleMembraneEditor.cs`: status line for the generated mode.
- `Assets/_Scripts/Tests/Editor/CapsuleMembraneLoopTests.cs` (new).

## Next steps it opens

- A per-cell membrane genome: a few bytes on `CellConfigDataSO` (Shombith's) passed to `ApplyWobble`, so cells
  differ in their membrane as well as their spawns. Together with the cytoplasm's `SnowChanger.IFlow` and tint hooks
  this is the "~60-byte cell genome" from the mining report (`docs/UNITY_MINING.md` in the genome project).
- The same pattern applies to the other baked-from-numbers assets the audit found: `SpindleWireRing*.asset`
  (2.4 MB, written by `Tools/Build/author_spindle_lod_meshes.py`) and the four `CrystalHullFusion/*_FusionTemplate.asset`
  (4.07 MB).
