---
name: froglet-compute
description: Run automated AI tests on this computer with Unity - asks what to run (a vessel's game, an arena game over rounds and intensities, a voyage with a set squad, a trainer run) and what to watch, then drives Unity and reports.
---

# /froglet-compute: automated tests on this computer, with Unity

Froglet Compute's cloud queue is paused (Shombith, 2026-11-06). This skill does the same kind of work on **this**
computer, when the person asks for it. It drives the game's own harnesses. Yash drives them the same way when he runs
AI races in the open editor: the `unity` CLI, the Mode Smoke Test driver, the escort sim and the trainer. It adds no
harness and no game code.

**Start every run by asking (section 1).** Never start Unity work before the person has confirmed the plan.

## 0. Rules

- **It only reads the game.** Every runner here writes result files only (`Logs/LocalTest/`, `~/froglet-tests/`). None
  of them writes a vessel, a kit, a course, an end condition or an AI. The vanilla squad (Squirrel, Dolphin, Sparrow)
  must not break, so this skill never changes code or assets to make a test pass. If a test needs something the game
  lacks, it says what is missing and asks first (section 6).
- **A trained genome is a proposal.** Yash owns the AIs. A genome goes on the AI Training window's Archive tab only
  when the person says yes, and the deploy goes into Yash's `/approve` queue.
- **Never ask the person to run a command.** Run it yourself. Ask only for what you cannot do: open Unity, or take your
  hands off the editor while a run plays.
- **Report what ran, not what should have.** If a step timed out, was skipped or only half ran, say so with its file.
- One Unity per project folder. The open editor runs the **match** steps. Voyage and trainer steps need their own
  process, so they run in Froglet Compute's own copy of the project (`~/.froglet-compute/unity/Cosmic-Shore`) and
  never touch the person's checkout.

## 1. Ask first: the plan

Ask in one message, with an example drawn from their words:

1. **What to run, in order.** Each step is one of these kinds:
   - **match**: an arcade or arena game played by AI on every seat. Name the game or the vessel's element game
     ("Sparrow's Time game"), the intensities (1-4, or all), the rounds per intensity, the AI difficulty (Legendary by
     default), the seats, and the clock (1x to time a race, 4x for a quick pass).
   - **voyage**: an Ark escort with a set squad of three, all flown by the AI. Name the difficulty (1-9), the squad
     and the number of runs. Or choose the voyage scene's smoke test.
   - **train**: the game's AI trainer for one vessel, mode and intensity, with the number of matches (the
     `LOCAL_TRAINING.md` sheet's runs).
2. **What to watch.** Offer these, and take anything else they name:
   - match: finished or timed out, game seconds to the end, AI seats that moved, errors and exceptions *in the match*
     (boot errors are reported apart from these), wall time, frame rate (watched runs only).
   - voyage: how the escort ended, the Ark's hull at each world's entry, lowest and exit, peak threats, the world it
     fell in.
   - train: best fitness per generation, whether it rose, the replay of the best match, console errors.
3. **Headless or watched.** Watched runs open a window at game speed and record a performance report.

Resolve vessel names to games with `python3 Tools/Build/elemental_loop.py vessel <Vessel>`: it prints the vessel's
Time, Charge, Mass and Space games and its arena games. For example, Sparrow's Time game is Breakwater, Squirrel's is
Skim Race and Dolphin's is Switchback. Use the `GameModes` enum name (`Assets/_Scripts/Data/Enums/GameModes.cs`) as
the mode name.

Then show the plan back as a table, with one row per step, the runs in it and a time estimate. Ask "go?" once. Example
for "Sparrow's Time game, then Regatta for 5 rounds at 4 intensities, then a voyage with a set squad":

| # | Kind | What | Runs | Watch | Est. |
|---|---|---|---|---|---|
| 1 | match | Breakwater (Sparrow), I1-I4, 1 round each, Legendary, 3 seats, 1x | 4 | finish, game s, errors in match | ~10 min |
| 2 | match | Regatta, I1-I4, 5 rounds each, Legendary, 1x | 20 | finish, game s, moved seats, errors | ~60 min |
| 3 | voyage | Ark escort d5, Squirrel + Dolphin + Sparrow, 3 runs, 3x | 3 | how it ended, Ark hull, fell in | ~45 min |

"5 rounds" means five matches per intensity unless the person says otherwise. Ask once if it could mean the mode's
own rounds.

Save the confirmed plan as `Logs/LocalTest/<yyyy-mm-dd-hhmm>/plan.json` (`Logs/` is git-ignored) so the run can resume.

## 2. Before the first step

From the repository root:

```
git fetch origin && git status --short          # name what is uncommitted; never stash or reset it
python3 Tools/Compute/unity_run.py --find       # the editor this project needs
unity command                                   # match steps: lists commands when the open editor is connected
```

- **Match steps need the open editor and the `unity` CLI** (`Docs/unity-cli-setup.md`). If `unity` is missing or
  cannot connect, tell the person the one thing to do: open the project in Unity, or install the CLI. Then run the voyage
  and train steps while you wait.
- `unity command recompile`, then read its errors. A compile error stops the match steps. Report it, and do not fix it
  unless the person asks.
- The editor must not be in Play mode. Ask the person to keep their hands off it while the match steps play.
- **Argument passing.** CLI `1.0.0-beta.13` against package `0.5.0-exp.1` cannot pass arguments. Run `eval` through the
  editor's own server instead, as `Docs/unity-cli-setup.md` shows: the port and the token are in
  `Library/Pipeline/.unity-pipeline-port`. Read them into variables in a script, and never print the token or write it
  anywhere. Check `unity command eval --help` on this machine first: the installed version is the authority.

## 3. The runners

### 3.1 match: the Mode Smoke Test driver, in the open editor

`CosmicShore.Editor.ModeSmokeLauncher` (`Docs/LAUNCH/SMOKE_TEST.md`) does this. It enters Play from Bootstrap, offline,
launches the card through the real launch pipeline, hands every seat to the AI, presses Ready between turns and
waits for game over. Then it writes `summary.json` and `modes/<Mode>.json` (`sceneLoaded`, `turnStarted`, `seats`,
`aiSeats`, `movingSeats`, `gameEnded`, `turnsEnded`, `winnerCalculated`, `gameSeconds`, `wallSeconds`, and `errors` /
`exceptions` split into boot, match and exit, with the first 8 lines).

Its own `Run()` reads process arguments, so the open editor would play every mode with the shipped config. For one
step, do this instead:

1. **Make a scratch config** (eval). Copy the shipped config, `AssetDatabase.CopyAsset(ModeSmokeConfigSO.AssetPath,
   "Assets/Resources/Generated/LocalTest/<step>.asset")`. That folder is git-ignored; create it with
   `AssetDatabase.CreateFolder` if it is missing. Then set the step's values on the copy through `SerializedObject`:
   `defaultIntensity`, `defaultPlayers`, `aiDifficulty` (4 = Legendary), `timeScale`, `matchBudgetSeconds`, and
   `ApplyModifiedProperties` + `AssetDatabase.SaveAssets()`. **Never edit `Assets/_SO_Assets/Launch/ModeSmokeConfig.asset`.**
2. **Start one round** (eval). Call the launcher's `Start(modes, outDir, configPath, quit: false, out error)` by
   reflection (`BindingFlags.NonPublic | BindingFlags.Static`). Pass `modes` = the mode name, `outDir` =
   `Logs/LocalTest/<run>/<step>/I<n>-r<k>` and `configPath` = the scratch asset. If it returns false, report `error`
   and stop the step.
3. **Wait** for `summary.json` to show every planned mode out of `not_run`, and for the editor to be back in Edit mode.
   Check about every 30 s; never sleep in a loop for longer than the match budget. One round takes about 1-4 minutes
   at 4x and up to 10 at 1x.
4. Run the next intensity with the next scratch config, and the next round in a new `outDir`.
5. **Delete every scratch asset** when the step ends, or when it fails (`AssetDatabase.DeleteAsset`). Then check that
   `git status --short` shows no change you made, and `git checkout` any scene that Play mode left dirty
   (`Docs/unity-cli-setup.md`: a save right after Play can write Play-time values into the open scene).

What the driver cannot do (say so in the report when it matters):
- **It cannot choose the AI hulls.** The host takes the card's first hull, and the other seats are the card's own
  backfill. So "Regatta with Sparrows only", or with a set lineup, cannot be run with it (section 6).
- It records no winner's name, no lap times and no score. Report `gameSeconds` as the time to game over (the first
  finisher in a race), and grep `Editor.log` for the mode's own end lines if the person asks for more.
- Its own `status` fails a round on any boot error, such as the Cloud Code `NotFound` before the menu. Judge the match
  apart from that by its *match* error and exception counts, as Yash's runs did, and list the boot errors once.

### 3.2 voyage: the escort sim, in the project copy

`ArkEscortSim` reads process arguments, so it needs its own Unity process (`Docs/Voyages/ARK_ESCORT.md` section 3).

1. Prepare the copy at the person's current commit. This reuses the trainer's code, and the first time it imports the
   whole project, which takes an hour or more. Tell the person before it starts:
   ```
   python3 -c "import sys; sys.path.insert(0,'Tools/Compute'); import unity_run as u; print(u.prepare_project('$(git rev-parse HEAD)'))"
   ```
   It refuses to use the copy while another Unity has that copy open.
2. Start it. Use the editor that `unity_run.py --find` named, and the copy's path:
   ```
   "<Unity.exe>" -projectPath "<copy>" -executeMethod CosmicShore.Editor.ArkEscortSimLauncher.Run \
     -arkEscortSim "5:Squirrel,Dolphin,Sparrow;5:Squirrel,Dolphin,Sparrow;5:Squirrel,Dolphin,Sparrow" \
     -arkEscortOut ~/froglet-tests/<run>/escort.jsonl -arkEscortTimeScale 3 -arkEscortVerbose -arkEscortQuit \
     -logFile ~/froglet-tests/<run>/escort.log
   ```
   One `difficulty:three hulls` per run, separated by `;`. Add `-batchmode` for headless runs. For the voyage scene's
   smoke test, use `RunSmoke` with `-voyageSmoke -arkEscortQuit`.
3. Read the results with `python3 Tools/Build/ark_escort_report.py ~/froglet-tests/<run>/escort.jsonl`, and grep the
   log for `Exception` and `at CosmicShore` (`Docs/Voyages/HANDOFF.md` section 4). Exit 0 means every run finished.
   A single run is noisy: the same squad has differed by 20 points. Say so when there are fewer than 3 runs.

The escort sim is an all-AI Ark escort. No voyage *job* (a faction's work verb) has an all-AI driver yet. If the
person names a job, say so and offer the escort or the voyage smoke test.

### 3.3 train: the game's AI trainer, in the project copy

`Tools/Compute/unity_run.py` (Compute README section 16), exactly as `vanilla-squad/LOCAL_TRAINING.md` lays it out:

```
python3 Tools/Compute/unity_run.py --sha $(git rev-parse HEAD) --mode <TrainerToken> --vessel <Vessel> \
  --intensity <n> --episodes <matches> --minutes <budget> [--watch] --out ~/froglet-tests/<run>/<mode>-<vessel>-I<n>
```

The trainer tokens are not always the mode name: Skim Race is `HexRace`. The catalog is what
`python3 Tools/Compute/make_unity_job.py` reads. Read `status.json` while it runs. At the end, read `result.json`,
`genome.json` and `recordings.json`. A watched run also writes `bugledger.json` and a diagnostics report.

## 4. While it runs

- Update the plan file after every round: done, failed or skipped, and where its files are. A crash then resumes from
  the next round, not from the start.
- Tell the person only about a blocker (Unity closed, a compile error, a step that cannot start), and when the plan is
  done.
- A round that times out is a finding about the mode, not a reason to retry. Retry once only for a harness fault
  (`harnessFault` not empty, or the editor left Play mode early), and say that you retried.

## 5. The report

Write `Logs/LocalTest/<run>/report.md`, and give the person the short version: one table per step, one row per
intensity, the watched values as columns, rounds summarised as median / min-max, and the first match error of each
failed round. End with what stands out, such as "I4 is 40 s slower than I3" or "Regatta I2 timed out 2 of 5", and name
the one next thing worth doing. Do not lead with a list of questions.

If a result belongs in a doc (a mode's `.md` timing table, or `Docs/UNITY_VERIFICATION_CHECKLIST.md`), offer to add it.
Write it only on a yes. The doc's owner (`python3 Tools/Build/who_works_where.py query <path>`) hears of it through
`approvals.py add`.

## 6. When the game lacks something

Say what is missing in one line, and **ask before adding it**: it is a game change. Known gaps on 2026-11-26:
- The smoke driver cannot pick each seat's hull, so an arena lineup cannot be set. The fix would be an AI-hull list on
  the config's per-mode override, passed to `GameDataSO.SetRequestedAIVessels`.
- There is no public editor entry that takes modes, intensity and output without process arguments. Reflection on
  `Start` stands in for one. The clean form is a `[CliCommand]` wrapper under `Assets/_Scripts/Editor/`.
- There is no all-AI driver for a voyage job.
- The smoke report has no winner, laps or score.
