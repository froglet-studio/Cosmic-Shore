# The cell genome (prototype, 2026-11-10)

A **cell genome** is 56 bytes that make one cell look unlike another: its membrane's wobble and tint, its
cytoplasm's flow and tint, and a reserved block of ambient-sound numbers. It is **visual only**. Stacked on
`claude/genome-membrane-loop` (`Docs/GENOME_MEMBRANE_LOOP.md`), whose `CapsuleMembrane.ApplyWobble` lets one
membrane have its own wobble with no bake.

Why: across the 104 cell configs the cytoplasm is the same `SnowChanger` in all 104 and 92 use the same membrane,
and `CellConfigDataSO` has no palette, fog, light or audio fields. Every cell reads the same. A few bytes per cell,
fed into hooks that already exist, can make each one distinct.

## The pieces

All in `Assets/_Scripts/Controller/Environment/CellGenome/` (namespace `CosmicShore.Gameplay`):

- `CellGenome`: a serializable struct with the genes, `Pack()` / `TryUnpack()` (56 bytes), `ToBase64()` /
  `TryFromBase64()`, `Quantised()`, `FromSeed(int)` and `StableHash(string)` (FNV-1a over a cell's name; not
  `string.GetHashCode`, which can change per process).
- `CellGenomeFlow`: the cytoplasm flow, implementing `SnowChanger.IFlow`.
- `CellGenomeSO`: a ScriptableObject holding one genome, with the packed base64 shown beside it.
- `CellGenomeApplier`: a MonoBehaviour that dresses one `Cell` on Start and undresses it on disable.
- Tests: `Assets/_Scripts/Tests/Editor/CellGenomeTests.cs`.

## Genes and byte layout (version 1, little-endian)

| Bytes | Gene | Encoding | Range |
|---|---|---|---|
| 0 | layout version | byte | 1 |
| 1-4 | membrane wobble seed | int32 | any |
| 5-6 | wobble noise frequency | u16 / 65535 x 2 | 0-2 (shipped 0.6) |
| 7-8 | wobble noise amplitude | u16 / 65535 x 40 | 0-40 (shipped 10) |
| 9-10 | wobble pulse speed | u16 / 65535 x 2 | 0-2 (shipped 0.4) |
| 11-13 | membrane bright tint, HSV offset | 3 x s8 / 127 x 0.5 | each -0.5 to 0.5 |
| 14-16 | membrane dull tint, HSV offset | 3 x s8 | same |
| 17-19 | cytoplasm tint, HSV offset | 3 x s8 | same |
| 20 | flow enabled | byte (0/1) | |
| 21 | flow base stretch | u8 / 255 | 0-1 |
| 22-39 | three flow terms, 6 bytes each: kind (Off, Swirl, Abc, Rings), axis yaw (u8 of 360), axis pitch (u8 of 180, -90 to 90), frequency (u8 of 4, cycles per membrane radius), strength (s8, -1 to 1), phase (u8, 0-1) | | |
| 40 | ambient pitch | s8 / 127 x 12 | -12 to 12 semitones |
| 41-43 | ambient volume, low-pass, rate | u8 / 255 | 0-1 |
| 44-54 | reserved | zero | |
| 55 | checksum | rotate-xor over bytes 0-54 | |

`TryUnpack` refuses a wrong length, version or checksum. Unpacking packed bytes and packing again gives the same
bytes, so a search can mutate the packed form directly.

The ambient block is just numbers: nothing reads it, and no FMOD event or `EventReference` was added. When a sound is
wired, its slot follows the LOCKED FMOD rule (an empty exposed `EventReference`).

## The flow

`CellGenomeFlow` sums up to three terms, in membrane radii from the membrane's centre (positions outside are pulled
onto the unit ball so a far shard still gets a finite answer):

- **Swirl**: rotation round the term's axis, its speed banded along the axis.
- **Abc**: an Arnold-Beltrami-Childress cell (A 1, B 0.7, C 0.5) in the term's frame, divergence-free, with
  chaotic streamlines.
- **Rings**: radial breathing rings out from the centre.

|velocity| is bounded by `MaxMagnitude` (the sum of |strength| x 3.2). The shard's stretch is
`baseStretch + (1 - baseStretch) x |v| / MaxMagnitude`. At a still point it returns false and that shard keeps the
shipped law (pointing at the crystal). It does not allocate and is asked once per shard per reorientation pass, as the
`IFlow` contract asks.

## FromSeed

`FromSeed(seed)` makes a tasteful genome inside safe ranges, deterministic on every platform (xorshift32, not
`UnityEngine.Random`). The wobble stays near the shipped values (frequency 0.45-0.8, amplitude 7-14, pulse
0.28-0.55). Tints are small (hue ±0.05, saturation and value ±0.08). The flow has two or three terms, strength 0.3-0.9
and frequency 0.5-2. Calling `FromSeed(StableHash(cell.name))` gives each of the 104 cells its own genome with no
asset.

## How it is wired, now and later

- **Now:** add a `CellGenomeApplier` next to a `Cell` or under it, in a scene or at runtime. Without a `CellGenomeSO`
  it uses the cell's name hash. It finds the membrane through `Cell.MembraneVisual` and the cytoplasm through
  `Cell.CytoplasmVisual`. The cell spawns the cytoplasm late, when a crystal arrives, so the cytoplasm genes are
  applied on the first frame it exists. No shipped prefab or scene was changed.
- **Later (Shombith's call):** one optional `CellGenomeSO` reference on `CellConfigDataSO`, which `Cell` would apply
  when it builds its visuals. An empty reference would fall back to the name hash or to no genome at all.
  `CellConfigDataSO` was not edited.

## What it does not touch

The genome touches no gameplay and no LOCKED ecology rule (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`). It changes
no boundary, volume, spawn, phase, crystal or collider. The membrane wobble is drawn only: "Gameplay never reads the
wobble" (`CapsuleMembrane.ApplyWobble`). Tints go through per-draw property blocks or the cytoplasm's own material
clone, never the shared `SpindleMaterial`. The flow only decides where a shard points and how long it is drawn.

## Known edges

- **Restore after a baked wobble.** On disable, the applier gives the membrane back its own settings through
  `ApplyWobble`, which leaves it on the generated loop. The membrane-loop branch says the generated loop matches the
  bake to float precision, but nobody has looked at that in Unity.
- **`CellVisualTint`** (a mode's tint) also writes the membrane and cytoplasm overrides, and its `Restore` returns to
  the material colours. Where both run, whichever writes last wins. A combined palette (genome tint as the base the
  mode eases from) is the next step if both are wanted.
- **A mode's own flow.** Serpent Time sets its own flow. Untick the applier's *Apply Flow* there, or the genome's flow
  replaces the race line.
- **`SnowChanger.SetFlow` on a cell with no crystal builds the shard field.** That is `SnowChanger`'s own behaviour.
  The cytoplasm hook is only reached once the cell has spawned its cytoplasm.

## Unverified

- Nothing here has been looked at in Unity: the look of the flows and tints, and whether the ranges FromSeed uses read
  as tasteful.
- The EditMode tests have not been run in Unity. The code compiled with `Tools/Build/unity_refcompile` (player and
  editor configs).
