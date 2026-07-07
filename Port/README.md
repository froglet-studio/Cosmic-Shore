# Cosmic Shore — Standalone Port

A ground-up replication of Cosmic Shore onto a stack wholly owned by Froglet Inc. —
no Unity, no editor-bound tooling, no dependency that blocks a fully autonomous,
headless develop/build/test loop.

## Stack

| Concern | Choice | Why |
|---|---|---|
| Language | C# on .NET 10 LTS | Same language as all 1,321 existing first-party files — game logic ports near-verbatim ("lose nothing"). MIT-licensed, cross-platform, headless. |
| Engine | `CosmicShore.Engine` (first-party) | Replaces the Unity API surface piece by piece: math, SOAP, attributes, networking primitives, time, (later) scenes/components/rendering. |
| Tests | xunit + `dotnet test` | Fully headless verification on every iteration. |
| Rendering | Deferred — pluggable `IRenderer`, headless/null first | Simulation must never depend on a display. Backend decided in the presentation phase. |

## Build & test

```bash
export PATH=/opt/dotnet:$PATH   # or wherever the .NET 10 SDK lives
cd Port
dotnet build
dotnet test
```

## Android build (no Unity)

`src/CosmicShore.Client.Android` wraps the same `RaceWindow`/`FreestyleWindow`
presentation hosts in an Android APK: Silk.NET's SDL view (`SilkActivity`; the SDL2
natives ship inside the Silk aar) + a GLES 3.0 context (`GLES` define swaps the GL
namespace; `PlatformShader` retargets the GLSL 330 sources to ES 300), and an
`SdlTouchBackend` that pumps SDL finger state into the engine's EnhancedTouch
shim — so the ported, authentic `TouchInputStrategy` (the game's real dual-thumb
mobile scheme) drives the rig. Bluetooth gamepads use the same ported
`GamepadInputStrategy` as desktop.

The project is deliberately **not** in `CosmicShore.slnx`: plain
`dotnet build`/`dotnet test` must stay green on machines without the Android
toolchain. Build it explicitly:

```bash
dotnet workload install android              # once per SDK
export JAVA_HOME=<jdk-17-or-newer>           # JDK 21 verified
# once: provision the Android SDK (downloads platform + build-tools; USER must be set)
dotnet build src/CosmicShore.Client.Android -t:InstallAndroidDependencies \
    -p:AndroidSdkDirectory=/opt/android-sdk -p:AcceptAndroidSDKLicenses=True \
    "-p:JavaSdkDirectory=$JAVA_HOME"
# then: the APK (debug-signed, sideload-ready; latest copy committed at dist/CosmicShore-Android.apk)
dotnet build src/CosmicShore.Client.Android -c Release \
    -p:AndroidSdkDirectory=/opt/android-sdk "-p:JavaSdkDirectory=$JAVA_HOME"
adb install src/CosmicShore.Client.Android/bin/Release/net10.0-android/studio.froglet.cosmicshore.port-Signed.apk
```

Default mode is the SkimRace; launch extras pick mode/config:

```bash
adb shell am start -n studio.froglet.cosmicshore.port/.MainActivity \
    -e mode freestyle -e seed 7
```

Touch: thumbs on glass fly (the real scheme — drift comes from finger-lift
transitions, exactly as on the Unity mobile build); tap the finish screen to
rematch; three fingers down in freestyle = Tab (take/release the stick). Audio
is silent on device for now — OpenAL-soft ships no Android native and
`AudioEngine` is fail-safe by design. No trimming/AOT (the engine's reflective
lifecycle discovery forbids it — same rule as the desktop publishes).

## Layout

```
Port/
├── PORT_PLAN.md                 # master inventory, phase roadmap, live status — START HERE
├── CosmicShore.slnx
├── docs/                        # ENGINE_CORE.md, VESSEL_LAYER.md (arc survey + sequence)
├── src/
│   ├── CosmicShore.Engine/      # first-party engine layer (Unity replacement)
│   ├── CosmicShore.Data/        # ported Data layer (verbatim from Assets/_Scripts/Data)
│   ├── CosmicShore.Game/        # ported game code (mirrors Assets/_Scripts structure)
│   ├── CosmicShore.Cli/         # headless smoke/sim harness (engine boot, SOAP, sims)
│   ├── CosmicShore.Client/      # playable SkimRace/Freestyle window (Silk.NET, sprint builds)
│   └── CosmicShore.Client.Android/ # Android APK head (SDL view + GLES 3.0) — NOT in slnx, see below
├── dist/                        # playable progress-build zips (see play-latest.bat)
├── artifacts/                   # curated headless render verifications
└── tests/
    ├── CosmicShore.Tests/        # xunit suite (engine, vessel layer, enum freezes)
    └── CosmicShore.Tests.Ported/ # NUnit 3 suite (Unity EditMode tests, verbatim)
```

## Porting conventions

Ported files stay **verbatim** — same namespaces (`CosmicShore.*`), same file names,
same member names — except for these mechanical using-directive substitutions:

| Unity-era directive | Port directive |
|---|---|
| `using UnityEngine;` | `using CosmicShore.Engine;` |
| `using Unity.Netcode;` | `using CosmicShore.Engine.Networking;` |
| `using Unity.Collections;` | `using CosmicShore.Engine.Collections;` |
| `using Obvious.Soap;` | `using CosmicShore.Engine.Soap;` |
| `using Reflex.Attributes;` / `using Reflex.Core;` / `using Reflex.Injectors;` | `using CosmicShore.Engine.Injection;` |
| `using Unity.Services.Authentication;` / `using Unity.Services.Core;` | `using CosmicShore.Engine.Services;` |
| `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |
| `using TMPro;` | `using CosmicShore.Engine.UI;` (data-only TMP shim; frozen TMP numeric values) |
| `using UnityEngine.Serialization;` | (delete the line — `FormerlySerializedAs` lives in `CosmicShore.Engine`) |

Every ported enum's numeric values are frozen by tests in
`tests/CosmicShore.Tests/EnumFreezeTests.cs` — these values are wire format, save
format, and asset format simultaneously. Never change them.

See `PORT_PLAN.md` for the full inventory, the phase roadmap, and what's next.
