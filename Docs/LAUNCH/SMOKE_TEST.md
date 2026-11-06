# The per-mode smoke test

**Every mode a player can launch is played once, by the AI on every seat, from the Bootstrap scene to the game-over
event, with nothing logging an error.** Blocker 19 in `Docs/LAUNCH/READINESS.md` §10 ("a per-mode smoke run in
Unity"). It runs in the Unity editor, in Play mode, with a window or headless; Amoebius is not involved.

Status (2026-11-05): **built, not yet run in Unity.** The C# passes the reference compile and `smoke_report.py`
passes its self-test with its negative control; nobody has played a mode with it yet. The first run is the
2026-11-05 "Mode smoke test" entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## What one mode's run checks

One fresh Play session per mode, so a mode's fault cannot leak into the next:

1. **Boot.** Enter Play in `Bootstrap.unity`; wait (180 s) for the menu to hold a local pilot. Offline (a local host on
   127.0.0.1, the `CosmicShore.OfflinePreferred` preference, put back after the run), as the soak runners play. The
   first-run privacy overlay is closed unanswered and the username step skipped, exactly as `SwitchbackSoakRunner`
   does: nothing is recorded, analytics stay off.
2. **Launch through the real pipeline.** The arcade launch modal's `SyncAllGameDataForLaunch` for an all-AI match:
   `SyncFromArcadeGame(card)`, the card's first hull, the intensity, `ConfigurePlayerCounts(players, 1)`, the domain
   count, the AI difficulty, then `GameDataSO.InvokeGameLaunch()`, which `SceneLoader` answers as it does for a person.
3. **Scene loaded** (the check): the active scene is the card's `SceneName`, its `MiniGameControllerBase` exists and
   every seat has a vessel (180 s).
4. **AI on every seat** (the check): Ready is pressed through the public `OnReadyClicked` (the HUD's button) until a
   turn runs (120 s); the host's hull is handed to the AI with `PilotControl.TrySetAIFlies` (the Third Eye's AI-flies
   switch, which also arms the mode's own steering and the Skim Race pilot). A seat a human flies at any sample fails it.
5. **An AI vessel moved** (the check): every seat's path length is sampled every 0.5 s; at least one AI hull must fly
   10 units (`movingDistance`). The summary reports how many did.
6. **The end condition fired** (the check): `GameDataSO.OnMiniGameEnd` raised within the budget (600 game seconds;
   matches are designed around 90). Ready is pressed again between turns; the host is handed back to the AI if a
   turn respawned the hulls.
7. **No errors** (the check): every `Error`, `Assert` and `Exception` the log receives from entering Play to being back
   in Edit mode (boot, match and teardown, counted separately) fails the mode. There is no allow-list: an error that
   is "always there" is fixed at its source or made a warning, never filtered here.

It never writes a vessel, prism, score, course, timer or end condition. It changes `Time.timeScale` (4 while a turn
runs, put back to 4 when a hit-stop or celebration resets it to exactly 1) and the offline preference.

**What it does not check:** a player build (IL2CPP, stripping; this is the editor, Mono), Relay, UGS, a second
client, a human's input, more than one intensity per mode, frame rate, what anything looks or sounds like, and any
mode a person cannot reach from the shipped rosters.

### The end conditions are the shipped ones

The only override path for a mode's win count is `Assets/Resources/EndConditionOverrides.asset`
(`EndConditionOverridesSO`, `/EndGameConditions`). It is a shipped asset, so the smoke never writes it: it plays the
real target at 4x instead. The summary records `endConditionsMatchBuild`: false means the editor's Live counts differ
from the Build baseline a player build ships (`EndConditionBuildRestore`), so that run did not play the shipped
targets. To shorten a mode for the smoke, do not edit that asset; lower `matchBudgetSeconds` only if the mode really
is that long.

## What it plays

`Assets/_SO_Assets/Launch/ModeSmokeConfig.asset` (`ModeSmokeConfigSO`, `FrogletTools > Build > Mode Smoke Test >
Select Config`):

- **Rosters**: `ArcadeGames` and `ArenaGames`, the lists players launch from. Every card on them is played, so a new
  card is smoked the day it is listed. On 2026-11-05 that is 41 cards: 40 played, 1 skipped.
- **Excluded, with the reason** (reported as `skipped`, never `passed`):
  - **Maelstrom**: it chains Skim Race, Joust and Crystal Capture across scene loads and ends on the whole shuffle;
    each member mode is smoked on its own. Smoking Maelstrom itself needs a driver that follows its scene changes.
- **Not on a roster, so not played**, listed in every summary as `uncoveredBuildScenes`: `MinigameTandava` and
  `MinigameTandavaSevering` (Tandava is unlisted since 2026-10-25, `TANDAVA.md`).
- **Quick set**: Skim Race (the Skim Race pilot), Joust (enemy-seeking) and Switchback (a gate race, the Dolphin).
- **Defaults**: intensity 1 (clamped to the card), 3 seats (clamped to the card's seat range: an arena card seats one
  pilot per hull), Legendary AI, time scale 4. Per-mode `overrides` change intensity, seats or budget.

Every rostered hull carries an `AIPilot` (checked 2026-11-05 against `Assets/_Prefabs/Spacevessels/*.prefab`), so no
card is excluded for want of an AI. A mode whose AI cannot finish in the budget fails as a timeout: that is a
finding about the mode, not a reason to exclude it. Grizzly Charge (on the roster, on `DogFightController`) is played
like any other card.

## Why an `-executeMethod` runner and not a PlayMode test

- **It is how this project already plays real matches headless.** `SwitchbackSoakLauncher`, `ThresherTimeSoakLauncher`
  and Froglet Compute's trainer (`Tools/Compute/unity/FrogletComputeTraining.cs`) all enter Play from Bootstrap from an
  `-executeMethod` entry; the smoke is the same flow made general, not a parallel launch path.
- **A PlayMode test cannot reach the game code here.** The game is in `Assembly-CSharp`, which no asmdef can
  reference, and the project keeps its tests in `Editor/` folders without an asmdef for that reason
  (`Docs/claude/PROJECT_STRUCTURE.md`). `-testPlatform PlayMode` would need NUnit compiled into `Assembly-CSharp`.
- **The game must boot from Bootstrap** (AppManager, Reflex project scope, auth, Netcode); the PlayMode runner starts
  each test in its own scene.
- **It stays out of the EditMode Run All.** An EditMode `[UnityTest]` that yields `EnterPlayMode` would add an hour of
  matches to every Run All (and to the guard's EditMode job).
- The decision logic is still unit-tested: `Assets/_Scripts/Tests/Editor/ModeSmokeReportTests.cs` (EditMode, fast)
  holds the verdict, the JUnit escaping and the redaction.

The cost: the harness writes its own JUnit XML instead of the test runner's, and a GameCI `unity-test-runner` step
cannot run it. GameCI's **image** can, the way PR #1695 runs the editor (below).

## Run it

### In the editor

`FrogletTools > Build > Mode Smoke Test > Run Quick Set` (three modes, a few minutes) or `Run Every Mode` (40 modes;
an estimate of one to three minutes each, not yet measured). Leave the editor alone while it runs; the Console prints one line per mode and
the files land in `Logs/ModeSmoke/` (git-ignored).

### Headless

```
Unity -batchmode -projectPath <checkout> \
  -executeMethod CosmicShore.Editor.ModeSmokeLauncher.Run \
  -smokeModes quick -smokeOut Logs/ModeSmoke -smokeQuit -logFile -
```

- `-smokeModes`: `all` (default), `quick`, or names: `SkimRace,Joust,Switchback`. An unknown name stops the run (exit 3).
- `-smokeOut`: the result folder. `-smokeConfig`: another config asset. `-smokeQuit`: exit when done.
- **Never add `-quit`**: Unity would exit as soon as `Run` returns, before the first match.
- `-nographics` is not recommended where a GPU exists: a mode that errors without a graphics device fails the
  smoke. On a GPU-less CI runner it is the only option; read the first failures with that in mind.
- One Unity per project: close the editor (or use another checkout, as `verify_unity.sh` requires).

Exit codes: `0` every mode that ran passed (skips allowed), `1` a mode failed or never ran, `3` the run could not
start (no config, an unknown mode name).

### The files

| File | What |
|---|---|
| `summary.json` | the run: commit, Unity version, time scale, `endConditionsMatchBuild`, `uncoveredBuildScenes`, and one entry per planned mode (written before the first match, rewritten after each, so a crash leaves what finished and names what never ran) |
| `modes/<Mode>.json` | one mode's facts: `inBuild`, `reachedMenu`, `sceneLoaded`, `turnStarted`, `seats`, `aiSeats`, `movingSeats`, `gameEnded`, `errors`, `exceptions` (boot / match / exit), `gameSeconds`, `wallSeconds`, `notes`, the first 8 errors |
| `junit.xml` | one `testcase` per mode: failure with the first failed check, `skipped` with the exclusion reason |

Error lines are cut to one line of 300 characters and anything token-shaped (40+ unbroken characters, a URL's query)
is replaced before it is written: the files are CI artifacts on a public repository.

### Into the Launch Console

```
python3 Tools/Launch/smoke_report.py check   Logs/ModeSmoke   # table; exit 1 if a mode failed or never ran
python3 Tools/Launch/smoke_report.py records Logs/ModeSmoke   # the launch-status records, validated
python3 Tools/Launch/smoke_report.py publish Logs/ModeSmoke --dry-run
python3 Tools/Launch/smoke_report.py publish Logs/ModeSmoke  # one record per mode on launch-status
python3 Tools/Launch/smoke_report.py self-test
```

One record per mode, id `smoke-<mode>` in kebab case (`smoke-skim-race`, `smoke-wildlife-liberation`), platform
`quality`, kind `gate`, result `success` / `failure` / `skipped` (`Docs/LAUNCH/STATUS_CONTRACT.md`). The tool judges
each mode again from the facts, independently of the editor's `status`: a "passed" entry with an exception publishes
a failure, a mode that never ran publishes a failure, a skip with no reason publishes a failure. A record carries
counts and the first failed check, never error text. The self-test proves all of that, with the exception case as
its negative control, and runs in `launch-tools-check.yml` on every change under `Tools/Launch/`.

## In CI, once a Unity licence exists

Nothing here runs in CI yet: no workflow has a Unity licence (READINESS blocker 4; PR #1695 holds the GameCI compile
job and waits for `UNITY_EMAIL` / `UNITY_PASSWORD` in the `unity-ci` GitHub Environment). This PR does not touch
`bleeding-edge-guard.yml`. The change, for whoever lands it after #1695:

1. **A container script beside #1695's** (`Tools/CI/unity_compile_in_container.sh`): the same licence activation and
   return, then, instead of `-runTests`:
   `unity-editor -batchmode -nographics -projectPath "$PROJECT" -executeMethod CosmicShore.Editor.ModeSmokeLauncher.Run
   -smokeModes "${SMOKE_MODES:-all}" -smokeOut "$PROJECT/mode-smoke" -smokeQuit -logFile - | tee "$PROJECT/mode-smoke.log"`.
   Better still, give #1695's script a mode argument so activation lives in one place (ask Shombith, who heads tooling).
2. **A `mode-smoke` job** in the guard (or a `launch-mode-smoke.yml` with `workflow_dispatch` and a nightly
   `schedule`), `needs:` the Unity compile job, `environment: unity-ci`, on the same pinned `UNITY_IMAGE`, with the
   Library cache, `timeout-minutes: 180` for `all` (use `-smokeModes quick` on pull requests). It uploads
   `mode-smoke/summary.json`, `junit.xml` and `modes/` (not the editor log: it is the whole console of a run on a
   public repository) and runs `python3 Tools/Launch/smoke_report.py check mode-smoke` as its verdict.
3. **A publish job**, `needs: mode-smoke`, `if: always()`, `permissions: contents: write`, no Unity secrets: download the
   artifact and run `smoke_report.py publish mode-smoke` (with `--dry-run` on pull requests). If the summary is missing
   (the editor never started) the tool exits 2 and publishes nothing; the compile job's own record says why.
4. **Make it required**: in the "Protect trunks" ruleset (repo settings, an admin: Shombith), add the `mode-smoke` job's
   check name to the required status checks, once two runs on `bleeding-edge` have been green. A required check that
   is skipped (no licence) blocks merges, so add it only after the licence is in place. Until then run it by hand
   before every release candidate (`-smokeModes all`) and publish its records.

Hosted runners have no GPU, so the CI run is `-nographics`; a failure that only appears there is checked in a
windowed editor run before anything is changed for it.
