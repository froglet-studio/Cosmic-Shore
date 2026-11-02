# bugledger-android

Phone play-test logs from Cosmic Shore development builds (DeviceLogCapture). One folder per app launch:
`devicelogs/<user>/<session>/{session.json,logcat_app.txt,logcat_crash.txt}`.

Summarise: `python3 Tools/Perf/android_logs_report.py report` (on Ys-bleeding-edge). See `Docs/perf/DEVICE_LOGS.md`.
Data only; never merge this branch.
