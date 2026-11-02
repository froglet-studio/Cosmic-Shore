# Device play-test logs (Android)

Play the APK anywhere and swipe it away; every session is kept on the phone. Plug the phone into the PC and one command moves them all
to Git, so anyone (Claude or Grok Bot on a phone, any AI on a PC) can read them and fix what they show. Asked for by Yash, 2026-11-02:
"without me needing to run any command". It is the phone's half of the editor's `BugHuntAutoSend` (`Docs/DIAGNOSTICS.md`).

**For any AI asked to "check my phone reports":** follow `.claude/skills/phone-hunt/SKILL.md`: `ingest` if the phone is plugged in,
then `report --new`. An AI in the cloud cannot read files on the phone; the data branch is how it sees them.

**Development Android builds only.** `DeviceLogCapture` is behind `DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR`, and
`DeviceLogGradleHook` adds the Java (`DeviceLogAndroid~/`) and its manifest entries to a development build's Gradle project and
to nothing else, so a release APK carries none of it.

## Getting the logs off the phone: over USB (the way Yash uses it)

The phone keeps every session in `pending/` and sends nothing. Plug it into the PC with USB debugging on, and one command moves it all:

```
python3 Tools/Perf/android_logs_report.py ingest
```

It pulls each finished session and the HUD's captures, adds Android's own record of how each one ended (`dumpsys activity exit-info`),
gzips the game log, commits to `bugledger-android/<user>` and pushes. Only after origin has that commit does it delete those files from
the phone (`--keep` to leave them). The session being played right now is skipped. Running it twice commits nothing the second time.
Lost this way: the app's logcat, since Android's log buffer is long overwritten by then; the game's own log covers its lines.
Or just ask an AI on the PC to "check my phone reports", which runs this first. A collaborator's phone passes `--user <name>`.

### Optional: the phone uploads by itself

Not in use (Yash chose USB; there is no button for it). The phone can also commit each session itself after the swipe, with a GitHub
fine-grained personal access token for `froglet-studio/Cosmic-Shore` with **Contents: Read and write** only. Tokens cannot be limited
to one branch, so keep it on your own phone and revoke it if the phone is lost. Copy it on the phone, then run `devlog paste` in the HUD
console: it reads the clipboard, clears it and stores the token in the app's private no-backup folder, and a notification says whether
GitHub accepted it. `devlog user <name>` sets the folder name (default `yskha`).

**Never type the token as a command**: the console logs every line it runs, and logs get uploaded. `devlog token` refuses, and both
the console and the uploader blank anything token-shaped as a second line of defence.

## While playing

Nothing to do. **Mark** (right of the screen; hidden with the rest of the diagnostics by **Off**) records "that felt bad, right now".
The HUD console has `devlog` (state), `devlog mark <note>`, `devlog send` (finished sessions, now) and `devlog clear` (remove the token).

| When | What |
|---|---|
| Every 5 s | One line in `samples.jsonl`, and the game log flushed, so a crash or kill loses at most 5 s |
| Every 60 s, each scene change, each pause | `session.json` and `summary.txt` |
| 90 s into each scene | One `capture` (like the editor's `PerfAutoCapture`), if the HUD is on. Those samples carry `capturing:1`, and the report leaves them out of the fps |
| Each pause, and each launch | Schedules `DeviceLogJob` through Android's JobScheduler, which outlives the game. A force stop cancels it |
| After the swipe | The job (own process, `:reporter`) sees the game's process is gone, makes **one commit**, moves the session to `sent/` and posts a notification; tapping it opens the folder on GitHub |
| No token, no network, or the job did not run | The session stays in `pending/`, goes with the next launch if there is a token, or with `android_logs_report.py ingest` over USB |

## What lands on `bugledger-android/<user>`

Data only; never merge this branch. `devicelogs/<user>/<session>/`:

- `summary.txt`: the notification's sentence.
- `samples.jsonl`: every 5 s: scene, fps, frame percentiles, CPU and GPU ms, memory (RSS and Unity's), thermal status and headroom, battery temperature; and one line per Mark.
- `session.json`: device, per-scene frame histogram and lowest 5 s fps, error signatures, the last 400 warnings and errors with stacks, whether the previous session ended uncleanly.
- `unity_log.txt.gz`: every line the game logged (first 16 MB). `logcat_app.txt.gz`: this process's logcat (newest 8 MB). `logcat_crash.txt`: the crash buffer.
- `exit.txt`, `exit_trace.txt`: how Android says the process ended, with any crash or ANR trace.
- `load_*.txt`: the newest Load Time Insights.

Plus `BugLedger/shared/reports/<user>-phone/perf/capture_*.json`, the editor's capture format, so the daily sweep (`perf_hunt.py collect`)
and `perf_capture_report.py` judge it as they are, with their own baselines. And `devicelogs/<user>/handled.json`, which session a fix closed.

## What it cannot see

An app reads only itself, so no other app's lines can reach the repo. System-wide reports, per-thread CPU, the GPU's clock and Perfetto
traces need USB (`adb`). GPU ms is what the driver reports through `FrameTimingManager`, and it is a dev build, a little slower than release.

## Verification status (2026-11-02)

Compiled in a real IL2CPP development build and installed on an SM-M366B: it records, and the Mark button shows (that build still had
the Paste token button, since removed; the removal is not yet in a built APK). USB `ingest` ran against that phone: first into a
throwaway repo (first commit on a new branch, an unchanged re-run, `report`), then for real: commit `685d7abc0` on
`bugledger-android/yskha` with one session and one capture, after which both were gone from the phone. Owed: the sweep reading a phone
capture. The token upload (the job, the swipe, the notification) is unused and has never run.
A release APK's lack of the service is by the hook's code; no release has been built and inspected since it landed.
