# Branching, Promotion and Release

How code gets from a developer's machine to a Thursday test build, what guards
it on the way, and what still needs building.

Companion documents: `Docs/BUILD_AND_DELIVERY.md` (how a build is actually
produced), `GIT_RULES.md` (commit and branch naming conventions).

---

## 1. The branch model

| Branch | Role | Who writes to it |
|---|---|---|
| `bleeding-edge` | **Trunk.** Where development happens, and the repository default branch. | Everyone, via PR |
| `master` | The best known-good version at any time. Updated from trunk when trunk is worth keeping. | Merges from trunk only |
| `development` | Currently a third parallel line. Intended to take over the trunk role later, see §6. | Everyone, via PR |
| `build/android` | Disposable snapshot that UGS builds Android from. | **The promotion workflow only** |
| `build/windows` | Disposable snapshot that UGS builds Windows from. | **The promotion workflow only** |

Two rules matter more than the rest:

**Nobody commits to `build/*`.** They are force-moved to a trunk commit every
Thursday and hold no unique history. A commit landed on one is destroyed the
following week, which means a bug "fixed" there comes straight back. Fix it on
trunk and let the next promotion carry it.

**Trunk is defined as the repository default branch, not by name.** The
automation reads the default branch at run time. Nothing hardcodes
`bleeding-edge`, so moving trunk later is a GitHub setting change and not a
code change. See §6.

---

## 2. How a change reaches a test build

```
  developer branch
        │  PR
        ▼
  bleeding-edge (trunk)  ──────────────┐
        │                              │
        │  Thursday 06:00 PT           │  layer 1: static validation
        │  sync-build-branches.yml     │  runs BEFORE the refs move
        ▼                              │
  build/android, build/windows  ───────┤  layer 2: build-branch-ci.yml
        │                              │  full static set + Unity compile
        │  push webhook                │
        ▼                              │
  UGS Build Automation  ───────────────┘  layer 3: the real player build
        │
        ▼
     testers
```

### Defence in depth

Each layer is cheaper and earlier than the one after it. The point is that a
mistake gets caught by the cheapest layer that can see it.

| Layer | What | Cost | Catches |
|---|---|---|---|
| 1. Pre-promotion validation | `Tools/CI/validate_project.py` over a 22 MB sparse checkout | seconds | Editor-only API in player code, and other mechanical breakage. **Blocks the promotion**, so a known-bad commit never reaches UGS. |
| 2. Build branch CI | `build-branch-ci.yml` on push to `build/**` | seconds, or up to an hour with a Unity runner | The full static set, plus a real compile once a runner exists |
| 3. UGS Thursday build | The actual Android and Windows player builds | tens of minutes | Everything else: shaders, assets, addressables, platform toolchains |

### What layer 1 actually checks

`Tools/CI/validate_project.py` needs no Unity install, which is what lets it run
on every promotion. It catches the errors that have historically broken player
builds here and are visible without compiling:

- **`editor-in-runtime`** *(blocking)*. Code reaching `UnityEditor` from a
  player-visible line. `UnityEditor` does not exist in a player build, so this
  is a hard failure at build time even though the editor compiles it happily.
  This is the recurring "namespace error": it produced the commits *Move editor
  scripts to Editor folder to fix player build errors* and *Fully qualify Editor
  base class to avoid namespace conflict*. The check tracks `#if` / `#elif` /
  `#else` / `#endif` nesting properly, so a correctly guarded editor script is
  not flagged, and an `#if UNITY_EDITOR || UNITY_STANDALONE` is, because the
  second arm still reaches a player.
- **`monobehaviour-name`** *(warning)*. A MonoBehaviour whose class name does
  not match its file name. Unity refuses to attach the component and the failure
  surfaces later as a null in a scene. Warning rather than error because a file
  holding several MonoBehaviours is a legitimate pattern.
- **`meta`** *(warning)*. Orphan and missing `.meta` files, which is how GUID
  churn and missing-script references start when several people share scenes.

Run it locally before pushing anything structural:

```bash
python3 Tools/CI/validate_project.py          # all checks
python3 Tools/CI/validate_project.py --list   # what it can check
python3 Tools/CI/test_validate_project.py     # self-test, 16 cases
```

Only `editor-in-runtime` can block. A gate that cries wolf gets switched off, so
everything that can be intentional reports and gets out of the way.

### Autofix

When layer 1 or 2 fails, `build-branch-ci.yml` runs an autofix job that reads
`CLAUDE.md` and `.claude/skills/`, repairs the mechanical cases, and **opens a
PR against trunk**. It never pushes to the build branch, for the reason in §1.

It is inert until `ANTHROPIC_API_KEY` is set as a repository secret, and it is
deliberately scoped to mechanical fixes. If a failure is not one of the known
classes it stops and explains rather than guessing.

---

## 3. How you are notified

**One tracking issue, labelled `build-promotion`.** Every promotion run comments
on it, and its state is the health signal:

- **Closed** means the last run succeeded.
- **Open** means the pipeline needs attention and the Thursday build will be
  stale or missing until it is fixed.

So "is there an open `build-promotion` issue?" answers "is the pipeline broken?"
at a glance, without opening the Actions tab.

**Subscribe to that issue** (Watch on the issue itself). That is how the weekly
report reaches your inbox. Every run comments, including successful ones, which
matters more than it sounds: a workflow that silently stops firing otherwise
looks exactly like a quiet week.

The same report is written to the workflow run summary in the Actions tab.

### The failure modes notifications will not cover

- **GitHub disables scheduled workflows after 60 days of no repository
  activity.** It emails first. This repo is active enough that it should not
  arise, but if the cron ever stops for no visible reason, check this.
- **Scheduled-workflow failure emails go only to whoever last edited the cron
  line**, not to the repository watchers. Do not rely on that channel; rely on
  the tracking issue.

---

## 4. Roadmap

Ordered by what actually bites first.

### R1. Make builds identifiable *(not started, highest value)*

**Problem.** `bundleVersion` is `0.2.0` on every branch, `AndroidBundleVersionCode`
is a static `11`, and Standalone `buildNumber` is `0`. When a tester says "it
crashed", there is no way to know what they ran. The `testbuild/YYYY-MM-DD` tags
the promotion workflow writes get you from a date to a commit, but only if
someone recorded the date accurately.

**How.** Stamp the commit into the build and show it in game.

1. Generate a version file at build time. In
   `Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.cs`, before the build:

   ```csharp
   var sha = Environment.GetEnvironmentVariable("BUILD_COMMIT_SHA") ?? "local";
   var stamp = $"{PlayerSettings.bundleVersion}+{sha[..Math.Min(8, sha.Length)]}";
   // Android: a monotonically increasing code is required by Play
   PlayerSettings.Android.bundleVersionCode = int.Parse(
       Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "1");
   // Write stamp into a Resources TextAsset the runtime can read
   ```

2. Have UGS pass its build number through as `BUILD_NUMBER`, and the commit as
   `BUILD_COMMIT_SHA`.
3. Display the stamp in a corner of the main menu and include it in crash
   reports via `CrashReportingService`.

**Effort.** Half a day. Everything downstream (bug triage, crash grouping, "is
this fixed in the build you have") depends on it.

### R2. Branch hygiene *(not started)*

**Problem.** 322 remote branches, 269 of them `claude/*`. Finding a real branch
means scrolling past hundreds of dead ones, and stale branches make it unclear
what is in flight.

**How.**

1. Settings → General → Pull Requests → **Automatically delete head branches**.
   Stops the bleeding immediately.
2. Sweep what is already merged:

   ```bash
   git fetch --prune origin
   # list branches already contained in trunk
   git branch -r --merged origin/bleeding-edge \
     | grep -v -E 'bleeding-edge|master|development|build/' \
     | sed 's|origin/||'
   ```

   Review that list, then delete in batches with
   `git push origin --delete <branch>...`.
3. For unmerged `claude/*` branches older than ~60 days, tag before deleting if
   you want the work recoverable: `git tag archive/<name> origin/<name>`.

**Effort.** One hour, mostly review.

### R3. Protect the build branches *(not started)*

**Problem.** Nothing stops a person pushing to `build/android`. That work is
destroyed on the next Thursday promotion, silently.

**How.** Settings → Branches → add a ruleset targeting `build/*`:

- Restrict who can push to the GitHub Actions app only
- Allow force pushes **for that actor** (the promotion needs them)
- Do not require PRs, since the workflow pushes directly

**Effort.** Ten minutes.

### R4. Get a Unity runner, then turn on the green gate *(blocked on hardware)*

**Problem.** `unity-ci.yml` is written but inert: no `UNITY_RUNNER_LABEL`, so
nothing actually compiles. Layer 1 catches mechanical errors, but only a real
compile catches the rest, and right now the first true compile of the week is
the UGS build itself.

**How.**

1. Register the existing build box as a repository self-hosted runner. This is
   the best fit: no license activation, no minute costs, and `Library/` stays
   warm so builds are incremental. See `Docs/BUILD_AND_DELIVERY.md` §10 for the
   alternatives and their trade-offs.
2. Set repository variables `UNITY_RUNNER_LABEL` and `UNITY_PATH`. Both
   `unity-ci.yml` and `build-branch-ci.yml` pick it up with no edits.
3. Once the edit-mode suite is green on trunk, set
   `BUILD_PROMOTION_REQUIRES_GREEN=true` so a red commit can no longer be
   promoted at all.

**Security note.** This repository is **public**. Do not attach a self-hosted
runner without first requiring approval for outside-contributor workflow runs,
or a fork PR will execute arbitrary code on the build machine.

**Effort.** A day, mostly runner setup.

### R5. Separate cert from test *(not needed yet)*

**Problem.** `build/*` is right for weekly QA, where the newest code wins. Store
submission is the opposite: the build must be frozen while review runs, which
can take days. Submitting from a branch that force-moves every Thursday means
the submitted commit is gone by the time a reviewer asks about it.

**How.** When the first store submission approaches, cut `release/<version>`
from a known-good trunk commit, build from that, and cherry-pick only
submission blockers onto it. `GIT_RULES.md` already anticipates `release/*`.

**Effort.** Nothing now. Decide before the first submission.

### R6. Collapse the parallel trunks *(the underlying issue)*

**Problem.** `master` drifted **3188 commits** behind `bleeding-edge`, to the
point where merging normally was not possible: the input system had been
restructured, audio had moved to FMOD, and the Unity version had moved from
6000.0.62f1 to 6000.3.17f1. The August 2026 sync resolved it by taking trunk's
tree wholesale. `development` is currently 208 commits down the same road.

That drift is not a merge problem, it is a shape problem. Three long-lived
parallel lines, each accepting its own work, will always diverge like this.

**How.** One trunk, short-lived branches off it, everything else strictly
downstream:

- Trunk takes all development.
- `master` only ever **receives** from trunk. It never accumulates unique
  commits, so it can always fast-forward and can never drift.
- When `development` takes over the trunk role, make that switch explicit:
  merge trunk into it, change the repository default branch on GitHub, and
  retire the old trunk. The promotion workflow follows the default branch
  automatically, so nothing in CI needs editing.
- Anything with unique commits that is not trunk is a branch that will need
  another 3000-commit rescue eventually. Either merge it or delete it.

**Effort.** A conversation, then ongoing discipline.

---

## 5. Configuration reference

| Name | Kind | Used by | Purpose |
|---|---|---|---|
| `BUILD_PROMOTION_REQUIRES_GREEN` | Repo variable | `sync-build-branches.yml` | `true` refuses to promote a commit whose check runs are red or absent. Leave unset until Unity CI runs, see R4. |
| `UNITY_RUNNER_LABEL` | Repo variable | `unity-ci.yml`, `build-branch-ci.yml` | Runner label. While unset, every Unity job is **skipped** rather than queued. |
| `UNITY_PATH` | Repo variable or runner env | same | Absolute path to the Unity 6000.3.17f1 executable. |
| `UNITY_TESTS_BLOCKING` | Repo variable | `unity-ci.yml` | `false` downgrades edit-mode failures to warnings. Temporary measure only. |
| `ANTHROPIC_API_KEY` | Repo secret | `build-branch-ci.yml` | Enables the autofix job. Inert while unset. |

### Workflows

| File | Trigger | Does |
|---|---|---|
| `sync-build-branches.yml` | Thursday 06:00 PT, or manual | Validates trunk, moves `build/*`, tags the build, reports to the tracking issue |
| `build-branch-ci.yml` | Push to `build/**` | Full static set, Unity compile where available, autofix on failure |
| `unity-ci.yml` | PR into trunk, nightly, weekly | Tiered compile and build verification |

Scheduled workflows only fire from the **default branch**. A change to any cron
here has no effect until it is merged to trunk.
