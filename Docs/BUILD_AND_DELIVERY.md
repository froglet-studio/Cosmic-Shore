# Build & Delivery — Windows x64 / Steam

Covers launch-checklist **Workstream B**. The Steam half is written and reviewable but **not armed**:
nothing can upload until the Steamworks app exists (checklist **A2**).

Owner: Shombith. Related: `Docs/STEAM_EA_INVESTOR_CHECKPOINT.pdf`, `Tools/Steam/README.md`.

---

## 0. Status of each B item

| # | Item | State |
|---|---|---|
| B1 | Windows x64 build configuration | **Scripted** — `CosmicShoreBuildPipeline`. In-editor Build Profile asset is a 60-second manual step, see §2. |
| B2 | PC platform sanity pass | **Code gaps closed** (quit path, share fallbacks, window/orientation settings). Manual pass still required, see §6. |
| B3 | Offline / service-outage launch | **Done** — the flow already reached the menu; it now also tells the player why. |
| B4 | SteamPipe upload + branch convention | **Ready, not armed** — `Tools/Steam/`. Needs app id + depot id. |
| B5 | Repeatable build checklist | **This document** + `Tools/Build/build_windows.sh`. |
| B6 | Crash reporting behind consent | **Done** — `CrashReportingService`, gated by the existing analytics consent. |
| B7 | Steam overlay verification | **Blocked** — needs a real Steam build on the beta branch. Do it during the closed playtest (E7). |
| — | Nightly build verification (CI) | **Authored, not running** — `.github/workflows/unity-ci.yml`. Needs a runner, see §10. |
| — | Bleeding-edge landing guard | **Authored, static half running** — `.github/workflows/bleeding-edge-guard.yml`. Checks every commit that lands on trunk and autofixes; the compile half needs the same runner, see §10.1. |
| — | Test build promotion | **Authored** — `.github/workflows/sync-build-branches.yml`. Moves `development` into `build/android` and `build/windows` every 3 weeks, Wednesday 06:00 PT, see §11. |
| — | Internal build tagging | **Authored** — `.github/workflows/tag-internal-build.yml`. Tags `bleeding-edge` every Friday 06:00 PT; UGS builds that branch directly. |

---

## 1. Prerequisites

| Requirement | Value |
|---|---|
| Unity | **6000.3.17f1** (must match `ProjectSettings/ProjectVersion.txt` exactly) |
| Module | *Windows Build Support (IL2CPP)* |
| Toolchain | Visual Studio Build Tools with the **Desktop development with C++** workload — IL2CPP will not link without it |
| Disk | ~25 GB free for Library + build output |

---

## 2. The build profile question

`Assets/Settings/Build Profiles/` holds a **Linux** profile only. Rather than hand-author a Windows
profile asset (the platform GUID inside those files is not something to guess at), the shipping
configuration lives in **`Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.cs`**. That is
deliberate:

- CI drives the build through `-executeMethod`, so the script is what actually runs.
- It is diffable, reviewable, and versioned with the code — a `.asset` blob is none of those.
- It cannot drift from what CI produces, because it *is* what CI produces.

If you also want the in-editor profile for convenience: **File → Build Profiles → Add Build Profile
→ Windows**, then add `WINDOWS_BUILD` to its scripting defines. Purely optional; the script sets that
define itself.

**What the script configures**

- Target `StandaloneWindows64`, IL2CPP (`--useMono` flag available for fast iteration builds)
- IL2CPP compiler configuration: `Release` for release, `Debug` for development builds
- Scripting define `WINDOWS_BUILD` (mirrors `LINUX_BUILD` on the Linux profile)
- Desktop window behaviour: landscape, resizable, fullscreen-window default, native resolution,
  runs in background
- Scene list from Build Settings, and it **warns loudly if Bootstrap is not index 0** — DI
  registration and the splash flow both depend on that

**What it deliberately does not touch:** managed stripping. Reflex DI, Netcode for GameObjects, and
Newtonsoft all resolve types by reflection; raising the stripping level silently removes types they
need and the failure only shows up at runtime in the player. Do not raise it without authoring a
`link.xml` first.

---

## 3. Building

### From the editor
**Tools → Cosmic Shore → Build → Windows x64 (Release)**

### From the command line
```bash
export UNITY_PATH="C:/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe"
./Tools/Build/build_windows.sh --version 0.2.0
```

### Raw batchmode (what a build server runs)
```bash
Unity -quit -batchmode -nographics \
      -projectPath . \
      -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildWindowsRelease \
      -buildOutput Builds/Windows64 \
      -buildVersion 0.2.0 \
      -buildCommit $(git rev-parse HEAD) \
      -logFile -
```

Exits **non-zero on failure**, so a build server fails the job instead of publishing a half-written
depot. Output lands in `Builds/Windows64/` (git-ignored) alongside a `build_manifest.txt` recording
version, Unity version, configuration, scripting backend, commit, and UTC timestamp.

### UGS Build Automation
Point the existing automated build config at the same `-executeMethod` target. Nothing bespoke is
needed — pass `-buildCommit` so the manifest stays traceable, and archive `Builds/Windows64/` as the
artefact that `Tools/Steam/upload.sh` consumes.

---

## 4. Delivering to Steam

See **`Tools/Steam/README.md`** for the full procedure. Summary:

```bash
export STEAM_APPID=<appid> STEAM_DEPOTID=<depotid> STEAM_USER=<builder-account>
./Tools/Steam/upload.sh --build-dir Builds/Windows64 --branch internal
```

Branches: `internal` (team) → `beta` (playtesters, used for E7) → `default` (players).
Publishing to `default` requires both `--set-live` and typing the app id back. Every other upload
lands in Steamworks unpublished, which is the normal path.

---

## 5. Crash reporting (B6)

Crash capture runs on **Unity Cloud Diagnostics** and is wired to the analytics consent the settings
panel already exposes:

- `CrashReportingService.Disarm()` runs at `BeforeSceneLoad` and forces capture **off**. Unity's
  default is capture-on, so this is load-bearing — without it, a player who never answered the
  consent prompt would still have crash payloads uploaded.
- `AnalyticsServiceFacade.ApplyCrashReportingGate()` arms it only when the player is age-eligible
  **and** has granted consent. It is keyed on consent and age only — not sign-in or connectivity —
  because a crash that happens offline or pre-sign-in is exactly the one worth having.
- Metadata attached: build version, Unity version, platform, GPU + graphics API, CPU thread count,
  system memory, and current game mode. **No player id** — a crash report cannot be joined back to
  an individual.

Project settings changed to enable it: `enableCrashReportAPI: 1` and
`CrashReportingSettings.m_Enabled: 1`.

> **In-editor check required:** confirm Cloud Diagnostics is enabled for the project in the Unity
> Dashboard, then verify a forced exception appears in the dashboard from a playtest build.

---

## 6. PC platform pass (B2)

### Code gaps closed

| Gap | Fix |
|---|---|
| **No quit path anywhere.** `Application.Quit` appeared nowhere in the project — a PC game that cannot be closed from its own UI. | `DesktopPlatformServices.Quit()`, surfaced as a quit button in the settings panel. Auto-hidden on mobile, where the OS owns app exit. |
| **NativeShare dead-ends.** `ShareByEmail` and `SnsShare` called NativeShare unguarded; it has no desktop implementation and silently does nothing, so the support and share buttons looked broken on PC. | Desktop fallbacks: `mailto:` for support email, save-and-reveal into a `Shared/` folder for screenshots. Mobile path untouched. |
| Mobile-shaped player settings on desktop. | Build script sets landscape, resizable window, fullscreen-window default, native resolution, run-in-background. |

`PaintingShareExporter` already guarded itself (`#if UNITY_EDITOR || UNITY_STANDALONE`), and Ads /
DailyRewardCard were already mobile-gated. Multi-mouse input already has a Windows raw-input path.

### Still requires a human on Windows

- [ ] Wwise audio initialises and all beds play
- [ ] Keyboard + mouse, Xbox pad, and PlayStation pad each drive menu and flight
- [ ] Alt-tab away and back: audio resumes, input recovers, no stuck modifier keys
- [ ] Windowed ↔ fullscreen ↔ borderless via the settings panel, at several resolutions
- [ ] Quit button closes the process cleanly (no orphaned process in Task Manager)
- [ ] Support email button opens a mail client; screenshot share opens the folder
- [ ] First-run on a machine that has never had the game: no missing DLL prompts

---

## 7. Offline behaviour (B3)

Launching with no network already reached the menu — a 10-second safety timeout force-navigates —
but it did so **silently**, which reads as a broken sign-in. Now:

- The loading line says *"No connection. Starting offline…"* when the device reports no reachability.
- Timeout and unhandled-failure paths show *"Starting in offline mode — online play and progress
  sync are unavailable"* and hold it for `offlineNoticeDwell` (2s, inspector-tunable) before moving on.
- Guest-login failure shows a player-readable message instead of a raw SDK exception string.

Verify by launching with the network adapter disabled: you should reach the main menu in roughly
10–12 seconds having been told why.

---

## 8. Release checklist

Run top to bottom. Every step is either a command above or a box to tick.

1. [ ] `git status` clean; on the intended commit
2. [ ] `ProjectVersion.txt` matches the installed editor exactly
3. [ ] Bump `bundleVersion` (or pass `--version`)
4. [ ] Bootstrap is scene index 0 in Build Settings
5. [ ] Build: `./Tools/Build/build_windows.sh --version <x.y.z>`
6. [ ] Build exited 0 and `build_manifest.txt` shows the right version and commit
7. [ ] Launch the built player locally: reaches the main menu, plays one Tournament round
8. [ ] Offline smoke test (§7)
9. [ ] Upload to `internal`: `./Tools/Steam/upload.sh --build-dir Builds/Windows64 --branch internal`
10. [ ] Install from Steam on a clean machine; verify the overlay renders (**B7**)
11. [ ] Promote to `beta` for playtesters, or `default` with `--set-live` for release
12. [ ] Confirm crashes and funnel events appear in their dashboards

---

## 9. Known gaps

- **B7 (overlay verification)** cannot be done until a build is on Steam. Fold it into the closed
  playtest (E7).
- The build script has not been executed in this environment — there is no Unity install here. It
  needs one clean run on a Windows machine with the IL2CPP toolchain before it is trusted; expect
  the first IL2CPP build to take considerably longer than a Mono one.
- Cloud Diagnostics must also be switched on in the Unity Dashboard; the project-side settings are
  set but the service is per-project on the web side.


---

## 10. Continuous integration

`.github/workflows/unity-ci.yml`. The repository had no CI before this; this is the first workflow.

### Tiers

| Tier | Trigger | What runs | Catches | Rough time |
|---|---|---|---|---|
| `compile` | Every PR into `bleeding-edge` | Edit-mode tests (which force a full compile of runtime, editor, and test assemblies) | Non-compiling C#, broken tests | 5–15 min |
| `il2cpp` | **Thursday 09:00 UTC**, against `bleeding-edge` | Full IL2CPP release build | Above, plus AOT/generic failures and native link errors — the ones that only appear in a shipping build | 60–150 min cold, far less warm |
| `il2cpp` | **Tuesday 13:00 UTC**, against `development`, only when the next day is an on-cycle promotion Wednesday | Full IL2CPP release build | Same, on the branch UGS is about to build | as above |
| `mono` | **Manual dispatch only** | Mono standalone player build | Produces a player in ~a third of the time, but is blind to AOT and native link errors — useful when iterating, never a gate | 20–40 min |

**Nothing scheduled builds Mono.** A Mono player proves an executable can be produced; it cannot see
the failure class this tier exists to catch. On an owned runner with a warm `Library/` there is no
reason to verify something weaker than what ships.

**Both scheduled tiers are pre-flights for a UGS build**, one working day ahead of it:

| CI build | Runs | UGS build it protects |
|---|---|---|
| Thursday 09:00 UTC (02:00 PT) | `bleeding-edge` | Friday 06:00 PT internal build (`tag-internal-build.yml`) |
| Tuesday 13:00 UTC (06:00 PT) | `development` | Wednesday 06:00 PT test-build promotion (`sync-build-branches.yml`) |

Every branch UGS builds is now player-built by CI first, with a day of slack to fix what it finds.
The Tuesday tier is cycle-gated on the same `CYCLE_ANCHOR` / `CYCLE_DAYS` as the promotion, so it
fires exactly one Tuesday in three rather than burning an IL2CPP build on a commit nothing will ship.

Any tier can also be run on demand from the Actions tab (**Run workflow** → pick a mode).

### The runner is not chosen yet

The `unity` job is **skipped entirely until the repository variable `UNITY_RUNNER_LABEL` is set**,
and then targets whatever runner label it names. Skipping rather than defaulting to a label is
deliberate: a job queued against a runner that does not exist shows as a *permanently pending* check
on every PR, and GitHub only reaps it after roughly 24 hours — `timeout-minutes` governs execution
time, not queue time. Set the variable the day a runner is registered; nothing else needs editing.
Options:

| Option | What it needs | Notes |
|---|---|---|
| **Self-hosted on the existing build box** | Register the machine as a repo runner; set `UNITY_PATH` | No license activation, no minute costs, and `Library/` stays warm so builds are incremental. Best fit given the build server already exists. |
| **UGS Build Automation** | Replace the build step with an API call that triggers the existing build target | Least new machinery; logs and status live in UGS rather than on the PR. |
| **GitHub-hosted** | `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` secrets, plus a larger runner | Standard `windows-latest` offers ~14 GB free disk. This project is a 2.8 GB checkout plus ~10 GB of Unity and Windows IL2CPP tooling, and `Library/` for a project this size lands well beyond that. Windows minutes also bill at 2×. Not viable for a player build; workable for `compile` only. |

### Configuration

| Variable | Where | Purpose |
|---|---|---|
| `UNITY_PATH` | Runner env **or** repo variable | Absolute path to the Unity 6000.3.17f1 executable. The job fails fast with a clear message if unset. |
| `UNITY_RUNNER_LABEL` | Repo variable | Runner label to target. **While unset, the `unity` job is skipped** and only the `resolve` job runs. |
| `UNITY_TESTS_BLOCKING` | Repo variable | Set to `false` to report edit-mode failures as a warning instead of failing the PR. See the caveat below. |

### Two things to know before turning it on

- **The edit-mode suite has not been run here.** If it is not currently green, the PR gate goes red
  on day one. Either green it up first, or set `UNITY_TESTS_BLOCKING=false` for a grace period —
  but treat that as temporary, since a non-blocking gate is not a gate.
- **Scheduled workflows run the default branch's copy of the file** and check out the default
  branch. The `resolve` job therefore pins scheduled runs to `bleeding-edge` explicitly, but the
  workflow file itself still has to reach the **default branch** before any nightly fires. Merging
  `bleeding-edge` *into* a feature branch does not move the file the other way — it travels only
  when the PR merges. Until then the per-PR `compile` tier is the only thing that can trigger.

### Security

If `froglet-studio/Cosmic-Shore` is **public**, do not attach a self-hosted runner without first
restricting who can trigger it: a fork PR would otherwise execute arbitrary code on the build
machine. This workflow uses `pull_request` (never `pull_request_target`) and takes
`permissions: contents: read`, but the safe configuration on a public repo is to require approval
for outside-contributor runs, or drop the PR trigger and keep only `schedule` +
`workflow_dispatch`. On a private repo this is a non-issue.

### What it does not do

- No player artefact upload. The build is multiple GB; only test results and the build manifest are
  retained. SteamPipe uploads run from the build machine (§4), not from CI.
- No notifications beyond GitHub's own. Failures surface in the Actions tab and via GitHub's default
  email; wiring Discord is a later addition if the signal gets missed.

---

## 10.1 The bleeding-edge landing guard

`.github/workflows/bleeding-edge-guard.yml`. Verifies every commit that **lands on** `bleeding-edge`
and attempts a repair when one breaks.

### Why it exists separately from §10

`unity-ci.yml` triggers on `pull_request` into `bleeding-edge`. Most work does not arrive that way —
it arrives as a direct merge push (`Merge remote-tracking branch 'origin/claude/…' into
bleeding-edge`), which fires no `pull_request` event at all. Those commits were reaching trunk
completely unverified, and the first thing to notice was a human running a Windows build by hand.

### Stages

| # | Job | Runs on | Gate | Catches |
|---|---|---|---|---|
| 1 | `validate` | `ubuntu-latest`, seconds, free | always | Editor-only API in player code, guard mistakes, `MonoBehaviour` name/file mismatch, missing `.meta` |
| 2 | `unity` | `UNITY_RUNNER_LABEL` | skipped while that variable is unset | **Everything a real compile sees** — package-level errors, asset import breakage, AOT/IL2CPP |
| 3 | `autofix` | `ubuntu-latest` | on failure of 1 or 2, and only if `ANTHROPIC_API_KEY` is set | Repairs the mechanical failure classes and opens a PR |

### The load-bearing caveat

**Stage 2 is the only stage that can see a build break, and it is skipped until `UNITY_RUNNER_LABEL`
is set.** Until then this workflow catches the static classes only, and a red Windows build can still
reach trunk. That is the same gate as §10 and it is the single configuration change that turns all
three workflows on at once.

Verify the state at any time by opening a recent **Unity CI** run and reading the job list: if
`unity` shows *skipped*, no Unity has compiled the project in CI and every green check on that run
came from the Python checkers alone.

### Autofix rules

- It opens a **pull request against `bleeding-edge`** and never pushes to the branch. An unreviewed
  autofix landing on the integration trunk is worse than the break it was meant to repair.
- It reuses an existing open `autofix/bleeding-edge*` PR rather than opening a second one, so a
  repeated failure updates one PR instead of spawning a queue of them.
- It is instructed to stop and explain when the failure is not one of the known mechanical classes,
  rather than guessing at gameplay code.
- With no `ANTHROPIC_API_KEY` configured the job warns and exits cleanly; verification still fails,
  it simply is not repaired automatically.

### Security note

This repository is **public**. The guard is push-triggered on a branch that requires write access,
so a self-hosted runner attached to it is not reachable from fork pull requests. Do not add a
`pull_request` trigger to this workflow without first requiring approval for outside-contributor
runs.

---

## 10.2 Turning the compile stage on — self-hosted runner runbook

**Decision (2026-08-07): self-hosted, on the existing Windows build box.** No license activation, no
billed minutes, and `Library/` stays warm between runs so a compile is minutes rather than tens of
minutes. Nothing in any workflow needs editing — all three already target `vars.UNITY_RUNNER_LABEL`.

### Prerequisites on the machine

| Requirement | Value |
|---|---|
| Unity | **6000.3.17f1**, matching `ProjectSettings/ProjectVersion.txt` exactly |
| Module | *Windows Build Support (IL2CPP)* |
| Toolchain | Visual Studio Build Tools, **Desktop development with C++** workload |
| Git LFS | `git lfs install` — the checkout pulls binary assets |
| Disk | ~25 GB free for `Library/` plus build output |

### Register the runner

1. **Settings → Actions → Runners → New self-hosted runner**, architecture **Windows x64**. GitHub
   shows a download-and-configure snippet containing a single-use registration token.
2. Run it in an empty directory on the build box — **not** inside a checkout of this repository.
3. When `config.cmd` asks for labels, add one memorable label, e.g. `cosmic-build-win`. Keep the
   default `self-hosted`, `Windows`, `X64` labels it adds for you.
4. **Answer `Y` to "Would you like to run the runner as service?"** when `config.cmd` asks.

   On Windows the service is installed **by `config.cmd` itself** — this is the step that makes the
   runner survive reboot and logout. There is **no `svc.cmd`**: that is the Linux pattern
   (`svc.sh`), and running it here just returns
   `The term '.\svc.cmd' is not recognized`. GitHub's own wording is *"Configuring the self-hosted
   runner application as a service on Windows is part of the application configuration process."*

   If you already configured the runner and answered `N` (or were never asked), you do not need to
   start over — see "Converting an interactive runner to a service" below.
5. Confirm the runner shows **Idle** on the Runners page.

### Managing the Windows service

All of these are PowerShell, **run as Administrator**:

| Task | Command |
|---|---|
| Status | `Get-Service "actions.runner.*"` |
| Start | `Start-Service "actions.runner.*"` |
| Stop | `Stop-Service "actions.runner.*"` |
| Uninstall | `Remove-Service "actions.runner.*"` |

### Converting an interactive runner to a service

A runner started with `.\run.cmd` goes offline the moment that window closes or the user logs out,
and every job for its label then **queues forever** — the symptom is a job stuck in *Queued* with no
runner assigned, on a label that served a job minutes earlier.

To fix it permanently, in the runner folder:

```powershell
Get-Service "actions.runner.*"        # nothing listed = not a service
.\config.cmd remove --token <REMOVAL TOKEN>
.\config.cmd                          # answer Y to the service question this time
```

Get the removal token from **Settings → Actions → Runners → the runner → Remove**. Re-register with
the same label (`cosmic-build-win`) so no workflow needs editing.

To unblock a queued job *right now* without reconfiguring, just run `.\run.cmd` and leave the window
open — the queued job starts within seconds. That is a stopgap, not the fix.

### Set the two repository variables

**Settings → Secrets and variables → Actions → Variables tab.** These are variables, not secrets.

| Variable | Value | Effect |
|---|---|---|
| `UNITY_PATH` | `C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe` | Which editor the jobs invoke. Jobs fail fast with a clear message if unset. |
| `UNITY_RUNNER_LABEL` | `cosmic-build-win` | **This is the switch.** Setting it un-skips the `unity` job in `bleeding-edge-guard.yml`, `unity-ci.yml`, and `build-branch-ci.yml` simultaneously. |

Set `UNITY_PATH` **first**. Setting the label first means the next push schedules a Unity job that
immediately fails on the missing path.

### Expect day one to be red, and plan for it

The edit-mode suite has never been run in CI, so it is unknown whether it is currently green. The
guard already separates the two failure modes (§10.1) — a compile break always blocks, a red test is
reported separately — but if the suite turns out to be red you will still get a failing check on
every push. Set a third variable during the grace period:

| Variable | Value | Effect |
|---|---|---|
| `UNITY_TESTS_BLOCKING` | `false` | A red edit-mode test becomes a warning. A compile break still blocks and still triggers autofix. |

Treat that as temporary and remove it once the suite is green; a non-blocking gate is not a gate.

### Verify it actually took

Push any commit to `bleeding-edge` (or **Actions → Bleeding-edge guard → Run workflow**), then open
the run and read the job list:

- `unity` shows **skipped** → `UNITY_RUNNER_LABEL` is still unset or misspelled. Nothing is being
  compiled and the guard is static-only.
- `unity` shows **queued** and stays there → the label does not match any registered runner. Fix the
  label rather than waiting; GitHub only reaps such a job after roughly 24 hours.
- `unity` shows **success** → the compile stage is live. This is the first commit in the repository's
  history to have actually been compiled by CI.

### Autofix prerequisite

Stage 3 needs the `ANTHROPIC_API_KEY` **secret**, which already exists (`build-branch-ci.yml` uses
it). With no key the job warns and exits cleanly; verification still fails, it is simply not
repaired automatically.

### Housekeeping

- The runner keeps its workspace between jobs, which is exactly what makes compiles fast. Do not add
  a clean step to these workflows.
- If `Library/` corrupts and the compile starts failing for no diff-visible reason, delete
  `Library/` in the runner's workspace once and let the next run rebuild it.

---

## 11. Build branches and the promotion workflows

> **The release model — which branch feeds which build, on what schedule, and what to do
> when one breaks — is in `Docs/BRANCHING_AND_RELEASE.md`.** Read that first. This section
> is only the build-side mechanics.

Three builds, three sources:

| Build | Source | Cadence | Automation |
|---|---|---|---|
| Internal | `bleeding-edge` | Friday, weekly | `tag-internal-build.yml` (tags only; UGS watches the branch directly) |
| Test | `development` | Wednesday, every 3 weeks | `sync-build-branches.yml` (promotes into `build/*`) |
| Release | `master` | On demand | None yet, deliberately |

### Why the test build uses snapshot branches

UGS watches a branch rather than being told what to build. A target pointed straight at
`development` would build whatever landed seconds earlier, and there would be no stable name for
"the build testers are on". So `build/android` and `build/windows` are force-moved to one chosen
commit and UGS reads those.

**Nobody commits to them.** They hold no unique history and are overwritten every cycle. Fix
things on `bleeding-edge`.

The internal build has no snapshot branch because UGS reads `bleeding-edge` live; the Friday
workflow only records which commit the slot opened on.

### Scheduling mechanics

Both workflows register **two** cron entries (`13:00` and `14:00` UTC) and let a guard drop the
wrong one. GitHub's scheduler is UTC-only and ignores daylight saving, so a single entry would
drift an hour twice a year; two entries plus a real Pacific-hour check hold the slot at 06:00 PT
year round with no seasonal edits.

Cron also cannot express "every three weeks". The test promotion fires every Wednesday and counts
whole days from `CYCLE_ANCHOR` (`2026-08-12`), proceeding only when the count divides by
`CYCLE_DAYS` (21). Both are env values at the top of the workflow. Verified to fire on exactly
1 Wednesday in 3, and the Pacific *date* is identical for both DST twins, so the cycle never
shifts at a transition.

Refs move through the GitHub API rather than a checkout. This is a ~2.7 GB Unity project; a
full-history clone would take longer than the rest of the job combined.

### Tags

| Tag | Written by | On |
|---|---|---|
| `testbuild/YYYY-MM-DD` | Test promotion | The promoted commit |
| `internal/YYYY-MM-DD` | Friday tagger | Whatever `bleeding-edge` pointed at |

These are what turn "the build from the 12th" into an exact commit after the branches have moved
on. Until R1 in `BRANCHING_AND_RELEASE.md` lands, they are the *only* way back from a build to a
commit, and they depend on someone recording the date correctly.

### Running a promotion by hand

Actions → **Promote test build** → **Run workflow**:

- `source_ref` — blank uses `BUILD_SOURCE_BRANCH` (default `development`). Pass a SHA to promote a
  known-good commit when the branch tip has since broken.
- `targets` — `both`, or one platform when only one needs a respin.

Manual runs skip both the clock and cycle guards, so this is also how you create the build
branches for the first time rather than waiting for the first scheduled run.

### Verification

The promotion is gated on `Tools/CI/validate_project.py` over a sparse checkout of
`Assets/_Scripts` (22 MB against 1.4 GB for all of `Assets`), needing no Unity install. It blocks
on editor-only API reaching player code — the recurring failure behind *Move editor scripts to
Editor folder to fix player build errors* and *Fully qualify Editor base class to avoid namespace
conflict*. A known-bad commit never reaches UGS.

Afterwards `build-branch-ci.yml` re-runs the full static set on what landed, compiles it where a
Unity runner exists, and can attempt an autofix PR against `bleeding-edge`.

### Notifications

Every test promotion comments on one tracking issue labelled `build-promotion`; **closed means
healthy, open means the test build is stale or missing**. Subscribe to it. Successful runs comment
too, so a workflow that silently stops firing does not look like a quiet cycle.

Do not rely on GitHub's own failure email: for scheduled workflows it reaches only whoever last
edited the cron line.

### Configuration

| Name | Kind | Purpose |
|---|---|---|
| `BUILD_SOURCE_BRANCH` | Repo variable | Test build source. Defaults to `development`. |
| `BUILD_PROMOTION_REQUIRES_GREEN` | Repo variable | `true` refuses to promote a commit whose check runs are red or absent. **Leave unset until Unity CI runs** (§10). |
| `ANTHROPIC_API_KEY` | Repo secret | Enables the autofix job. Inert while unset. |

### Where these files have to live

Scheduled workflows only fire from the **default branch**, which is `bleeding-edge`. A cron edited
anywhere else does nothing until it merges there.
