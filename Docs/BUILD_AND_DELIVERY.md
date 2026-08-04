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
| — | Weekly test build promotion | **Authored** — `.github/workflows/sync-build-branches.yml`. Moves `bleeding-edge` into `build/android` and `build/windows` every Thursday 06:00 PT for UGS, see §11. |

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
| `mono` | Nightly, 07:00 UTC | Mono standalone player build | Above, plus broken scenes, missing assets, bad build settings | 20–40 min |
| `il2cpp` | Weekly, Sunday 08:00 UTC | Full IL2CPP release build | Above, plus AOT/generic failures and native link errors — the ones that only appear in a shipping build | 60–150 min cold |

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

## 11. Build branches for the weekly test build

`.github/workflows/sync-build-branches.yml`.

The Thursday test build runs out of Unity Build Automation, and UGS watches a branch rather than
being told what to build. Pointing a UGS target straight at `bleeding-edge` would mean the build
picks up whatever landed in the minutes before it started, and there would be no stable name for
"the build QA is testing". So there are two disposable snapshot branches instead:

| Branch | Platform |
|---|---|
| `build/android` | Android test build |
| `build/windows` | Windows x64 test build |

**Nobody commits to these branches.** They are force-moved to a commit on `bleeding-edge` and hold
no unique history. If you need to fix something in a build, fix it on `bleeding-edge` and re-run the
promotion.

### The schedule

The workflow promotes `bleeding-edge` into both branches at **06:00 America/Los_Angeles every
Thursday**, then UGS takes over.

GitHub's scheduler is UTC-only and does not observe daylight saving, so a single cron entry drifts
by an hour twice a year. The workflow registers `0 13,14 * * 4` (both 06:00 PDT and 06:00 PST) and
the first step checks the real Pacific hour, letting the twin that is an hour off exit quietly. The
slot stays at 06:00 Pacific year round without anyone editing the cron.

The refs are moved through the GitHub API, not by checking the repository out. This is a ~2.7 GB
Unity project and a full-history clone would take longer than everything else in the job combined.

### The tag

Each promotion also writes a lightweight tag, `testbuild/YYYY-MM-DD`, on the promoted commit. This
exists so a bug filed three months from now against "the August 6th build" resolves to an exact
commit instead of a guess. It is the cheapest traceability that survives the branches being
force-moved every week.

### Running it by hand

Actions tab, **Sync build branches**, **Run workflow**. Two inputs:

- `source_ref` (default `bleeding-edge`): any branch or commit SHA. Use a SHA to promote a known
  good commit when the branch tip has since broken.
- `targets` (default `both`): promote only `android` or only `windows` when one platform needs a
  respin and the other build is fine.

Manual runs skip the Pacific clock guard.

### Configuration

| Variable | Where | Purpose |
|---|---|---|
| `BUILD_PROMOTION_REQUIRES_GREEN` | Repo variable | Set to `true` to refuse promotion of a commit whose check runs are red or absent. **Leave unset until Unity CI is actually running** (see §10), or every promotion blocks on evidence that does not exist yet. |

### Where the workflow file has to live

Scheduled workflows only fire from the **default branch**, which for this repository is
`bleeding-edge`. The file has to be merged into `bleeding-edge` before any Thursday run happens.
Merging it onto `master` or a feature branch does nothing for the schedule, though
`workflow_dispatch` still works from the Actions tab once it is on the default branch.

### UGS side

Each build target in Unity Build Automation needs its branch field set to `build/android` or
`build/windows` respectively, with auto-build on push enabled. Nothing else in UGS needs to change
week to week. Note that pushes made with `GITHUB_TOKEN` do not trigger *GitHub Actions* workflows,
but they do still deliver the ordinary push webhook that UGS listens on, so the auto-build fires
normally.
