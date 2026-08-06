# Build Pipeline Setup

One-time setup checklist. Steps 1 to 4 are required; step 5 is optional.

*Why any of this works the way it does: `Docs/BRANCHING_AND_RELEASE.md`. This
page is only the steps.*

---

## 1. Create the build branches

`build/android` and `build/windows` do not exist yet. They are created by the
first promotion run, so trigger one now rather than waiting for the schedule.

1. GitHub → **Actions** tab
2. Left sidebar → **Promote test build**
3. **Run workflow** button (top right)
4. Leave `source_ref` **blank**, leave `targets` as **both**
5. **Run workflow**

**Expect:** the run goes green in under a minute, and `build/android` and
`build/windows` appear in the branch list.

> Blank `source_ref` means "use `development`". Manual runs skip the schedule
> checks, so it runs immediately instead of waiting for the cycle.

- [ ] Both build branches exist

---

## 2. Set up UGS Build Automation

You need **4 build targets**: two for the test build, two for the internal build.

### Test builds (what testers get)

| Target | Branch | Auto-build on push |
|---|---|---|
| Android test | `build/android` | **On** |
| Windows test | `build/windows` | **On** |

Auto-build on push is what makes these fire. The promotion workflow pushes to
these branches every 3 weeks and UGS picks it up from there.

### Internal builds (what the team gets)

| Target | Branch | Auto-build on push | Schedule |
|---|---|---|---|
| Android internal | `bleeding-edge` | **Off** | Weekly, Friday |
| Windows internal | `bleeding-edge` | **Off** | Weekly, Friday |

Auto-build stays **off** here or you get a build on every merge to trunk.

- [ ] 4 targets created and pointed at the right branches

---

## 3. Turn on build notifications

The first promotion run (step 1) creates a GitHub issue labelled
`build-promotion`.

1. Open that issue
2. Click **Subscribe**

Every promotion comments on it. **Closed = healthy. Open = the test build is
broken.**

- [ ] Subscribed to the issue

---

## 4. Repository settings

**Settings → General → Pull Requests**

- [ ] Tick **Automatically delete head branches** (stops branch sprawl)

**Settings → Rules → Rulesets → New branch ruleset**

- Ruleset name: `Build branches`, enforcement **Active**
- Target branches → **Add target → Include by pattern** → `build/*`
- [ ] Tick **Restrict deletions**
- [ ] **Untick Block force pushes** — it is on by default and **would break the
      promotion**, which force-moves these branches every cycle
- Leave every other rule off

> **Do not tick "Restrict updates".** It means "only users with bypass
> permissions can push", and GitHub Actions **cannot** be added to a bypass list
> (eligible actors are repo admins, org/enterprise owners, maintain/write-role
> users, teams, GitHub Apps and Dependabot). Ticking it stops your own promotion
> workflow.
>
> Before settling for that, open **Add bypass** and search `actions`. If your
> plan does surface a usable GitHub Actions entry, add it, and you can then tick
> **Restrict updates** and leave **Block force pushes** on for the strong
> version of this rule. If it is not there, use the settings above.

This protects against accidental deletion, not against a human pushing to a
build branch. That residual risk is accepted: such a push is overwritten by the
next promotion, which is the documented behaviour of these branches.

---

## 5. Optional: real compilation

Everything above works today. This section adds a Unity compile before builds
reach UGS. Skip it until you want it.

### 5a. Secure the repo first

**This repository is public.** Do this before attaching a runner, or a fork PR
can run arbitrary code on your build machine.

**Settings → Actions → General → Fork pull request workflows**

- [ ] Require approval for **all outside collaborators**

### 5b. Attach the runner

**Settings → Actions → Runners → New self-hosted runner**, register your build
machine, then set two variables under
**Settings → Secrets and variables → Actions → Variables**:

| Variable | Value |
|---|---|
| `UNITY_RUNNER_LABEL` | the runner's label |
| `UNITY_PATH` | full path to the Unity **6000.3.17f1** executable |

Unity jobs are skipped while `UNITY_RUNNER_LABEL` is unset, so nothing breaks
before this.

- [ ] Runner registered, both variables set

### 5c. Block broken commits

Only after 5b, and only once the edit-mode tests actually pass:

| Variable | Value |
|---|---|
| `BUILD_PROMOTION_REQUIRES_GREEN` | `true` |

Set this too early and every promotion blocks on tests that were never green.

- [ ] Enabled (after tests pass)

### 5d. Autofix

**Settings → Secrets and variables → Actions → Secrets**

| Secret | Value |
|---|---|
| `ANTHROPIC_API_KEY` | your API key |

Lets failed checks attempt their own fix and open a PR against `bleeding-edge`.

- [ ] Added

---

## When things run

| What | When |
|---|---|
| Internal build | Every **Friday** |
| Test build | Every **3 weeks, Wednesday** — first is **12 Aug 2026** |
| Release build | Manual, see `BRANCHING_AND_RELEASE.md` §3 |

All automated times are 06:00 Pacific.

---

## Your only recurring job

Before a Wednesday cycle, if testers should get newer code:

```bash
git fetch origin
git checkout development
git merge --ff-only origin/bleeding-edge
git push origin development
```

If `--ff-only` refuses, **stop** — someone committed directly to `development`.
Don't force it. See `BRANCHING_AND_RELEASE.md` §6 R6.

---

## Known gaps

Not blocking, but worth knowing:

- **You can't tell which build a tester is running.** Every build reports
  version `0.2.0`. Fix in `BRANCHING_AND_RELEASE.md` §6 R1 — highest value item.
- **321 branches**, 82 safely deletable. Cleanup commands in §6 R2.
- **No release automation.** Deliberate; §6 R5.
