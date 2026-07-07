Temporary distribution channel: chat attachments proved non-downloadable for
large binaries, so progress builds are committed here. This folder is dropped
before any merge to a mainline branch (a squash-merge never carries the blob).
Build recipe: see PORT_PLAN.md "Progress builds" item 5.

CosmicShore-Android.apk — the no-Unity Android build (SkimRace + Freestyle,
touch + Bluetooth gamepad; debug-signed, arm64). Install: enable "install
unknown apps" on the device and open the APK, or `adb install
CosmicShore-Android.apk`. Freestyle launch + build recipe: Port/README.md
section "Android build (no Unity)".
