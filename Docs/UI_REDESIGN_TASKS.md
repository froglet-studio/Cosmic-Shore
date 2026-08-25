# Cosmic Shore — UI Redesign Task Tracker

**Branch:** `bleeding-edge` · **Companion docs:** `Docs/UI_ARCHITECTURE_AUDIT.md`, `Docs/STYLE_FOUNDATION.md`, `Docs/GAMECANVAS.md`, `Docs/PALETTE.md`

Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table — run the skill.

**Status legend:** `TODO` · `IN PROGRESS` · `BLOCKED` · `NEEDS DESIGN` · `DONE`

---

## Status

| ID | Task | Status | Depends on | Branch | PR | Completed |
|---|---|---|---|---|---|---|
| T1 | Safe area component | IN PROGRESS | — | `claude/safe-area-fitter-component-wrmdva` | #777 | |
| T2 | Finish canvas resolution migration | TODO | — | | | |
| T3 | Unify GameCanvas fork | TODO | T2 | | | |
| T4 | UIThemeSO + literal inventory | TODO | — | | | |
| T5 | Download & install TMP fonts | TODO | — | | | |
| T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |

**Critical path:** T2 → T3 is the long pole. T1, T4, T5 are independent and can run in parallel. T6 needs T5's font assets to exist.

---

## T1 — Safe area component

**Spec:** Style Foundation §9 · **Audit ref:** §1.3

Acceptance criteria:
- [~] `Assets/_Scripts/UI/SafeAreaFitter.cs` exists and compiles
- [x] Reads `Screen.safeArea`, drives `anchorMin`/`anchorMax`
- [x] Recalculates on resolution and orientation change
- [x] Caches last-applied rect — no per-frame work when unchanged
- [x] Full-screen safeArea (desktop) is a no-op
- [x] `androidRenderOutsideSafeArea` confirmed still enabled
- [x] Not yet applied to any shipping prefab
- [x] Test scene demonstrates the two-layer contract

**Deliverables:**
- `Assets/_Scripts/UI/SafeAreaFitter.cs` — reads `Screen.safeArea`, drives the RectTransform's
  `anchorMin`/`anchorMax`. Change-gated on the safe-area `Rect` + `Screen.width`/`height`/
  `orientation`, so the steady-state per-frame cost is one `safeArea` read and a `Rect`/int compare;
  resizes are also caught event-style via `OnRectTransformDimensionsChange`. Exposes two pure
  statics (`IsFullScreenSafeArea`, `ComputeAnchors`) so the math is testable without a device.
- `Assets/_Scripts/Tests/Editor/SafeAreaFitterTests.cs` — 7 tests over those statics: real
  iPhone-class landscape (`132,63,2172,1062` @ 2436×1125) and portrait safe areas, sub-pixel
  rounding, out-of-range clamping, degenerate mid-rotation screen.
- `Assets/_Scenes/Game_TestDesign/SafeAreaFitterTestScene.unity` — two full-stretch sibling layers
  under one Canvas: `Background Art (bleeds under notch)` (magenta, no fitter) and
  `Safe Area Content` (translucent, carries the fitter, four corner markers). Not in Build Settings.
- `Assets/_Scenes/Game_TestDesign/SafeAreaTestReadout.cs` — scene-local IMGUI readout of live
  safe area / resolution / orientation / applied anchors, drawn in the full screen rect.
- `Docs/UNITY_VERIFICATION_CHECKLIST.md` — 🔴 entry with the in-editor steps.

**Findings:**
- `Screen.safeArea` appeared **zero times** in the codebase before this; there was no prior pattern
  to match. The nearest siblings solve the horizontal half of the same problem —
  `AdaptiveCanvasScaler` (aspect matching + optional ultrawide HUD safe zone) and
  `WidescreenLayoutAdapter` (pillarboxing). This component writes anchors, so it composes under
  either without contention.
- `androidRenderOutsideSafeArea: 1` at `ProjectSettings/ProjectSettings.asset:74`, already enabled
  before this branch and untouched by it (branch diff over `ProjectSettings/` is empty).
- The spec source this task cites — `Docs/STYLE_FOUNDATION.md` §9 — **does not exist in the repo**,
  nor does `Docs/UI_ARCHITECTURE_AUDIT.md` §1.3. The implementation was written against this
  tracker's acceptance criteria alone. Anything §9 says beyond them has not been checked.
- `com.unity.device-simulator.devices` is in the manifest, so the Device Simulator is the in-editor
  way to exercise this — it is what overrides `Screen.safeArea` in the editor.
- Verified out of editor: all three new sources compile under Roslyn against a faithful
  `UnityEngine` stub, and the shipped NUnit suite was **executed** — 7/7 pass. The hand-authored
  scene YAML parses with zero dangling local `fileID` references, and its three UGUI script GUIDs
  are the ones `Menu_Main.unity` already uses.

**Deviations from spec:**
- **Only anchors are written.** Authored `offsetMin`/`offsetMax` are deliberately left alone, where
  most reference implementations zero them. A full-stretch zero-offset rect is unaffected; a rect
  authored with padding keeps that padding relative to the safe rect.
- **The no-op is reversible.** Beyond "full-screen safeArea does nothing", the component captures
  the anchors the rect was authored with and restores them if a device that *had* insets returns to
  a full-screen safe area (rotating a cutout off-axis). Without this the rect would keep a stale
  inset. On desktop nothing is ever written, so the criterion holds literally.
- **Additions not in the criteria:** the edit-mode suite; the scene-local `SafeAreaTestReadout`
  diagnostic; and one `CSDebug.LogWarning` at enable when the rect is point-anchored on both axes
  (driving its anchors would slide it, not resize it).
- **"Compiles" is `[~]`, not `[x]`.** No Unity compile has happened — this branch was authored in a
  remote session with no editor and no `unity` CLI binary.

## T2 — Finish canvas resolution migration

**Spec:** Style Foundation §5 · **Audit ref:** §1.3

Acceptance criteria:
- [ ] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
- [ ] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
- [ ] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated
- [ ] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
- [ ] `AdaptiveCanvasScaler` on every scene canvas that lacked it
- [ ] Static `matchWidthOrHeight` overrides removed from those scenes
- [ ] Android max aspect raised 2.1 → 2.4
- [ ] No remaining reference resolution outside 1920×1080 project-wide
- [ ] Project builds; no canvas visibly regressed in a smoke pass

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T3 — Unify GameCanvas fork

**Spec:** `Docs/GAMECANVAS.md` · **Audit ref:** §5.1

Acceptance criteria:
- [ ] Prefab Kit Validate run, output recorded
- [ ] Prefab Kit Consolidate run
- [ ] Identical overrides (~1,734) pushed into the prefab
- [ ] **One scene only** re-placed, diff reported, explicit go-ahead received before the rest
- [ ] Override count per canvas instance below 25 in each migrated scene
- [ ] `statsToTrack` preserved per mode (the one real per-mode value)
- [ ] Joust toast feed rect normalised from ~(-1416, -463)
- [ ] 8 cross-asset dangling refs into the CORE prefab resolved
- [ ] Dangling `CountdownDisplay` ref into never-instantiated `MiniGameHUD.prefab` resolved
- [ ] All six modes launch and reach the Ready gate
- [ ] End-game scoreboard renders in each mode

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T4 — UIThemeSO + literal inventory

**Spec:** Style Foundation §10 · **Audit ref:** §1.4, §5.4

Acceptance criteria:
- [ ] `UIThemeSO` authored to §10 **verbatim** — 25 fields, no additions
- [ ] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
- [ ] **No team colour fields** — they stay in `SO_ColorSet`
- [ ] Live asset created and referenced
- [ ] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
- [ ] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
- [ ] No call sites changed yet

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T5 — Download & install TMP fonts

**Spec:** Style Foundation §4

Acceptance criteria:
- [ ] Chakra Petch (400/500/600/700) TTFs in `Assets/_Graphics/Fonts/ChakraPetch/`
- [ ] Space Grotesk (300/400/500/600) in `Assets/_Graphics/Fonts/SpaceGrotesk/`
- [ ] JetBrains Mono (400/500/700) in `Assets/_Graphics/Fonts/JetBrainsMono/`
- [ ] **Not** placed in `Assets/Unity Assests/TextMesh Pro/`
- [ ] `OFL.txt` shipped per family; credits attribution added
- [ ] TMP font assets generated: SDFAA, sampling 90, padding 9, atlas 1024²
- [ ] Charset: ASCII + Latin-1 Supplement + `× · — – ‑ … ← → ↑ ↓ ✕ + −`
- [ ] Multi Atlas Textures on; dynamic overflow fallback set
- [ ] Fallback chain: Space Grotesk → Chakra Petch → Liberation Sans
- [ ] TMP Settings default font asset = Space Grotesk 400
- [ ] Base material presets only — no outline, glow, or bevel
- [ ] Type-scale test scene screenshot captured at 1920×1080
- [ ] Tabular figure check: `0123456789` over `1111111111` columns align

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T6 — TMP Style Sheet + Aldrich audit

**Spec:** Style Foundation §4

Acceptance criteria:
- [ ] TMP Style Sheet with all 10 named styles from §4
- [ ] Each style carries family, weight, size @1920, tracking
- [ ] Aldrich migration cost report: reassignable vs per-component
- [ ] Liberation Sans leak list produced (~174 refs)
- [ ] Migration **not** executed — estimate only

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## Design feedback queue

Anything found during implementation that needs a design decision. The implementer **adds** entries here and does not resolve them or edit `STYLE_FOUNDATION.md` directly.

| # | Raised by | Task | Question | Status |
|---|---|---|---|---|
| 1 | T1 impl | T1 | Should the fitter conform BOTH axes always, or expose a per-axis / per-edge opt-out? A HUD often wants the notch inset but not the bottom gesture-bar inset. Currently both axes, always. | OPEN |
| 2 | T1 impl | T1 | Which layer of `GameCanvas.prefab` becomes the constrained content layer, and does background art move to a sibling above it? The two-layer contract needs a home in the prefab before T1 can be applied — likely settled inside T3. | OPEN |

---

## Style Foundation version log

| Version | Date | Change | Driven by |
|---|---|---|---|
| 0.1 | — | Initial token system, team-colour contract, type scale | Design |

---

## Deferred / out of scope

Decisions taken during the redesign to explicitly not do something. Recorded so they don't get silently relitigated.

| Item | Decision | Rationale |
|---|---|---|
| UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
| Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
| Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
