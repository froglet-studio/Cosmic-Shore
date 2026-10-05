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
| **SETTINGS** | Folded sections: Game, Source, Look, Claude, Advanced, Toolchain, About (versions). |
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

## Updating the launcher (and keeping old versions)

The launcher never updates itself. When the selected branch has a newer launcher, an **UPDATE**
badge pulses on the rail; click it to see what changed, then **UPDATE NOW** or **NOT NOW**. The
new version is built from that branch's own source (no zip to download), the screen shows the
build, and the launcher restarts into it.

SETTINGS > ABOUT shows this launcher's version and lets you **INSTALL** any branch, tag or
commit. Every version you install (and the one you had before) is kept, and **USE** switches
between them, so testers can each run a different launcher. The zip in `dist/` is only for the
first install.

## LOOK

SETTINGS > LOOK: eight backgrounds (synthwave, warp, starfield, nebula, aurora, the HyperSea
lattice, plain, or your own picture), six accent themes (Cosmic, and the Jade, Ruby, Gold, Ice
and Mono domain colours), motion speed (down to still), dim, and the intro and page animations.

## Play-session reports

Every game started from PLAY writes a report when it closes or crashes: scenes and time in each,
frame-time percentiles, every distinct error, warning and exception with its count, and the
crash, branch and commit. They are kept in `%LOCALAPPDATA%\FrogletEngine\sessions` (the last
40). The CLAUDE page's LAST SESSION hands the newest one to Claude to analyse.

## CLAUDE

![CLAUDE](architecture/launcher_claude.png)

The page is the **Froglet Engine agent**. It works on `Port/`, reads the game in `Assets/` as
input, and is refused any edit to `Assets/`, `Packages/` or `ProjectSettings/` in every mode.
The layout follows Claude Code: a bullet per message and tool call, each tool's result folded
under it, to-do lists as checklists, the game's screenshots inline, and a status line (model,
mode, context size, cost, and whether it runs on your plan or an API key).

- **Model** and **effort** on the bar (default / fable / opus / sonnet / haiku; low to max).
- **PLAN**: Claude investigates (it may build, test and run the game) and answers with a plan
  card. **APPROVE + EDIT** or **APPROVE + AUTO** lets it build the plan; typing keeps planning.
  **EDIT** edits engine files directly; **AUTO** may also run any command.
- **Chips**: RUN TESTS, SMOKE TEST (boots the game headless and reports every error), LAST
  SESSION, PARITY CHECK (compares one system with the Unity game, in plan mode).
- **Commands**: `/clear /plan /edit /auto /model NAME /effort LEVEL /test /smoke /session`. Up
  arrow recalls earlier messages.
- **Voice** (Windows): the microphone dictates into the box; the speaker reads replies aloud.

Runs the official **Claude Code** CLI in the workspace, so it reads (and, if allowed, edits) the
branch you are building. If it is not installed, the page offers INSTALL: it downloads Claude Code
(~250 MB, with progress), checks it against Anthropic's published checksum and sets it up. No Node
needed.

**Which account pays.** With a Claude Pro or Max plan, leave the API key empty and press
**SIGN IN** (SETTINGS > Claude, or on the CLAUDE page): a window opens for the browser sign-in,
and chat then runs on your plan. An **API key** is only for pay-as-you-go billing from
console.anthropic.com; when one is set it is used *instead of* the plan. Keys and tokens are shown
as dots; SHOW reveals them to check a paste.

- **ASK** - reads and plans, changes nothing.
- **EDIT** - may edit files in the workspace.
- **AUTO** - may also run commands.

It is connected to the engine itself (the `froglet-engine` tools, `Port/CLAUDE.md`): Claude can
build the engine, start the game, take screenshots, click and type, and read or change live
objects - so "start the game and check the score panel updates" is a request it can carry out
and show you. Those tools never edit files, so ASK may use them too.

NEW starts a fresh conversation; otherwise each message continues the same session. A key is
stored only on this PC and passed only to the `claude` process. Rebuild with START to try what it changed.

## How START works

```
git fetch <branch>  ->  FMOD library from Git LFS  ->  dotnet build Port/src/CosmicShore.Player
                    ->  CosmicShore.exe --size ... [--fullscreen] [--scene ...]
```

Settings live in `%LOCALAPPDATA%\FrogletEngine\launcher.json`. The launcher never writes to your
own clone: *Beside my clone* uses a git worktree next to it.

## Testing the launcher itself

`FrogletLauncher --page build --screenshot out.png --frames 40` renders a page and exits
(`--page project:1` opens a Project tab, `--page options:look` one settings section).
`--auto launcher-update:REV` / `launcher-use:SHORT` exercise the version flow. `--auto play|update|android|ios` presses the button on
its own and echoes the log; `--auto "chat:<message>"` sends one chat message.
