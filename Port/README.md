# Cosmic Shore — Standalone Port

A ground-up replication of Cosmic Shore onto a stack wholly owned by Froglet Inc. —
no Unity, no editor-bound tooling, no dependency that blocks a fully autonomous,
headless develop/build/test loop.

**Just want to play a branch?** Unzip `dist/FrogletLauncher-Windows.zip` and run
`FrogletLauncher.exe`: pick a branch, press START GAME. See `docs/LAUNCHER.md`.

**Working with Claude Code?** `CLAUDE.md` in this folder: the engine's MCP server lets an agent
build, run, see and drive the game.

**Start here:** `docs/ARCHITECTURE.md` — how the whole port fits together, with
diagrams and annotated screenshots of every screen and control.

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

## Try it (Windows)

Run `FrogletLauncher.exe` (unzip `dist/FrogletLauncher-Windows.zip`): pick a branch, press
START GAME. It fetches that branch into its own workspace (or a worktree beside your clone),
builds it from source and starts the game. Your own checkout is never touched. `docs/LAUNCHER.md`.

## Run the real game

`CosmicShore.Player` runs the project's own `Assets/_Scripts` (compiled by `CosmicShore.Live`)
against the engine, loading the real scenes, prefabs, materials, shaders, meshes, fonts and FMOD
banks straight from `Assets/`:

```bash
python3 Port/tools/fetch_native.py          # once: the FMOD Studio runtime out of Git LFS
cd Port
dotnet run -c Release --project src/CosmicShore.Player
```

`-c Release` is the customer-shaped player (no `DEVELOPMENT_BUILD` overlay, stripped logging);
Debug is a Unity development build. Sound comes from the vendor FMOD Studio runtime the project
ships and the banks its FMOD Studio project built (`Cosmic Shore/Build/Desktop`).
`COSMIC_SHORE_AUDIO=off` silences it; `COSMIC_SHORE_AUDIO=wav:PATH` records the mix to a WAV.
On Windows, a clone made with git-lfs already has `fmodstudio.dll`; otherwise run
`python tools\fetch_native.py --platform win-x64`. Progress and remaining gaps:
`docs/PROGRESS_2026-09-29.md`.

## Edit content without Unity

`cs-asset` writes the project's scenes, prefabs and assets: set any field, create and delete
GameObjects (with their hierarchy, nested prefabs and references), add and remove components
(project scripts, built-ins, uGUI/TMP), and edit through placed prefabs (overrides, added and
removed objects, placing new prefabs, applying an instance's changes to its prefab) the way the Unity Editor writes them. A file is written back
byte-identical except for what the edit changed; that holds on all 2030 YAML files in
`Assets/`. Close the scene in Unity first. Details, measurements and what's not built yet:
`docs/AUTHORING.md`.

```bash
cd Port && dotnet build src/CosmicShore.AssetTool
src/CosmicShore.AssetTool/bin/Debug/net10.0/cs-asset list ../Assets/_Scenes/Authentication.unity
```

## Build for Android and iOS

`cs-build` makes a player build the way Unity's Build Settings does: only what the game ships
(enabled build scenes + `Resources/` + preloaded assets, followed through every reference;
nothing under `Editor/`, no script source), named and versioned from Player Settings, then
handed to the platform toolchain. Details and what is verified: `docs/MOBILE_BUILDS.md`.

```bash
Port/build-android.bat                         # Windows: double-click → Builds/Android/CosmicShore.apk
dotnet run --project Port/src/CosmicShore.Build -- android   # any OS
dotnet run --project Port/src/CosmicShore.Build -- ios       # on a Mac: builds the .ipa; elsewhere: exports Builds/iOS
```

## Layout

```
Port/
├── PORT_PLAN.md                 # master inventory, phase roadmap, live status — START HERE
├── CosmicShore.slnx
├── docs/                        # ARCHITECTURE.md (start here), ENGINE_CORE.md, VESSEL_LAYER.md
├── src/
│   ├── CosmicShore.Engine/      # first-party engine layer (Unity replacement)
│   ├── CosmicShore.Data/        # ported Data layer (verbatim from Assets/_Scripts/Data)
│   ├── CosmicShore.Game/        # ported game code (mirrors Assets/_Scripts structure)
│   ├── CosmicShore.AssetTool/   # cs-asset: edit/create/delete in scenes & prefabs (docs/AUTHORING.md)
│   ├── CosmicShore.Build/       # cs-build: Android/iOS player builds (docs/MOBILE_BUILDS.md)
│   ├── CosmicShore.Launcher/    # FrogletLauncher.exe: branch > fetch > build > play, phone builds (docs/LAUNCHER.md)
│   ├── CosmicShore.Mobile/      # the phone player (Android activity / iOS app around PlayerWindow)
│   ├── CosmicShore.Cli/         # headless smoke/sim harness (engine boot, SOAP, sims)
│   └── CosmicShore.Client/      # playable SkimRace window (Silk.NET, sprint builds)
├── dist/                        # FrogletLauncher-Windows.zip (the one file to give a tester)
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
| `using Unity.Services.Friends;` / `.Exceptions` / `.Models` / `.Notifications` | `using CosmicShore.Engine.Services.Friends;` (one flat placeholder namespace) |
| `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |
| `using TMPro;` | `using CosmicShore.Engine.UI;` (data-only TMP shim; frozen TMP numeric values) |
| `using UnityEngine.Serialization;` | (delete the line — `FormerlySerializedAs` lives in `CosmicShore.Engine`) |

Every ported enum's numeric values are frozen by tests in
`tests/CosmicShore.Tests/EnumFreezeTests.cs` — these values are wire format, save
format, and asset format simultaneously. Never change them.

See `PORT_PLAN.md` for the full inventory, the phase roadmap, and what's next.
