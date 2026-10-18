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
- **On GitHub**: every task is mirrored to an issue (issues #1105-#1144 hold the first 40), titled `[<id>] <task>`
  and labelled `team-task`, `kind:<kind>`, `area:<area>` and `taken`. Filter the repository's issues by `team-task`
  to see the board on GitHub or a phone, and comment on an issue to discuss a task. **The board is the source**: take
  and close tasks here (`take`, `done`); the sync rewrites an issue's body and closes it when the task is done. It
  runs on every push of this file to `bleeding-edge` or `Ys-bleeding-edge`
  (`.github/workflows/team-tasks-sync.yml`), nightly, and by hand (`team_tasks.py gh-sync`, `--dry-run` to preview).
  It never reopens an issue or moves one backwards, so an older copy of the board on another branch cannot undo a
  newer one; a task closed on GitHub but still open here is reported, so it can be closed here with `done`. `/ship`
  also puts `team_tasks.py pr-section` into the pull request body, so a reviewer sees what a branch handed off.
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
- **Task:** First editor performance capture, S2 Lattice: Code Optimization = Release, Play, Cell Selector > Lattice, keep playing 8 minutes (the editor captures S2 by itself; the Scene view badge counts down), stop playing (it goes to Claude by itself) (the file is one JSON holding burst, diag, prof, renderers, cells and the console; pasting it into a Claude session also works). Close the Third Eye window and any preview first: an extra camera renders the world again and the capture says so.
- **Done when:** A capture_Menu_Main_S2_*.json whose reader output (python3 Tools/Build/perf_capture_report.py) shows no ERROR line (Release, Burst on, no managed job time) is pasted and recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2 with its numbers.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; Docs/DIAGNOSTICS.md, the Bug Hunt & Performance badge

### T-20261015-3e0b · open · test · Performance
- **Task:** Skim Race I2 with 2 AI, Release: play 90 s (the editor captures SkimRace by itself), then stop: it goes to Claude by itself. This is the Burst check the 10-08 fix still owes (burst must read BURST and the Profiler must show no managed job time) and the test of the PoolRefill.Squirrel Prism ~9 KB/frame allocation lead from the Amoebius run.
- **Done when:** The capture is shared or pasted; its burst section reads Run() BURST, Schedule() BURST; prof.managedJobMs < 0.2; the Profiler top-allocators list confirms or clears PoolRefill.Squirrel Prism. Recorded in Docs/PERFORMANCE_OPTIMIZATION.md 2.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.6 and 4.6; Docs/DIAGNOSTICS.md, the Bug Hunt & Performance badge

### T-20261015-3972 · open · test · Performance
- **Task:** S5 Wildlife Liberation, Release: play two minutes into a match (the editor captures S5 by itself), then stop: it goes to Claude by itself. It is the go/no-go for lever L8 (batched creature body re-sync): Fauna.BodySync must average 8 ms or more for L8 to be worth building.
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
- **Task:** Test the Bug Hunt & Performance badge in the Scene view: open Unity, check the card shows bugs and perf with no dialog on load, fold/unfold/hide it, press Release if shown, then Play Menu_Main past the badge's auto-capture countdown, stop, and check the console says it was sent to Claude by itself.
- **Done when:** Every step of the 2026-10-15 badge entry in Docs/UNITY_VERIFICATION_CHECKLIST.md passes (or the failing step is reported to a Claude session), and the entry is moved to verified.
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-15; Docs/DIAGNOSTICS.md, The Bug Hunt & Performance badge

### T-20261015-0bc6 · open · test · diagnostics
- **Task:** Test the one-button bug flow in Unity: play until a red error appears (or Debug.LogError from any script), stop Play (it is sent by itself; the console says so); play and stop again with the same error
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
- **Task:** Confirm the lava lamp fps fix in Unity: Release, Third Eye closed, Play Menu_Main for 8 minutes (the editor captures S2 by itself), then stop: it goes to Claude by itself. Then fly close to an omni and a Charge crystal and check their light looks as before.
- **Done when:** The capture reads well above the 19-30 fps reported on 10-15, and the 2026-10-15 lava-lamp entry in Docs/UNITY_VERIFICATION_CHECKLIST.md passes.
- **Who can take it:** Yash (reported it), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-15 lava lamp row); Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261015-a5a5 · done · decision · Performance
- **Task:** The Charge light now sleeps when a crystal is under 24 px in radius on screen (CrystalChargeArcsProfileSO.sleepBelowScreenRadiusPixels; it rebuilt a mesh per crystal per frame and cost more than every other menu script together in the lava lamp). Check the look at the distance it wakes and pick the number; also worth deciding whether the arcs should rebuild at a lower rate mid-distance.
- **Done when:** Garrett has looked at it in the lava lamp and the field holds his number (generator and asset).
- **Who can take it:** Garrett (owns the crystals)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-15 lava lamp row)
- **Done:** 2026-10-17, 2026-10-17, Yash took it over: kept 24 px (a bolt core is ~0.3 px there) and added halfRateBelowScreenRadiusPixels = 64 (alternate-frame rebuild between 24 and 64 px), in the profile, the asset and author_omni_crystal_triangles.py. Garrett may still retune both.

### T-20261015-1551 · open · test · Performance
- **Task:** Find why Regatta runs at 10 fps in the editor: in MinigameRegatta (Release code optimization, Deep Profile off), play two sessions of 90 s or more, one with the Third Eye CLOSED and one with it OPEN (the editor captures each by itself, labelled MinigameRegatta; the capture records the open cameras), stop Play each time (each capture goes to Claude by itself)
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

### T-20261015-fafa · done · dev · Performance
- **Task:** Pool the AOE explosions: ExplosionHelper.SpawnAllAndDetonate does Object.Instantiate(prefab) per detonation and AOEExplosion Destroys itself after (Projectiles/AOEExplosion.cs), so every crystal collect and every explodable projectile mints a GameObject tree. Measured in Amoebius (Skim Race I2, 2026-10-15): VesselImpactor.AcceptImpactee 5.6 KB/frame, all of it SquirrelVesselExplosionByCrystalEffect (the Squirrel's boost ring, ~300 KB per omni collect in .NET; the ring's prisms are the design, the un-pooled explosion object is the practice gap). A GenericPoolManager<AOEExplosion> per prefab (like PrismExplosionPoolManager) with Release instead of Destroy at the end of ExplodeAsync; keep the reset-for-replay cleanup. Go/no-go: a Unity Release capture's Instantiate / GC rows on a crystal collect.
- **Done when:** AOE explosions come from a pool; perf_hunt.py amoebius shows VesselImpactor.AcceptImpactee under 1 KB/frame on Skim Race I2 and the baseline is adopted; a Release capture of Skim Race shows no Instantiate on a crystal collect.
- **Who can take it:** Garrett (projectiles / the Squirrel's ring), Yash (offered)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.3 (L13) and 2 (10-15 rows)
- **Done:** 2026-10-17, 2026-10-17, Yash's session: AOEExplosionPool (opt-in, on for the Squirrel ring spawner: 1 miss / 7 reuses per 8 collects). The real allocator was CrystalMorphMeshBuilder.TryBuild (5.22 of 5.53 KB/frame); its per-source emit cache brought VesselImpactor.AcceptImpactee 5.53 -> 3.41 KB/frame, under the 3.83 baseline (rest is Amoebius's Mesh.Set* copy). Unity check: T-20261017-c82c / Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-17.

### T-20261015-641c · done · decision · Performance
- **Task:** Spindles are the last big GameObject-renderer cost in the lava lamp (lever L1): one Renderer per limb, ~40k enabled in a grown Lattice with ~4.6k visible, +5.3 ms CPU measured in S2 (record 3.3). Every Prism is already on entities (record 1.2); spindles are not prisms. Decide the shape: draw spindles through Entities Graphics like prisms, or merge a plant's spindles into one mesh per plant, keeping the per-spindle wither fade (continuity law). Ecology is LOCKED, so this is yours to call before anyone builds it.
- **Done when:** Garrett has picked an approach (or ruled L1 out) and recorded it in Docs/PERFORMANCE_OPTIMIZATION.md 3.3; a build task follows if he picks one.
- **Who can take it:** Garrett (ecology / flora owner)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 1.2 and 3.3 (L1); Docs/Reports/PERFORMANCE.md, Waiting on Garrett
- **Done:** 2026-10-17, 2026-10-17, Yash's call: measured before building. A grown Lattice holds 2,898 spindles at 8 min in Amoebius (not ~40k); a sub-pixel render LOD hid 0 of them (and 0 in Garland), so it was removed. No structural build until a Unity S2 renderer census (T-20261015-56dd) shows the count is still large. Docs/PERFORMANCE_OPTIMIZATION.md 3.3 L1.

### T-20261015-6009 · done · test · Regatta AI
- **Task:** Teach the Regatta Squirrel AI from your flying: Third Eye > Record on, fly Regatta as the Squirrel by hand 5 times to the finish, stop Play and Send to Claude; then ask a session to run learn_from_flights.py --write and test the AI
- **Done when:** LEARNED_FROM_FLIGHTS.md lists your races and the Squirrel AI flies the learned line without striking its rail
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude for Yash (`Ys-bleeding-edge`)
- **See:** Docs/SKIM_RACE_AI.md §16
- **Done:** 2026-10-16

### T-20261015-1188 · done · test · AI / training
- **Task:** Fly 3-5 Regatta races by hand (Squirrel first) in the Unity EDITOR (training data comes from the editor only, 2026-10-16) (each record is stored by itself when Play stops)
- **Done when:** `python3 Tools/Build/situation_features.py --write` reads them (flight/3) and writes learned_situations.json for the hull
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude (Yash's session) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md
- **Done:** 2026-10-16

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

### T-20261015-ac73 · open · test · Multiplayer
- **Task:** Check that multiplayer test logs collect themselves in Unity: an MPPM test (main + P2 + P3) leaves one Logs/NetSessions file per player, the main editor sends them by itself (one console line names the players), the send lands them on bugledger/<you>, and net_logs.py show 1 prints one timeline. Solo play writes nothing. From now on, press Send to Claude after every multiplayer hand test instead of pasting consoles.
- **Done when:** The six steps of the checklist entry 'Multiplayer test logs collect themselves' pass, or a failure is noted with its console
- **Who can take it:** Andrew, Yash (offered)
- **Added:** 2026-10-15 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Multiplayer test logs entry); Docs/DIAGNOSTICS.md § Multiplayer test logs; Amoebius writes the same logs during the live UGS test (Docs/MULTIPLAYER_START_HERE.md 5.3)

### T-20261015-d7f8 · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: VesselImpactor.AcceptImpactee allocates 3.831 -> 5.578 KB/frame (+46%)
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-15, Duplicate of T-20261015-fafa: attributed with nested markers to the un-pooled AOE ring explosion on each omni collect (lever L13, Garrett).

### T-20261015-c8e2 · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: PoolRefill.FastGrowPrism allocates 0.526 -> 0.7 KB/frame (+33%)
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-17, 2026-10-17: not a code regression. The baseline commit (574e0d39a) itself re-measures 0.766 KB/frame today; the refill tracks boost-prism lays 1:1 at ~20 KB each in .NET (net new ring mass), so it moves with how many rings the AIs lay. Current build 0.693 (repeats to 0.3%); the Skim Race Amoebius baseline was re-adopted from that run with this evidence (Docs/PERFORMANCE_OPTIMIZATION.md 2, 10-17 pool row).

### T-20261015-c7db · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: new allocating marker PoolRefill.PrismImplosion: 0.122 KB/frame
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-17, 2026-10-17: fixed. The implosion pool topped its buffer up to 160 although death implosions are batched since D4 and only the Sparrow's ReverseSuction Get()s it; PrismManagers.prefab bufferSizeTarget 160 -> 24. PoolRefill.PrismImplosion 0.157 (baseline commit) / 0.122 -> 0.000 KB/frame. Same prefab: the explosion pool (never Get()) no longer prewarms 1,500 objects at boot.

### T-20261015-7392 · done · dev · AI training
- **Task:** Add the pilot's VIEW to the situation recording: record the camera pose/FOV each sample (V1), compute on-screen / occluded / time-to-contact view features and a coarse view grid (V2), gate the target and skipped-skim labels on what was visible (V3), measure the per-pilot reaction lag (V4)
- **Done when:** flight/3 (or /4) carries the camera; situation_features --self-test passes with the view group; the skim rule is fit only on skims the pilot could see; learn_from_flights reports a measured lag
- **Who can take it:** Yash (his AI-training session); Garrett for V4's handicap numbers
- **Added:** 2026-10-15 by Caleb (`claude/friendly-faraday-qropbs`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §7
- **Done:** 2026-10-16, V1-V4 built on Ys-bleeding-edge 2026-10-16 (Yash's session): camera in flight/3, view group + grid, labels gated on what was seen, measured reaction lag reported by both learners; situation_features --self-test covers each with a negative control. Editor check is T-20261015-0576 (checklist step 2b); V5 not started.

### T-20261016-f695 · open · decision · AI training
- **Task:** Once 3+ hand-flown Regatta editor flights with the camera exist, compare the measured reaction lag (situation_features.py report / learned_situations.json reactionLag) with SkimRaceDifficultySO's Easy/Medium/Hard ReactionSeconds (0.5 / 0.25 / ...) and decide whether any should change
- **Done when:** A decision is recorded in Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §7 (keep, or new numbers)
- **Who can take it:** Garrett (handicap owner)
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §7 Status

### T-20261016-6695 · open · test · AI training
- **Task:** Race the Squirrel in Regatta in the Unity editor, about 100 races (Docs/AI_SYSTEM/RACING_LINE.md §4). Nothing to type: each leg's style (rail riding, drift, abilities, boost) and the rival difficulty are read from the recording. Mix it up: ride your rail, drift, press abilities, pick Hard rivals in the lobby for some races, play I2-I4 too. Store every race, the bad ones too
- **Done when:** about 100 stored Squirrel flights; racing_line.py reports every leg trusted and stable on each course
- **Who can take it:** Yash
- **Added:** 2026-10-16 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/RACING_LINE.md

### T-20261016-39e6 · open · test · Regatta AI
- **Task:** Race the Regatta Squirrel AI after its ring-face change (SkimHeight -6.5, learned from your flights): watch it in the Third Eye skim the side of the rail the rings are on; count its rail hits and its rings when you finish (old median 14/24); revert the one asset to compare with the old AI
- **Done when:** Checklist entry 'Regatta Squirrel AI skims the ring face of the rail' passes or the failure is reported
- **Who can take it:** Yash
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md; Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §8

### T-20261016-89ed · open · test · Regatta AI
- **Task:** Fly Regatta I1 as the Rhino, the Dolphin, the Manta and the Urchin, 5+ finished races each, storing every one; in one race per hull flip Third Eye > Pilots > AI on your hull for a lap and back (gives the AI's numbers beside yours). Then ask a session to run learn_drives_from_flights.py --write
- **Done when:** Each of the four hulls has 3+ finished Regatta flights on bugledger/<you>
- **Who can take it:** Yash
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9

### T-20261016-0eb3 · done · decision · Regatta AI
- **Task:** Two decisions for the Regatta AIs: (1) put the Stoat on the Regatta card (it is one of the roadmap's six but cannot be flown in Regatta, so its AI cannot learn from flights); (2) cece/relaxed-hopper-r62hbd carries a second, older RampBoostAIPolicySO for the Rhino than Ys-bleeding-edge's - pick one before it merges
- **Done when:** Both answered in Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md
- **Who can take it:** Yash with Garrett (Stoat and Rhino owner sessions)
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9
- **Done:** 2026-10-16, (1) Yash put the Stoat on the Regatta card (2026-10-17, author_regatta_assets.py); (2) the Rhino ramp policy question moved to its own task.

### T-20261016-2292 · open · test · Arena launch
- **Task:** Open Menu > Arena in the editor and look at the AI Difficulty row and the re-fitted vessel carousel on all four cards (Astro League, Brood Rush, Regatta, Broadside)
- **Done when:** Row sits under the intensity row like Arcade's Skim Race; icon, arrows and SELECT VESSEL fit inside the carousel frame; Easy/Medium/Hard light and step with the pad
- **Who can take it:** Yash (offered); Shombith owns Menu_Main
- **Added:** 2026-10-16 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (AI difficulty on every Arena card)

### T-20261017-c82c · open · test · Performance
- **Task:** Look at the instanced snow and the Squirrel's collect in Unity (2026-10-17 perf change): (1) Menu_Main and any arcade cell: the cytoplasm snow fills the cell at the same density as before and turns towards the crystal after a collect; look for transparent shards drawing in the wrong order where two overlap (they are one instanced draw now, unsorted inside it). (2) Skim Race or Regatta as the Squirrel: ten omni collects in a row, each lays its shielded ring and the crystal still morphs onto it and dissolves.
- **Done when:** Someone has looked and written here whether the snow reads the same (if not, what is wrong) and the Squirrel collect still morphs; Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-17 entry ticked
- **Who can take it:** Yash (offered), Garrett (crystals, cytoplasm)
- **Added:** 2026-10-17 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-17; Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-17 row)

### T-20261016-b12d · open · test · Brittle Star
- **Task:** Walk the Brittle Star four-mode build step by step in the Editor: the danger trail when overheated in Space Mode, the spare-ring stock (LT refused with none, one per crystal, two at most, the ring regrowing), the HUD cards (Charge = spare rings, Space = heat, red when overheated), an AI seat in an arcade match, and two MPPM clients (petals only; the analog-RT gap). Garrett played it 2026-10-16 and reported it working, not step by step.
- **Done when:** Each step of BRITTLE_STAR.md 'In-editor verification' 2-12 is reported pass or fail; failures become dev tasks
- **Who can take it:** Yash (offered), anyone with the Editor
- **Added:** 2026-10-16 by Claude session cece/beautiful-knuth-j3atgk (`cece/beautiful-knuth-j3atgk`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md (In-editor verification); Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261016-3e80 · open · decision · Brittle Star
- **Task:** Brittle Star ring stock: should a hull start with one spare ring? With none, the studio pilot almost never throws the ring (Hard and 'Hard, no ring' are equal on every Derby row) and Derby I4's super-shielded walls are no faster with the modes than without. Also confirm retiring Overdrive (a crystal at a full stock of two now does nothing) and Charge Mode on RB / E (FlipAction; on a phone, turning it sideways raises the same event).
- **Done when:** Garrett picks: a starting spare or not, Overdrive retired or brought back in some form, Charge Mode's binding kept or moved
- **Who can take it:** Garrett
- **Added:** 2026-10-16 by Claude session cece/beautiful-knuth-j3atgk (`cece/beautiful-knuth-j3atgk`)
- **See:** Docs/Studios/BRITTLE_STAR.md 5.4; Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md 1 and 3.5

### T-20261016-5ed6 · open · dev · Brittle Star
- **Task:** Replicate the Brittle Star's analog RT depth (two bytes a frame) so remote copies fire, heat, overheat (and lay the danger trail) and stiffen the Charge cords at the owner's real pull; today a remote copy reads a press as a full pull, so its heat state can differ by peer (vessel rule 48). The Thresher has the same gap.
- **Done when:** Two MPPM clients agree on overheat timing and the danger trail during a half pull
- **Who can take it:** Yash (offered: party / multiplayer), Garrett
- **Added:** 2026-10-16 by Claude session cece/beautiful-knuth-j3atgk (`cece/beautiful-knuth-j3atgk`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md 3.4 and follow-up F13; .claude/skills/vessel/SKILL.md 4.ab4

### T-20261016-12f8 · open · content · Brittle Star
- **Task:** Draw the Brittle Star's Charge Mode icon (five whip cords / sabers) for the HUD's Charge card, which still wears the Ring Detonation icon; author it through Tools/Build/author_brittlestar_icons.py so --check holds it.
- **Done when:** The Charge card shows the cords; author_brittlestar_icons.py --check passes
- **Who can take it:** anyone (icon art); Garrett to approve
- **Added:** 2026-10-16 by Claude session cece/beautiful-knuth-j3atgk (`cece/beautiful-knuth-j3atgk`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md 4 and follow-up F12

### T-20261016-e2fb · open · test · Brittle Star
- **Task:** Play the Brittle Star's 2026-10-16 tuning pass in the Editor: the ring regrows over 3 s after LT (modes off meanwhile); RT at a full pull in Charge Mode leaves the sabers flexing on a ring flick (cordMaxRigidity 0.4) and released they whip slower (cordLooseWave 144); Space Mode at a full pull overheats after ~6 s at Space 0 and ~20 s at Space 10, same ~5 s lockout; the ring explosion is 4x thicker and bursts out five-fold as lit faceted volumes (five coiling arms led by crystals, the ring's pentagon with crystal knots, a faceted torus front with five crests, turning with the ring's spin; nothing flat from any angle); the trail blocks are long and slim (1 x 1 x 6, base trail 0.5 wide) and widen in x and y with Mass.
- **Done when:** Each item seen in Play mode and noted in Docs/UNITY_VERIFICATION_CHECKLIST.md's 'Brittle Star tuning pass' section
- **Who can take it:** Garrett, or Yash (offered)
- **Added:** 2026-10-16 by a Claude session (`cece/gallant-bell-eq7kul`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md

### T-20261016-acc7 · open · test · Brittle Star
- **Task:** Profile a Brittle Star Space-10 burst: heatCapacity x3.33 lets five guns fire ~20 s at 450 rounds/s with 9 s flights, so ~4,000 rounds can be alive (~2,000 at Space 0, 6 s bursts) against the turrets' pool max of 2,400 (GenericPoolManager then instantiates on demand and destroys on release). Capture with 3-4 AI Brittle Stars at Space 10 and decide: raise the pool, shorten the flight, or cap the burst.
- **Done when:** A capture (capture console command) attached to the task, and the pool or the numbers changed, or kept with the measurement as the reason
- **Who can take it:** Yash (offered: diagnostics), Garrett decides
- **Added:** 2026-10-16 by a Claude session (`cece/gallant-bell-eq7kul`)
- **See:** BRITTLE_STAR.md section 3.4 Cost and F9

### T-20261017-60e9 · open · test · Prisms
- **Task:** Editor-verify the prism destruction and steal detail levels (cece/dreamy-pascal-7xxx1m, lab rounds 9-12): a close death bursts into silver triangles that lean along the hit; neighbours on a trail rammed at once do not play the same animation, yet each cloud stays one smooth burst (round 12; fragmentVariety 0 makes them copies); a FAR death is 24 wedges on the same law (not the old explosion graph); a big trail wipe draws every death; the steal's roll looks as before. Frame Debugger: destructions draw with keyword _BURST_FRAGMENT, steals with _BURST_ROLL, the roll's mesh is Prism_HighPoly_<n>. Run EditMode PrismCollisionBurstTests.
- **Done when:** Checklist items 9-12 in Docs/UNITY_VERIFICATION_CHECKLIST.md pass (or each failure is filed as a dev task) and PrismCollisionBurstTests are green
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor
- **Added:** 2026-10-17 by a Claude session (`cece/dreamy-pascal-7xxx1m`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Prism movement section, items 9-12); Docs/Studios/COLLISION_BURST_LAB.md rounds 11-13

### T-20261017-9cab · open · decision · Prisms
- **Task:** Three calls the round-11/12 burst leaves: (1) round 13 tripled the spread (burst 0.36 / drift 0.216): the level-0 cloud is now 2.57 R at 2 s against the old explosion's 1.78 R - confirm in game; (3) round 12's per-death variety (fragmentVariety 0.5): judge it on the lab's Neighbours view - lower it if the deaths read too unalike; (2) your decision set the grade length to 2.2 s, but that is MaterialPropertyAnimator.DefaultTransitionSeconds, every prism colour change in the game - not adopted; adopt or not.
- **Done when:** All three recorded in the Collision Burst Lab decision log and the asset / animator updated to match
- **Who can take it:** Garrett (prisms / product owner)
- **Added:** 2026-10-17 by a Claude session (`cece/dreamy-pascal-7xxx1m`)
- **See:** Docs/Studios/COLLISION_BURST_LAB.md round 11, https://claude.ai/artifact/F47yG1UjBkK662rEnhk7m9

### T-20261017-1249 · open · dev · Tooling
- **Task:** unity_refcompile's editor config reports 3 errors in AuthenticationServiceFacade.cs (Unity.Multiplayer.PlayMode / CurrentPlayer, from 296123a9e) as PROJECT errors on a clean bleeding-edge, so every branch's editor run reads FAILED. Unity.Multiplayer.Playmode is already an unobtainable package: map that namespace to it (note the case difference) in Tools/Build/unity_refcompile so they land in the unobtainable-package bucket. Do not change the game file.
- **Done when:** A clean bleeding-edge editor run reads RESULT: OK with those 3 in the unobtainable bucket, and a planted typo in a changed Editor file still fails it
- **Who can take it:** Shombith (CI), Yash (offered)
- **Added:** 2026-10-17 by a Claude session (`cece/dreamy-pascal-7xxx1m`)
- **See:** Tools/Build/unity_refcompile/README.md

### T-20261017-75d5 · open · docs · Prisms
- **Task:** Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs has two back-to-back summary blocks (near lines 666 and 2220 on cece/dreamy-pascal-7xxx1m; already on bleeding-edge): a member was inserted between a doc comment and its method, so the doc now sits on the wrong member. Move each onto the method it describes or delete it if superseded. Doc comments only.
- **Done when:** A scan for '/// </summary>' followed by '/// <summary>' finds nothing in the file
- **Who can take it:** Yash (offered), anyone
- **Added:** 2026-10-17 by a Claude session (`cece/dreamy-pascal-7xxx1m`)
- **See:** .claude/skills/ship/SKILL.md section 2 (inserting a member above a method)

### T-20261017-b06e · open · decision · Crew vessels
- **Task:** Decide how the Kite Navigator seat goes online: today the board, motes, AI and every effect run on the hull's local human's machine. A second human on another machine needs the seat replicated, and a choice of which machine decides pops, blasts, Mass clusters, boost and steals (vessel rule 48). Also decide the arena case: the seat's swap uses PilotSwapGesture, which in an arena match also asks the arena to swap hulls while the human flies.
- **Done when:** A written plan in KITE_NAVIGATOR.md that names the deciding machine per effect and the gesture owner in arenas
- **Who can take it:** Garrett with Yash (party / multiplayer)
- **Added:** 2026-10-17 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Follow-ups)

### T-20261017-efca · done · dev · Crew vessels
- **Task:** When the duo-vessel system (cece/sharp-tesla-j0kd7z) reaches bleeding-edge, register the Kite Navigator as a duo design so a hull can carry it from a mode or a menu, not only from the play-mode FrogletTools menu; keep its one swap (PilotSwapGesture) and its AI-in-every-empty-seat rule.
- **Done when:** A duo-vessel entry seats the kite on a hull with no editor menu
- **Who can take it:** Garrett (duo-vessel owner)
- **Added:** 2026-10-17 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md; .claude/skills/duo-vessel/SKILL.md on cece/sharp-tesla-j0kd7z
- **Done:** 2026-10-18, Done on cece/sharp-tesla-j0kd7z (2026-10-18): the Wrasse is the duo-vessel skill's second design, and both crew seats (KiteNavigator, GobyPistolDuo) implement ICrewSeat and share CrewSeat (wake, PilotSwapGesture swap, AI hand-over), CrewPictureInPicture and CrewViewRender; check_crew_vessels.py holds every crew hull to the card / roster / container rules. The Wrasse already reaches the game from the Toybox Vessel Changer; a mode carrying a crew hull is the networking decision (T-20261017-b06e).

### T-20261017-3cbe · open · content · Crew vessels
- **Task:** Author FMOD events for the Kite Navigator's eight empty EventReference slots on KiteNavigatorConfig.asset: loop closed, loop fizzled, prism popped, Mass cluster, Time boost, Charge, steal, fauna struck.
- **Done when:** Every slot holds a real event and plays in a playtest
- **Who can take it:** anyone who does audio
- **Added:** 2026-10-17 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md; Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset

### T-20261017-aca2 · open · test · Crew vessels
- **Task:** Playtest the Wrasse in the Unity Editor: open the Toybox, fly the Wrasse station in the Vessel Changer, then the 9 verify steps of its checklist entry (board draws, AI loops, 2/D-pad right swaps to the kite, each element's loop fires, a charged ram steals and scores, 1 swaps back with no stuck input or boost, the changer swaps cleanly in and out of the Wrasse). It has only been compiled headlessly.
- **Done when:** The checklist entry is marked verified, or each failing step is a dev task naming what was seen
- **Who can take it:** Yash (offered), anyone with the Editor
- **Added:** 2026-10-17 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md; Docs/UNITY_VERIFICATION_CHECKLIST.md (Kite Navigator entry)

### T-20261017-cb31 · open · test · vessels
- **Task:** Fly the Shrike in Menu_Main freestyle and run its editor checklist (modes on X/RB/B, LT eyepiece with crosshairs, RT per mode, docking, Mass detach, crystal flora, missile count)
- **Done when:** Every step of the Shrike entries in Docs/UNITY_VERIFICATION_CHECKLIST.md is ticked or filed as a bug
- **Who can take it:** Yash, or Garrett (vessels)
- **Added:** 2026-10-17 by a Claude session (`cece/affectionate-clarke-s738vh`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Shrike entries); Assets/_Scripts/Controller/Vessel/R_VesselActions/SHRIKE.md

### T-20261017-868c · open · test · vessels
- **Task:** Shrike in a 2-player match: docking, nests, flora missiles and the lance agree on both machines
- **Done when:** Two peers see the same docks, nests and missiles; scored hits land once
- **Who can take it:** Yash (multiplayer)
- **Added:** 2026-10-17 by a Claude session (`cece/affectionate-clarke-s738vh`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/SHRIKE.md section 5

### T-20261017-114a · open · decision · vessels
- **Task:** Shrike level-5 upgrades for Charge, Mass and Time (Space is Void Pierce)
- **Done when:** Three upgrades named in the Shrike ability map
- **Who can take it:** Garrett
- **Added:** 2026-10-17 by a Claude session (`cece/affectionate-clarke-s738vh`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/SHRIKE.md section 7

### T-20261017-d598 · open · dev · vessels
- **Task:** Bring the Shrike Studio artifact up to the Unity redesign (modes on RT, LT eyepiece, crystal flora, Mass detach) and give it one minigame per element
- **Done when:** shrike.html plays the shipped controls and has Time race, Space destruction, Mass creation, Charge dogfight
- **Who can take it:** Garrett (vessels), any session
- **Added:** 2026-10-17 by a Claude session (`cece/affectionate-clarke-s738vh`)
- **See:** Docs/Studios/SHRIKE.md section 0 and 6; .claude/skills/labmaker/SKILL.md section 1.5

### T-20261016-92b0 · done · decision · Regatta AI
- **Task:** cece/relaxed-hopper-r62hbd carries a second, older RampBoostAIPolicySO for the Rhino than bleeding-edge's - pick one before it merges (learn_drives_from_flights.py writes only fields both share, so either works)
- **Done when:** The choice is recorded in Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md and the other branch is reconciled
- **Who can take it:** Yash with Garrett (Rhino owner sessions)
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9
- **Done:** 2026-10-16, Yash 2026-10-17: no decision step; the Ys-bleeding-edge RampBoostAIPolicySO was rebuilt (brake on the ramp, corner speed, payback) and took the re-arm + explicit end from the cece/relaxed-hopper-r62hbd cut. When that branch merges, keep this file's states and that branch's executor changes (AI_BOOST.md).
- **Done:** 2026-10-18, merge 5b09c6896 took bleeding-edge's RampBoostAIPolicySO wholesale (a superset of this branch's re-arm + End), Rhino.prefab points at its asset guid ece63fe0fab446188650420e8b018690

### T-20261016-b525 · open · test · Regatta AI
- **Task:** Fly Regatta I1 as the Stoat, 5+ finished races, storing every one: Mass mode's dipole on the straights, Time mode (B) slings into the hard corners; in one race flip Third Eye > Pilots > AI on your Stoat for a lap and back. Then ask a session to run learn_drives_from_flights.py --write (writes StoatModeAction / StoatDipoleConfig / StoatSlingConfig _Regatta)
- **Done when:** 3+ finished Stoat Regatta flights on bugledger/<you> and the checklist entry 'The Stoat races Regatta' steps 1-4 pass
- **Who can take it:** Yash
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md; Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9

### T-20261016-822d · done · dev · Multiplayer
- **Task:** Find and fix what the first real multiplayer logs showed: B31 (the party session refresh is rate-limited about every 10 s while a party exists), B32 (after a guest leaves mid-match, the host's party lobby answers NotInLobby and the host sits in Reconnecting) and B33 (in the editor, a second Play starts inside the first Play's dead party session, because the project skips the domain reload).
- **Done when:** Each of B31-B33 in Docs/PartySystem/BUGS.md is root-caused and fixed, or closed with the reason, and a new two-machine log (net_logs.py show) shows it gone
- **Who can take it:** Yash (party owner)
- **Added:** 2026-10-16 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/PartySystem/BUGS.md B31-B33; Caleb's logs on bugledger/belac (python3 Tools/Build/net_logs.py collect)
- **Done:** 2026-10-17, Root-caused and fixed 2026-10-17 (B31 GET budget, B32 presence leave deleted the shared lobby + stuck Reconnecting, B33 GameDataSO session carried across Plays); edit-mode tests on Amoebius. The proof by log is the retest task.

### T-20261016-c76e · done · test · Multiplayer
- **Task:** Two-machine repro for B32 with both logs: host and guest party up on two PCs, start a Regatta match, the guest leaves mid-race, then the host waits 2 minutes in the menu and stops Play. Both editors send their logs by themselves.
- **Done when:** net_logs.py collect shows ONE test with both machines (host/main and guest/main), and the host's timeline says whether it left Reconnecting
- **Who can take it:** Caleb, Yash (offered)
- **Added:** 2026-10-16 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/PartySystem/BUGS.md B32; Docs/UNITY_VERIFICATION_CHECKLIST.md 'Multiplayer test logs' steps 7-9
- **Done:** 2026-10-17, Folded into the B31-B33 retest task, whose step 3 is this repro with the fix in place.

### T-20261016-01ec · open · test · Regatta AI
- **Task:** Run RhinoRampDriveTests and watch an AI Rhino race Regatta I1-I4 in the Third Eye: it should ramp on straights, brake ON the ramp before each ring, and only let go round sharp corners
- **Done when:** the tests pass and the four times are written into REGATTA_AI_ROADMAP.md's Rhino row
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-16 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (The Rhino AI brakes on the ramp)

### T-20261016-b719 · open · test · Regatta AI
- **Task:** Run DolphinChargeDriveTests and watch an AI Dolphin race Regatta I1-I4 in the Third Eye: after each discharge it should drift again near its top speed (Third Eye: 'drifts again at the peak'), and every Regatta AI should thread rings through their centre without circling
- **Done when:** the tests pass and the four Dolphin times are written into REGATTA_AI_ROADMAP.md
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-16 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (The Dolphin AI drifts again at the discharge's peak)

### T-20261016-630e · open · test · Regatta AI
- **Task:** Check that no Regatta AI targets a crystal: run RegattaNoCrystalTargetTests (edit mode), then race Regatta with a Rhino and other AI seats in the Third Eye; no readout says 'Crystal on the way' and no AI bends toward an omni crystal
- **Done when:** both tests pass and a full Regatta race shows every AI aiming only at rings or its rail
- **Who can take it:** Yash, anyone with the Unity Editor
- **Added:** 2026-10-16 by a Claude session (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/Controller/Arcade/REGATTA.md §6

### T-20261016-d177 · open · test · Regatta AI
- **Task:** Watch an AI Manta race Regatta I1-I4 in the Third Eye: it should hold Soar (~700 u/s) through most of each leg, pivot only in hard turns, and thread rings without circling; run RegattaDriveTuningTests
- **Done when:** tests pass and the four Manta times are written into REGATTA_AI_ROADMAP.md
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-16 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (The Manta AI holds Soar deeper into turns)

### T-20261017-1809 · open · dev · Performance
- **Task:** Performance regression in MinigameRegatta|MinigameRegatta (belac, 2026-10-16 13:10:56): marker Inl_UniversalRenderTotal 0.9318 -> 1.3824 ms/frame (+48%)
- **Done when:** a new capture of MinigameRegatta|MinigameRegatta compares within its baseline (perf_hunt.py compare)
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-17 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md

### T-20261017-2129 · open · dev · Performance
- **Task:** Performance regression in MinigameRegatta|MinigameRegatta (belac, 2026-10-16 15:13:50): gcKB 47.01902389526367 -> 89.48501586914062 (+90%)
- **Done when:** a new capture of MinigameRegatta|MinigameRegatta compares within its baseline (perf_hunt.py compare)
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-17 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md

### T-20261017-19f0 · open · dev · Performance
- **Task:** Performance regression in MinigameGrizzlyCharge|MinigameGrizzlyCharge (mrglo, 2026-10-16 15:42:28): marker LateBehaviourUpdate 0.3973 -> 0.6018 ms/frame (+51%)
- **Done when:** a new capture of MinigameGrizzlyCharge|MinigameGrizzlyCharge compares within its baseline (perf_hunt.py compare)
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-17 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md

### T-20261017-0026 · open · test · Regatta / launch panel UI
- **Task:** Hand-test the AI seat chip hull labels on Regatta's launch panel: hover name, (i) badge, vessel info card (Docs/UNITY_VERIFICATION_CHECKLIST.md, 2026-10-17 entry)
- **Done when:** All six verify steps pass in the editor; the checklist entry is marked green
- **Who can take it:** Yash (offered); Shombith (UI prefabs, if authored art is wanted)
- **Added:** 2026-10-17 by a Claude session (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/UI/Elements/VesselInfoPopup.cs

### T-20261017-53f4 · open · decision · Dolphin
- **Task:** Two Dolphin calls were made for now on 2026-10-17 so the game stays fun while you are away: (1) keep the shipped SCALAR model + holdSpeedWhileDrifting (the docs said vector + Locked; driftThrottlePolicy reset to Live since only the vector model reads it); (2) a drift KEEPS boosted speed (re-drift at a discharge's peak holds ~347 u/s), kept as a skill move the AI also uses. Confirm or change either
- **Done when:** Garrett confirms both, or the change he wants is made (cap: clamp _heldDriftSpeed to cruise and revert the Dolphin AI peak rule; vector: vectorFlightModel 1 + driftThrottlePolicy 1 + holdSpeedWhileDrifting 0, then play-test)
- **Who can take it:** Garrett (Dolphin design owner)
- **Added:** 2026-10-17 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md §2 and §2a

### T-20261017-8cc0 · open · test · Regatta AI
- **Task:** Watch the AI hulls race their own lines in Regatta (Third Eye, trails on): Rhino, Manta, Dolphin, Stoat should fly between two rails outside the braid's core, never crossing a rail, the Rhino widest; run RegattaRaceLineTests; then fly each hull yourself a few races so learn_drives_from_flights.py can set its line from you
- **Done when:** tests pass, the lines look right on I1-I4, and each hull has 3 finished flights
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-17 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Every Regatta AI hull races its own line)

### T-20261017-2215 · open · test · Performance
- **Task:** Try the after-play confirmation and the Project Record in Unity (2026-10-17): stop Play after a run that leaves something new; the small window says what went to Claude (SENDING -> SENT) and closes by itself; Project record ▸ expands into Overview / Performance / Network / AI flights / Errors with Ys-bleeding-edge and bleeding-edge's performance status and your sends; also FrogletTools > Diagnostics > Project Record. Say what to change.
- **Done when:** The six steps of Docs/UNITY_VERIFICATION_CHECKLIST.md (2026-10-17, after-play confirmation) pass, or what to change is written here
- **Who can take it:** Yash (asked for it)
- **Added:** 2026-10-17 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/DIAGNOSTICS.md (The after-play confirmation and the Project Record)

### T-20261017-e2c3 · open · decision · AI training
- **Task:** Before storing AI training flights in UGS, read Docs/AI_SYSTEM/AI_DATA_IN_UGS.md and talk to Yash or Garrett: per-race summaries in Cloud Save, full flights unchanged in a file store, lossless compression only
- **Done when:** the storage choice is recorded in AI_TRAINING_PLAN.md §5.2 (with T-20261015-0064)
- **Who can take it:** Shombith (UGS owner), with Yash and Garrett
- **Added:** 2026-10-17 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/AI_DATA_IN_UGS.md

### T-20261017-5109 · open · test · Refactor
- **Task:** Open Ys-bleeding-edge in the Unity Editor once after the 2026-10-17 basic refactor pass (enum numbering, TryGetComponent, 3 inspectors moved to Assets/_Scripts/Editor, dead Find branches): let it compile, then open a CameraSettingsSO, a ShapeDefinition and a UniversalStatsProvider and check each custom inspector still draws.
- **Done when:** Editor compiles with no new console errors and the three custom inspectors draw; note the result in Docs/REFACTOR_BACKLOG.md section 0.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-17 by Yash (session) (`Ys-bleeding-edge`)
- **See:** Docs/REFACTOR_BACKLOG.md section 0

### T-20261017-4c56 · open · dev · Refactor
- **Task:** Delete the measured-unreferenced vestiges the 2026-10-17 session could not delete (blocked by its permission check): Assets/_Scripts/System/Architectures/ (LoginEventBus.cs, a static event bus nothing calls, plus folder metas) and the stale namespaced copy Assets/_Scripts/Controller/IO/_Input Mapping/InputActionsAsset.cs (+ folder meta; the live generated wrapper is Game/IO/_Input Mapping/InputActionsAsset.cs). Also drop the Controller/IO/_Input Mapping/ entry from Tools/Build/check_console_logging.py TOOL_PATHS.
- **Done when:** git rm'd on Ys-bleeding-edge, Unity compiles, check_console_logging.py 0 problems.
- **Who can take it:** Yash (offered)
- **Added:** 2026-10-17 by Yash (session) (`Ys-bleeding-edge`)
- **See:** Docs/REFACTOR_BACKLOG.md section 0 row S17

### T-20261017-bb32 · open · test · Multiplayer
- **Task:** Retest the B31-B33 party fixes in the editor: (1) Play, stop, Play again - no 'lobby not found' and no 'Relay session not ready' on the second Play; (2) two machines in a party, 10 minutes in the menu - no 'rate-limited' lines; (3) the player who went online first leaves a Regatta match mid-race and stops Play - the other stays InParty.
- **Done when:** The four steps of the checklist entry 'Party: B31-B33 fixed' pass with both machines' logs sent (net_logs.py collect shows them), or a failure is noted with its log
- **Who can take it:** Caleb, Yash (offered), Andrew
- **Added:** 2026-10-17 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Party: B31-B33 fixed); Docs/PartySystem/BUGS.md B31-B33

### T-20261017-1154 · open · test · QA
- **Task:** EditMode Run All on Ys-bleeding-edge (or a bleeding-edge that contains it): the 23 remaining DT-003 failures were fixed offline 2026-10-17 without an editor
- **Done when:** Window > General > Test Runner > EditMode > Run All is fully green (or only CrystalHullFusionBakeTests.ShippedBakes_AreCurrent red, with its message pasted into DT-003)
- **Who can take it:** Yash (offered), Caleb or Andrew (QA)
- **Added:** 2026-10-17 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/QA/DEV_TASKS.md DT-003

### T-20261017-9025 · done · test · Vessel models
- **Task:** Open branch ccr-d93a6dfb-ugbxng in the Editor after the seven vessel models were rebuilt in place by /vessel-model (2026-10-17): let the FBX files reimport with no console warning, open the Squirrel / Dolphin / Sparrow / Serpent / Manta (and Falcon, Shrike, Termite) prefabs, run FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs (four REAL shapes on each live file), Audit Vessel Construction and Audit Vessel Tails and Jets, re-bake Crystal Hull Fusions for the Squirrel, Dolphin and Manta, then fly each hull in freestyle: pitch / yaw / roll / throttle move the whole hull, the domain colour lands on the domain slot, glass and engine mouths read, each element morph is nameable
- **Done when:** (2026-10-18: scope is now the SQUIRREL only; the other six files are the artist's again) the Squirrel checks in Docs/UNITY_VERIFICATION_CHECKLIST.md's 2026-10-18 paragraph (round 8) are ticked - every take moves like the artist's Squirrel (rear engines included, roll banks the whole wing), the spine follows the chevrons, Mass shows - or the failing step is written there
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor; Garrett judges the look
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md; Docs/VesselModels/README.md; Docs/VesselModels/sheets/ (before/after)
- **Done:** 2026-10-18, Garrett, 2026-10-18: the round-8 Squirrel passes in the Editor (elements upgraded, domain changed, indistinguishable from the original); the other six vessels are the artist's files, unchanged

### T-20261017-3b6b · done · dev · Vessel models
- **Task:** Swap the Urchin and Rhino prefabs from their part-per-mesh placeholders (Urchan_Test.fbx, Rhino_Test.fbx) onto the rebuilt rigs urchan_shapekey_with_animations.fbx and rhino_shapekey_with_animations.fbx with FrogletTools > Vessels > Wiring > Swap Vessel Rig, which moves the geometry, remaps the materials and makes the animation rig-safe (Docs/VESSEL_CONSTRUCTION.md); the rigs now carry four REAL element morphs where the artist rigs had none
- **Done when:** Both prefabs draw one SkinnedMeshRenderer from the rig file, Audit Vessel Elemental Morphs reports four REAL shapes on each, the jets and colliders sit where they did, and a flight in freestyle shows the puppetry (UrchinAnimation / RhinoAnimation resolve their parts by bone name)
- **Who can take it:** Garrett (Urchin and Rhino owner), Yash (offered)
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/Urchin.md section 8; Docs/VesselModels/Rhino.md section 8; .claude/skills/vessel-model/SKILL.md section 6
- **Done:** 2026-10-18, 2026-10-18 fleet pass: both prefabs now draw the artist rigs with four real morphs (specs/urchin.py, specs/rhino.py), done offline without the swapper; the bones follow the placeholder parts, which keep the colliders. Editor test is the new Urchin/Rhino task.

### T-20261017-eecd · done · decision · Vessel models
- **Task:** Judge the seven rebuilt vessel models against the artist files on the before/after contact sheets in Docs/VesselModels/sheets/ (and in the Editor after the reimport task): for each hull say keep, iterate (name the three biggest faults) or revert the file (git checkout the FBX restores the artist file; the .meta is untouched). Also say whether the C# procedural hulls (Scarab, Stoat, Thresher, Butterfly, Brittle Star HullForm/HullBuilder) should be replaced by /vessel-model specs next
- **Done when:** A line per hull in Docs/VesselModels/README.md or in the session that iterates them
- **Who can take it:** Garrett
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/sheets/; .claude/skills/vessel-model/references/LESSONS.md
- **Done:** 2026-10-18, Superseded: the 2026-10-17 rebuilds were withdrawn; the fleet pass (2026-10-18) reproduced or replaced every hull; the judging is now the per-vessel flight tasks.

### T-20261018-13dd · open · test · Vessel models
- **Task:** Fly the rebuilt Dolphin on ccr-d93a6dfb-ugbxng (2026-10-18, /vessel-model loop round 4): it should look and fly exactly like the original; upgrade its elements and change its domain as on the Squirrel; re-run the Dolphin hull-fusion bake if the crystal fusion looks off
- **Done when:** Garrett says it passes as the Squirrel did, or the difference is written in Docs/UNITY_VERIFICATION_CHECKLIST.md's Dolphin paragraph
- **Who can take it:** Garrett (judges); Yash (offered) can run the Editor steps
- **Added:** 2026-10-18 by Claude session for Garrett (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md; Docs/VesselModels/sheets/dolphin_after.jpg

### T-20261018-f892 · open · test · Butterfly model
- **Task:** Fly the new Butterfly.fbx in the Editor: four wings beat from their hinges, domain colour on the wings (change domain), Mass mode spreads, the Fold closes the wings OVER THE BACK with a butterfly ghost, each element visibly changes the hull; run Audit Vessel Elemental Morphs + Audit Vessel Construction
- **Done when:** the owner says it meets the fleet's look and plays like the placeholder did (or names what to change)
- **Who can take it:** Garrett (owner's eye); Yash can run the audits
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Butterfly entry); Assets/_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY.md section 2

### T-20261018-5511 · open · decision · Butterfly model
- **Task:** After the Butterfly model is accepted: delete the procedural ButterflyHullBuilder / ButterflyHullForm and Tools/Build/butterfly_hull_harness (keep its Stubs.cs if other harnesses share it), and confirm the ButterflyAnimation wing sign fix (the Fold used to close under the belly) and the model's 41% domain share
- **Done when:** the placeholder code is gone and the two calls are recorded in BUTTERFLY.md section 2
- **Who can take it:** Garrett
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (`ccr-d93a6dfb-ugbxng`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY.md section 2

### T-20261018-9fcf · open · test · vessels
- **Task:** Editor-verify the Angler (vessel 17): run the seven Angler entries in Docs/UNITY_VERIFICATION_CHECKLIST.md (rounds 1-7) in Menu_Main freestyle - lure/tame on A, paint RT, erase/lock LT, dash B, squad X, recall Y, the lit queue, the level camera
- **Done when:** Every Angler checklist entry passes with a clean console, or each failure is filed as a dev task
- **Who can take it:** Yash (editor tests), Garrett (vessel owner)
- **Added:** 2026-10-18 by a Claude session (`cece/zealous-feynman-po8k8d`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/ANGLER.md

### T-20261018-bf51 · open · decision · vessels
- **Task:** Angler design slots still open: Charge and Time level-5 upgrades, and element scaling for Charge, Mass and Space (only Time scales today)
- **Done when:** Garrett names the four, or says they stay open
- **Who can take it:** Garrett
- **Added:** 2026-10-18 by a Claude session (`cece/zealous-feynman-po8k8d`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/ANGLER.md section 7

### T-20261018-5d70 · open · decision · Brittle Star
- **Task:** The Brittle Star's base trail lays blocks HALF as wide as its Creation corner trails (0.5 x 1 x 6 against 1 x 1 x 6): the fleet spawner halves BaseScale.x for a single trail, the corners do not. Pick: halve the corners' x, or author the base block as (2, 1, 6).
- **Done when:** Garrett picks one and the corners and base trail lay the same block (BRITTLE_STAR.md R3)
- **Who can take it:** Garrett
- **Added:** 2026-10-18 by a Claude session (`cece/gallant-bell-eq7kul`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md R3

### T-20261018-3ec7 · open · test · Vessel models
- **Task:** Fly the six NEW hulls on ccr-d93a6dfb-ugbxng: Thresher, Scarab, Stoat, Angler, Brittle Star, Shrike (spaceships inspired by their clades, replacing the procedural C# hulls). For each: reimport the FBX, run Audit Vessel Elemental Morphs + Construction + Tails and Jets, fly it, change domain, upgrade each element, and check the puppetry signs listed per vessel in the checklist
- **Done when:** each hull is accepted or its faults are named (one line per hull in Docs/VesselModels/README.md)
- **Who can take it:** Garrett (owner's eye); Yash (offered) can run the Editor steps and audits
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (fleet pass) (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (fleet pass entry); Docs/VesselModels/README.md; Docs/VesselModels/sheets/

### T-20261018-97b6 · open · test · Vessel models
- **Task:** Fly the Urchin and Rhino rig swaps: the prefabs now draw the artist's rigs (urchan_/rhino_shapekey_with_animations.fbx) with four real element morphs. Reimport, check the drawn pods/plates stay inside their colliders in every pose, plumes come out of the drawn mouths, the domain lands on the accent slot, each element morphs the hull
- **Done when:** both pass in the Editor, or the differences are written in the checklist's fleet-pass entry
- **Who can take it:** Garrett (Urchin and Rhino owner); Yash (offered)
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (fleet pass) (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (fleet pass entry); Docs/VesselModels/README.md; Docs/VesselModels/sheets/

### T-20261018-987b · open · test · Vessel models
- **Task:** Fly the reproduced Sparrow, Serpent and Manta (2026-10-18): the FBXs are rebuilt by the pipeline and proven identical to the artist files (every take, morph, material and node); they should look and fly exactly as before
- **Done when:** Garrett says they are indistinguishable, as the Squirrel was
- **Who can take it:** Garrett; Yash (offered)
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (fleet pass) (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (fleet pass entry); Docs/VesselModels/README.md; Docs/VesselModels/sheets/

### T-20261018-1432 · open · setup · Vessel models
- **Task:** Run FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions on ccr-d93a6dfb-ugbxng after the reimport and push the Thresher, Scarab, Brittle Star, Urchin and Rhino bake assets: they are stale (keyed to the old placeholder meshes) and solve at runtime until re-baked
- **Done when:** CrystalHullFusionBakeTests.ShippedBakes_AreCurrent is green with the new hulls
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (fleet pass) (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (fleet pass entry); Docs/VesselModels/README.md; Docs/VesselModels/sheets/

### T-20261018-2070 · open · decision · Vessel models
- **Task:** Fleet-pass calls: (1) Scarab: connected modules vs the 'floating parts' reading of your 2026-09-01 note; its elytra open on the INSIDE of a turn in code, the OUTSIDE in the doc. (2) Stoat: the tail curls with the bend in code, 'swings out' in the doc. (3) Shrike: a new talon grip while docked (keep, change, drop). (4) Angler: first-cut swim/jaw/esca numbers; its hit box is still the Stoat's (narrower than the new bow). (5) Brittle Star: flames wear the Engine slot (the Fire ember since the 2026-10-18 colour pass, Docs/VesselModels/COLOR.md), the arms curl one way (chiral). (6) Butterfly 41% domain share. (7) When each placeholder (HullForm/HullBuilder/harness) is retired
- **Done when:** each call is answered and recorded in the vessel's doc
- **Who can take it:** Garrett
- **Added:** 2026-10-18 by Claude session ccr-d93a6dfb (fleet pass) (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (fleet pass entry); Docs/VesselModels/README.md; Docs/VesselModels/sheets/

### T-20261018-d3e5 · open · test · Vessel colour
- **Task:** Look at the new vessel colour scheme in the Editor: fly each domain (Jade, Ruby, Gold) at the chase camera and in the hangar on a few hulls (Squirrel, Rhino, Manta, Thresher). Check the body for self-overlap ghosting (it is transparent with no depth write and now has a visible steel rim), the Manta/Termite/Falcon/Rhino domain colour on their accents and the ember on their engines (brighter on boost), and that every class card and hangar preview shows the new silver-plate render.
- **Done when:** Docs/UNITY_VERIFICATION_CHECKLIST.md's colour-scheme entry is ticked or an issue is filed; if the body ghosts, the hull moves to BlueBaseOpaqueVesselMaterial (Docs/VesselModels/COLOR.md section 6)
- **Who can take it:** Yash (offered), anyone with the Editor; Garrett for the look
- **Added:** 2026-10-18 by Garrett's session (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/COLOR.md

### T-20261018-be55 · done · decision · Vessel colour
- **Task:** Decide whether Jade's ship colours move from azure (hue 209, next to the neutral Blue windows and body at 220-232) to the teal of the palette's other Jade entries (about 177-182), and whether Gold's inverted rim/face (bright edge, dark face; Ruby and Jade run dark edge to bright face) should match the others. The UI reads the same ShipColor1/2, so it is a palette change, not a vessel one.
- **Done when:** OriginalColorSetSO JadeColors/GoldColors updated (or kept) and the decision recorded in Docs/VesselModels/COLOR.md section 6
- **Who can take it:** Garrett (colour language owner)
- **Added:** 2026-10-18 by Garrett's session (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/COLOR.md section 6
- **Done:** 2026-10-18, Gold fixed 2026-10-18 (rim/face swapped in OriginalColorSetSO and the unused Yellow materials, a data-entry mistake: DomainScorePanel reads ShipColor1 as the muted tone). Jade and Ruby hues deferred by Garrett; reopen as a new task when he takes them up.

### T-20261018-a0e2 · open · dev · Vessel colour
- **Task:** Move the Grizzly's jet renderers (Ship_Wedge_Jet_UL/UR/BL/BR and Ship_Wedge_Body slot 3) off LimeVesselMaterial onto EngineVesselMaterial: Lime is the call-to-action colour only, a jet exit is the Fire ember (Docs/VesselModels/COLOR.md).
- **Done when:** Grizzly.prefab references no LimeVesselMaterial; the jets flare red on boost
- **Who can take it:** jtgjones (Grizzly, grizzly-v3 in flight)
- **Added:** 2026-10-18 by Garrett's session (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/COLOR.md section 3

### T-20261018-0886 · open · test · Rhino
- **Task:** Editor-verify the Rhino four-row kit on cece/relaxed-hopper-r62hbd (never opened in Unity): compile, then run the 'Rhino deep dive' section of Docs/UNITY_VERIFICATION_CHECKLIST.md - sword energize/cooldown and Charge gauge, afterimages cut by the Rhino's own blade after 1 s, Time card lit on boost + Momentum flash + speed gauge, the L1+R1 and sticks-apart chips, Manta Soar chip, a Grizzly losing petals to a danger prism, and a 2-player check that a replica's sword/ramp match the owner's
- **Done when:** Every numbered check in that section passes, or each failure is filed as a dev task
- **Who can take it:** Yash (offered) or Caleb/Andrew (QA); Garrett for the balance calls
- **Added:** 2026-10-18 by a Claude session (`cece/relaxed-hopper-r62hbd`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Rhino deep dive); Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SWORD_AFTERIMAGE.md

### T-20261018-c5a4 · open · decision · Rhino
- **Task:** Playtest the Rhino afterimage self-farm: one energize (cost 0.1) then cutting its own wake can refill the sword meter now the blade cuts its own afterimages. Keep, or lower energy per own-afterimage cut (not immunity)
- **Done when:** A call is recorded in Docs/ElementalAbilitySystem/BACKLOG.md (Rhino four-row branch, Open)
- **Who can take it:** Garrett (vessel owner)
- **Added:** 2026-10-18 by a Claude session (`cece/relaxed-hopper-r62hbd`)
- **See:** Docs/ElementalAbilitySystem/BACKLOG.md 'Afterimage self-farm'

### T-20261018-2962 · open · dev · CI gates
- **Task:** check_vessel_on_vessel_motion.py is red on bleeding-edge: it flags VesselImpulseByExplosionEffectSO (the Grizzly's self-launch, which returns unless SourceVessel is the vessel hit). Teach the gate the self-only guard (or a reasoned allow-list entry) with a negative control; do not change the effect
- **Done when:** The gate exits 0 on bleeding-edge and still fails when the SourceVessel guard is deleted
- **Who can take it:** Yash (offered) or whoever owns the CI gates
- **Added:** 2026-10-18 by a Claude session (`cece/relaxed-hopper-r62hbd`)
- **See:** Docs/ElementalAbilitySystem/BACKLOG.md '[gate false positive] check_vessel_on_vessel_motion.py'

### T-20261018-3247 · open · dev · Crew vessels
- **Task:** Bring the Crew Vessel Lab up to the Wrasse's rounds 4-4f, which were directed in Unity on 2026-10-18 (KITE_NAVIGATOR.md "Where it stands"). They cover: the kite view and the pilot view swapping (the small view lower left); the kite following the mouse or stick with no click; a two-stick pilot who spends saved Time boosts on the right trigger (2 s cooldown, none at Time L5); lime five-petal motes in the totem's proportions, each moving like its element; mote frequency weighted by the hull's petals; Charge rams on fauna; Eject below Charge L5 and Steal at it; shielded Mass clusters at Mass L5; Space L5 blasts through shields and super shields; the Omni crystal's burst of motes; and out-of-plane prisms seen with depth from the plane. Then re-run parity.cjs and verify_lab.cjs and republish.
- **Done when:** The lab's SHIPPED block and scorecard carry round 4 and parity.cjs still passes
- **Who can take it:** any session; Garrett to sign off
- **Added:** 2026-10-18 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Round 4); Docs/Studios/CrewVesselLab.html

### T-20261018-49d0 · done · content · Crew vessels
- **Task:** Give the Wrasse its own HUD. It still wears the Sparrow's (SparrowHUDController + SparrowHUDVariant): the four-icon ability row shows the Sparrow's gun, missile and barrel-roll icons, though the Wrasse has none of those since round 4c. Its four icons should be the kite's four loop effects (Space blast, Mass cluster, Time boost, Charge ram), each with its L5 upgrade (LOCKED four-icon row). Also give it its own ElementalAbilityMap entries (Wrasse.asset is still the Sparrow's map, kept only for the L5 unlock levels).
- **Done when:** The Wrasse's HUD shows its own four icons and no Sparrow ability
- **Who can take it:** Garrett (vessels, icons); any session for the wiring
- **Added:** 2026-10-18 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Round 4c); Docs/claude/VESSEL_HUD_AND_ELEMENTS.md (four-icon row)
- **Done:** 2026-10-18, Round 4d: WrasseHUDController drives the four cards (saved boosts, rams, last blast, Mass flash) and Wrasse.asset has the Wrasse's own cards with no button chips. The icon ART is still the Sparrow's: see the follow-up task.

### T-20261018-8ff7 · open · content · Crew vessels
- **Task:** Draw the Wrasse's four ability-card icons. Since round 4d the cards read the navigator (WrasseHUDController: Time = saved boosts, Charge = kite rams, Space = last blast's kills, Mass = flash on a cluster) and ElementalAbilityMaps/Wrasse.asset names them Space Blast, Saved Boosts, Mass Cluster and Kite Ram, but the sprites on the HUD (SparrowHUDVariant) are still the Sparrow's gun, missile and stance icons.
- **Done when:** The Wrasse's HUD shows its own four icons (a Wrasse HUD variant of SparrowHUDVariant, made with Create > Prefab Variant), each with its L5 upgrade art, on the LOCKED four-icon row.
- **Who can take it:** Garrett (vessels, icons); any session for the variant wiring
- **Added:** 2026-10-18 by a Claude session (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md Round 4d

### T-20261018-5c29 · open · test · Crew vessels
- **Task:** Fly the Wrasse's round 4d in the Unity Editor: let BlockGraph and the swarm member shaders re-import with the new PrismDepthPlane.hlsl include, confirm the pilot's own view of prisms is unchanged, then run Docs/UNITY_VERIFICATION_CHECKLIST.md's Wrasse steps 2-4 (two-thumb flight, the HUD counts, the five-petal motes, out-of-plane prisms opening with distance from the plane and dithering above it).
- **Done when:** The checklist's Wrasse entry is ticked or its failures are filed, and the two-thumb tuning (the Sparrow's numbers today) is kept or re-tuned.
- **Who can take it:** Garrett (sign-off); Yash can run the editor pass
- **Added:** 2026-10-18 by a Claude session (`cece/compassionate-babbage-gc13io`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md Kite Navigator

### T-20261018-8b32 · open · test · Crew vessels
- **Task:** Fly both crew vessels in the Unity Editor after the crew unification (cece/sharp-tesla-j0kd7z, 2026-10-18): the Goby and Pistol (VesselClassType 18) and the Wrasse and Grouper, from the Toybox Vessel Changer. Check the six steps of Docs/UNITY_VERIFICATION_CHECKLIST.md 'One notion of a crew vessel': both cards name both subvessels, the seat view is a lower-left square while flying the hull and swaps with the pilot view on 2 / 1, the Pistol's foreground dithers instead of vanishing, and the Wrasse behaves exactly as before. Then the three steps of 'The Goby's vortex ring and flares are lit faceted volumes' (the ring shaded and faceted in both views, the flares lit, nothing pink).
- **Done when:** Both crew vessels flown in the editor, the checklist entry marked green or its failures filed
- **Who can take it:** Yash (offered), Garrett (sign-off), anyone with the Editor
- **Added:** 2026-10-18 by a Claude session (`cece/sharp-tesla-j0kd7z`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md

### T-20261018-7c2d · open · decision · Crew vessels
- **Task:** Decide whether the Pistol's view keeps its flat marks (primed/hot/held rings round prisms, the aim ring and line, the contact dot, the pops) as the view's own markings, or makes them lit volumes like the vortex ring
- **Done when:** Garrett's call is recorded in .claude/skills/duo-vessel/references/designs/goby-and-pistol.md (decisions log) and, if they stay flat, as an owner's-call exception under 'Nothing in the world looks flat' in Docs/claude/SHADERS_AND_PERFORMANCE.md
- **Who can take it:** Garrett (the rule's and the vessel's owner)
- **Added:** 2026-10-18 by Claude session on cece/sharp-tesla-j0kd7z (`cece/sharp-tesla-j0kd7z`)
- **See:** .claude/skills/duo-vessel/references/designs/goby-and-pistol.md; Docs/claude/SHADERS_AND_PERFORMANCE.md

### T-20261018-2c7e · open · dev · Vessel models
- **Task:** Work the nine follow-ups the ship review of the vessel-model branch left (Docs/VesselModels/README.md 'Follow-ups from the ship review'): one Editor check (BakeMesh scale), the Shrike beam origin under the Space morph, a Thresher pectoral sign question, two latent Rhino/VesselCustomization nits, two deletion proposals (GhostAction, the six procedural hull builders: check Amoebius first) and two per-spawn/per-frame perf trims.
- **Done when:** each row is fixed, filed or answered in the README's table
- **Who can take it:** Yash (offered), anyone; Garrett for the sign question and the deletions
- **Added:** 2026-10-18 by Garrett's session (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/README.md
