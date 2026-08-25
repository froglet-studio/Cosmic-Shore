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
| T7 | Component sprite kit | TODO | — | | | |

**Critical path:** T2 → T3 is the long pole. T1, T4, T5, T7 are independent and can run in parallel. T6 needs T5's font assets to exist.

---

## T1 — Safe area component

**Spec:** Style Foundation §8 · **Audit ref:** §1.3

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
- `Assets/_Scripts/Tests/Editor/SafeAreaFitterTests.cs` — 8 tests over those statics: real
  iPhone-class landscape (`132,63,2172,1062` @ 2436×1125) and portrait safe areas, an Android
  single-edge cutout mirrored across landscape-left/landscape-right, sub-pixel rounding,
  out-of-range clamping, degenerate mid-rotation screen.
- `Assets/_Scenes/Game_TestDesign/SafeAreaFitterTestScene.unity` — two full-stretch sibling layers
  under one Canvas: `Background Art (bleeds under notch)` (magenta, no fitter) and
  `Safe Area Content` (translucent, carries the fitter, four corner markers, authored at §8's
  24 px edge inset via `sizeDelta -48,-48`). Not in Build Settings.
- `Assets/_Scenes/Game_TestDesign/SafeAreaTestReadout.cs` — scene-local IMGUI readout of live
  safe area / resolution / orientation / applied anchors, drawn in the full screen rect.
- `Docs/UNITY_VERIFICATION_CHECKLIST.md` — 🔴 entry with the in-editor steps, including the
  16:9 · 20:9 · 4:3 test aspects that v0.2 §9 called for — superseded, see the v0.3 note below.

**Findings:**
- `Screen.safeArea` appeared **zero times** in the codebase before this, as §1.3 records; there was
  no prior pattern to match. The nearest siblings solve the horizontal half of the same problem —
  `AdaptiveCanvasScaler` and `WidescreenLayoutAdapter`. This component writes anchors, so it
  composes under either without contention.
- `androidRenderOutsideSafeArea: 1` at `ProjectSettings/ProjectSettings.asset:74`, already enabled
  before this branch and untouched by it (branch diff over `ProjectSettings/` is empty).
- **The project is landscape-only** — `allowedAutorotateToPortrait: 0`, portrait-upside-down `0`,
  both landscape orientations `1`. So the rotation this component must survive is landscape-left ↔
  landscape-right, where the **resolution is identical** and only the safe-area rect moves. A change
  check keyed on width/height alone would sleep through the only rotation the game permits; this one
  compares the safe-area rect and `Screen.orientation` too. There is now a test for it.
- **Real iPhone landscape safe areas are symmetric** (the OS insets both ends, e.g. 132 px each side
  at 2436×1125), so they cannot demonstrate a cutout swapping sides — the mirror test needed an
  Android-style single-edge cutout. Caught by running the test, not by reading it.
- `com.unity.device-simulator.devices` is in the manifest, so the Device Simulator is the in-editor
  way to exercise this — it is what overrides `Screen.safeArea` in the editor.
- Verified out of editor: all three new sources compile under Roslyn against a faithful
  `UnityEngine` stub, and the shipped NUnit suite was **executed** — 8/8 pass. The hand-authored
  scene YAML parses with zero dangling local `fileID` references, and its three UGUI script GUIDs
  are the ones `Menu_Main.unity` already uses.

**Deviations from spec:**
- **§8's 24 px minimum edge inset is authored padding, not enforced by the component**, and the two
  §8 rules cannot both hold any other way: a fitter-enforced inset would break "full-screen safeArea
  is a no-op" on desktop, where §8 still wants content 24 px off the edge. So the component writes
  **anchors only** and leaves authored `offsetMin`/`offsetMax` alone (most reference implementations
  zero them), and the inset is authored on the content layer, where it composes with the fit and
  survives the no-op. Demonstrated in the test scene. Nothing enforces it on a future content layer
  — see queue #3.
- **The no-op is reversible.** Beyond "full-screen safeArea does nothing", the component captures the
  anchors the rect was authored with and restores them if a device that *had* insets returns to a
  full-screen safe area. Without this the rect would keep a stale inset. On desktop nothing is ever
  written, so the criterion holds literally.
- **Additions not in the criteria:** the edit-mode suite; the scene-local `SafeAreaTestReadout`
  diagnostic; and one `CSDebug.LogWarning` at enable when the rect is point-anchored on both axes
  (driving its anchors would slide it, not resize it).
- **"Compiles" is `[~]`, not `[x]`.** No Unity compile has happened — this branch was authored in a
  remote session with no editor and no `unity` CLI binary.
- v0.2 §9's other two rules were **out of T1's scope and untouched here**: Android max aspect
  2.1 → 2.4 is T2's criterion, and the 16:9 · 20:9 · 4:3 test aspects are an editor check, carried
  into the verification checklist. Both moved in v0.3 — see the note below.

> **Re-scoped against Style Foundation v0.3.** Safe area moved **§9 → §8** and shrank to a
> paragraph. Two rules this task's record cites no longer read the same: the test aspects are now
> **16:9 · 16:10 · 21:9** (v0.2 said 16:9 · 20:9 · 4:3), and **the Android max-aspect 2.1 → 2.4
> rule is gone from the spec entirely** — v0.3 §8 defers mobile and says `SafeAreaFitter` ships
> dormant, which is what this task built. The 24 px inset survives, and v0.3 now states it is a
> **floor, authored as padding** — i.e. it ratifies this task's deviation. Status and criteria are
> unchanged; re-check the verification-checklist aspect list before closing.

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

> **Re-scoped against Style Foundation v0.3.** §5 still exists but is now *Geometry — the corner
> sliver*; the 1920×1080 / PPU 240 reference this task migrates to is stated in the **document
> header**, not in §5. The **Android max aspect 2.1 → 2.4** criterion has no spec home in v0.3 —
> §8 defers mobile — so that criterion is currently unbacked and needs a design call before it is
> actioned. Status and criteria are unchanged.

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

**Spec:** Style Foundation §11 · **Audit ref:** §1.4, §5.4

Acceptance criteria:
- [ ] `UIThemeSO` authored to §11 **verbatim** — 25 fields, no additions
- [ ] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
- [ ] **No team colour fields** — they stay in `SO_ColorSet`
- [ ] Live asset created and referenced
- [ ] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
- [ ] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
- [ ] No call sites changed yet

> **Re-scoped against Style Foundation v0.3.** The field map moved **§10 → §11** (§10 is now the
> component library). The field list itself was rebuilt on the studio palette: the criterion's
> **"25 fields"** and every v0.1 hex it implied are superseded — v0.3 §11 lists ~15 rows keyed to
> the guide colours (`textLight E6E9FF`, `surfaceBlack 00010A`, `cta 99FF80`, …), and `chamfer*`
> is now `sliver*`. `danger FF4B3A` is still **proposed, not approved**. Author to v0.3 §11, not to
> the field count in the criteria. The no-team-colours rule is unchanged. Status and criteria are
> unchanged.

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

> **Re-scoped against Style Foundation v0.3.** §4 is still §4 but the family set changed
> fundamentally: v0.3 §0-C **cancels JetBrains Mono and Space Grotesk**, retains **Aldrich** for
> headings and body, and keeps **Chakra Petch SemiBold** for buttons only. Tabular figures are now
> bought with TMP `<mspace>` rather than by a mono family. Most of the criteria above
> therefore name assets that should no longer be installed, and the Space Grotesk fallback-chain
> and default-font criteria contradict v0.3. Re-derive the criteria from v0.3 §4 before starting.
> No italic face is needed — emphasis resolved to colour shift only (queue #6), so the four
> upright Chakra Petch weights stand. **T5 gains one output:** report the widest digit advance for
> **both Aldrich and Chakra Petch SemiBold**, since v0.3.1 scopes `<mspace>` per face rather than
> to Aldrich alone (queue #9). Status and criteria are left as written pending that pass.

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

> **Re-scoped against Style Foundation v0.3.** §4 is still §4, but v0.3 makes **Aldrich the
> retained brand font**, not a font to migrate off — which inverts this task's premise. The
> "Aldrich migration cost report" criterion is obsolete as written; what v0.3 needs instead is an
> `<mspace>` / `TabularText` plan for the numeric roles. The named styles are now **11 roles**
> (Display, H1–H3, Body, Body small, Button, Button small, Data large/Data/Data small), not 10.
> The Liberation Sans leak list still stands. Status and criteria are left as written pending a
> re-derive against v0.3 §4.

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T7 — Component sprite kit

**Spec:** Style Foundation §5 (geometry) · §10 (components)

Acceptance criteria:
- [ ] 9-slice sprites authored for: button (two sliver sizes + flipped), panel/popup (border and
      fill variants), card, hexagon tile, hexagon slider handle, currency pill, end-game banner +
      end caps
- [ ] Corner sliver is a **diagonal cut on two opposite corners, flippable** — not a rounded rect,
      not a single chamfer
- [ ] Border insets correct — slivers hold their shape at any width
- [ ] All sprites **white + alpha, no baked colour** — tinted from `UIThemeSO` at runtime
- [ ] Test scene shows each sprite at three widths
- [ ] **No shipping prefab touched**

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
| 3 | T1 impl | T1 | §8's 24 px minimum edge inset is currently authored padding on the content layer (nothing enforces it). Should it stay authored — which is what lets it survive the desktop no-op — or become a serialized floor on `SafeAreaFitter`? And is 24 px canvas units at the 1920×1080 reference, or device pixels? | OPEN |
| 4 | v0.3 intake | — | **`STYLE_FOUNDATION.md` §3 cross-references "§11.9" for the end-of-game victory banner, but §11 is the UIThemeSO field map and has no subsections — the banner is §10.9.** Broken internal citation in the spec, left unfixed here because the tracker may not edit the spec. Correct it to §10.9 in the next spec revision. | OPEN |
| 5 | v0.3 intake | T5/T6 | ~~**§4's type scale has no source art backing it.**~~ **RESOLVED** — the Typography page was supplied. §4's *Mobile @800* column is a faithful transcription (H1 24 / H2 20 / H3 16 / Body 16 / Button 16 / Button small 12 all match), as is the emphasis rule. Transcription of record in `Docs/StyleGuide/README.md`; the divergences it surfaced are queue #6–#9. | RESOLVED |
| 6 | Typography art | T5 | ~~Emphasis needs a Chakra Petch *Italic* face.~~ **RESOLVED — emphasis is colour shift only.** The italic clause is dropped from §4; no italic face is installed, so T5's four upright weights stand as written. | RESOLVED |
| 7 | Typography art | T5/T6 | ~~Display, Body small and the three Data roles are spec-authored, not on the source page.~~ **RESOLVED — kept, and marked as such.** §4's table now daggers those five rows with a footnote stating they carry no guide backing and are open to revision in a way the transcribed six are not. | RESOLVED |
| 8 | Typography art | — | ~~The button caps rule has a documented exception that v0.3 dropped.~~ **RESOLVED — caps is unconditional; the exception is retired with the Port screen** (already cut from the overhaul). §4 records the decision so it is not relitigated. | RESOLVED |
| 9 | Typography art | T5 | ~~A live countdown renders in the button face, not a Data role.~~ **RESOLVED — `<mspace>` generalised.** It now applies to any live-updating numeric in **any** face, not just the Aldrich Data roles. `X` is per-face, `TabularText` takes the face as a parameter, and T5 reports the digit advance for **both** Aldrich and Chakra Petch SemiBold. | RESOLVED |

---

## Style Foundation version log

| Version | Date | Change | Driven by |
|---|---|---|---|
| 0.1 | — | Initial token system, team-colour contract, type scale | Design |
| 0.2 | — | Rebuilt on the studio palette and typography | Design |
| 0.3 | 2026-08-25 | Team names resolved (Jade = Team 1 cyan, Ruby = Team 2 purple, Gold = Team 3 amber). PC type scale set. **Aldrich retained**, with TMP `<mspace>` for numerics — JetBrains Mono and Space Grotesk cancelled. Chamfer corrected to the **flippable corner sliver**. Component library §10 added from the source guide; UIThemeSO field map moved §10 → §11. | Design |
| 0.3.1 | 2026-08-25 | Typography source page received; §4's six transcribed rows confirmed against it. Queue #6–#9 resolved into the spec: emphasis **colour shift only**; Display / Body small / Data ×3 marked **spec-authored**; button caps **unconditional**; `<mspace>` **generalised to any live-updating numeric in any face**. Section numbering unchanged. | Design |

---

## Deferred / out of scope

Decisions taken during the redesign to explicitly not do something. Recorded so they don't get silently relitigated.

| Item | Decision | Rationale |
|---|---|---|
| UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
| Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
| Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
