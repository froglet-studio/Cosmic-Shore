# Branching and Release

What each branch is for, when builds happen, and what to do when something
breaks.

> **Setting the pipeline up for the first time?** Follow
> [`BUILD_PIPELINE_SETUP.md`](BUILD_PIPELINE_SETUP.md) — the click-by-click
> checklist for GitHub, CI and UGS. This document is the reasoning behind it.

---

## 1. The four branches, in one line each

| Branch | What it is |
|---|---|
| `bleeding-edge` | Where you work. All development lands here. |
| `development` | What testers get. Updated from `bleeding-edge` when you decide a batch is worth testing. |
| `master` | What players get. Release builds only. |
| `build/android`, `build/windows` | Not branches you use. Robot-owned snapshots that Unity Build Automation reads. |

Work flows one direction only:

```
   you write code
        ↓
   bleeding-edge  ──► internal build,  every Friday
        ↓
   development    ──► TEST build,      every 3 weeks on Wednesday
        ↓
   master         ──► release build,   when you ship
```

Nothing ever flows back up. `development` never gets its own commits; it only
receives from `bleeding-edge`. `master` only receives from `development`.

---

## 2. The three builds

| Build | Source branch | When | Who sees it |
|---|---|---|---|
| **Internal** | `bleeding-edge` | Every **Friday**, 06:00 PT | The team |
| **Test** | `development` | Every **3 weeks**, **Wednesday**, 06:00 PT | Testers |
| **Release** | `master` | When you decide | Players |

### Internal build (Friday)

UGS watches `bleeding-edge` directly. There is no snapshot branch and nothing
gets promoted. You do not have to do anything for this build to happen.

The only automation is `tag-internal-build.yml`, which writes a tag like
`internal/2026-08-07` on whatever `bleeding-edge` pointed at when the slot
opened. That tag is the point: `bleeding-edge` will have moved on by the time
someone reports a problem, and without the tag there is no way back to the
commit that was actually built.

### Test build (every 3 weeks, Wednesday)

This one is automated end to end by `sync-build-branches.yml`:

1. Checks it is really 06:00 Pacific and really an on-cycle week.
2. Runs static validation on `development`. **If that fails, it stops here** and
   nothing is promoted.
3. Force-moves `build/android` and `build/windows` to that commit.
4. Tags it `testbuild/2026-08-12`.
5. UGS sees the push and starts building.
6. Comments the result on the tracking issue.

**First run: Wednesday 12 August 2026.** Then every 21 days:

| | |
|---|---|
| 12 Aug 2026 | 2 Sep 2026 |
| 23 Sep 2026 | 14 Oct 2026 |
| 4 Nov 2026 | 25 Nov 2026 |
| 16 Dec 2026 | 6 Jan 2027 |

To move testers to a newer batch, merge `bleeding-edge` into `development`
before the Wednesday. That is the only manual step in the whole cycle.

### Release build (master)

**No automation today, on purpose.** Releases are rare and you want a human
picking the exact commit. When the first real release approaches, see R5 in §6.

---

## 3. What you actually have to do

**Every cycle:** merge `bleeding-edge` into `development` when a batch is ready
for testers. That is it.

**Once, during setup:**

1. Point the UGS Android and Windows *test* targets at `build/android` and
   `build/windows`, with auto-build on push.
2. Point the UGS *internal* targets at `bleeding-edge`, on a Friday timer.
3. Subscribe to the `build-promotion` tracking issue (§4).

**Never:** commit to `build/android` or `build/windows`. They are force-moved
every three weeks, so anything you put there is destroyed and the problem you
fixed comes straight back. Fix it on `bleeding-edge` instead.

> ### The build branches do not exist yet
>
> They are created by the first run of the promotion workflow, so you will not
> find them in the branch list until then. That is expected, not a mistake.
>
> To create them now instead of waiting: **Actions → Promote test build → Run
> workflow**, leaving `source_ref` blank. Manual runs skip both the clock and
> the cycle check, so it will run immediately.

### Runbook: promote `bleeding-edge` to `development`

Do this before a Wednesday cycle when you want testers on a newer batch. Skip it
and the cycle simply rebuilds what testers already have, which is a valid choice.

```bash
git fetch origin
git checkout development
git merge --ff-only origin/bleeding-edge
git push origin development
```

That is the whole operation. `--ff-only` is the safety catch, not a formality:
it succeeds only if `development` has no commits of its own. **If it refuses,
do not force it and do not merge manually.** Something has committed directly to
`development`, which breaks the one-way rule, and that stray commit is the thing
to find. See §6 R6.

Verify before the cycle runs:

```bash
git diff --stat origin/development origin/bleeding-edge   # empty = in sync
```

Prefer the GitHub UI? Open a PR from `bleeding-edge` into `development` and use
**Create a merge commit**. **Never squash a promotion** — it discards the shared
history that makes the next one a fast-forward, which is exactly how these
branches drifted 3000 commits apart before.

### Runbook: cut a release

There is no release automation yet (§6 R5), so this is deliberate and manual.
Release from `development`, not `bleeding-edge`: `development` is the code that
has actually been through a test build.

1. **Pick the commit.** Normally `development`'s tip, and normally one that
   testers have already been running for a cycle. If you need an older one, take
   the `testbuild/YYYY-MM-DD` tag for the build QA signed off on.

2. **Set the version.** Bump `bundleVersion` in `ProjectSettings/ProjectSettings.asset`
   on `bleeding-edge` and let it flow down, rather than editing it on `master`,
   which would give `master` a commit of its own and break the fast-forward rule.

3. **Move `master`:**

   ```bash
   git fetch origin
   git checkout master
   git merge --ff-only origin/development
   git push origin master
   ```

   Same `--ff-only` rule, same reasoning.

4. **Tag it**, so the shipped build is recoverable after the branches move on:

   ```bash
   git tag -a v0.3.0 -m "Release 0.3.0"
   git push origin v0.3.0
   ```

5. **Build from `master` in UGS**, manually. Until R1 lands, the tag is the only
   link between what players are running and a commit, so do not skip step 4.

6. **Store submission?** Do not build directly from `master` for a submission
   that will sit in review for days while `master` may move. Cut
   `release/<version>` from the tagged commit and build from that. See §6 R5.

---

## 4. How you find out something broke

**One GitHub issue, labelled `build-promotion`.** Every test-build promotion
comments on it, and its open/closed state is the signal:

| Issue is | Means |
|---|---|
| **Closed** | Last promotion worked. Nothing to do. |
| **Open** | Promotion failed. **The test build is stale or missing.** |

Subscribe to that issue and you get an email every cycle. Successful runs
comment too, which matters: a workflow that quietly stops firing otherwise
looks exactly like a normal quiet week.

Two things that will not notify you, so do not rely on them:

- GitHub **disables scheduled workflows after 60 days** of no repository
  activity. It emails first.
- For scheduled workflows, GitHub's own failure email goes **only to whoever
  last edited the cron line**, not to the repo watchers.

---

## 5. What stops a broken build reaching testers

Three checks, cheapest first. The point is that each one catches what it can so
the expensive ones are not the first to notice.

| | Check | Runs | Takes | Blocks the build? |
|---|---|---|---|---|
| 1 | `validate_project.py` | Before the branches move | seconds | **Yes** |
| 2 | `build-branch-ci.yml` | After they move | seconds, or ~1h with a Unity runner | No, reports |
| 3 | UGS build | Last | 20-60 min | It is the build |

### Check 1: what it actually looks for

It needs no Unity install, which is why it can run on every promotion. It looks
for mistakes that break a *player* build while compiling perfectly fine in the
editor:

**`UnityEditor` code reaching player code — blocks the promotion.** `UnityEditor`
does not exist in a shipped game, so this is a hard failure at build time even
though everything looks fine in the editor. This is the recurring one. It caused
both *Move editor scripts to Editor folder to fix player build errors* and *Fully
qualify Editor base class to avoid namespace conflict*.

**A MonoBehaviour whose class name does not match its file name — warning only.**
Unity refuses to attach the component and you find out later as a null in a
scene. Warning rather than blocking, because one file holding several
MonoBehaviours is a normal pattern.

**Missing or orphaned `.meta` files — warning only.** How GUID churn and
"missing script" errors start when several people share scenes.

Only the first can stop a build. A check that cries wolf gets switched off.

Run it yourself before pushing anything structural:

```bash
python3 Tools/CI/validate_project.py
```

### Check 3 is the real one

Static checks cannot prove a build succeeds. They catch a specific set of known
mistakes. Shaders, addressables, asset imports and platform toolchains are only
proven by the UGS build itself.

### Autofix

If check 1 or 2 fails, `build-branch-ci.yml` can attempt the repair itself,
reading `CLAUDE.md` and `.claude/skills/`, and open a PR **against
`bleeding-edge`**. Never against a build branch, for the reason in §3.

Inert until `ANTHROPIC_API_KEY` is set. It is scoped to mechanical fixes and
stops rather than guessing if the failure is not one it recognises.

---

## 6. What is still missing

Ordered by what hurts first.

### R1. You cannot tell which build a tester is running

**The problem.** `bundleVersion` is `0.2.0` on every branch, `AndroidBundleVersionCode`
is stuck at `11`, Standalone `buildNumber` is `0`. When a tester says "it
crashed", nothing in the build tells you what they ran. The `testbuild/` and
`internal/` tags get you from a *date* to a commit, but only if someone wrote
the date down correctly.

**The fix.** Stamp the commit into the build and show it on screen.

1. In `Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.cs`, before building:

   ```csharp
   var sha = Environment.GetEnvironmentVariable("BUILD_COMMIT_SHA") ?? "local";
   var stamp = $"{PlayerSettings.bundleVersion}+{sha[..Math.Min(8, sha.Length)]}";
   PlayerSettings.Android.bundleVersionCode = int.Parse(
       Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "1");
   // write `stamp` into a Resources TextAsset the runtime can read
   ```

2. Have UGS pass its build number as `BUILD_NUMBER` and the commit as
   `BUILD_COMMIT_SHA`.
3. Show the stamp in a corner of the main menu, and attach it to crash reports
   via `CrashReportingService`.

**Effort:** half a day. Bug triage, crash grouping and "is this fixed in your
build" all depend on it.

### R2. 322 branches, 269 of them `claude/*`

**The fix.**

1. Settings → General → Pull Requests → **Automatically delete head branches**.
   Stops it getting worse immediately.
2. Clear the backlog:

   ```bash
   git fetch --prune origin
   git branch -r --merged origin/bleeding-edge \
     | grep -v -E 'bleeding-edge|master|development|build/' \
     | sed 's|origin/||'
   ```

   Review, then `git push origin --delete <branch>...` in batches.
3. For unmerged branches older than ~60 days, tag before deleting if you want
   them recoverable: `git tag archive/<name> origin/<name>`.

**Effort:** an hour, mostly reading the list.

### R3. Anyone can push to the build branches

**The fix.** Settings → Branches → ruleset targeting `build/*`:

- Restrict pushes to the GitHub Actions app only
- Allow force pushes **for that actor** (promotion needs them)
- Do not require PRs; the workflow pushes directly

**Effort:** ten minutes.

### R4. Nothing actually compiles before the UGS build

**The problem.** `unity-ci.yml` is written but inert, because no runner is
configured. Check 1 catches mechanical errors; only a real compile catches the
rest. Today the first true compile of a cycle is the UGS build itself.

**The fix.**

1. Register the existing build box as a self-hosted runner. Best fit: no license
   activation, no minute costs, and `Library/` stays warm so builds are
   incremental. Alternatives in `Docs/BUILD_AND_DELIVERY.md` §10.
2. Set repository variables `UNITY_RUNNER_LABEL` and `UNITY_PATH`. Both
   `unity-ci.yml` and `build-branch-ci.yml` pick them up with no edits.
3. Once the edit-mode suite is green, set `BUILD_PROMOTION_REQUIRES_GREEN=true`
   so a red commit cannot be promoted at all.

> **This repository is public.** Do not attach a self-hosted runner without first
> requiring approval for outside-contributor workflow runs, or a fork PR can run
> arbitrary code on the build machine.

**Effort:** a day, mostly runner setup.

### R5. Release builds are entirely manual

Fine for now. When the first store submission approaches, the shape to build is
a `release/<version>` branch cut from a known-good `master` commit, with only
submission blockers cherry-picked onto it. A store review can take days and the
submitted commit must not move underneath it, which is exactly why `master`
cannot be a branch that gets force-moved. `GIT_RULES.md` already anticipates
`release/*`.

### R6. Why `master` and `development` were 3000 commits behind

**What happened.** `master` drifted **3188 commits** behind `bleeding-edge` and
could not be merged normally: the input system had been restructured, audio had
moved to FMOD, and Unity had gone from 6000.0.62f1 to 6000.3.17f1. `development`
was 2772 behind. Both were fixed in August 2026 by taking `bleeding-edge`'s tree
wholesale.

**Why it happened.** All three branches were accepting their own commits. Three
lines each taking their own work will always diverge like that. It is a shape
problem, not a merge problem.

**How the model in §1 prevents it.** `development` and `master` only ever
*receive*. Because they never accumulate unique commits, every update is a
fast-forward, so they cannot drift and can never need rescuing again.

If you ever find yourself resolving conflicts merging `bleeding-edge` into
`development`, something has committed directly to `development` and that is the
bug to fix.

---

## 7. Reference

### Files

| File | What it does |
|---|---|
| `.github/workflows/sync-build-branches.yml` | Test build promotion, Wednesday every 3 weeks |
| `.github/workflows/tag-internal-build.yml` | Tags `bleeding-edge` every Friday |
| `.github/workflows/build-branch-ci.yml` | Verifies what landed on `build/**`, autofix |
| `.github/workflows/unity-ci.yml` | Tiered compile verification (inert, needs a runner) |
| `Tools/CI/validate_project.py` | The static checks |
| `Tools/CI/test_validate_project.py` | Self-test for the above, 16 cases |

### Settings

| Name | Kind | Default | What it does |
|---|---|---|---|
| `BUILD_SOURCE_BRANCH` | Variable | `development` | Which branch test builds come from |
| `BUILD_PROMOTION_REQUIRES_GREEN` | Variable | off | Refuse to promote a commit with red or missing CI. Leave off until R4. |
| `UNITY_RUNNER_LABEL` | Variable | unset | Runner for Unity jobs. While unset those jobs are **skipped**, not queued. |
| `UNITY_PATH` | Variable | unset | Path to the Unity 6000.3.17f1 executable |
| `ANTHROPIC_API_KEY` | Secret | unset | Enables autofix |

### Two things about scheduling

**Cron cannot say "every 3 weeks."** The workflow fires every Wednesday and
counts days from the anchor date `2026-08-12`, proceeding only when that count
divides by 21. Change the cadence by editing `CYCLE_ANCHOR` and `CYCLE_DAYS` at
the top of the workflow.

**Scheduled workflows only run from the default branch.** The default branch is
`bleeding-edge`. Editing a cron on any other branch changes nothing until it
merges there.
