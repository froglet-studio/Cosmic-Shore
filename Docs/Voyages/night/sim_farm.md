# Ark escort sim farm: 100+ runs/hour, co-evolving vessel AI vs fauna

Read-only research, 2026-10-31, against `D:\Voyage\wt-c` (branch `claude/voyage-wt-c-scratch`, 44aedd874).
Today: `C:\CosmicShoreVoyageLogs\run_sim.ps1` boots the GUI editor on `C:\CosmicShoreVoyage`, the launcher
(`Assets/_Scripts/Editor/Voyages/ArkEscortSimLauncher.cs`) opens Bootstrap and enters Play, and
`ArkEscortSim` (runtime, `[RuntimeInitializeOnLoadMethod]`, keyed on `-arkEscortSim`) does the rest.
That gets about 20 runs/h. The farm replaces the editor with N headless player processes.

## 1. Standalone Windows player: yes, with three small changes

**Editor-only dependencies.** Only the launcher is editor-only: `OpenScene`, `isPlaying`, `EditorApplication.Exit`.
`ArkEscortSim`, `ArkEscortRun`, `ArkEscortPilot` and `ArkEscortReport` are runtime code under `Controller/Voyages`,
and `Boot()` already reads `Environment.GetCommandLineArgs()`. A player started with
`-arkEscortSim ...` runs the sim as it is, because Bootstrap is build index 0 and the normal boot flow is
Bootstrap -> Authentication -> Menu_Main -> freestyle harbour. The only thing missing is a quit when the plan is done.

**The "lobby error" that stopped a sweep** (`sim_confirm2.log`, run 6/8, line ~308374).
- During the run the log shows `[HostConnectionService] Rate limited during refresh`, two Vivox
  `HTTP/1.1 403 Forbidden` errors, and a Unity Connect 408.
- Then `[Multiplayer]: NetworkManagerSession.StopAsync: Called after dispose`, `Internal_ApplicationQuit`, and
  `[ArkEscortSim] stopped`, followed by `PublishPartyState error: lobby not found`.
- So the online party session hosts the Netcode server, and through it the AI wingmates
  (`ArkEscortRun.Deploy` needs `NetworkManager.IsServer`). When that session went away, the sim went with it.
- Every editor sim today is a UGS-online session: anonymous sign-in, presence lobby, and a Relay-backed host.
  N parallel copies of that would also run into UGS rate limits and duplicate-membership errors.

**Fix: run the farm with no Unity Gaming Services at all.** The project already has this path, the browser Toybox
(`Assets/_Scripts/System/WebToybox.cs`, define `FROGLET_WEB_TOYBOX`). With the define set:
- `AppManager.Start` (line 182) skips the network monitor, UGS sign-in, Cloud Save, lobby, Friends and analytics.
- `AuthenticationSceneController.RunAuthFlowAsync` (line 169) goes straight to `NavigateToMainMenu`.
  Its web-audio wait returns at once off WebGL.
- `OfflineModeService.UsesInMemoryTransport` (line 219) puts the host on NGO's `SinglePlayerTransport`.
  That means no socket and no port 7777, so parallel instances cannot collide.
- Menu_Main boots into freestyle, which is where the harbour and escort live.

So the zero-gameplay-change build is a **Windows x64 development player with `extraScriptingDefines =
{"FROGLET_WEB_TOYBOX"}` and the toybox's three scenes** (`WebToyboxBuild.Scenes`: Bootstrap, Authentication,
Menu_Main). If the Toybox UI gating ever gets in the way, the cleaner long-term fix is a runtime flag:
`static bool NoServices => WebToybox.IsBuild || HasArg("-simFarm")`, ORed into those three call sites only.
Of the two options, use the define first because it needs no code change.

**Exact changes for a player entry point** (all runtime code, no asset edits):
1. `ArkEscortSim.Boot`: accept `-arkEscortSim @C:\farm\jobs\job_0042.json` (a leading `@` means a file).
   The job JSON holds `{seed, runs:[{difficulty, squad:[3 names]}], params:{...}}` (section 3).
   Keep the current `"5:Sparrow,Rhino,Manta;..."` string form.
2. `ArkEscortSim.RunAsync` `finally`: after `AllDone?.Invoke(ok)`, when `!Application.isEditor` and
   `-arkEscortQuit` is given:
   - write `{"done":true,"ok":ok,"runs":n}` as the last JSONL line,
   - `Application.Quit(ok ? 0 : 1)`.
   The driver also kills the process after a 30 s grace period, because the editor's
   "Cleanup mono" exit hang may happen in the player too.
3. Seeding: `ArkEscortRun.SetSail` seeds `_rng` with `TickCount`. Add a `SeedOverride` that the sim sets from
   `job.seed + runIndex`, and call `UnityEngine.Random.InitState` with it, so a run can be reproduced.
4. Per-run report extras: add `Seed`, `JobId`, `GenomeIds` (vessel and fauna) and `RealSeconds` to `ArkEscortReport`
   (plain fields, so `JsonUtility` serialises them).
5. Logging: Bootstrap writes 30-70 MB of log per sweep, mostly `NetworkSceneObjectGuard` warnings with full
   stacks. In farm mode, call `Application.SetStackTraceLogType(LogType.Log|Warning, StackTraceLogType.None)`
   in `Boot()`. The project default is Script-only stacks (`m_StackTraceTypes`).

**Build** (batchmode, from a dedicated farm worktree, not `C:\CosmicShoreVoyage`):
- Add `SimFarmBuild.Build()` to `Assets/_Scripts/Editor/Build/`, modelled on `WebToyboxBuild`. It should:
  - use `BuildTarget.StandaloneWindows64` with `BuildOptions.Development`,
  - pass the toybox scenes and `extraScriptingDefines = {FROGLET_WEB_TOYBOX, "WINDOWS_BUILD"}`,
  - output to `Builds/SimFarm/CosmicShore.exe`.
- Do not reuse `CosmicShoreBuildPipeline.Run` as is: its `ConfigurePlayerSettings` writes ProjectSettings
  (backend, define, fullscreen).
- Use Mono (`-useMono`, as in that pipeline) for fast iteration builds.
- Command:
  `Unity.exe -quit -batchmode -nographics -projectPath D:\Voyage\wt-farm -buildTarget StandaloneWindows64
  -executeMethod CosmicShore.Editor.SimFarmBuild.Build -logFile D:\farm\build.log`.
- The `cosmic-shore-build` skill only covers Android (a remote branch -> worktree -> batchmode -> Drive).
  It is a useful pattern, but there is no Windows recipe in it.

## 2. -batchmode -nographics, time scale, and how many instances fit

**What the GPU does:**
- **Entities Graphics** (prism/hull rendering) has a probe, `ECS/Bootstrap/EntitiesGraphicsSupportProbe.cs:77`.
  It treats `GraphicsDeviceType.Null` as unsupported and stands the Entities Graphics world down cleanly.
- **Swarm fauna** (`Swarm/SwarmMemberRenderer.cs`) keeps its members as CPU data ticked by a job. The GPU buffer
  is for drawing only. But with neither a structured-buffer path nor the SM3.5 fallback, it falls back to
  **one GameObject per member**. Under `-nographics` that is slower and might change the simulation.
- No gameplay code uses `AsyncGPUReadback` or compute for logic. The only `ComputeBuffer`/`GraphicsBuffer` users
  are renderers: Substrate, Swarm, ArcLightning fallback.
- Physics is PhysX on GameObjects. There is no `Unity.Physics` in `_Scripts`.

**Verdict:** `-nographics` is probably fine for the simulation, but it must be proven, not assumed.
Smoke test: run the same 6 seeded runs windowed and with `-batchmode -nographics`, then compare outcome, hull-min
and kills. If swarm GameObject-per-member is slow or the results differ, use the fallback instead: a windowed player
at `-screen-width 320 -screen-height 180 -screen-quality Fastest -force-d3d11`. Keep `-force-d3d11` in both modes,
because the device-lost was `D3D12Fence::Wait ... failed to wait for fence` at high fauna count, and headless mode
removes that risk entirely.

**Time scale.** `ProjectSettings/TimeManager.asset` has Fixed Timestep 0.04 and a maximum allowed of 0.333.
Physics steps are fixed in game time, so `timeScale` only adds more 0.04 s steps per real second, and PhysX stays
stable. The real risk is in `Update`:
- `VesselTransformer` moves vessels by `transform.position += speed * Time.deltaTime` (lines 1433, 1510).
- That means each frame's jump grows as timeScale/fps grows: frames jump through prisms, and the AI's 0.5 s
  retarget cadence turns coarse.

**Recommendation:** in farm mode stop using `timeScale` and set `Time.captureDeltaTime = 1/30`.
- Every frame then advances exactly 33 ms of game time, which is the same step a 30 fps play session sees.
- The player runs as fast as the CPU allows, with no vsync and `targetFrameRate = -1`.
- This keeps per-frame motion identical to a normal session, and the speed-up becomes "fps/30".

Caveats:
- `ArkEscortSim` uses `Time.unscaledTime` and `DelayType.Realtime` for its ready, settle and drain waits.
  Those stay in real time, which is fine: the 8 s drain becomes the only wasted wall time.
- Make the drain 2 s in farm mode.
- If you keep `timeScale` instead, cap it at about 4 and verify kill and graze counts against a 1x reference run.

**Instance budget (estimates; measure in step 3):**
- Editor play used about 1.1-1.2 GB managed ("Memory consumption went from 1.23 GB..."). A player is lighter.
  Assume about 2-3 GB per player including native memory and fauna at d9.
- 32 GB minus about 6 GB for the OS and the driver leaves room for **8-10 by RAM**.
- With `-nographics`, VRAM is 0. Windowed at 320x180 is about 0.5-1 GB each, so 10 GB is not the limit.
- CPU is the limit: 16 threads, each player has one main thread plus job workers.
  Pass `-job-worker-count 1` and run **6 instances** (6 main + 6 workers, with headroom for the driver).
  Raise the count only if total CPU stays under about 85%.

## 3. Parameter injection, with no asset edits

The job's `params` block is applied by a new runtime `ArkEscortParams` class, called from `ArkEscortSim` before each
`SetSailEscort` and from `ArkEscortRun` hooks.

Overwriting uses `JsonUtility.FromJsonOverwrite(json, target)`. It sets serialised (also private `[SerializeField]`)
fields by name and works on plain `[Serializable]` objects as well as SOs and components. Newtonsoft
(`com.unity.nuget.newtonsoft-json` 3.2.2, already used at runtime) splits the job JSON into per-target sub-objects.

Every SO it touches is snapshotted first with `JsonUtility.ToJson` and restored after the run.
- In a player that is belt and braces, because a runtime write to an SO never reaches disk.
- In the editor it is required, or the SOs get dirtied.

```json
{"seed":7101,"jobId":"g07-v3-f5","runs":[{"difficulty":5,"squad":["Sparrow","Rhino","Manta"]}],
 "params":{
  "escort": {"tier": {"Population":1.15,"HostileShare":0.8,"ArkSpeed":1.0,"RegrowSeconds":2.0,"BreachHull":0.3}},
  "escortPilot": {"GuardRadius":900,"StationRadius":260,"RetargetSeconds":0.5,"Assign":"Rank","Aim":"Heart"},
  "aiPilot": {"*":{"defaultAggressivenessHigh":0.05,"avoidanceHigh":2.5,"breakOrbits":true},
              "Manta":{"defaultThrottleHigh":0.8}},
  "fauna": {"*":{"goalUpdateInterval":4.0,"goalOrbitRadius":60,"starvationSeconds":30},
            "QuadFish":{"predationImmunitySeconds":4}},
  "faunaSO": {"SwarmFaunaConfigSO:<assetName>":{"...":0}},
  "population": {"legs":[1.0,1.2,0.9,1.4,1.1]}}}
```

| Block | Target | Where it is applied |
|---|---|---|
| `escort.tier` | `ArkEscortSO.Tier` for the run's difficulty (`Population, RegrowSeconds, HostileShare, ArkSpeed, BreachHull`) | `ArkEscortSim`, before `SetSailEscort`: `FromJsonOverwrite(json, escort.TierFor(d))`. Find the SO with `Resources.FindObjectsOfTypeAll<ArkEscortSO>()` |
| `escortPilot` | `ArkEscortPilot`: today `GuardRadius` (also read by `ArkEscortRun.Sample`/`BringWingmates`), `StationRadius` and `RetargetSeconds` are `const` | Code change: move them into a `[Serializable] class Tuning` with today's defaults, `static Tuning Current`. Add strategy genes (`Assign`: Rank / Nearest / Zone; `Aim`: Heart / Body; station angle). These are what "diversity of winning strategies" varies |
| `aiPilot` | `AIPilot` serialised fields (`skillLevel`, throttle/aggressiveness/avoidance highs and lows, `ram`, `drift`, `breakOrbits`, boost policy) per vessel class | In `ArkEscortRun.Escort()`, after `pilot.Arm` and also for the local seat in `SeedSeats`. `AIPilot.ConfigureForGameMode` may reset `skill`, so apply after it |
| `fauna` | `Fauna` (and subclass) serialised fields per prefab name (instance name minus `(Clone)`) | One hook: a static `Cell.FaunaRegistered` event raised in `Cell.RegisterLiveFauna` (Cell.cs:1702). Values cached in `Awake` (check per species) need the prefab instead: overwrite the prefab's component in the player (in memory only) before the leg's spawners run, from `PrepareCell` |
| `faunaSO` | Swarm/fauna config SOs (`SwarmFaunaConfigSO` ...) by type and asset name | Snapshot, overwrite before sail, restore |
| `population` | Per-leg multiplier on `cell.RuntimePopulationScale` | `ArkEscortRun.PrepareCell` already sets it; multiply there |

Echo the applied params, and any unknown key as a warning, into the report so the driver can find typos.

## 4. The existing Pareto/"diamond" optimizer, and the co-evolution driver

**Search results:**
- Nothing in the repo (whole-word `pareto` hits are unrelated). `C:\Claude\hybrid-nca` has a single "Pareto front"
  mention in a brief (`Tools/NCA/briefs/lite.md`). `C:\Claude\hybrid-nca-driver\driver.py` is a GPU-run restarter.
- The engine itself is on **Google Drive only**: `nca_ecology_lab/pareto_complexity.py`
  (file id `1AmdqMZkhd5TTVe-fThH5PEWS3IS8qO_z`, folder `1eY5zmfeK5mnseN-eh1YydC6IV5MLwvWj`;
  skill `nca-ecology-lab`). The same folder has `runner.py`, `jobs.py` and `notion_client.py`
  (a Notion backlog acting as a phone-steerable queue).
- What the engine is: pure stdlib. `Candidate(genome, score, complexity)`, `dominates`, `ParetoArchive`, a
  `Problem` protocol (`seed/evaluate/grow/shrink`), and `DiamondController`. The controller alternates
  COMPLEXIFY (climb score) and COMPRESS (minimise complexity with a leaky-ReLU floor
  `c + beta*leaky_relu(floor - score)`), switching on plateau (`patience`).
- Limits for this use: it is single-lineage hill-climbing over two fixed objectives, and `evaluate` is synchronous.

**How to reuse it** (copy it into `Tools/SimFarm/pareto_complexity.py`, credited):
- Keep `ParetoArchive`/`dominates`, generalised to a tuple of objectives: `dominates(a,b)` over `objs`, all maximised.
- Keep the regime switch and the leaky floor, re-labelled:
  - **FIT** regime: maximise difficulty fit.
  - **DIVERSIFY** regime: maximise strategy diversity, while `leaky_relu(retain*peak_fit - fit)` holds fit near its peak.
- Flip regimes on plateau, or every generation for strict alternation.
- Replace the synchronous `evaluate` with a batch `evaluate_many(genomes) -> futures` fed by the job queue below.
- "Complexity" becomes an optional third objective: the number of genes away from defaults (parsimony), the same
  role it plays in the NCA lab.

**Driver sketch** (`Tools/SimFarm/coevo.py`, Python 3.10, stdlib + numpy; no torch needed):

- **Genomes.**
  - `VesselGenome` = `escortPilot` + `aiPilot` blocks + squad. A real vector, plus categorical strategy genes.
  - `FaunaGenome` = `fauna` + `faunaSO` + `population` + `escort.tier` blocks.
  - Both have `mutate(rng, sigma)` (Gaussian in normalised bounds, with a categorical flip probability) and
    `crossover`. Bounds come from a `space.json`.
- **Populations.** Vessels P_v = 12, fauna P_f = 8, each with its own archive.
  Hall of fame: each archive's non-dominated set plus 4 elites from earlier generations, used as opponents to stop
  cycling (the Red Queen problem).
- **Evaluation per generation.**
  - Each vessel plays 3 fauna (2 current + 1 hall of fame) and each fauna meets 4-5 vessels.
  - Each pairing runs at 2 difficulties (d drawn from the tier the fauna targets), for about 48-60 runs per generation.
  - Batch 4 runs per job so the about 40 s player boot is shared.
- **Objectives.**
  - Fauna **difficulty fit**: `-mean |win(d) - target(d)|`, with `target(d) = 1 - (d-1)/8`
    (d1 always won, d9 almost never), and a "tension" bonus when the hull minimum ends within 0.1 of `BreachHull`.
  - Fauna **diversity**: entropy or spread of the *winning* vessels' behaviour descriptors against it.
    The fauna is good when many different strategies can beat it.
  - Vessel fit: win rate weighted by difficulty, `+ hull_min`.
  - Vessel diversity: novelty, i.e. the k-NN distance of its behaviour descriptor to its archive.
  - Behaviour descriptor, from `ArkEscortReport`:
    `[kills/min, threatSeconds share, EscortNear/EscortSamples, hull loss by leg (5), mean time to kill]`, normalised.
- **Noise.** Runs are stochastic. Score with the mean over pairings plus a confidence term. Re-evaluate archive
  members whenever they are drawn again (age them out the way AFPO does) instead of trusting a single run.
- **Alternation.** Even generations: vessels evolve and fauna are frozen. Odd generations: the reverse.
  Inside each, the FIT/DIVERSIFY regime follows the diamond controller's patience rule.
- **Job queue.**
  - A `jobs/` dir: `pending/*.json` -> `running/` -> `done/` (an atomic `os.replace`, so a crash leaves a visible trail).
  - `WorkerPool(N=6)`: each slot runs `CosmicShore.exe -batchmode -nographics -arkEscortSim @job -arkEscortOut
    out/job.jsonl -arkEscortQuit -logFile logs/job.log -job-worker-count 1 -force-d3d11`.
    Assign a CPU affinity mask per slot with `psutil`, if it is available.
- **Crash and restart.** Each slot watches:
  - a heartbeat (`out/*.jsonl` or log mtime, 5 min stall = dead),
  - a wall clock of 8 min per run,
  - the exit code, and `Crash!!!`/`device lost`/`[ArkEscortSim] (never|threw|stopped)` in the log.

  On failure the slot kills the process tree (`taskkill /T /F`) and requeues only the runs without a report line.
  After 2 failures, a run is recorded as `Result=None` with an error tag and excluded from scoring, not counted as
  a loss. The pool restarts the slot, and the driver never blocks on one slot.
- **Outputs** under `D:\farm\coevo\<session>\`:
  - `runs.jsonl` (every report, with genome ids),
  - `genomes/*.json`,
  - `gen_NNN.json` (populations, objectives, regime, archive),
  - `archive_vessel.json` / `archive_fauna.json` (Pareto fronts),
  - `summary.md`, which is `ark_escort_report.py` extended with a front table,
  - `best/` with a ready-to-apply `params` JSON for the chosen fauna tier set and the chosen escort strategies.

  The session can resume from the last `gen_NNN.json`.

## 5. Throughput and step plan

**Throughput:**
- One run is about 250-290 s of game time under way (logs: 172-286 s), plus about 20 s setup and the drain.
- Headless at `captureDeltaTime=1/30`, a 1-core player should manage roughly 120-240 fps.
  That is 4-8x real time, or about 45-90 s of wall time per run, so **40-80 runs/h per instance**.
- With 6 instances, and assuming 40% loss to contention, that is roughly **150-300 runs/h**.
- Even at the editor's 3x pace (about 2 min/run), 6 instances give about 180/h.
- Player boot is about 30-60 s against 5 min in the editor, and batching 4 runs per job keeps it under 15%.
- The 100/h target holds even if only 3-4 instances turn out stable.
- One co-evolution generation (about 54 runs) takes about 15-25 min, so roughly 25-40 generations overnight.

| # | Step | Est. |
|---|---|---|
| 1 | Farm worktree off bleeding-edge (`D:\Voyage\wt-farm`); add `SimFarmBuild.cs` (copy of WebToyboxBuild for StandaloneWindows64 + toybox define + dev + Mono) | 1 h |
| 2 | `ArkEscortSim`: `@job.json` input, seed, player quit, done line, stack-trace-off, `captureDeltaTime` mode, short drain; report extra fields | 2 h |
| 3 | First batchmode build (cold Library import for a new worktree is about 30-60 min); boot `CosmicShore.exe` windowed with a 2-run plan; confirm harbour -> escort -> JSONL -> quit with no UGS traffic in the Player.log | 1.5 h |
| 4 | `-batchmode -nographics` A/B against windowed on 6 seeded runs (outcomes, kills, hull-min, fps, RAM). Pick the mode | 1.5 h |
| 5 | Parallel scaling: 2/4/6/8 instances; measure runs/h, RAM, CPU; set N and the worker count | 1 h |
| 6 | `ArkEscortParams`: tier/pilot/AIPilot/fauna/SO overwrite with snapshot and restore; `ArkEscortPilot.Tuning` + strategy genes; `Cell.FaunaRegistered` hook; echo applied params | 3-4 h |
| 7 | Verify injection: extreme-value job (population 3x, `GuardRadius` 200) visibly changes the reports | 0.5 h |
| 8 | `Tools/SimFarm/farm.py` job queue + worker pool + crash handling; fault test (kill a worker mid-run, check requeue) | 2-3 h |
| 9 | `coevo.py`: genomes and space.json, generalised Pareto archive (from `pareto_complexity.py`), objectives and descriptors, alternation, outputs, resume | 4-5 h |
| 10 | Pilot session: 3 generations, sanity-check fronts against hand-tuned tiers (the PR #1641 numbers); then an overnight run | 1.5 h + overnight |

**Total: about 2.5-3 working days to an overnight co-evolution run.** Steps 1-5 alone (about 1 day) already
deliver the 100+ runs/h farm with today's plan strings.

**Open risks:**
- The Toybox define may hide something the harbour needs (step 3 shows it). Fallback: the runtime `-simFarm` flag.
- Swarm GameObject fallback under `-nographics` (step 4).
- `PlayerPrefs` and `persistentDataPath` (LocalCloudDataCache) are shared across instances. The Toybox boot does
  not sign in, so writes should be rare, but watch for file-lock warnings.
- `AIPilot` fields that are re-derived after arming (step 7 shows it).
