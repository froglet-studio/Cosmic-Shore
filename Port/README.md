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
│   └── CosmicShore.Client/      # playable SkimRace window (Silk.NET, sprint builds)
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
| `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |

Every ported enum's numeric values are frozen by tests in
`tests/CosmicShore.Tests/EnumFreezeTests.cs` — these values are wire format, save
format, and asset format simultaneously. Never change them.

See `PORT_PLAN.md` for the full inventory, the phase roadmap, and what's next.
