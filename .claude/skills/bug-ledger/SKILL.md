---
name: bug-ledger
description: Fix the console errors collaborators sent from Unity with the one "Send to Claude" button after a play test (they land on origin/bugledger/<user> branches) - root cause, not silencing - and hand each fix back so their editor validates it over the next play runs; and answer "are my errors fixed?". Use on "/bug-ledger", "fix the console errors", "fix the errors I sent", "I got these errors while testing", "is it fixed yet", "check the bug ledger", a pasted screenshot or text of red Console errors, or the scheduled daily sweep.
---

# Bug Ledger — fix what the play tests sent

**There is no Unity UI at all** (`BugHuntAutoSend`; Yash, 2026-10-15: "no complex UI toolboxes in
Unity... just auto send consoles", and 2026-10-16: "make send to Claude automatic too, no question").
After a play test that logged a new red error the editor sends by itself: it pushes that machine's issues (plus new redlog / crash / perf
files) to its own branch **`origin/bugledger/<user>`**, under `BugLedger/shared/`, built in a
throwaway index so the person's working tree and branch are never touched (`BugLedgerSender`).
There is no Bug Ledger window, no staging, no settings — never tell anyone to press buttons in one.
Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger" / "Send to Claude".

## 1. Get the errors

```
git fetch origin
python3 Tools/Build/bug_ledger.py collect --out /tmp/board.json   # every origin/* branch, merged
python3 Tools/Build/bug_ledger.py show <id>                        # one issue in full
```

`collect` reads the `bugledger/*` branches (and older `BugLedger/shared/` folders on any branch).
Each issue lists the branches it is on — `bugledger/yskha` means it came from Yash's machine.
Report files: `git show origin/bugledger/<user>:BugLedger/shared/reports/<user>/<kind>/<file>`; a
**redlog** session has the full context around a ledger signature.

**Nothing sent?** Fix from what the person gave you (screenshot / pasted Console text), then tell
them in one line: after the next play test, do nothing: after the next play test the editor sends it to Claude by itself.

Kinds: `E-…` captured error (validated by play runs) · `T-…` editor-tool finding (closed only by a
clean re-run of that tool — tell the person which) · `C-…` hand-filed (no signature).

### Multiplayer test logs (the `net` report kind)

A multiplayer test (Unity MPPM, or Amoebius START ONLINE with + PLAYER 2) writes one log per player
at stop, and the same "Send to Claude" carries them to `reports/<user>/net/`. **Never ask the person
to paste a multiplayer console**: read the logs.

```
python3 Tools/Build/net_logs.py collect                 # every test on every origin/* branch
python3 Tools/Build/net_logs.py show 1                  # players' summaries + ONE merged timeline
python3 Tools/Build/net_logs.py show 1 --lines          # ... with every log line
python3 Tools/Build/net_logs.py collect --dir <folder>  # a local Logs/NetSessions or Amoebius netlogs
```

Each player's line gives its role, CLEAN/DIRTY (the record's verdict), errors, warnings, lobby
reads, 429s and lifecycle events; the timeline interleaves every player's lifecycle (party,
clientApproved, clientLeft, leaverToAI, refused, bounce) and warnings/errors in wall-clock order.
Use it to find which player saw what first. A test between two machines arrives on two
`bugledger/*` branches; `net_logs.py` groups the logs whose Plays overlapped into one test
(`belac/main`, `yskha/main`). Read `429s 2 (console 69)` as 69: the refresh ticks' 429s went uncounted
until 2026-10-16. A "carried over" mark means a pre-fix Unity file held earlier Plays: trust its
events, not its counters or its `why`. The party findings so far are B31-B33 in
`Docs/PartySystem/BUGS.md`. A test missing a player means that player never stopped cleanly
(a killed process writes nothing). A test with no logs at all means the person did not press
Send, or the main editor saw no other player (solo online play writes nothing): say so in one
line. The mechanism is in `Docs/DIAGNOSTICS.md` § "Multiplayer test logs".

## 2. Fix each one — properly

Worst first (open, then regressions, then times seen). One issue on several branches is ONE bug.

1. **Find the emitting line**: the stack excerpt skips Unity's logging plumbing, so its first line
   is the code that logged; otherwise `grep -rn` a distinctive phrase of the message. Most of this
   project's errors are deliberate diagnostics that NAME their remedy — read the whole message and
   the doc it cites. Note the issue's `scope`: an `EditMode` error captured right after a play run
   often fired DURING teardown (the Regatta "no course" error was a despawned controller's last frame).
2. **Root-cause it**: is the DATA, the CODE, or the CHECK wrong? Measure before you conclude.
3. **Look for an existing fix first**: `python3 Tools/Build/who_works_where.py query <files>` and
   `git log --all -S '<phrase>'` — the stale-`LocalPlayer` storm had a finished, unmerged fix on
   another branch for three weeks. Also check the area's task lists (e.g. FMOD content issues are
   already on `Docs/AudioSystem/CHARLES_TASKS.md`).
4. **Never fix by silencing**: downgrading `LogError`, deleting the log, a null guard that skips the
   work, or a `Debug.Log` are not fixes (CLAUDE.md § What Claude Code Should Never Do).
5. **Load the area's skill / topic file first**; shared prefabs and SOs are fixed once at the source.
6. A DESIGN or CONTENT call (an FMOD event's loop region, what a skimmer should do) is not yours:
   restore established behaviour if you can, and file it as a `decision` ticket (`/team-tasks add`, naming nobody).
7. Team rules (CLAUDE.md § Team): a fix in another collaborator's area beyond a wiring/data repair
   is a task for its owner, not a commit; commits there say `Affects: <person>'s <area>`.

## 3. Hand the fix back

Commit and **push the fix first**, then:

```
python3 Tools/Build/bug_ledger.py mark-fixed <id> --note "<root cause> — <what changed, where>"   # --commit defaults to HEAD
```

This rewrites the issue as VALIDATING **on the sender's `bugledger/<user>` branch** with the fix
commit (throwaway index; your HEAD, index and working tree are untouched). Their editor adopts it at
its next sync (editor load, or before a send) **only once that commit is in their HEAD** — tell them
to pull. Two clean play tests then close it unseen; if the error returns it reopens and their next
"Send to Claude" brings it back (`regressions > 0` = your fix did not hold: re-root-cause from
scratch). **A fix that reached some senders only** (the issue is `validating` on one `bugledger/*` branch and still
`open`, with no `fixCommit`, on another): hand it on with `mark-fixed <id> --commit <the same sha> --branch <user>`.
Copies already validating that commit are left alone (a rewrite would zero their clean runs), so never re-run a
plain `mark-fixed` just to reach one more tester. Skip it when the open copy has `regressions` > 0 — that machine
saw the error AFTER taking the fix. Leave `ignored` issues alone (`mark-fixed` refuses without `--force`); never delete issue
files — closing is the editor's job, after validation.

## 4. "Is it fixed?" — answer from their branch

`collect`, then read each of their issues:

| On their branch | Tell them |
|---|---|
| `open`, no `fixCommit` | not fixed yet (then fix it) |
| `validating`, `fixCommit` not in their branch yet | fixed in `<sha>` — pull it; it closes after two clean play tests |
| `validating`, `cleanSessions` k of n | fixed and being proven: n−k more clean play tests close it |
| gone from the branch | closed (it stayed silent), or they discarded it |
| `open` with `regressions` > 0 | the fix did not hold — say so, and fix it again |

**A fix stuck at 0 of n** although they have played since pulling it: read their trail,
`git show origin/bugledger/<user>:BugLedger/shared/reports/<user>/ledger/validation.log`. Each play run
has a line (its length, the fixes it counted, what fired); "waiting for a pull" means the fix commit
is not in their checkout; no play-run lines at all means the ledger's play-mode handler never ran.

## 5. Verification

- C# changes: `/verify-unity`; with no editor, a *Verification status* section in the commit saying
  so. Compile against stubs with Roslyn anyway (`/asset-surgery` §4).
- The gates you touched, and `python3 Tools/Build/bug_ledger.py --self-test` if you touched the script.

## 6. The daily sweep (scheduled task, once a day)

`collect` → fix what you can on `Ys-bleeding-edge` (§2-§3) → import bugs filed on the Team Bug Board
(`ArtifactData list` of `filed`, rows with `status == "new"`: `bug_ledger.py file --title … --notes …
--severity … --reporter "Team Bug Board"`, commit, then mark the row `imported` with `importedAs`) →
mirror into the board (https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) with `ArtifactData`
batches: `issues/<id>` per issue (drop `signature`, `stack` ≤ 1500 chars), delete issues gone from
every branch, `board/meta` `{generatedUtc, branchesScanned, reports ≤ 300}`, and `runs/<yyyy-mm-dd>`
`{date, branch, summary, fixed[], commit}` — written even when nothing was found.
