---
name: proven-win
description: Prove and ship ONE cost-free performance improvement to the Cosmic Shore Unity project at a time, measured in real Unity on the GPU machine, by a fixed checklist and three tools that make every judgment mechanical - run.py (all measurements and controls on D3D12, Vulkan and D3D11), verdict.py (PASS / FAIL / HOLD by the rules), signoff.py (the blind A/B page). Use on "/proven-win", "prove a win", "measure this change in real Unity", "is this stand-in really free", "A/B this shader for sign-off", "take the next candidate", or with a candidate from Docs/ProvenWin/WINS.md's queue or /proven-win-scout.
---

# Proven Win

Follow these steps IN ORDER. Do not skip, merge or reorder steps. Each step says what "done" looks like; do not go on
until it is true. When a step says STOP, tell the person what it says and end there. Background and every trap that
cost a day: `Docs/ProvenWin/PLAYBOOK.md`. The ledger of proven, rejected and queued candidates: `Docs/ProvenWin/WINS.md`.
Tools: `Tools/Build/proven_win/` (`run.py`, `verdict.py`, `signoff.py`). Candidate format: `Docs/ProvenWin/candidates/README.md`.

On Windows there is no `python3`: use `python`. In Git Bash an argument that starts with `/` (like `/proven-win` in
an `approvals.py` summary) is silently rewritten to a Windows path: prefix the command with `MSYS_NO_PATHCONV=1` or
run it from PowerShell. Unity runs take 10-30 minutes: start `run.py` in the background and
wait for it to finish; never start a second Unity on the same worktree while one runs, and do not edit `Assets/` in the
worktree while it runs (the runner puts back every `Assets/`, `ProjectSettings/` and `Packages/` file that changed).

## 1. Take ONE candidate from the queue (done: `queue.py take` printed an entry and marked it measuring)

The queue is `Docs/ProvenWin/QUEUE.json` (view: `QUEUE.md`). Change it only with `Tools/Build/proven_win/queue.py`,
run in your bench worktree (step 2; make it first if it does not exist yet), on a branch cut from `origin/bleeding-edge`.
```
git fetch origin bleeding-edge            # the queue's file hashes are checked against what you have
python Tools/Build/proven_win/queue.py stale
python Tools/Build/proven_win/queue.py count
```
- If `count` is below 3, run `/proven-win-scout` FIRST (it refills the queue), then come back here.
- Then `python Tools/Build/proven_win/queue.py take` (the best queued entry) and read the entry it prints: `where`,
  `runtimeWriters`, `change`, `probes`, `negativeControl`, `otherStates` become your candidate folder in step 3.
- A stale entry (its files changed since it was queued) is never measured as it is: re-read the code, then either
  `add` a corrected entry under a new id or finish it with `done --outcome rejected`.
- STOP if you cannot confirm `runtimeWriters` in the code: an unknown runtime state is how the first measurement of W1
  measured the wrong crystal.

## 2. Your own worktree on the GPU machine (done: `git -C <wt> status` is clean, Library copied)

```
git -C <main repo> fetch origin bleeding-edge
git -C <main repo> worktree add -b <branch> D:/ABLab/wt origin/bleeding-edge
robocopy <a worktree with a warm Library for the same build target> D:/ABLab/wt/Library /E /MT:16
```
- NEVER work in the person's checkout (`run.py` refuses it). NEVER open their Unity project.
- Copy the Library from a worktree built for the SAME target (Android here) and always pass that target: a switch
  reimports everything for hours.

## 3. Write the candidate folder (done: `run.py --dry-run` prints three launches and no error)

Copy `Docs/ProvenWin/candidates/example-w4-sky-stars/` to `Docs/ProvenWin/candidates/<id>/` (without its `reports/`,
`verdict.*` and `signoff` block values) and edit it:
- `variants/A.*`: `git show origin/bleeding-edge:<path> > variants/A.<ext>`. Never type it.
- `variants/B.*`: A plus the change. Change as few lines as possible and do not move existing lines: on Vulkan,
  moving code alone changed pixels (the sky nebula).
- `variants/C.*`: B made wrong on purpose, so the comparison has something to catch (a bound 10x too tight, a skip
  that fires too often).
- `candidate.json`: fill EVERY field; `runtimeWriters` and `exposure` with file:line evidence. Put the game's state
  into `spec` (floats, colors, globalVectors, prepare). If the change skips work under a condition, set
  `"earlyOut": true` and add both probes (`variants/probe_reach.*`, `variants/probe_guard.*`).
- List every OTHER state the game draws this in under `states` (the change must not cost there).

## 4. Measure (done: `run.py` printed a verdict)

```
python Tools/Build/proven_win/run.py Docs/ProvenWin/candidates/<id> --project D:/ABLab/wt
```
It runs Vulkan first. To save time on a candidate that may fail, run `--apis vulkan` alone first.

## 5. Read the verdict (done: you acted on PASS, FAIL or HOLD exactly as below)

`verdict.py` decides; you do not. Re-run it any time: `python Tools/Build/proven_win/verdict.py <candidate>`.
- **FAIL**: record it (below), copy its numbers into WINS.md's Rejected table, commit, and go back to step 1.
  Do not tune B until it passes on the same candidate without saying so in WINS.md: that is how a win is faked.
- **HOLD**: the evidence is not good enough. Fix exactly what it names (a busy machine: re-run; a negative control
  that was not caught: make C more wrong; a probe that changed nothing: fix the bench state in `spec`). Delete the
  affected `reports/<api>/<role>.json` and re-run step 4. STOP after two HOLDs in a row and ask the person.
- **PASS**: go on.

Every candidate ends with ONE `queue.py done` call, which also teaches the scout (the lesson goes into
`Docs/ProvenWin/LENSES.md`):
```
MSYS_NO_PATHCONV=1 python Tools/Build/proven_win/queue.py done <id> --outcome won|rejected|inconclusive   --reason "<the numbers verdict.py printed>" --lesson "<one sentence: what the scout should look for or avoid next time>"
```
Do it for a FAIL now, for a PASS after it ships (step 8), and for a candidate you give up on (`inconclusive`, with what
would settle it).

## 6. The blind sign-off page (done: the person has the link)

Fill the `signoff` block of `candidate.json` (plain words a non-programmer understands), then:
```
python Tools/Build/proven_win/signoff.py Docs/ProvenWin/candidates/<id>
```
Publish with the Artifact tool exactly as it prints (copy the folder to the scratchpad first if the tool refuses the
path). Send the person the link and ask them to compare 1 and 2. A "B worse" answer stops the win: record it as
rejected. Bit-identical wins may ship before the answer; "within-one-level" wins wait for it.

## 7. Apply, gate, record (done: one commit with the change, its candidate folder and its WINS.md row)

- Apply B to the real file in your worktree (copy `variants/B.*` over it).
- Real compile and tests: `Unity.exe -batchmode -projectPath <wt> -buildTarget Android -runTests -testPlatform EditMode
  -testFilter <area> -testResults <xml>`; compare the failures with the same run on A. Only failures that are new with
  B count. `bash Tools/Build/unity_refcompile/run.sh` does not run on Windows (`fcntl`): say so.
- `python Tools/Build/approvals.py check --me <person>`. Exit 2 (COLLISION): STOP and tell the person who occupies
  the lane; continue only on their "go". For each owner it names whose work changed: `approvals.py add`.
- Add the WINS.md row (`verdict.md` is its draft), and commit the change, the candidate folder and WINS.md together.

## 8. Ship (done: merged, or the person told what blocks it)

Use `/ship`. Never use `--admin`, never approve your own PR: if branch protection blocks the merge, give the person
the exact command or button and stop. Then `queue.py done <id> --outcome won ...` (step 5) and commit QUEUE.json,
QUEUE.md and LENSES.md. If `done` says the queue is below 3, the next run starts with `/proven-win-scout`.

## What this does not cover (say so in every report)

A phone GPU (the device bench APK, PLAYBOOK §3.5, needs a phone on USB), the change's share of a real frame, and
anything not drawn by the bench. A C# change that is not "switch a component off" needs a scene bench candidate
(`kind: "scene"`, README) or a new toggle; if neither fits, STOP and say so.
