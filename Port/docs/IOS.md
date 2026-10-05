# iOS builds of the Froglet Engine

Apple only compiles iPhone apps on macOS. The engine gives three ways around that, all from the
same source the Windows player builds from (no Unity export involved):

| Way | Command | Output |
|---|---|---|
| **No Mac: GitHub's Mac** | Launcher > BUILD > iOS > GITHUB, or Actions > *Froglet Engine iOS ipa* > Run workflow (type the branch) | unsigned `CosmicShore-unsigned.ipa` (artifact `CosmicShore-ios-ipa`, kept 3 days) |
| **Xcode project** | `cs-build ios --xcode` (any OS) | `Builds/iOS/CosmicShore.xcodeproj` + `PlayerData/` |
| **On a Mac** | `cs-build ios` (signed) / `cs-build ios --unsigned` | `Builds/iOS/*.ipa` |

`cs-build` is `dotnet run --project Port/src/CosmicShore.Build -- ...`.

## Installing the unsigned .ipa (Windows, no Mac)

Sideloadly signs it with a free Apple ID; the confirmed Windows steps (iTunes and iCloud
installers, trusting the phone) are in `Docs/IOS_BUILD.md` section 2, Path A, step 4. A free Apple ID
signature lasts 7 days; re-sideload to renew.

## The Xcode project

`CosmicShore.xcodeproj` has one app target whose build phase runs `build-dotnet.sh`, which
compiles the engine with the .NET iOS SDK and places the app into Xcode's product, so Xcode does
the signing (pick your Team under Signing & Capabilities) and Run / Archive work as usual. The
Mac needs Xcode and the .NET 10 SDK (`dotnet workload install ios` is run for you). The project
points at the repository it was exported from; set `COSMIC_SHORE_REPO` to use another clone.

## Identifiers

Bundle id, version and build number come from Unity's Player Settings unless the engine's
Project Settings (`Port/ProjectSettings/FrogletProject.json`, launcher PROJECT page) override
them. iOS defaults to the test id `com.FrogletGames.CosmicShore.dev` (`Docs/IOS_BUILD.md` section 1).
