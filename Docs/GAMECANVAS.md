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

### `GameModeUIConfigSO` + `GameModeSceneConfig` — where per-mode differences live now

The remaining per-mode values needed a home that is **not** a prefab override. That home is a
ScriptableObject:

```
GameModeUIConfigSO   (one asset per mode)          <- the data
   ^
   | referenced by
GameModeSceneConfig  (one GameObject in the scene) <- how the scene points at it
   ^
   | resolved at runtime by
EventDrivenStatsProvider  (end-game stat list)
MultiplayerHUD            (per-domain vs per-player score layout)
```

`GameModeSceneConfig` is a plain component holding one SO reference. It is deliberately **not**
part of GameCanvas, so pointing a scene at its config never creates an override on the shared
prefab. Consumers call `GameModeSceneConfig.Resolve()` instead of holding a serialized field —
that is what keeps new inspector references off the canvas.

**Every field is opt-in.** An empty `EndGameStats` falls through to the scene's own list and then
to vessel-telemetry discovery, and `ScoreLayout = Inherit` derives the layout from the controller
type — so most modes need no config asset at all, and a scene without one still behaves correctly.
This is what makes it safe to migrate one mode at a time.

| Field | Replaces | Neutral value |
|---|---|---|
| `EndGameStats` | `EventDrivenStatsProvider.statsToTrack` overridden per scene | empty → scene list, then vessel-telemetry discovery |
| `ScoreLayout` | the fact that the *only* way to get per-player cards was to ship a canvas without domain wiring | `Inherit` → per-domain if the scene's controller is a `MultiplayerDomainGamesController`, else per-player |

`ScoreLayout` is the field that actually unblocks the merge. Today the two canvas forks differ in
whether the domain containers exist at all, and `MultiplayerHUD` picked its layout from whether they
happened to be wired — shipping a canvas *without* the wiring was the only way to get per-player
cards, which is precisely why a second prefab had to exist. A single unified canvas always carries
the wiring, so that signal is gone and the choice moves to the controller type, overridable per
mode by this field.

**Net effect:** a brand-new game-mode scene can drop GameCanvas in, add one `GameModeSceneConfig`
object, and the Ready button, Play Again, stat list and score layout all work with no inspector
wiring on the canvas itself.

---

## 6. What to do in Unity

**Decision: `CORE/GameCanvas.prefab` is the survivor. `GameCanvas-HexRace.prefab` gets deleted.**

This is the cheaper direction, and by a wide margin. The base prefab's GUID is already referenced
by **10 scenes**; the fork by **6**. Keeping the base means only those 6 scenes need their canvas
re-instantiated — the other 10 keep working untouched. (Promoting the fork instead would have
meant redoing 10.) The fork's extra content moves *into* the base, so nothing is lost.

### Step 0 — what the tooling does and does not do

**FrogletTools ▸ Game Modes ▸ Game Mode Prefab Kit ▸ Validate** is a *read-only report*: prefab
health, presence in the open scene, and which other scenes carry unapplied overrides. It has an
**Ignore** button for scenes that are meant to differ. It does not merge prefabs and does not apply
overrides. Use it to check your work after each step, not to do the work.

**Maelstrom is excluded by default** and should stay that way. It is the tournament **hub**, not a
playable mode — it chains the real modes as rounds, so its canvas deliberately strips the gameplay
HUD (8 removed GameObjects) and adds `Intro Panel` / `Summary Panel`. Correct, not drift. Leave it
completely alone until every playable mode is done, then re-check it last.

---

### Step 1 — bring the base prefab up to the superset

Open `Assets/_Prefabs/CORE/GameCanvas.prefab` and, copying from `GameCanvas-HexRace.prefab`:

1. **Swap the HUD scripts.** On the `MiniGameHUD` object, change the Script field:
   - `MiniGameHUD` → `MultiplayerHUD`
   - `MiniGameHUDView` → `MultiplayerHUDView`

   Both are subclasses, so every inherited field keeps its value and the component fileIDs do not
   change — existing scene overrides that target these components survive. Verify the inherited
   references (view, scoreboard, connectingPanel, event channels) are still populated afterwards.

2. **Add the domain-score wiring** under `MiniGameHUD`: `AllyDomainContainer`,
   `MultiplayerPlayerScoreCard`, and assign them plus the `DomainScorePanel` prefab on
   `MultiplayerHUDView` (ally container / opposing container / panel prefab).

3. **Add the end-game scoreboard additions**: the three `TeamScorecard` objects under
   `Scoreboard/MultiplayerView/MultiplayerView`, `Scoreboard/Buttons/Continue` (+ its text and
   controller-button image), and the `Goodies/XPEarned` + `XPIcon` under
   `Scoreboard/MultiplayerView/BackgroundBottom`.

4. **Add `CrystalDisplayBG`** (+ `Icon`, `XPEarnedText`) and **`XPDisplayBG`** (+ `XPEarnedText`).

5. **Add `EventDrivenStatsProvider`** to the `ScoreboardController` object. Leave its
   `statsToTrack` list **empty** — the per-mode list comes from the config asset now.

6. **Replace the plain `NotificationUI` GameObject** with the nested `NotificationUI.prefab` the
   fork uses.

7. Decide on `Scoreboard/MultiplayerView/MultiplayerScores/PlayerFour`: keep it if the legacy
   4-player row is still used anywhere, delete it if the TeamScorecards have replaced it.

8. **Do not copy the 8 dangling references** listed in §4. When you add `GameOverPanel` /
   `EndGameStatsPanel` content, point `animatedRoot`, `bestScoreText`, `highScoreText`,
   `continueButton`, `endGameStatsPanel`, the Home button's `onClick` target, and
   `EndGameStatsPanel.view` / `.connectingPanel` at objects **inside this same prefab**.

Save. At this point the 10 base scenes still work — the additions are inert until a mode asks for
them.

---

### Step 2 — author the per-mode config assets

Create ▸ ScriptableObjects ▸ Game Modes ▸ Game Mode UI Config, one asset per mode. Suggested home:
`Assets/_SO_Assets/Game Modes/`.

You only need to fill in what differs from the automatic default:

| Mode | `ScoreLayout` | `EndGameStats` |
|---|---|---|
| HexRace | leave `Inherit` | CleanCrystals, Jousts Won, Longest Drift, MaxBoost, PrismsDamaged |
| Joust | leave `Inherit` | Jousts Won, MaxBoost, PrismsDamaged |
| Crystal Capture | leave `Inherit` | Longest Drift, MaxBoost, PrismsDamaged |
| AstroLeague | leave `Inherit` | Longest Drift, MaxBoost, PrismsDamaged |
| NucleusRush | leave `Inherit` | Longest Drift, MaxBoost, PrismsDamaged |
| Rampage | leave `Inherit` | Longest Drift, MaxBoost, PrismsDamaged |
| **Multiplayer Cellular Duel** | **`PerPlayer`** ⚠ | leave empty |
| Wildlife Blitz, 2v2 CoOp, Freestyle MP, single-player modes | leave `Inherit` | leave empty |

`Inherit` resolves to **per-domain when the scene's controller derives from
`MultiplayerDomainGamesController`**, per-player otherwise — right for every mode except
**Multiplayer Cellular Duel**, which is a domain controller that currently ships the per-player
cards. Set that one explicitly, or accept the flip deliberately.

The stat lists above are transcribed from the values currently sitting as scene overrides (§3), so
this is copying, not redesigning.

---

### Step 3 — the 6 fork scenes, one at a time

For `MinigameHexRace`, then Joust, Crystal Capture, AstroLeague, NucleusRush, Rampage:

1. Note the scene's own wiring first: the `ReadyButton` onClick target, `ScoreboardController`'s
   controller reference, and `statsToTrack`. You will not need to re-create any of them.
2. Delete the `GameCanvas-HexRace` instance from the scene.
3. Drag in `CORE/GameCanvas.prefab`. **A GUID swap in YAML does not work** — the two prefabs have
   different fileIDs, so every override would dangle.
4. Add an empty GameObject, add **Game Mode Scene Config**, assign that mode's config asset.
5. Re-apply only the layout values you actually want (position/size). Leave everything else at
   prefab defaults.
6. Play-test: countdown, Ready button, live score panels, end-game scoreboard, Play Again.
7. Run Validate on the GameCanvas row. What remains should be layout you chose on purpose.

Do **not** batch these. One scene, one play-test, one commit.

---

### Step 4 — delete the fork

Once no scene references `abd30ad4cfca9ae4a8aecfde9f650cf3`:

1. Delete `Assets/_Prefabs/GameCanvas-HexRace.prefab` (and its `.meta`).
2. Remove its entry from the Prefab Kit list (**Edit list** in the tool).
3. Re-check Maelstrom last — it should still open, run its rounds, and show Intro/Summary.

---

### What you no longer have to wire, ever again

| Was a per-scene override | Now |
|---|---|
| `ReadyButton.onClick` → concrete controller | `MiniGameHUD.EnsureReadyButtonWiring()` finds the controller |
| `Scoreboard.gameController` | `Scoreboard.ResolveGameController()` |
| `EventDrivenStatsProvider.statsToTrack` | `GameModeUIConfigSO.EndGameStats` |
| A whole second prefab, just to get per-player cards | `GameModeUIConfigSO.ScoreLayout` |

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
| Per-mode config asset | `Assets/_Scripts/ScriptableObjects/GameModeUIConfigSO.cs` |
| Scene -> config link | `Assets/_Scripts/Controller/Arcade/GameModeSceneConfig.cs` |
| Kit config asset | `Assets/Resources/GameModePrefabKit.asset` |
| Kit window | `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs` |
| Drift scanner (read) | `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs` |
| Validation rules | `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs` |
