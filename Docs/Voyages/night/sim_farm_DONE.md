# Sim farm: DONE (2026-10-31)

Branch `claude/voyage-sim-farm` (worktree `D:\Voyage\wt-c`, off `claude/voyage-wt-c-scratch`), pushed, no PR.
Farm root `D:\VoyageFarm`. The player used for the numbers below is `D:\VoyageFarm\build\CosmicShoreSimFarm.exe`
(IL2CPP, development).

## What is on the branch

| File | What |
|---|---|
| `Assets/_Scripts/Editor/Build/SimFarmBuild.cs` | `CosmicShore.Editor.SimFarmBuild.Build`: a Windows x64 dev player. Toybox scenes (`WebToyboxBuild.Scenes`), `FROGLET_WEB_TOYBOX` + `WINDOWS_BUILD` via `extraScriptingDefines` (no UGS, `SinglePlayerTransport`, so no ports or lobbies are shared). IL2CPP by default (`-mono` for a quicker build). Product name `Cosmic Shore SimFarm`, so PlayerPrefs (registry) and `persistentDataPath` stay separate from the game's. Windowed, run in background, no splash. Backend, product name and window settings are set for the build and restored in `finally`. |
| `ArkEscortSim.cs` | `-arkEscortSim @job.json` (jobId, seed, indexed runs; the plan string still works). `-arkEscortSeed N`: run i gets seed N+i through `ArkEscortRun.SeedOverride` and `Random.InitState`. `-arkEscortFps N` caps the frame rate with no vsync. `-arkEscortDrain s` sets the pause between runs. `-arkEscortFixedDt` uses `Time.captureDeltaTime` (experimental, see issues). `-arkEscortQuit` writes `{"done":true,"ok":..,"runs":..}` as the last line, then `Application.Quit` in a player. Under quit in a player, stack traces are off for Log and Warning. Each line starts with `JobId, RunIndex, Seed, RealSeconds, FixedDt`, spliced ahead of `JsonUtility.ToJson(report)`, so `ArkEscortReport` is untouched. A heartbeat line every 30 s of a run reports real s, game s, speed-up and fps. **Fix:** the old ready wait asked for freestyle once; if the boot veil was still up, the toggle was dropped and the wait ran out its full 600 s. It now asks again while the menu is idle. |
| `ArkEscortRun.cs` | Only `public static int? SeedOverride` (consumed by `SetSail`). No param injection. |
| `Tools/Build/ark_escort_report.py` | Skips the `{"done":true}` line. |
| `Tools/SimFarm/farm.py`, `farmlib.py` | The runner and the job folder (below). |
| `Tools/SimFarm/coevo.py`, `pareto.py` | The co-evolution driver, and a Pareto archive with the FIT/DIVERSIFY regime and soft floor. |
| `Tools/SimFarm/space.example.json` | Example genome space. Its keys are real `-arkEscortParams` registry keys from `claude/voyage-night`. |
| `Tools/SimFarm/README.md` | Short usage. |

## Build

Close any editor that has `D:\Voyage\wt-c` open. Then:

```
& C:\Unity\6000.3.17f1\Editor\Unity.exe -batchmode -quit -nographics -buildTarget StandaloneWindows64 `
  -projectPath D:\Voyage\wt-c -executeMethod CosmicShore.Editor.SimFarmBuild.Build `
  -buildOutput D:\VoyageFarm\build -logFile D:\VoyageLogs\simfarm_build.log
```

- On a Library that was on Android, the first build reimports for Windows: about 60 min, plus a 22 min build.
- After that, an IL2CPP rebuild takes about 30 min, and `-mono` takes 4-11 min.
- Build into a new folder (`-buildOutput D:\VoyageFarm\build_next`) while players are running, because they lock `build\`.
  Then swap the folders when the farm is idle.
- The build leaves Unity's own reimport noise in the worktree: materials, URP assets, `packages-lock.json`, and
  `ProjectSettings.asset` (Adaptive Performance empties `preloadedAssets` for a non-Android target). Do not commit
  any of it. It is uncommitted in `wt-c` now. `git checkout -- Assets ProjectSettings Packages` cleans it up.

## Run the farm

```
cd D:\Voyage\wt-c
python Tools/SimFarm/farm.py run --root D:/VoyageFarm -n 6          # leave running; Ctrl+C or create D:\VoyageFarm\STOP
python Tools/SimFarm/farm.py submit --root D:/VoyageFarm --plan "1:Sparrow,Sparrow,Sparrow;5:Sparrow,Sparrow,Sparrow" --jobs 6 --runs-per-job 4 --tag mytag [--params p.json]
python Tools/SimFarm/farm.py status --root D:/VoyageFarm
python Tools/SimFarm/farm.py stats  --root D:/VoyageFarm --tag mytag     # runs/h, RAM, CPU, failures
python Tools/SimFarm/farm.py report --root D:/VoyageFarm --tag mytag     # per d/squad: won/fell/hull/kills
```

**Defaults** (all measured):
- `--mode nographics`, time scale 1, `--fps 30`, `--drain 3`, `-job-worker-count 1`, `-force-d3d11`, below-normal
  priority.
- `--mode windowed` also works, at `--width 640 --height 360`.

**Job folder:** `jobs/pending -> running -> done`, using atomic renames.
- Each attempt writes `logs/<job>.a<k>.log`.
- Results go to `results/<job>.jsonl`, appended across attempts. A finished result beats a None result.
- A job's `params` dict is written to `params/<job>.json` and passed as `-arkEscortParams`. The farm never reads it.

**Failure handling:**
- **What it watches:** the exit, the done line, a log stall (180 s; the sim beats every 30 s), no new result
  within 600 s (+300 s for boot), and `Crash!!!`, device lost or `[ArkEscortSim] never ready|threw` in the log.
- **On a failure:** it kills the process tree (`taskkill /T /F`) and requeues only the unfinished runs, at the front of
  the queue.
- **After `--max-attempts`** (3), the missing runs are written as `Result 0` with `FarmError`, and the job goes to
  `done/` as partial or failed.
- **After the done line:** a player that hangs on exit is killed after 5 s.
- **On restart:** the runner requeues orphaned `running/` jobs and kills stray players.

I tested all of this with a Python test-double player: crash, hang, hang on exit, and max attempts. With the real
player, it caught three real stalls (below).

## Run the co-evolution driver

```
python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session s1 --space Tools/SimFarm/space.example.json `
  --generations 40 --spawn-farm --farm-n 6
# or run farm.py separately and leave out --spawn-farm. Re-running the same command resumes (state.json).
python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session dry --space Tools/SimFarm/space.example.json --fake --generations 6   # no Unity
```

**Populations and evaluation:**
- Vessels P_v = 12 and fauna P_f = 8; each genome is a flat dict of registry keys plus a `__squad` gene.
- Even generations evolve the vessels against frozen fauna; odd generations do the reverse.
- Each evolving genome meets K opponents (3 for vessels, 4 for fauna). One of them comes from the frozen side's hall of
  fame (its archive plus 4 elites).
- Each pairing is one job of 4 runs, with difficulties cycled from `--difficulties 1,3,5,7,9`. That makes 144 runs
  per vessel generation and 128 per fauna generation.

**Objectives** (scored over each genome's last 40 runs, as the mean minus half a standard error):

| Side | Fit | Diversity |
|---|---|---|
| Vessel | win weighted by d, + 0.25 hull | k-NN novelty of the behaviour descriptor |
| Fauna | -mean_d \|win(d) - (1-(d-1)/8)\| + tension | spread of the descriptors of the runs won against it |

**Selection:**
- The selection objective alternates FIT and DIVERSIFY each time a side evolves (`--regime patience` uses the plateau
  rule instead).
- In DIVERSIFY, the score is diversity minus a leaky penalty below peak fit - `--floor-margin` (the soft floor).

**Outputs** go to `D:\VoyageFarm\coevo\<session>\`:
- `state.json`, `runs.jsonl`, `gen_NNN.json`, `archive_{vessel,fauna}.json` and `summary.md`;
- `best/*.json`: ready-made `-arkEscortParams` files (`vessel_fit`, `vessel_diverse`, `fauna_fit`, the fronts, and
  `combined.json`).

## Measured (Gbox: 16 threads, 32 GB, RTX 3080, shared with other agents' editors)

| Config | Result |
|---|---|
| Windowed 640x360, IL2CPP, x1 | d1 SSS survived, hull min 80%, 58 fps, x1.0. Job wall 263 s for 1 run. Peak RSS 2.4 GB, private 3.3 GB, 1.6 cores |
| `-batchmode -nographics`, IL2CPP, x1, same seed | d1 SSS survived, hull min 89%, 59 fps, x1.0. RSS 2.0 GB, 1 core. **Same outcomes as windowed; nographics chosen** |
| 4 instances, nographics, 30 fps, 2 runs/job | 51 runs/h, 0 failures, 2.2 GB and 1.3 cores each |
| 6 instances, 2 runs/job | 57 runs/h, 0 failures |
| **6 instances, 4 runs/job (benchmark)** | **70 runs/h over 20.6 min, 24/24 runs, 0 failures.** About 78/h steady state (278 s per run per instance, including boot). Peak RSS 2.65 GB mean, 2.9 GB max; 1.3 cores each |

**Co-evolution pilot** (`coevo.py --session pilot1 --spawn-farm --farm-n 2`, 1 generation, 2x2 genomes, 4 runs):
it spawned the farm, queued the jobs with params files, waited about 10 min, scored the runs, and wrote the archives, `best/`
and `summary.md`. Then it stopped the farm through `STOP`.

**Outcomes look sane** (bench6, 6 runs each):

| d | Squad | Won | Mean hull min |
|---|---|---|---|
| d1 | SSS | 6/6 | 40% |
| d3 | Sparrow/Squirrel/Dolphin | 5/6 | 37% |
| d5 | SSS | 3/6 | 65% |
| d7 | Squirrel/Dolphin/Serpent | 1/6 | 55% |

The editor baseline (`D:\VoyageLogs\baseline.jsonl`) shows the same ladder.

**100+ runs/h needs 8 instances** (about 22 GB RAM and 10-11 cores) at time scale 1. One run is about 210-280 s of
game time, and the farm plays it in real time, so the cap is about 13 runs/h per instance. Today the machine had
only 9-19 GB free next to the other agents' editors, so I ran 6. Use `-n 8` when Gbox is quiet: 8 x 13 is about
104/h. RAM is the limit, not CPU.

## Known issues

1. **No speed-up beyond real time.** At time scale 2 or 3, and with `-arkEscortFixedDt 1/30`, the player falls into a
   frame-time spiral right after setting sail.
   - What it looks like: a 20-25 s frame, then x0.6-0.8 at 12-18 fps, then single frames of 170-260 s. All of it is
     on the main thread (per-thread CPU: the main thread is pinned at 100%, the others idle).
   - Cause: at x1 a headless instance already spends about 1 core per game second. That cost is in the simulation:
     physics fixed steps, fauna and AI, not rendering. Acceleration asks for more steps per frame than the main
     thread can run.
   - Fix: cut the per-game-second simulation cost (profile the escort's FixedUpdate and fauna in a player). A lower
     `Time.maximumDeltaTime` in farm mode would only turn the spiral into slow motion.
   - The editor sims' `-arkEscortTimeScale 3` likely do not reach x3 either. Their runs took 2-4 min of real time for
     210-280 s of game time.
2. **Mono players stalled** for minutes after sail (one thread busy, main thread waiting). I did not separate this from
   issue 1, since those runs also used the fixed step. IL2CPP is the default; `-mono` is not verified.
3. **Merging with the params branch.** `claude/voyage-night` and `claude/voyage-ark-core` also edit `ArkEscortSim.cs`
   (`LoadParams` and `ParamsArg`), `ArkEscortRun.cs` and `ark_escort_report.py`. `git merge-tree` shows conflicts in
   those 3 files. Resolve by keeping both sides:
   - in `Boot`: their `sim.LoadParams(Arg(args, ParamsArg))` next to my parsing;
   - in `finally`: their `ArkEscortParams.Pending = null`;
   - in `SetSail`: their changes next to my `SeedOverride` line.

   **This build has no params registry,** so `-arkEscortParams` is passed but ignored until the merge. The pilot
   co-evolution therefore only proved the pipeline (jobs, results, archives), not that genes have any effect. After
   the merge, check each line's `Params` echo for refused keys. The `fauna.*` defaults in `space.example.json` are
   guesses.
4. **Persistence.** `persistentDataPath` and PlayerPrefs are per product, not per instance. They are shared by all farm
   players but kept apart from the game. The toybox boot does not sign in, so writes are rare (cloud cache misses only).
   I saw no file-lock warnings.
5. **Players never exit cleanly.** Every player hung on exit after `Application.Quit`, as the editor does, so the farm
   kills it 5 s after the done line.
6. **Disk.** The build folder is 4.6 GB (IL2CPP, including `*_BackUpThisFolder_ButDontShipItWithYourGame`). Old
   builds can be deleted by hand: `D:\VoyageFarm\build_il2cpp`, `build_next` and `build_mono_old`. The earlier
   smoke/bench jobs, logs and results also remain under `D:\VoyageFarm`.
7. **Two warnings every boot** (not the farm's): STALE `HullFusion` bakes ("re-run Bake Crystal Hull Fusions") and a
   looping FMOD one-shot.
