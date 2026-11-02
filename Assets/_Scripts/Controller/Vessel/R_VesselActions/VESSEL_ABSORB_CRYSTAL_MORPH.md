# The hull-absorb omni-crystal morph (Brittle Star, Sparrow, Stoat, Orca, Wrasse)

**The crystal does not shatter. The vessel takes it in.** A design placeholder: chosen by a Claude session on
2026-11-02 so that every hull with an omni ability stops spraying the husk. Garrett redirects any hull when he tests it.

These five hulls answer an omni crystal with something that is not in the world: there is nothing for the crystal to
become, so the morph has it fold into the vessel itself.

| hull | its omni ability (unchanged) | why there is no world target |
|---|---|---|
| Brittle Star | a spare ring charge (`BrittleStarExecutor.CollectCrystal`) | shown on the HUD only |
| Sparrow | its ward (`SparrowVesselWardByCrystalEffect`) | a buff on the hull |
| Stoat | a charge toward its next Fulmen bolt (`FulmenByCrystalEffect`, Yash 2026-11-02) | the bolt strikes at the cell centre only every few crystals; the crystal is the charge, so it goes into the hull |
| Orca | its magazine (`OrcaOmniCrystalEffect`) | rounds in the gun, not in the world |
| Wrasse | board motes (`KiteOmniCrystalEffect`) | the crew's board, not the world |

This is a **per-vessel omni-crystal retirement** (`VesselImpactorDataContainerSO.OmniCrystalRetirement`); the other
hulls land on what their ability spawns (Squirrel, Scarab, Dolphin, sphere blast, Shrike, fauna birth, Honeyguide). It
shares everything with them (`CrystalMorphRunner`); the feel is the fleet's (`Resources/CrystalMorphConfig`).

## 1. The target: a shell inside the hull

`CrystalHullFusion.FindHull` finds the hull the elemental hull fusion uses (a skin, or a static body and its parts).
The shell is the ellipsoid inscribed in the hull's bounds in the hull's own frame, pulled in to 80%
(`VesselAbsorbMorphGeometry.UnitSphereToShell`), so the plates land just inside the skin and dissolve there. The cage
keeps each vertex's direction from the crystal's middle (`CrystalMorphRecast`), so it wraps the whole hull instead of
piling onto the side that faces the crystal (`VesselAbsorbCrystalMorphGeometryTests`: 8 of 8 octants, the plain cast at
most 4).

The bounds are read from the mesh's bounds, which Unity keeps on an unreadable import too, so no Read/Write is needed.

## 2. How it runs

The target is known at the pickup, so the morph stamps at once: no waiting, no sibling event, no grace. It rides the
hull's pose every frame, holds nothing (the vessel is drawn throughout), and keeps the crystal's own colours, so it
sinks in as itself. The hull's own omni effect runs after it, unchanged. It plays on every peer (each peer has the hull).

## 3. Replacing it for one hull

When a hull gets a target of its own (a ward shell for the Sparrow, a round for the Orca), give it its own retirement
asset the way the others did. `author_brittlestar_assets.py` and `author_stoat_assets.py` write this one into their
containers; the Sparrow, Orca and Wrasse containers name it directly.

## 4. Files

| file | role |
|---|---|
| `R_VesselActions/VesselAbsorbCrystalMorph.cs` | find the hull, build the shell, cast, stamp, follow |
| `…/Vessel Crystal Effects/VesselAbsorbCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/VesselAbsorbCrystalMorphByCrystal.asset` | the retirement, in the five containers |
| `Utility/VesselAbsorbMorphGeometry.cs` | the shell, pure |
| `Tests/Editor/VesselAbsorbCrystalMorphGeometryTests.cs` | five gates, run headlessly by `Tools/Build/crystal_morph_harness/` |

## 5. Verification

**Not opened in Unity.** Proven offline: the headless morph harness (all suites) and `unity_refcompile` (player and
editor, 0 errors in the changed files). Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
