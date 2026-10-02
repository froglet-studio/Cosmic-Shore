# The Android player (no Unity)

`src/CosmicShore.Player.Android` is the port's **player** on Android: the real game
(`CosmicShore.Live` — the unmodified `Assets/_Scripts` compiled against the port engine) booting
from the project's own scenes and content, drawn through an OpenGL ES 3.2 build of the port's
renderer. It supersedes the July `CosmicShore.Client.Android` head (branch
`claude/android-build-no-unity-gh65ai`), which wrapped the hand-ported SkimRace harness rather
than the game.

| | |
|---|---|
| Package id | `studio.froglet.cosmicshore.engine` — distinct from the Unity Android build (`com.FrogletGames.TailGlider`) and the July port APK (`studio.froglet.cosmicshore.port`), so all three install side by side |
| Label | Cosmic Shore (Engine) |
| ABI / OS | arm64-v8a, Android 7.0+ (API 24), **OpenGL ES 3.2 required** (declared in the manifest) |
| Runtime | .NET 10 Android (Mono JIT; no trimming, no AOT — the game and engine resolve types by reflection) |
| Signing | the SDK's debug keystore of the machine that built it — sideload only |

## Build

```bash
export PATH=/opt/dotnet:$PATH              # .NET 10 SDK with `dotnet workload install android`
python3 Port/tools/fetch_native.py --platform android-arm64   # FMOD's Android runtime, out of Git LFS
cd Port
dotnet publish src/CosmicShore.Player.Android -c Release -p:AndroidSdkDirectory=/opt/android-sdk
# → src/CosmicShore.Player.Android/bin/Release/net10.0-android/publish/studio.froglet.cosmicshore.engine-Signed.apk
adb install -r <that apk>
adb logcat -s CosmicShore                  # the player's console
```

## What is Android's own

Everything else is the desktop player's code, compiled verbatim (`../CosmicShore.Player/*.cs`
minus `Program.cs` and the headless training host).

1. **The renderer on GLES.** `CosmicShore.Render.Gles` compiles the same sources as
   `CosmicShore.Render` with `GLES` defined: each file swaps its Silk.NET binding
   (`Silk.NET.OpenGL` → `Silk.NET.OpenGLES`) and `GlProgram.ToEs` rewrites every shader's
   `#version 330 core` line to `#version 320 es` plus default precisions. ES **3.2** rather than
   3.0 because the scene shader reads its per-instance clock block from a texture buffer
   (`samplerBuffer`), and base-vertex instancing and float colour targets are core there.
   Every shader the renderer ships was validated with `glslangValidator` as GLSL ES 3.20; it
   found exactly one desktop-only construct (an int literal assigned to a float in the HyperSea
   skybox), fixed at the source.
2. **The content.** The player reads the project off a filesystem; an APK's assets are zip
   entries. `tools/pack_android_content.py` packs what the player reads (~12k files, ~100 MB:
   every `.meta`, the build-reachable asset set, every shader/script source, ProjectSettings and
   the FMOD bank build) into one asset; `MainActivity.EnsureContent` unpacks it into private
   storage on first launch and points `COSMIC_SHORE_PROJECT` at it. A version stamp makes later
   launches one file read. Audio clips and video are left out — the port plays sound through FMOD
   banks and never opens a clip (verified with strace on a desktop boot).
3. **FMOD.** `libfmod.so` + `libfmodstudio.so` ship as APK native libraries and `fmod.jar` is
   dexed in; `org.fmod.FMOD.init(Context)` runs in `OnCreate` (the UI thread, where the app class
   loader can see it) before the Studio runtime starts. The Desktop bank build is used — FMOD
   banks are platform-neutral.
4. **Input.** `AndroidTouchBridge` mirrors SDL's fingers into `EnhancedTouch.Touch.activeTouches`
   (what the game's `TouchInputStrategy` reads — the authentic dual-thumb scheme) and adds a
   `Touchscreen` device. SDL's touch→mouse synthesis drives the same `Mouse` the desktop bridge
   feeds, so uGUI taps work with no second pointer path. `SoftKeyboardBridge` raises Android's
   keyboard while an `InputField`/`TMP_InputField` holds focus (the age gate and the username
   prompt need it). A Bluetooth gamepad goes through the desktop bridge unchanged.
5. **Platform identity.** `SystemInfo.deviceType = Handheld` (the game picks its touch strategy
   off this), `Application.platform = Android`, the real `Screen.dpi`.

## Verifying without a device

`src/CosmicShore.Player.Gles` is the desktop player on a desktop OpenGL ES 3.2 context — the
mobile render path on a workstation, or headlessly under `xvfb-run` with Mesa. Run it with
`COSMIC_SHORE_PROJECT` pointed at an unpacked content pack to test exactly what the APK carries:

```bash
python3 Port/tools/pack_android_content.py /tmp/content.zip && mkdir /tmp/pk && unzip -q /tmp/content.zip -d /tmp/pk
dotnet build Port/src/CosmicShore.Player.Gles -c Release -o /tmp/glesbin
COSMIC_SHORE_PROJECT=/tmp/pk xvfb-run -a dotnet /tmp/glesbin/CosmicShore.dll --size 1280x720 --shot 2500:menu.png --frames 2500
```

Verified this way (2026-10-02, Mesa llvmpipe, GLES 3.2): Bootstrap → age gate → consent →
username → Menu_Main (Garland, prisms, HUD), the arcade grid, the Bloomrush launch modal with its
live preview, and the Bloomrush scene loading and rendering — all from the content pack alone,
0 errors (the one exception, `Squad..ctor`, is the faithful one the progress report documents).

## Known gaps

- **Not yet run on a phone.** Everything above was proven on desktop GLES and in the build; the
  Android-only pieces (SDL view, FMOD Java init, touch, soft keyboard, content unpack) follow the
  July head's device-verified shapes but have not been seen on hardware.
- **Startup is long and dark.** The first launch unpacks ~100 MB; every launch then boots the
  content bridge (Bootstrap takes ~8 s on a desktop JIT, expect several times that on a phone)
  before the first frame. A toast says it is loading; there is no splash yet.
- **Performance.** Mono JIT, no AOT, desktop-scale worlds. Menu_Main is ~4k instanced prisms.
- **16 KB pages.** `libSDL2.so`/`libmain.so` (Silk's aar) are 4 KB-aligned; Android 15+ devices
  running 16 KB pages need the compatibility mode.
- **Networking** is the port's LAN directory transport only, as on desktop.
