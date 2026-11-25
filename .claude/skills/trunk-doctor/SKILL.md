---
name: trunk-doctor
description: When bleeding-edge is red, find out exactly why and who broke it, get it green again with the smallest safe change (a fix forward or a revert PR), confirm the Bleeding-edge guard's next run is green, record the incident in Docs/Reports/TRUNK_HEALTH.md, and stop more pushes piling onto a red trunk (`trunk_doctor.py --gate`, called from /ship before every push and merge). Reads the guard's runs and the "bleeding-edge is broken" issues; classifies the failing log as static validation, generator drift or crash, compile, tests or infrastructure; names the PR that introduced each finding. Use on "/trunk-doctor", "is bleeding-edge green", "bleeding-edge is broken / red", "why is the guard red", "who broke the trunk", "fix the trunk", "the bleeding-edge is broken issue", "can I push", or when /ship's trunk gate stops.
---

# Trunk doctor - get bleeding-edge green, find who broke it, stop the pile-on

`bleeding-edge` is where all work lands (`Docs/BRANCHING_AND_RELEASE.md` 1). `.github/workflows/bleeding-edge-guard.yml`
verifies every push to it: **validate** (static checks and every generator's `--check`, ubuntu, about two minutes),
**unity** (a real compile and the edit-mode tests; skipped while `UNITY_RUNNER_LABEL` is unset, which it has been
since 2026-10-13), **report** (opens, comments on and closes the one `ci-bleeding-edge` issue titled "bleeding-edge is
broken") and **autofix** (a Claude repair PR; it has never run, no key is set). This skill is what a person or a
session does when that guard is red. Read `Docs/BUILD_AND_DELIVERY.md` 10.1 for the guard and
`Docs/Reports/TRUNK_HEALTH.md` for every past red trunk.

**What the history taught** (`trunk_doctor.py history`, `Docs/Reports/TRUNK_HEALTH.md`):
- **A red trunk gets piled on.** 2026-11-09 to 11-12: three generators that import numpy, which the runner does not
  have, held it red for about three days. 31 more PRs merged on top of it, from three people. Each new generator
  broke the same way and was invisible behind the first. The local gates passed every time because the developers'
  Python has numpy.
- **The issue's open/closed state lies.** Until this skill's first fix, the report job read a CANCELLED run (one a
  newer push superseded) as green, so it commented "Recovered" and closed the issue while the trunk was still red:
  #1867, #1872, #1889 and #1897 all closed that way. **Read the runs, never the issue state.** `trunk_doctor.py`
  skips cancelled runs.
- **The guard's own words can be wrong.** #1025 said "The project did not compile" and it was a Unity licence error
  on the runner (no `error CS` line, no results XML). #1037 was red for five days on a validator false positive from
  `Port/` code, fixed by scoping the checker (#1069), not by silencing it.

## 0. Rules (never broken, whatever the pressure)

- **Never force-push, never rewrite history**, on `bleeding-edge` or anyone's branch.
- **Never skip, disable, quarantine or loosen a check to go green.** No `|| true`, no `continue-on-error`, no
  generator that SKIPs when a dependency is missing, no threshold raised, no file added to an allow list to hide a
  finding. A real checker false positive gets a scoped, explained fix with a negative control proving the real case
  still fails (#1069 is the model).
- **Never edit another person's branch.** A revert is a NEW PR from a `trunk-doctor/` branch.
- **Never push to `bleeding-edge` directly.** The repair is a PR like any other.
- **Ask before merging anything**, the repair included. The person merges it or says "merge it".
- **A re-run is the whole fix only for infrastructure.** Never re-run a code failure hoping it goes green.

## 1. Status

```bash
python3 .claude/skills/trunk-doctor/trunk_doctor.py            # status: green or red, the runs, first red, every push since
python3 .claude/skills/trunk-doctor/trunk_doctor.py --gate     # exit 0 green, 2 red, 3 unknown (for skills)
```

It reads the guard's runs on `bleeding-edge` (`gh api`, or the public REST API) and prints:
- **GREEN or RED**, how long it has been red, and any run still in progress;
- the open tracking issue, or a warning that none is open while the trunk is red;
- the latest guard runs, then the **first red run** (its PR, author and who merged it), the **last green run**, and
  **every push since the first red one**, by whom, with a count per person ("pushed onto the red trunk").

It is a READER: it writes nothing and runs nothing on CI. Green means stop here. Give the person the status in a
few lines. Red means go on to §2.

## 2. Diagnose

```bash
python3 .claude/skills/trunk-doctor/trunk_doctor.py diagnose               # the newest red run
python3 .claude/skills/trunk-doctor/trunk_doctor.py diagnose --run <id>    # a given run
python3 .claude/skills/trunk-doctor/trunk_doctor.py diagnose --issue <n>   # the run a tracking issue names
python3 .claude/skills/trunk-doctor/trunk_doctor.py diagnose --log <file>  # a saved job log, offline
```

It pulls each failed job's log (cached under `~/.cache/trunk_doctor/`) and names the **failure class** and every
finding in it:

| Class | What it prints | Seen in |
|---|---|---|
| `static` | the check (the step's command), the file and line, the message; a hit outside `Assets/`, `Packages/` or `ProjectSettings/` is flagged "likely checker false positive" | #1037 (`Port/` code in `validate_project.py`) |
| `generator` | each red `author_*_assets.py`, and whether it is **drift** (the assets differ from a re-run) or a **crash** (it could not run; a missing module is named) | #1681, #1767 (drift); #1897 (numpy crash) |
| `compile` | each `error CS` with file, line and code | #826, #829, #870, #878 |
| `tests` | each failed edit-mode test; the project compiled | none yet (the runner is gone) |
| `infra` | licence, runner lost, timeout, network, disk, runner setup: no code fix | #1025 (licence, misreported as compile) |

Then **who introduced it.** For each finding it walks back through the earlier red runs to the first one that still
shows it, and lists the pushes between that run and the last verdict before it. Cancelled runs give no verdict, so
several pushes can share one range. Each push is marked when its PR touched the files involved. One push in range,
or one that touched the files, is named as **INTRODUCED BY** (the PR, its branch, its author and who merged it).
Otherwise it prints the bisect command:

```bash
git fetch origin bleeding-edge --deepen=200    # the range must be local
python3 .claude/skills/trunk-doctor/trunk_doctor.py bisect --good <sha> --bad <sha> --cmd "python3 Tools/Build/author_x_assets.py --check"
```

`bisect` walks the first-parent pushes of `bleeding-edge` in a throwaway worktree under the cache directory (never
your working tree) and removes it afterwards.

Then run `python3 Tools/Build/who_works_where.py query <the files>` and say who owns them. The person who MERGED is
not necessarily who wrote the change: name both.

## 3. Decide: fix forward or revert

`diagnose` prints a "Decide" block per class. The session makes the call with these rules:

**Fix forward** only when the fix is **small and certain**: you can name the exact change and show it working.
- a missing `using`, a renamed member the introducing PR obviously missed;
- a stale generator output, where the introducing PR changed the generator and did not re-run it (re-run it, commit
  its output);
- a checker false positive (a scoped checker fix with a negative control, as #1069 did);
- a missing CI dependency, where a check is right but the runner cannot run it (#1897: give the job the package. A
  skip is never the fix).

**Revert the offending merge** when the fix is unclear, large, or lands in someone else's area: their PR changed
assets the generator owns by hand, a compile break spread across files, tests the PR turned red. `git revert -m 1
<merge sha>` on a `trunk-doctor/` branch, opened as a PR that names the original author and links the original PR.
The author re-lands it fixed. A revert is not a judgement on the work.

**Infrastructure** is never a code change. Re-run the failed jobs (`gh run rerun <run id> --failed`), add a line to
the incident in TRUNK_HEALTH.md, and tell the person what was wrong with the runner. If the same infra failure
repeats, the runner or a workflow needs fixing. That is a finding for Shombith, who owns tooling, not a re-run loop.

A finding in another person's area (`who_works_where.py`, `Docs/TEAM.json` owners) goes into their `/approve` queue
either way (`python3 Tools/Build/approvals.py add --to <them> ...`). A fix forward that touches their code is
queued too; a revert names them in the PR.

## 4. Repair

1. **Branch** from the current tip: `git fetch origin bleeding-edge && git checkout -b trunk-doctor/<YYYY-MM-DD>-<short> origin/bleeding-edge`
   (`trunk-doctor/2026-11-12-numpy`). If the session may push only to one assigned branch, do the repair there and
   say so in the PR.
2. **Reproduce the failure locally first**, with the same check the guard ran, and paste the red output:
   `python3 Tools/CI/validate_project.py`, `bash Tools/CI/check_generators.sh <the generator>`,
   `python3 Tools/Build/check_*.py`, `bash Tools/Build/unity_refcompile/run.sh` for a compile error,
   `bash Tools/Build/verify_unity.sh` when an editor is available. **If it does not reproduce, find out why before
   changing anything.** A difference between your machine and the runner IS the finding (#1897: the developers'
   Python had numpy, the runner's did not. Reproduce with a Python that lacks it:
   `python3 -m venv --without-pip /tmp/x` and `PYTHONPATH=<pyyaml only>`).
3. **Make the smallest change** from §3. Nothing else rides along.
4. **Show the same check passing**, on the same command, and paste it. Then run the whole guard sweep
   (`bash .claude/skills/ship/gates.sh`) so the fix does not trade one red for another.
5. **Open the PR** against `bleeding-edge` with the diagnosis at the top:

   ```
   ## Trunk doctor: <one line: what is red and why>
   - Red since: <time> (run <n>), <k> pushes since; issue #<n>
   - Class: <static | generator | compile | tests | infra>; finding: <check, file:line>
   - Introduced by: #<PR> (<branch>) by <author>, merged by <merger>
   - Repair: <fix forward | revert of #<PR>>, because <rule from section 3>
   - Reproduced: <command> -> <red output>
   - Passes: <command> -> <green output>
   ```

   Then the change, the verification (the repo's verify rule: `unity_refcompile` for C#, or say it did not run), and
   "Affects: <person>'s <area>" for anyone whose work it touches. **Ask before merging.** /ship §5.7's merge applies
   only on the person's GO.

## 5. Confirm

After the repair merges:

```bash
python3 .claude/skills/trunk-doctor/trunk_doctor.py            # wait for the guard run on the merge commit
```

- **GREEN on the merge commit or later**: check the issue closed itself ("Recovered" comment). If it closed but the
  runs say red, that is the cancelled-run bug (§ What the history taught); say so.
- **Still RED with the same finding**: the repair did not reach the cause. Say why (read the new run's log with
  `diagnose`). Back to §2.
- **RED with a different finding**: it was hidden behind the first. That is a new incident with its own introducer.
- **The issue is still open while the runs are green**: the report job did not run or failed. Read its log; do not
  close the issue by hand.

## 6. Guard against piling on

`trunk_doctor.py --gate` is the check other skills call before they push or merge onto `bleeding-edge`:

| Exit | Meaning | What the caller does |
|---|---|---|
| 0 | green, or red but this change IS the fix (a `trunk-doctor/*` branch, or `--fix`) | carry on |
| 2 | red | **stop**: tell the person bleeding-edge is red, why (the gate prints the first red PR and the issue), and point to `/trunk-doctor`. Push only if the person says this change is the fix (rerun with `--fix`) or tells you to go ahead anyway. Record that in the PR. |
| 3 | unknown (no API, no verdict yet) | say so, link the guard's runs page, carry on |

/ship runs it in §1.5 before the push and in §5.7 before the merge. /ship-quick, /ship-deep and /ship promote
(/to-bleeding-edge) inherit both. There is no `/land-queue` skill yet; if one is made, it calls the gate before each
landing.

## 7. Record

Every incident gets a row in `Docs/Reports/TRUNK_HEALTH.md`: when it went red, the cause (class, finding), who
pushed the first red commit and whose PR it was, how long it stayed red, the fix (PR and kind), and how many pushes
landed while red. `python3 .claude/skills/trunk-doctor/trunk_doctor.py history --diagnose` prints those rows for
every streak in the guard's history: copy the new one, and add the fix and any note by hand. Commit it with the
repair, or in the confirm step once the guard is green.

## 8. Prove it works

```bash
python3 .claude/skills/trunk-doctor/trunk_doctor.py --self-test
```

No network. It classifies the fixture logs in `fixtures/`: the real logs of #1037 (static), #1767 (generator drift),
#1897 (generator crash), #870 (compile) and #1025 (infra licence), trimmed, and two synthetic ones (tests, runner
lost). It checks each class and its file, line and generator. Negative controls: a clean log and the guard's own
echoed script text are no finding, and an infra pattern never hides a real `error CS`. It also checks the
green/red/cancelled run logic on fixture runs: a green trunk reports green and `--gate` exits 0; a cancelled newest
run is no verdict; a red streak finds its first red and last green. Add a fixture whenever a new incident shape
appears.

## 9. Not this skill

- Fixing the underlying feature: once trunk is green, the owner's skill takes over (`/arcadegame` for a mode
  generator, `/bug-ledger` for console errors, `/vessel` ...).
- Other workflows: Amoebius parity CI, perf-daily and the build branches have their own reports
  (`Docs/Reports/CI.md`). `development` has the same guard. `trunk_doctor.py` reads `bleeding-edge` only.
- Closing out a bug in every record: `/bug-closeout`.
