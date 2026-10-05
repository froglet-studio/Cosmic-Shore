# Froglet Engine Launcher

One `.exe` for anyone who should test the engine: pick a branch, press **START**. The launcher
fetches that branch, builds it from its own source and runs it. It also builds phone apps, edits
the engine's Project Settings, and has Claude Code built in.

Source: `Port/src/CosmicShore.Launcher` (C#, Dear ImGui on our own Silk.NET window).

## Getting the .exe

- Ready-made: `Port/dist/FrogletLauncher-Windows.zip` (one file, nothing to install).
- Rebuild it: double-click `Port\build-launcher.bat`.

| Needed | Why | If missing |
|---|---|---|
| git (GitHub Desktop is enough) | fetches the branch | install GitHub Desktop |
| .NET 10 SDK | compiles the branch | installed automatically on the first START |
| ~3 GB of disk | project files + build output | - |

## Pages (left rail)

| Page | What it is for |
|---|---|
| **PLAY** | Branch, **START**, and four quick toggles (fullscreen, audio, online, pull first). The small buttons beside them update without playing and open the workspace folder. |
| **BUILD** | One card per phone platform. Each card has one choice and one button; everything else is under *Options*. |
| **PROJECT** | The engine's own Project Settings (below). |
| **CLAUDE** | Claude Code, inside the launcher (below). |
| **SETTINGS** | Folded sections: Game, Source, Claude, Advanced, Toolchain, About. |
| **CONSOLE** | Every command the launcher ran and its output. COPY for a bug report. |

![PLAY](architecture/launcher_play.png)

The two dots at the bottom of the rail are git and .NET (hover for versions). The bar at the
bottom shows what is happening, a progress bar and CANCEL.

## BUILD

![BUILD](architecture/launcher_build.png)

**Android** - APK (install directly) or AAB (Play Store). Options: CPU, keystore, debug.

**iOS** - three ways, picked with the switch on the card:

| Mode | Needs | Produces |
|---|---|---|
| **GITHUB** (default without a Mac) | a GitHub sign-in (GitHub Desktop is enough) | an unsigned `.ipa`, built on GitHub's free Mac and downloaded to `Builds/iOS` |
| **XCODE** | nothing | `Builds/iOS/CosmicShore.xcodeproj` + player data, like Unity's iOS export |
| **THIS MAC** | a Mac with Xcode | a signed `.ipa` |

GITHUB mode runs `.github/workflows/froglet-engine-ios.yml` for the selected branch, shows each
step of the Mac build in the status bar (15-25 minutes), then downloads the `.ipa`. Install it with
**Sideloadly** and a free Apple ID - the same Windows steps as the Unity build
(`Docs/IOS_BUILD.md` section 2, Path A, step 4). OPEN RUN shows the run on GitHub.

Token: GitHub Desktop's sign-in works as-is. With a typed token (SETTINGS > Source) it needs
**Actions: read & write** (fine-grained) or the `workflow` scope. Until the workflow is on the
default branch, the launcher starts it by committing `Port/ios-build-request.txt` to your feature
branch, which needs **Contents: read & write**.

## PROJECT - the engine's Project Settings

![PROJECT](architecture/launcher_project_1.png)

Unity keeps Player Settings and Build Settings in `ProjectSettings/`. The engine keeps its own in
**`Port/ProjectSettings/FrogletProject.json`**, so it never edits Unity's files. Every field is an
override: leave it empty and Unity's value applies (shown greyed in the field).

| Tab | Holds | Read by |
|---|---|---|
| **PLAYER** | company, product name, version, Android package + version code, iOS bundle id + build number | `cs-build` (APK/AAB/IPA/Xcode) |
| **SCENES** | Scenes In Build: enable, reorder (scene 0 boots). USE UNITY'S drops the override | `cs-build`, the game's scene list, the launcher's *Start in* |
| **QUALITY** | anti-aliasing (MSAA), render scale, texture filtering, vsync, frame cap | the player at startup |

Edits save as you make them. Commit the file to share them with the branch.

## CLAUDE

![CLAUDE](architecture/launcher_claude.png)

Runs the official **Claude Code** CLI in the workspace, so it reads (and, if allowed, edits) the
branch you are building. If it is not installed, the page offers INSTALL (Anthropic's installer,
no Node needed).

- **ASK** - reads and plans, changes nothing.
- **EDIT** - may edit files in the workspace.
- **AUTO** - may also run commands.

It is connected to the engine itself (the `froglet-engine` tools, `Port/CLAUDE.md`): Claude can
build the engine, start the game, take screenshots, click and type, and read or change live
objects - so "start the game and check the score panel updates" is a request it can carry out
and show you. Those tools never edit files, so ASK may use them too.

NEW starts a fresh conversation; otherwise each message continues the same session. The API key
(SETTINGS > Claude) is optional: without one, Claude Code uses its own sign-in. A key is stored
only on this PC and passed only to the `claude` process. Rebuild with START to try what it changed.

## How START works

```
git fetch <branch>  ->  FMOD library from Git LFS  ->  dotnet build Port/src/CosmicShore.Player
                    ->  CosmicShore.exe --size ... [--fullscreen] [--scene ...]
```

Settings live in `%LOCALAPPDATA%\FrogletEngine\launcher.json`. The launcher never writes to your
own clone: *Beside my clone* uses a git worktree next to it.

## Testing the launcher itself

`FrogletLauncher --page build --screenshot out.png --frames 40` renders a page and exits
(`--page project:1` opens a Project tab). `--auto play|update|android|ios` presses the button on
its own and echoes the log; `--auto "chat:<message>"` sends one chat message.
