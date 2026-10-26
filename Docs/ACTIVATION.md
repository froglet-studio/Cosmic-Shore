# Player Activation — where every plan lives, what is built, what is left

**Written 2026-10-15** from a sweep of `Docs/**`, `Assets/**`, every open PR and the 45 most
recently active remote branches. Start here before any onboarding / FTUE / tutorial / tooltip /
"new player doesn't know what to do" work, so it lands on the plan that already exists instead of
beside it.

**Activation** = a brand-new player, unaided, reaches their first meaningful match and comes back.
The paid-EA gate measures it as **D1 ≥ 35 % and D7 ≥ 15 %** across two consecutive invite waves
(`Analytics/DATA_ARCHITECTURE.md`).

---

## 1. The one-paragraph answer

The onboarding is **designed and largely built, but no player sees it.** On `bleeding-edge` the
quest graph (`Assets/FTUE/`) is stood down by `DeveloperUnlockGate.DefaultAllUnlocked = true`, which
applies in shipped builds too, so a fresh install gets every lock open and no guidance at all. The
redesign that answers "tooltips" and "a tutor that flies with you" is the **Microgame Training
plan** (`Docs/ModePreview/TRAINING_PLAN.md`, product-owner decisions locked 2026-09-25): a
first-login **spotlight guide**, a forced per-ship **Lesson**, then a curated **Mentor** tip
stream. Its phases 1-4 were written on `claude/exciting-bell-69b398` and were **merged into
`Ys-bleeding-edge` on 2026-10-15**, together with the **ghost demo** (the autopilot flies a Lesson's
button step once, then hands the stick over - TRAINING_PLAN §10.7). None of it has run in the editor.
Playing it once (§6.2), then measuring it, is the shortest path to activation. The first-login phase
opts in to running under the master unlock, so the guide reaches a default build without flipping
the gate; the later quest phases still wait for it.

---

## 2. Every plan, and its status

| Plan | Where | Status |
|---|---|---|
| **Microgame Training** — first-login spotlight guide → Game of the Week microgame → Lesson (forced the first time) → Mentor (curated tips) | `Docs/ModePreview/TRAINING_PLAN.md` | Phases 1-4 and the ghost demo (§10.7) on `Ys-bleeding-edge` since 2026-10-15 (from `claude/exciting-bell-69b398`), **not yet on `bleeding-edge`, never run in the editor.** A playtest on 2026-10-06 found the spotlight drew nothing; the fix (`CanvasRenderer` on the code-built graphics) has not been re-tested (`UNITY_VERIFICATION_CHECKLIST.md`). Phase 5 (non-racing tip lists, relevance-gated leaderboards) and 6 (authoring window, coverage test, `drill_lesson` / `drill_mentor` analytics) not started. §11 lists three open product questions |
| **Quest graph** (the engine the guide runs on) | `Assets/FTUE/`, `Docs/claude/FTUE_DIALOGUE_AI.md`, `Assets/FTUE/QUEST_GRAPH_TOOL.md` | Built and in `Menu_Main`; **stood down by the master developer unlock**, except a phase that opts in (`runsUnderDeveloperUnlock`): on `Ys-bleeding-edge` `MainQuest` P0 is the first-login guide and opts in, and the runner stands down at P1. On `bleeding-edge` P0 is still the freestyle flight school and nothing runs |
| **Quest Track** (the unlock chain) | `Docs/QUEST_TRACK_AND_BREADCRUMB.md` | Live: Scurry → Hex Race → Joust → Maelstrom → Vessel Hangar. Not persisted (`ProgressionBackendGate.CloudEnabled = false`), and the master unlock opens all of it |
| **Breadcrumb / call-to-action** | same doc | **Retired 2026-09-08** (`retire_call_to_action.py --check` guards it). The training branch's spotlight is its replacement; it follows the guided-path rule below |
| **Arcade card teaching** — controls panel, `GameBriefingView` tips | `Docs/ArcadeLaunch/ARCHITECTURE.md` | Controls panel live. **`SO_ArcadeGame.Tips` is authored on 2 of 56 cards** |
| **In-match guidance** — objective arrows, `GoalStack`, idle-hint toasts | `Assets/_Scripts/UI/GameToastSystem/GAME_TOASTS.md` | Live |
| **Activation analytics** | `Docs/Analytics/event-taxonomy.md` §4 | `ftue_step_completed` / `ftue_phase_completed` / `ftue_completed` **landed 2026-10-15** (§5). `session_started`, `game_quit_midway`, `network_disconnected` still unbuilt. PostHog funnel + D1/D7 dashboards **not built** |
| **First-session validation** — watch 3-5 first-timers | `STEAM_EA_INVESTOR_CHECKPOINT.html` C2 / E7 | Not run (nothing to watch while the gate hides the FTUE) |
| **Starter economy** | `Docs/ECONOMY_AND_PRICING.md` | Open: "a new player who loses their first several games earns zero, sees a 4000-crystal price tag, and has no visible path to it" |

**Not planned anywhere:** a generic hover tooltip component, or an AI companion that flies beside a
new player in a real match. The answers that exist are the spotlight (a guided "press this next"), the
Mentor (TRAINING_PLAN D3: curated text, "a friend flying alongside") and the **ghost demo** (the
preview's own autopilot shows a step before the player flies it). The only AI wingman is the Toy
System's `ReleaseCompanion` (`Docs/HomeHub/ARCHITECTURE.md`).

---

## 3. Rules any activation work keeps

- **Guide, don't carry** (LOCKED on the training branch, `Docs/HomeHub/ARCHITECTURE.md` §8 there):
  to send a player somewhere, spotlight the one control to press next, dim and disable the rest,
  never take Settings away, and let the player press it themselves. The first-login *railroad* that
  opened screens for the player was built and deleted for exactly this reason.
- **C1-C4** of `QUEST_TRACK_AND_BREADCRUMB.md`: no XP; one guidance channel; hints bloom and wither,
  never pop; unlocks are persistent and monotonic.
- **Every player-facing line is an authored field**, never a string in code.
- **The master developer unlock is a product decision.** Do not flip `DefaultAllUnlocked` as a side
  effect of other work. The training branch's per-phase opt-in is how a guide runs without it.
- **Build on the quest graph** (`Assets/FTUE/`), never on the deleted `TutorialFlowController` family.
  Open PR #406 is built on those deleted classes and is dead.

---

## 4. Who is on what (avoid collisions)

**Shombith leads activation; Caleb heads FTUE with him; no one else is on it** (Shombith, 2026-10-26). Every
activation task on `Docs/TEAM_TASKS.md` names them; the rows below are the branches the work touches, not owners.

| Work | Branch / PR | Collides on |
|---|---|---|
| Microgame Training + first-login guide | merged into `Ys-bleeding-edge` 2026-10-15 from `claude/exciting-bell-69b398` (no PR of its own; last commit 2026-10-13) | `QuestGraphRunner`, quest nodes, `Menu_Main.unity`, `ArcadeExploreView`, `ScreenSwitcher`, `ModalWindowManager`, `MenuHubButton`, `GateRaceController` (the course-source refactor) |
| Menu Redesign B — every Menu_Main screen re-styled (Shombith owns Menu_Main UI, per the team roster) | `claude/new-session-76hnyc` (no PR) | `Menu_Main.unity` (heavy), `QuestTrackView`, `ProfileScreen`, `ArcadeExploreView`, `ArcadeGameConfigureModal`; adds a `tooltip` colour token to `UIThemeSO` |
| Regatta AI seats, racing AI | PR #1098 (`Ys-bleeding-edge`) | `ServerPlayerVesselInitializerWithAI`, `ArcadeGameConfigureModal`; the AI seating any new-player bot would use |
| Quest dialogue copy (C9) | Caleb, Garrett, Shombith | `QuestDialogueNode` content |
| Funnel dashboards (C3) | Ian | PostHog |

**What the merge cost (2026-10-15)**, for whoever brings `Ys-bleeding-edge` into `bleeding-edge`: three
text conflicts (`CLAUDE.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Tools/Build/check_generated_assets.py`,
all keep-both); one compile break (Thresher Time was written against the old `GateRaceController` API -
ported to a `ThresherTimeCourseSource` like Redline's); and eight generator `--check`s that went red on
a tree whose numbers had not moved (the course-source refactor moved each race's `laps` default into a
const, which `arcade_mode_lib.cs_field_default` now follows; Warpline's circuit check now reads its
course source; the Elemental Converter counted every Menu_Main addition as its own). A text-clean merge
of `Menu_Main.unity` still has to be opened in the editor before anyone trusts it.

---

## 5. Instrumentation (landed 2026-10-15)

The quest graph raises `FTUEEventManager.OnQuestNodeCompleted` at the moment each node is persisted.
`AnalyticsServiceFacade` records it and the existing phase and quest completion events:

| Event | Parameters |
|---|---|
| `ftue_step_completed` | `quest_id`, `phase_index` (int), `step_id` (the node's stable id), `step_name` (its display name), `seconds_since_launch` (int) |
| `ftue_phase_completed` | `quest_id`, `phase_index` (int), `seconds_since_launch` (int) |
| `ftue_completed` | `quest_id`, `seconds_since_launch` (int) |

**Declare all three, and every parameter, in the UGS dashboard Event Manager**, or UGS discards them
silently (PostHog receives them either way). A debug single-phase run (`debugPhaseOverride`) persists
nothing and raises nothing. On `Ys-bleeding-edge` the first-login phase runs under the master unlock,
so its steps and its `ftue_phase_completed` (phase 0) fire in a default build - the guide's funnel is
measurable now. The later phases wait for the gate, and stay at zero until it flips.

---

## 6. The path to activation, in order

**Every step below that needs a person is a task on [`TEAM_TASKS.md`](TEAM_TASKS.md)** (`python3
Tools/Build/team_tasks.py list --area Activation`). Every task there names Shombith and Caleb; take one before starting it.

1. ~~**Land the training branch.**~~ Merged into `Ys-bleeding-edge` 2026-10-15 (§4). It reaches
   `bleeding-edge` with this branch's next PR.
2. **Play it once in the editor**: reset quest progress, run *FrogletTools > FTUE > Reset
   Microgame Lessons (testing)*, enter `Menu_Main`, follow the spotlight into the Game of the Week,
   watch the ghost demo on the Time and drift steps, finish the Lesson, and stay for the Mentor.
   TRAINING_PLAN §10.6 and §10.7 list what only the editor can settle; the steps are in
   `UNITY_VERIFICATION_CHECKLIST.md` under "Ghost demo + first-login guide".
3. **Declare the §5 events in UGS and build the funnel**: `game_first_launched → menu_ready →
   ftue_step_completed (guide) → ftue_phase_completed (P0) → game_started → game_completed`, split by
   `invite_wave`.
4. **Watch three to five first-timers** (C2 / E7) with the gate at its shipped default.
5. **Decide the rest of the Main Quest** (TRAINING_PLAN §11.1): phases 1-5 still assume the
   freestyle flight school ended P0.
6. **Fill the content gaps**: Mentor tip lists for the non-racing families (phase 5), `Tips` on the
   54 cards that have none (the mode generators own card content, so add them there), and quest
   dialogue (C9).
7. **Then decide the gate**: flip `DefaultAllUnlocked` and `ProgressionBackendGate.CloudEnabled` once
   the chain is signed off (`STEAM_RELEASE_TASKS.md` DoD #4).

---

## 7. Stale records found by this sweep

- ~~`MENU_PROGRESSION_AND_IAP.md` (chain table) and `QUEST_TRACK_AND_BREADCRUMB.md` (authored-content
  table) still list Crystal Capture / Hex Race / Joust / PartyGame.~~ Corrected 2026-10-17: both tables now
  match `GameModeQuestList.asset` (Scurry → Hex Race → Joust → Maelstrom → Vessel Hangar).
- `Docs/QuestMap/QUEST_MAP.html` still draws the retired breadcrumb nodes. It now carries an "out of date"
  banner; redrawing it waits on the phases decision (T-20261015-fb65).
- `Analytics/ANALYTICS_HANDOFF.md` §8 still calls the quest branch unmerged. Its two reconcile items
  (the `Version` vs `SchemaVersion` field name, and the second progression key `QUEST_GRAPH_PROGRESS`)
  have no record of being done.
- The asset `GameModeQuest_SkimRace` unlocks **Hex Race** (mode 33).
- `QuestRewardRevealView` and the whole legacy `DialogueManager` stack are referenced by no scene.
