# Launch flow: what belongs to each platform

Checklist keys are the console store's `platform` values; `mobile` (activation) counts for both iOS and Android.
Components are `launch-status` ids (`Docs/LAUNCH/STATUS_CONTRACT.md`). PRs as of 2026-11-06 (all open then); re-read
their state, never trust this list for it. Owners are who decides, from `Docs/TEAM.json`, `CLAUDE.md` or the cited
doc; "not named" means no doc names one.

| Platform | Skill | Checklist keys | Components | READINESS | Launch PRs | Read first | Owners |
|---|---|---|---|---|---|---|---|
| **Android** | `/launch-flow-android` | `android`, `mobile` | `android-prerelease`, `android-production`, `build-android` | §4; §10 #0, #16-18, #26 | #1751 (builds, Play upload, promote, halt), #1749 (build stamp), #1755, #1756 | `Docs/ANDROID_BUILD.md`, `Docs/Reports/ANDROID_BUILD.md`, `/androidbuild`, `Docs/ACTIVATION.md` | builds/UGS: Shombith; Android build work: Yash (#1514/#1515); store forms: not named |
| **iOS** | `/launch-flow-ios` | `ios`, `mobile` | `ios-prerelease`, `ios-production`, `build-ios` | §3; §10 #0, #14-16 | #1751 (UBA iOS, the hand-off decision), #1749, #1755, #1756 | `Docs/IOS_BUILD.md`, `.github/workflows/ios-testflight.yml`, `Docs/PLATFORM_UNIFICATION.md`, `Docs/ACTIVATION.md` | Shombith (builds, secrets); store forms: not named |
| **Windows / Steam** | `/launch-flow-windows` | `windows` | `windows-prerelease`, `windows-production`, `build-windows` | §1, §2; §10 #3, #8, #25 | #1751 (UBA Windows, SteamPipe opt-in), #1749 | `Docs/STEAM_RELEASE_TASKS.md`, `Docs/STEAM_BUSINESS_SETUP.md`, `Docs/STEAM_PLAYTEST_RUNBOOK.md`, `Tools/Steam/README.md`, `Docs/Reports/STEAM_LAUNCH.md` | Steamworks: Garrett (A1a, A1c, A3, A6) and Shombith (A1b, A2, A7); Playtest app A4: Shombith creates, Garrett names; store page A5: Caleb |
| **Web Toybox** | `/launch-flow-web` | `web` | `web-staging`, `web-production`, `build-web` | §5; §10 #13 | #1753 | `Docs/WEBGL_TOYBOX.md`, `Tools/Build/publish_toybox.py` | not declared; DNS: whoever holds the froglet.games zone |
| **Live ops** | `/launch-flow-liveops` | `liveops` | `ugs-development`, `ugs-production`, `launch-flags` | §6; §10 #1, #7, #11 | #1748 (deploy), #1756 (flags), #1749 (environment per build) | `Docs/LAUNCH/LIVE_UPDATES.md`, `Docs/LAUNCH/LIVE_FLAGS.md`, `Tools/UGS/README.md` | Shombith (UGS); Cloud Code work: Yash, through Shombith |
| **Backend** | `/launch-flow-backend` | `backend` | – | §7 (Backend); §10 #6, #12, #15, #21 | #1755 (protocol), #1756 (minimum version) | `Docs/MULTIPLAYER_START_HERE.md`, `Docs/claude/MULTIPLAYER_AND_SOCIAL.md`, `Docs/Reports/MULTIPLAYER.md` | UGS/backend: Shombith; multiplayer code: ask Shombith and Yash |
| **Launch-wide** | `/launch-flow-launch-wide` | `launch` | `trunk-static`, `unity-compile`, `smoke-*`, `release` | §7-§9; §10 #2, #4, #5, #9, #10, #19, #20, #22-24 | #1750 (status), #1747 (release notes), #1754 (smoke), #1757 (editor console), #1758 (docs, console), #1695, #1693 | `Docs/LAUNCH/READINESS.md` §9, `Docs/Legal/README.md`, `Docs/THIRD_PARTY_REGISTER.md`, `Docs/BUILD_AND_DELIVERY.md` §10 | tooling/CI: Shombith; legal: counsel; licences: not named |

The console's artifact URL: `https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2`.
