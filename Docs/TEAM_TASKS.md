# Team tasks: hand work to each other

**One board for work that one collaborator's session leaves for another**: a test that needs the Unity
Editor, a playtest, a dashboard or service to set up, content to write, a decision to take. Almost all of
our work goes through Claude sessions, and a session cannot open Unity, sign in to a dashboard or watch a
first-timer play. This is where it says so, and where somebody picks it up (Docs/TEAM.md rule 9).

**Yash has offered to take tasks** (Yash, 2026-10-15): testing in the editor, playtests, UGS and
dashboard setup, and follow-up fixes. When you add a task he could do, name him in "Who can take it".
Shombith, Garrett, Caleb, Andrew and anyone else: take what fits, and add your own.

## How to use it

- **Add** a task whenever your work leaves something another person can do or test. Claude sessions do
  this before they finish (a hook reminds them), and tell the person what they added:
  `python3 Tools/Build/team_tasks.py add --area Activation --kind test --task "..." --done-when "..." --who "Yash (offered), anyone with the Editor" --see "Docs/..."`
- **Find** work: `python3 Tools/Build/team_tasks.py list` (open tasks), `--for Yash`, `--area Activation`,
  `--kind test`.
- **Take** one, so nobody does it twice: `team_tasks.py take <id> --who <you>`. **Close** it:
  `team_tasks.py done <id> --note "what happened, commit or result"`. A test that FAILS is not done: write
  what failed in the note and add the fix as a new `dev` task.
- **On GitHub**: `/ship` puts `team_tasks.py pr-section` into the pull request body, so a reviewer sees what a
  branch handed off. The board itself is plain markdown, readable and editable on GitHub.
- **Kinds:** `test` (run something and report pass or fail), `dev` (code), `content` (words, tips, art),
  `setup` (a dashboard, a service, a key), `decision` (the owner or the core four decide), `docs`.
- Ids are `T-<date>-<hash>`, never "the next number", so two branches cannot collide. If two branches both
  append a task, git conflicts at the end of the file: keep both. `team_tasks.py --check` validates the board.
- QA's own failures stay in `Docs/QA/DEV_TASKS.md` (written by `/qa-backlog`); vessel and AI test scripts stay
  in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. A task here may point at either.

## The board

<!-- team-tasks -->

### T-20261015-7b5a · open · test · Activation
- **Task:** Re-test the first-login spotlight in Unity: the 2026-10-06 playtest saw no dim, no frame, no caption and dead presses; a CanvasRenderer fix landed after it and was never re-tested.
- **Done when:** On a fresh profile Menu_Main shows the dim, the pulsing frame on ARCADE, its caption and a pressable Settings gear, and the menu takes presses. Result written in the checklist section.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md, 'First-login guide - spotlight draws nothing in Unity'

### T-20261015-b47d · open · test · Activation
- **Task:** Hand-test the first-login guide, the microgame Lesson with its ghost demo, and the ftue_* analytics events, end to end.
- **Done when:** All nine steps of the checklist section pass, or each failure is written there and added here as a dev task.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md, 'Ghost demo + first-login guide + FTUE analytics'; Docs/ModePreview/TRAINING_PLAN.md 10.7

### T-20261015-9809 · open · setup · Activation
- **Task:** Declare ftue_step_completed (quest_id, phase_index, step_id, step_name, seconds_since_launch), ftue_phase_completed (quest_id, phase_index, seconds_since_launch) and ftue_completed (quest_id, seconds_since_launch) in the UGS dashboard Event Manager; UGS silently drops undeclared events.
- **Done when:** The three events and their parameters are in the Event Manager, and one test session's events show up in UGS.
- **Who can take it:** Yash (offered; UGS), Shombith (analytics handoff)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 5; Docs/Analytics/event-taxonomy.md 4

### T-20261015-2fc0 · open · setup · Activation
- **Task:** Build the activation funnel and D1/D7 retention in PostHog: game_first_launched > menu_ready > ftue_step_completed (guide) > ftue_phase_completed (phase 0) > game_started > game_completed, split by invite_wave.
- **Done when:** The funnel and the retention charts exist and read real sessions; the paid-EA gate (D1 >= 35%, D7 >= 15%) can be read off them.
- **Who can take it:** Ian (funnel owner, C3), Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 6.3; Docs/Analytics/POSTHOG_SETUP.md; Docs/Analytics/DATA_ARCHITECTURE.md

### T-20261015-99cd · open · test · Activation
- **Task:** Watch three to five first-time players on a default build (master unlock at its shipped default) without helping them, and note where each one stalls.
- **Done when:** Notes per player (where they stalled, how long to the first match, whether they came back) are linked here, and each stall is a new task.
- **Who can take it:** Andrew, Shombith, Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 6.4; STEAM_EA_INVESTOR_CHECKPOINT C2 / E7

### T-20261015-fb65 · open · decision · Activation
- **Task:** Decide what Main Quest phases 1-5 become now that first login goes to the Game of the Week microgame; they still assume the freestyle flight school ended phase 0 and a Crystal Capture funnel follows.
- **Done when:** The decision is recorded in TRAINING_PLAN 11 and the phases are re-authored or retired accordingly.
- **Who can take it:** Garrett and the core four (product call)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ModePreview/TRAINING_PLAN.md 11.1

### T-20261015-f4c7 · open · dev · Activation
- **Task:** Reconcile Menu_Main between the first-login guide (merged into Ys-bleeding-edge 2026-10-15) and Menu Redesign B (claude/new-session-76hnyc) before either reaches bleeding-edge: the spotlight must find the restyled ARCADE entry, cards and Settings gear.
- **Done when:** Both are on one branch, the spotlight walks the restyled menu in the editor, and author_first_login_guide.py --check passes.
- **Who can take it:** Shombith (Menu_Main owner), Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 4; Docs/MENU_REDESIGN_B.md on claude/new-session-76hnyc

### T-20261015-d364 · open · content · Activation
- **Task:** Write Mentor tip lists for the non-racing mode families (TRAINING_PLAN phase 5): only the racing family (SwitchesThreaded) and seven racing modes have tips today.
- **Done when:** Every playable card composes a Mentor list (DrillCoverageTests asserts it once phase 6 lands); author_drill_assets.py --check passes.
- **Who can take it:** Caleb, Garrett, anyone who writes game copy; Yash (offered) to wire them
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ModePreview/TRAINING_PLAN.md 4.2 and 10

### T-20261015-ed8e · open · content · Activation
- **Task:** Author SO_ArcadeGame.Tips for the 54 of 56 arcade cards that have none, so GameBriefingView has something to teach before a match. Write them in each mode's author_<mode>_assets.py generator, not only in the asset.
- **Done when:** Every card has two or more tips and every touched generator's --check passes.
- **Who can take it:** Caleb (arcade mode map), anyone; Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ArcadeLaunch/ARCHITECTURE.md (Tips); the /arcadegame skill

### T-20261015-81b3 · open · dev · Analytics
- **Task:** Reconcile the two items ANALYTICS_HANDOFF 8 left for the quest-branch merge: the save record's Version vs SchemaVersion, and the second progression key QUEST_GRAPH_PROGRESS beside GAME_MODE_PROGRESSION.
- **Done when:** One schema field name and a written decision on the two keys, with a test.
- **Who can take it:** Shombith (handoff addressee), Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/Analytics/ANALYTICS_HANDOFF.md 8

### T-20261015-013e · open · test · Activation
- **Task:** Run the three progression QA items that have never been run: QA-PROGRESSION-GATE-ON, QA-PROGRESSION-GATE-OFF and QA-FTUE-QUEST-ROWS.
- **Done when:** Results are submitted under Docs/QA/RESULTS and /qa-backlog has applied them.
- **Who can take it:** Andrew, Caleb, Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/QA/QA_BACKLOG.md

### T-20261015-ff1c · open · docs · Activation
- **Task:** Fix stale activation docs: the unlock-chain tables in MENU_PROGRESSION_AND_IAP.md and QUEST_TRACK_AND_BREADCRUMB.md (live chain: Scurry > Hex Race > Joust > Maelstrom > Vessel Hangar), and QuestMap/QUEST_MAP.html, which still draws the retired breadcrumb nodes.
- **Done when:** The three docs match the shipped GameModeQuestList.asset and the quest graph.
- **Who can take it:** anyone; Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 7

### T-20261015-1a5b · done · dev · Performance
- **Task:** check_mathf_params_alloc.py is red on two sites that predate the activation work: BlackHoleWarp.cs:87 (3-value Mathf.Max) and FlipWave.cs:153 (4-value Mathf.Max), each allocating a params array.
- **Done when:** Both use MathfNoAlloc or nested two-value calls and the gate passes.
- **Who can take it:** the owners of the black hole and FlipWave code (check who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Tools/Build/check_mathf_params_alloc.py

### T-20261015-18e7 · taken by Yash · test · Regatta AI
- **Task:** Playtest Regatta in Unity on Ys-bleeding-edge: five hulls (Squirrel, Manta, Rhino, Urchin, Dolphin) on AI seats, Hard, I1 then I3, watched in the Third Eye with Trails on.
- **Done when:** Every hull finishes three laps at I1 (and I3, or the ring it fails at is noted); AIs read Approach then Through near rings and break off straight after a miss; result written in the 2026-10-15 Regatta section of Docs/UNITY_VERIFICATION_CHECKLIST.md.
- **Who can take it:** Yash (testing it), anyone with the Unity Editor
- **Added:** 2026-10-15 by Yash's session (regatta-ai) (`Ys-bleeding-edge`)
- **See:** Docs/Reports/REGATTA_AI.md section 2
- **Taken:** 2026-10-15, Yash

### T-20261015-0064 · open · decision · AI training
- **Task:** Decide the AI-training direction: extend the genetic trainer (Assets/_Scripts/Utility/AITraining) for Regatta rather than build a second one; where consented player flights are stored and the privacy wording; whether a neural-network AI may ever ship.
- **Done when:** The answers are written in Docs/AI_SYSTEM/AI_TRAINING_PLAN.md section 5.
- **Who can take it:** Garrett and the core four; studyholic and Shombith for the trainer
- **Added:** 2026-10-15 by Yash's session (regatta-ai) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/AI_TRAINING_PLAN.md

### T-20261015-cb8f · open · decision · Regatta AI
- **Task:** Decide whether the gate-race steering fix (commit at the approach point, re-attack after a missed ring; on in Regatta only) is turned on for Headlong, Switchback, Breakwater, Skein and Redline (inspector switch aiCommitFromApproachPoint) after a playtest.
- **Done when:** Each mode's switch is set by its owner and the choice noted in REGATTA.md section 6.1.
- **Who can take it:** Garrett (gate races owner)
- **Added:** 2026-10-15 by Yash's session (regatta-ai) (`Ys-bleeding-edge`)
- **See:** Docs/Reports/REGATTA_AI.md section 3

### T-20261015-2082 · open · dev · AI training
- **Task:** Build T1 of the AI training plan: the AI Lab (Test Range R1-R3) in MinigameRegatta - any hull on any seat (trained or untrained), 0.25x-8x time, the Third Eye's trails and readout in a development build, speed graph and phase timeline, a scorecard JSON per run.
- **Done when:** A development build of Regatta opens the AI Lab, races chosen hulls at chosen speed, and writes a scorecard file the artifact's numbers can be compared with.
- **Who can take it:** Yash (offered), with Garrett for the trainer hand-off
- **Added:** 2026-10-15 by Yash's session (regatta-ai) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/AI_TRAINING_PLAN.md section 3 L1, Docs/Studios/VESSEL_TEST_RANGE_PLAN.md

- **Done:** 2026-10-15, Fixed on perf/performance-optimization: BlackHoleWarp.cs uses MathfNoAlloc.Max, FlipWave.cs nests two-value Max. Proof: python3 Tools/Build/check_mathf_params_alloc.py -> OK (2093 files); unity_refcompile player-dev RESULT OK; Amoebius Debug build. /verify-unity unavailable in the session.

### T-20261015-56dd · open · test · Performance
- **Task:** First editor performance capture, S2 Lattice: Code Optimization = Release, Play, Cell Selector > Lattice, wait 8 minutes, FrogletTools > Performance > Capture Performance (one file) with label S2, 15 s, then paste the clipboard into a Claude session (it is one JSON holding burst, diag, prof, renderers, cells and the console). Close the Third Eye window and any preview first: an extra camera renders the world again and the capture says so.
- **Done when:** A capture_Menu_Main_S2_*.json whose reader output (python3 Tools/Build/perf_capture_report.py) shows no ERROR line (Release, Burst on, no managed job time) is pasted and recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2 with its numbers.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; FrogletTools > Performance > Capture Performance (one file)

### T-20261015-3e0b · open · test · Performance
- **Task:** Skim Race I2 with 2 AI, Release: capture SkimRace 15 (same window). This is the Burst check the 10-08 fix still owes (burst must read BURST and the Profiler must show no managed job time) and the test of the PoolRefill.Squirrel Prism ~9 KB/frame allocation lead from the Amoebius run.
- **Done when:** The capture is pasted; its burst section reads Run() BURST, Schedule() BURST; prof.managedJobMs < 0.2; the Profiler top-allocators list confirms or clears PoolRefill.Squirrel Prism. Recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; FrogletTools > Performance > Capture Performance (one file)

### T-20261015-3972 · open · test · Performance
- **Task:** S5 Wildlife Liberation, Release: capture S5 15, two minutes into a match. It is the go/no-go for lever L8 (batched creature body re-sync): Fauna.BodySync must average 8 ms or more for L8 to be worth building.
- **Done when:** The capture is pasted and the Fauna.BodySync / LightFauna.Update rows are read; the L8 decision is written in Docs/PERFORMANCE_OPTIMIZATION.md 3.3.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; FrogletTools > Performance > Capture Performance (one file)

### T-20261015-949d · open · test · diagnostics
- **Task:** Test the Bug Hunt check-in and Team Bug Board in Unity: FrogletTools > Diagnostics > Bug Hunt Check-in, Share now; play with an error; daily sweep picks it up
- **Done when:** A check-in commit with BugLedger/shared/issues + reports lands on the branch, and the next sweep shows the issue on https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#the-bug-hunt-check-in

### T-20261015-8bae · open · decision · diagnostics
- **Task:** Assets/_Models/shield1.blend makes every machine without Blender log a red 'Blender could not be found' on import (Bug Ledger E-fd64b43840). Nothing in Assets references its guid. Delete it, or export it to .fbx?
- **Done when:** The .blend is gone or replaced by an .fbx, and the error stops on a machine without Blender
- **Who can take it:** Garrett (decides); Yash can do it
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#the-bug-hunt-check-in
