# The build stamp: every build knows who it is, and which UGS environment it talks to

Launch blockers **#1** (release builds talk to UGS `production`) and **#5** (every build is identifiable) in
`Docs/LAUNCH/READINESS.md` §10. Owner: Shombith (UGS and the build pipelines).

## What it is

While Unity builds a player, `BuildStampProcessor` (`Assets/_Scripts/Editor/Build/BuildStampProcessor.cs`, an
`IPreprocessBuildWithReport` at callbackOrder -1000) writes `Assets/Resources/Generated/build_stamp.json` into the
build, and `BuildStampCleanup` (postprocess, callbackOrder 10000) deletes it and puts Player Settings back. The
folder and its `.meta` are git-ignored, so a build never leaves `Assets/` or `ProjectSettings.asset` modified. The
scripted pipelines (`CosmicShoreBuildPipeline` Windows and Android) also clean up in their `finally`, because a
failed build never reaches the postprocess; an interactive editor cleans up on its next tick.

At runtime `BuildStamp.Current` (`Assets/_Scripts/System/BuildStamp.cs`) reads it once:

| Field | From | Example |
|---|---|---|
| `version` | `-buildVersion` / `CS_BUILD_VERSION`, else Player Settings `bundleVersion` | `0.3.0` |
| `buildNumber` | `-buildNumber` / `CS_BUILD_NUMBER`; Android: the versionCode; iOS: Player Settings build number; else `local` | `42` |
| `commit` | `-buildCommit`, else `GITHUB_SHA`, else `git rev-parse HEAD`, else `unknown` | full sha |
| `gitRef` | `-buildRef` / `CS_BUILD_REF`, else `GITHUB_REF_NAME`, else the tag on HEAD, else the branch | `launch/v1` |
| `channel` | `-releaseChannel` / `CS_RELEASE_CHANNEL`, else `local` | `playtest` |
| `ugsEnvironment` | the rule below | `production` |
| `builtAtUtc`, `development` | the build | |

A command-line argument beats its environment variable. In the Editor (and a player that somehow lacks the stamp)
`BuildStamp` answers an Editor stamp: commit and channel `editor`, environment `development`. A release player
without its stamp logs a warning; it never falls back to production.

## The UGS environment rule

1. An explicit value wins: `-ugsEnvironment` or `CS_UGS_ENVIRONMENT` (an Editor run reads the variable too).
2. Otherwise a **development** build and the Editor use **`development`**.
3. Otherwise (a release player) **`production`**.

Any value other than `development` or `production` (any case) **fails the build** with a `BuildFailedException`
naming the bad value. `AuthenticationServiceFacade.InitializeCore` initializes Unity Services with
`new InitializationOptions().SetEnvironmentName(BuildStamp.Current.UgsEnvironment)` and logs the choice once on the
`Boot` channel. It is the only `UnityServices.InitializeAsync` in the game; Friends, Party, Cloud Save, Vivox and
Analytics all ride on it. `ProjectSettings/Packages/com.unity.services.core/Settings.json` (`development`) is
overridden by that explicit name in every player and Editor run.

An Android build number must be a positive whole number (it becomes the versionCode), or the build fails.
`-buildVersionCode` on the Android pipeline still wins over `-buildNumber`.

## Where it shows

- **Settings > General**, the version line: an authored template on `GameSettingsPanelController.versionLine`,
  default `Version {version} ({build}) {commit} {env}`. Tokens: `{version} {build} {commit}` (8 characters of the
  sha) `{ref} {channel} {env}`. Empty shows nothing.
- **Crash reports** (`CrashReportingService`, consent-gated): `build_version`, `build_number`, `build_commit`,
  `release_channel`, `ugs_environment`, `development_build`.
- **Analytics person properties** (PostHog only, so no UGS Event Manager rows): `build_version`, `build_number`,
  `build_commit`, `release_channel`, `ugs_environment` (`Docs/Analytics/EVENT_SCHEMA.json`).
- **`build_manifest.txt`** next to a Windows player now records the same fields.

## What each pipeline passes

| Pipeline | Must pass | Notes |
|---|---|---|
| Windows (Steam Playtest) | `-buildVersion`, `-buildNumber` (the run number), `-releaseChannel playtest` (or `internal`) | `Tools/Build/build_windows.sh --version --build-number --channel [--ugs-environment]`; the commit is taken from git. A release build is `production` unless told otherwise; `--dev` is `development`. |
| Android (Play internal track) | `-buildVersion`, `-releaseChannel internal` | The build number is the versionCode (`-buildVersionCode`, or `-buildNumber` / `CS_BUILD_NUMBER`, else minutes since 2026-01-01). |
| iOS (TestFlight) | `CS_BUILD_VERSION`, `CS_BUILD_NUMBER`, `CS_RELEASE_CHANNEL testflight` at the Unity export | `ios-testflight.yml` rewrites `CFBundleVersion` to `<base>.<run>` after the export, so the stamp shows the export's number unless the export is given the same `CS_BUILD_NUMBER`. |
| Unity Build Automation / GameCI | the `CS_*` environment variables (UBA: the target's environment variables) or `customParameters: -buildNumber ... -releaseChannel ...` | `GITHUB_SHA` and `GITHUB_REF_NAME` are read on GitHub runners. |
| Web Toybox | `-buildVersion`, `CS_RELEASE_CHANNEL web` | No UGS in the toybox; the stamp still names the build. |

Never pass a secret in any of these: every value is shown on screen and in crash reports.

## Amoebius

The engine compiles `Assets/_Scripts` live, so its `UnityServices` stand-in
(`Port/src/CosmicShore.Engine/Services/UnityServices.cs`) gained `InitializationOptions`, the `SetEnvironmentName`
extension and the matching `InitializeAsync` overload, recording the name and nothing more. Amoebius is not a launch
platform; this only keeps its compile whole.
