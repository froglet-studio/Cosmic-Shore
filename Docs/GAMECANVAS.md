# GameCanvas — one canvas, every mode

`GameCanvas.prefab` is meant to be the single in-game UI surface: drop it into a new game-mode
scene and the HUD, scoreboard, pause menu, countdown and connecting panel all work. Today it is
not that, and this document says exactly why, with numbers, plus what was fixed in code and what
is left to do in the editor.

**Read this before touching any game-mode scene's canvas.**

---

## 0. The diagnosis in one paragraph

There are **two forked GameCanvas assets**, and the six scenes that share the newer fork each carry
**~1,770 unapplied overrides — of which 1,734 are byte-identical in all six**. An override parked in
a scene always beats the prefab, so those 1,734 properties are effectively hand-maintained six
times over: editing the prefab changes nothing in any of them. That is the mechanism behind
"I have to go to all 3 scenes to make one common change". Only **20** override keys genuinely differ
between the six scenes — the real per-mode configuration is tiny and always was.

---

## 1. Two prefabs, not one

| | `_Prefabs/CORE/GameCanvas.prefab` | `_Prefabs/GameCanvas-HexRace.prefab` |
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
| `GameCanvas-HexRace` (6) | HexRace (Skim Race), Joust, Crystal Capture (Scurry), AstroLeague, NucleusRush, Rampage |
| `CORE/GameCanvas` (10) | 2v2CoOpVsAI, Maelstrom, DuelForCell, FreestyleMultiplayer, WildlifeBlitz (MP + SP), CellularDuel, BenchmarkStressTest, Recording Studio ×2 |

### Structural delta (root name normalised)

`GameCanvas-HexRace` is a near-perfect **superset**: 101 of 105 GameObjects are shared.

- **+40 in HexRace**: `MiniGameHUD/AllyDomainContainer`, `MiniGameHUD/MultiplayerPlayerScoreCard`,
  `Scoreboard/Buttons/Continue` (+ its text and controller-button image),
  `Scoreboard/MultiplayerView/MultiplayerView/TeamScorecard` ×3 (the per-domain scorecards),
  `Scoreboard/MultiplayerView/BackgroundBottom/Goodies/XPEarned` + `XPIcon`,
  `CrystalDisplayBG` (+ `Icon`, `XPEarnedText`), `XPDisplayBG` (+ `XPEarnedText`).
- **−4 in HexRace**: `MiniGameHUD/NotificationUI` (replaced by the nested prefab — an upgrade, not
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
| MinigameCrystalCaptureMultiplayer_Gameplay | HexRace | **1774** | layout 1249 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameJoust_Gameplay | HexRace | **1771** | layout 1245 · other 457 · script-field 39 · active 15 · button 10 |
| MinigameRampage | HexRace | **1771** | layout 1245 · other 458 · script-field 37 · active 16 · button 10 |
| MinigameAstroLeague | HexRace | **1770** | layout 1245 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameNucleusRush | HexRace | **1770** | layout 1245 · other 458 · script-field 36 · active 16 · button 10 |
| MinigameHexRace | HexRace | **1766** | layout 1247 · other 458 · script-field 37 · active 15 · button 4 |
| BenchmarkStressTest | CORE | 105 | layout 36 · script-field 27 · button 21 · active 12 |
| MinigameWildlifeBlitz (SP) | CORE | 105 | layout 36 · script-field 27 · button 21 · active 12 |
| ArcadeGameMultiplayer2v2CoOpVsAI | CORE | 96 | layout 58 · font-noise 12 · button 11 · script-field 8 |
| MinigameDuelForCellMultiplayer_Gameplay | CORE | 96 | layout 58 · font-noise 12 · button 11 · script-field 8 |
| MinigameCellularDuel | CORE | 85 | layout 53 · button 13 · active 6 · script-field 5 |
| MinigameFreestyleMultiplayer_Gameplay | CORE | 81 | layout 49 · button 11 · script-field 11 · active 5 |
| Maelstrom | CORE | 65 | layout 49 · button 10 (+ 8 removed GameObjects) |
| MinigameWildlifeBlitzMultuplayerCoOp | CORE | 61 | layout 49 · button 10 |
| Recording Studio / MattsRecording Studio | CORE | 27 each | layout 21 · other 4 |

---

## 3. The finding that matters: 1,734 of them are the same everywhere

Comparing override *values* key-by-key across the six HexRace-fork scenes:

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
| `ScoreboardController` (`EventDrivenStatsProvider`) | `statsToTrack` (array + 5 elements) | **Real per-mode data.** HexRace tracks 5 (CleanCrystals, Jousts Won, Longest Drift, MaxBoost, PrismsDamaged); the other five track 3 (Longest Drift, MaxBoost, PrismsDamaged); Joust's list leads with Jousts Won. |
| `MiniGameHUD/ReadyButton` (`Button`) | `m_OnClick…m_TargetAssemblyTypeName` | **Eliminated in code** — see §5. |
| `MiniGameHUD` (`MultiplayerHUD`) | `_eventResponses…m_TargetAssemblyTypeName` ×3 | Per-mode SOAP listener wiring; HexRace omits them entirely. |
| `ScoreboardController` | `multiplayerController`, `hexRaceController` | Scene references — **HexRace only**; the other five leave them null. Auto-resolvable. |
| `NotificationUI` (`RectTransform`) | `m_AnchoredPosition.x/y`, `m_SizeDelta.x/y` | **Accidental drift.** 3 of 6 agree exactly (−314.4, 90); HexRace, Crystal Capture and Joust each wandered. Joust is far out at (−1416, −463). |
| `Scoreboard/Buttons/Continue`, `HomeButton`, `PlayAgainButton` | `m_AnchoredPosition.x/y` | **Accidental drift.** 5 of 6 agree; HexRace alone differs. |

Only the first row is unambiguously per-mode configuration. Everything else is either fixed in code
or is drift to be normalised.

---

## 4. Latent bug: 8 dangling cross-prefab references

`GameCanvas-HexRace.prefab` contains overrides whose `objectReference` points at objects **inside
`CORE/GameCanvas.prefab`** — a different asset:

| Owner (inside GameCanvas-HexRace) | Field | Points into |
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
UnityEvent hookups naming a concrete controller (`HexRaceController`,
`MultiplayerJoustController`, …) never needed to be per-scene. The HUD now finds the scene's
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

## 6. What to do in Unity

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

> Both `GameCanvas.prefab` and `GameCanvas-HexRace.prefab` should be rows in the kit so each fork
> consolidates against its own scenes.

### Step 2 — Normalise the accidental drift (manual, small)

In the prefab (not the scenes), settle one value for each and revert the scene overrides:

- `NotificationUI` RectTransform → adopt the 3-scene majority `(-314.4, 90)`, size `(489.08, 420)`.
- `Scoreboard/Buttons/Continue|HomeButton|PlayAgainButton` → adopt the 5-scene majority
  (`Continue.x = 537.6`, `HomeButton.x = 912`, `PlayAgain.x = 163.2`).

### Step 3 — Retire the fork (the real unification)

`GameCanvas-HexRace` is the superset, so it becomes the single canvas:

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
| Base canvas prefab | `Assets/_Prefabs/CORE/GameCanvas.prefab` |
| Forked canvas prefab | `Assets/_Prefabs/GameCanvas-HexRace.prefab` |
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
