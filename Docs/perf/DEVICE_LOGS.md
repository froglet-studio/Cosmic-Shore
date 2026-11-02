# Device play-test logs (Android)

`DeviceLogCapture` (`Assets/_Scripts/Utility/PerformanceBenchmark/DeviceLogCapture.cs`) records phone play tests and
stores them on the **`bugledger-android/<user>`** branch (`devicelogs/<user>/<session>/`), separate from the
Editor's `bugledger/<user>`.

**Gating:** body is `#if DEVELOPMENT_BUILD && !UNITY_EDITOR` and returns unless `Application.platform == Android`.
Release builds contain an empty class. Build the APK with **Development Build** ticked.

Per session (one folder per app launch):
- `session.json` — device (model, OS, GPU/API, RAM, CPU, resolution, refresh, quality tier, app version, buildGUID);
  warnings/errors/exceptions/asserts ring buffer (last 400, with stacks and scene) and per-signature counts;
  per-scene frame-time summary (avg, max, >50 ms hitches, 8-bucket histogram); 10 s samples of fps, battery level,
  battery temperature, PowerManager thermal status (API 29+), mono/allocated memory;
  `previousSessionCrashed` (the last session never reached a clean pause/quit: crash, ANR or OS kill).
- `logcat_app.txt` — `logcat -d --pid=<own pid> -t 4000`; `logcat_crash.txt` — `logcat -d -b crash -t 300`.
  Read on pause/quit/`devlog send` only.

Written to `<persistentDataPath>/CosmicShore DeviceLogs/pending/<session>/` every 60 s, on each scene change
(match end), on pause and on quit. Frame tracking allocates nothing per frame.

## Getting the logs onto the branch
1. **Direct upload (no cable).** In the dev build's DiagnosticsHUD console: `devlog token <PAT>` once
   (fine-grained GitHub token, repo `froglet-studio/Cosmic-Shore`, *Contents: read & write*). It is stored in
   PlayerPrefs on that phone only, never in the build. Uploads go through the GitHub contents API on pause and on
   next launch (unsent sessions). `devlog send` forces it, `devlog` shows status, `devlog clear` drops the token.
2. **adb (no token on the phone).** Plug in with USB debugging and run
   `python3 Tools/Perf/android_logs_report.py ingest` (commits in a throwaway index, pushes to the branch).

## Analysing
`python3 Tools/Perf/android_logs_report.py report` — per-session device, crash markers, thermal/throttling,
top exception signatures with counts, perf per scene.
