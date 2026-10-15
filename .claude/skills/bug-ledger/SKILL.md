---
name: bug-ledger
description: Read the console errors the Unity editor captured during play tests (the Bug Ledger, FrogletTools > Diagnostics > Bug Ledger) and FIX them - root cause, not silencing - then hand each fix back so the editor validates it over the next play runs. Use on "/bug-ledger", "fix the console errors", "I got these errors while testing", "fix the errors from my last test", "check the bug ledger", a pasted screenshot or text of red Console errors, or a scheduled run that checks BugLedger/shared/issues for new issues. Also use when the person asks how to make errors reach Claude automatically after each test.
---

# Bug Ledger — fix what the play tests logged

The editor's **Bug Ledger** auto-captures every distinct error / exception / assert signature into
`BugLedger/local/` (gitignored, machine-local). With **Auto-publish after each play test** on, each
play run that files a new error, reopens a regression or closes an issue pushes the change to
`BugLedger/shared/issues/` on the current branch. That folder is your input. Read
`Docs/DIAGNOSTICS.md` § "The agent loop" once for the whole mechanism.

## 1. Get the issues

```
git fetch origin <branch> && git pull origin <branch>      # the auto-publish commits live here
python3 Tools/Build/bug_ledger.py list                      # open, worst first (regressions first)
python3 Tools/Build/bug_ledger.py show <id>                 # message, stack excerpt, scope, counts
```

**Nothing published?** (`No open issues. Store: (no BugLedger/shared/issues yet)`) — the person
has auto-publish off or never pushed. Fix from what they gave you (screenshot / pasted Console
text), then tell them in one line: *Bug Ledger ▸ Settings ▸ "Auto-publish after each play test"*
makes the next test's errors reach you without copying them. Never ask them to hand-edit JSON.

Kinds: `E-…` auto-captured error (validated by play runs) · `T-…` editor-tool finding (closed
only by a clean re-run of that tool — fix it, mark-fixed it, and tell the person which tool to
re-run) · `C-…` hand-filed (no signature; a human resolves it).

## 2. Fix each one — properly

For every open issue, worst first:

1. **Find the emitting line** from `sample` / `stack` (`grep -rn` a distinctive phrase of the
   message). Most of this project's errors are deliberate, one-shot diagnostics that NAME their
   own remedy ("Run FrogletTools > …", "widen the band if …", "Check the vessel's …") — read the
   whole message and the doc it cites before deciding anything.
2. **Root-cause it.** The log is a symptom report. Decide which is wrong: the DATA (an unwired
   field, a mis-authored asset), the CODE, or the CHECK itself (a sanity band that does not fit a
   real case). Measure before you conclude — `Tools/Build/` has offline readers for most assets
   (FBX: `measure_vessel_models.py`, prefab YAML, SO assets).
3. **Never fix by silencing**: downgrading `LogError` to a warning, deleting the log, a null guard
   that just skips the work, or a `Debug.Log` are not fixes (CLAUDE.md § What Claude Code Should
   Never Do). The one legitimate change to a check is when the check is provably wrong for a real
   case — then fix the check's DATA (e.g. the band) and say what you measured.
4. **Load the area's skill / topic file first** (`/vessel`, `/ecology`, `/prism-morph`, … and the
   `Docs/claude/` index in CLAUDE.md) — the same rules apply as for any change in that area.
5. A shared prefab or SO is the fix site when every consumer has the same hole (fix it once, at
   the source — CLAUDE.md § Shared prefabs). Check every nesting consumer's overrides first.
6. A fix that is a DESIGN call (what should this vessel's skimmer actually do?) is not yours to
   invent: make the error go away only by restoring the established behaviour, and name the open
   design question in the note and to the person.

## 3. Hand the fix back

```
python3 Tools/Build/bug_ledger.py mark-fixed <id> --note "<root cause> — <what changed, where>"
```

This writes the SHARED copy as `validating`. Commit it **in the same commit as the fix** (the code
and its claim travel together), conventional-commit style, then push to the session's branch.
When the person pulls, the editor adopts it (console: `Adopted a published fix`) and their next
`cleanSessionsRequired` play runs close it — or it reopens as a regression and comes back to you
via the next auto-publish. A **regressed** issue (`regressions > 0`) means your last fix did not
hold: re-root-cause from scratch; the same fix is never re-adopted.

Do not touch `ignored` issues (a human parked them; `mark-fixed` refuses without `--force`), and
never delete files from `shared/` — closing is the editor's job, after validation.

## 4. Verification and the report

- C# changes: `/verify-unity` gates every C# commit. If no editor is reachable in this session, say
  so in a *Verification status* section of the commit body / PR (where the `/qa-backlog` scan picks
  it up — `Docs/UNITY_VERIFICATION_CHECKLIST.md` takes no new sections). You can still
  COMPILE editor/runtime files against stubs with Roslyn (`/asset-surgery` §4) — do it.
- Run the gates your change touches (`check_console_logging.py`, `check_conditional_compilation.py`,
  an `author_*.py --check`, the area's verify script) and `python3 Tools/Build/bug_ledger.py --self-test`
  if you touched the script.
- Report per issue: the error, the root cause, the fix, what the person should see in their next
  play test, and anything left for a human (design questions, tools to re-run).

## 5. "Automatically after each test"

The editor half is the auto-publish setting plus the **Bug Hunt check-in** (`BugHuntCheckIn`: at most
once a day, on load or after a play test, and only when something is new, it asks the collaborator
to share their errors, redlog sessions, crash reports and perf JSONs — `BugHuntReports` copies those
into `BugLedger/shared/reports/<user>/<kind>/`). The Claude half is the **daily sweep** below, run
by a scheduled task (Yash asked for once a day, 2026-10-15) or by hand with `/bug-ledger sweep`.

## 6. The daily sweep (every collaborator, every branch)

Collaborators publish to whatever branch they are on, so the sweep reads ALL of them:

```
git fetch origin
python3 Tools/Build/bug_ledger.py collect --out /tmp/board.json   # every origin/* branch, merged
```

1. **Triage** `board.json` worst first (open, then regressions, then times seen). An issue on several
   branches is ONE bug. Read the shared report files it points at (`git show origin/<branch>:<path>`)
   — a redlog session gives the full context around a ledger signature.
2. **Fix on `Ys-bleeding-edge`** (the sweep's working branch), one commit per issue, §2-§4 rules,
   `mark-fixed` in the same commit. **Team rules apply** (CLAUDE.md § Team): run
   `who_works_where.py query` on the files a fix touches; a fix in another collaborator's area that
   is more than a wiring/data repair is a REPORT (a `team_tasks.py add` for the owner), not a commit —
   and every commit there says `Affects: <person>'s <area>`.
3. **Import bugs filed on the board** — `ArtifactData list` of `filed`, rows with `status == "new"`:
   `bug_ledger.py file --title … --notes … --severity … --reporter "Team Bug Board"`, commit, then
   `update` the row to `{status: "imported", importedAs: "<C-id>"}`.
4. **Mirror the board** into the Team Bug Board artifact
   (https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno, `BugHuntCheckIn.TeamBoardUrl`) with
   `ArtifactData` batch writes (≤ 50 per batch): one `issues/<id>` doc per issue from `board.json`
   (drop `signature`; keep `stack` ≤ 1500 chars), delete `issues/<id>` docs no longer on any branch,
   `board/meta` = `{generatedUtc, branchesScanned, reports}` (reports capped at 300 rows), and one
   `runs/<yyyy-mm-dd>` doc `{date, branch, summary, fixed: ["<id> — <one line>"], commit}`.
   Never republish the page for data — it reads the store.
5. **Report** in the run doc: found / fixed / left for a human (and why). Nothing found is a valid
   run — write the run doc anyway, so the board shows the sweep is alive.
