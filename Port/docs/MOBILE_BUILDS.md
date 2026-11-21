# Android and iOS builds (`cs-build`)

`cs-build` is the port's **File ▸ Build Settings ▸ Build**. It produces a phone build of the
game from the Unity project on disk, the way Unity does: it decides what ships, packs it, and
hands it to the platform toolchain.

```
cs-build content [--out DIR] [--pack FILE]   player data only (what would ship)
cs-build android [--out X.apk|X.aab] [--debug] [--abi arm64|arm64,x64] [--id PACKAGE]
                 [--keystore FILE --alias NAME]      passwords in CS_KEYSTORE_PASS / CS_KEY_PASS
cs-build ios     [--out DIR] [--debug] [--id BUNDLE]
```

Run it from inside a clone (`dotnet run --project Port/src/CosmicShore.Build -- android`), or
double-click `Port/build-android.bat` on Windows. Outputs go to `Builds/` (git-ignored).

## 1. What ships — Unity's inclusion rules

| | Unity | `cs-build` |
|---|---|---|
| Roots | enabled scenes in Build Settings, every asset under a `Resources/` folder, preloaded assets | the same three, read from `ProjectSettings/` |
| Edges | every reference, transitively | every guid in an asset or its `.meta` (importer remaps count) |
| Editor folders | never ship | never ship |
| Scripts | compiled; no source in the player | `.meta` only; namespace + class recorded in `ScriptTypes.tsv` |
| Settings | baked into the player | `ProjectSettings/` copied (build scenes, time, physics, graphics) |
| Audio | FMOD banks for the platform | the banks the FMOD Studio project built (`sourceBankPath`) |

The player data is laid out as a project (`Assets/`, `ProjectSettings/`), so the content runtime
reads it exactly as it reads the editable project. Today: **35 build scenes, 96 Resources assets,
2 preloaded → 3,079 reachable → 2,260 files, 467 MB, 819 scripts, 3 banks.**

Verified: booting `Menu_Main`, `MinigameRampage` and `MinigameSkimRace` from the packaged data
gives the same object counts and the same log as booting them from the project.

## 2. Player Settings → the package

| Player Settings | Android | iOS |
|---|---|---|
| `productName` | app label | `CFBundleDisplayName` |
| `applicationIdentifier.Android` / `.iPhone` | package name | bundle id |
| `bundleVersion` | `versionName` | `CFBundleShortVersionString` |
| `AndroidBundleVersionCode` / `buildNumber.iPhone` | `versionCode` | `CFBundleVersion` |
| default icon (`m_BuildTargetIcons`) | launcher icon | (not yet) |

Today's settings give `com.FrogletGames.TailGlider`, 0.2.0 (11). `--id` overrides the package.

## 3. Android

`cs-build android`:

1. installs the .NET `android` workload if missing, and the Android SDK + JDK on first use
   (or uses `ANDROID_HOME` / `JAVA_HOME`) — the job Unity Hub's Android module does;
2. writes the player data and packs it into `data.pak` (+ `data.hash`);
3. builds `src/CosmicShore.Mobile` for Android and signs it — with the debug key, like a Unity
   development build, unless `--keystore` is given. `.aab` output is for the Play Store.

On the phone, the first launch of each new build unpacks `data.pak` into app storage (about 7 s
for 433 MB on a desktop; longer on a phone), then boots build scene 0. Unchanged builds skip
straight to the game.

Package details: arm64 by default, minSdk 28, OpenGL ES 3.0 required, landscape, JIT (the game
reflects over its own types, so nothing is trimmed — Unity's "Strip Engine Code: off").

## 4. iOS

Apple's toolchain only runs on macOS, so — exactly like Unity's Xcode export — the build has two
halves. On any machine `cs-build ios` writes `Builds/iOS/` (player data + `build-ios.sh`). On a
Mac, `cs-build ios` (or `build-ios.sh`) installs the .NET `ios` workload and builds the signed
`.ipa`. Signing: `CS_IOS_CODESIGN_KEY` and `CS_IOS_PROVISIONING_PROFILE`, else Xcode's default.

The player data ships unpacked inside the app bundle (read-only, as Unity's `Data/` folder), so
nothing is extracted on launch. FMOD is linked statically from the integration's iOS library.

## 5. What changed in the engine to run on a phone

| Area | Desktop | Phone |
|---|---|---|
| Window | GLFW window, GL 3.3 core | SDL full-screen view, **OpenGL ES 3.0** (`MobileHost`) |
| Shaders | GLSL 3.30 | translated at the one compile point (`GlCaps.Translate`): `#version 300 es` + precision |
| Per-instance block | texture buffer (`samplerBuffer`) | 1024-wide RGBA32F 2D texture read with `texelFetch` (`CS_EXT_2D`) |
| HDR targets | RGBA16F / R11F_G11F_B10F | the same when the GPU can render floats, else RGBA16F, else RGBA8 |
| Draws | `DrawElementsInstancedBaseVertex` (base 0) | `DrawElementsInstanced` (identical: base vertex is always 0) |
| Input | keyboard, mouse, pads | touch → the Input System's `Touchscreen` + EnhancedTouch list (`TouchFeed`); `SystemInfo.deviceType = Handheld`, so the game picks its own TouchInputStrategy |
| Data | the project folder | packaged player data (`COSMIC_SHORE_PROJECT`) |
| Saves | LocalAppData | app sandbox (`Application.persistentDataPathOverride`) |
| FMOD | `fmodstudio.dll` / `.so` beside the app | `libfmodstudio.so` in the APK (+ `org.fmod.FMOD.init`); iOS static link |

## 6. Verification — what was and was not checked

Checked in this environment:

- **The APK builds**: `cs-build android` → `Builds/Android/CosmicShore.apk`, 439 MB, signed
  (`apksigner verify`), label/icon/package/version from Player Settings, `data.pak` and
  `libSDL2.so` inside.
- **Every renderer shader compiles as GLSL ES 3.00** under the Khronos reference compiler
  (`GlslEsTranslationTests`).
- **The GL ES render path draws the game**: the desktop player on an OpenGL ES 3.0 context
  (`COSMIC_SHORE_GLES=1`, Mesa) boots through the auth screens into Menu_Main and draws
  ~5,800 prism instances through the ES fallbacks, matching the GL 3.3 render.
- **The phone host runs**: `MobileHost` + `PlayerDataInstaller` + the SDL view + `TouchBridge`,
  compiled as a desktop program and fed the APK's own `data.pak`, unpacks, boots Bootstrap →
  Authentication on GL ES with 0 errors.
- Touch phases (`TouchFeedTests`) and the inclusion rules (`PlayerDataBuilderTests`).

**Not checked:** running the APK on a phone or emulator (none available here), audio on Android
(this clone's FMOD Android libraries are Git LFS pointers; with `git lfs pull` they are packaged),
anything on iOS (needs a Mac). First things to look at on a device: `adb logcat -s DOTNET` for
the boot log, frame time on the device's GPU, and touch flight.

Desktop check of the ES path: `COSMIC_SHORE_GLES=1 CosmicShore` (add
`COSMIC_SHORE_DUMP_SHADERS=DIR` to write every translated shader stage).
