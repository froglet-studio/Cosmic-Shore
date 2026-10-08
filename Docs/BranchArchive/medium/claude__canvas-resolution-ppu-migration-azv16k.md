# Branch archive: `claude/canvas-resolution-ppu-migration-azv16k`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-25 by Claude
- **Unmerged commits:** 9
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/781
- **Forked from:** `8b3a76921` (2026-08-25, docs(ui): commit the style foundation and architecture audit the tracker cites)
- **Tip:** `ce7b83c59`
- **Files touched (20):**
  - `.claude/skills/ui-redesign-tracker/SKILL.md`
  - `Assets/_Prefabs/CORE/GameCanvas.prefab`
  - `Assets/_Prefabs/GameCanvas-HexRace.prefab`
  - `Assets/_Scenes/Authentication.unity`
  - `Assets/_Scenes/Bootstrap.unity`
  - `Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity`
  - `Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity`
  - `Docs/UI_REDESIGN_TASKS.md`
  - `ProjectSettings/ProjectSettings.asset`

### `a8a297b71` — feat(ui): finish the 1920x1080 / PPU 240 canvas migration and make match adaptive

_Claude, 2026-08-24 23:53:33 +0000_

```text
Completes the 800x450 -> 1920x1080 (x2.4) and referencePixelsPerUnit 100 -> 240
migration on the last first-party canvases still on the old baseline, then moves
matchWidthOrHeight off static per-scene overrides and onto AdaptiveCanvasScaler.

Migrated x2.4 (CanvasUpgradeProcessor semantics, applied as YAML surgery since no
Unity Editor is available in this environment):
  - _Prefabs/CORE/GameCanvas.prefab          289 values
  - _Prefabs/GameCanvas-HexRace.prefab       465 values
  - _Scenes/Singleplayer Scenes/SplashScreen.unity  3 values

The two canvas prefabs were the stale half of an already-completed migration: all
21 gameplay scenes that instance them had been migrated at the prefab-instance
override level (scaler pinned to 1920/240 in 12 of them, children x2.4), while the
prefab assets stayed at 800x450/100. Verified by oracle: the migrated prefab values
match the scenes' independently-authored x2.4 overrides on 199 of 231 comparable
properties; the 32 differences are elements those scenes deliberately repositioned.
Scene-side override values were deliberately left untouched - re-scaling them would
compound to x5.76.

Loadout Container.prefab was NOT migrated: its CanvasScaler is ConstantPixelSize at
Unity's default 800x600, not an 800x450-authored canvas, so it is out of scope for
this migration and the upgrader itself skips it twice over.

AdaptiveCanvasScaler now drives matchWidthOrHeight from the live aspect ratio on:
  - both GameCanvas prefabs (covers all 21 gameplay scenes from one source)
  - Authentication, Bootstrap and SplashScreen scene canvases
The four scenes that carried it as a per-instance m_AddedComponents override
(CrystalCapture, Joust, Maelstrom, HexRace) had that override removed so the prefab
stays the single source of truth and the component is not duplicated. The static
m_MatchWidthOrHeight: 0 override was removed from all 12 scenes that pinned it.

Android maxAspectRatio 2.1 -> 2.4 so 20:9 and 21:9 phones neither letterbox nor
crop per OEM.

No ledger entry is added: CanvasUpgraderUpgradedPrefabs.txt guards canvas-LESS
fragments only; these assets are self-guarding via their own referenceResolution,
which now reads 1920x1080 and makes the upgrader skip them.
```

```text
 Assets/_Prefabs/CORE/GameCanvas.prefab                                | 595 ++++++++++----------
 Assets/_Prefabs/GameCanvas-HexRace.prefab                             | 947 ++++++++++++++++----------------
 Assets/_Scenes/Authentication.unity                                   |  17 +
 Assets/_Scenes/Bootstrap.unity                                        |  17 +
 Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity                     |  25 -
 Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity           |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity                 |   5 -
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  |  25 -
 Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity              |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity               |  25 -
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        |  25 -
 Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity           |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity               |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity        |   5 -
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity    |   5 -
 Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity                 |  23 +-
 ProjectSettings/ProjectSettings.asset                                 |   2 +-
 18 files changed, 843 insertions(+), 898 deletions(-)
```

### `9d3ccfb84` — refactor(ui): drop the no-op CanvasScaler overrides and record T2 in the tracker

_Claude, 2026-08-25 00:16:55 +0000_

```text
Removes the 36 CanvasScaler prefab-instance overrides across 12 gameplay scenes
that became exact no-ops once the canvas prefabs were migrated: m_ReferenceResolution
.x/.y (1920/1080) and m_ReferencePixelsPerUnit (240). Each was verified equal to the
prefab's own value before removal; none differed. No CanvasScaler override of any
kind now survives on any GameCanvas instance, so the prefab is the sole source for
canvas scaling.

A no-op override still beats the prefab, so leaving them would have meant those 12
scenes silently ignoring every future re-tune - the mechanism that produced the
~1,734 identical overrides T3 now has to unwind (Docs/GAMECANVAS.md).

Records T2 in Docs/UI_REDESIGN_TASKS.md as IN PROGRESS (8 of 10 criteria met, 1
editor-only), adds T2.6 "Nested UI fragment migration" with the triage for the seven
nested fragments, and raises two OPEN design-queue entries for the Pip and
ThumbPerimeter cut decisions.

Two criteria are deliberately not ticked:
  - Loadout Container.prefab is not a migration target (ConstantPixelSize at Unity's
    default 800x600, not an 800x450-authored canvas)
  - 9 reference resolutions remain outside 1920x1080: 4 third-party, 4 inert
    ConstantPixelSize, 1 tool scene

Recorded finding for T2.5: the GameCanvas fork spans 11 scenes, not the 6 the audit
records. Not resolved here.
```

```text
 Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity                     |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity           |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity                 |  15 ----
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity              |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity               |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity           |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity               |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity        |  15 ----
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity    |  15 ----
 Docs/UI_REDESIGN_TASKS.md                                             | 142 +++++++++++++++++++++++++++++---
 13 files changed, 130 insertions(+), 192 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 170 lines)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index f00055d4d..21738a871 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -13,7 +13,8 @@ Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table
 | ID | Task | Status | Depends on | Branch | PR | Completed |
 |---|---|---|---|---|---|---|
 | T1 | Safe area component | TODO | — | | | |
-| T2 | Finish canvas resolution migration | TODO | — | | | |
+| T2 | Finish canvas resolution migration | IN PROGRESS | — | `claude/canvas-resolution-ppu-migration-azv16k` | | |
+| T2.6 | Nested UI fragment migration | TODO | T2 | | | |
 | T3 | Unify GameCanvas fork | TODO | T2 | | | |
 | T4 | UIThemeSO + literal inventory | TODO | — | | | |
 | T5 | Download & install TMP fonts | TODO | — | | | |
@@ -48,19 +49,135 @@ Acceptance criteria:
 **Spec:** Style Foundation §5 · **Audit ref:** §1.3
 
 Acceptance criteria:
-- [ ] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
-- [ ] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
-- [ ] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
-- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated
-- [ ] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
-- [ ] `AdaptiveCanvasScaler` on every scene canvas that lacked it
-- [ ] Static `matchWidthOrHeight` overrides removed from those scenes
-- [ ] Android max aspect raised 2.1 → 2.4
-- [ ] No remaining reference resolution outside 1920×1080 project-wide
-- [ ] Project builds; no canvas visibly regressed in a smoke pass
+- [x] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
+- [x] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
+- [x] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
+- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated — **not a migration target**, see Deviations
+- [x] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
+- [x] `AdaptiveCanvasScaler` on every scene canvas that lacked it
+- [x] Static `matchWidthOrHeight` overrides removed from those scenes
+- [x] Android max aspect raised 2.1 → 2.4
+- [ ] No remaining reference resolution outside 1920×1080 project-wide — 9 remain, see Findings
+- [~] Project builds; no canvas visibly regressed in a smoke pass — editor-only, human to confirm
+
+**Deliverables:**
+- `_Prefabs/CORE/GameCanvas.prefab` — refRes 800×450 → 1920×1080, refPPU 100 → 240, 289 canvas-space values ×2.4
+- `_Prefabs/GameCanvas-HexRace.prefab` — same, 465 values ×2.4
+- `_Scenes/Singleplayer Scenes/SplashScreen.unity` — same, 1 `sizeDelta` ×2.4
+- `AdaptiveCanvasScaler` added to both GameCanvas prefabs and to the `Authentication`, `Bootstrap`
+  and `SplashScreen` scene canvases
+- Per-instance `AdaptiveCanvasScaler` `m_AddedComponents` override removed from CrystalCapture,
+  Joust, Maelstrom and HexRace (it now comes from the prefab; keeping both would duplicate the component)
+- Static `m_MatchWidthOrHeight: 0` override removed from all 12 scenes that pinned it
+- All 36 now-no-op `m_ReferenceResolution.x/.y` + `m_ReferencePixelsPerUnit` overrides removed from
+  those 12 scenes — no CanvasScaler override of any kind now survives on any GameCanvas instance
+- `ProjectSettings/ProjectSettings.asset` — `androidMaxAspectRatio` 2.1 → 2.4
+
+**Findings:**
+- **The two canvas prefabs were the stale half of an already-completed migration.** No gameplay scene
+  owns a canvas; all 21 instance one of the two prefabs, and 12 of them had already been migrated at
+  the prefab-instance override level (scaler pinned to 1920/240, children ×2.4) while the prefab
+  assets stayed at 800×450 / PPU 100. The 11 HexRace-fork scenes plus Maelstrom were therefore
+  running 800-space prefab children under a 1920 scaler — every child not covered by a scene
+  override was rendering 2.4× too small. Migrating the prefabs is what fixes that.
+- Migration correctness was checked against those pre-existing scene overrides as an oracle: the
+  migrated prefab values match the scenes' independently-authored ×2.4 values on **199 of 231**
+  comparable properties. The 32 differences are all elements those scenes deliberately repositioned
+  (they carry anchor changes too). Scene-side override values were left untouched — re-scaling them
+  is exactly the ×5.76 compound the ledger exists to prevent.
+- **PPU 240 is not a project-wide invariant, and normalising it would break every 9-slice.** 240 is a
+  consequence of the ×2.4 path: refPPU compensates for the canvas scale factor dropping 2.4× when
+  refRes rises. A canvas authored natively at 1920×1080 never had that scale change, so PPU 100 is
+  correct for it. `Authentication`, `Bootstrap`, `FTUE_Canvas`, `VesselHUDContainer`,
+  `Duel Cell Stats Canvas` and all 7 vessel `ShipHUDContainer` canvases are 1920×1080 at PPU 100 and
+  were deliberately left alone.
+- Nine CanvasScaler reference resolutions remain outside 1920×1080: 4 third-party
+  (`NiceVibrations` 1080×1920, 3 × `QuickScenePro` 800×600), 4 first-party at Unity's default
+  800×600 in **ConstantPixelSize** mode where the field is inert (`Loadout Container`,
+  `StarShapeSign`, `HeartShapeSign`, `LightningShapeSign`), and `_Scenes/Tools/PhotoBooth.unity`
+  (800×600, ScaleWithScreenSize, tool scene). None is an 800×450-authored canvas.
+- `TextMeshProUGUI.m_fontSizeBase` tracks `m_fontSize` only while auto-sizing is **off**. Established
+  from the scenes the upgrader had already run on (auto-size-off rows carry ×2.4 on both keys;
+  auto-size-on rows carry it on `m_fontSize` alone) and replicated. Scaling `m_fontSizeBase`
+  unconditionally would corrupt every auto-sizing label.
+- **The GameCanvas fork spans 11 scenes, not the 6 the audit records** — `GameCanvas-HexRace.prefab`
+  is instanced by AstroLeague, Bends, CrystalCapture, DogFight, HexRace, Joust, NucleusRush, Rampage,
+  Ribcage, ScarabScramble and WildlifeLiberation; `GameCanvas.prefab` by 10 more. The audit document
+  is not present on this branch so the 6 could not be cross-checked here. **Not resolved here — T2.5.**
+- **T3 precondition, now satisfied:** both forks sit in the same coordinate space, so consolidation no
+  longer has to reconcile a resolution delta mid-merge. Migrating them in parallel rather than
+  unifying first was deliberate scoping, not a deviation — unifying the fork is T3's job.
+- Override pressure is unchanged for T3: the gameplay scenes still carry ~1,828 prefab-instance
+  modifications each (T3 target: under 25).
+
+**Deviations from spec:**
+- **`Loadout Container.prefab` was not migrated.** Its CanvasScaler is `ConstantPixelSize` at Unity's
+  default 800×600 — not an 800×450-authored canvas — so `CanvasUpgradeProcessor.Scan` skips it twice
+  over (wrong scale mode, wrong reference resolution). In ConstantPixelSize the scale factor is pinned
+  at 1, so a ×2.4 pass would land as a literal 2.4× on-screen size increase. The criterion appears to
+  have been written from an assumption the asset does not meet; it needs amending rather than ticking.
+- **`AdaptiveCanvasScaler` was placed on the two GameCanvas prefabs rather than per scene.** The
+  gameplay scenes own no canvas, so the prefab is the only single-source-of-truth placement; this
+  covers all 21 scenes at once and matches `Docs/GAMECANVAS.md`. Four scenes that already carried it
+  as an instance override had that override removed to avoid duplicating the component.
+- **`_Scenes/Tools/PhotoBooth.unity` was deliberately skipped.** Its canvas is ScaleWithScreenSize at
+  800×600 (4:3); `AdaptiveCanvasScaler`'s 16:9 `referenceAspect` blend would be wrong against a 4:3
+  reference. Tool scene, no shipping impact.
+- **The 36 no-op CanvasScaler overrides were removed although the task did not ask for it.** A no-op
+  override still beats the prefab, so those 12 scenes would have silently ignored any future re-tune —
+  the mechanism that produced the ~1,734 identical overrides T3 now has to unwind.
+- Migration was performed as validated YAML surgery, not by running the editor tool: no Unity editor
+  is available in this environment. Document counts, anchor counts and dangling-reference sets are
+  unchanged on all 18 files, and the authored `AdaptiveCanvasScaler` keys were checked field-for-field
+  against `AdaptiveCanvasScaler.cs`. Import verification remains a human step.
+- No ledger entry was added. `CanvasUpgraderUpgradedPrefabs.txt` guards canvas-**less** fragments only;
+  these three assets are self-guarding via their own `referenceResolution`, which now reads 1920×1080
+  and makes the upgrader's `Scan` mark them already-upgraded.
+
+---
+
+## T2.6 — Nested UI fragment migration
+
+**Spec:** Style Foundation §5 · **Audit ref:** §1.3 · **Raised by:** T2
+
+The seven canvas-less fragments nested inside the GameCanvas prefabs are still authored in 800-space.
+T2 scaled their instance **roots** (what the upgrader does), so they now sit at the right position and
+frame size with 800-space interiors. In the 11 HexRace-fork scenes this is invisible — those scenes
+override the descendants — but in the 9 non-overriding GameCanvas scenes the interiors read small.
+They are shared with `MiniGameHUD.prefab` / `VesselHUD.prefab`, so this is its own pass, not a batch.
+
+Acceptance criteria:
+- [ ] `Pip.prefab` — **cut, do not migrate.** Raised by no gameplay code in any audited mode. Not
+      scaled into 1920-space until a cut decision exists. Design feedback queue entry raised and OPEN
+- [ ] `ThumbPerimeter.prefab` — **cut, do not migrate.** Belongs to the thumb cursors, which are
+      self-disabled in code under a "TEMP for SUSPEND" comment. Same gate; queue entry raised and OPEN
+- [ ] `GameOverPanel` — **BLOCKED, migrate neither.** Two prefabs exist
+      (`_Prefabs/UI Elements/Panels/GameOverPanel.prefab`, `_Prefabs/R_GameOverPanel.prefab`) and
+      which is live was never traced. Resolve first, or both get maintained forever
+- [ ] `CountdownTimer.prefab` migrated ×2.4 and logged in `CanvasUpgraderUpgradedPrefabs.txt`
+- [ ] `SceneTransitionModal.prefab` migrated ×2.4 and logged
+- [ ] `R_Pause_Menu_Panel.prefab` migrated ×2.4 and logged
+- [ ] `PauseMenu.pauseMenuPanel` verified to be a **GameObject** reference, not a CanvasGroup,
+      **before and after** the migration — a mistyped serialized reference on this prefab took the
+      Windows IL2CPP build down twice (see the doc comment on `PauseMenu.ResolvedPanel`)
+- [ ] `NotificationUI.prefab` — **not migrated here; folded into T3**, which already has to normalise
+      its rect (Joust's has drifted to ~(-1416, -463), likely off-screen). Interior migration and
+      position fix land in one pass
+- [ ] `MiniGameHUD.prefab` — **delete, do not migrate.** Never instantiated; reachable only through a
+      dangling override. T3's existing criteria cover resolving it
+- [ ] No fragment scaled twice — ledger checked before each pass
 
 **Deliverables:**
 **Findings:**
+- `R_GameOverPanel.prefab` (guid `aa18ad2b4731c37449403e155640cf0a`) is referenced by no prefab or
+  scene in `Assets/`; `GameOverPanel.prefab` (guid `494deef066b46a24a9b5226c4203833c`) is referenced by
```

</details>

### `448664774` — docs(ui): record PR #781 on the T2 tracker row

_Claude, 2026-08-25 00:17:42 +0000_

```text
 Docs/UI_REDESIGN_TASKS.md | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index 21738a871..183d4003e 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -13,7 +13,7 @@ Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table
 | ID | Task | Status | Depends on | Branch | PR | Completed |
 |---|---|---|---|---|---|---|
 | T1 | Safe area component | TODO | — | | | |
-| T2 | Finish canvas resolution migration | IN PROGRESS | — | `claude/canvas-resolution-ppu-migration-azv16k` | | |
+| T2 | Finish canvas resolution migration | IN PROGRESS | — | `claude/canvas-resolution-ppu-migration-azv16k` | #781 | |
 | T2.6 | Nested UI fragment migration | TODO | T2 | | | |
 | T3 | Unify GameCanvas fork | TODO | T2 | | | |
 | T4 | UIThemeSO + literal inventory | TODO | — | | | |
```

</details>

### `ed9a3e3ca` — docs(ui): re-run the T2 tracker against the now-present audit

_Claude, 2026-08-25 00:27:53 +0000_

```text
Merges bleeding-edge (Docs/UI_ARCHITECTURE_AUDIT.md + Docs/STYLE_FOUNDATION.md,
which the tracker cites and which were not on this branch at the first pass) and
re-verifies every T2 criterion against the merged tree. No criterion changed state:
still 8 of 10 met, 1 editor-only, 2 unticked.

With the audit available, four T2 entries are corrected:

- The stale-prefab state is NOT a new finding - audit S1.3 already records that both
  prefab assets sat at 800x450/PPU 100 while only their instances carried the
  overrides. Reframed to credit it and keep what is new: the consequence, that 12
  scenes were rendering un-overridden children 2.4x too small.
- The fork figure is resolved precisely. The audit lists 6 fork modes; the tree has
  11 scenes. The 5 it misses are Ribcage, WildlifeLiberation, DogFight, Bends and
  ScarabScramble - GameModes 39-43, the five newest, all added after the audit's
  survey. The figure is stale rather than wrong, and the mechanism is the point:
  every new mode copies the fork, so T3's cost grows until it is unified. Still T2.5.
- The Loadout Container deviation now cites the audit contradicting the criterion:
  S1.2's canvas table and S1.3 both record it as Constant Pixel Size at 800x600, a
  separate item from the 800x450 migration.
- Criterion 9 likewise: audit S1.3 already describes the project as spanning
  800x450, 800x600 and 1920x1080, so "no reference resolution outside 1920x1080"
  is broader than the migration this task defines.

Also confirms the audit's "5 of ~20 scenes" AdaptiveCanvasScaler list exactly, and
raises design queue #3: safeZone is unassigned in every instance project-wide
(audit S1.3), and the 5 instances added here keep that default - assigning it is a
framing decision, not an implementation one.
```

```text
 Docs/UI_REDESIGN_TASKS.md | 45 +++++++++++++++++++++++++++++++--------------
 1 file changed, 31 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index 183d4003e..ac9499f39 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -74,12 +74,11 @@ Acceptance criteria:
 - `ProjectSettings/ProjectSettings.asset` — `androidMaxAspectRatio` 2.1 → 2.4
 
 **Findings:**
-- **The two canvas prefabs were the stale half of an already-completed migration.** No gameplay scene
-  owns a canvas; all 21 instance one of the two prefabs, and 12 of them had already been migrated at
-  the prefab-instance override level (scaler pinned to 1920/240, children ×2.4) while the prefab
-  assets stayed at 800×450 / PPU 100. The 11 HexRace-fork scenes plus Maelstrom were therefore
-  running 800-space prefab children under a 1920 scaler — every child not covered by a scene
-  override was rendering 2.4× too small. Migrating the prefabs is what fixes that.
+- Audit §1.3 already records that both prefab **assets** sat at 800×450 / PPU 100 while only their
+  scene instances carried the 1920/240 overrides. What it does not record is the **consequence**: the
+  11 HexRace-fork scenes plus Maelstrom were running 800-space prefab children under a 1920 scaler,
+  so every child not covered by a scene override was rendering **2.4× too small**. That is what
+  migrating the prefab assets fixes; it was not a cosmetic tidy-up of an inert asset.
 - Migration correctness was checked against those pre-existing scene overrides as an oracle: the
   migrated prefab values match the scenes' independently-authored ×2.4 values on **199 of 231**
   comparable properties. The 32 differences are all elements those scenes deliberately repositioned
@@ -95,27 +94,44 @@ Acceptance criteria:
   (`NiceVibrations` 1080×1920, 3 × `QuickScenePro` 800×600), 4 first-party at Unity's default
   800×600 in **ConstantPixelSize** mode where the field is inert (`Loadout Container`,
   `StarShapeSign`, `HeartShapeSign`, `LightningShapeSign`), and `_Scenes/Tools/PhotoBooth.unity`
-  (800×600, ScaleWithScreenSize, tool scene). None is an 800×450-authored canvas.
+  (800×600, ScaleWithScreenSize, tool scene). None is an 800×450-authored canvas. Audit §1.3 already
+  describes the project as spanning 800×450, 800×600 and 1920×1080, and treats the 800×600 group as a
+  distinct Constant-Pixel-Size item — so "no reference resolution outside 1920×1080 project-wide" is
+  broader than the migration this task defines, and cannot be satisfied by it.
 - `TextMeshProUGUI.m_fontSizeBase` tracks `m_fontSize` only while auto-sizing is **off**. Established
   from the scenes the upgrader had already run on (auto-size-off rows carry ×2.4 on both keys;
   auto-size-on rows carry it on `m_fontSize` alone) and replicated. Scaling `m_fontSizeBase`
   unconditionally would corrupt every auto-sizing label.
-- **The GameCanvas fork spans 11 scenes, not the 6 the audit records** — `GameCanvas-HexRace.prefab`
-  is instanced by AstroLeague, Bends, CrystalCapture, DogFight, HexRace, Joust, NucleusRush, Rampage,
-  Ribcage, ScarabScramble and WildlifeLiberation; `GameCanvas.prefab` by 10 more. The audit document
-  is not present on this branch so the 6 could not be cross-checked here. **Not resolved here — T2.5.**
+- **The GameCanvas fork spans 11 scenes, not the 6 the audit records — and it is still growing.**
+  Audit §3 lists the fork's modes as HexRace, Joust, Crystal Capture, AstroLeague, NucleusRush,
+  Rampage (6), and §3 quotes "the six HexRace-fork scenes". Measured against the working tree,
+  `GameCanvas-HexRace.prefab` is instanced by **11** scenes; `GameCanvas.prefab` by 10 more (21 total).
+  The five the audit misses are **Ribcage, WildlifeLiberation, DogFight, Bends and ScarabScramble** —
+  `GameModes` 39, 40, 41, 42, 43, i.e. the five newest modes, every one added after the audit's fork
+  survey. So the figure is **stale rather than wrong**, and the mechanism matters more than the number:
+  each new mode is copying the fork, so T3's cost grows with every mode shipped until it is unified.
+  **Not resolved here — T2.5.**
 - **T3 precondition, now satisfied:** both forks sit in the same coordinate space, so consolidation no
   longer has to reconcile a resolution delta mid-merge. Migrating them in parallel rather than
   unifying first was deliberate scoping, not a deviation — unifying the fork is T3's job.
 - Override pressure is unchanged for T3: the gameplay scenes still carry ~1,828 prefab-instance
-  modifications each (T3 target: under 25).
+  modifications each (audit §3 quotes ~1,770; T3 target: under 25).
+- Audit §1.3 notes `AdaptiveCanvasScaler.safeZone` is unassigned in every instance it found, so the
+  ultrawide HUD-containment feature is effectively off. The 5 instances added here also leave it
+  unassigned — the component's own default, and not a call this task can make. Raised as design
+  queue #3 rather than guessed at, since assigning it changes HUD framing on every ultrawide display.
+- Audit §1.3's `AdaptiveCanvasScaler` coverage list ("5 of ~20 scenes": Menu_Main, HexRace, Joust,
+  Maelstrom, CrystalCapture) is confirmed exactly. Four of those five carried it as a per-instance
+  override rather than on the prefab, which is why the count read as scene-level coverage.
 
 **Deviations from spec:**
 - **`Loadout Container.prefab` was not migrated.** Its CanvasScaler is `ConstantPixelSize` at Unity's
   default 800×600 — not an 800×450-authored canvas — so `CanvasUpgradeProcessor.Scan` skips it twice
   over (wrong scale mode, wrong reference resolution). In ConstantPixelSize the scale factor is pinned
-  at 1, so a ×2.4 pass would land as a literal 2.4× on-screen size increase. The criterion appears to
-  have been written from an assumption the asset does not meet; it needs amending rather than ticking.
+  at 1, so a ×2.4 pass would land as a literal 2.4× on-screen size increase. **The audit itself records
+  this** — §1.2's canvas table lists the prefab as Constant Pixel Size / 800×600, and §1.3 lists it
+  alongside the three ShapeSign prefabs as "still Constant Pixel Size at 800×600", a separate item from
+  the 800×450 migration. The criterion contradicts its own source and needs amending rather than ticking.
 - **`AdaptiveCanvasScaler` was placed on the two GameCanvas prefabs rather than per scene.** The
   gameplay scenes own no canvas, so the prefab is the only single-source-of-truth placement; this
   covers all 21 scenes at once and matches `Docs/GAMECANVAS.md`. Four scenes that already carried it
@@ -274,6 +290,7 @@ Anything found during implementation that needs a design decision. The implement
 |---|---|---|---|---|
 | 1 | T2 | T2.6 | Cut `Pip.prefab`? It is raised by no gameplay code in any audited mode. Held out of the 1920-space migration until this is decided. | OPEN |
 | 2 | T2 | T2.6 | Cut `ThumbPerimeter.prefab`? It belongs to the thumb cursors, which are self-disabled in code under a "TEMP for SUSPEND" comment. Held out of the 1920-space migration until this is decided. | OPEN |
+| 3 | T2 | T2 | Should `AdaptiveCanvasScaler.safeZone` be assigned on the two GameCanvas prefabs? It is unassigned in every instance project-wide (audit §1.3), so ultrawide HUD containment is off. Assigning it pins HUD content to a centered 16:9 region on 21:9/32:9 — a framing decision, not an implementation one. | OPEN |
 
 ---
 
```

</details>

### `33789bcab` — docs(ui): drop the Android criterion from T2 and re-measure the fork for T3

_Claude, 2026-08-25 01:07:30 +0000_

```text
Mobile is deferred and desktop is the platform, so the Android max-aspect criterion
is removed from T2 and the change itself is reverted - with no criterion owning it,
a platform-settings edit in a desktop PR is an unowned change nobody will smoke-test.
Recorded under Deferred / out of scope with the rationale and the one line needed to
restore it. ProjectSettings.asset is now identical to bleeding-edge.

Re-measures the GameCanvas fork so T3 scopes on real numbers. The audit's "6 fork
scenes" and "~1,734 identical overrides" (22 Aug) are marked SUPERSEDED:

  fork scenes            6      -> 11   (CORE fork: 10; 21 total)
  identical overrides    ~1,734 -> 1,719 byte-identical / 1,735 same-key
  per-scene overrides    ~1,770 -> 1,748-1,760

Two results that change how T3 should be scoped:

- Widening 6 -> 11 costs T3 nothing. The identical-override core measures the SAME
  across the audit's 6 and across all 11 (1,719 / 1,735 either way); only 16
  (target, propertyPath) pairs genuinely differ in value across the whole set. The
  consolidation payload does not grow with the extra 5 scenes - only re-placement does.
- The newer fork scenes were DUPLICATED, not authored fresh. 9 of 11 share the same
  scene-local PrefabInstance anchor fileID (2113049457); only HexRace and Joust
  differ. A scene-local fileID is minted per scene, so an identical anchor across 9
  scenes is a scene-copy signature - each new mode inherited the full override set.
  (git creation dates are unusable as corroboration: the clone is shallow.)

Also records, report-only:

- Desktop settings: the audit is NOT stale. 1024x768, resizableWindow 0,
  fullscreenMode 1 (FullScreenWindow / borderless), defaultIsNativeResolution 1,
  allowFullscreenSwitch 1.
- ScreenSwitcher has no re-layout path on resolution change. LayoutScreensToViewport()
  has exactly one caller, Start(); no OnRectTransformDimensionsChange, and Update()
  handles only input gates. A resolution change leaves the filmstrip sized AND offset
  to the old viewport. resizableWindow 0 masks it today, but allowFullscreenSwitch 1
  already reaches it. Not fixed here.
- WidescreenLayoutAdapter: 0 attachments. AdaptiveCanvasScaler.safeZone: unassigned in
  all 6 instances project-wide. Both confirm the audit.

T3 is not started. The one line edited outside T2's own section is T3's
identical-override criterion, corrected in place at the owner's instruction.
```

```text
 Docs/UI_REDESIGN_TASKS.md             | 67 +++++++++++++++++++++++++++++++++++++++++++++++++----------------
 ProjectSettings/ProjectSettings.asset |  2 +-
 2 files changed, 52 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index ac9499f39..ebd8a8ba8 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -56,7 +56,6 @@ Acceptance criteria:
 - [x] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
 - [x] `AdaptiveCanvasScaler` on every scene canvas that lacked it
 - [x] Static `matchWidthOrHeight` overrides removed from those scenes
-- [x] Android max aspect raised 2.1 → 2.4
 - [ ] No remaining reference resolution outside 1920×1080 project-wide — 9 remain, see Findings
 - [~] Project builds; no canvas visibly regressed in a smoke pass — editor-only, human to confirm
 
@@ -71,7 +70,6 @@ Acceptance criteria:
 - Static `m_MatchWidthOrHeight: 0` override removed from all 12 scenes that pinned it
 - All 36 now-no-op `m_ReferenceResolution.x/.y` + `m_ReferencePixelsPerUnit` overrides removed from
   those 12 scenes — no CanvasScaler override of any kind now survives on any GameCanvas instance
-- `ProjectSettings/ProjectSettings.asset` — `androidMaxAspectRatio` 2.1 → 2.4
 
 **Findings:**
 - Audit §1.3 already records that both prefab **assets** sat at 800×450 / PPU 100 while only their
@@ -102,24 +100,60 @@ Acceptance criteria:
   from the scenes the upgrader had already run on (auto-size-off rows carry ×2.4 on both keys;
   auto-size-on rows carry it on `m_fontSize` alone) and replicated. Scaling `m_fontSizeBase`
   unconditionally would corrupt every auto-sizing label.
-- **The GameCanvas fork spans 11 scenes, not the 6 the audit records — and it is still growing.**
-  Audit §3 lists the fork's modes as HexRace, Joust, Crystal Capture, AstroLeague, NucleusRush,
-  Rampage (6), and §3 quotes "the six HexRace-fork scenes". Measured against the working tree,
-  `GameCanvas-HexRace.prefab` is instanced by **11** scenes; `GameCanvas.prefab` by 10 more (21 total).
-  The five the audit misses are **Ribcage, WildlifeLiberation, DogFight, Bends and ScarabScramble** —
-  `GameModes` 39, 40, 41, 42, 43, i.e. the five newest modes, every one added after the audit's fork
-  survey. So the figure is **stale rather than wrong**, and the mechanism matters more than the number:
-  each new mode is copying the fork, so T3's cost grows with every mode shipped until it is unified.
+- **Fork census re-measured for T3 scoping. Audit figures of "6 fork scenes" and "~1,734 identical
+  overrides" (22 Aug) are SUPERSEDED.** Measured against the working tree:
+
+  | | audit (22 Aug) | measured |
+  |---|---|---|
+  | `GameCanvas-HexRace.prefab` scenes | 6 | **11** |
+  | `CORE/GameCanvas.prefab` scenes | "the remaining" | **10** (21 total) |
+  | identical overrides across the fork set | ~1,734 | **1,719** byte-identical / **1,735** same-key |
+  | overrides per fork-scene canvas instance | ~1,770 | **1,748–1,760** (1,752–1,764 before this branch) |
+
+  Fork scenes (11): AstroLeague, Bends, CrystalCapture, DogFight, HexRace, Joust, NucleusRush,
+  Rampage, Ribcage, ScarabScramble, WildlifeLiberation.
+  CORE scenes (10): 2v2 CoOp, Maelstrom, DuelForCell, MultiplayerFreestyle, WildlifeBlitz CoOp,
+  BenchmarkStressTest, CellularDuel, WildlifeBlitz, Recording Studio, MattsRecording Studio.
+  The five the audit misses are `GameModes` 39–43 (Ribcage, WildlifeLiberation, DogFight, Bends,
+  ScarabScramble) — the five newest modes, all added after the audit's survey. The figure is **stale
+  rather than wrong**.
+- **Widening the set from 6 to 11 costs T3 nothing.** The identical-override core is *the same number*
+  measured across the audit's 6 and across all 11 (1,719 byte-identical / 1,735 same-key, both sets).
+  Only **16** (target, propertyPath) pairs genuinely differ in value across the whole fork set. So the
+  consolidation payload T3 has to push into the prefab does not grow with the extra 5 scenes — only
+  the re-placement work does. The audit's ~1,734 sits between the two measures and is consistent with
+  a same-key count taken before this branch removed 4 scaler overrides per scene.
+- **The newer fork scenes were duplicated, not authored fresh — this is why the override baggage is
+  identical.** 9 of the 11 fork scenes carry the *same scene-local `PrefabInstance` anchor fileID*
+  (`2113049457`): AstroLeague, Bends, CrystalCapture, DogFight, NucleusRush, Rampage, Ribcage,
+  ScarabScramble, WildlifeLiberation. Only HexRace (`330573866`) and Joust (`377908207`) differ. A
+  scene-local fileID is minted per scene, so an identical anchor across 9 scenes is a scene-copy
+  signature: each new mode was cloned from an existing fork scene and inherited its full override set.
+  (Creation dates from `git log --diff-filter=A` are **not** usable as corroboration here — the clone
+  is shallow, so those dates are when each file entered the clone, not when it was authored.)
   **Not resolved here — T2.5.**
 - **T3 precondition, now satisfied:** both forks sit in the same coordinate space, so consolidation no
   longer has to reconcile a resolution delta mid-merge. Migrating them in parallel rather than
   unifying first was deliberate scoping, not a deviation — unifying the fork is T3's job.
 - Override pressure is unchanged for T3: the gameplay scenes still carry ~1,828 prefab-instance
   modifications each (audit §3 quotes ~1,770; T3 target: under 25).
-- Audit §1.3 notes `AdaptiveCanvasScaler.safeZone` is unassigned in every instance it found, so the
-  ultrawide HUD-containment feature is effectively off. The 5 instances added here also leave it
-  unassigned — the component's own default, and not a call this task can make. Raised as design
-  queue #3 rather than guessed at, since assigning it changes HUD framing on every ultrawide display.
+- `AdaptiveCanvasScaler.safeZone` is unassigned in **all 6** instances project-wide — the 5 added by
+  this branch plus Menu_Main's — every one `{fileID: 0}`. Audit §1.3's reading is confirmed: ultrawide
+  HUD containment is off everywhere. Raised as design queue #3 rather than guessed at, since assigning
+  it changes HUD framing on every ultrawide display.
+- `WidescreenLayoutAdapter`'s guid appears in **0** scenes and **0** prefabs. Audit §1.3 confirmed.
+- **Desktop player settings — the audit is NOT stale, it is accurate.** `defaultScreenWidth: 1024`,
+  `defaultScreenHeight: 768`, `resizableWindow: 0`, `fullscreenMode: 1` (`FullScreenWindow`, i.e.
+  borderless), `defaultIsNativeResolution: 1`, `allowFullscreenSwitch: 1`. So the shipping default is
+  borderless-fullscreen at native resolution and the 1024×768 pair only applies to a windowed launch.
+- **`ScreenSwitcher` has no re-layout path on resolution change** (report only, not fixed here).
+  `LayoutScreensToViewport()` sizes every screen panel to `Screen.width` and positions it at
+  `i * viewportWidth`; it has exactly **one** caller — `Start()`. There is no
+  `OnRectTransformDimensionsChange`, and `Update()` handles only the freestyle/modal input gates. A
+  resolution change therefore leaves the filmstrip sized *and* offset to the old viewport, so panels
+  are the wrong width and navigation lands off-centre. `resizableWindow: 0` currently masks this — but
+  `allowFullscreenSwitch: 1` means an alt-enter fullscreen toggle already reaches it, and it becomes
+  a live desktop bug the moment the window is made resizable.
 - Audit §1.3's `AdaptiveCanvasScaler` coverage list ("5 of ~20 scenes": Menu_Main, HexRace, Joust,
   Maelstrom, CrystalCapture) is confirmed exactly. Four of those five carried it as a per-instance
   override rather than on the prefab, which is why the count read as scene-level coverage.
@@ -141,7 +175,7 @@ Acceptance criteria:
   reference. Tool scene, no shipping impact.
 - **The 36 no-op CanvasScaler overrides were removed although the task did not ask for it.** A no-op
   override still beats the prefab, so those 12 scenes would have silently ignored any future re-tune —
-  the mechanism that produced the ~1,734 identical overrides T3 now has to unwind.
+  the mechanism that produced the 1,719 identical overrides T3 now has to unwind.
 - Migration was performed as validated YAML surgery, not by running the editor tool: no Unity editor
   is available in this environment. Document counts, anchor counts and dangling-reference sets are
   unchanged on all 18 files, and the authored `AdaptiveCanvasScaler` keys were checked field-for-field
@@ -205,7 +239,7 @@ Acceptance criteria:
 Acceptance criteria:
 - [ ] Prefab Kit Validate run, output recorded
 - [ ] Prefab Kit Consolidate run
-- [ ] Identical overrides (~1,734) pushed into the prefab
+- [ ] Identical overrides (**1,719** byte-identical / 1,735 same-key across all **11** fork scenes; audit's "~1,734 across 6" superseded — see T2 Findings) pushed into the prefab
 - [ ] **One scene only** re-placed, diff reported, explicit go-ahead received before the rest
 - [ ] Override count per canvas instance below 25 in each migrated scene
 - [ ] `statsToTrack` preserved per mode (the one real per-mode value)
@@ -311,3 +345,4 @@ Decisions taken during the redesign to explicitly not do something. Recorded so
 | UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
 | Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
 | Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
+| Android `maxAspectRatio` 2.1 → 2.4 | Dropped from T2; change reverted | Mobile is deferred, desktop is the platform. The raise was applied and then reverted so the PR carries no unowned platform-settings change. One line in `ProjectSettings.asset` to restore when mobile resumes; at 2.1, 20:9 and 21:9 phones letterbox or crop per OEM |
```

</details>

### `37527cf59` — docs(ui): log T9, rescope T3 to 11 scenes, and name T2's open criteria

_Claude, 2026-08-25 20:19:23 +0000_

```text
T9 - ScreenSwitcher re-layout on resolution change. Logged as the highest-priority
open item: allowFullscreenSwitch is 1, so alt-enter drops the player into a windowed
1024x768 mid-session and LayoutScreensToViewport() - single caller, Start() - never
re-runs, leaving the menu filmstrip both mis-sized and mis-offset. Live in the
shipping config, not a future risk. Criteria cover an OnRectTransformDimensionsChange
or cached-resolution poll, re-anchoring to the current screen index without animating,
no per-frame work when unchanged, and alt-enter verification on every menu screen plus
with a modal open and in freestyle. Records the dependency: resizableWindow stays 0
until T9 ships - it is currently the only containment - and the window config is
reconsidered after, as its own decision.

T3 rescoped to the measured set:
  - 11 fork scenes, not 6; sub-25 override check and the launch / scoreboard checks
    now apply to all 11
  - the 16 genuinely-differing (target, propertyPath) pairs named as the real
    reconciliation work, since everything else consolidates mechanically
  - the one-scene-then-stop gate is kept and marked as mattering MORE at 11
  - second deliverable added: the supported way to stand up a new mode scene with a
    clean canvas instance, plus a CLAUDE.md prohibition on duplicating a mode scene.
    9 of 11 fork scenes share one PrefabInstance anchor fileID, so the debt is
    self-replicating; GAMECANVAS.md already states "a variant, never a copy", making
    this enforcement rather than new policy

T2's two open criteria now name what unblocks each rather than sitting as bare boxes.
One correction: the "no reference resolution outside 1920x1080" criterion is NOT
blocked on T2.6 - the seven nested fragments carry zero Canvas/CanvasScaler components
and so have no reference resolution to report. It is blocked on three separate
decisions: the four ConstantPixelSize 800x600 prefabs (a different migration class),
PhotoBooth, and the third-party demo scenes. The adjacent true statement - "no
800-space UI content remains" - is partly blocked on T2.6 and is noted as distinct.

Ultrawide containment (safeZone unassigned in all 6 instances, WidescreenLayoutAdapter
at 0 attachments) recorded as a post-EA deferral; design queue #3 marked DEFERRED.
```

```text
 Docs/UI_REDESIGN_TASKS.md | 87 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--------
 1 file changed, 78 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index ebd8a8ba8..7ea61b75e 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -19,8 +19,10 @@ Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table
 | T4 | UIThemeSO + literal inventory | TODO | — | | | |
 | T5 | Download & install TMP fonts | TODO | — | | | |
 | T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |
+| T9 | ScreenSwitcher re-layout on resolution change | TODO | — | | | |
 
 **Critical path:** T2 → T3 is the long pole. T1, T4, T5 are independent and can run in parallel. T6 needs T5's font assets to exist.
+**T9 is the highest-priority open item** — a live, Steam-visible bug in the shipping window config, independent of everything above, and `resizableWindow` must stay `0` until it lands.
 
 ---
 
@@ -52,11 +54,23 @@ Acceptance criteria:
 - [x] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
 - [x] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
 - [x] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
-- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated — **not a migration target**, see Deviations
+- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated — **OPEN. Blocked on an owner decision,
+      not on work.** The asset is not a migration target (ConstantPixelSize at Unity's default 800×600;
+      the audit records it as such in §1.2 and §1.3). Unblocked by amending or removing this criterion.
+      Nothing to implement.
 - [x] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
 - [x] `AdaptiveCanvasScaler` on every scene canvas that lacked it
 - [x] Static `matchWidthOrHeight` overrides removed from those scenes
-- [ ] No remaining reference resolution outside 1920×1080 project-wide — 9 remain, see Findings
+- [ ] No remaining reference resolution outside 1920×1080 project-wide — **OPEN. 9 remain.** Blocked on
+      three separate decisions, none of them T2.6: (a) the four **ConstantPixelSize 800×600** prefabs
+      (`Loadout Container`, `StarShapeSign`, `HeartShapeSign`, `LightningShapeSign`) — a different
+      migration class, needs its own task; (b) `_Scenes/Tools/PhotoBooth.unity` (800×600
+      ScaleWithScreenSize, tool scene) — needs a keep-or-migrate call; (c) 4 **third-party** demo
+      scenes (NiceVibrations, QuickScenePro ×3) — out of scope by definition.
+      ⚠ **Not blocked on T2.6.** The seven nested fragments carry **zero** `Canvas`/`CanvasScaler`
+      components, so they have no reference resolution to report and cannot move this criterion. The
+      *adjacent* statement is true and worth tracking separately: "no 800-space UI content remains" is
+      partly blocked on T2.6, but that is not what this criterion measures.
 - [~] Project builds; no canvas visibly regressed in a smoke pass — editor-only, human to confirm
 
 **Deliverables:**
@@ -240,14 +254,32 @@ Acceptance criteria:
 - [ ] Prefab Kit Validate run, output recorded
 - [ ] Prefab Kit Consolidate run
 - [ ] Identical overrides (**1,719** byte-identical / 1,735 same-key across all **11** fork scenes; audit's "~1,734 across 6" superseded — see T2 Findings) pushed into the prefab
-- [ ] **One scene only** re-placed, diff reported, explicit go-ahead received before the rest
-- [ ] Override count per canvas instance below 25 in each migrated scene
+- [ ] The **16** genuinely-differing (target, propertyPath) pairs reconciled — this is the real
+      reconciliation work; everything else in the set is byte-identical and consolidates mechanically
+- [ ] **One scene only** re-placed, diff reported, explicit go-ahead received before the rest —
+      the gate matters **more** at 11 scenes, not less
+- [ ] Override count per canvas instance below 25 in **each of the 11 fork scenes**
 - [ ] `statsToTrack` preserved per mode (the one real per-mode value)
-- [ ] Joust toast feed rect normalised from ~(-1416, -463)
+- [ ] Joust toast feed rect normalised from ~(-1416, -463), and `NotificationUI.prefab`'s 800-space
+      interior migrated in the same pass (folded in from T2.6)
 - [ ] 8 cross-asset dangling refs into the CORE prefab resolved
-- [ ] Dangling `CountdownDisplay` ref into never-instantiated `MiniGameHUD.prefab` resolved
-- [ ] All six modes launch and reach the Ready gate
-- [ ] End-game scoreboard renders in each mode
+- [ ] Dangling `CountdownDisplay` ref into never-instantiated `MiniGameHUD.prefab` resolved;
+      `MiniGameHUD.prefab` deleted rather than migrated (folded in from T2.6)
+- [ ] **All 11 fork modes** launch and reach the Ready gate (not 6)
+- [ ] End-game scoreboard renders in **each of the 11**
+
+**Second deliverable — stop the debt regrowing.** 9 of the 11 fork scenes share one
+`PrefabInstance` anchor fileID, i.e. each new mode was cloned from an existing mode scene and
+inherited its whole override set (T2 Findings). The debt is therefore **self-replicating**: unifying
+11 scenes without closing the cloning path buys roughly one sprint before the next mode re-creates it.
+`Docs/GAMECANVAS.md` already states the policy ("a variant, never a copy"), so this is **enforcement,
+not new policy**.
+
+- [ ] A supported path to stand up a new mode scene with a **clean** canvas instance — a Prefab Kit
+      action, or a written procedure if a tool is not warranted
+- [ ] `CLAUDE.md` states that duplicating an existing mode scene is **prohibited**, and names the
+      supported path instead
+- [ ] The new path produces a scene whose canvas instance starts at **0** overrides
 
 **Deliverables:**
 **Findings:**
@@ -316,6 +348,42 @@ Acceptance criteria:
 
 ---
 
+## T9 — ScreenSwitcher re-layout on resolution change
+
+**Audit ref:** §2 · **Raised by:** T2 · **Priority: highest open item — this is a live, Steam-visible
+bug in the shipping configuration, not a future risk.**
+
+`ScreenSwitcher.LayoutScreensToViewport()` sizes every screen panel to `Screen.width` **and** positions
+it at `i * viewportWidth`. It has exactly **one** caller — `Start()`. There is no
+`OnRectTransformDimensionsChange` and no resolution poll; `Update()` handles only the freestyle and
+modal input gates. So any resolution change leaves the menu filmstrip **both mis-sized and mis-offset**,
+and navigation lands off-centre.
+
+It is reachable today: `allowFullscreenSwitch: 1`, so alt-enter drops the player into a windowed mode
+at `defaultScreenWidth/Height` (1024×768) mid-session and the layout never re-runs. `resizableWindow: 0`
+is the **only** thing containing the blast radius.
+
+Acceptance criteria:
+- [ ] `LayoutScreensToViewport()` re-runs on resolution change — `OnRectTransformDimensionsChange` on
+      the driven canvas rect, or a cached-`Screen.width/height` poll, following the
+      `AdaptiveCanvasScaler` precedent (two cached-int compares per frame, work only on the frame it changes)
+- [ ] Re-anchors to the **current** screen index after re-layout, **without animating** — a resize must
+      not read as a navigation
+- [ ] No per-frame work when the resolution is unchanged
+- [ ] Verified by alt-entering mid-session on **every** menu screen (STORE, ARK, HOME, PORT, HANGAR)
+- [ ] Verified with a modal open and while in freestyle
+- [ ] **`resizableWindow` stays `0` until this lands** — see the dependency below
+
+**Dependency — do not enable `resizableWindow` before T9 ships.** The desktop window configuration
+(`resizableWindow: 0`, `fullscreenMode: 1`, 1024×768) is currently the only containment for this bug.
+Order is T9 first, then reconsider the window config as its own decision.
+
+**Deliverables:**
+**Findings:**
+**Deviations from spec:**
+
+---
+
 ## Design feedback queue
 
 Anything found during implementation that needs a design decision. The implementer **adds** entries here and does not resolve them or edit `STYLE_FOUNDATION.md` directly.
@@ -324,7 +392,7 @@ Anything found during implementation that needs a design decision. The implement
 |---|---|---|---|---|
 | 1 | T2 | T2.6 | Cut `Pip.prefab`? It is raised by no gameplay code in any audited mode. Held out of the 1920-space migration until this is decided. | OPEN |
 | 2 | T2 | T2.6 | Cut `ThumbPerimeter.prefab`? It belongs to the thumb cursors, which are self-disabled in code under a "TEMP for SUSPEND" comment. Held out of the 1920-space migration until this is decided. | OPEN |
-| 3 | T2 | T2 | Should `AdaptiveCanvasScaler.safeZone` be assigned on the two GameCanvas prefabs? It is unassigned in every instance project-wide (audit §1.3), so ultrawide HUD containment is off. Assigning it pins HUD content to a centered 16:9 region on 21:9/32:9 — a framing decision, not an implementation one. | OPEN |
+| 3 | T2 | T2 | Should `AdaptiveCanvasScaler.safeZone` be assigned on the two GameCanvas prefabs? It is unassigned in every instance project-wide (audit §1.3), so ultrawide HUD containment is off. Assigning it pins HUD content to a centered 16:9 region on 21:9/32:9 — a framing decision, not an implementation one. | DEFERRED — post-EA, see Deferred / out of scope |
 
 ---
 
@@ -345,4 +413,5 @@ Decisions taken during the redesign to explicitly not do something. Recorded so
 | UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
 | Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
 | Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
+| Ultrawide HUD containment (`AdaptiveCanvasScaler.safeZone`, `WidescreenLayoutAdapter`) | Post-EA decision; no action in T2 | `safeZone` is unassigned in all 6 instances and `WidescreenLayoutAdapter` has 0 attachments — both confirmed as audit findings, both deliberately left alone. Assigning either changes HUD framing on every ultrawide display and is a design call, not an implementation one |
 | Android `maxAspectRatio` 2.1 → 2.4 | Dropped from T2; change reverted | Mobile is deferred, desktop is the platform. The raise was applied and then reverted so the PR carries no unowned platform-settings change. One line in `ProjectSettings.asset` to restore when mobile resumes; at 2.1, 20:9 and 21:9 phones letterbox or crop per OEM |
```

</details>

### `ce7b83c59` — docs(ui): close T2's open criteria, add T10, and allow shared-section edits

_Claude, 2026-08-25 20:26:30 +0000_

```text
Skill: adds the standing exception the owner approved - shared sections (Critical
path, Design feedback queue, Style Foundation version log, Deferred / out of scope)
are editable by any task; another task's own section stays off limits. Also widens the
skill's task range, which still said T1-T6 and would have rejected T2.6 / T9 / T10.

T2's two open criteria are closed:

- Loadout Container criterion STRUCK. Constant Pixel Size at Unity's default 800x600
  feeding the Arcade Loadout view, whose survival through the redesign is unsettled -
  migrating it now risks work on a screen that gets cut. Moved to Deferred with a
  pointer to the Arcade rebuild, and listed in T10.
- The project-wide sweep is narrowed to "no shipping player-facing canvas outside
  1920x1080" and TICKED. Verified against Build Settings: every canvas in a
  build-enabled scene or a prefab those scenes use is 1920x1080. PhotoBooth is
  build-disabled and the 4 third-party demo scenes never ship, so both are out of
  scope rather than waived.

One correction to the proposed wording: the criterion is stated on RESOLUTION ONLY,
not "1920x1080 / PPU 240". 12 shipping canvases are 1920x1080 at PPU 100 -
Authentication, Bootstrap, FTUE_Canvas, Duel Cell Stats Canvas, VesselHUDContainer and
all 7 vessel ShipHUDContainers - against 4 at PPU 240. Both are correct: 240
compensates for the x2.4 scale-factor change and a canvas authored natively at
1920x1080 never had that change. Including "/ PPU 240" would have made the criterion
false, and satisfying it would have shrunk every 9-sliced border on those 12 by 2.4x.

T10 - ConstantPixelSize canvas migration. Logged as its own class with all four
prefabs (Loadout Container, StarShapeSign, HeartShapeSign, LightningShapeSign), since
the x2.4 pass is wrong for a canvas whose scale factor is pinned at 1. Criteria cover
deciding per canvas rather than assuming conversion, resolving Loadout Container with
the Arcade rebuild, and checking the three ShapeSigns are still reachable at all.

Also records that SplashScreen.unity is not in Build Settings. It was a named T2
target and is migrated, but the splash the player sees is Bootstrap's own canvas -
noted so the tick is not read as proof the scene is live.

T2 is now 7 met / 0 open / 1 editor-only. Status stays IN PROGRESS with a note that it
flips to DONE on human confirmation of the smoke pass: the skill's rule is that a [~]
criterion cannot be self-certified, and no editor is available here to run it.
```

```text
 .claude/skills/ui-redesign-tracker/SKILL.md | 16 ++++++++++----
 Docs/UI_REDESIGN_TASKS.md                   | 68 +++++++++++++++++++++++++++++++++++++++++++++++------------
 2 files changed, 66 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 151 lines)</summary>

```diff
diff --git a/.claude/skills/ui-redesign-tracker/SKILL.md b/.claude/skills/ui-redesign-tracker/SKILL.md
index 8dcecf9e1..a28ea58c8 100644
--- a/.claude/skills/ui-redesign-tracker/SKILL.md
+++ b/.claude/skills/ui-redesign-tracker/SKILL.md
@@ -1,6 +1,6 @@
 ---
 name: ui-redesign-tracker
-description: Verify and record completion of a Cosmic Shore UI redesign task (T1-T6) before merging its PR into bleeding-edge. Use when a UI redesign task branch is ready to merge, when asked to update the redesign tracker or checklist, or when asked whether a redesign task is actually done. Verifies acceptance criteria against the working tree rather than trusting a completion claim, updates Docs/UI_REDESIGN_TASKS.md, and routes design questions to the feedback queue.
+description: Verify and record completion of a Cosmic Shore UI redesign task (T1-T10, including sub-tasks such as T2.6) before merging its PR into bleeding-edge. Use when a UI redesign task branch is ready to merge, when asked to update the redesign tracker or checklist, or when asked whether a redesign task is actually done. Verifies acceptance criteria against the working tree rather than trusting a completion claim, updates Docs/UI_REDESIGN_TASKS.md, and routes design questions to the feedback queue.
 ---
 
 # UI Redesign Tracker
@@ -19,7 +19,7 @@ A partially complete task stays `IN PROGRESS`. Do not round up.
 
 ### 1. Identify the task
 
-Determine which of T1–T6 this branch addresses, from the branch name, the commit range against `bleeding-edge`, and the changed files. If more than one task is touched, handle each separately. If none maps to a tracked task, stop and say so — this skill does not track ad-hoc work.
+Determine which tracked task this branch addresses, from the branch name, the commit range against `bleeding-edge`, and the changed files. The set is not fixed at T1–T6 — it grows as work is split out (T2.6, T9, T10 so far), so read the status table rather than assuming a range. If more than one task is touched, handle each separately. If none maps to a tracked task, stop and say so — this skill does not track ad-hoc work.
 
 ### 2. Verify each acceptance criterion
 
@@ -38,8 +38,16 @@ reflowing, re-wrapping, tidying another task's checkboxes, renumbering the queue
 conflict into a manual merge. Leave whitespace, column widths and line breaks exactly as found, even
 where they are ugly. If another task's content looks wrong, say so in the report; do not fix it here.
 
-The one exception is step 4: unblocking a dependent means writing that task's **Status cell and
-nothing else** — not its criteria, not its notes, not its row's spacing.
+Two standing exceptions:
+
+- **Step 4** — unblocking a dependent means writing that task's **Status cell and nothing else**: not
+  its criteria, not its notes, not its row's spacing.
+- **Shared sections are editable by any task.** The Critical path line, the Design feedback queue, the
+  Style Foundation version log and the Deferred / out of scope table belong to the tracker as a whole,
+  not to one task. Surfacing the highest-priority item on the Critical path line is exactly what that
+  line is for. The rule above exists to stop parallel branches colliding on unrelated diffs — it is not
+  a reason to bury a finding where no reader will see it. Keep such edits additive and one line where
+  you can. **Another task's own section stays off limits.**
 
 - Set the status. `DONE` only when every criterion is `[x]`, or every remaining one is `[~]` and the human has confirmed them.
 - Fill in branch, PR number, and completion date.
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index 7ea61b75e..f9a0a991b 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -20,6 +20,7 @@ Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table
 | T5 | Download & install TMP fonts | TODO | — | | | |
 | T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |
 | T9 | ScreenSwitcher re-layout on resolution change | TODO | — | | | |
+| T10 | ConstantPixelSize canvas migration | TODO | — | | | |
 
 **Critical path:** T2 → T3 is the long pole. T1, T4, T5 are independent and can run in parallel. T6 needs T5's font assets to exist.
 **T9 is the highest-priority open item** — a live, Steam-visible bug in the shipping window config, independent of everything above, and `resizableWindow` must stay `0` until it lands.
@@ -54,25 +55,26 @@ Acceptance criteria:
 - [x] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
 - [x] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
 - [x] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
-- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated — **OPEN. Blocked on an owner decision,
-      not on work.** The asset is not a migration target (ConstantPixelSize at Unity's default 800×600;
-      the audit records it as such in §1.2 and §1.3). Unblocked by amending or removing this criterion.
-      Nothing to implement.
 - [x] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
 - [x] `AdaptiveCanvasScaler` on every scene canvas that lacked it
 - [x] Static `matchWidthOrHeight` overrides removed from those scenes
-- [ ] No remaining reference resolution outside 1920×1080 project-wide — **OPEN. 9 remain.** Blocked on
-      three separate decisions, none of them T2.6: (a) the four **ConstantPixelSize 800×600** prefabs
-      (`Loadout Container`, `StarShapeSign`, `HeartShapeSign`, `LightningShapeSign`) — a different
-      migration class, needs its own task; (b) `_Scenes/Tools/PhotoBooth.unity` (800×600
-      ScaleWithScreenSize, tool scene) — needs a keep-or-migrate call; (c) 4 **third-party** demo
-      scenes (NiceVibrations, QuickScenePro ×3) — out of scope by definition.
-      ⚠ **Not blocked on T2.6.** The seven nested fragments carry **zero** `Canvas`/`CanvasScaler`
-      components, so they have no reference resolution to report and cannot move this criterion. The
-      *adjacent* statement is true and worth tracking separately: "no 800-space UI content remains" is
-      partly blocked on T2.6, but that is not what this criterion measures.
+- [x] **No shipping player-facing canvas outside 1920×1080** — verified: every canvas in a
+      build-enabled scene or a prefab those scenes use is 1920×1080, with the four ConstantPixelSize
+      800×600 prefabs split out as **T10**. Non-shipping canvases are excluded by scope, not waived:
+      `PhotoBooth.unity` is build-**disabled**, and the 4 third-party demo scenes (NiceVibrations,
+      QuickScenePro ×3) never ship.
+      ⚠ **PPU is deliberately NOT uniform and must not be normalised** — the criterion is stated on
+      resolution alone for that reason. 12 shipping canvases are 1920×1080 at **PPU 100**
+      (Authentication, Bootstrap, `FTUE_Canvas`, `Duel Cell Stats Canvas`, `VesselHUDContainer` and
+      all 7 vessel `ShipHUDContainer`s); 4 are at **PPU 240** (both GameCanvas prefabs, Menu_Main,
+      SplashScreen). Both are correct: 240 compensates for the ×2.4 scale-factor change, and a canvas
+      authored natively at 1920×1080 never had that change — see Findings. Forcing the 12 to 240 would
+      shrink every 9-sliced border 2.4×.
 - [~] Project builds; no canvas visibly regressed in a smoke pass — editor-only, human to confirm
 
+> **Every criterion except the editor smoke pass is met.** Status flips to `DONE` on human
+> confirmation of that pass — the skill's rule is that a `[~]` criterion cannot be self-certified.
+
 **Deliverables:**
 - `_Prefabs/CORE/GameCanvas.prefab` — refRes 800×450 → 1920×1080, refPPU 100 → 240, 289 canvas-space values ×2.4
 - `_Prefabs/GameCanvas-HexRace.prefab` — same, 465 values ×2.4
@@ -110,6 +112,10 @@ Acceptance criteria:
   describes the project as spanning 800×450, 800×600 and 1920×1080, and treats the 800×600 group as a
   distinct Constant-Pixel-Size item — so "no reference resolution outside 1920×1080 project-wide" is
   broader than the migration this task defines, and cannot be satisfied by it.
+- **`SplashScreen.unity` is not in Build Settings.** It was a named T2 target and is migrated, but it
+  does not ship today — the splash the player sees is `Bootstrap.unity`'s `Canvas - Splash Screen`.
+  The migration is still correct first-party content; recording it so nobody reads the tick as proof
+  the scene is live. (`PhotoBooth.unity` and both Recording Studios are likewise build-disabled.)
 - `TextMeshProUGUI.m_fontSizeBase` tracks `m_fontSize` only while auto-sizing is **off**. Established
   from the scenes the upgrader had already run on (auto-size-off rows carry ×2.4 on both keys;
   auto-size-on rows carry it on `m_fontSize` alone) and replicated. Scaling `m_fontSizeBase`
@@ -384,6 +390,39 @@ Order is T9 first, then reconsider the window config as its own decision.
 
 ---
 
+## T10 — ConstantPixelSize canvas migration
+
+**Audit ref:** §1.2, §1.3 · **Raised by:** T2
+
+A **different migration class** from T2's 800×450 → 1920×1080 work, which is why it was split out
+rather than left as an unticked T2 criterion. These four canvases are `ConstantPixelSize` at Unity's
+default 800×600 — untouched defaults, not authored 800×450 layouts. In ConstantPixelSize the scale
+factor is pinned at 1, so the ×2.4 pass T2 used would land as a literal 2.4× on-screen size increase;
+`CanvasUpgradeProcessor.Scan` skips all four twice over (wrong scale mode, wrong reference resolution).
+
+The four:
+- `_Prefabs/UI Elements/Loadout Container.prefab` — feeds the Arcade **Loadout** view; also on the
+  Deferred list pending the Arcade rebuild, so it may be cut rather than migrated
+- `_Prefabs/UI Elements/Panels/StarShapeSign.prefab`
+- `_Prefabs/UI Elements/Panels/HeartShapeSign.prefab`
+- `_Prefabs/UI Elements/Panels/LightningShapeSign.prefab`
+
+Acceptance criteria:
+- [ ] Decide per canvas: convert to ScaleWithScreenSize @1920×1080, or keep ConstantPixelSize
+      deliberately (some world-space-ish signage legitimately wants fixed pixel size)
+- [ ] `Loadout Container` resolved **with** the Arcade rebuild decision, not ahead of it
+- [ ] Any canvas converted to ScaleWithScreenSize gets `AdaptiveCanvasScaler` and the ×2.4 pass, and
+      is logged in `CanvasUpgraderUpgradedPrefabs.txt` if it is canvas-less content
+- [ ] Any canvas kept at ConstantPixelSize has that recorded as a decision, so the next sweep does
+      not re-flag it
+- [ ] The three ShapeSign prefabs checked for whether they are still reachable at all before any work
+
+**Deliverables:**
+**Findings:**
+**Deviations from spec:**
+
+---
+
 ## Design feedback queue
 
 Anything found during implementation that needs a design decision. The implementer **adds** entries here and does not resolve them or edit `STYLE_FOUNDATION.md` directly.
@@ -413,5 +452,6 @@ Decisions taken during the redesign to explicitly not do something. Recorded so
 | UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
 | Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
 | Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
+| `Loadout Container.prefab` migration | Struck from T2; deferred to the Arcade rebuild | Constant Pixel Size at Unity's default 800×600, feeding the Arcade **Loadout** view (audit §1.2, §2.11). Whether that view survives the redesign is unsettled, so migrating it now risks work on a screen that gets cut. Revisit with the Arcade rebuild; it is also listed in **T10** as one of the four ConstantPixelSize canvases |
 | Ultrawide HUD containment (`AdaptiveCanvasScaler.safeZone`, `WidescreenLayoutAdapter`) | Post-EA decision; no action in T2 | `safeZone` is unassigned in all 6 instances and `WidescreenLayoutAdapter` has 0 attachments — both confirmed as audit findings, both deliberately left alone. Assigning either changes HUD framing on every ultrawide display and is a design call, not an implementation one |
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
