# QA session results

> **This file keeps this name from now until it merges — there is nothing to rename.**
> Nothing here is published until you run the Submit button:
>
> ```
> python3 Tools/QA/submit.py
> ```
>
> `Result` must be exactly `PASS` / `FAIL` / `PARTIAL` / `BLOCKED` / `SKIP`. Never
> delete the `qa-results-table` markers. Full guide: `Docs/QA/README.md`.

## Session

| Field | Value |
|---|---|
| Tester | Claude (session) — **static verification only, no editor** |
| Date | 2026-09-21 |
| Branch | `claude/ecstatic-goodall-r2tefd` (merging `claude/ftue-editor-tool-69acq5`) |
| Commit | `adadb2280` |
| Unity version | n/a — not run |
| Platform(s) | *none — no Unity Editor and no dotnet in this container* |
| Submitted | *no — there is nothing to publish; see below* |

## Results

**The table is deliberately EMPTY. Nothing here was run in an editor**, so there is no
verdict to publish. Rows below this line would be fabricated, and a published verdict is
frozen — so the two real tests this merge creates are written up under *Anything else*
as proposed backlog items instead, which is the sanctioned channel for new items.

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|

<!-- /qa-results-table -->

---

## What WAS verified, and how

All of this is static — asset YAML, source reads and the standing gates. None of it is
evidence that the game runs.

**The fix itself.** `Assets/_Scenes/Menu_Main.unity` carries a GameObject named
`GameModeProgressionService` at `fileID 501288519`, `m_IsActive: 1`, whose MonoBehaviour
`501288520` is `m_Enabled: 1` and carries all three references. Every guid resolves to
exactly one owning `.meta`:

| Field | guid | Resolves to |
|---|---|---|
| *(script)* | `541692fb…` | `_Scripts/System/Progression/GameModeProgressionService.cs` |
| `questList` | `5eee61fa…` | `_SO_Assets/GameModeQuest/GameModeQuestList.asset` |
| `progressionConfig` | `21825e67…` | `_SO_Assets/GameModeQuest/ProgressionConfig.asset` |
| `gameData` | `b35f3375…` | `_SO_Assets/Game Data/Runtime GameData.asset` |

Its Transform `501288521` has `m_Father: {fileID: 0}` and appears in `SceneRoots.m_Roots`,
so it is a real scene root and not an orphan.

**The scene survived the merge.** 6,994 objects, **0 duplicate anchors**, 0 conflict
markers, exactly 1 `SceneRoots` block. 25 `m_Script` guids do not resolve — but the
**identical 25** do not resolve on `bleeding-edge` either (they are package scripts, and
`Library/PackageCache` is absent from a fresh clone). The branch introduces **zero** new
unresolved scripts and adds 4 resolvable ones. This is a differential result, not an
absolute one: it does **not** prove the scene has no missing scripts, only that the merge
adds none.

**Gates.** All green: `check_enum_member_references` (182 enums / 1,990 files),
`check_switch_label_collisions` (179 / 1,990), `check_using_directives`,
`check_conditional_compilation` (2,000 files), `check_console_logging` (0 problems),
`check_credits_manifest`, `check_gamelist_scenes`, `check_fauna_replication_seam`,
`check_elemental_floats`, `check_self_referential_locals`, `check_shield_collider_claims`,
`check_vessel_class_icons`. The first two are confirmed to carry
`SCAN_ROOTS = ["Assets/_Scripts", "Assets/FTUE"]`, i.e. the widening is real.

⚠ **`Tools/Build/check_tmp_glyph_coverage.py` does not exist** — it was named in the merge
instructions but is not in the tree. The *rule* was checked by hand instead: every
displayed-text field across the 29 changed asset/prefab/scene files is inside the shipped
`ALDRICH-REGULAR SDF` coverage (ASCII 32–126 + nbsp + ellipsis). No tofu.

**Not verified at all:** anything requiring the editor or a compiler — that the project
compiles, that Menu_Main opens, that either gate state behaves as designed.

---

## Anything else

### Two proposed backlog items (this merge creates new testable surface)

There is no existing backlog item covering unlock gating, because until this merge the
service was instantiated by nothing. Both need running before this surface can be called
tested. Suggested IDs:

**`QA-PROGRESSION-GATE-ON` — the shipped default: nothing is locked.**
1. Fresh launch into `Menu_Main`. Do *not* touch the Froglet Toolbox.
2. Console shows exactly one `[DeveloperUnlockGate] ALL ENTITLEMENTS OPEN …` warning.
3. Arcade: every card selectable, no locks. Hangar: open. Intensity: all four.
4. No quest dialogue, no instruction panel, no arcade funnel — the graph is stood down.
5. **PASS** = a fully open game and a healthy main menu. **FAIL** = any lock, any quest
   UI, or a broken/missing main menu.

**`QA-PROGRESSION-GATE-OFF` — real progression.**
1. **FrogletTools ▸ Toolbox** → Quest Debug (or Vessel Unlock) → turn the master unlock
   **off**. It persists in `PlayerPrefs["DEV_ALL_UNLOCKED"]`.
2. The quest chain starts (5 quests: Scurry → Hex Race → Joust → Maelstrom → Vessel Hangar).
3. The arcade shows locks; the **18** modes in `ProgressionConfig.alwaysUnlockedModes`
   (2, 39, 40, 41, 42, 44, 45, 46, 48–57) stay open.
4. Intensity is capped at `defaultMaxIntensity: 3`, except Maelstrom (36), which is in
   `fullIntensityModes` and gets all 4.
5. **PASS** = locks appear and the chain runs. **FAIL** = no locks, or the always-unlocked
   18 are locked.

> ⚠ **Correction to the merge instructions, so nobody files a false bug.** Those
> instructions list **Scurry (35) among the modes that should be locked with the gate
> off. It should NOT be.** `ProgressionConfig.firstQuestAlwaysUnlocked` is `1` and Scurry
> is `Quests[0]`, so `IsGameModeUnlocked` returns true for it via the "first game is free"
> branch. A tester expecting Scurry to lock will report correct behaviour as a defect.
> The other nine named (28, 29, 32, 33, 34, 36, 37, 38, 43) do lock.

### Things that looked worth saying

- **The FTUE has not shipped, and the default hides it.** `DeveloperUnlockGate` is the
  sixth choke point at `QuestGraphRunner.TryStart:137`, so with the gate on (the shipped
  default) the quest graph **does not run at all** — no dialogue, no rewards. The FTUE can
  only be play-tested with the gate off. That is the design, not a defect, but it means
  merging this does not put an FTUE in front of anyone.
- **Progression does not persist.** `ProgressionBackendGate.CloudEnabled` is `false`, so
  `GameModeProgressionService` neither loads nor saves the `GAME_MODE_PROGRESSION` cloud
  record and **every session starts from a fresh progression state**. Any gate-off test is
  therefore a single-session test: quit and the unlocks are gone. That is deliberate, and
  it is what DoD #4 now waits on.
