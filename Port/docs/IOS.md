# iOS builds of Amoebius

Apple only compiles iPhone apps on macOS. The engine builds iOS two ways, both from the same
source the Windows player builds from (no Unity export involved):

| Way | Command | Output |
|---|---|---|
| **Xcode project** | `cs-build ios --xcode` (any OS), or launcher BUILD > iOS > XCODE | `Builds/iOS/CosmicShore.xcodeproj` + `PlayerData/` |
| **On a Mac** | `cs-build ios`, or launcher BUILD > iOS > THIS MAC | a signed `Builds/iOS/*.ipa` |

`cs-build` is `dotnet run --project Port/src/CosmicShore.Build -- ...`.

The unsigned `.ipa` for Sideloadly (the GitHub-Mac workflow, `cs-build ios --unsigned` and the
launcher's GITHUB mode) was retired on 2026-10-13, with the game's free-Apple-ID sideload path:
TestFlight is the only route onto a phone (`Docs/IOS_BUILD.md`).

## The Xcode project

`CosmicShore.xcodeproj` has one app target whose build phase runs `build-dotnet.sh`, which
compiles the engine with the .NET iOS SDK and places the app into Xcode's product, so Xcode does
the signing (pick your Team under Signing & Capabilities) and Run / Archive work as usual. The
Mac needs Xcode and the .NET 10 SDK (`dotnet workload install ios` is run for you). The project
points at the repository it was exported from; set `COSMIC_SHORE_REPO` to use another clone.

## Identifiers

Bundle id, version and build number come from Unity's Player Settings unless the engine's
Project Settings (`Port/ProjectSettings/AmoebiusProject.json`, launcher PROJECT page) override
them. iOS Player Settings carry the production id `com.FrogletGames.CosmicShore` (`Docs/IOS_BUILD.md` section 1).
