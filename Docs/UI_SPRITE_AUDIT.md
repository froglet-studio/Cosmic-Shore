# UI Sprite & UI-Rendering Performance Audit

_Engine: Unity 6000.0.62f1 · URP · mobile-first (GDC demo target)_

This document captures the state of the UI sprite assets and the UI render
path, why they cost frame time, and the concrete plan to fix it. Tooling that
implements the asset-side fixes lives in
`Assets/_Scripts/Editor/UISpriteOptimizer.cs` (menu: **Tools > Cosmic Shore >
UI Sprites**).

---

## TL;DR

The UI is expensive for two compounding reasons:

1. **Sprite atlasing is completely off.** `EditorSettings.spritePackerMode = 0`
   (Disabled) and there are **zero** `.spriteatlas` assets. Every one of ~990
   UI sprites is a standalone texture, so UGUI cannot batch them — the in-game
   HUD fragments into 30+ draw calls that should be 1–2. This is a CPU cost that
   hits the mobile demo hardest.
2. **The HUD canvas mixes per-frame-dynamic content with static content**, so a
   score/timer/fuel update rebuilds the whole canvas every frame, and decorative
   graphics are needlessly flagged as raycast targets.

Fixing #1 (atlasing) is the single biggest win and is low risk (atlases are
additive — no sprite GUIDs change). #2 is per-frame CPU and needs small,
verified prefab edits.

---

## Inventory & measurements

| Metric | Value |
|---|---|
| PNGs under `_Graphics` | 1,162 (1,253 first-party total) |
| Sprite-type UI PNGs | ~992 |
| **SpriteAtlas assets** | **0** |
| **`m_SpritePackerMode`** | **0 (Disabled)** |
| Sprites with a legacy `spritePackingTag` | 0 |
| Sprites with **mipmaps ON** (wasteful for UI) | **91** |
| Sprites **≥1024 px** (oversized for UI) | **42** (incl. 3200 px, 3056 px) |
| `maxTextureSize` = 2048 on every sprite | 1,031 / 1,031 |
| Crunch compression off | 996 / 1,031 |
| Android / iPhone import overrides | 4 / 0 |
| Approx uncompressed RGBA footprint | ~367 MB |
| `_Graphics` on disk | 630 MB |
| UI sprites path-loaded (`Resources.Load`) | 4 (`Resources/ElementPetals` — intentional) |

### Per-vessel HUD sprite counts (disjoint sets → batch breaks on vessel switch)

| Vessel HUD | Images | Unique sprite GUIDs |
|---|---|---|
| Manta | — | 2 |
| Rhino | 10 | 8 |
| Squirrel | 1 | 15 |
| Dolphin | 5 | 19 |
| Serpent | 10 | 27 |
| **Sparrow** | **32** | **53** |

Sparrow and Squirrel are the GDC demo vessels, so their HUDs are the priority.

### Worst offenders (by FPS impact)

1. **`_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab`** — 32 Images, 33
   raycast targets, 53 unique sprite GUIDs → ~32+ draw calls + ~33 raycast walks
   per frame.
2. **`_Prefabs/UI Elements/In Game/VesselHUDContainer.prefab`** — single Canvas
   mixing dynamic (timer/score/fuel) and static vessel graphics → full-canvas
   rebuild on any update.
3. **`_Prefabs/UI Elements/In Game/EndGameStatsPanel.prefab`** — 16/16 graphics
   are raycast targets (all decorative).
4. **`Menu_Main.unity`** — 40 raycast-enabled Images, 8 CanvasGroups.

---

## Folder organization problems

- **`{LEGACY}/` is misleadingly named** — 91 of its sprites are still wired into
  live prefabs/scenes, so it cannot be bulk-deleted. **99** sprites across
  `{LEGACY}`, `{PLACEHOLDERS}`, `Hangar/Archive`, and
  `Design Assests/Menu_Main/R_Menu_Main/References` are **unreferenced** removal
  candidates.
- **`Design Assests/`** (typo) is the largest folder (344 files / 33 MB). Its
  `Menu_Main/R_Menu_Main/References/` subfolder holds 1600 px **design-comp
  mockups** (`PROFILE.png`, `ARK.png`, …) that are not runtime sprites.
- Misc churn: `nav line (1..4).png`, `PROFILE (1).png`, `icon_*-1.png`,
  `… - Copy.png`, `Hanger_New` (typo), `Old/` subfolders.
- **Build vs repo:** unreferenced sprites in normal folders do **not** ship in
  the player build (Unity only builds referenced assets + `Resources/` +
  `StreamingAssets/`). So the dead sprites bloat the **repo**, not the build.
  Cleaning them is repo hygiene, not an FPS or build-size fix.

No C# code references graphics folders by path string, so folder renames are
reference-safe (GUIDs are unaffected by moving files + their `.meta`).

---

## Plan (ordered by FPS leverage)

| # | Action | Leverage | Risk | How |
|---|---|---|---|---|
| 1 | Enable Sprite Atlas V2 + author per-screen atlases | **Highest** | Low | Tool cmd 1 |
| 2 | Split `VesselHUDContainer` into static + dynamic sub-canvases | Very high | Med | Manual (below) |
| 3 | Strip raycastTarget from decorative HUD/menu graphics | High | Low | Tool cmd 3 |
| 4 | Mipmaps off + right-size + crunch + ASTC mobile overrides | Medium | Low | Tool cmd 2 |
| 5 | Fuel-level sprite swaps → single filled `Image` | Medium | Med | Manual (below) |
| 6 | Folder cleanup (rename typo folders, evict mockups, drop dead sprites) | Repo hygiene | Low | Partly done |

### Proposed atlas groups (one atlas per screen so only the active screen is resident)

| Atlas | Source folders | Sprites |
|---|---|---|
| `UI_HUD` | HUD UI, Controls Panel, End Scene, ElementIcons, ElementShapes, Silhouettes | 249 |
| `UI_Menu` | Nav Bar, Buttons, Menu_Main | 138 |
| `UI_Arcade` | ARCADE, CardImages | 112 |
| `UI_Port` | Port | 104 |
| `UI_Hangar` | Hangar, VesselButtons | 93 |
| `UI_Profile` | Profile, Pilots | 59 |
| `UI_Misc` | Store, Settings | 24 |

Full-screen backgrounds (e.g. `Menu_Main/Background Basic.png`, scene-transition
doors) should be **excluded** from atlases — they only ever draw alone, so
atlasing them just wastes atlas pages. After running cmd 1, remove those few
large sprites from the atlas in the inspector if they got pulled in by folder.

---

## How to run the tooling (in the Unity Editor)

> These operations use the Unity asset pipeline, so they must run in-editor.
> All are reversible via git. Run on a clean working tree and review the diff.

1. **`Tools > Cosmic Shore > UI Sprites > 1. Configure Sprite Atlasing`**
   - Sets `spritePackerMode = SpriteAtlasV2` (project-wide).
   - Creates `Assets/_Graphics/_Atlases/UI_*.spriteatlasv2` for each group above,
     packed from whole folders, mipmaps off, no rotation, padding 4, ASTC on
     mobile. Idempotent — safe to re-run.
   - **Verify:** open Window > 2D > Sprite Atlas / the Frame Debugger and confirm
     the HUD and a menu screen now draw in 1–2 SetPass calls.

2. **`Tools > Cosmic Shore > UI Sprites > 2. Fix UI Sprite Import Settings`**
   - For every Sprite-type texture under `_Graphics` (excluding FX, App Icons,
     References, Video): mipmaps off, `alphaIsTransparency` on, not readable,
     crunch on, `maxTextureSize` capped to next-pow2 of source (never upscaled),
     and explicit ASTC_6x6 overrides for Android + iPhone.

3. **`Tools > Cosmic Shore > UI Sprites > 3. Disable Raycast On Selection`**
   - Select the vessel HUD variant prefabs (and `EndGameStatsPanel`, etc.) in the
     Project window, then run. Disables `raycastTarget` only on graphics that have
     no `Selectable`/`EventTrigger`/`IEventSystemHandler` and are not a
     `Selectable.targetGraphic`, so buttons keep working.
   - **Verify:** click every interactive control on the affected prefabs.

---

## Manual steps (need in-editor visual verification)

### #2 — Split `VesselHUDContainer` into static + dynamic canvases

The container's single Canvas dirties entirely whenever the timer/score/fuel
change. Split it:

1. Open `_Prefabs/UI Elements/In Game/VesselHUDContainer.prefab`.
2. Add a child `Canvas` (with `GraphicRaycaster`) named `DynamicHUD`; leave it as
   an overlay/screen-space child (a nested Canvas isolates its own rebuilds).
3. Move the **frequently-updating** elements onto it: `Time Container` /
   countdown timer, `PlayerScoreCard` score text, fuel/boost/energy bars,
   life-form counter.
4. Leave static decorative vessel graphics (chassis/wings/frame) on the outer
   Canvas. They now never trigger a rebuild from a score tick.
5. **Verify** in the Frame Debugger that updating the score no longer rebuilds
   the static canvas (`Canvas.BuildBatch` should only touch `DynamicHUD`).

### #5 — Fuel levels: 10 sprite swaps → one filled Image

`Design Assests/HUD UI/Fuel Levels/1..10.png` (609×66 each) are swapped to show
fuel level. Fill-based bars already exist in the codebase (`SparrowHUDView`,
`SquirrelVesselHUDView`, `ResourceDisplay` all use `Image.fillAmount`). Replace
the swap with a single `Image` (`Image.Type.Filled`, horizontal) driven by
`fillAmount = level / maxLevel`. This removes 9 textures and the per-change
texture state swap. Verify the bar reads identically across all 10 levels.

---

## Folder cleanup (#6) — status

- **Done on this branch:** renamed `Design Assests` → `Design Assets` and
  `Hangar/Hanger_New` → `Hangar/Hangar_New` (reference-safe; no code path refs).
- **Deferred (do deliberately, off-crunch):** the 99 unreferenced sprites in
  `{LEGACY}` / `{PLACEHOLDERS}` / `Hangar/Archive` / `…/References`. These are
  repo-only bloat (not in the build). Removing them is safe but irreversible and
  the automated reference scan can't see Addressables/editor-only refs, so do it
  with a human pass. The still-referenced 91 `{LEGACY}` sprites must stay until
  their prefabs are re-pointed.

> **Tradeoff:** folder renames create large (rename-only) git diffs and can cause
> merge conflicts for teammates with WIP in `Design Assests`. They are committed
> separately so they can be merged when convenient.

---

## Patterns to keep

- **One atlas per screen/context**, not one mega-atlas — keeps only the active
  screen's pages resident and lets each screen batch internally.
- **Never enable mipmaps on UI sprites.** New UI sprites should import with
  mipmaps off + ASTC on mobile (cmd 2 enforces this for existing ones; consider a
  folder-scoped `AssetPostprocessor` if drift becomes a problem).
- **Decorative graphics get `raycastTarget = false`.** Only interactive controls
  and intentional input blockers (modal backdrops) should be raycast targets.
- **Keep dynamic and static UI on separate (nested) canvases** so a per-frame
  value change doesn't rebuild static geometry.
