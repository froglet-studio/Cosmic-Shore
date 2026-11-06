# Launch readiness: Cosmic Shore, every platform

**Measured 2026-11-05** against `origin/bleeding-edge` @ `1ac6f87ae` by a read-only audit (Phase 1 of the launch
plan; the Launch Console and the pipelines come after scope is confirmed). Seven parallel read-only audits plus spot
checks; every headline row marked **✔ re-checked** was read again by hand before it went in this file.

- **Working milestone: "Invite build v1", 2026-12-03** (4 weeks from the audit). Not yet confirmed: see §10.
- **Platforms:** Windows / Steam, iOS, Android, Web Toybox. **macOS: recommended out** (§2). **Amoebius: out of the
  launch** (Shombith, 2026-11-05: "No launch from amoebius"); it ships nothing and no launch gate depends on it.
- **What this file is not:** a fix. Nothing here was changed. Every row names its evidence so it can be re-measured;
  re-measure before acting, because the team pushes many times a day.
- **What could not be seen from a cloud session:** repository secrets, variables, Environments' protection rules,
  runners and Actions permissions (the API returned 403), every store dashboard (Steamworks, App Store Connect, Play
  Console), the Unity Cloud / UGS dashboard, the Cloudflare account. Rows that depend on those say **unverified**.

Owners below are the areas' declared owners (`Docs/TEAM.json`, `who_works_where.py`) or the person a doc names.
"unknown" means no doc names one; nobody is assigned anything here (`CLAUDE.md` § Team).

Legend: **Size** S ≤ 1 day, M ≤ 1 week, L > 1 week. **Blocks** = blocks the invite build (Y), a public store launch
only (Store), or nothing (N).

---

## 0. The answer in five seconds

| Platform | Status | Why, in one line |
|---|---|---|
| Windows / Steam | 🔴 | No Steamworks account or app id; no Windows IL2CPP player has ever been built (R1). |
| iOS | 🔴 | No reproducible build (a hand export is signed); no privacy manifest; deletion is a web form; #1068 leftovers unverified. |
| Android | 🟠 | On Play internal testing, but no CI, an undeclared microphone permission, a 4 KB-aligned library, undocumented key custody. |
| Web Toybox | 🟠 | Live, but from the rate-limited `r2.dev` host, a hand upload of unknown commit, no staging or rollback tooling. |
| Live ops | 🔴 | Every build talks to the UGS **development** environment; no remote config, kill switch or minimum-version gate. |
| Backend | 🟠 | Anonymous sign-in only, protocol version not used in matchmaking, production deploy has no approval gate. |
| CI / quality | 🔴 | No Unity compile since 2026-10-06; 48 EditMode failures last measured; 181 open QA items. |
| Security | 🔴 | Trunk review is bypassed routinely; production UGS secrets are repo-level; a leaked token has no rotation record. |

---

## 1. Windows / Steam

| Item | Current state + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| Build profile | Only `Assets/Settings/Build Profiles/CS Linux build profile.asset`. Windows is configured in code: `Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.cs:47-55,98` (`BUILD_AND_DELIVERY.md` §2). | Nothing (a profile asset is optional). | Shombith | S | N |
| **IL2CPP player reaches the menu (R1)** | IL2CPP set (`ProjectSettings.asset:945`, pipeline `:144-152`). **No Windows player has ever been built** (`QA_BACKLOG.md:167`, `Reports/STEAM_LAUNCH.md` §2). The pipeline does not restore `bundleVersion` after a build (`:83`) and leaves `WINDOWS_BUILD` in the defines. | `Tools/Build/build_windows.sh` → launch → sign in → Menu_Main → pause → read `Player.log` (`STEAM_RELEASE_TASKS.md` R1). | Shombith | M–L | **Y** |
| Steamworks SDK | Steamworks.NET 2025.164.1 (`Packages/manifest.json:5`), sign-in only (`Assets/_Scripts/System/Steam/SteamPlatform.cs`). **Off ✔ re-checked**: `Assets/Resources/SteamConfig.asset:15-16` `appId: 0`, `signInWithSteam: 0`. `Reports/STEAM_LAUNCH.md:67` ("no Steamworks SDK") is stale. | A real app id; the UGS Steam provider; the editor checks in `UNITY_VERIFICATION_CHECKLIST.md` § "Fair play: Steam sign-in on PC" (`claude/vigilant-archimedes-euoz8z`). | Shombith | S | **Y** |
| App id & depots | Placeholders only. `Tools/Steam/upload.sh` refuses without `STEAM_APPID`/`STEAM_DEPOTID` (base) or `STEAM_PLAYTEST_APPID`/`STEAM_PLAYTEST_DEPOTID`. | Steamworks A1/A2 (base app), A4 (Playtest child app). §9. | Shombith creates, Garrett signs | S (human) | **Y** |
| SteamPipe upload | `Tools/Steam/upload.sh` + `Tools/Steam/templates/{app_build,depot_build}.vdf` (one Windows depot, no `.pdb`). Proven only against a stub `steamcmd` (R6). No CI by design (`BUILD_AND_DELIVERY.md` §10). | A first real upload; a builder account with a cached Steam Guard session on the build machine. | Shombith | S | **Y** |
| Beta / playtest branches | Model in `STEAM_PLAYTEST_RUNBOOK.md` § Branch model: `internal` (password) → the child app's `default`. `default` can only be set live from App Admin. | Branch passwords; the signed wave policy (H4/A6). | Shombith / Garrett | S | **Y** |
| Store page | Not found. Five capsule sizes specified (`STEAM_BUSINESS_SETUP.md` Phase 3); art (H13) and copy (H16) open; system requirements wait on D1/H8. | Art, copy, screenshots, trailer, Valve review (H3/H5). Fee → art → page → review → Coming Soon puts a public page about **39 days** after the fee is paid. | Caleb (+ art contributor) | L | Store |
| Achievements | Not found (no `SteamUserStats`). | Optional; defer. | – | M | N |
| Cloud saves / persistence | No `SteamRemoteStorage`. **Nothing persists ✔ re-checked**: `ProgressionBackendGate.cs:19` `CloudEnabled = false`. Windows `PlayerPrefs` live in the registry, which Steam Auto-Cloud cannot sync. | Decide UGS Cloud Save on/off (DoD #4). | Shombith | M | **Y** |
| Steam Deck | No evidence. No Linux depot although a Linux profile exists. | A Proton pass. | – | M | N |
| Controller-first UX | Pad input and glyphs exist (`ControlGlyphSetSO.cs`, `InputDeviceIconSetSwitcher`). Known defects: dead Y freestyle toggle, Hangar drops focus, "No Cancel/B/Escape handling exists in the menu UI" (`MENU_REDESIGN_B.md:301,316-317`); arcade-grid fix unverified (`:327`). | Pad-only pass (`BUILD_AND_DELIVERY.md` §6, all ⬜). | Shombith (UI) | M | Store (Deck) |
| Build identity | Only `Application.version` on screen (`GameSettingsPanelController.cs:306`) = `0.2.0` everywhere ✔; Standalone `buildNumber: 0` (`:179`). The SHA goes only into `build_manifest.txt`, which the depot excludes. | Version + build + SHA on screen, in crash metadata, in analytics (`BRANCHING_AND_RELEASE.md` §6 R1). | Shombith | S | **Y** |
| CI build | `unity-ci.yml:426` IL2CPP tier skipped since 2026-10-13 (`UNITY_RUNNER_LABEL` unset). `build-branch-ci.yml` last ran 2026-08-12. Whether UGS Build Automation has a Windows target is unverifiable (`BUILD_PIPELINE_SETUP.md` §2 unticked). | §7 and the pipeline plan. | Shombith | M | **Y** |
| Rollback | Git side: `testbuild/` and `internal/` tags (`BUILD_AND_DELIVERY.md` §11). Steam side: re-set an earlier build live in App Admin (runbook:134), not written down as a procedure. No `release/*` branch (`BRANCHING_AND_RELEASE.md` §6 R5). | A written Steam rollback. | Shombith | S | N |
| Open R-items | R2 regenerate QA backlog; R3 daily all-modes pass (QA stopped 2026-10-12); R7 load-time target (76 combinations, DoD #5); R13 a player **cannot send a friend request**; R16 editor half (H10, 4-player two-machine pass). R12, R15 non-blocking; R10/R11 shipped, editor check pending. H1-H19 all unmarked (`STEAM_RELEASE_TASKS.md:82-100`). | See each item. | various | – | R2, R3, R7, R13, R16 = **Y** |

## 2. macOS — recommended **out**

No build target in the pipeline (Windows64 only), no codesign or notarization, no depot, and "nobody on the team has a
Mac" (`PLATFORM_UNIFICATION.md:495`). What exists: `macRetinaSupport: 1`, `macOSTargetOSVersion: 12.0`, borderless
fullscreen (`GraphicsSettingsApplier.cs:151-158`), Steamworks compiles for OSX. Adding a mac depot later costs a Mac
host, a Developer ID certificate and a notarization step (L). **Decision for you (§10).**

## 3. iOS / App Store

| Item | Current state + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| **Unity iOS build in CI** | **None.** `ios-testflight.yml` runs no Unity: a person exports Xcode on Windows from any branch (`IOS_BUILD.md:173`) and the zip arrives through the Dropbox URL in a secret. `unity_refcompile` has no iOS config, so `#if UNITY_IOS` code (incl. `GameCenterPlatform.cs`, 2026-11-01) has not compiled since 2026-10-13. | A reproducible build from a commit (UBA iOS target or a macOS runner). | Shombith | L | **Y** |
| Signing + TestFlight upload | Runs 4–7 green 2026-10-12/13; last [38373220571](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/38373220571). **The hardened workflow (#1068) has never run.** | One green run from `bleeding-edge`. | Shombith | S | **Y** |
| `testflight` Environment | Named at `ios-testflight.yml:50`. Branch rule and reviewers **unverified** (403). | Confirm in Settings → Environments. | Shombith (admin) | S | **Y** |
| Bundle id | `com.FrogletGames.CosmicShore` (`ProjectSettings.asset:177`); the job refuses any other. | – | – | – | N |
| Export compliance | `IosExportCompliancePostprocess.cs:39` writes `ITSAppUsesNonExemptEncryption=NO`. Never verified on a build (`UNITY_VERIFICATION_CHECKLIST.md` § "Store readiness: production iOS id, …", step 6). | Sign-off. | owner | S | N |
| **Privacy manifest** | **No `PrivacyInfo.xcprivacy` anywhere.** Required-reason APIs in use: `PlayerPrefs`/UserDefaults (36 files), file timestamps (2), system boot time (16). Native: LofeltHaptics, FMOD, NativeShare, `GameCenterSignIn.mm`. | App manifest with reasons; each SDK's manifest checked. | Shombith | S–M | **Y** (ITMS-91053) |
| Privacy nutrition label | `Docs/Legal/STORE_PRIVACY_LABELS.md` is a fair-play-only draft; omits PostHog/UGS Analytics, crash data, public display name, friends. | Full label + counsel + App Store Connect entry. | owner + counsel | M | **Y** |
| Age rating | Not found (D5 open, `Analytics/ANALYTICS_HANDOFF.md:399`). | Questionnaire (chat/social), COPPA decision (§8). | owner + counsel | M | **Y** |
| App Store metadata | Not in the repo (`PLATFORM_UNIFICATION.md` §3.9 points to a shared doc outside it). | Description, keywords, support URL, review notes. | owner | M | Store |
| Screenshots | Not found. `targetDevice: 2` (iPhone + iPad) means iPad sets too. | 6.9″ iPhone + 13″ iPad. | owner | M | Store |
| TestFlight external | Not found. `README.md:11` links `join/9ReKxeGf`, bound to the **old** bundle id (`IOS_BUILD.md:33`). | External group, Beta App Review, new link. | owner | S | **Y** (invites) |
| Sign in with Apple | Not needed today: anonymous sign-in only, Game Center off (`MobileSignInConfig.asset`). The Apple/Google/Facebook methods in `AuthenticationServiceFacade.cs:403-411` are **stubs that return success**. | Needed (4.8) only if a third-party login comes to iOS. | Shombith | – | N |
| **In-app account deletion** | Web link only (`SettingsView.prefab:134` → `/delete-my-data`, handled by hand). The `AccountSettingsView` delete button calls `NoPlatformSignIn.DeleteAccountAsync`, a no-op. UGS `DeleteAccountAsync` exists only in the editor tool `PlayerDataWipeWindow.cs:286`. | Delete in-app: UGS `DeleteAccountAsync` + Cloud Save + PostHog person. | Shombith | M | **Y** (5.1.1(v)) |
| External payment fenced | Yes: `IAPManager.cs:90-94` `#if UNITY_STANDALONE`, test `CheckoutIsFencedToDesktopBuilds`, `com.unity.purchasing` removed. | – | – | – | N |
| App icon | Every iOS slot empty (`ProjectSettings.asset:418-514`); the default RGBA `CSThumbApp.png` is used and only CI flattens its alpha onto black (`ios-testflight.yml:181-203`). | An opaque 1024 source in Unity. | unknown | S | N |
| Build identity | `0.2.0`, CI sets `14.<run>` (`ios-testflight.yml:284-295`); no SHA. | SHA stamp. | Shombith | S | **Y** |
| Rollback | Not found. No version gate on matchmaking (`IOS_BUILD.md:290-293`), so a phased release mixes builds in one match. | Phased-release plan, hotfix path, version filter (§5). | Shombith | M | **Y** |
| **#1068 leftovers** | The log scrub landed (`ios-testflight.yml:403-421`), but runs 4–7 used the old workflow, so their public `testflight-logs` artifacts (now expired) carried `USYM_UPLOAD_AUTH_TOKEN`. **No rotation recorded.** The token also sits inside the Dropbox export zip. **Deleting the 7 repo-level iOS secrets has no record** (`IOS_BUILD.md:200` still warns). Actions tag-pinned, fastlane unpinned (`:369-377`). `Tools/iOS/` not in CODEOWNERS though it runs with the signing key. | Rotate, delete, SHA-pin, CODEOWNERS (§9). | Shombith (admin) | S | **Y** |

Also open: the iOS 27 SDK stops honouring `UIRequiresFullScreen` (TN3192, `PLATFORM_UNIFICATION.md`).

## 4. Android / Google Play

| Item | Current state + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| Target / min API | Target 36 (`ProjectSettings.asset:186`; the build refuses lower, but lets 0 "Automatic" through, `CosmicShoreBuildPipeline.Android.cs:302-306`). Min 28. | – | – | – | N |
| ARM64 / IL2CPP / AAB | IL2CPP, ARMv7 + ARM64 (#1514), `buildAppBundle = true` (`…Android.cs:127`). **No bundle with ARMv7 uploaded yet** (ticket #1515 open). No Android build profile (not needed). | Upload the 32-bit build. | Yash | S | **Y** |
| Build identity | Package `com.FrogletGames.CosmicShore`, versionCode = minutes since 2026-01-01 (`…Android.cs:274-283`). No SHA shown. | SHA stamp (shared fix). | – | S | **Y** |
| Upload key / Play App Signing | No keystore committed; fields blank; Play App Signing with a Google key (`ANDROID_BUILD.md` §5). **Nothing records where the upload key is backed up or who holds it.** | Password manager, two holders. | whoever uploaded build 412826 | S | **Y** (if lost) |
| Play Console app / tracks | Build 412826 on **internal testing** (`ANDROID_BUILD.md:5-8`). Closed/open/production "later"; the cited rollout tracker is not in the repo. Account type (Personal vs Organization) unrecorded: a Personal account must run a 12-tester, 14-day closed test before production. | Track runbook; account type. | Play admin | M | Store |
| **Data safety form** | Only the fair-play draft. Vivox's `.aar` merges **`RECORD_AUDIO` and `BLUETOOTH_CONNECT`** into the manifest, undeclared. | Full form (PostHog, UGS Analytics, crash, Cloud Save, friends, display name, faction chat, audio) — or strip the permissions if chat is text-only. | Play admin (not named in any doc) + counsel | M | **Y** |
| Content rating / audience | Not found; COPPA gap (§8). | IARC, target audience, ads declaration. | Play admin (not named in any doc) + counsel | M | **Y** |
| **16 KB page size** | **arm64 `liblofelt_sdk.so` LOAD align `0x1000` — fails.** FMOD (arm64, x86_64), resonance audio, Vivox all `0x4000`. 32-bit exempt. Unity's own libs: read the build's `.aab.check.txt`. Ticket #1710 open. | Update or replace Nice Vibrations / Lofelt. Play enforces from 2027-02-01. | unknown | S–M | Store (Feb 2027) |
| Play Games sign-in | #1675 merged, ships **off** (`MobileSignInConfig.asset:17-19`), never built with Gradle. | PGS project, UGS provider, a Gradle build. | Shombith | M | N |
| Deletion URL | `https://www.froglet.games/delete-my-data` live (200, 2026-11-05). | Enter it in Play Console. | Play admin | S | **Y** |
| CI build | None. No workflow builds Android; `build/android` is 1,030 commits behind. Upload by hand (`ANDROID_BUILD.md` §6). | UBA builder + Actions orchestration (§11). | Shombith | L | N (Y for cadence) |
| Rollback | No staged-rollout or halt procedure; every device in a match must run the same commit (`ANDROID_BUILD.md:208`) and nothing checks it. | Halt runbook + version gate (§5). | Shombith | M | **Y** |
| Editor verification | `UNITY_VERIFICATION_CHECKLIST.md` § "Android release build + pre-upload App Bundle check" red (refusal dialogs, exit codes, `AndroidBundleCheckTests`). | Steps 1, 2, 4-7. | anyone with Unity + Android | S | N |

## 5. Web Toybox (www.froglet.games/play)

| Item | Current state + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| **Build source** | Hand upload. Live `latest.json` `"version":"2"`, Last-Modified 2026-10-22. `toybox-publish.yml` has **0 runs**; the UBA download path "has not run" (`WEBGL_TOYBOX.md` §5.3). | UBA WebGL target (`WebToyboxBuild.PreExport`) and a first workflow run. | not declared | M | **Y** |
| **Hosting** | The live build root is **`https://pub-9f94c53ca4e4416fa39c5622426f1ccc.r2.dev/toybox`**, Cloudflare's rate-limited development URL. `play.froglet.games` returned 502 (not set up). The doc says to switch before announcing (`WEBGL_TOYBOX.md` §0, §7). `/play/` (trailing slash) is 404; `/play` redirects to `/#play`. | Custom domain on R2 + CORS; landing repo `_data/toybox.yml`. | DNS owner | M | **Y** |
| Versioning / identity | Version = UBA build number or a free string. `build_manifest.txt` is never uploaded (`builds/2/build_manifest.txt` 404). No commit on the live build. | SHA + date in `latest.json` and on the page. | – | S | **Y** |
| Cache-busting | Good: hashed names under `builds/<v>/`, `immutable` year-long cache; `latest.json` `no-cache`. | – | – | – | N |
| Rollback | Hand-rewrite `toybox/latest.json` (§5.3). `builds/1` still on R2, but its hashed file names are recorded nowhere. | `publish_toybox.py rollback` + a stored manifest per build. | – | S | **Y** |
| Staging | None (`/play-staging` not found). `--prefix` and `check --root` could host one. | A staging prefix and a page that loads it. | – | S | N |
| Load size / time | No budget. Measured: **114 MiB** (`.data.br` 65 MB, `.wasm.br` 17 MB, `SFX.bank` **37 MB uncompressed**). "Loading sound…" waits up to 90 s. Memory 256 MB → 4 GB max. | Budget, time-to-first-frame measurement, smaller web banks. | – | M | N |
| Compression | Brotli, served with `Content-Encoding: br` and `application/wasm` (`publish_toybox.py check`: "All good"). FMOD banks raw. | – | – | – | N |
| Mobile browsers | The site shows "Best on a Desktop" on small/touch screens; untested; 4 GB max memory is unrealistic on iOS Safari. | Decide supported or not. | – | M | N |
| Analytics / UTM | Site: GTM + GA4 + Google Ads with consent mode (denied by default in EEA/UK, **granted elsewhere**). Loader events `toybox_launch/ready/error/gamepad`. In-game: none (UGS and PostHog off under `FROGLET_WEB_TOYBOX`). **0 `utm_` links.** `utm-conventions.md` still has `{STEAM_URL}`. | Tagged store links; a privacy review of default-granted consent outside the EEA. | – | S | N |
| Store links | None. "Get the Full Game" → `/#platforms` (Windows/Android "Request test access", iOS a TestFlight form). | Real store URLs with UTM once they exist. | – | S | Store |
| Secrets hygiene | Env `toybox`: `UBA_API_KEY, UBA_ORG_ID, UBA_PROJECT_ID, UBA_TOYBOX_TARGET, R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET`. Actions tag-pinned. **Leak path:** `cmd_fetch_uba` prints up to 4000 chars of the UBA response on an unexpected shape, and a failed `urlretrieve` can print a signed URL GitHub does not mask. Reviewers unverified. | SHA pins, redact the error dump. | – | S | **Y** (before first run) |

`UNITY_VERIFICATION_CHECKLIST.md` § "The browser Toybox: a WebGL build of Menu_Main's freestyle" is still "NOT EDITOR-VERIFIED".

**Amoebius:** confirmed not shipped (no `Assets/` dependency on `Port/`; `LaunchAmoebius.cs` is editor-only). Two
public-repo facts for you to decide on, nothing more: `Port/dist/Amoebius-Windows.zip` (35 MB, plain git) and the
prerelease `prisma-launcher-b9a7df3` are downloadable by anyone, and `cs-build` can sign an `.ipa`/`.aab` under the
production bundle id. Its CI (`amoebius-parity-ci`) has 0 green runs of 253; that is not a launch gate.

---

## 6. Cross-cutting: live updates without a store release

**✔ re-checked: every build signs in to the UGS `development` environment.**
`ProjectSettings/Packages/com.unity.services.core/Settings.json` has `"EnvironmentName": "development"`,
`AuthenticationServiceFacade.cs:185` calls `UnityServices.InitializeAsync()` with no options, and nothing in `Assets/`
or the build scripts calls `SetEnvironmentName`. So a launch today puts players' data next to test data,
`economy.js:165,179`'s production-only refusal of debug crystals never applies, and a production deploy reaches nobody.

| Item | Current state + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| **UGS environment per build** | `development` everywhere (above). | Release builds → `production`, chosen by the build pipeline (dev builds stay `development`). | Shombith | S | **Y** |
| Remote Config | Not found (no package, no code; `REFACTOR_BACKLOG.md` S70). | A remote source for flags. **A new package: needs your approval first.** Alternative with no new package: a Cloud Code endpoint or a Cloud Save public-data key read at boot. | Shombith | M | **Y** |
| Kill switches | None remote. ✔ `static readonly bool Enabled = false` in `ServerEconomy.cs:18`, `ServerScores.cs:17`, `ServerMatchReports.cs:16`, `ServerTrust.cs:16`, `ServerEntitlements.cs:18`; sign-in toggles in `Resources/` assets. | Remote flags for each. | Shombith | M | **Y** |
| Minimum version / forced update | Not found. `Application.version` is display and telemetry only. | A version gate + a "please update" screen with store links. | Shombith | M | **Y** |
| **Multiplayer protocol** | ✔ `ProtocolVersion: 24` (`NetworkManager.prefab:51`); the bump rule is in `Docs/claude/MULTIPLAYER_AND_SOCIAL.md:12-61`. **Not in lobby or session data**: queries filter on game mode and player count only (`MultiplayerSetup.cs:475-476,602-605`; `PresenceLobbyService.cs:246,561-568`). Old and new clients find each other; the host refuses with `NetworkConfig mismatch`, seen as a generic bounce (B35). | Protocol in a session property, filtered on; a clear message. | Yash (MP) / Shombith | S | **Y** |
| Cloud Save schema | `SchemaVersion` written (`WeeklyChallengeCloudData`=4, `PlayerProfileData`=2, others 1) but **never read**. Whole-object writes, no lock (`CloudDataRepository.cs:114`, `UGSCloudSaveProvider.cs:206`): an old client loads a newer record, drops unknown fields and saves the rest back. | Refuse or preserve newer records; write locks. | Shombith | M | **Y** (once saves are on) |
| CCD / Addressables | Not used; all content ships in the binary. | Accept for v1. | Shombith | L | N |
| `ugs-deploy.yml` | Dispatch only, `bleeding-edge` only (`:34-38`), dev/prod input. Tests + price check first (`Tools/UGS/deploy.sh:23-25`). **No GitHub `environment:` ✔**, so no approval: anyone with write access can deploy to production; secrets are repo-level. No dry-run/diff, no rollback (a revert merge). `npm install -g ugs` **unpinned**, right before the step holding secrets. Never run against production. | `ugs-production` Environment with reviewers, a diff step, pinned CLI, SHA-pinned actions, a rollback-to-ref input. | Shombith | S | **Y** |
| Web republish | `toybox-publish.yml` (§5). The web build never signs in to UGS. | Reviewers. | Shombith | S | N |

**What can change live today, with no new build:** Cloud Code script rules (but every server gate is off, so nothing
calls them yet), leaderboard reset schedules and boards, moderation decisions, per-player Cloud Save edits, the
Toybox's `latest.json`, Vivox on/off in the dashboard (also needs settings that are not committed).

**What needs a new build today:** every gate and kill switch, the UGS environment, sign-in providers, the protocol
version, the weekly challenge (its pick is `hash(UTC Monday)` over a pool built into the build,
`WEEKLY_CHALLENGE.md:38-46`), retry and rate settings (`UgsRequestPolicy.cs:90-111`), client-side prices, all content.

## 7. Cross-cutting: backend, CI, quality

### Backend (Unity Gaming Services — Shombith's area; Yash does Cloud Code)

| Area | State | Blocks |
|---|---|---|
| Auth | Anonymous only (`AuthenticationServiceFacade.cs:282`): a reinstall or new device loses the account, no recovery. Steam, Game Center, Play Games built and **off**; Apple/Google/Facebook are stubs that return success (`:403-411`). | **Y** for Steam (sign-in via Steam is the account) |
| Lobby | Sessions SDK 2.3.3; one global presence lobby capped at 100 (`HostConnectionService.cs:57`); more than 100 concurrent is unhandled. B31 (429s) fixed, awaiting retest. | **Y** at public scale |
| Relay | B36 (host dropped after ~10 s freeze) root-caused, unfixed (`MULTIPLAYER_START_HERE.md` U6); B35 awaiting retest. | **Y** |
| Vivox | Port text chat only; not enabled in ProjectSettings; no word filter. | N (hide or enable) |
| Leaderboards | `weekly_challenge` only; clients post directly (`WeeklyChallengeLeaderboardService.cs:239`) with the score gate off, so scores can be faked. | Store |
| Rate limits | `UgsRequestPolicy`: 3 retries, 10/min budget; Cloud Save keeps its own loop (`UGSCloudSaveProvider.cs:196`). | N |
| Quotas / cost | Not found for UGS (Lobby, Relay, Cloud Save, Vivox, Analytics). PostHog $0 cap step unrecorded (`ANALYTICS_HANDOFF.md:273`). | Store |
| Analytics | UGS Analytics + PostHog (EU) behind opt-in consent. **UGS drops undeclared events and A3 (28 events, 67 parameters per environment) is open** (`ANALYTICS_HANDOFF.md:274`). | **Y** (no data otherwise) |
| Crash reporting | Cloud Diagnostics on (`UnityConnectSettings.asset:20`), managed capture armed after consent (`CrashReportingService.cs`), Engine Diagnostics off. Dashboard never checked, no test crash (`BUILD_AND_DELIVERY.md` §5). Whether native dumps wait for consent: unverified. | **Y** |

### CI health

| Workflow | Last trunk run | Real work? | Note |
|---|---|---|---|
| `bleeding-edge-guard` | green 2026-11-05 ([42095529959](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42095529959)) | static checks only; **Unity compile skipped** | [Cosmic-Shore#1695](https://github.com/froglet-studio/Cosmic-Shore/pull/1695) (open) moves the compile to GitHub-hosted GameCI; waits on `UNITY_EMAIL`/`UNITY_PASSWORD` in the `unity-ci` Environment. Its negative control already failed correctly (run #644). |
| `unity-ci` | green 2026-11-05 | static only; `unity` job skipped | Red 11-03 on console logging. |
| `amoebius-parity-ci` | red; 0 / 253 ever | real | Out of launch scope. |
| `perf-daily` | red 2026-11-05 (its Amoebius job) | real | Last green 10-27. |
| `toybox-publish` | never run | – | §5. |
| `ugs-deploy` | green 2026-11-01 (development) | real | §6. |
| `ios-testflight` | green 2026-10-13 | real | §3. |
| `build-branch-ci` | last run 2026-08-12 | Unity skipped | Does not fire: pushes made with `GITHUB_TOKEN` trigger no workflow. |

- **Last real Unity compile in CI: 2026-10-06** (guard run [37488258879](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/37488258879)). Every green check since is Python.
- Generator `--check` gates (`Tools/CI/check_generators.sh`: 60 `author_*` + 2) run in the guard, `unity-ci` and `/ship`: green.
- Node 20 deprecation warnings in every job.

### Quality

| Item | State + evidence | What's missing | Size | Blocks |
|---|---|---|---|---|
| EditMode suite | Last Run All 2026-10-13: **48 failures** (`Docs/QA/DEV_TASKS.md` DT-003); fixes claimed since, never re-run. | A Run All (comes free with #1695's licence). | S–M | **Y** until measured |
| QA backlog | **181 open** (P0 80, P1 87, P2 14; 170 never run). Itemised only to about PR #1010. | Rescan (`/qa-backlog`, R2) + the P0 pass. | L | **Y** (P0) |
| Verification checklist | `UNITY_VERIFICATION_CHECKLIST.md`: ~580 🔴 sections, 402 unticked boxes. | Triage to the launch path. | L | **Y** (launch-path rows) |
| Perf budgets | **No per-tier budget table.** One target: 60 fps / 16.7 ms on the reference PC, no frame over 50 ms (`PERFORMANCE_OPTIMIZATION.md` §3). Tiers exist (Desktop / MobileHigh / MobileLow, mobile 30 fps). Minimum Android spec undecided. `Docs/perf/status.json`: 47 regressions, all 5 budget scenarios `owed`. | Budgets per tier + an enforcing run. | M | **Y** (mobile) |
| Low-end phone memory | Not found. `MEMORY_AUDIT.md` has editor readings only (1.5–1.8 GB managed on a 32 GB PC). | A device measurement. | M | **Y** (mobile) |
| Bug ledger | 266 open, 15 validating (`Docs/perf/status.json`, 11-02). | Triage to the launch path. | L | partial |

## 8. Cross-cutting: legal, store assets, analytics, support

### Legal and compliance

| Item | State + evidence | What's missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| Privacy policy | **Live**: `https://www.froglet.games/privacy` (200, "Last updated October 14, 2026"), from repo `froglet-studio/cosmic-shore-landing-page`; linked in-game. | Counsel review; the 11-row code/policy gap table (`Docs/Legal/README.md:104-116`). | Caleb asked (`Reports/STEAM_LAUNCH.md` §5, item 6) | S | **Y** |
| Deletion page | **Live**: `/delete-my-data` (a form, deletion by hand within 30 days). | In-app delete (§3). | Shombith | M | **Y** (stores) |
| **COPPA / age gate** | Birth-year gate exists (`PrivacyConsentOverlay.cs:155-250`), but age is computed from the year alone (`AnalyticsServiceFacade.cs:273`), so some 12-year-olds pass as 13; and **sign-in, Cloud Save, presence and Friends start before the gate** (`AppManager.cs:189` runs before `:301`). Under-13s keep public names and Friends. | Counsel's posture; the year-only fix; social features gated for under-13s. | counsel; Shombith (H19/F4) | M | **Y** |
| GDPR consent | Opt-in, gated; crash capture gated. ✔ `ProjectSettings.asset:96` `submitAnalytics: 1` sends Unity hardware stats with no consent. Consent text does not mention crash reports. UGS `Start/StopDataCollection` deprecated. | `submitAnalytics: 0`; consent copy; PostHog DPA, Unity DPA; retention periods. | unknown | S | **Y** |
| Report & block | Built (`FriendProfilePopup.cs` → `PlayerReports.cs`, UGS Moderation; `BlockPlayerAsync`; name filter `DisplayNameValidator.cs:86`). Reachable only from a friend's profile. Editor-untested. | Report from scoreboard/leaderboard. | unknown | S | N |
| Credits | Built (`CreditsManifest.asset`, `CreditsModal.cs`, `CreditsReleaseGuard.cs` fails a release build without it); never opened in the editor. Bangers font not credited. | Fix + one look. | unknown | S | N |
| Third-party register | No rows for Vivox, Steamworks.NET, PostHog, Moderation. "Effects Library" vendor unknown. **Electronic Highway Sign font has no licence and ships** (Resources, `Manta.prefab`). Obvious SOAP: no licence file. | Complete; licence or remove the font. | unknown | M | **Y** |
| FMOD licence | Credit done; tier undecided (free under $600k budget / $200k a year, `THIRD_PARTY_DECISIONS.md:39`). | Check thresholds, buy if needed. | unknown | S | **Y** |
| **Commercial SFX in a public repo** | ✔ `Cosmic Shore/Assets/AEROJet_Airplane Jet_S6k01-50.wav`, `Cosmic Shore/Assets/VEHInt_Car Wash_S6k04-97.wav` (and others) carry sound-library names, in the public tree. | Licence check; remove from the public tree (and history if the licence forbids redistribution). | unknown | M | **Y** (licence) |
| EULA / ToS | Steam default EULA (`Docs/Legal/README.md:16`); Terms of Sale on hold (DLC vs tokens); Code of Conduct draft; no mobile ToS. | Counsel. | unknown | S | Store |
| Company entity | "Froglet Inc." (privacy page) vs "Froglet Games" (`companyName`, footer, delete page); two addresses (Grand Rapids vs Ada). | One name, one address (Steam KYC must match the bank exactly). | Garrett (A1a) | S | **Y** |

### Store assets

| Item | State | Missing | Owner | Size | Blocks |
|---|---|---|---|---|---|
| Steam capsules (5) | Not found (H13). | All five. | Caleb (A5) | M | Store |
| Screenshots | Not found; capture tool `SCREENSHOT_DIRECTOR.md`. | Steam ≥5 at 1920×1080; iPhone 6.9″ + iPad 13″; Play phone/tablet (H15). | unknown | S | Store |
| Trailer | Not found (H14). | v1 + 30 s. | unknown | M | N |
| Store copy | Not found (H16). | Short/long, tags, per store. | unknown | S | Store |
| Icons | iOS slots empty (default RGBA used); Android adaptive slot points at a guid no asset has (`1f1e73d4…`). Play feature graphic exists (`App Icons/Feature Graphic.png`). | Opaque iOS 1024; fix the Android reference. | unknown | S | N |
| Age ratings | Not done (H2): IARC, Apple, Steam content survey. | All. | Garrett (A3) | S | **Y** (mobile tracks) |
| Store privacy labels | Fair-play draft only. | Labels that match this build. | unknown | S | **Y** (mobile) |
| Localisation | None (no `com.unity.localization`). | Decide English-only. | – | L | N |

### Launch metrics — where each is instrumented

| Metric | Where | Missing |
|---|---|---|
| D1 / D7 retention | `invite_wave` person property (`DATA_ARCHITECTURE.md:690-696`) | Dashboards not built (`:778`); UGS A3 unconfirmed. |
| Crash-free sessions | Cloud Diagnostics, consent-gated | No session-start event, no stability view; decliners drop out of the number. |
| Session length | Partial: `session_ended` has no duration; `game_completed.duration_seconds`; `menu_ready.seconds_since_launch` | **Not instrumented as a metric.** |
| Mode pick share | `game_started.game_mode` (`AnalyticsServiceFacade.cs:628`) | A dashboard. |
| Website → install | **Not instrumented.** Vocabulary only (`utm-conventions.md`); no tagged links. | Tagged links; store-side attribution once apps exist. |
| Installs per platform | Store dashboards only. | Not connectable from the repo. |

Note: Frogma's **Froglet Analytics** tab (Rev 50) proposes "our own backend replaces PostHog". The launch metrics
above must not be re-plumbed twice: whichever backend wins, the event names stay.

### Support

| Item | State | Missing | Blocks |
|---|---|---|---|
| Support contact | `support@froglet.games` + `/support` live. Code defaults to `support@frogletgames.zendesk.com` (`SendBugReport.cs:20`), only in unreferenced prefabs. | One address. | N |
| In-game feedback | Settings bug row opens `/support` (menu only). | A form with build id + log. | N |
| Status channel | Discord, linked from `/support`. No status page. | A status post template. | N |
| **Incident runbook** | Not found (only `STEAM_PLAYTEST_RUNBOOK.md` pause criteria §217). | Written in Phase 4 (`Docs/LAUNCH/RUNBOOK.md`). | **Y** |

## 9. Security

| Item | State + evidence | What's missing | Size | Blocks |
|---|---|---|---|---|
| **Trunk review** | Ruleset "Protect trunks" requires a PR, 1 review, code-owner review, last-push approval, **no required status checks**, and allows a bypass team in PR mode. **The bypass is routine**: #1743, #1742, #1737, #1723 and the workflow-changing #1679 and #1674 merged with 0 reviews, some within 1–3 minutes. `build/*` is protected only against deletion. In flight: [Cosmic-Shore#1693](https://github.com/froglet-studio/Cosmic-Shore/pull/1693) runs the guard on PRs so a ruleset can require it, adds CODEOWNERS and `Docs/BRANCH_PROTECTION.md`. | Merge #1693; required checks; remove routine bypass; `non_fast_forward` on build branches. | S | **Y** |
| Agent identity | `.github/CODEOWNERS:5`: agent sessions act on GitHub as **Shombith03**, a code owner with bypass. `frogletagent-builder` exists, unused. | Agents on a bot account or App without code-owner or bypass rights. | S | **Y** |
| USYM token | Scrubbed now; earlier public artifacts carried it; no rotation record; also in the Dropbox zip. | Rotate; replace the zip; record it. | S | **Y** |
| Old repo-level iOS secrets | #1068 asked to delete 7; no record; unverified. | Admin check and delete. | S | **Y** |
| UGS production secrets | `UGS_CLI_SERVICE_KEY_ID`, `UGS_CLI_SERVICE_SECRET_KEY` repo-level, readable by any branch's workflow. | Move into a `ugs-production` Environment. | S | **Y** |
| Action pinning | Third-party and first-party actions pinned by tag (`@v4`, `@v5`) in every workflow except `claude-code-action`; `npm install -g ugs` and fastlane unpinned. No Dependabot. | SHA pins; pinned CLI versions; Dependabot for actions. | S | **Y** (release workflows) |
| Self-hosted runners | None attached. Fork-approval setting unverified. | Keep it that way; set "Require approval for all external contributors". | S | N |
| Committed secrets | None found (no `.p12`, `.mobileprovision`, `.keystore`, `.jks`, `.p8`, `.pem`). | – | – | N |
| Binaries in git | 2.24 GB tree, 4.08 GiB pack; LFS covers only `*.so`, `*.bundle`. FMOD project 573 MB (incl. `.cache`); music `.wav`s 36–55 MB; `Port/dist` zip 34 MB. | Licence review (above); LFS or cleanup later (R15). | M | N (Y for licence rows) |

---

## 10. Ranked blocker list — everything that must be true on launch day

Ordered by **risk × effort**: the biggest risk for the least work first. "Who" is the owner who decides; "Human" means
a step only a person with an account can do. Pipeline and console work in Phases 2–3 covers the rows marked **P2**/**P3**.

| # | Must be true on launch day | Risk | Effort | Who | Phase |
|---|---|---|---|---|---|
| 1 | **Release builds talk to UGS `production`**, chosen by the build pipeline; the production dashboard has the board, the 28 events / 67 parameters, the identity providers and Moderation. | Very high | S + human | Shombith | P2 + Human |
| 2 | **Repo security settings**: no routine bypass on trunk, required checks, `ugs-production` Environment with reviewers holding the UGS secrets, `testflight`/`toybox` restricted to `bleeding-edge` with reviewers, the 7 old iOS secrets deleted, `USYM_UPLOAD_AUTH_TOKEN` rotated and recorded, agents on a separate account. | Very high | S (human, minutes each) | Shombith (admin) | Human (+ #1693) |
| 3 | **Steamworks partner account, fee, base app and Playtest child app exist** (A1–A4), entity name and bank match. Starts every Steam clock. | Very high | S (human) | Garrett + Shombith | Human |
| 4 | **A Unity compile + EditMode run gates trunk**: #1695 merged, `UNITY_EMAIL`/`UNITY_PASSWORD` in `unity-ci`. | High | S + human | Shombith | P2 (build on #1695) |
| 5 | **Every build is identifiable**: version, build number and commit SHA on screen, in every artifact, in crash metadata and analytics. | High | S | Shombith | P2 |
| 6 | **Old and new clients never meet**: protocol version in session data and filtered on; a minimum-version gate with an update screen. | High | S–M | Yash / Shombith | P2 (code PR) |
| 7 | **Production deploys are gated**: `ugs-deploy` behind an Environment with reviewers, a diff before upload, pinned CLI, a rollback input. | High | S | Shombith | P2 |
| 8 | **A Windows IL2CPP player reaches the menu and plays a match** (R1), built by a pipeline from a tag, uploaded to the Steam Playtest `internal` branch. | Very high | M–L | Shombith | P2 + Human |
| 9 | **Consent and children**: counsel's COPPA posture applied (social features after the gate, a real age check), `submitAnalytics: 0`, consent copy names crash reports. | High | M | counsel + Shombith | Human + code |
| 10 | **Licences clean**: FMOD tier, SOAP receipt, Electronic Highway Sign licensed or removed, commercial SFX out of the public tree, register complete. | High | S–M | unknown | Human |
| 11 | **Remote kill switches** for the five server gates and the mode list (Remote Config if you approve the package; else Cloud Code / Cloud Save). | High | M | Shombith | P2 (decision first) |
| 12 | **Persistence decided** (`CloudEnabled`), and Cloud Save refuses newer schemas instead of overwriting them. | High | M | Shombith | P2 (code PR) |
| 13 | **Web on its own domain**: `play.froglet.games` on R2, the publish pipeline run once, a staging root, a rollback command, the SHA in `latest.json`, the error dump redacted. | High | S–M + DNS | DNS owner + Shombith | P2 + Human |
| 14 | **iOS builds from a commit** (UBA iOS target → Xcode export → the existing signing workflow), with a privacy manifest. | High | L | Shombith | P2 |
| 15 | **In-app account deletion** (UGS `DeleteAccountAsync` + Cloud Save + PostHog), required by both mobile stores. | High (review) | M | Shombith | Code PR |
| 16 | **Mobile store forms match the build**: Play Data safety (incl. the Vivox microphone permission or its removal), IARC, target audience; App Store privacy label, age rating; TestFlight external group and a new public link. | High (review) | S–M (human) | store admins (not named in any doc) + counsel | Human |
| 17 | **Android is reproducible**: a UBA Android target driven from Actions, upload to the internal track behind approval; key custody recorded; 32-bit build uploaded. | Medium | L | Shombith / Yash | P2 + Human |
| 18 | **Crash reporting proven**: Cloud Diagnostics on in the dashboard, a forced crash from each platform's build seen there. | Medium | S | Shombith | Human + P2 |
| 19 | **Measured test state for the launch path**: EditMode Run All, the QA P0s for the launch path, a per-mode smoke run in Unity. | High | M–L | team | P2 (smoke) + QA |
| 20 | **Multiplayer holds**: B36 fixed, B35/B31 retested, R13 friend requests reachable, R16 4-player two-machine pass. | High | M | Yash / Shombith | Code + playtest |
| 21 | **Analytics live**: UGS A3 declared per environment, D1/D7, crash-free, session length and mode-share views, tagged website links. | Medium | M | Shombith | Human + P3 |
| 22 | **Runbook and comms**: launch-day runbook, per-platform rollback, incident steps, status post template. | Medium | S | – | P4 (this plan) |
| 23 | **Perf and memory budgets per device tier**, one low-end phone measurement. | Medium | M | team | Later |
| 24 | **Store pages** (Steam capsules, screenshots, copy, trailer; mobile screenshot sets). Not needed for an invite build. | Medium (Store) | L | Caleb + art | Store launch |
| 25 | **Lofelt 16 KB** fixed or replaced before 2027-02-01. | Low now | S–M | unknown | Before Feb 2027 |

### Needs a human, in order

1. **Repo settings (Shombith, admin):** ruleset bypass and required checks; create `ugs-production` and move
   `UGS_CLI_SERVICE_KEY_ID`/`UGS_CLI_SERVICE_SECRET_KEY` into it; `testflight` and `toybox` deployment branch
   `bleeding-edge` + a required reviewer; delete repo-level `IOS_*`, `ASC_*`, `APPLE_ID_EMAIL`,
   `APPLE_APP_SPECIFIC_PASSWORD`, `ASC_APP_ID`, `ASC_ITC_PROVIDER` if present and revoke the app-specific password;
   Actions → General → "Require approval for all external contributors"; agents on `frogletagent-builder` or an App.
2. **Rotate** the Cloud Diagnostics symbol-upload token behind `USYM_UPLOAD_AUTH_TOKEN` (Unity dashboard), replace the
   Dropbox export zip, and write the date in `Docs/IOS_BUILD.md`.
3. **Unity CI seat:** a Unity ID made for CI, its `UNITY_EMAIL`/`UNITY_PASSWORD` (and `UNITY_SERIAL` for a Pro seat)
   in the `unity-ci` Environment, limited to `bleeding-edge`/`development` (`BUILD_AND_DELIVERY.md` §10.1 on #1695).
4. **Steamworks (Garrett + Shombith):** entity, EIN, address, bank name matching exactly; partner account on a
   company-controlled Steam account; agreements signed; $100 Steam Direct fee; tax + banking; base app and depot ids;
   Playtest child app (its customer-visible name is permanent); builder account on the build machine only.
5. **UGS production (Shombith):** board `weekly_challenge`, the 28 events / 67 parameters, identity providers (Steam
   Web API key entered in the dashboard only), Moderation, Vivox on or the Port tab hidden, Cloud Diagnostics on,
   Billing allowances and spend alerts.
6. **Counsel:** privacy policy vs the gap table, COPPA posture, DLC vs tokens, Steam's default EULA; entity name.
7. **Licences:** FMOD tier; SOAP receipt; Electronic Highway Sign; the sound-library `.wav` files.
8. **DNS / Cloudflare:** `play.froglet.games` → R2 + CORS; landing repo `_data/toybox.yml` `build_root`.
9. **Unity Build Automation:** Web, Android and iOS targets on `bleeding-edge` with the repo's build methods; the
   Android keystore in UBA's signing store only.
10. **Play Console:** account type; Data safety; IARC; target audience; deletion URL; key custody in the password
    manager with two holders; upload the 32-bit build.
11. **App Store Connect:** privacy label, age rating, external TestFlight group, new public link.

---

## 11. Decisions (confirmed by Shombith, 2026-11-05)

| Decision | Answer |
|---|---|
| Milestone | **Invite build v1, 2026-12-03.** Invite-only everywhere; public store pages come later. |
| Platforms | **Windows / Steam (Playtest), iOS (TestFlight), Android (Google Play), Web Toybox.** macOS out. Amoebius out. |
| Live flags | **Cloud Code / Cloud Save**, no new package: one flags document read at boot (kill switches, minimum version, the mode list), deployed through `ugs-deploy` with development first. |
| Launch Console | **A FrogletTools editor window and a claude.ai artifact**, both reading one status file the pipelines write; designed on Frogma's *Launch Console* tab. |

The questions as they were asked:


1. **Milestone and date.** Working assumption "Invite build v1", 2026-12-03. Steam's store page cannot be live by then
   (about 39 days after the fee); a Steam **Playtest** invite can, if A1–A4 start this week.
2. **Platforms in the invite build.** Recommended: Windows (Steam Playtest), iOS (TestFlight external), Android (Play
   internal → closed), Web (Toybox). macOS out.
3. **Remote flags.** Adopt Remote Config (a new package), or carry flags on Cloud Code / Cloud Save with no new package
   (recommended for v1: no new dependency, and the five gates already talk to Cloud Code).
4. **Player builds in CI.** Recommended: GitHub-hosted GameCI for compile + EditMode (#1695, already built), Unity
   Build Automation for player builds driven from Actions. No self-hosted runner on this public repo.
5. **The Launch Console's form.** A FrogletTools editor window (your note), a claude.ai artifact (the original brief,
   phone-readable, with a live checklist store), or both reading one status file.

---

## 12. Where each blocker stands (Phase 2, 2026-11-06)

Every PR below is open into `bleeding-edge`, **not merged**, and depends on the status-contract PR. No C# in them has
been run in the Unity editor; each adds its editor steps to `Docs/UNITY_VERIFICATION_CHECKLIST.md`. This session's
proxy refuses tag pushes and ref deletes, so the tag-triggered pipelines have run only as pull-request rehearsals.

| # | Blocker | PR | Proven | Still needs |
|---|---|---|---|---|
| – | The status contract (every pipeline's record, the console's data) | [#1750](https://github.com/froglet-studio/Cosmic-Shore/pull/1750) | run [42158056726](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42158056726) green; negative control [#1752](https://github.com/froglet-studio/Cosmic-Shore/pull/1752) red on both checks; 8 real records on `launch-status` | merge |
| 1, 5 | Release builds on UGS production; every build identified | [#1749](https://github.com/froglet-studio/Cosmic-Shore/pull/1749) | refcompile player + editor OK; negative control fails to compile | editor check; the production environment set up (human, §10 #1) |
| 4 | Unity compile gates trunk | [#1695](https://github.com/froglet-studio/Cosmic-Shore/pull/1695) (an earlier session's) | reference-compile path proven there | the `unity-ci` licence secrets |
| 6 | Old and new clients never meet | [#1755](https://github.com/froglet-studio/Cosmic-Shore/pull/1755) | refcompile OK; 16/16 tests in a stand-alone harness; two negative controls | the two-editor test |
| 6, 11 | Minimum-version gate, kill switches | [#1756](https://github.com/froglet-studio/Cosmic-Shore/pull/1756) | run [42160372613](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42160372613) green; three negative controls; refcompile OK | editor check; `disabledModes` needs the menu owners |
| 7 | Production UGS deploys gated | [#1748](https://github.com/froglet-studio/Cosmic-Shore/pull/1748) | run [42157122987](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42157122987) green; negative control run [42121172001](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42121172001) red at the gate | `ugs-development` / `ugs-production` Environments |
| 13 | Web staging, promote, rollback | [#1753](https://github.com/froglet-studio/Cosmic-Shore/pull/1753) | run [42158295337](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42158295337) green; 33 self-test checks; 8 mutants killed; fake-R2 refusals | `toybox-production`; DNS; `adopt --version 2`; a first real stage |
| 8, 14, 17 | Tag → release notes → player builds → pre-release channels; promote and rollback | [#1747](https://github.com/froglet-studio/Cosmic-Shore/pull/1747), [#1751](https://github.com/froglet-studio/Cosmic-Shore/pull/1751) | rehearsal [42157116324](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42157116324) green (76 PRs); malformed-tag control [42157168367](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42157168367); builds run [42157929803](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42157929803) stops at the missing Environments; platform control [42158099693](https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42158099693) | UBA targets; the Environments below; a real tag run; two decisions (SteamPipe from CI; the iOS hand-off) |
| 19 | Per-mode smoke test in Unity | [#1754](https://github.com/froglet-studio/Cosmic-Shore/pull/1754) | refcompile player + editor OK; report self-test with its negative control | a Unity run (licence or an editor) |
| – | The Launch Console (artifact) | [#1758](https://github.com/froglet-studio/Cosmic-Shore/pull/1758) | live artifact, 38 store documents, one phone/desktop render check | sharing it with the team |
| – | The Launch Console (FrogletTools window), drawn as Frogma Rev 58 | [#1757](https://github.com/froglet-studio/Cosmic-Shore/pull/1757) | refcompile player + editor OK (a planted type error fails it); 15/15 status tests outside Unity | open it in the editor |

### Environments and secrets to create (names only; values never go in the repo)

| Environment | Reviewers | Secrets | Variables | For |
|---|---|---|---|---|
| `unity-ci` | – (deployment branches `bleeding-edge`, `development`) | `UNITY_EMAIL`, `UNITY_PASSWORD` (+ `UNITY_SERIAL` for a Pro seat) | – | #1695, the smoke test |
| `ugs-development` | – (branch `bleeding-edge`) | `UGS_CLI_SERVICE_KEY_ID`, `UGS_CLI_SERVICE_SECRET_KEY` | – | #1748 |
| `ugs-production` | **required**, prevent self-review | the same two (better: a separate production service account) | – | #1748 |
| `toybox` (exists) | – | its 8 secrets as now | allow `web-v*` tags | #1753 |
| `toybox-production` | **required** | `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET` (better: a second R2 token) | – | #1753 |
| `uba-builds` | – | `UBA_API_KEY`, `UBA_ORG_ID`, `UBA_PROJECT_ID` | `UBA_WINDOWS_TARGET`, `UBA_ANDROID_TARGET`, `UBA_IOS_TARGET` | #1751 |
| `steam-prerelease` | – | `STEAM_CONFIG_VDF`, `STEAM_USER` + the UBA secrets | `STEAM_PLAYTEST_APPID`, `STEAM_PLAYTEST_DEPOTID` | #1751 (opt-in; a decision) |
| `play-prerelease` | – | `PLAY_SERVICE_ACCOUNT_JSON` + the UBA secrets | – | #1751 |
| `windows-production`, `ios-production` | **required** | – | – | #1751 |
| `android-production` | **required** | `PLAY_SERVICE_ACCOUNT_JSON` | – | #1751 |
| `testflight` (exists) | confirm **required** + branch `bleeding-edge` | its 7 secrets | – | `ios-testflight.yml` |

After moving a secret into an Environment, delete its repository-level copy: an Environment without its own copy falls
back to the repository's.

**Branches to delete by hand** (the session's proxy refuses ref deletes): `ci-scratch/launch-tools-negative`,
`scratch/ugs-plan-negative-control`.
