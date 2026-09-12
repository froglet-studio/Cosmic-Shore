# Prompt — replace Shift Sci-Fi UI and PrimitivePlus with first-party assets

> ## ✅ EXECUTED 12 Sep 2026 — do not run this again
>
> `Assets/Shift - Complete Sci-Fi UI/` and `Assets/PrimitivePlus/` are **deleted**, each in its own
> commit carrying a per-asset guid-ownership proof. Replacements: `Assets/_Graphics/UI/Frames/`
> (`Tools/Build/author_ui_frame_sprites.py`) and `Assets/_Models/Primitives/`
> (`Tools/Build/author_primitive_meshes.py`), both with `--check`; references re-pointed by
> `Tools/Build/repoint_vendored_ui_assets.py`. Outcome, measurements and the Cone's before/after
> bounds/pivot/axis: [`THIRD_PARTY_REGISTER.md` §2.1](../THIRD_PARTY_REGISTER.md).
>
> **Two of the three things this prompt asked for were answered with a REMOVAL and evidence rather
> than a rebuild**, as §2.1 records: `PrimitivePlusMaterial` is inert at runtime (its only callers
> are the vendor's own editor inspector), and the Shift switch had no live instance to rebuild — the
> options panel expresses every on/off row as `GameSettingsPanelController.OnOffControl`, and the
> one `Switch.prefab` instance was inactive inside a prefab nothing references.
>
> ⚠️ **Not opened in the Unity editor** — no compile, no import, no play mode. The generators and the
> guid proofs answer offline; the rendering and the mesh import are unverified. The ranked in-editor
> test list is `THIRD_PARTY_REGISTER.md` §2.1.1. Kept for the record.

Paste everything below into a fresh session.

---

Two commercial Asset Store packs ship in the player with **no licence document anywhere in the tree**
and no recoverable purchase record. The studio's decision is to **replace both rather than chase
receipts**, because in both cases what is actually used is small and the packs' unused remainder is
shipping anyway.

Read `Docs/THIRD_PARTY_DECISIONS.md` §4 (row 6) first. Every number below is measured on
`claude/zealous-davinci-vjwev0` with the guid-ownership method CLAUDE.md mandates.

## What is actually live

### `Assets/Shift - Complete Sci-Fi UI/` — 584 KB, 15 files, **0 first-party code references**

| Asset | Referenced by |
|---|---|
| `Textures/Border/Cut/Cut Frame Big - 6px (200ppu).png` | `_Prefabs/UI Elements/ModalWindows.prefab`, `_Prefabs/UI Elements/Panels/OptionsMenuContent.prefab` |
| `Textures/Border/Cut/Cut Frame Filled Big (300ppu).png` | `_Scenes/Authentication.unity`, `_Scenes/Menu_Main.unity` |
| `Textures/Border/Cut/Cut Frame Filled Big (200ppu).png` | `_Scenes/Menu_Main.unity` |
| `Prefabs/Switch/Switch.prefab` | `_Prefabs/UI Elements/ModalWindows.prefab` |

Plus `Resources/Shift UI Manager.asset`, which **ships whether or not anything references it** (Unity
packs every `Resources/` folder whole). No first-party `.cs` file contains `Michsky` or
`SwitchManager`. The pack is **pure art plus one prefab**.

### `Assets/PrimitivePlus/` — 1008 KB, 53 files, **0 first-party code references**

| Asset | Referenced by |
|---|---|
| `Scripts/PrimitivePlusMaterial.cs` (36 lines) | `_Prefabs/FloraAndFauna/oldWallFlora.prefab`, `_Prefabs/Projectile/AOEConicExplosion.prefab`, `_Prefabs/Projectile/AOEConicSkyBurst.prefab` |
| `Resources/Meshes/Sphere.asset` | 7 projectile prefabs |
| `Resources/Meshes/Cube.asset` | 5 FX prefabs |
| `Resources/Meshes/Cone.asset` | `AOEConicExplosion.prefab`, `AOEConicSkyBurst.prefab`, `_Graphics/Materials/Graphs/LaserGraph.shadergraph` |
| `Resources/Meshes/CylinderTube.asset` | `oldWallFlora.prefab` |

**4 meshes of 47 are used, and all 47 ship** — `PrimitivePlus/Resources/` is packed whole. The one
live script is `[ExecuteInEditMode]` + `[RequireComponent(typeof(MeshRenderer))]` and does nothing
but cache a renderer and manage a material instance.

## The two hazards

**1. `Cone.asset` is gameplay-critical.** It is the mesh on `AOEConicExplosion.prefab` (the Dolphin's
crystal blast) and `AOEConicSkyBurst.prefab` (the Sparrow's skyburst) — two of the most-read visuals
in the game, both of which the arcade modes The Bends, Rampage, Dog Fight, Salvo and Breakwater are
cut against. A mesh whose orientation, pivot or unit scale differs from the original changes the blast
**visual** without changing the blast **volume**, which is worse than an obvious break: the trigger
`BoxCollider`, the Burst sweep job and `ExplosionImpactor.SweptCylinder.Contains` all keep their own
numbers (`Docs/SPATIAL_INDEX.md`, and CLAUDE.md's four-places-must-move-together rule for the Scarab
plate). **Measure the replacement mesh's bounds, pivot and axis against the original and state both.**

**2. `Switch.prefab` cannot be edited in place.** Two of the four `m_Script` guids it carries are
owned by no `.meta` under `Assets/` (`fe87c0e1cc204ed48ad3b37840f39efc`,
`4e29b1a8efbd4b44bb3f3716e73f07ff`). They are almost certainly package scripts — most likely
TextMeshPro — but that is **unverifiable in this clone**, because `Library/PackageCache/` is not
committed (the same limitation §3.2 of the register records). Rebuild the toggle from the project's
own UI primitives rather than reusing that prefab's internals.

## What to build

### Shift → first-party frames + a first-party toggle

* **The three border textures** are 9-sliced sci-fi frames. Re-author them first-party at the same
  pixel dimensions and — critically — the **same 9-slice border and PPU**, because a 9-slice border
  is a constraint on the smallest rect the sprite can be drawn into, and Menu_Main's canvas
  `referencePixelsPerUnit` is **240**, not 100 (`Docs/HomeHub/ARCHITECTURE.md` §4.1.8 records the
  `pixelsPerUnitMultiplier = 240/100` correction this project already had to make once).
  `Tools/Build/author_toy_card_sprites.py` and `Tools/Build/author_topbar_glow.py` are the house
  precedent for generating a UI sprite from a script with a `--check`.
* **The switch** is a settings toggle in `ModalWindows.prefab`. Rebuild it on `UnityEngine.UI.Toggle`
  or the project's own controls and wire it through `GameSettingsPanelController`'s existing
  `OnOffControl` pattern — the panel already models an ON/OFF row as two buttons with underlines, so
  the honest cheapest answer may be to convert that one row to the pattern the rest of the panel
  already uses, and delete the switch entirely.

### PrimitivePlus → four baked meshes + one first-party component

* **Bake the four meshes first-party.** `Sphere` and `Cube` have Unity built-in equivalents but are
  referenced here as shared **mesh assets**, so a built-in is not a drop-in — generate replacements
  (the project already generates meshes: `OctahedronMeshGenerator`,
  `StellatedOctahedronMeshGenerator`, `PrismMesh`, `CellMiniatureBuilder`). Put them somewhere that
  is **not** a `Resources/` folder unless something loads them by name — nothing does; every
  reference above is a direct guid reference.
* **Rewrite `PrimitivePlusMaterial`** as a first-party component of the same behaviour, and re-point
  the three prefabs' `m_Script` guids. Or determine it does nothing the prefabs need and remove the
  component — check `oldWallFlora`, `AOEConicExplosion` and `AOEConicSkyBurst` in play before
  deciding, and say which you did.
* **Deleting `PrimitivePlus/Resources/` is most of the win**: 47 meshes and a material that ship for
  four that are used.

## Constraints

* **Delete nothing until the replacement is referenced and proved.** Each pack's removal is its own
  commit with a guid-count proof that zero references remain.
* **Re-point references; do not squat on the vendor's guids.** Keeping the old guid on new content
  leaves the asset path saying `Shift - Complete Sci-Fi UI/…`, so the next audit re-reports it and a
  re-import restores vendor art over ours.
* **Nothing here changes gameplay.** If a blast reads differently after the mesh swap, that is a
  defect, not a style change.
* **Do not touch `MIgration_Prefabs (DELETE LATER)`** beyond noting what it references.

## Definition of done

1. Zero references to `Assets/Shift - Complete Sci-Fi UI/` and `Assets/PrimitivePlus/` from any scene,
   prefab, SO, material or shadergraph — proved by guid count per asset, not by `grep -rl`.
2. Both folders removed, in **two separate commits**, each carrying its proof.
3. A measured before/after of `Cone.asset`'s bounds, pivot and axis versus its replacement.
4. `Docs/THIRD_PARTY_REGISTER.md` §2 and `Docs/THIRD_PARTY_DECISIONS.md` §3 updated.
5. Verified in the editor (`/verify-unity`): Menu_Main, Authentication, the options panel, the modal
   windows, the Dolphin blast and the Sparrow skyburst — or stated plainly that it was not.
