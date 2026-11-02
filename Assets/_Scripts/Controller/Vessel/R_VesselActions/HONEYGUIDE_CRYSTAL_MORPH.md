# The Honeyguide's omni-crystal morph: the crystal becomes the hive

**The crystal does not shatter. It breaks into the heart of the hive comb.**

"The omnicrystal becomes the hive" (`HoneyguideCrew.Hive.cs`, Garrett 2026-10-31): an omni crystal the Honeyguide
collects opens hive mode and lays a flat hex comb of real, super-shielded prisms `hiveOffset` (40 u) behind where it was
taken. Until now the crystal also burst into the shared husk spray. It now flies back and breaks into the comb.

This is a **per-vessel omni-crystal retirement** (`VesselImpactorDataContainerSO.OmniCrystalRetirement`), after the
Squirrel, Scarab, Dolphin, sphere-blast, Shrike and fauna-birth morphs. It shares everything but the target with them
(`CrystalMorphRunner`); the feel is the fleet's (`Resources/CrystalMorphConfig`).

## 1. The target: the comb's eight heart cells

The omni cage is 64 panels, eight octahedra's worth, and the Squirrel's panel census (`CrystalMorphMeshBuilder.TryBuild`
over `OctahedronTarget`s) lands every panel on one octahedron face, 1:1. A full comb is 37 cells (radius-3 hex). So the
crystal spreads onto the **eight cells nearest the comb's middle** (`HiveCombMorphGeometry.CentralCells`: the centre,
its six neighbours, one of the next ring), and the other cells bloom in around them as they always did.

Each cell is super-shielded, drawn as a stellated octahedron (`PrismStellatedOctahedronShield`). Its two tetrahedra
meet in an octahedron with vertices at the spike-tip cube's face centres, so each cell's landing octahedron is
`FromSemiAxes(prism pose at TargetScale, ShellCenterLocal, ShellSemiAxesLocal)`.

## 2. The hand-off

1. It draws the crystal's own renderers (`CrystalMorphRunner.Adopt`), so frame 0 is the crystal.
2. **Only the eight cells' photons wait** (`Prism.SetOwnerHidden`). The hive, its shields, the hull's debuff immunity
   and the raid run from the frame the comb is laid. **The vessel kit is unchanged.**
3. At the hand-off, while still hidden, the eight cells' own arrivals are settled (`CompleteGrowthImmediately`,
   stellation `Engage(instant)`), the Squirrel's rule, and then they are revealed.

**Not exact**, said plainly:

- The plates land on each cell's inner octahedron, and its eight spikes appear at the hand-off. The dissolve tail covers
  part of it; expect the spikes to read as a quick sprout.
- The comb is many domains and the morph converges on one colour pair (the hull's own domain cell, else the first). The
  other heart cells change colour at the hand-off.

## 3. How it runs

```
Honeyguide touches an omni crystal
  └─ VesselImpactor.ExecuteOmniCrystalImpact                   [every peer]
        ├─ OmniCrystalRetirement (runs FIRST): HoneyguideCrystalMorphByCrystalEffectSO
        │     └─ HoneyguideCrystalMorph.Begin: adopt the crystal · listen to HoneyguideCrew.CombLaid
        └─ VesselCrystalEffects: HoneyguideOmniCrystalEffect → HoneyguideCrew.OnOmniCrystal (pending)
  next hull step (the pilot's machine), once any pick of three is done:
        HoneyguideCrew.EnterHive → LayComb → CombLaid (isolated)
              └─ HoneyguideCrystalMorph.OnCombLaid: same vessel · the eight central cells within 60 u of the collect
                    census onto their octahedra · hold their photons · ONE stamp
  t = morphFraction · duration : the eight cells are settled and revealed
  t = duration                 : the crystal's body has dissolved off them; the morph is destroyed
```

**The morph never opens the hive.** It fades the crystal, quietly, when a hive is already open (a crystal taken in hive
mode opens none) or on a peer that is not the pilot's (the comb is laid on the pilot's machine). Otherwise a missing comb
fades it with a warning that names the sibling.

## 4. Networking

The comb is laid by the hull's own step, so the morph plays on the pilot's machine. On other peers the crystal fades.

## 5. Files

| file | role |
|---|---|
| `R_VesselActions/HoneyguideCrystalMorph.cs` | the comb target: listen, pick the heart cells, census, hold, settle, reveal |
| `…/Vessel Crystal Effects/HoneyguideCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/HoneyguideCrystalMorphByCrystal.asset` | the retirement, in `HoneyguideImpactorDataContainer` (`author_honeyguide_assets.py --check` holds it) |
| `Crew/HoneyguideCrew.Hive.cs` | `CombLaid` (isolated, raised at the end of `LayComb`) and `HiveOpen` |
| `Utility/HiveCombMorphGeometry.cs` | which cells, pure |
| `Tests/Editor/HoneyguideCrystalMorphGeometryTests.cs` | four gates, run headlessly by `Tools/Build/crystal_morph_harness/` |

## 6. Verification

**Not opened in Unity.** Proven offline: the headless morph harness (all suites) and `unity_refcompile` (player and
editor, 0 errors in the changed files). Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Trace with
**FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.
