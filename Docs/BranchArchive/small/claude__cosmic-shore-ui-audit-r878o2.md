# Branch archive: `claude/cosmic-shore-ui-audit-r878o2`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-22 by Claude
- **Unmerged commits:** 1
- **Forked from:** `12838aeb8` (2026-08-22, Merge remote-tracking branch 'origin/claude/pensive-hopper-35d6h4' into bleedi)
- **Tip:** `2345e03ff`
- **Files touched (1):**
  - `Docs/UI_Audit.md`

### `2345e03ff` — docs(ui): add UI architecture audit for the HUD/menu redesign

_Claude, 2026-08-22 14:27:23 +0000_

```text
A designer-facing, self-contained audit of every player-facing UI surface:
tech foundation (uGUI/TMP only, canvas + scaler inventory, the unfinished
800x450 -> 1920x1080 migration, no safe-area handling, fonts/colors/sprites),
the full app-shell screen and modal inventory with a navigation map, the
per-mode in-game HUD inventory (Joust, HexRace, Crystal Capture, Freestyle,
party context, vessel HUDs, screen-space vs world-space), loading/disconnect/
pause state coverage, the constraints-and-debt register (GameCanvas fork +
override drift, duplicated toast/pause/profile paths, logic-in-UI, SOAP
rewiring tax, dead UI), and a screenshot checklist covering every distinct
state. Uncertain behaviors are flagged rather than guessed.
```

```text
 Docs/UI_Audit.md | 1139 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 1139 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 1145 lines)</summary>

```diff
diff --git a/Docs/UI_Audit.md b/Docs/UI_Audit.md
new file mode 100644
index 000000000..545b5375d
--- /dev/null
+++ b/Docs/UI_Audit.md
@@ -0,0 +1,1139 @@
+# Cosmic Shore — UI Architecture Audit
+
+**Date:** 2026-08-22 · **Branch:** `claude/cosmic-shore-ui-audit-r878o2` · **Scope:** every player-facing UI surface in the project — app shell and in-game HUDs.
+
+**Who this is for:** a designer preparing a complete UI/HUD redesign who **cannot see the codebase**. Everything is described in plain language first, with file paths attached so engineers can find the owner of any element. This is an **audit only** — no redesign proposals are made.
+
+**How the audit was produced:** by reading C# source, the serialized YAML of scenes and prefabs (Unity scenes and prefabs are text files), and the project's own engineering docs (`Docs/GAMECANVAS.md`, `Docs/PartySystem/UI.md`, `Docs/MENU_PROGRESSION_AND_IAP.md`, `CLAUDE.md`). **Nothing was observed running in the Unity editor.** Where a behavior could not be confirmed from code — for example, which of two overlapping modals a button actually opens — it is flagged as ⚠ **UNVERIFIED** rather than guessed. A consolidated list of uncertainties appears at the end of each major section.
+
+**Reading conventions:**
+- Paths are relative to the repo root (e.g. `Assets/_Scripts/UI/ScreenSwitcher.cs`).
+- "Domain" = team. The three playable teams are **Jade, Ruby, Gold**; **Blue** is the "no team / neutral" sentinel and is never a playable side.
+- "SOAP" = the project's ScriptableObject-based event/variable system (Obvious.Soap). UI elements frequently subscribe to SOAP events wired in the inspector — relevant because rebuilding a prefab means rewiring those references (see §5).
+- "SO" = ScriptableObject, a Unity data asset.
+
+---
+
+## Table of contents
+
+1. [Tech foundation](#1-tech-foundation)
+2. [App-shell screen inventory](#2-app-shell-screen-inventory)
+3. [In-game HUD inventory — per game mode](#3-in-game-hud-inventory--per-game-mode)
+4. [State and edge cases](#4-state-and-edge-cases)
+5. [Constraints and technical debt](#5-constraints-and-technical-debt)
+6. [Screenshot checklist](#6-screenshot-checklist)
+
+---
+
+# 1. Tech foundation
+
+## 1.1 Which UI systems are in use
+
+**The entire runtime UI is Unity uGUI** — `Canvas` + `RectTransform` + `Image`/`Button` + **TextMeshPro** for all text. There is **no UI Toolkit anywhere in the project**: zero `.uxml` files, zero `.uss` files, zero runtime or editor scripts using `UnityEngine.UIElements`. (Even the project's ~26 custom editor windows are old-style IMGUI.) This is unusually uniform — a redesign does not need to plan around a mixed UI stack.
+
+- **TextMeshPro is the universal text solution.** 118 scripts reference TMPro; zero runtime scripts use the legacy `UnityEngine.UI.Text`. The only legacy Text component in any asset is in an internal tool scene (`Assets/_Scenes/Tools/PhotoBooth.unity`), which does not ship.
+- **IMGUI (`OnGUI`)** appears in exactly three runtime scripts, all developer diagnostic overlays that players never see: `EcosystemPerfProbe`, `BenchmarkHUDOverlay`, `AOEBenchmarkOverlay`.
+
+## 1.2 Canvas structure
+
+There are **22 first-party Canvas components** across all scenes and prefabs (48 counting third-party demo content that doesn't ship). The important structural facts:
+
+### One canvas per context, not many stacked canvases
+
+- **`Menu_Main` (the entire main menu) is ONE canvas**, a GameObject named `UI_Refactored` — Screen Space Overlay, sort order 0. Every menu screen, modal, toast container, and the freestyle "Game UI" HUD area are children of this single canvas. There is no per-screen canvas splitting.
+- **Game scenes contain no scene-authored canvas.** Every gameplay scene gets its UI from an instance of one of two shared prefabs: `Assets/_Prefabs/CORE/GameCanvas.prefab` or `Assets/_Prefabs/GameCanvas-HexRace.prefab` (Screen Space Overlay, sort order 1). These two prefabs are forked copies of each other — a central piece of technical debt covered in §5.1.
+- **Each vessel prefab carries its own overlay canvas** (`ShipHUDContainer`, sort order 0) holding that vessel's HUD. At runtime the HUD's children are **reparented out of the vessel prefab and into the game canvas** (§3 and §5.7) — the vessel canvas is effectively a delivery container.
+
+### Canvas inventory table
+
+| Canvas | Where | Render mode | Sort order | Scaler | Reference resolution | Match |
+|---|---|---|---|---|---|---|
+| `UI_Refactored` (whole main menu) | `Assets/_Scenes/Menu_Main.unity` | Overlay | 0 | Scale w/ Screen Size | **1920×1080** (ref PPU 240) | 1.0 (height) |
+| `Canvas - Splash Screen` | `Assets/_Scenes/Bootstrap.unity` | Overlay | 10 (→ 32767 at runtime) | Scale w/ Screen Size | 1920×1080 | 0.5 |
+| `Canvas` (auth scene) | `Assets/_Scenes/Authentication.unity` | Overlay | 0 | Scale w/ Screen Size | 1920×1080 | 0.5 |
+| `GameCanvas` (shared in-game UI) | `Assets/_Prefabs/CORE/GameCanvas.prefab` | Overlay | 1 | Scale w/ Screen Size | **800×450 in the prefab asset** — overridden to **1920×1080 / PPU 240** in every scene instance | 1.0 in asset, **0 (width)** in scene overrides |
+| `GameCanvas-HexRace` (fork) | `Assets/_Prefabs/GameCanvas-HexRace.prefab` | Overlay | 1 | same as above | same as above | same |
+| `ShipHUDContainer` | each vessel prefab under `Assets/_Prefabs/Spacevessels/` (Manta, Dolphin, Rhino, Scarab, Serpent, Sparrow, Squirrel) + `Assets/_Prefabs/UI Elements/In Game/VesselHUDContainer.prefab` | Overlay | 0 | Scale w/ Screen Size | 1920×1080 | 1.0 |
+| `HUDContainer` | `Assets/_Prefabs/CORE/HUDContainer.prefab` | Overlay | 0 | **no CanvasScaler at all** | — | — |
+| `FTUE_Canvas` (tutorial, dormant) | `Assets/_Graphics/FTUE_Canvas.prefab` | Overlay | 1 | Scale w/ Screen Size | 1920×1080 | 1.0 |
+| `Duel Cell Stats Canvas` | `Assets/_Prefabs/UI Elements/Panels/Duel Cell Stats Panel/Duel Cell Stats Canvas.prefab` | Overlay | 10 | Scale w/ Screen Size | 1920×1080 | 1.0 |
+| `Loadout Container` | `Assets/_Prefabs/UI Elements/Loadout Container.prefab` | Overlay | 0 | **Constant Pixel Size** | 800×600 | — |
+| 3× ShapeSign (Star/Heart/Lightning) | `Assets/_Prefabs/UI Elements/Panels/*ShapeSign.prefab` | **World Space** | 0 | Constant Pixel Size | 800×600 | — |
+| `SplashScreen.unity` canvas | `Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity` | Overlay | 0 | Scale w/ Screen Size | **800×450** | 0.5 |
+
+### Runtime-created canvases (not in any prefab or scene)
+
+Three canvases are built entirely in code and exist only at runtime:
+
+| Canvas | Sort order | Built by | Purpose |
+|---|---|---|---|
+| `[SceneTransition_Overlay]` | **32767** (max) | `Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs` | Fallback black fade overlay (in practice the Bootstrap splash canvas is *adopted* instead and bumped to 32767 — see §4.1) |
+| Environment load veil | **30000** | `Assets/_Scripts/Controller/Environment/Spawning/EnvironmentLoadVeil.cs` | "GROWING \<WORLD\>…" hold screen while heavy cell environments build |
+| Privacy consent overlay | **32766** | `Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs` | First-run age gate + analytics consent |
+
+So the effective sort-order stack, top to bottom: scene-transition fade (32767) → privacy overlay (32766) → environment veil (30000) → Duel stats / splash (10) → game canvas / FTUE (1) → menu, auth, vessel HUD (0).
+
+### World-space UI
+
+Only the three `*ShapeSign.prefab` canvases are world-space, and **no scene or asset references them** — they appear to be orphans from the retired shape-drawing feature (⚠ UNVERIFIED — a name-based runtime load would not show in a reference search, but none was found). **No world-space canvas is used for live gameplay UI**: there are no floating nameplates, no world-space damage numbers, no 3D menu panels. Anything "in the world" that reads as UI (toy switch rings, the Dolphin's Echo Sight halo) is game geometry/shader work, not canvas UI (see §3.7).
+
+## 1.3 Scaling strategy, target resolutions, and aspect handling
+
+### The migration in progress: 800×450 → 1920×1080
+
+The project is **mid-way through a canvas-resolution migration** from a mobile-era 800×450 / 100-PPU baseline to a PC-ready 1920×1080 / 240-PPU baseline. A purpose-built editor tool (`Assets/_Scripts/Editor/CanvasUpgrader/` — `CanvasUpgraderWindow`, `CanvasUpgradeProcessor`) multiplies every canvas-space value by 2.4, optionally attaches the `AdaptiveCanvasScaler`, re-anchors center-anchored elements, and tracks upgraded prefabs in `ProjectSettings/CanvasUpgraderUpgradedPrefabs.txt` to prevent a double-pass (which would compound to ×5.76).
+
+**The migration is unfinished.** Evidence:
+
+- `GameCanvas.prefab` and `GameCanvas-HexRace.prefab` **assets are still authored at 800×450 / PPU 100**; only their scene instances carry the 1920×1080 / PPU 240 overrides. Opening the prefab in isolation shows a different layout than any scene.
+- `Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity` is still 800×450.
+- `Loadout Container.prefab` and the three ShapeSign prefabs are still Constant Pixel Size at 800×600.
+- Reference resolutions across the project currently span **800×450, 800×600, and 1920×1080**; reference PPU spans **100 and 240**.
+
+### Aspect-ratio handling
+
+Two adapters exist; coverage is partial:
+
+1. **`AdaptiveCanvasScaler`** (`Assets/_Scripts/UI/AdaptiveCanvasScaler.cs`) — drives `CanvasScaler.matchWidthOrHeight` from the live aspect ratio: match-height (1.0) at 16:9 and wider, blending to match-width (0.0) as the screen narrows below 16:9 (blend range 0.15). It is attached in only **5 of ~20 scenes**: `Menu_Main`, `MinigameHexRace`, `MinigameJoust_Gameplay`, `Maelstrom`, `MinigameCrystalCaptureMultiplayer_Gameplay`. Every other game scene is pinned at a static match-width override. The component has an optional `safeZone` field that pins a child rect to a centered maximum-aspect region on ultrawide — **it is unassigned in every instance found**, so the ultrawide containment feature is effectively off.
+2. **`WidescreenLayoutAdapter`** (`Assets/_Scripts/UI/WidescreenLayoutAdapter.cs`) — would pillarbox a full-screen rect to a max aspect (default 2.17 ≈ 19.5:9). **Its GUID appears in zero scenes and zero prefabs — the component is written but attached to nothing.**
+
+### ⚠ Safe area / notch handling: NONE
+
+This is one of the most important findings for a redesign:
+
+- **`Screen.safeArea` appears zero times in the entire codebase.** There is no safe-area component, first-party or third-party.
+- **`AspectRatioFitter` appears in zero scenes and zero prefabs.**
+- The Android player setting `androidRenderOutsideSafeArea` is **enabled**, meaning the game explicitly draws under camera cutouts and gesture bars.
+
+**Consequence:** on a notched phone in landscape, any HUD content anchored to the left/right screen edges sits under the notch and the gesture pill, and nothing compensates. A redesign that repositions HUD elements toward screen edges will need to introduce safe-area handling from scratch.
+
+### Target platforms, resolution, and orientation (from `ProjectSettings/ProjectSettings.asset`)
+
+| Setting | Value | Notes |
+|---|---|---|
+| Orientation | **Landscape only** (auto-rotate between landscape L/R; portrait disabled) | Mobile |
+| Android max aspect | **2.1 (~18.9:9)** | Below modern 20:9 / 21:9 phones — devices wider than this letterbox or crop per OEM behavior |
+| Android min SDK | 28 (Android 9) | |
+| iOS target | 15.0, Universal (iPhone + iPad) | Bundle id is still the legacy `com.FrogletGames.Tail-Glider` |
+| Desktop default window | **1024×768 (4:3)**, not resizable, borderless fullscreen default | The 4:3 default matches no canvas reference resolution — likely stale rather than intentional |
+| Color space | Linear | Matters for authoring UI colors (see `Docs/PALETTE.md`) |
+| Target frame rate | 60 (from `BootstrapConfigSO`), VSync 0 | |
+| Build profiles | Only one exists: `CS Linux build profile.asset` | Android/iOS/Windows configured via ProjectSettings directly |
+
+The in-game Settings modal additionally exposes a resolution dropdown (built from `Screen.resolutions` with "Native" default), display-mode dropdown, frame cap, VSync, and a **60–90 FOV slider** on desktop (§2.10).
+
+### How the menu lays out across aspect ratios
+
+`ScreenSwitcher` (`Assets/_Scripts/UI/ScreenSwitcher.cs`, 1,047 lines) arranges the menu screens as a **horizontal filmstrip**: on `Start()` each screen panel is anchored to the left edge, stretched vertically, sized to exactly one viewport width (read live from the canvas rect, so it tracks the actual aspect), and offset by its index. Navigation slides the whole strip with a hand-rolled smoothstep coroutine (not DOTween). ⚠ The layout runs once at `Start()` — no re-layout hook on resolution change was found, so a mid-session desktop window resize would leave the strip sized to the old viewport (UNVERIFIED at runtime, but no code path was found).
+
+The menu root also carries a `PhoneFlipDetector` and per-screen `FlipUI` components responding to device flip.
+
+## 1.4 Frameworks, tweening, and theming
+
+### Animation: three coexisting mechanisms
+
+| Mechanism | Where it's used |
+|---|---|
+| **DOTween** (Demigiant; the only tween library installed) | Toasts, card entrance animations, score punch/roll animations, quest track choreography, hangar grid cards, all vessel HUD views, elemental petal bars, the in-game HUD show/hide fades, countdown timer, dialogue UI, end-game sequencer. ~30 files total. |
+| **Unity Animator state machines** | All modals (`ModalWindowManager` crossfades `"Window In"`/`"Window Out"` states), the Home screen panel, pause menu panel, settings modal, profile modal, the `SceneTransitionModal` sliding-door wipe. |
+| **Hand-rolled coroutine lerps** | `ScreenSwitcher` screen slide, `SceneTransitionManager` fade, `EnvironmentLoadVeil` fades, `ConnectingPanelController` dots. |
+
+No LeanTween/PrimeTween/iTween; no Timeline used for UI.
+
+### Theming: what is centralized and what is not
+
```

</details>
