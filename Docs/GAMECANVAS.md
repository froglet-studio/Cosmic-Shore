# GameCanvas — one canvas, every mode

`GameCanvas.prefab` is meant to be the single in-game UI surface: drop it into a new game-mode
scene and the HUD, scoreboard, pause menu, countdown and connecting panel all work. Today it is
not that, and this document says exactly why, with numbers, plus what was fixed in code and what
is left to do in the editor.

**Read this before touching any game-mode scene's canvas.**

---

## 0. The diagnosis in one paragraph

There are **two forked GameCanvas assets**, and the scenes that share the newer fork each carry
**~1,770 unapplied overrides — of which 1,733 are byte-identical in every one of them**. An override
parked in a scene always beats the prefab, so those properties are effectively hand-maintained once
per scene: editing the prefab changes nothing in any of them. That is the mechanism behind "I have
to go to every scene to make one common change". Only **16** override keys genuinely differ between
the scenes — the real per-mode configuration is tiny and always was.

> **Scope, re-measured 2026-09-07 (`Tools/Build/gamecanvas_unification_report.py`):** the fork is
> on **15** scenes, not six — every mode cloned since (Drumfire, Hijack, Switchback, Salvo, Scarab
> Scramble, Dog Fight, Bends, Wildlife Liberation, Peel the Cage) inherited the whole override blob
> from its donor scene. And the overrides were only half the story: **every fork scene also carries
> STRUCTURAL edits on its canvas instance** — 9 removed objects, 3 removed components and 3
> scene-added components — identical across 12 of the 15. See §9. **The prefab asset was never what
> ran.** The retirement is driven by **FrogletTools ▸ Game Modes ▸ GameCanvas Unifier** (§9).

---

## 1. Two prefabs, not one

| | `_Prefabs/CORE/GameCanvas.prefab` | `_Prefabs/GameCanvas-SkimRace.prefab` |
|---|---|---|
| GUID | `65bf1ed35b752374ca46ae214710e41c` | `abd30ad4cfca9ae4a8aecfde9f650cf3` |
| Relationship | base | **hard copy — NOT a prefab variant** |
| Serialized objects | 453 | 641 |
| GameObjects | 105 | 141 (101 shared) |
| HUD component | `MiniGameHUD` + `MiniGameHUDView` | `MultiplayerHUD` + `MultiplayerHUDView` |
| Toast feed | plain `NotificationUI` GameObject | nested `NotificationUI.prefab` |
| Stats provider | — | `EventDrivenStatsProvider` |

Because it is a copy and not a variant, **nothing propagates between them**. A fix to the base
never reaches the six newer modes, and vice versa.

### Who uses which

| Fork | Scenes |
|---|---|
| `GameCanvas-SkimRace` (15) | SkimRace, Joust, Crystal Capture (Scurry), AstroLeague, BroodRush, Rampage, PeelTheCage, WildlifeLiberation, DogFight, Bends, ScarabScramble, Salvo, Switchback, Hijack, Drumfire |
| `CORE/GameCanvas` (10) | 2v2CoOpVsAI, Maelstrom, DuelForCell, FreestyleMultiplayer, WildlifeBlitz (MP + SP), DuelForTheCell, BenchmarkStressTest, Recording Studio ×2 |

(The 6-scene table below §2 is the 2026-08 measurement kept for the record; the nine newer scenes
carry the same 1,770-override / 9-removed / 3-added signature as Rampage.)

### Structural delta (root name normalised)

`GameCanvas-SkimRace` is a near-perfect **superset**: 101 of 105 GameObjects are shared.

- **+40 in SkimRace**: `MiniGameHUD/AllyDomainContainer`, `MiniGameHUD/MultiplayerPlayerScoreCard`,
  `Scoreboard/Buttons/Continue` (+ its text and controller-button image),
  `Scoreboard/MultiplayerView/MultiplayerView/TeamScorecard` ×3 (the per-domain scorecards),
  `Scoreboard/MultiplayerView/BackgroundBottom/Goodies/XPEarned` + `XPIcon`,
  `CrystalDisplayBG` (+ `Icon`, `XPEarnedText`), `XPDisplayBG` (+ `XPEarnedText`).
- **−4 in SkimRace**: `MiniGameHUD/NotificationUI` (replaced by the nested prefab — an upgrade, not
  a loss) and `Scoreboard/MultiplayerView/MultiplayerScores/PlayerFour` + its `Name`/`Score` (the
  4th row of the legacy per-player scoreboard, superseded by the TeamScorecards).

Both HUD pairs are inheritance chains — `MultiplayerHUD : MiniGameHUD` and
`MultiplayerHUDView : MiniGameHUDView` — and the derived view **degrades gracefully**
(`MultiplayerHUDView.HasDomainPanelWiring` falls back to the legacy per-player layout when the
domain containers are unassigned). So the superset can serve both families.

---

## 2. The override load, per scene

Counts are unapplied `m_Modifications` on the GameCanvas instance in each scene, bucketed by what
the property is.

| Scene | Fork | Mods | Breakdown |
|---|---|---:|---|
| MinigameScurryMultiplayer_Gameplay | SkimRace | **1774** | layout 1249 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameJoust_Gameplay | SkimRace | **1771** | layout 1245 · other 457 · script-field 39 · active 15 · button 10 |
| MinigameRampage | SkimRace | **1771** | layout 1245 · other 458 · script-field 37 · active 16 · button 10 |
| MinigameAstroLeague | SkimRace | **1770** | layout 1245 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameBroodRush | SkimRace | **1770** | layout 1245 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameSkimRace | SkimRace | **1766** | layout 1247 · other 458 · script-field 37 · active 15 · button 4 |
| BenchmarkStressTest | CORE | 105 | layout 36 · script-field 27 · button 21 · active 12 |
| MinigameWildlifeBlitz (SP) | CORE | 105 | layout 36 · script-field 27 · button 21 · active 12 |
| ArcadeGameMultiplayer2v2CoOpVsAI | CORE | 96 | layout 58 · font-noise 12 · button 11 · script-field 8 |
| MinigameDuelForCellMultiplayer_Gameplay | CORE | 96 | layout 58 · font-noise 12 · button 11 · script-field 8 |
| MinigameDuelForTheCell | CORE | 85 | layout 53 · button 13 · active 6 · script-field 5 |
| MinigameFreestyleMultiplayer_Gameplay | CORE | 81 | layout 49 · button 11 · script-field 11 · active 5 |
| Maelstrom | CORE | 65 | layout 49 · button 10 (+ 8 removed GameObjects) |
| MinigameWildlifeBlitzMultuplayerCoOp | CORE | 61 | layout 49 · button 10 |
| Recording Studio / MattsRecording Studio | CORE | 27 each | layout 21 · other 4 |

---

## 3. The finding that matters: 1,734 of them are the same everywhere

Comparing override *values* key-by-key across the six SkimRace-fork scenes:

```
overrides present & IDENTICAL in every scene : 1734   <-- belong in the prefab
present in some scenes only, same value      :   36
genuinely DIFFERENT values between scenes    :   20   <-- real per-mode config
```

1,734 properties were laid out once, then re-created (or copy-pasted) into five more scenes and
never applied back. They are not configuration — they are six copies of the same decision. **This
is the whole problem.** Consolidating them into the prefab is a mechanical, reversible operation
and is what the tooling below automates.

### The 20 that genuinely differ

| Target | Property | Verdict |
|---|---|---|
| `ScoreboardController` (`EventDrivenStatsProvider`) | `statsToTrack` (array + 5 elements) | **Real per-mode data.** SkimRace tracks 5 (CleanCrystals, Jousts Won, Longest Drift, MaxBoost, PrismsDamaged); the other five track 3 (Longest Drift, MaxBoost, PrismsDamaged); Joust's list leads with Jousts Won. |
| `MiniGameHUD/ReadyButton` (`Button`) | `m_OnClick…m_TargetAssemblyTypeName` | **Eliminated in code** — see §5. |
| `MiniGameHUD` (`MultiplayerHUD`) | `_eventResponses…m_TargetAssemblyTypeName` ×3 | Per-mode SOAP listener wiring; SkimRace omits them entirely. |
| `ScoreboardController` | `multiplayerController`, `hexRaceController` | Scene references — **SkimRace only**; the other five leave them null. Auto-resolvable. |
| `NotificationUI` (`RectTransform`) | `m_AnchoredPosition.x/y`, `m_SizeDelta.x/y` | **Accidental drift.** 3 of 6 agree exactly (−314.4, 90); SkimRace, Crystal Capture and Joust each wandered. Joust is far out at (−1416, −463). |
| `Scoreboard/Buttons/Continue`, `HomeButton`, `PlayAgainButton` | `m_AnchoredPosition.x/y` | **Accidental drift.** 5 of 6 agree; SkimRace alone differs. |

Only the first row is unambiguously per-mode configuration. Everything else is either fixed in code
or is drift to be normalised.

---

## 4. Latent bug: 8 dangling cross-prefab references

`GameCanvas-SkimRace.prefab` contains overrides whose `objectReference` points at objects **inside
`CORE/GameCanvas.prefab`** — a different asset:

| Owner (inside GameCanvas-SkimRace) | Field | Points into |
|---|---|---|
| `ScoreboardPanel` → `GameOverPanel` | `animatedRoot` | `CORE/GameCanvas.prefab` fileID `1619673762671833843` |
| `ScoreboardPanel` → `GameOverPanel` | `bestScoreText` | … `6110111930378262139` |
| `ScoreboardPanel` → `GameOverPanel` | `highScoreText` | … `5279711995503518383` |
| `ScoreboardPanel` → `GameOverPanel` | `continueButton` | … `1462904225399142908` |
| `ScoreboardPanel` → `GameOverPanel` | `endGameStatsPanel` | … `7197415339648910700` |
| `ScoreboardPanel` → `HomeButton` | `m_OnClick…m_Target` | … `8557494847420733543` |
| `EndGameStatsPanel` | `view` | … `3219158110593327102` |
| `EndGameStatsPanel` | `connectingPanel` | … `1897917299` |

These resolve to objects in the *asset*, not to anything in the running scene, so the end-game
panel is driving UI nobody can see. This is the signature of a prefab created by copying rather
than by **Create → Prefab Variant**. Fixing the fork fixes these.

---

## 5. What was fixed in code (already done)

Two of the three reasons a scene had to hand-wire GameCanvas are gone. Both are strictly additive —
an explicit inspector assignment still wins, so **no existing scene changes behaviour**.

### `MiniGameHUD.EnsureReadyButtonWiring()`
`MiniGameControllerBase.OnReadyClicked()` is **public on the base class**, so the per-scene
UnityEvent hookups naming a concrete controller (`SkimRaceController`,
`JoustController`, …) never needed to be per-scene. The HUD now finds the scene's
controller at `Start()` and connects the button itself, unless a persistent listener already
targets a controller (checked against the live target object, not the serialized type name, so a
renamed or subclassed controller still counts).

### `Scoreboard.ResolveGameController()`
`Scoreboard.gameController` used to be a required per-scene reference, and Play Again logged an
error without it. There is exactly one `MiniGameControllerBase` per gameplay scene, so it now
resolves itself when unassigned. Menu and tool scenes with no controller log an informational line
instead of an error.

**Net effect:** a brand-new game-mode scene can drop GameCanvas in and the Ready button and Play
Again work with zero inspector wiring.

---

## 6. What to do in Unity (superseded — see §9)

> **This section is the 2026-08 plan and is superseded by §9.** Its Step 1 (consolidate the uniform
> overrides through the Prefab Kit) would have pushed the 1,733 values into the FORK, but the fork
> is not what the scenes run (§9.1), and Step 3's "delete the instance and drag the prefab in by
> hand, re-doing ~20 values" is now a button with a dry run. Kept for the reasoning; run §9.

Tooling: **FrogletTools ▸ Game Modes ▸ Game Mode Prefab Kit**. Its **Validate** pass reads scene
YAML directly (no scenes are opened), reports every instance carrying unapplied overrides, and
separates the *identical-everywhere* set from the *genuinely-different* set. Every write goes back
through `PrefabUtility`.

### Step 1 — Consolidate the 1,734 (low risk, reversible)

1. Open the Prefab Kit, find the **GameCanvas** row, press **Validate**.
2. The first issue reads *"N override(s) are IDENTICAL in all 6 scenes"*. Press **Consolidate**.
   It applies them to the prefab from a donor scene, then reverts them in the other five, and
   saves each scene.
3. Re-run **Validate**. The remaining per-scene overrides should be ~20 keys, all listed in §3.
4. Play-test the six modes. Commit scenes and prefab together.

> Both `GameCanvas.prefab` and `GameCanvas-SkimRace.prefab` should be rows in the kit so each fork
> consolidates against its own scenes.

### Step 2 — Normalise the accidental drift (manual, small)

In the prefab (not the scenes), settle one value for each and revert the scene overrides:

- `NotificationUI` RectTransform → adopt the 3-scene majority `(-314.4, 90)`, size `(489.08, 420)`.
- `Scoreboard/Buttons/Continue|HomeButton|PlayAgainButton` → adopt the 5-scene majority
  (`Continue.x = 537.6`, `HomeButton.x = 912`, `PlayAgain.x = 163.2`).

### Step 3 — Retire the fork (the real unification)

`GameCanvas-SkimRace` is the superset, so it becomes the single canvas:

1. Restore the two things only the base has: the legacy `PlayerFour` row under
   `Scoreboard/MultiplayerView/MultiplayerScores` (or delete it from the base too if the
   TeamScorecards have replaced it).
2. Re-point the 8 dangling references in §4 at the objects inside the *same* prefab.
3. Rename it `GameCanvas` and move it to `_Prefabs/CORE/`.
4. For each of the 10 scenes still on the old fork: delete the old instance, drag the unified
   prefab in, and re-do only the ~20 real per-scene values.
   *A GUID swap does not work* — the two forks have different fileIDs, so every override would
   dangle.
5. Delete the old asset once no scene references its GUID.

Step 3 is the one that needs judgement and play-testing per scene; steps 1 and 2 are safe and
should land first.

### Step 4 — Close the last override (optional)

`statsToTrack` is the only genuinely per-mode value that must live on the canvas. To reach zero
overrides, move it into a `GameModeStatsProfileSO` keyed by `GameDataSO.GameMode` and have
`EventDrivenStatsProvider` consult it when its explicit list is empty (it already falls back to
vessel-telemetry discovery, so the priority chain becomes explicit → profile → telemetry).

---

## 7. Rules going forward

- **One canvas asset.** If a mode needs a different canvas, make a **Prefab Variant** — never a
  copy. A copy severs propagation and re-creates this exact problem.
- **Never leave overrides in a scene.** If a change should apply to every mode, `Apply to Prefab`.
  If it is genuinely per-mode, it belongs in config (an SO keyed by `GameModes`) or in code that
  resolves it at runtime — not in a scene override.
- **Never hand-wire a scene reference the canvas can find itself.** There is one
  `MiniGameControllerBase` per gameplay scene; resolve it in code.
- **Never bind a UnityEvent to a concrete controller subclass.** `OnReadyClicked` and friends are
  public on `MiniGameControllerBase`; binding the subclass creates a per-scene override for no gain.
- **Run the Prefab Kit's Validate before committing a scene** that contains GameCanvas. Drift is
  cheap to fix the day it appears and expensive a year later.

---

## 8. File index

| Role | Path |
|---|---|
| The canvas prefab (the only one, once §9 lands) | `Assets/_Prefabs/CORE/GameCanvas.prefab` |
| Forked canvas prefab (retired by §9) | `Assets/_Prefabs/GameCanvas-SkimRace.prefab` |
| Unifier (operations) | `Assets/_Scripts/Editor/FrogletTools/GameCanvasUnifier.cs` |
| Unifier (window) | `Assets/_Scripts/Editor/FrogletTools/GameCanvasUnifierWindow.cs` |
| Offline report + CI gate | `Tools/Build/gamecanvas_unification_report.py` |
| Per-mode scoreboard stats (the one real per-mode value) | `Assets/_Scripts/ScriptableObjects/GameModeStatsProfileSO.cs`, `Assets/Resources/GameModeStatsProfile.asset` |
| Canvas behaviour | `Assets/_Scripts/UI/GameCanvas.cs` |
| HUD (base / derived) | `Assets/_Scripts/UI/MiniGameHUD.cs`, `MultiplayerHUD.cs` |
| HUD views | `Assets/_Scripts/UI/View/MinigameHUDView.cs`, `MultiplayerHUDView.cs` |
| End-game scoreboard | `Assets/_Scripts/UI/Scoreboard.cs` |
| Per-mode stats | `Assets/_Scripts/Controller/Vessel/EventDrivenStatsProvider.cs` |
| Kit config asset | `Assets/Resources/GameModePrefabKit.asset` |
| Kit window | `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs` |
| Drift scanner (read) | `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs` |
| Drift fixer (write) | `Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs` |
| Validation rules | `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs` |

---

## 9. The retirement: what actually ships, and the tool that unifies it (2026-09-07)

### 9.1 The finding the override counts hid

Reading the 15 fork scenes' `PrefabInstance` blocks in full — the `m_RemovedGameObjects`,
`m_RemovedComponents`, `m_AddedGameObjects` and `m_AddedComponents` sections, not just
`m_Modifications` — gives a different picture from "1,770 overrides":

| | 12 scenes (Rampage family) | Joust | Scurry | Skim Race |
|---|---|---|---|---|
| Property overrides | 1,770–1,771 | 1,771 | 1,774 | 1,766 |
| Removed objects | 9 (`Scoreboard/SinglePlayerView`, the whole `Scoreboard/MultiplayerView` subtree incl. both extra `TeamScorecard`s, `MultiplayerScores`, `MainHeader`, `Top Bar`, `Column Headers`, `BackgroundBottom`) | 10 (+ one stale id) | 9 | 9 |
| Removed components | the prefab's own `MultiplayerHUD`, `Scoreboard`, and one stale id | same | same | **none** |
| Added components | `MultiplayerHUD` (on `MiniGameHUD`), `Scoreboard` (on `ScoreboardController`), `EndGameSequencer` (on the nested `EndGameStatsPanel`) | + `AdaptiveCanvasScaler` | + `AdaptiveCanvasScaler` | `AdaptiveCanvasScaler` only |
| Added objects | `ConnectingPanel.prefab` (nested, under the root), `BackgroudTop/Scroll View/…` (under `Scoreboard`) | + a second `NotificationUI.prefab` | same as the 12 | same as the 12 |

So in 14 of 15 scenes the prefab's HUD and Scoreboard components are **removed and re-added as
scene components** (their serialized values agree across all 14 to the field — only two stale
renamed keys differ), the old end-game layout is deleted, and a newer one is added. The
`m_TargetAssemblyTypeName` strings on the HUD's listeners still say `JoustHUD` / `ScurryHUD` —
classes that no longer exist — which dates the replacement. **The canvas that ships is the fork
minus that subtree plus those components, and it exists only as a scene-side edit, fifteen times.**
Consolidating the 1,733 uniform overrides into the fork prefab (§6 Step 1) would therefore have
produced a prefab nobody runs.

Two smaller things the full read also settled:

- **1,390 of a scene's 1,771 overrides address objects INSIDE nested prefabs** (the GameOverPanel's
  scoreboard layout, the pause panel, the countdown), through fileIDs the outer prefab's YAML never
  contains. That is why every earlier "look at the prefab" pass under-counted, and why a resolver
  must treat an unresolvable target as *nested*, not as *stale*.
- The 16 keys that differ everywhere are the ones already itemised in §3 (Skim Race's button
  positions and 5-stat list, the Ready button's cosmetic type name, Joust's off-screen toast rect at
  (−1416, −463)), plus **`m_MatchWidthOrHeight` — 1 in Skim Race, 0 elsewhere** — moot once the
  `AdaptiveCanvasScaler` drives it. `R_GameOverPanel.prefab` no longer exists; the `CountdownDisplay`
  override that points into `MiniGameHUD.prefab` is a dead key (no script declares it) and is dropped
  with the rest.

### 9.2 The one real per-mode value is now config

`EventDrivenStatsProvider.statsToTrack` was the single override the audit called genuine per-mode
data. It now resolves **explicit list → `Resources/GameModeStatsProfile` (keyed by
`GameDataSO.GameMode`) → vessel-telemetry discovery**, and the shared prefab ships the list EMPTY.
The asset carries the measured lists: `{Longest Drift, MaxBoost, PrismsDamaged}` for every mode,
Skim Race's five, Joust leading with Jousts Won. Retuning a mode is one asset edit and **no scene
carries a survivor override at all**, which is what lets the re-point clear everything.

### 9.3 The tool — FrogletTools ▸ Game Modes ▸ GameCanvas Unifier

> **Status: kept, half spent.** The migration has run — the fork is deleted, all 15 domain
> scenes are on CORE, and `gamecanvas_unification_report.py --check` passes. Absorb, Re-point
> and Delete fork are dormant until another canvas forks. **Fix prefab** and **Fix scene** stay
> live and are worth re-running: the first enforces the contract and reverts nulled nested
> references, the second reverts redundant scene overrides.

Three steps, no options. Every row has a dry run; read it before the button beside it. The
Prefab Kit's toolbar links here. The offline twin of the status column is
`python3 Tools/Build/gamecanvas_unification_report.py`, whose `--check` is the CI gate for the
end state (CORE at the contract, fork gone, no reference to its guid, every migrated scene on CORE
with no structural edit and no non-default override).

**A nested-instance override that NULLS a reference is reverted by Fix prefab, and the gate
fails on one.** CORE nests other prefabs (`NotificationUI`, the pause menu), and an override on
one of those instances that sets a script-declared reference to nothing is the shape §"Shared
prefabs" already warns about: the nested asset looks correctly wired and the feature quietly does
nothing. The absorbed CORE carried exactly one — `GameToastView.itemPrefab` on the nested
`NotificationUI` — so every toast in every mode logged `[GameToastView] Missing references` and
drew nothing, while `NotificationUI.prefab` itself was fully wired. `RevertNulledNestedReferences`
reverts such overrides (Unity built-ins `m_*` and the runtime-resolved `gameController` are left
alone) and `gamecanvas_unification_report.py --check` names any that remain.

**An absorbed override that moves a nested panel OFF SCREEN fails the gate too.** The rect is
the second thing a nested-instance override can quietly get wrong, and it fails the same way the
nulled reference does — the nested prefab is correct, the feature runs, and nothing is visible.
CORE carried one: `NotificationUI` (the in-game toast feed) anchored **bottom-left at x −314 with
a 489-wide rect**, so the whole panel sat off the left edge while every toast still resolved,
formatted and animated. `offscreen_nested_rects` fails on a point-anchored nested rect that lies
strictly past the edge it is anchored to; a rect flush against that line is left alone, because
that is how a slide-in modal is authored at rest (`SceneTransitionModal`, `ConnectingPanel`).
The fix is to DELETE the rect override so the nested prefab's own placement applies.

 The one in-game canvas is Scale-With-Screen-Size at
**1920x1080** with an `AdaptiveCanvasScaler` on its root driving the width/height match from the
live aspect. It has to be stated because **both prefab assets are authored at 800x450** — every
fork scene was upgraded IN-SCENE by the Canvas Upgrader (its 1,733 identical overrides are the
x2.4 rects plus the 1920x1080 reference), and the ten CORE scenes never were. The first re-point
was run before the absorb, so it dropped Skim Race's overrides onto a CORE still at 800x450 and
the scene came up at 800x450. Two things now make that impossible: **Fix prefab** ends by
enforcing the contract, and **Fix scene** refuses while CORE is not at it.

1. **Fix prefab** — `CORE/GameCanvas.prefab`. While the fork still exists and CORE has not yet
   absorbed the shipped canvas, this is the absorb: the donor scene (Rampage — it holds the
   majority value on every divergent key) is opened *additively*, its canvas instance is unpacked
   ONE level in memory (nested prefabs stay nested), and the result is merged into CORE's loaded
   contents **by hierarchy path** — objects the shipped canvas lacks are deleted (the dry run lists
   each), objects it adds are moved in (`ConnectingPanel`, `BackgroudTop`, the nested
   `NotificationUI.prefab` replacing CORE's plain one), components are paired by exact type,
   **script-swapped in place** where CORE holds the base type (`MiniGameHUD → MultiplayerHUD`,
   `MinigameHUDView → MultiplayerHUDView`, so the fileID and every scene reference survive), or
   added / removed; every value is copied; every reference is remapped (a reference into the CORE
   *asset* resolves to the same path in the contents; a reference to a scene-local object — the
   donor's controller, the Ready button's persistent call — is dropped and listed, those being what
   §5 made self-resolving; a persistent call whose target was dropped is deleted). `statsToTrack`
   is cleared (§9.2). CORE keeps every fileID it had. The donor is closed **without saving**; if
   Unity asks, Don't Save. Then — and on every later run, when it is the whole step — the
   **contract** is applied through the Canvas Upgrader's own passes
   (`CanvasUpgradeProcessor`): if the canvas is still at 800x450 it is upgraded (every rect x2.4,
   reference 1920x1080, `referencePixelsPerUnit` x2.4) rather than merely relabelled; the scale
   mode is forced to Scale-With-Screen-Size; `AdaptiveCanvasScaler` is added; and the canvas's
   direct children are **smart re-anchored** (nearest corner/edge, visual position preserved at
   16:9, stretched / edge-anchored / layout-driven elements left alone) so the layout holds on
   16:10, 21:9, 4:3 and portrait. The upgrader's full per-rect report goes to the console.
2. **Fix scene** — one button per scene, whatever family it is on. A scene **on the fork** is
   re-pointed: the old instance's placement, scene-added objects and components the prefab does
   not now carry, and **every scene-side reference into the canvas** (`countdownTimer` on every
   controller, `volumeUI`, parents of added panels) are recorded by hierarchy path; the instance is
   destroyed; a CORE instance is placed; everything recorded is re-applied and re-wired; the scene
   is saved. No override survives (§9.2 — the one real per-mode value is config), a scene-added
   object the prefab now carries at the same path is dropped (the 15 `ConnectingPanel`s, the
   `BackgroudTop`s), as is one with the same NAME beside a prefab sibling (Joust's second
   `NotificationUI`, the trace of remove-then-re-add). A scene **already on CORE** has every
   override whose value merely repeats the prefab's dropped (`SerializedProperty.DataEquals`
   against `GetCorrespondingObjectFromSource`, so a nested prefab's property is judged against what
   CORE shows) — Unity never prunes those, and the absorb turns a scene's old 1920x1080 / x2.4
   overrides into a wall that says nothing. Settings are identical before and after. Dry-run
   first; then ONE scene; play-test; then **Fix all**. The log ends with the number of non-default
   overrides the instance still carries; the target is 0.
3. **Delete the fork.** Enabled only when nothing references its guid.

Then **Validate & Push** on the panel at the bottom (it stages only what the tool recorded — the
prefab, the scenes, the deleted fork), run `Tools/Build/gamecanvas_unification_report.py --check`,
and `/ship-tools`. The unifier is a permanent tool: with the fork gone, its status column, the
contract check and **Fix scene** are what keep the canvas unified when the next mode is cloned.

**A prefab with a missing script cannot be saved, and CORE had one.** The first in-editor Fix
prefab failed with *"You are trying to save a Prefab with a missing script … 'EndGameStatsPanel'"*:
CORE carries an old end-game view (script guid `1b511b9bcb0249f6b4ab9a9103a0ec66`, no `.cs` in
the project — its fields are `scoreRevealPanel` / `bestScoreText` / `connectingPanel`…) added onto
the nested `EndGameStatsPanel`. Component pairing cannot see it (a missing script has no type and
`GetComponents` returns null for it), so both Fix prefab paths now sweep
`GameObjectUtility.RemoveMonoBehavioursWithMissingScript` over CORE's contents before saving and
list what they removed. The shipped canvas carries none — a missing script never runs — so anything
missing in CORE is dead by definition. `MinigameWildlifeBlitz` and `BenchmarkStressTest` reference
the same dead guid at scene level; that is theirs to clean, not the unifier's.

**Known cost, stated:** the ten CORE-family scenes (the single-player and tool scenes) were
running the 800x450 canvas un-upgraded. After Fix prefab they inherit the 1920x1080 layout the
fifteen game-mode scenes have shipped for months; the handful of rect overrides they carry
(`m_AnchoredPosition` on a few nested elements, in 800-space units) will read slightly off until
someone opens those scenes. They are legacy scenes; the fix was not widened to re-scale their
overrides.

### 9.4 What was decided in code rather than by hand, and why

- **Donor = a scene, never the fork asset** (§9.1).
- **Majority wins on every divergent key**, because the 12-scene family holds the majority value
  on all sixteen; picking Rampage as donor makes that a property of the donor rather than a table.
- **`AdaptiveCanvasScaler` fleet-wide, and 1920x1080 is a CONTRACT the tool enforces, not a
  value it copies** — the four scenes that had the scaler are the oldest and most play-tested;
  `m_MatchWidthOrHeight` stops being a scene value; and a re-point onto a CORE that is not at the
  contract is refused rather than warned about, because the one time it ran it shipped an 800x450
  Skim Race with nothing in the console.
- **Zero survivors** — §9.2. If a survivor prefix is ever entered, it re-applies by path and the
  gate's allow-list must grow with it.
- **Same-named additions are leftovers** — a scene-added `NotificationUI` beside the prefab's own
  is the trace of remove-then-re-add, not a second toast feed anybody designed.
