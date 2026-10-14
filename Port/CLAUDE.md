# Port/CLAUDE.md — working on Amoebius

The repository's root `CLAUDE.md` is about the Unity game. **This file is about `Port/`**: the
Amoebius, Froglet's own engine that runs the same game with no Unity. Both apply when you
edit `Assets/_Scripts`, because the engine compiles those files live.

**Name.** The engine is **Amoebius** (renamed from Prisma on 2026-10-09). The code followed on
2026-10-12: `Amoebius.exe` and `Amoebius-Windows.zip`, `Library/Amoebius`, the `AMOEBIUS_*`
environment variables, the `amoebius_*` MCP tools (server `amoebius-mcp`), the `/amoebius` skill, the
`amoebius-*` workflows, class names (`AmoebiusBoard`, `AmoebiusTracks`), `AMOEBIUS001`, the data folder
`%LOCALAPPDATA%/Amoebius`. Existing installs keep working: the old data folder is moved over, and
`PrismaProject.json`, the `PRISMA_*` variables and `prisma-session` reports are still read. A few names
still carry the old one (the `#prisma` page hash the studio pages read, `Tools/Build/prisma_party_scenarios/`,
`docs/PRISMA_BOARD_SCHEDULER.md`); renaming those is its own change. The game's `Prism*` names are not
the engine's and were never renamed.

## The one rule

**The port never changes the Unity project.** Nothing under `Port/` is read by Unity, and the
port only *reads* `Assets/`. A port branch may change, outside `Port/`, only `.gitignore`
`Port/**` rules, `.github/workflows/amoebius-*` and `.claude/skills/amoebius*` (the legacy `froglet-*`
names are still accepted), plus one editor-only file the port owns:
`Assets/_Scripts/Editor/LaunchAmoebius.cs` (**FrogletTools > Amoebius > Launch Amoebius**, which builds
`Amoebius.exe` from the checkout into `Library/Amoebius` and opens it). Check before every commit:

```bash
python3 Port/tools/check_unity_isolation.py        # or the MCP tool unity_isolation_check
```

A gameplay fix belongs in `Assets/_Scripts` on its own Unity PR, not here. An engine gap (an API
the game uses that the engine lacks) belongs in `Port/src/CosmicShore.Engine` / `.Compat`.

## Map

| Where | What |
|---|---|
| `src/CosmicShore.Engine` | Unity's runtime API re-implemented (scene graph, loop, physics, uGUI, TMP, Input System, Reflex, SOAP, Netcode) |
| `src/CosmicShore.Compat` | Third-party packages in their own namespaces (UniTask, DOTween, Entities, Cinemachine ...) |
| `src/CosmicShore.Live` | The game: `Assets/_Scripts` synced into `obj/live-src` with namespaces rewritten, then compiled. Never hand-port a game file |
| `src/CosmicShore.Content` | Reads Unity's files: YAML scenes/prefabs, metas, FBX, textures, materials, fonts |
| `src/CosmicShore.Render` | The OpenGL renderer (GL 3.3 / GL ES 3.0), post stack, uGUI and TMP drawing |
| `src/CosmicShore.Player` | `CosmicShore.exe`: window, headless mode, scripted input, **control port** |
| `src/CosmicShore.Mobile` · `src/CosmicShore.Build` | Phone player · `cs-build` (player data, APK/AAB, iOS) |
| `src/CosmicShore.Launcher` | `Amoebius.exe` (Dear ImGui): play a branch, phone builds, Project Settings, agent chats, GIT, the EDITOR page (TOOLS, DATA, MODELS) |
| `src/CosmicShore.AssetTool` | `cs-asset`: edit scenes/prefabs/assets without Unity, plus the JSON the EDITOR page and the `asset_*` MCP tools read (`EditorData.cs`) |
| `src/CosmicShore.Mcp` | `amoebius-mcp`: this engine as an MCP server for Claude Code |
| `ProjectSettings/AmoebiusProject.json` | The engine's own Player/Scenes/Quality settings; empty fields inherit Unity's |
| `parity/` | The parity harness (C1): replays, Unity goldens, C9 tolerances, scoreboard catalogue. `engine_parity` diffs; `tools/gen_parity_scoreboard.py` writes `docs/PARITY.md`. Read `parity/README.md` |
| `tests/` | `CosmicShore.Tests` (engine, xunit, ~70 s, no GPU) · `CosmicShore.Tests.Ported` (the game's EditMode tests) |
| `docs/ARCHITECTURE.md` | How it all fits; read the section for the area you touch |
| `docs/ROADMAP.md` | The milestones (gameplay parity, then Unity-free development), checkpoints, open gaps and ready prompts. Pick work from here |
| `../Docs/MULTIPLAYER_START_HERE.md` | **Multiplayer's front door** (Unity and Amoebius): the owner's goals and rules, current state, the hand-test list, the ordered next steps. Read before `docs/MULTIPLAYER.md` |
| `docs/MULTIPLAYER.md` | Online play and its test tools: licensing (why NGO's code is not used), backends (UGS from our engine, own servers, Steam), the network simulator, stats, faults, the MULTIPLAYER panel, the UDP transport, the relay (Unity Relay's protocol, our relay server) and UGS sign-in. Read before touching `Networking/` |
| `docs/ARCHITECTURE_REVIEW_2026-10-06.md` | The architecture review's 20 items: what was checked, decided (with reasons) and measured. Read it before reopening one of those questions |

## Who works where

| Session | Scope | May edit | Started from |
|---|---|---|---|
| **Amoebius Agent** (powered by Claude) | the game, as it runs in Amoebius | `Assets/` and the rest of the repo, **never `Port/`** | Amoebius's AGENT page |
| **Engine development / milestones** | the engine, toward a roadmap checkpoint | `Port/` (this file's rules), **never `Assets/`, `Packages/`, `ProjectSettings/`** | Claude Code at the repo root |
| **Tool build** | one FrogletTools tool, made native | `Port/src/CosmicShore.AssetTool`, `Port/tests/CosmicShore.AssetTool.Tests`, `Port/tools/froglet-tools` only | Amoebius's EDITOR > TOOLS > BUILD |

Deny rules on the Claude Code CLI enforce the agent's scope in every mode. Amoebius runs any number of
agent chats side by side, all in Amoebius's workspace (not the user's clone): their edits
stay uncommitted there until the user commits and pushes them on Amoebius's GIT page, so a session
in Amoebius does not commit, push or switch branches unless asked, and START never discards them. Milestone work
records progress in `docs/milestones.json` (status plus a dated note with evidence), and marks a
checkpoint done only after running its exit criterion. Amoebius plays its own workspace, so it can be
on a different branch from the user's Unity checkout; opened from Unity's Launch Amoebius it follows
Unity's branch (`--clone`) until the user picks another.

## Amoebius's memory: tracks and the board

Every play run writes a session report (`--session-report`); Amoebius folds them into **tracks**
(`%LOCALAPPDATA%/Amoebius/tracks/tracks.json`, a brief in `MEMORY.md`): runs, performance per
scene, features (modes, vessels, scenes), audio (instances, missing events, unwired one-shots)
and every distinct problem with first/last seen. The **board** (`board.json`, same folder) holds
bugs and tasks; Amoebius and agents add only *suggestions*, which the user accepts. Every item has
a **done when** criterion, the check that proves it: `amoebius_board_suggest` requires one, and a bug
that came from the tracks is verified by them (not seen in 3 runs through its scene marks it MET; a
relapse reopens it). Tools: `amoebius_tracks` (read this before asking what is wrong), `amoebius_board`,
`amoebius_board_suggest`. Code: `src/Shared/AmoebiusTracks.cs`, `src/Shared/AmoebiusBoard.cs`.
Several writers share `board.json` (Amoebius.exe and every `amoebius-mcp`): a save re-reads it and merges
card by card (`src/Shared/Workspace/BoardMerge.cs`, cards matched by `Uid`), Amoebius pulls other writers'
changes in once a second, and an unreadable file is copied to `board.json.corrupt-*` before anything
replaces it. The scheduler's UI-free logic (quick add, TODAY/UPCOMING queries, repeat rules) lives in
`src/Shared/Workspace/` - linked into the launcher, the MCP server and the tests, never a project of its
own (the launcher's UPDATE builds only `CosmicShore.Launcher`, `Shared` and `Directory.Build.props`).
Details, the quick-add syntax, Windows test steps and the roadmap: `docs/PRISMA_BOARD_SCHEDULER.md`.

## The editor (M2): tools, data sets, models - not a hierarchy

Amoebius's editor starts where the work is (`docs/ROADMAP.md` § M2, decided 2026-10-08): EDITOR >
TOOLS (every FrogletTools tool, handed to the agent with its source), DATA (the ScriptableObject
data sets, edited field by field through `cs-asset set`) and MODELS (each FBX, and `.blend`/`.ma`/`.mb`
through the installed Blender/Maya as Unity does, with a CPU-drawn drag turntable in the colours of
the materials the game's prefabs give it, and VIEW IN ENGINE: the player's `--view-model FILE`). There is no hierarchy or scene inspector: scene and prefab structure is
edited by the agent through `cs-asset`. MCP: `asset_froglet_tools`, `asset_datasets`,
`asset_dataset`, `asset_model`, `asset_model_preview`. Tests: `tests/CosmicShore.AssetTool.Tests`
(cs-asset's editor commands) and `tests/CosmicShore.Launcher.Tests` (Amoebius.exe's chats, usage, git
and the DATA page's quoting).

## The loop

With the **amoebius MCP server** (preferred - see below), the loop is tools:

1. Edit code.
2. `engine_build` (target `player`) - fix every error it lists. `engine_smoke` then boots the
   game headless and answers PASS/FAIL with every error, exception and warning it logged.
3. `game_start` - boots the real game with a control port (under xvfb on a display-less server).
4. Look and act (and read `amoebius_tracks` first when chasing a reported problem): `game_screenshot`, `game_state`, `game_input` ("click X,Y", "type pilot",
   "key Enter", "hold W 60", "scroll X,Y -3", "drag X,Y X,Y 8"), `game_wait`, `game_find`, `game_hierarchy`, `game_get` /
   `game_set`, `game_ui_at`, `game_dump_ui`, `game_logs`, `game_load_scene`.
5. `game_stop`, then `engine_test` and `unity_isolation_check` before committing.

When something that used to work is broken, `amoebius_bisect` (good, bad, check) finds the commit:
`git bisect run` over the commits that touch `Port/`, in a scratch worktree (your checkout never
moves), each candidate built there and judged by an `engine_smoke` error `signature` (substring or
`/regex/`) or a replay diff (`check: parity`, against the good commit's own replay). A candidate
that does not build is skipped. One engine build per step, so it is slow; `Port/tools/bisect_demo.py`
proves it on a planted regression.

Without MCP, the same from a shell:

```bash
export PATH=/opt/dotnet:$PATH                       # wherever the .NET 10 SDK is
dotnet build Port/src/CosmicShore.Player
Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore --control-port 47800 &
curl -s -X POST -d '{"cmd":"state"}' http://127.0.0.1:47800/
curl -s -X POST -d '{"cmd":"do","arg":"click 640,360"}' http://127.0.0.1:47800/
```

Commands: `state`, `do VERB ARGS`, `wait N`, `screenshot [PATH]`, `find TEXT`,
`hierarchy [ROOT[:DEPTH]]`, `get OBJ COMP MEMBER`, `set OBJ COMP MEMBER VALUE`, `ui_at X,Y`,
`dump_ui NAME[:DEPTH]`, `logs [N]`, `scene NAME`, `quit` (`src/CosmicShore.Player/ControlServer.cs`).
An unattended run without a port: `--frames N --shot F:out.png --do "F:click X,Y"`
(`docs/ARCHITECTURE.md` §11.4).

## Things that bite

- **Coordinates are screenshot pixels, top-left origin.** Input lands a frame or two later:
  `game_wait` 5-30 frames before you look.
- **A fresh profile boots through three prompts**: birth year, data-collection consent, then
  username. `profile` in `game_start` picks a save slot (a new name is a first-time user; reuse a
  name to skip them). Never accept consent on the user's behalf unless asked. Offline (`COSMIC_SHORE_NET=off`) and silent
  (`COSMIC_SHORE_AUDIO=off`) are the MCP defaults.
- **Online play is UGS, as the Unity build's** (`docs/MULTIPLAYER.md` §6.10): the launcher's START ONLINE
  (`game_start` `online: true`) signs in to the game's live UGS project and plays through UGS Lobby and UGS
  Relay. START is offline, and there is no LAN play, no local server and no shared-folder sessions. **Never
  start an online game without the owner's go-ahead** (a sign-in creates a player in the live project); the
  tests use `UgsLobbyStandIn`, `UgsAuthStandIn` and `FrogletRelayServer`. Every player needs their own profile
  (`COSMIC_SHORE_PROFILE`), which is their own UGS player. A match starts only when every player presses the
  HUD's GO (the ReadyButton), not when GO appears.
- **A player's networking is built in ONE place, `NetStack.Install`** (transport, relay, sessions, then
  simulator and faults; `docs/MULTIPLAYER.md` §6.9). Never set `NetDriver.TransportFactory`,
  `MultiplayerService.Instance`, `NetSimulator` or `NetFaults` from `PlayerBoot` directly: a merge did, and
  silently disabled the UDP transport, the relay, the simulator and the faults. `NetStackTests` fails if it
  happens again. The player calls it from `UgsSetup.InstallOnline`.
- **Under xvfb, rendering is software**: a few frames per second. `game_wait 600` is minutes.
  Use `headless: true` when you need no pictures - it ticks as fast as the CPU allows.
- **A scripted run (`--frames`) steps every game clock by 1/60 s**, so the game's DiagnosticsHUD (F7) reads
  "FPS 60 · 16.7 ms" there however slow the frame is. That is deliberate: the pictures repeat. Its CPU (busy)
  and GPU rows come from `FrameTimingManager`, which measures the real frame in every run. Judge fps from a
  normal run, or from F3. A normal run passes the real frame time unclamped (`deltaTime` is capped at the
  project's Maximum Allowed Timestep), and there the HUD and F3 agree within a few percent.
- **Unity injects Reflex before Awake/OnEnable**, with the scene active. Bugs where a value
  "never updates" are often an injection-order gap in the engine, not the game.
- **The live compile takes whatever `Assets/` is checked out.** Errors, warnings and stack traces
  name the original `Assets/_Scripts` file and line (the sync writes `#line`); fix the engine's
  missing API, not the synced copy in `obj/live-src` (regenerated as needed). Warning
  **`AMOEBIUS001`** marks an RPC the sync cannot intercept (a generic method, an attribute split over
  lines, `[Rpc(SendTo...)]`): in Amoebius it would run locally instead of over the network.
- **Serialization follows Unity's rules for script types**: public or `[SerializeField]` fields
  of a serializable type, `[field: SerializeField]` backing fields, `[FormerlySerializedAs]`, then
  `OnAfterDeserialize`. Properties and private unmarked fields never load. `cs-asset
  serialization-audit` lists every key Unity and Amoebius read differently; keep it at 0 DROPPED and
  0 EXTRA (`src/CosmicShore.Content/Serialization/UnitySerializationRules.cs`).
- **GL stays behind the render boundary**: only `CosmicShore.Render` (and the player window's
  present/read-back) may use the GL binding. `RenderBoundaryTests` fails otherwise, so a Metal or
  WebGPU backend later replaces one project.
- **Debug vs Release**: Debug is a Unity development build (diagnostics overlay, logs); Release
  is the customer build. The MCP server runs Debug.
- `--verbose` opens every `CSDebug` log channel; `game_logs grep` filters them.
- **Session reports**: `--session-report PATH` (the launcher always passes one) writes JSON at
  exit - scenes, frame-time percentiles, CPU per loop phase (`cpu.phaseAvgMs`), allocations per
  phase and steady-state GC (`memory`), audio, distinct errors/warnings/exceptions with counts,
  crash. A user's "LAST SESSION" message points at one; read it before guessing. A `--headless`
  run has no GPU: its render figures are zero and the prism render service is off, so
  `[PrismClock]`/`[PrismFactory]` errors in a headless report are expected (xvfb renders in
  software and has neither caveat).

## Connecting Claude Code to the engine

The server is `Port/src/CosmicShore.Mcp` (stdio, no dependencies). From the repository root:

```bash
claude mcp add amoebius -- dotnet run --project Port/src/CosmicShore.Mcp --
# or for one session:
claude --mcp-config Port/.mcp.json
```

The launcher's CLAUDE page wires it in by itself. The `/amoebius` skill
(`.claude/skills/amoebius/SKILL.md`) is the short version of this file for an agent.
