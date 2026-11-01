# The Shrike's omni-crystal morph

**The crystal does not shatter. It folds into the nest flora it grows.**

The Shrike's pickup (`ShrikeCrystalEffect` → `ShrikeExecutor.CollectCrystal`, `SHRIKE.md` §3.5) grows a NEST FLORA
where the crystal was: a six-sided stem rising from the collect point and an octahedral bud on top that visibly grows
missiles (Garrett, 2026-10-15: "collecting a crystal should morph it into a nest flora"). Until now the crystal also
burst into the shared husk spray, so the flora appeared beside a shattering crystal. Now the cage rises and folds onto
the bud, and the flora takes over.

This is a per-vessel omni-crystal retirement (`VesselImpactorDataContainerSO.OmniCrystalRetirement`), on the shared
`CrystalMorphRunner` beside the Squirrel's, Scarab's, Dolphin's (#1680) and the sphere-blast hulls' (#1690). The feel is
the fleet's (`Resources/CrystalMorphConfig`).

## 1. The target

| part | shape | where (missileLength 4) | in the morph |
|---|---|---|---|
| bud | octahedron, radius 0.3 × missileLength = 1.2 u | 0.75 × missileLength = 3 u above the collect point | **the target**: every vertex lands on it |
| stem | six-sided frustum | from the collect point up to the bud | appears at the hand-off |
| missiles | zero size on a fresh flora | round the bud | nothing to land on yet |

The bud is convex and about the crystal's size, so the mapping is the Scarab's hull cast. The bud stands *beside* the
crystal, not around it: seen from the bud's centre the whole cage lies below, and the plain cast lands it on the
underside only. So every vertex keeps its direction from the crystal's own middle and is cast from the bud's centre
(`Utility/CrystalMorphRecast.cs`). That is built on the shipped builder without changing it: translate the hull onto the
crystal's middle, cast, translate the landed points back. It is the same mapping the Dolphin's PR adds as a builder
overload; once both land, `CrystalMorphRecast` can forward to it.

## 2. Seams

- **Photons only.** `ShrikeNestFlora.SetMorphStandIn` forces every part's renderer off and nothing else; the
  executor's missile count and growth run from the pickup frame as before. The flora is visual only anyway.
- **One target, one authority.** The morph does not grow the flora; it listens to
  `ShrikeExecutor.FloraGrownFromCrystal` (isolated, raised only by `CollectCrystal`, never by the Mass detach) and
  filters on the same hull's status and the collect position.
- **Colours** converge on the bud material's `_DarkColor`/`_BrightColor`, else the hull material's `_Color2`/`_Color1`
  (the flora wears the hull's domain material).

**Not exact:** the stem is not landed on, so it appears at the hand-off under the dissolving crystal. It is a thin
column under a bud the plates already cover; the dissolve tail should carry it. If it pops, the follow-up is to land
part of the cage on the stem too (the stem is convex as well, so it can be a second hull).

## 3. Files

| file | role |
|---|---|
| `R_VesselActions/ShrikeCrystalMorph.cs` | the bud target |
| `…/Vessel Crystal Effects/ShrikeCrystalMorphByCrystalEffectSO.cs` + `ShrikeCrystalMorphByCrystal.asset` | the retirement; wired by `author_shrike_assets.py` |
| `Executors/ShrikeExecutor.cs` | `FloraGrownFromCrystal`; `AddFlora` returns the view |
| `Vessel/ShrikeNestFlora.cs` | `Bud`, `SetMorphStandIn`, `TryGetColours` |
| `Utility/CrystalMorphRecast.cs` | the cast around the crystal's middle |
| `Tests/Editor/ShrikeCrystalMorphGeometryTests.cs` | five gates, in `Tools/Build/crystal_morph_harness/` |

## 4. In-editor verification

1. Fly the Shrike through an omni crystal: **no husk spray**; the cage rises onto the bud; the flora appears and grows
   its missiles; ≈ 0.44 s. Set `duration` to 9 on `CrystalMorphConfig` to watch it slowly.
2. An elemental crystal still grows a flora with no morph (the retirement is omni only), and the shared spray as before.
3. Mass detach still leaves its flora on the nest with no morph.
4. Two clients (MPPM): the morph plays on both.
5. Trace: **FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.
