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
stream. Its phases 1-4 are written **on the unmerged branch `claude/exciting-bell-69b398`**, which has
no PR and has never been run in the editor. Landing that branch, playing it once, and measuring it is
the shortest path to activation.

---

## 2. Every plan, and its status

| Plan | Where | Status |
|---|---|---|
| **Microgame Training** — first-login spotlight guide → Game of the Week microgame → Lesson (forced the first time) → Mentor (curated tips) | `Docs/ModePreview/TRAINING_PLAN.md` **on `claude/exciting-bell-69b398`** | Phases 1-4 written, **unmerged, no PR, never run in the editor.** Phase 5 (non-racing tip lists, relevance-gated leaderboards) and 6 (authoring window, coverage test, `drill_lesson` / `drill_mentor` analytics) not started. §11 lists three open product questions |
| **Quest graph** (the engine the guide runs on) | `Assets/FTUE/`, `Docs/claude/FTUE_DIALOGUE_AI.md`, `Assets/FTUE/QUEST_GRAPH_TOOL.md` | Built and in `Menu_Main`; **stood down by the master developer unlock**. `MainQuest` P0 is still the freestyle flight school on `bleeding-edge`; the training branch replaces it with the first-login guide and lets that one phase run under the gate (`runsUnderDeveloperUnlock`) |
| **Quest Track** (the unlock chain) | `Docs/QUEST_TRACK_AND_BREADCRUMB.md` | Live: Scurry → Hex Race → Joust → Maelstrom → Vessel Hangar. Not persisted (`ProgressionBackendGate.CloudEnabled = false`), and the master unlock opens all of it |
| **Breadcrumb / call-to-action** | same doc | **Retired 2026-09-08** (`retire_call_to_action.py --check` guards it). The training branch's spotlight is its replacement; it follows the guided-path rule below |
| **Arcade card teaching** — controls panel, `GameBriefingView` tips | `Docs/ArcadeLaunch/ARCHITECTURE.md` | Controls panel live. **`SO_ArcadeGame.Tips` is authored on 2 of 56 cards** |
| **In-match guidance** — objective arrows, `GoalStack`, idle-hint toasts | `Assets/_Scripts/UI/GameToastSystem/GAME_TOASTS.md` | Live |
| **Activation analytics** | `Docs/Analytics/event-taxonomy.md` §4 | `ftue_step_completed` / `ftue_phase_completed` / `ftue_completed` **landed 2026-10-15** (§5). `session_started`, `game_quit_midway`, `network_disconnected` still unbuilt. PostHog funnel + D1/D7 dashboards **not built** |
| **First-session validation** — watch 3-5 first-timers | `STEAM_EA_INVESTOR_CHECKPOINT.html` C2 / E7 | Not run (nothing to watch while the gate hides the FTUE) |
| **Starter economy** | `Docs/ECONOMY_AND_PRICING.md` | Open: "a new player who loses their first several games earns zero, sees a 4000-crystal price tag, and has no visible path to it" |

**Not planned anywhere:** a generic hover tooltip component, or an AI companion that flies beside a
new player in a real match. The Mentor is the planned "friend flying alongside" (TRAINING_PLAN D3),
and it is curated text in the microgame, not a bot. The only AI wingman is the Toy System's
`ReleaseCompanion` (`Docs/HomeHub/ARCHITECTURE.md`).

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

| Work | Branch / PR | Collides on |
|---|---|---|
| Microgame Training + first-login guide | `claude/exciting-bell-69b398` (no PR; last commit 2026-10-13) | `QuestGraphRunner`, quest nodes, `Menu_Main.unity`, `ArcadeExploreView`, `ScreenSwitcher`, `ModalWindowManager`, `MenuHubButton` |
| Menu Redesign B — every Menu_Main screen re-styled (Shombith owns Menu_Main UI, per the team roster) | `claude/new-session-76hnyc` (no PR) | `Menu_Main.unity` (heavy), `QuestTrackView`, `ProfileScreen`, `ArcadeExploreView`, `ArcadeGameConfigureModal`; adds a `tooltip` colour token to `UIThemeSO` |
| Regatta AI seats, racing AI | PR #1098 (`Ys-bleeding-edge`) | `ServerPlayerVesselInitializerWithAI`, `ArcadeGameConfigureModal`; the AI seating any new-player bot would use |
| Quest dialogue copy (C9) | Caleb, Garrett, Shombith | `QuestDialogueNode` content |
| Funnel dashboards (C3) | Ian | PostHog |

A test merge of `claude/exciting-bell-69b398` into `Ys-bleeding-edge` (2026-10-15) conflicts in only
three files, all docs or tooling (`CLAUDE.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`,
`Tools/Build/check_generated_assets.py`), and each is a keep-both resolution. A text-clean merge of
`Menu_Main.unity` still has to be opened in the editor before anyone trusts it.

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
nothing and raises nothing. With the master unlock on and the training branch not merged, the graph
does not run, so these stay at zero. That is the gate, not the instrumentation.

---

## 6. The path to activation, in order

1. **Land the training branch.** Open a PR for `claude/exciting-bell-69b398` (its owner) or merge it
   into a working branch, then resolve the three doc/tool conflicts.
2. **Play it once in the editor**: reset quest progress, run *FrogletTools > Quest Graph > Reset
   Microgame Lessons (testing)*, enter `Menu_Main`, follow the spotlight into the Game of the Week,
   finish the Lesson, and stay for the Mentor. TRAINING_PLAN §10.6 lists what only the editor can
   settle.
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

- `MENU_PROGRESSION_AND_IAP.md` (chain table) and `QUEST_TRACK_AND_BREADCRUMB.md` (authored-content
  table) still list Crystal Capture / Hex Race / Joust / PartyGame. The live chain is Scurry → Hex Race
  → Joust → Maelstrom → Vessel Hangar.
- `Docs/QuestMap/QUEST_MAP.html` still draws the retired breadcrumb nodes.
- `Analytics/ANALYTICS_HANDOFF.md` §8 still calls the quest branch unmerged. Its two reconcile items
  (the `Version` vs `SchemaVersion` field name, and the second progression key `QUEST_GRAPH_PROGRESS`)
  have no record of being done.
- The asset `GameModeQuest_SkimRace` unlocks **Hex Race** (mode 33).
- `QuestRewardRevealView` and the whole legacy `DialogueManager` stack are referenced by no scene.
