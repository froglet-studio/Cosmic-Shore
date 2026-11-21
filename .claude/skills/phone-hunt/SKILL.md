---
name: phone-hunt
description: Read the play-test reports Yash's phone sent by itself (the bugledger-android/<user> branches, written by DeviceLogCapture after the game is swiped away), rank what they show, fix it on a branch, retest what can be retested without a phone, and record which sessions a fix closed. Works from a phone's Claude or Grok Bot, a cloud session or a PC, because the reports are on Git. Use on "/phone-hunt", "check my phone reports", "check my phone logs", "what did my phone find", "fix what my playtest found", "find those datas", or any question about a play test on an Android phone.
---

# Phone hunt: the phone's reports, read and fixed

The person played the game on a phone, possibly away from any PC, and swiped it away. The reports reach Git one of two ways: the phone
sends them itself (a token is set), or they wait on the phone until it is plugged into the PC. **Never ask them to run a command or paste
a log.** Everything about the data is in `Docs/perf/DEVICE_LOGS.md`.

## 1. Read

On the PC with the phone plugged in (USB debugging on), first move what the phone holds onto the data branch. This pulls, commits,
pushes, checks origin has the commit, and only then deletes it from the phone; the session being played now is left alone:

```
python3 Tools/Perf/android_logs_report.py ingest
```

Then, anywhere:

```
git fetch origin
python3 Tools/Perf/android_logs_report.py report --new      # sessions no fix has closed yet
```

Nothing new: say so in one line, with the newest session's time from `report`. If `bugledger-android/<user>` has no session at all and
no phone is plugged in, the logs are still on the phone: say that, not "no bugs". The raw files are `git show origin/bugledger-android/<user>:devicelogs/<user>/<session>/<file>`;
for a `.gz`, pipe it through `gunzip`. A `capture_*.json` under `BugLedger/shared/reports/<user>-phone/perf/` reads with `perf_capture_report.py`.

Read the **Marks** first: the person pressed that button because something felt wrong at that moment. Then:

| Order | Look at | It means |
|---|---|---|
| 1 | `ended:` not user-requested, `FATAL`, `Fatal signal`, `ANR`, `exit_trace.txt` | A crash or kill. Fix first |
| 2 | Exceptions and errors by count and by how many sessions they hit | Real bugs. Route them the way `/bug-ledger` does: root cause, not silencing |
| 3 | Memory `rssMB` climbing across a session | A leak or unbounded growth: 3.1 GB was seen in one 25-minute session |
| 4 | Fps falling from the first third to the last while thermal status rises | Throttling: the frame's cost has to fall, not just average 60. See `Docs/PERFORMANCE_OPTIMIZATION.md` |
| 5 | Worst scene by fps; GPU-bound or CPU-bound from the report's line | Which cost to attack; then the scene's `capture` for the system |

Say what you found before fixing it, in plain words, with the numbers and which sessions. Compare builds with `buildGuid`: a number from an
older build says nothing about the newest code.

## 2. Fix

Load `/perf-hunt` for a performance item and `/bug-ledger` for an error; their checklists apply, and so does `CLAUDE.md`'s team rule
(`who_works_where.py query <files>` first; never re-tune another person's work unasked). One item per commit, on a branch off
`Ys-bleeding-edge`, where Yash's performance work lands first; it reaches `bleeding-edge` by PR. Never push to `bleeding-edge` directly.
The logs stay on `bugledger-android/<user>` and are never merged: only fixes go into `Ys-bleeding-edge`, so megabytes of logs per session
never enter every clone of the game.

## 3. Retest what can be retested

No Unity runs in the cloud, and a new APK needs a PC. So say plainly what was checked and what waits:

| Check | Proves | Cannot prove |
|---|---|---|
| `bash Tools/Build/unity_refcompile/run.sh --config player-dev` and `bash .claude/skills/ship/gates.sh` | It compiles, and the allocation gates hold | Behaviour |
| `python3 Tools/Build/perf_hunt.py amoebius --no-build`, or a headless Amoebius run of the mode (`/amoebius`) | Logic, allocations, relative script cost | Phone GPU, heat, real fps |
| The next APK on the phone (`build and install` at the PC), then the next report | The fix, on the real device | |

The verify rule still holds: never claim a test that did not run. A fix is proven only by a later session from a build that contains it.

## 4. Close out

```
python3 Tools/Perf/android_logs_report.py handled <session>... --fix <sha> --note "what it fixed"
```

Write the delta into `Docs/PERFORMANCE_OPTIMIZATION.md` §2 for a performance fix, once a later session proves it. Leave the person one
short line: what was found, what was fixed and on which branch, and what the next phone test should show.
