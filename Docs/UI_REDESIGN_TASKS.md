# Cosmic Shore — UI Redesign Task Tracker

**Branch:** `bleeding-edge` · **Companion docs:** `Docs/UI_ARCHITECTURE_AUDIT.md`, `Docs/STYLE_FOUNDATION.md`, `Docs/GAMECANVAS.md`, `Docs/PALETTE.md`

Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table — run the skill.

**Status legend:** `TODO` · `IN PROGRESS` · `BLOCKED` · `NEEDS DESIGN` · `DONE`

---

## Status

| ID | Task | Status | Depends on | Branch | PR | Completed |
|---|---|---|---|---|---|---|
| T1 | Safe area component | TODO | — | | | |
| T2 | Finish canvas resolution migration | IN PROGRESS | — | `claude/canvas-resolution-ppu-migration-azv16k` | #781 | |
| T2.6 | Nested UI fragment migration | TODO | T2 | | | |
| T3 | Unify GameCanvas fork | TODO | T2 | | | |
| T4 | UIThemeSO + literal inventory | TODO | — | | | |
| T5 | Download & install TMP fonts | TODO | — | | | |
| T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |

**Critical path:** T2 → T3 is the long pole. T1, T4, T5 are independent and can run in parallel. T6 needs T5's font assets to exist.

---

## T1 — Safe area component

**Spec:** Style Foundation §9 · **Audit ref:** §1.3

Acceptance criteria:
- [ ] `Assets/_Scripts/UI/SafeAreaFitter.cs` exists and compiles
- [ ] Reads `Screen.safeArea`, drives `anchorMin`/`anchorMax`
- [ ] Recalculates on resolution and orientation change
- [ ] Caches last-applied rect — no per-frame work when unchanged
- [ ] Full-screen safeArea (desktop) is a no-op
- [ ] `androidRenderOutsideSafeArea` confirmed still enabled
- [ ] Not yet applied to any shipping prefab
- [ ] Test scene demonstrates the two-layer contract

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T2 — Finish canvas resolution migration

**Spec:** Style Foundation §5 · **Audit ref:** §1.3

Acceptance criteria:
- [x] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
- [x] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
- [x] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated — **not a migration target**, see Deviations
- [x] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
- [x] `AdaptiveCanvasScaler` on every scene canvas that lacked it
- [x] Static `matchWidthOrHeight` overrides removed from those scenes
- [ ] No remaining reference resolution outside 1920×1080 project-wide — 9 remain, see Findings
- [~] Project builds; no canvas visibly regressed in a smoke pass — editor-only, human to confirm

**Deliverables:**
- `_Prefabs/CORE/GameCanvas.prefab` — refRes 800×450 → 1920×1080, refPPU 100 → 240, 289 canvas-space values ×2.4
- `_Prefabs/GameCanvas-HexRace.prefab` — same, 465 values ×2.4
- `_Scenes/Singleplayer Scenes/SplashScreen.unity` — same, 1 `sizeDelta` ×2.4
- `AdaptiveCanvasScaler` added to both GameCanvas prefabs and to the `Authentication`, `Bootstrap`
  and `SplashScreen` scene canvases
- Per-instance `AdaptiveCanvasScaler` `m_AddedComponents` override removed from CrystalCapture,
  Joust, Maelstrom and HexRace (it now comes from the prefab; keeping both would duplicate the component)
- Static `m_MatchWidthOrHeight: 0` override removed from all 12 scenes that pinned it
- All 36 now-no-op `m_ReferenceResolution.x/.y` + `m_ReferencePixelsPerUnit` overrides removed from
  those 12 scenes — no CanvasScaler override of any kind now survives on any GameCanvas instance

**Findings:**
- Audit §1.3 already records that both prefab **assets** sat at 800×450 / PPU 100 while only their
  scene instances carried the 1920/240 overrides. What it does not record is the **consequence**: the
  11 HexRace-fork scenes plus Maelstrom were running 800-space prefab children under a 1920 scaler,
  so every child not covered by a scene override was rendering **2.4× too small**. That is what
  migrating the prefab assets fixes; it was not a cosmetic tidy-up of an inert asset.
- Migration correctness was checked against those pre-existing scene overrides as an oracle: the
  migrated prefab values match the scenes' independently-authored ×2.4 values on **199 of 231**
  comparable properties. The 32 differences are all elements those scenes deliberately repositioned
  (they carry anchor changes too). Scene-side override values were left untouched — re-scaling them
  is exactly the ×5.76 compound the ledger exists to prevent.
- **PPU 240 is not a project-wide invariant, and normalising it would break every 9-slice.** 240 is a
  consequence of the ×2.4 path: refPPU compensates for the canvas scale factor dropping 2.4× when
  refRes rises. A canvas authored natively at 1920×1080 never had that scale change, so PPU 100 is
  correct for it. `Authentication`, `Bootstrap`, `FTUE_Canvas`, `VesselHUDContainer`,
  `Duel Cell Stats Canvas` and all 7 vessel `ShipHUDContainer` canvases are 1920×1080 at PPU 100 and
  were deliberately left alone.
- Nine CanvasScaler reference resolutions remain outside 1920×1080: 4 third-party
  (`NiceVibrations` 1080×1920, 3 × `QuickScenePro` 800×600), 4 first-party at Unity's default
  800×600 in **ConstantPixelSize** mode where the field is inert (`Loadout Container`,
  `StarShapeSign`, `HeartShapeSign`, `LightningShapeSign`), and `_Scenes/Tools/PhotoBooth.unity`
  (800×600, ScaleWithScreenSize, tool scene). None is an 800×450-authored canvas. Audit §1.3 already
  describes the project as spanning 800×450, 800×600 and 1920×1080, and treats the 800×600 group as a
  distinct Constant-Pixel-Size item — so "no reference resolution outside 1920×1080 project-wide" is
  broader than the migration this task defines, and cannot be satisfied by it.
- `TextMeshProUGUI.m_fontSizeBase` tracks `m_fontSize` only while auto-sizing is **off**. Established
  from the scenes the upgrader had already run on (auto-size-off rows carry ×2.4 on both keys;
  auto-size-on rows carry it on `m_fontSize` alone) and replicated. Scaling `m_fontSizeBase`
  unconditionally would corrupt every auto-sizing label.
- **Fork census re-measured for T3 scoping. Audit figures of "6 fork scenes" and "~1,734 identical
  overrides" (22 Aug) are SUPERSEDED.** Measured against the working tree:

  | | audit (22 Aug) | measured |
  |---|---|---|
  | `GameCanvas-HexRace.prefab` scenes | 6 | **11** |
  | `CORE/GameCanvas.prefab` scenes | "the remaining" | **10** (21 total) |
  | identical overrides across the fork set | ~1,734 | **1,719** byte-identical / **1,735** same-key |
  | overrides per fork-scene canvas instance | ~1,770 | **1,748–1,760** (1,752–1,764 before this branch) |

  Fork scenes (11): AstroLeague, Bends, CrystalCapture, DogFight, HexRace, Joust, NucleusRush,
  Rampage, Ribcage, ScarabScramble, WildlifeLiberation.
  CORE scenes (10): 2v2 CoOp, Maelstrom, DuelForCell, MultiplayerFreestyle, WildlifeBlitz CoOp,
  BenchmarkStressTest, CellularDuel, WildlifeBlitz, Recording Studio, MattsRecording Studio.
  The five the audit misses are `GameModes` 39–43 (Ribcage, WildlifeLiberation, DogFight, Bends,
  ScarabScramble) — the five newest modes, all added after the audit's survey. The figure is **stale
  rather than wrong**.
- **Widening the set from 6 to 11 costs T3 nothing.** The identical-override core is *the same number*
  measured across the audit's 6 and across all 11 (1,719 byte-identical / 1,735 same-key, both sets).
  Only **16** (target, propertyPath) pairs genuinely differ in value across the whole fork set. So the
  consolidation payload T3 has to push into the prefab does not grow with the extra 5 scenes — only
  the re-placement work does. The audit's ~1,734 sits between the two measures and is consistent with
  a same-key count taken before this branch removed 4 scaler overrides per scene.
- **The newer fork scenes were duplicated, not authored fresh — this is why the override baggage is
  identical.** 9 of the 11 fork scenes carry the *same scene-local `PrefabInstance` anchor fileID*
  (`2113049457`): AstroLeague, Bends, CrystalCapture, DogFight, NucleusRush, Rampage, Ribcage,
  ScarabScramble, WildlifeLiberation. Only HexRace (`330573866`) and Joust (`377908207`) differ. A
  scene-local fileID is minted per scene, so an identical anchor across 9 scenes is a scene-copy
  signature: each new mode was cloned from an existing fork scene and inherited its full override set.
  (Creation dates from `git log --diff-filter=A` are **not** usable as corroboration here — the clone
  is shallow, so those dates are when each file entered the clone, not when it was authored.)
  **Not resolved here — T2.5.**
- **T3 precondition, now satisfied:** both forks sit in the same coordinate space, so consolidation no
  longer has to reconcile a resolution delta mid-merge. Migrating them in parallel rather than
  unifying first was deliberate scoping, not a deviation — unifying the fork is T3's job.
- Override pressure is unchanged for T3: the gameplay scenes still carry ~1,828 prefab-instance
  modifications each (audit §3 quotes ~1,770; T3 target: under 25).
- `AdaptiveCanvasScaler.safeZone` is unassigned in **all 6** instances project-wide — the 5 added by
  this branch plus Menu_Main's — every one `{fileID: 0}`. Audit §1.3's reading is confirmed: ultrawide
  HUD containment is off everywhere. Raised as design queue #3 rather than guessed at, since assigning
  it changes HUD framing on every ultrawide display.
- `WidescreenLayoutAdapter`'s guid appears in **0** scenes and **0** prefabs. Audit §1.3 confirmed.
- **Desktop player settings — the audit is NOT stale, it is accurate.** `defaultScreenWidth: 1024`,
  `defaultScreenHeight: 768`, `resizableWindow: 0`, `fullscreenMode: 1` (`FullScreenWindow`, i.e.
  borderless), `defaultIsNativeResolution: 1`, `allowFullscreenSwitch: 1`. So the shipping default is
  borderless-fullscreen at native resolution and the 1024×768 pair only applies to a windowed launch.
- **`ScreenSwitcher` has no re-layout path on resolution change** (report only, not fixed here).
  `LayoutScreensToViewport()` sizes every screen panel to `Screen.width` and positions it at
  `i * viewportWidth`; it has exactly **one** caller — `Start()`. There is no
  `OnRectTransformDimensionsChange`, and `Update()` handles only the freestyle/modal input gates. A
  resolution change therefore leaves the filmstrip sized *and* offset to the old viewport, so panels
  are the wrong width and navigation lands off-centre. `resizableWindow: 0` currently masks this — but
  `allowFullscreenSwitch: 1` means an alt-enter fullscreen toggle already reaches it, and it becomes
  a live desktop bug the moment the window is made resizable.
- Audit §1.3's `AdaptiveCanvasScaler` coverage list ("5 of ~20 scenes": Menu_Main, HexRace, Joust,
  Maelstrom, CrystalCapture) is confirmed exactly. Four of those five carried it as a per-instance
  override rather than on the prefab, which is why the count read as scene-level coverage.

**Deviations from spec:**
- **`Loadout Container.prefab` was not migrated.** Its CanvasScaler is `ConstantPixelSize` at Unity's
  default 800×600 — not an 800×450-authored canvas — so `CanvasUpgradeProcessor.Scan` skips it twice
  over (wrong scale mode, wrong reference resolution). In ConstantPixelSize the scale factor is pinned
  at 1, so a ×2.4 pass would land as a literal 2.4× on-screen size increase. **The audit itself records
  this** — §1.2's canvas table lists the prefab as Constant Pixel Size / 800×600, and §1.3 lists it
  alongside the three ShapeSign prefabs as "still Constant Pixel Size at 800×600", a separate item from
  the 800×450 migration. The criterion contradicts its own source and needs amending rather than ticking.
- **`AdaptiveCanvasScaler` was placed on the two GameCanvas prefabs rather than per scene.** The
  gameplay scenes own no canvas, so the prefab is the only single-source-of-truth placement; this
  covers all 21 scenes at once and matches `Docs/GAMECANVAS.md`. Four scenes that already carried it
  as an instance override had that override removed to avoid duplicating the component.
- **`_Scenes/Tools/PhotoBooth.unity` was deliberately skipped.** Its canvas is ScaleWithScreenSize at
  800×600 (4:3); `AdaptiveCanvasScaler`'s 16:9 `referenceAspect` blend would be wrong against a 4:3
  reference. Tool scene, no shipping impact.
- **The 36 no-op CanvasScaler overrides were removed although the task did not ask for it.** A no-op
  override still beats the prefab, so those 12 scenes would have silently ignored any future re-tune —
  the mechanism that produced the 1,719 identical overrides T3 now has to unwind.
- Migration was performed as validated YAML surgery, not by running the editor tool: no Unity editor
  is available in this environment. Document counts, anchor counts and dangling-reference sets are
  unchanged on all 18 files, and the authored `AdaptiveCanvasScaler` keys were checked field-for-field
  against `AdaptiveCanvasScaler.cs`. Import verification remains a human step.
- No ledger entry was added. `CanvasUpgraderUpgradedPrefabs.txt` guards canvas-**less** fragments only;
  these three assets are self-guarding via their own `referenceResolution`, which now reads 1920×1080
  and makes the upgrader's `Scan` mark them already-upgraded.

---

## T2.6 — Nested UI fragment migration

**Spec:** Style Foundation §5 · **Audit ref:** §1.3 · **Raised by:** T2

The seven canvas-less fragments nested inside the GameCanvas prefabs are still authored in 800-space.
T2 scaled their instance **roots** (what the upgrader does), so they now sit at the right position and
frame size with 800-space interiors. In the 11 HexRace-fork scenes this is invisible — those scenes
override the descendants — but in the 9 non-overriding GameCanvas scenes the interiors read small.
They are shared with `MiniGameHUD.prefab` / `VesselHUD.prefab`, so this is its own pass, not a batch.

Acceptance criteria:
- [ ] `Pip.prefab` — **cut, do not migrate.** Raised by no gameplay code in any audited mode. Not
      scaled into 1920-space until a cut decision exists. Design feedback queue entry raised and OPEN
- [ ] `ThumbPerimeter.prefab` — **cut, do not migrate.** Belongs to the thumb cursors, which are
      self-disabled in code under a "TEMP for SUSPEND" comment. Same gate; queue entry raised and OPEN
- [ ] `GameOverPanel` — **BLOCKED, migrate neither.** Two prefabs exist
      (`_Prefabs/UI Elements/Panels/GameOverPanel.prefab`, `_Prefabs/R_GameOverPanel.prefab`) and
      which is live was never traced. Resolve first, or both get maintained forever
- [ ] `CountdownTimer.prefab` migrated ×2.4 and logged in `CanvasUpgraderUpgradedPrefabs.txt`
- [ ] `SceneTransitionModal.prefab` migrated ×2.4 and logged
- [ ] `R_Pause_Menu_Panel.prefab` migrated ×2.4 and logged
- [ ] `PauseMenu.pauseMenuPanel` verified to be a **GameObject** reference, not a CanvasGroup,
      **before and after** the migration — a mistyped serialized reference on this prefab took the
      Windows IL2CPP build down twice (see the doc comment on `PauseMenu.ResolvedPanel`)
- [ ] `NotificationUI.prefab` — **not migrated here; folded into T3**, which already has to normalise
      its rect (Joust's has drifted to ~(-1416, -463), likely off-screen). Interior migration and
      position fix land in one pass
- [ ] `MiniGameHUD.prefab` — **delete, do not migrate.** Never instantiated; reachable only through a
      dangling override. T3's existing criteria cover resolving it
- [ ] No fragment scaled twice — ledger checked before each pass

**Deliverables:**
**Findings:**
- `R_GameOverPanel.prefab` (guid `aa18ad2b4731c37449403e155640cf0a`) is referenced by no prefab or
  scene in `Assets/`; `GameOverPanel.prefab` (guid `494deef066b46a24a9b5226c4203833c`) is referenced by
  both GameCanvas prefabs. Evidence toward which is live, **not** a resolution — recorded for whoever
  traces it.
- `MiniGameHUD.prefab` (guid `491eb8350c5c6ab45a8b291192f9891a`) is referenced only by the two
  GameCanvas prefabs, consistent with the dangling-override reachability T3 records.
- `PauseMenu.pauseMenuPanel` is currently declared `[SerializeField] GameObject pauseMenuPanel`, and
  `PauseMenu.cs` carries the type-check and the account of the two IL2CPP build failures.

**Deviations from spec:**

---

## T3 — Unify GameCanvas fork

**Spec:** `Docs/GAMECANVAS.md` · **Audit ref:** §5.1

Acceptance criteria:
- [ ] Prefab Kit Validate run, output recorded
- [ ] Prefab Kit Consolidate run
- [ ] Identical overrides (**1,719** byte-identical / 1,735 same-key across all **11** fork scenes; audit's "~1,734 across 6" superseded — see T2 Findings) pushed into the prefab
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
| 1 | T2 | T2.6 | Cut `Pip.prefab`? It is raised by no gameplay code in any audited mode. Held out of the 1920-space migration until this is decided. | OPEN |
| 2 | T2 | T2.6 | Cut `ThumbPerimeter.prefab`? It belongs to the thumb cursors, which are self-disabled in code under a "TEMP for SUSPEND" comment. Held out of the 1920-space migration until this is decided. | OPEN |
| 3 | T2 | T2 | Should `AdaptiveCanvasScaler.safeZone` be assigned on the two GameCanvas prefabs? It is unassigned in every instance project-wide (audit §1.3), so ultrawide HUD containment is off. Assigning it pins HUD content to a centered 16:9 region on 21:9/32:9 — a framing decision, not an implementation one. | OPEN |

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
| Android `maxAspectRatio` 2.1 → 2.4 | Dropped from T2; change reverted | Mobile is deferred, desktop is the platform. The raise was applied and then reverted so the PR carries no unowned platform-settings change. One line in `ProjectSettings.asset` to restore when mobile resumes; at 2.1, 20:9 and 21:9 phones letterbox or crop per OEM |
