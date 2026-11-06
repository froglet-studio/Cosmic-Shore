# Launch pipelines: from a tag to every pre-release channel, then to production

**One tag drives every platform.** A `v<major>.<minor>.<patch>` or `rc-<label>` tag makes a draft release
(`release.yml`), then every player build and every pre-release upload (`release-builds.yml`); a person promotes to
production with `release-promote.yml`, one platform at a time, behind a reviewer. Every job ends by publishing its
record to the launch status branch (`Docs/LAUNCH/STATUS_CONTRACT.md`), so the Launch Console shows what ran, where,
and how long ago.

Decided in the readiness audit (`Docs/LAUNCH/READINESS.md` §11, Shombith 2026-11-05): **Unity Build Automation (UBA)
builds the players**, driven from Actions over its REST API; GitHub-hosted GameCI only compiles and tests (#1695). No
self-hosted runner. macOS and Amoebius are out of the launch.

```
git push origin rc-0.3.0-1
  └─ Release (release.yml)            notes, build-identity.json, DRAFT release           -> record: release
       └─ Release builds (release-builds.yml, on Release's success, or Run workflow)
            resolve    tag, commit, version, platforms (windows, android, ios, web; anything else fails)
            preflight  the Environments below exist (before any job names one)
            windows    build-windows (UBA)  -> steam-prerelease  (Playtest app, internal branch)  -> build-windows, windows-prerelease
            android    build-android (UBA)  -> play-prerelease   (Play internal track)             -> build-android, android-prerelease
            ios        build-ios (UBA)      -> a person hands it to iOS TestFlight (the seam, below) -> build-ios
            web        dispatches the web pipeline, which records build-web / web-staging itself
  Release promote (release-promote.yml, Run workflow, one platform)                       -> record: <platform>-production
```

## Tools

| Tool | What it does | Proof |
|---|---|---|
| `Tools/Launch/release_notes.py` | Release tag rules, notes since the previous release tag, `build-identity.json` | `self-test`: 41 checks |
| `Tools/Launch/uba.py` | Start a UBA build at a commit, wait, verify the built commit, download; never prints a signed URL or a response body | `self-test`: 29 checks, a fake HTTP layer |
| `Tools/Launch/play.py` | Play Developer API: upload to internal only; promote / rollout / halt / resume; one edit per change, deleted on failure | `self-test`: 27 checks, a fake HTTP layer and a real `openssl` signature |
| `Tools/Launch/environments.py` | The Environments a run needs exist; production ones have Required reviewers | `self-test`: 6 checks |
| `Tools/Launch/launch_status.py` | The status record (`Docs/LAUNCH/STATUS_CONTRACT.md`) | `self-test` |

Every `self-test` runs on each change under `Tools/Launch/` (`launch-tools-check.yml`), which also fails any launch
workflow whose actions are not pinned to a commit.

## Environments and secrets (the user creates every one of these; no session does)

Settings > Environments > New environment. A **secret** is a value nobody may read back; a **variable** is a plain
id. Restrict each Environment's deployment branches and tags to `v*`, `rc-*` and `bleeding-edge`.

| Environment | Reviewers | Secrets | Variables | Used by |
|---|---|---|---|---|
| `uba-builds` | none needed (it builds, it ships nothing) | `UBA_API_KEY`, `UBA_ORG_ID`, `UBA_PROJECT_ID` | `UBA_WINDOWS_TARGET`, `UBA_ANDROID_TARGET`, `UBA_IOS_TARGET` | build-windows, build-android, build-ios |
| `steam-prerelease` | recommended | `STEAM_CONFIG_VDF`, `STEAM_USER`, `UBA_API_KEY`, `UBA_ORG_ID`, `UBA_PROJECT_ID` | `STEAM_PLAYTEST_APPID`, `STEAM_PLAYTEST_DEPOTID`, `UBA_WINDOWS_TARGET` | steam-prerelease (opt-in, below) |
| `play-prerelease` | recommended | `PLAY_SERVICE_ACCOUNT_JSON`, `UBA_API_KEY`, `UBA_ORG_ID`, `UBA_PROJECT_ID` | `UBA_ANDROID_TARGET` | play-prerelease |
| `windows-production` | **required** | none | none | release-promote (prints App Admin steps) |
| `android-production` | **required** | `PLAY_SERVICE_ACCOUNT_JSON` | none | release-promote |
| `ios-production` | **required** | none (an App Store Connect key later, optional) | none | release-promote (prints App Store Connect steps) |
| `testflight` (exists) | required (it does today) | as `ios-testflight.yml` lists | | iOS TestFlight, unchanged |

The web pipeline's own Environments (`toybox`, `toybox-production`) are listed in its workflow,
`toybox-publish.yml` on `claude/launch-web-pipeline`.

Why the UBA key is in three Environments: an upload job downloads its player from UBA itself. Passing the player
between jobs as a workflow artifact would publish it, because **an artifact on a public repository can be downloaded
by anyone signed in to GitHub**. The upload Environments need only the read side of UBA; if UBA offers a read-only
key, use that there.

**A missing Environment stops the run before anything else.** A job that names an Environment that does not exist
makes GitHub create it, empty and unprotected, so `preflight` (and release-promote's first job) checks first and
fails with the names to create. A production Environment without a Required reviewers rule is refused the same way.

## What to create in Unity Build Automation

Unity Cloud > DevOps > Build Automation > the Cosmic Shore project > Configurations. One target per platform, **no
auto-build** (the workflow starts every build at the tag's commit, so the branch a target names is only its
default):

| Target | Platform | Unity | Settings that matter | Its id goes in |
|---|---|---|---|---|
| Windows | Windows 64-bit | 6000.3.17f1 | IL2CPP; executable name `CosmicShore` (Tools/Steam/upload.sh and the depot expect `CosmicShore.exe`) | `UBA_WINDOWS_TARGET` |
| Android | Android | 6000.3.17f1 | **Build App Bundle (.aab)**; the upload keystore in UBA's signing store only (`Docs/ANDROID_BUILD.md` §2, key custody) | `UBA_ANDROID_TARGET` |
| iOS | iOS | 6000.3.17f1 | Xcode 26; the paid team's certificate and App Store profile (`Docs/IOS_BUILD.md` Path B) | `UBA_IOS_TARGET` |
| WebGL Toybox | WebGL | 6000.3.17f1 | Pre-Export Method `CosmicShore.Editor.WebToyboxBuild.PreExport` (exists; the web pipeline owns it) | `UBA_TOYBOX_TARGET` (env `toybox`) |

**The repository's build methods do not run on UBA by themselves.** UBA builds with its own pipeline from the
project's settings, so `CosmicShoreBuildPipeline.BuildWindowsRelease` / `BuildAndroidRelease` (which stamp the
version, write `build_manifest.txt`, and set the shipping player settings) are what a person or `build_windows.sh`
runs, not UBA. The WebGL target closes this gap with a Pre-Export Method; **Windows, Android and iOS have none yet**.
Until they do, a UBA player is built from the committed Player Settings. The upload job writes `build_manifest.txt`
(version, commit) next to the Windows player so the Steam build description still names the tag and commit.
Follow-up: a `PreExport` per platform, as `WebToyboxBuild.PreExport` does (Shombith's area).

**The build stamp** (`claude/launch-build-stamp`, in flight; `Docs/LAUNCH/BUILD_STAMP.md` there) is a build
preprocessor, so it does run on UBA. It reads the commit from git and the version, build number, channel and UGS
environment from `CS_BUILD_VERSION`, `CS_BUILD_NUMBER`, `CS_RELEASE_CHANNEL` and `CS_UGS_ENVIRONMENT`. On UBA those
are the **target's** environment variables (Config > Environment variables), which are fixed per target, not per
build: set `CS_RELEASE_CHANNEL` (`playtest`, `internal`, `testflight`) and leave `CS_UGS_ENVIRONMENT` unset so a
release player talks to `production`. `uba.py start` sends only the commit and a label (the tag); whether UBA's
start-build call can also carry per-build variables, so the stamp shows the tag's version, is **not verified**.

API key: Unity Cloud > Build Automation > Settings > API key. The org and project ids are in the dashboard URL.

## Per platform

### Windows / Steam (Playtest)

`build-windows` starts the UBA Windows target at the tag's commit, waits (up to 220 minutes), and checks UBA built
exactly that commit (`uba.py verify`; another commit is refused). `steam-prerelease` downloads it from UBA, writes
`build_manifest.txt`, installs `steamcmd`, and runs the repository's own uploader:
`Tools/Steam/upload.sh --target playtest --branch internal --set-live` to the Playtest app's password-protected
`internal` branch (`Tools/Steam/README.md`, `Docs/STEAM_PLAYTEST_RUNBOOK.md`). Promotion to `default` (every granted
tester) is never automated: Valve does not let a build script set `default` live, so `release-promote` prints the
App Admin steps and records `windows-production` as waiting on a person.

**The decision this needs from you: SteamPipe from CI or not.** The standing decision (`Docs/BUILD_AND_DELIVERY.md`
§10, *What it does not do*) is that no CI runner uploads to Steam, because the builder account's login must survive
Steam Guard. The standard CI approach, built here as an **opt-in job that refuses to run until you add its secret**:

- a **dedicated builder account** (never a person's) with build rights on the Playtest app only;
- log it in once with `steamcmd +login <builder>` on a trusted machine, answering Steam Guard; that writes
  `config/config.vdf` (inside the steamcmd folder) holding a cached login token;
- store that file base64-encoded as `STEAM_CONFIG_VDF` and the account name as `STEAM_USER` in `steam-prerelease`.

Trade-off: whoever holds `STEAM_CONFIG_VDF` can upload builds and set any non-`default` branch live on the apps the
account can build, without Steam Guard, until the token expires or is revoked (Steamworks > the account > deauthorize,
or a password change). The token expires on Valve's schedule and has to be refreshed by repeating the login; when it
has expired the job fails with "could not log in with the cached session". Mitigations: the account builds the
Playtest app only (not the base app), `steam-prerelease` has reviewers and accepts only `v*` / `rc-*` tags, and the
job writes the file with `umask 077` and deletes it when it ends. The alternative is to keep uploading by hand from
the build machine (`Tools/Steam/README.md`), downloading the UBA build first: nothing else in this pipeline changes.
`steamcmd` cannot be pinned (Valve publishes no versioned build and it updates itself); the job logs the
bootstrapper's sha256 so a change is visible.

### Android / Google Play

`build-android` builds the UBA Android target (an App Bundle) at the tag's commit and verifies the commit.
`play-prerelease` downloads it from UBA and `play.py upload` puts it on the **internal track only** (the tool
refuses any other track). The first upload was made by hand (build 412826, `Docs/ANDROID_BUILD.md` §5), so the API
can make every later one. Service account: Google Cloud > a service account with a JSON key; Play Console > Users and
permissions > invite its email with "Release to testing tracks" (and "Release to production" for the production
Environment's copy, ideally a second account). `play.py` signs its token request with `openssl`, so no Google
library is installed.

Promotion (`release-promote`, platform android, behind `android-production`):

| action | what happens |
|---|---|
| promote | the version code must already be on internal; it goes to `track` (a closed track such as `alpha`, or `production`). Production needs `user_fraction` (a staged rollout); 100% needs `allow_full`. |
| rollout | widens the staged rollout to `user_fraction` |
| halt | stops the staged rollout (no new installs) |
| resume | restarts a halted rollout |
| rollback | halts, then says how: Play cannot re-serve an older version code, so build the earlier tag again (its version code is higher) and promote that |

A Personal Play account must run a 12-tester, 14-day closed test before production (`Docs/ANDROID_BUILD.md` §5); the
account type is still unrecorded (READINESS §4).

### iOS / TestFlight

`build-ios` builds the UBA iOS target at the tag's commit and verifies the commit. **The hand-off to TestFlight is a
seam a person crosses today**, deliberately:

- `ios-testflight.yml` (signing, the #1068 log scrub, the upload) is unchanged. It takes the Xcode export from the
  `IOS_RELEASE_EXPORT_URL` secret.
- Making the export a **workflow artifact** was the brief's preferred seam, and it is not safe here: this repository
  is public, so the artifact would be downloadable by anyone signed in, and the readiness audit found the Xcode
  export carries `USYM_UPLOAD_AUTH_TOKEN` (READINESS §3, #1068 leftovers).
- Today: download the export from UBA (Build Automation > the iOS target > the build the run names), share it
  privately, put the link in `IOS_RELEASE_EXPORT_URL`, run **iOS TestFlight**. Or let UBA sign and upload itself
  (`Docs/IOS_BUILD.md` Path B).
- The safe automated seam, for Shombith to decide (it changes `ios-testflight.yml`'s secrets): give `ios-testflight.yml`
  a `workflow_call` input `uba_build` and the UBA secrets in the `testflight` Environment, and have its download step
  call `uba.py download --platform ios --build <n> --commit <sha> --artifact-key <the Xcode project's key>` instead of
  the URL. The player then goes UBA -> the signing job directly and is never published. Which artifact key UBA gives
  the Xcode project is not verified (no target exists yet).

Production (`release-promote`, platform ios): phased release pause / resume / release-to-all and rollback are App
Store Connect steps the job prints; `ios-production` is recorded as waiting on a person. An App Store Connect API key
could automate pause / resume later (optional, not built).

### Web Toybox

The web pipeline is another session's (`claude/launch-web-pipeline`, `toybox-publish.yml`): it builds on UBA,
stages, promotes and records `build-web`, `web-staging` and `web-production` itself. Its contract is a `web-v*` tag,
or a dispatch with `action=stage` and a `commit`. This workflow dispatches
`gh workflow run toybox-publish.yml -f action=stage -f commit=<the tag's sha> -f start_uba_build=true`. It has to:
a tag pushed with the run's own token would start nothing, because GitHub does not trigger workflows from
`GITHUB_TOKEN` pushes. It only stages; `/play` is that workflow's `promote`, behind `toybox-production`'s
reviewers. Until that pipeline is on bleeding-edge, GitHub refuses the dispatch (the old workflow has no such
inputs), and the web job fails alone.

## Status records

| Job | Record (`Docs/LAUNCH/STATUS_CONTRACT.md`) |
|---|---|
| release.yml | `release` |
| build-windows / build-android / build-ios | `build-windows`, `build-android`, `build-ios` (the UBA build number in `build`) |
| steam-prerelease | `windows-prerelease` |
| play-prerelease | `android-prerelease` (the version code in `build`) |
| release-promote | `windows-production`, `android-production`, `ios-production` |

All are published by a final `status` job that holds `contents: write` and no other secret, with `if: always()`, so a
stop is recorded too ("Stopped before building windows: a GitHub Environment it needs is missing"). A pull-request
rehearsal publishes with `--dry-run`.

## What is proven and what is not

| Piece | Proven how | Not proven |
|---|---|---|
| Tag rules, notes, identity | `release_notes.py self-test`; a local run on the branch | a real `v*` tag run (the session's proxy refuses tag pushes; a person pushes the first) |
| UBA client | `uba.py self-test` against a fake UBA, incl. a failed build, a timeout and a build of another commit (refused) | any call against real UBA: no Windows, Android or iOS target exists yet |
| Play client | `play.py self-test` against a fake Play API; a real openssl RS256 signature verified | any call against the real Play API |
| Environments gate | `environments.py self-test`; the pull-request rehearsal run, which stops at the missing Environments | — |
| Steam upload job | read against `Tools/Steam/upload.sh` | any run: no Steamworks app exists (A2/A4), and it is opt-in |
| iOS hand-off | — | it is a manual seam today (above) |
| Promotion | `play.py self-test`; `actionlint` | any run: the production Environments do not exist |

Never part of any run so far: an upload to any store.
