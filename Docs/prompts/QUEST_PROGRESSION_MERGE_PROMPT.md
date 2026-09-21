# Prompt — merge the quest/progression branch into `bleeding-edge`

Paste everything below into a fresh session.

---

Merge **`claude/ftue-editor-tool-69acq5`** into `bleeding-edge`. It closes the finding that three
consecutive readiness audits have called the project's single blocking item, and it is gated behind
a master unlock that is **ON by default**, so merging it cannot lock anybody — team or player — out
of anything.

Everything below was measured on 2026-09-21 against branch head `63dc3a3d5` and `bleeding-edge`
`0eef7c5b0`, merge-base `9116237879`. **Re-verify before you start** — the branch is 41 commits
behind and both sides move.

---

## What this branch actually fixes

`GameModeProgressionService` has been present in the codebase and **instantiated by nothing** since
at least Revision 3 of the readiness series. Its only host was
`_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab`, whose guid is referenced by
zero scenes and zero prefabs, and its serialized block carried no `progressionConfig` — so the
authored unlock list was dead and `ArcadeExploreView` failed open, rendering every mode unlocked.

On this branch `Assets/_Scenes/Menu_Main.unity` carries a **live, enabled GameObject named
`GameModeProgressionService`** with all three fields wired:

| Field | Value |
|---|---|
| `questList` | guid `5eee61facaac4bb46b9e9892512f74cb` |
| `progressionConfig` | guid `21825e67a6164a0da2941f28fa24fabc` → `_SO_Assets/GameModeQuest/ProgressionConfig.asset` |
| `gameData` | guid `b35f33752bb10a44cb5033b5670f50aa` |

`m_IsActive: 1`, `m_Enabled: 1`, and the service `DontDestroyOnLoad`s itself in `Awake`. **That is
the fix.** Confirm all of it survives your merge — it is the whole point of the exercise.

---

## The two gates, and what they actually mean

This is the part to read twice. The branch adds **two independent kill-switches**, and only one of
them is the "master toggle" people mean when they talk about this branch.

### `DeveloperUnlockGate` — the master unlock (ON by default)

`Assets/_Scripts/System/Progression/DeveloperUnlockGate.cs`, `public const bool
DefaultAllUnlocked = true;`, overridden per-machine through `PlayerPrefs["DEV_ALL_UNLOCKED"]`, flipped
from **Froglet Toolbox** (`LogControlWindow.cs`). It is **not** `#if UNITY_EDITOR` and **not** under an
`Editor/` folder, so *it is live in release builds*.

It has **six** choke points, not the five the commit message lists. Verify each still routes through
it after the merge:

| Choke point | Line |
|---|---|
| `SO_Vessel.IsLocked` | `SO_Vessel.cs:74` |
| `GameModeProgressionService.IsGameModeUnlocked` | `:135` |
| `GameModeProgressionService.GetMaxUnlockedIntensity` | `:420` |
| `GameModeProgressionService.IsVesselHangarUnlocked` | `:184` |
| `QuestArcadeConstraints.Active` | `QuestArcadeConstraints.cs:56` |
| **`QuestGraphRunner.TryStart`** | `QuestGraphRunner.cs:137` |

That sixth one is the consequence nobody states out loud: **while the gate is ON the quest graph
does not run at all.** The file says so itself — *"THE FTUE CAN ONLY BE PLAY-TESTED WITH THE GATE
OFF."* So merging this branch does **not** put a working FTUE in front of anyone by default; it puts
a correctly-wired, fully-unlocked game in front of everyone, with the FTUE available to whoever
flips the switch. That is the right default for now. Just do not report it as "the FTUE shipped".

### `ProgressionBackendGate` — the cloud kill-switch (OFF by default)

`ProgressionBackendGate.CloudEnabled = false`, read by 7 files. While false:

* quest progress lives **only** in a local PlayerPrefs mirror — no UGS reads or writes;
* `GameModeProgressionService` neither loads nor saves the `GAME_MODE_PROGRESSION` cloud record, so
  **every play session starts from a fresh progression state**;
* vessel unlock changes are **not** persisted to the hangar cloud record.

**This is the one thing that must not be mis-reported.** Definition of Done #4 is *"A fresh account
completes the quest chain, with unlocks and progression persisting through Cloud Save."* With this
gate false, progression **does not persist**. So the merge **moves** DoD #4's blocker from *"the
service is instantiated by nothing"* to *"persistence is deliberately switched off"* — it does not
clear it. Say that plainly in the PR body. Flipping it to true is a separate, later change with its
own test pass, and it is the remaining work on DoD #4.

---

## What the merge costs — measured, so you do not rediscover it

**There are no merge conflicts.** `git merge-tree --write-tree origin/bleeding-edge <branch>` exits
0. Five files are touched on both sides and all auto-merge: `Assets/_Scenes/Menu_Main.unity`,
`CLAUDE.md`, `Tools/Build/check_using_directives.py`, `.claude/skills/ship/SKILL.md`,
`.claude/skills/ship-deep/SKILL.md`.

**`Menu_Main.unity` auto-merging textually is not a guarantee it is coherent Unity YAML.** Both
sides add objects to the same scene (branch +305/−6, upstream +319/−18). This is the one file where
a silent semantic merge can break the main menu, and it is also the file carrying the fix. Open it.

The branch is **274 files, +14,998/−2,407**, and it deletes ~40 real files — the entire legacy
adapter/step tutorial stack plus the older `Quest`/`QuestSystem`/`UserJourneySystem` prototype.
**`CLAUDE.md`'s FTUE section is already rewritten on the branch** to describe the quest graph and to
say the 25 deleted types must not come back, so there is no documentation debt to pay here. Confirm
that survives the merge rather than re-deriving it.

### The five riskiest files, none of which is about progression

1. `System/Progression/ProgressionBackendGate.cs` — one line, disables progression cloud sync
   project-wide (above).
2. `System/Progression/DeveloperUnlockGate.cs` — default-true master unlock, live in release builds
   (above).
3. `_Scenes/Menu_Main.unity` — the semantic-merge risk (above).
4. `Controller/Arcade/TurnMonitorController.cs` — wraps `gameData.InvokeGameTurnConditionsMet()` in
   a try/catch. **This changes end-of-turn failure semantics for every arcade mode** and has nothing
   to do with FTUE. Read it and decide deliberately whether it rides along.
5. `_SO_Assets/Cell Configs/Blob Cell/{Blob Cell Spawn Profile, Blob Fauna Config Data}.asset` —
   ecology tuning edits, 4 lines each, on an FTUE branch. **Ecology is a LOCKED system**
   (`Docs/ECOSYSTEM.md`, the `/ecology` skill). Either justify them against the invariants or drop
   them. Also `R_VesselActionHandler.cs` (+23, a new `_suppressedInputs` set) touches every vessel's
   input path.

Also riding along: 9 new control-icon PNGs and 2 UI PNGs. Harmless, but they are not progression.

---

## What to do

1. **Resync first.** Merge current `bleeding-edge` into the branch, not the other way round, so the
   41-commit gap is resolved on the branch where it can be tested. Re-run `git merge-tree` after.
2. **Open `Menu_Main.unity` in the editor** and confirm: the `GameModeProgressionService` GameObject
   is present, active, and still carries all three references; nothing upstream added in the same
   window was lost; the scene has no missing scripts.
3. **Decide on the two hitchhikers** — `TurnMonitorController`'s try/catch and the Blob Cell ecology
   assets. Keep them with a stated reason or revert them on the branch. Do not merge them silently.
4. **Run the gates.** All four static gates exist on the branch; `check_enum_member_references.py`
   and `check_switch_label_collisions.py` were widened there to scan `Assets/FTUE` as well as
   `Assets/_Scripts` (twelve stale `GameModes` references in FTUE had survived an upstream rename
   and reached a human as `CS0117`). `check_using_directives.py` is touched on both sides — re-run
   it after merging. Also run `check_tmp_glyph_coverage.py`, `check_conditional_compilation.py`,
   `check_console_logging.py`, `check_credits_manifest.py`.
5. **Play it twice, once per gate state.** With the gate ON (default): every mode and vessel open,
   no quest graph, main menu healthy. With `DEV_ALL_UNLOCKED` OFF: the quest chain starts, the
   arcade shows locks, `ProgressionConfig.asset`'s 18 always-unlocked modes are open and the ten
   gated ones (`MultiplayerFreestyle`, `OnlineDuelForTheCell`, `CoOpWildlifeBlitz`, `SkimRace`,
   `Joust`, `Scurry`, `Maelstrom`, `AstroLeague`, `BroodRush`, `ScarabScramble`) are not, and
   intensity is capped at `defaultMaxIntensity: 3`.
6. **Log a QA result.** This is the first merge in months that changes what a new player sees. Put
   one entry in `Docs/QA/RESULTS/` — the project has held exactly one verdict since 14 August, and
   `Docs/STEAM_RELEASE_TASKS.md` R2 cannot absorb its 68-item backlog until result files exist.
7. **Open the PR** with a body that states, in these words or better: the progression service is now
   instantiated and configured; the master unlock ships ON so nothing is locked; the quest graph
   does not run while it is on; and progression does **not** persist to cloud because
   `ProgressionBackendGate.CloudEnabled` is false, so DoD #4 is moved, not closed.

---

## Constraints

1. **Do not flip either gate in this change.** `DefaultAllUnlocked` stays `true` and
   `CloudEnabled` stays `false`. Each flip is its own change with its own test pass.
2. **Do not reintroduce the deleted tutorial stack** — the adapters, step handlers, `TutorialStep`,
   `TutorialSequenceSet`, `FTUEProgress`, `Quest`, `QuestSystem`, `UserJourneySystem`,
   `SO_QuestChain`. `CLAUDE.md` on the branch says so; keep it saying so.
3. **Do not let the ecology edits through unexamined** (constraint 3 of `/ecology`: state which
   invariants a change touches and confirm none is violated).
4. **Do not widen the scope.** No FTUE authoring, no quest content, no cloud work. This is a merge.

---

## Definition of done

1. `claude/ftue-editor-tool-69acq5` is merged into `bleeding-edge` with no conflicts and no lost
   upstream work.
2. `Menu_Main.unity` opens clean and `GameModeProgressionService` is live with all three references.
3. Both gate states have been played, and the ten gated modes really do lock with the gate off.
4. Every standing gate is green, including the two widened ones.
5. One QA result file exists in `Docs/QA/RESULTS/` for this merge.
6. `Docs/STEAM_RELEASE_TASKS.md` records that the progression-instantiation blocker is closed and
   that DoD #4 now waits on `ProgressionBackendGate.CloudEnabled`, not on the service existing.
7. The PR body does not claim the FTUE has shipped.
