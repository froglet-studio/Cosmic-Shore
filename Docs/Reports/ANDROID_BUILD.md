# Android build — where it stands

**From:** Yash's session, 2026-10-25. **Branch:** everything below is on `bleeding-edge` (PRs #1087, #1296, #1514).
**Status:** the release build has run once and that build is on Google Play internal testing. It is 64-bit only.
32-bit is back on bleeding-edge since 2026-10-25, but **no build with it has been uploaded yet**. That is the next
step (section 5, team task T-20261025-b37f).

## 1. In one minute

- **The newest Play build is 0.2.0, version code 412826**, built 2026-10-14 16:26 UTC from the release menu and
  uploaded to **internal testing** for `com.FrogletGames.CosmicShore`. Git cannot see Play Console, so its Internal
  testing page is the authority if someone uploaded since.
- **Two testers could not install it.** Play showed "Item not found" on a Moto G Power (Caleb) and a Samsung
  Galaxy M (Yash), while a third tester installed it. Those phones run 32-bit Android on 64-bit chips, and the
  2026-10-08 ARM64-only setting hid the app from them.
- **32-bit (ARMv7) ships again** beside ARM64 (owner's call 2026-10-15, merged to bleeding-edge 2026-10-25 in #1514).
  Play gives each phone one ABI, so the download does not grow.
- **Development builds run on phones.** Garrett passed the phone round on an Android development build on
  2026-10-20 (tap-to-press cards, the Wrasse drawing, the double-tap seat swap).
- **The Editor checks of the release build have never been run**: the refusal dialogs, batchmode exit codes and
  `AndroidBundleCheckTests` in the Test Runner. Nothing builds Unity in CI, and uploads are by hand.

## 2. Try it

**Release build for Play** (`Docs/ANDROID_BUILD.md` §1-3):
1. Pull `bleeding-edge`. Unity Hub: Android Build Support with OpenJDK and SDK & NDK. File > Build Profiles >
   Android > Switch Platform.
2. Player Settings > Android > Publishing Settings: Custom Keystore, select **the same upload key the first upload
   used**, type both passwords.
3. FrogletTools > Build > **Android App Bundle (Release)**. Expect "Ready to upload, with 1 warning(s)"
   (`liblofelt_sdk.so` is not 16 KB-aligned). `Builds/Android/CosmicShore-0.2.0-<code>.aab.check.txt` should list
   **ABIs: arm64-v8a, armeabi-v7a** and **Native debug symbols: in the bundle**.
4. Play Console > Test and release > Testing > Internal testing > Create new release > upload > Save > Start rollout.

**Development APK for a phone test** (`Docs/ANDROID_BUILD.md` §8): FrogletTools > Build > **Android APK
(Development)**, then `adb install -r`. A phone with the Play install must uninstall it first (different key). Hold
four fingers for 2 s to hide the diagnostics so it plays like release.

## 3. What changed (all on bleeding-edge)

| Date | What | Commit / PR | Files |
|---|---|---|---|
| 2026-10-13 | Signed release App Bundle build that refuses Play-rejected settings and restores Player Settings; version code = minutes since 2026-01-01 | `dc0613aa2`, #1087 | `CosmicShoreBuildPipeline.Android.cs` |
| 2026-10-13 | Pre-upload check of the `.aab` (signing, package, target SDK, debuggable, ABIs, size, 16 KB alignment, category, Billing) | `dc0613aa2`, #1087 | `AndroidBundleCheck.cs`, `AndroidBundleCheckTests.cs` |
| 2026-10-13 | CI fails on a committed keystore path | `dc0613aa2`, #1087 | `Tools/Build/check_android_signing_settings.py`, `unity-ci.yml` |
| 2026-10-14 | Native debug symbols in the bundle (Play had warned on 412826) | `9db26d63c` | `CosmicShoreBuildPipeline.Android.cs` |
| 2026-10-15 | Release guards judge a build by its own options, not the Development checkbox | `6f2ce1be2` | `ReleaseBuildKind.cs` and the three guards |
| 2026-10-18 | A development build that plays like release (one switch for HUD, recorder, red console, logs) | `36e47e09c` | `DevDiagnostics.cs`, `DevDiagnosticsToggle.cs` |
| 2026-10-18 | Android APK (Development) menu item and batchmode entry | `7a5b7e931` | `CosmicShoreBuildPipeline.Android.cs` |
| 2026-10-19 | The Wrasse's kite draws in a player build (stripped `Shader.Find` shader) | `1543a6e93` | `KiteNavigatorView.cs`, `Resources/KiteNavigatorUnlit.mat` |
| 2026-10-22 | Very Low on a phone: one view, no picture-in-picture; the Wrasse seat turns the phone to portrait | `22fbc2e44` | crew views, `ProjectSettings.asset` |
| 2026-10-25 | 32-bit (ARMv7) back; the check warns without it and sizes the base per phone; docs brought up to date | `cc918a577`, #1514 | `ProjectSettings.asset`, `AndroidResolverDependencies.xml`, `AndroidBundleCheck.cs` |

## 4. Not done, and known risks

- **Editor checks never run** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, Android release build, steps 1-2 and 4-7).
- **16 KB pages:** `liblofelt_sdk.so` (Nice Vibrations haptics) is 4 KB-aligned. Play blocks **updates** without 16 KB
  support from 1 Feb 2027. Replace or rebuild it before then.
- **Other `Shader.Find` callers** may draw nothing in a player build, as the kite did (ToyFactory, BlackHole,
  SniperBeam, the Shrike and others; on the team board).
- **No version check across devices:** every phone and PC in a cross-platform match must run the same commit.
- **No CI build or automatic Play upload** (`Docs/ANDROID_BUILD.md` §6 lists what it would take).
- `build/android` was last moved in April and is not what testers run.

## 5. Next steps

1. **T-20261025-b37f:** build from bleeding-edge, upload, and install on the Moto G Power, the Galaxy M, the 4 GB
   Samsung and a flagship. Play Console > Device catalog should read Supported for both 32-bit models.
2. Run the release build's Editor checks and record them in the checklist entry.
3. Phone tests already on the board: the diagnostics switch (T-20261018-20ac) and Very Low single view / portrait
   Wrasse (T-20261022-b932). Garrett owes a decision on the portrait splash (T-20261022-b797).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-25 from git (last 30 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area android-build --days 30`._

### Recent changes (20)
- 2026-10-19 `77b18f39a` fix(diagnostics): keep Amoebius building - developer console calls need a player platform — on bleeding-edge, bleeding-edge
- 2026-10-18 `7a5b7e931` feat(build): Android development APK entry point (menu + batchmode) — on bleeding-edge, bleeding-edge
- 2026-10-18 `36e47e09c` feat(diagnostics): one switch makes a development build play like release — on bleeding-edge, bleeding-edge
- 2026-10-15 `cc918a577` feat(android): ship 32-bit (ARMv7) beside ARM64 so 32-bit-Android phones can install — on bleeding-edge, bleeding-edge
- 2026-10-15 `2be8cf634` docs(android): "Item not found" for a listed tester is an account mismatch — on bleeding-edge, bleeding-edge
- 2026-10-14 `9db26d63c` feat(android): embed native debug symbols in the App Bundle; the check warns without them — on bleeding-edge, bleeding-edge
- 2026-10-14 `1cdad6e9a` docs(android): record the native debug symbols follow-up in ANDROID_BUILD.md §6 — on bleeding-edge, bleeding-edge
- 2026-10-13 `dc0613aa2` feat(android): scripted release App Bundle build with a pre-upload Play check — on bleeding-edge, bleeding-edge
- 2026-10-09 `0b434ceb8` merge: claude/peaceful-rubin-hhw49n into vessel-studio (Stoat round 15, Warpline, /vessel-studio, Ys-bleeding-edge) — on bleeding-edge, bleeding-edge
- 2026-10-09 `c0f96177a` feat(arcade): Warpline (GameModes 65) - the Stoat's time race — on bleeding-edge, bleeding-edge
- 2026-10-09 `55ce21a1f` feat(studios): Sync panel hands git work to a Claude session as jobs — on bleeding-edge, bleeding-edge
- 2026-10-08 `249c7f5df` feat(platform): store readiness - production iOS id, Android API 36 + ARM64, Unity IAP removed — on bleeding-edge, bleeding-edge
- 2026-10-08 `2d5676125` chore(android): drop the stale Unity Ads dependency from the Gradle templates — on bleeding-edge, bleeding-edge
- 2026-10-08 `7171aa126` fix(android): restore the unityLibrary namespace outside EDM4U's managed block — on bleeding-edge, bleeding-edge
- 2026-10-07 `931dcd51f` refactor(party): one UGS failure classifier and request counters (review Phase 0) — on bleeding-edge, bleeding-edge
- 2026-10-06 `8c7f890b8` feat(diag): flag jobs that ran without Burst; record code optimization — on bleeding-edge, bleeding-edge
- 2026-10-05 `7494fe5bd` Merge commit '03ba8b2c8' into overnight/balance — on bleeding-edge, bleeding-edge
- 2026-10-05 `57d17a977` Merge branch 'cece/swarm-fauna-game' into overnight/flora — on bleeding-edge, bleeding-edge
- 2026-10-02 `aa2b21fa2` ci(ios): store the Xcode project path as absolute — on bleeding-edge, bleeding-edge
- 2026-10-01 `d8f10ebf6` Merge pull request #936 from froglet-studio/Bug_Hunt — on bleeding-edge, bleeding-edge

### Also in flight on other branches
- `claude/eloquent-meitner-9e4u2a` (unattributed, 2026-10-04): chore(android): drop the stale Unity Ads dependency from the Gradle templates

### Open tickets (Froglet Tasks)
- T-20261025-b37f [open] test: After PR #1514 merges: build FrogletTools > Build > Android App Bundle (Release) from bleeding-edge, upload it to Play internal testing, and install it from the tester link on a Moto G Power and a Samsung Galaxy M (32-bit Android; Caleb and Yash have these), plus the 4 GB Samsung and one flagship (who: anyone with the Play upload key and a Moto G Power or Samsung Galaxy M)
<!-- team_report:end -->
