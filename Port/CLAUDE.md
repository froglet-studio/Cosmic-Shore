# Port/CLAUDE.md — working on Prisma

The repository's root `CLAUDE.md` is about the Unity game. **This file is about `Port/`**: the
Prisma, Froglet's own engine that runs the same game with no Unity. Both apply when you
edit `Assets/_Scripts`, because the engine compiles those files live.

## The one rule

**The port never changes the Unity project.** Nothing under `Port/` is read by Unity, and the
port only *reads* `Assets/`. A port branch may change, outside `Port/`, only `.gitignore`
`Port/**` rules, `.github/workflows/prisma-*` and `.claude/skills/prisma*` (the legacy `froglet-*`
names are still accepted), plus one editor-only file the port owns:
`Assets/_Scripts/Editor/LaunchPrisma.cs` (**FrogletTools > Prisma > Launch Prisma**, which builds
`Prisma.exe` from the checkout into `Library/Prisma` and opens it). Check before every commit:

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
| `src/CosmicShore.Launcher` | `Prisma.exe` (Dear ImGui): play a branch, phone builds, Project Settings, agent chats, GIT, the EDITOR page (TOOLS, DATA, MODELS) |
| `src/CosmicShore.AssetTool` | `cs-asset`: edit scenes/prefabs/assets without Unity, plus the JSON the EDITOR page and the `asset_*` MCP tools read (`EditorData.cs`) |
| `src/CosmicShore.Mcp` | `prisma-mcp`: this engine as an MCP server for Claude Code |
| `ProjectSettings/PrismaProject.json` | The engine's own Player/Scenes/Quality settings; empty fields inherit Unity's |
| `parity/` | The parity harness (C1): replays, Unity goldens, C9 tolerances, scoreboard catalogue. `engine_parity` diffs; `tools/gen_parity_scoreboard.py` writes `docs/PARITY.md`. Read `parity/README.md` |
| `tests/` | `CosmicShore.Tests` (engine, xunit, ~70 s, no GPU) · `CosmicShore.Tests.Ported` (the game's EditMode tests) |
| `docs/ARCHITECTURE.md` | How it all fits; read the section for the area you touch |
| `docs/ROADMAP.md` | The milestones (gameplay parity, then Unity-free development), checkpoints, open gaps and ready prompts. Pick work from here |
| `docs/ARCHITECTURE_REVIEW_2026-10-06.md` | The architecture review's 20 items: what was checked, decided (with reasons) and measured. Read it before reopening one of those questions |

## Who works where

| Session | Scope | May edit | Started from |
|---|---|---|---|
| **Prisma Agent** (powered by Claude) | the game, as it runs in Prisma | `Assets/` and the rest of the repo, **never `Port/`** | Prisma's AGENT page |
| **Engine development / milestones** | the engine, toward a roadmap checkpoint | `Port/` (this file's rules), **never `Assets/`, `Packages/`, `ProjectSettings/`** | Claude Code at the repo root |
| **Tool build** | one FrogletTools tool, made native | `Port/src/CosmicShore.AssetTool`, `Port/tests/CosmicShore.AssetTool.Tests`, `Port/tools/froglet-tools` only | Prisma's EDITOR > TOOLS > BUILD |

Deny rules on the Claude Code CLI enforce the agent's scope in every mode. Prisma runs any number of
agent chats side by side, all in Prisma's workspace (not the user's clone): their edits
stay uncommitted there until the user commits and pushes them on Prisma's GIT page, so a session
in Prisma does not commit, push or switch branches unless asked, and START never discards them. Milestone work
records progress in `docs/milestones.json` (status plus a dated note with evidence), and marks a
checkpoint done only after running its exit criterion. Prisma plays its own workspace, so it can be
on a different branch from the user's Unity checkout; opened from Unity's Launch Prisma it follows
Unity's branch (`--clone`) until the user picks another.

## Prisma's memory: tracks and the board

Every play run writes a session report (`--session-report`); Prisma folds them into **tracks**
(`%LOCALAPPDATA%/Prisma/tracks/tracks.json`, a brief in `MEMORY.md`): runs, performance per
scene, features (modes, vessels, scenes), audio (instances, missing events, unwired one-shots)
and every distinct problem with first/last seen. The **board** (`board.json`, same folder) holds
bugs and tasks; Prisma and agents add only *suggestions*, which the user accepts. Every item has
a **done when** criterion, the check that proves it: `prisma_board_suggest` requires one, and a bug
that came from the tracks is verified by them (not seen in 3 runs through its scene marks it MET; a
relapse reopens it). Tools: `prisma_tracks` (read this before asking what is wrong), `prisma_board`,
`prisma_board_suggest`. Code: `src/Shared/PrismaTracks.cs`, `src/Shared/PrismaBoard.cs`.

## The editor (M2): tools, data sets, models - not a hierarchy

Prisma's editor starts where the work is (`docs/ROADMAP.md` § M2, decided 2026-10-08): EDITOR >
TOOLS (every FrogletTools tool, handed to the agent with its source), DATA (the ScriptableObject
data sets, edited field by field through `cs-asset set`) and MODELS (each FBX, and `.blend`/`.ma`/`.mb`
through the installed Blender/Maya as Unity does, with a CPU-drawn drag turntable in the colours of
the materials the game's prefabs give it, and VIEW IN ENGINE: the player's `--view-model FILE`). There is no hierarchy or scene inspector: scene and prefab structure is
edited by the agent through `cs-asset`. MCP: `asset_froglet_tools`, `asset_datasets`,
`asset_dataset`, `asset_model`, `asset_model_preview`. Tests: `tests/CosmicShore.AssetTool.Tests`
(cs-asset's editor commands) and `tests/CosmicShore.Launcher.Tests` (Prisma.exe's chats, usage, git
and the DATA page's quoting).

## The loop

With the **prisma MCP server** (preferred - see below), the loop is tools:

1. Edit code.
2. `engine_build` (target `player`) - fix every error it lists. `engine_smoke` then boots the
   game headless and answers PASS/FAIL with every error, exception and warning it logged.
3. `game_start` - boots the real game with a control port (under xvfb on a display-less server).
4. Look and act (and read `prisma_tracks` first when chasing a reported problem): `game_screenshot`, `game_state`, `game_input` ("click X,Y", "type pilot",
   "key Enter", "hold W 60"), `game_wait`, `game_find`, `game_hierarchy`, `game_get` /
   `game_set`, `game_ui_at`, `game_dump_ui`, `game_logs`, `game_load_scene`.
5. `game_stop`, then `engine_test` and `unity_isolation_check` before committing.

When something that used to work is broken, `prisma_bisect` (good, bad, check) finds the commit:
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
- **Under xvfb, rendering is software**: a few frames per second. `game_wait 600` is minutes.
  Use `headless: true` when you need no pictures - it ticks as fast as the CPU allows.
- **Unity injects Reflex before Awake/OnEnable**, with the scene active. Bugs where a value
  "never updates" are often an injection-order gap in the engine, not the game.
- **The live compile takes whatever `Assets/` is checked out.** Errors, warnings and stack traces
  name the original `Assets/_Scripts` file and line (the sync writes `#line`); fix the engine's
  missing API, not the synced copy in `obj/live-src` (regenerated as needed). Warning
  **`PRISMA001`** marks an RPC the sync cannot intercept (a generic method, an attribute split over
  lines, `[Rpc(SendTo...)]`): in Prisma it would run locally instead of over the network.
- **Serialization follows Unity's rules for script types**: public or `[SerializeField]` fields
  of a serializable type, `[field: SerializeField]` backing fields, `[FormerlySerializedAs]`, then
  `OnAfterDeserialize`. Properties and private unmarked fields never load. `cs-asset
  serialization-audit` lists every key Unity and Prisma read differently; keep it at 0 DROPPED and
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
claude mcp add prisma -- dotnet run --project Port/src/CosmicShore.Mcp --
# or for one session:
claude --mcp-config Port/.mcp.json
```

The launcher's CLAUDE page wires it in by itself. The `/prisma` skill
(`.claude/skills/prisma/SKILL.md`) is the short version of this file for an agent.
