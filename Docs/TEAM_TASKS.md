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
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md, 'First-login guide - spotlight draws nothing in Unity'

### T-20261015-b47d · open · test · Activation
- **Task:** Hand-test the first-login guide, the microgame Lesson with its ghost demo, and the ftue_* analytics events, end to end.
- **Done when:** All nine steps of the checklist section pass, or each failure is written there and added here as a dev task.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
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
- **Task:** First editor performance capture, S2 Lattice: Code Optimization = Release, Play, Cell Selector > Lattice, wait 8 minutes, press Capture S2 on the Bug Hunt & Performance badge (bottom-left of the Scene view), stop playing and press Send to Claude when Unity asks (the file is one JSON holding burst, diag, prof, renderers, cells and the console; pasting it into a Claude session also works). Close the Third Eye window and any preview first: an extra camera renders the world again and the capture says so.
- **Done when:** A capture_Menu_Main_S2_*.json whose reader output (python3 Tools/Build/perf_capture_report.py) shows no ERROR line (Release, Burst on, no managed job time) is pasted and recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2 with its numbers.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; Docs/DIAGNOSTICS.md, the Bug Hunt & Performance badge

### T-20261015-3e0b · open · test · Performance
- **Task:** Skim Race I2 with 2 AI, Release: press Capture SkimRace on the Scene view badge, stop, then Send to Claude. This is the Burst check the 10-08 fix still owes (burst must read BURST and the Profiler must show no managed job time) and the test of the PoolRefill.Squirrel Prism ~9 KB/frame allocation lead from the Amoebius run.
- **Done when:** The capture is shared or pasted; its burst section reads Run() BURST, Schedule() BURST; prof.managedJobMs < 0.2; the Profiler top-allocators list confirms or clears PoolRefill.Squirrel Prism. Recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; Docs/DIAGNOSTICS.md, the Bug Hunt & Performance badge

### T-20261015-3972 · open · test · Performance
- **Task:** S5 Wildlife Liberation, Release: two minutes into a match, press Capture S5 on the Scene view badge, stop, then Send to Claude. It is the go/no-go for lever L8 (batched creature body re-sync): Fauna.BodySync must average 8 ms or more for L8 to be worth building.
- **Done when:** The capture is shared or pasted and the Fauna.BodySync / LightFauna.Update rows are read; the L8 decision is written in Docs/PERFORMANCE_OPTIMIZATION.md 3.3.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; Docs/DIAGNOSTICS.md, the Bug Hunt & Performance badge

### T-20261015-949d · done · test · diagnostics
- **Task:** Test the Bug Hunt check-in and Team Bug Board in Unity: FrogletTools > Diagnostics > Bug Hunt Check-in, Share now; play with an error; daily sweep picks it up
- **Done when:** A check-in commit with BugLedger/shared/issues + reports lands on the branch, and the next sweep shows the issue on https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#the-bug-hunt-check-in
- **Done:** 2026-10-15, Superseded 2026-10-15: the Bug Ledger window, check-in and Stage & Push were removed; the one 'Send to Claude' question replaces them (new test task added).

### T-20261015-8bae · open · decision · diagnostics
- **Task:** Assets/_Models/shield1.blend makes every machine without Blender log a red 'Blender could not be found' on import (Bug Ledger E-fd64b43840). Nothing in Assets references its guid. Delete it, or export it to .fbx?
- **Done when:** The .blend is gone or replaced by an .fbx, and the error stops on a machine without Blender
- **Who can take it:** Garrett (decides); Yash can do it
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#the-bug-hunt-check-in

### T-20261015-e266 · open · test · AI / Third Eye
- **Task:** Play-test the Third Eye You / AI toggle in Regatta: hand your hull to the AI, take over a rival AI hull, all-AI
- **Done when:** Steps 1-6 of the 'Third Eye: a You / AI toggle' entry pass in the editor
- **Who can take it:** Yash, Andrew
- **Added:** 2026-10-15 by Claude (Yash's session) (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261015-72e1 · open · dev · Performance
- **Task:** Amoebius: Mesh.SetVertices(List)/SetUVs(List<Vector4>)/SetTriangles(List) copy their lists into fresh arrays on every call (ToArray), and the renderer detects a changed mesh by array identity (SceneRenderer ReferenceEquals on RenderVertices / RenderUvWide / RenderSubmesh), so a per-frame mesh upload allocates the whole buffer each frame: CrystalChargeArcs.LateUpdate reads ~708 KB/frame in the Skim Race marker run while the game's own code is allocation-free (it reuses its writer lists). Unity's SetVertices(List) allocates nothing. Make the setters copy in place when the length matches, bump mesh.version, and have the renderer's dirty check key on version (or a per-buffer stamp) instead of array identity, so the engine's allocation numbers mean the game's.
- **Done when:** python3 Tools/Build/perf_hunt.py amoebius --no-build reads CrystalChargeArcs.LateUpdate under 1 KB/frame with the mesh still animating (a screenshot or the flux arcs visibly moving), engine tests green, and the CrystalChargeArcs entry removed from Docs/perf/amoebius_known.json.
- **Who can take it:** Shombith (the Amoebius engine), Yash (offered)
- **Added:** 2026-10-15 by the performance session (perf-hunt) (`perf/performance-optimization`)
- **See:** Docs/perf/amoebius_known.json; Port/src/CosmicShore.Engine/Rendering/Mesh.cs:249,289; Mesh.Extras.cs:78,139; Port/src/CosmicShore.Render/SceneRenderer.cs:2676,1428,2501; Port/docs/ARCHITECTURE.md 11.3b

### T-20261015-f106 · open · setup · Performance
- **Task:** Switch on the daily performance check: GitHub runs a scheduled workflow only from the repository's DEFAULT branch, so .github/workflows/perf-daily.yml (merged into Ys-bleeding-edge) fires once a day only after it reaches bleeding-edge / the default branch. Until then run it by hand: Actions > perf-daily > Run workflow on Ys-bleeding-edge. Check the first run's two artifacts (perf-sweep, perf-amoebius) read sensibly: gates all OK, the Amoebius Skim Race run 'within baseline'.
- **Done when:** A perf-daily run has completed green on the default branch on its own schedule (Actions shows a scheduled run), and its perf-sweep artifact lists the gates OK and the shared captures.
- **Who can take it:** Garrett (bleeding-edge / repo settings), Shombith (CI), Yash (offered)
- **Added:** 2026-10-15 by the performance session (perf-hunt) (`perf/performance-optimization`)
- **See:** .github/workflows/perf-daily.yml; Docs/PERFORMANCE_OPTIMIZATION.md 3.4; /perf-hunt section 0

### T-20261015-7d29 · open · test · Performance
- **Task:** Test the Bug Hunt & Performance badge in the Scene view: open Unity, check the card shows bugs and perf with no dialog on load, fold/unfold/hide it, press Release if shown, then in Play mode on Menu_Main press Capture S2, stop, and check Unity asks Send to Claude.
- **Done when:** Every step of the 2026-10-15 badge entry in Docs/UNITY_VERIFICATION_CHECKLIST.md passes (or the failing step is reported to a Claude session), and the entry is moved to verified.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-15; Docs/DIAGNOSTICS.md, The Bug Hunt & Performance badge

### T-20261015-0bc6 · open · test · diagnostics
- **Task:** Test the one-button bug flow in Unity: play until a red error appears (or Debug.LogError from any script), stop Play, press Send to Claude; play and stop again with the same error
- **Done when:** The dialog appears once; origin/bugledger/<you> gets BugLedger/shared/issues/E-*.bug.json; git status unchanged and you are still on your branch; the second stop shows no dialog
- **Who can take it:** Yash, Andrew
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#send-to-claude--the-only-ui

### T-20261015-4996 · open · content · audio
- **Task:** Boost Activate (event:/SFX/Oneshots/Gameplay sfx/Boost Activate) has a loop region but is fired as a one-shot, so the code refuses to play it: the Manta's boost is silent and logs one error per session (Bug Ledger E-cdf0c783c1). Remove the loop region, or tell engineering it is a held boost sound
- **Done when:** Boosting the Manta plays a sound and the [FMODOneShotVolumeHelper] looping error stops
- **Who can take it:** Charles (audio owner)
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/AudioSystem/CHARLES_TASKS.md (C2)

### T-20261015-aa0d · open · test · Regatta AI
- **Task:** Playtest the Squirrel AI skimming Regatta's rails: Squirrels on AI seats at I1 and I4, the Third Eye's skim readout on one (lift 7.5, direct off, shell gap 1.5-6), and tune Resources/SkimRaceAIConfig_Regatta.asset live if it still strikes or drifts off its rail
- **Done when:** The Squirrel rides outside its own rail, strikes near zero, threads every ring; any tuned numbers recorded in author_skimrace_ai_config.py
- **Who can take it:** Yash (found it), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by Claude for Yash (`Ys-bleeding-edge`)
- **See:** Docs/SKIM_RACE_AI.md §15; Docs/UNITY_VERIFICATION_CHECKLIST.md (Regatta Squirrel skim)

### T-20261015-7edd · open · test · Performance
- **Task:** Confirm the lava lamp fps fix in Unity: Release, Third Eye closed, Play Menu_Main, press Capture S2 on the Scene view badge in the first minute, stop, Send to Claude. Then fly close to an omni and a Charge crystal and check their light looks as before.
- **Done when:** The capture reads well above the 19-30 fps reported on 10-15, and the 2026-10-15 lava-lamp entry in Docs/UNITY_VERIFICATION_CHECKLIST.md passes.
- **Who can take it:** Yash (reported it), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-15 lava lamp row); Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261015-a5a5 · open · decision · Performance
- **Task:** The Charge light now sleeps when a crystal is under 24 px in radius on screen (CrystalChargeArcsProfileSO.sleepBelowScreenRadiusPixels; it rebuilt a mesh per crystal per frame and cost more than every other menu script together in the lava lamp). Check the look at the distance it wakes and pick the number; also worth deciding whether the arcs should rebuild at a lower rate mid-distance.
- **Done when:** Garrett has looked at it in the lava lamp and the field holds his number (generator and asset).
- **Who can take it:** Garrett (owns the crystals)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-15 lava lamp row)

### T-20261015-1551 · open · test · Performance
- **Task:** Find why Regatta runs at 10 fps in the editor: in MinigameRegatta (Release code optimization, Deep Profile off), press Capture on the Scene view's Bug Hunt & Performance badge once with the Third Eye CLOSED and once with it OPEN (label R_editor_A / R_editor_B), stop Play and Send to Claude each time
- **Done when:** Both captures are on bugledger/yskha and the report names what the 10 fps is spent on (game vs Third Eye vs editor state)
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by Claude for Yash (`Ys-bleeding-edge`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md §4.2a

### T-20261015-4354 · open · test · Multiplayer
- **Task:** Run the Unity multiplayer hand tests on Ys-bleeding-edge: the eleven QA-NET items in Docs/QA/QA_BACKLOG.md (QA-NET-SDK-UPGRADE first, it is the gate), in the order in Docs/QA/ANDREW_TEST_HANDOFF.md. Give each Multiplayer Play Mode virtual player its own tag (P2, P3, P4) first. Plus two checks with no QA item yet: host drops then the new host invites at once (5 times), and 5 minutes idle in the menu with no 429s.
- **Done when:** Every QA-NET item has a verdict in a Docs/QA/RESULTS/<date>-<name>.md file that was Submitted (BLOCKED is a valid verdict, e.g. QA-NET-ARCADE-LOBBY-FOLLOW without three machines); the two extra checks are written in its 'Anything else' section; then tell Claude to apply the results.
- **Who can take it:** Andrew, Yash (offered)
- **Added:** 2026-10-15 by Yash's session (multiplayer) (`Ys-bleeding-edge`)
- **See:** Docs/QA/ANDREW_TEST_HANDOFF.md; Docs/QA/README.md; Docs/MULTIPLAYER_START_HERE.md 5.2; Docs/PartySystem/TESTS.md

### T-20261015-0426 · open · test · Multiplayer
- **Task:** Hand-test Amoebius multiplayer on Windows from Prisma.exe: the eight steps of Docs/MULTIPLAYER_START_HERE.md 5.1 (four players tile, party up, simulated 4G, pulled cable, session fault 'party is full', capture, TCP still works, through the relay). Do NOT run the live UGS test (5.3); that touches the real UGS project and waits for the owner's go-ahead.
- **Done when:** Each of the eight steps is PASS or FAIL in the 'Anything else' section of a QA results file, with the log text verbatim for any FAIL; a FAIL is also added here as a dev task.
- **Who can take it:** Andrew, Yash (offered), a Windows machine
- **Added:** 2026-10-15 by Yash's session (multiplayer) (`Ys-bleeding-edge`)
- **See:** Docs/QA/ANDREW_TEST_HANDOFF.md; Docs/MULTIPLAYER_START_HERE.md 5.1; Port/docs/MULTIPLAYER.md

### T-20261015-fafa · open · dev · Performance
- **Task:** Pool the AOE explosions: ExplosionHelper.SpawnAllAndDetonate does Object.Instantiate(prefab) per detonation and AOEExplosion Destroys itself after (Projectiles/AOEExplosion.cs), so every crystal collect and every explodable projectile mints a GameObject tree. Measured in Amoebius (Skim Race I2, 2026-10-15): VesselImpactor.AcceptImpactee 5.6 KB/frame, all of it SquirrelVesselExplosionByCrystalEffect (the Squirrel's boost ring, ~300 KB per omni collect in .NET; the ring's prisms are the design, the un-pooled explosion object is the practice gap). A GenericPoolManager<AOEExplosion> per prefab (like PrismExplosionPoolManager) with Release instead of Destroy at the end of ExplodeAsync; keep the reset-for-replay cleanup. Go/no-go: a Unity Release capture's Instantiate / GC rows on a crystal collect.
- **Done when:** AOE explosions come from a pool; perf_hunt.py amoebius shows VesselImpactor.AcceptImpactee under 1 KB/frame on Skim Race I2 and the baseline is adopted; a Release capture of Skim Race shows no Instantiate on a crystal collect.
- **Who can take it:** Garrett (projectiles / the Squirrel's ring), Yash (offered)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.3 (L13) and 2 (10-15 rows)

### T-20261015-641c · open · decision · Performance
- **Task:** Spindles are the last big GameObject-renderer cost in the lava lamp (lever L1): one Renderer per limb, ~40k enabled in a grown Lattice with ~4.6k visible, +5.3 ms CPU measured in S2 (record 3.3). Every Prism is already on entities (record 1.2); spindles are not prisms. Decide the shape: draw spindles through Entities Graphics like prisms, or merge a plant's spindles into one mesh per plant, keeping the per-spindle wither fade (continuity law). Ecology is LOCKED, so this is yours to call before anyone builds it.
- **Done when:** Garrett has picked an approach (or ruled L1 out) and recorded it in Docs/PERFORMANCE_OPTIMIZATION.md 3.3; a build task follows if he picks one.
- **Who can take it:** Garrett (ecology / flora owner)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 1.2 and 3.3 (L1); Docs/Reports/PERFORMANCE.md, Waiting on Garrett

### T-20261015-6009 · open · test · Regatta AI
- **Task:** Teach the Regatta Squirrel AI from your flying: Third Eye > Record on, fly Regatta as the Squirrel by hand 5 times to the finish, stop Play and Send to Claude; then ask a session to run learn_from_flights.py --write and test the AI
- **Done when:** LEARNED_FROM_FLIGHTS.md lists your races and the Squirrel AI flies the learned line without striking its rail
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude for Yash (`Ys-bleeding-edge`)
- **See:** Docs/SKIM_RACE_AI.md §16

### T-20261015-1188 · open · test · AI / training
- **Task:** Fly 3-5 Regatta races by hand (Squirrel first) in the Unity EDITOR (training data comes from the editor only, 2026-10-16) and store each record when Unity asks
- **Done when:** `python3 Tools/Build/situation_features.py --write` reads them (flight/3) and writes learned_situations.json for the hull
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude (Yash's session) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md

### T-20261015-7fc3 · open · dev · AI / Regatta
- **Task:** Port the two learned knobs to Unity: the commit distance as a curve over the leg (GateRaceController aiCommitDistance -> a + b x leg) and the skim-worth rule (the Squirrel's Skim Race pilot in Regatta), behind inspector switches, off by default
- **Done when:** A learned tune from the page's Copy JSON sets them in Unity and the Third Eye shows the AI committing / skipping skims as the page does
- **Who can take it:** Garrett (gate races) with Yash
- **Added:** 2026-10-15 by Claude (Yash's session) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md S5

### T-20261015-0576 · open · test · AI training
- **Task:** After your next hand-flown Regatta race in the editor, open the stored flight_Regatta_*.json and check the new situation data: 44 columns per sample, rails (one per lane) with ownRail, ringList, railEvents after a rail hit, camMode not -1; toggle You/AI for a few seconds and those rows have held 1
- **Done when:** Checklist entry 'AI training recorder: flight/3 records the situation' steps 2 and 2a pass, or what is wrong is reported
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md
