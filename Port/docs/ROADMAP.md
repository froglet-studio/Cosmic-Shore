# Prisma roadmap: checkpoints, timeline, prompts

Written 2026-10-05; updated 2026-10-06 with the architecture review
(`ARCHITECTURE_REVIEW_2026-10-06.md`: what was checked, decided and done for each of its 20
items). Owner: Froglet Inc. Read this before planning engine work. Agents: the launcher's PARITY
CHECK and `/prisma` read the open lists here.

## The two milestones

| | Milestone | Done when | Target |
|---|---|---|---|
| **M1** | **Gameplay parity.** Playing Cosmic Shore in Prisma is indistinguishable from the Unity build. | A blind test passes (C9) and every row of the parity matrix is green on Windows, with phones close behind. | **Q2 2027** (~30 weeks) |
| **M2** | **Unity-free development.** Developers stop opening Unity. The engine's own tools do every Unity job Cosmic Shore needs, and nothing more. | One full release cycle (3 weeks) is shipped with Unity never opened (E9). | **Q1 2028** (~22 more weeks) |

M2 depends on M1: nobody gives up the editor for a runtime that still looks different. Some
M2 groundwork can start early (see the timeline).

**Scope rule for both: only what Cosmic Shore and Froglet's next games use.** Every checkpoint
starts by measuring use in `Assets/`, and anything the game doesn't touch stays unbuilt.

## Where the engine stands (measured 2026-10-05)

| Area | State | Evidence |
|---|---|---|
| Game code | All 2,090 `Assets/_Scripts` files compile and run unmodified | `CosmicShore.Live`; 0 compile errors |
| Boot / menus / arcade launch | Bootstrap -> Authentication -> Menu_Main -> a match, 0 errors | `engine_smoke`; PROGRESS_2026-09-29 |
| Scenes | 36 build scenes load from Unity YAML | `cs-build` player data |
| Rendering | GL 3.3 / GLES 3.0; MSAA, ACES, bloom, Panini, skybox, skinning, ECS prism mass, LOD-free culling | `docs/ARCHITECTURE.md` §7 |
| Shaders | **19 of 81** Shader Graph / `.shader` assets have a dedicated translation; the rest render through generic material-family fallbacks. The graphs use 85 node types and 30 custom HLSL functions, so a compiler covers them (C2) | name scan of `Port/src` vs `Assets`; node census in the review response (D10) |
| Particles / VFX | **Not drawn.** 26 prefabs carry a ParticleSystem; 2 VFX Graphs; 1 Timeline | §13 known gaps |
| Physics | Triggers and queries only; **no contact solver**. 17 scripts use Rigidbody (velocity, kinematic, gravity, torque; no joints or forces), 1 uses OnCollisionEnter | ARCHITECTURE §13; census in the review response (E13) |
| Animation | Animator, blend trees, FBX takes; **Animation Rigging is data only** | |
| Audio | FMOD Studio runtime, real banks, buses, VCAs | |
| Networking | Netcode model over TCP; LAN parties work. **No internet relay, no real UGS** | PROGRESS §Known gaps 1 |
| Performance | Heavy Bloomrush: collect 22-26 ms (was ~150 ms). Not yet at Unity's level on the GTX 1060 floor | PROGRESS §2 |
| Platforms | Windows and Linux desktop; Android APK builds (no device run yet); iOS .ipa via GitHub CI (unsigned, Sideloadly) | |
| Tooling | Launcher (play, phone builds, Project Settings, Claude, tracks, board, milestones), `cs-asset` (scene/prefab writes, `serialization-audit`), `cs-build`, MCP server, session reports with CPU/GC per phase, `engine_smoke` | `docs/LAUNCHER.md` |
| Tests | ~1,590 engine tests + 352 ported Unity EditMode tests | `dotnet test` |
| Unity editor surface we'd replace | 300 editor scripts, 44 EditorWindows, 23 custom inspectors/drawers, **91 FrogletTools menu items** | scan of `Assets/**/Editor` |

## What the engine lacks, and what would make it better (for Froglet, not in general)

1. **A parity harness.** There's no objective "same as Unity" check yet; it's done by eye. This
   is the single most important missing piece (C1): deterministic input replays that both
   engines run, golden captures from Unity, and image, score and audio-event diffs.
2. **Determinism and replays.** A recorded input stream plus a seed that reproduces a match
   exactly. It powers the parity harness, bug reports ("send the replay"), AI training and
   netcode debugging. The Unity build gains the same feature for free if it lives in game code.
3. **Particles.** 26 prefabs (impacts, crystals, trails) are invisible today. This is the most
   visible gap.
4. **The remaining 62 shaders.** Each one is a visible difference waiting to be noticed.
5. **Contact physics,** limited to what the 17 Rigidbody scripts use. No general-purpose solver.
6. **Online play over the internet.** We need a backend decision (see External factors), and
   the netcode also lacks delta compression and unreliable channels.
7. **Parity CI.** Every bleeding-edge push should compile the port against the new `Assets/`
   and smoke-run every mode headless on a Linux runner. Today a Unity-side change that breaks
   the port is found by hand.
8. **Cooked content.** The player parses Unity YAML at load time. A binary cooked format built
   by `cs-build` would cut load times (Steam DoD #5 wants a load-time target) and make phone
   builds smaller.
9. **Graphics API future.** OpenGL is deprecated on Apple platforms and GLES on iOS is frozen.
   A Metal (or Vulkan + MoltenVK) backend is needed before Apple removes GL. The renderer's
   material families are the right seam for it.
10. **Engine and game separated.** Some renderer families are keyed to Cosmic Shore's graph
    names. Moving them behind a data-driven material system lets Froglet's next game reuse the
    engine without forking it.
11. **Crash and session telemetry.** Session reports now exist locally (`--session-report`).
    The next step is an opt-in upload from testers' machines, so Claude can triage a week of
    play tests at once.
12. **Editor.** Everything in M2: hierarchy, inspector, prefab overrides, asset import, play
    mode. The launcher's Dear ImGui shell is where it grows.

## External factors (and what each one changes)

| Factor | Effect | Plan |
|---|---|---|
| **The game keeps moving in Unity.** Every day new scripts, Shader Graphs and packages land on bleeding-edge. | Parity is a moving target; the port can break on any push. | Parity CI (C1.3) on every push; a "port breakers" list reviewed weekly; new Unity APIs get engine support within a week. |
| **The Steam Early Access release runs on Unity** (`Docs/STEAM_RELEASE_TASKS.md`; 3-week test cadence) | The engine is never on the launch's critical path. | The port never changes `Assets/` (`check_unity_isolation.py`). Engine work never blocks a Unity release. Switching the shipped build to the engine is a separate decision after M1. |
| **Team capacity:** one developer plus Claude sessions | Throughput is bounded by review time, not typing. Claude plan usage limits cap long agent runs. | Parallel Claude sessions per checkpoint (one per row of the parity matrix); the launcher's session reports and `engine_smoke` keep each loop short. Assume ~2 checkpoint-weeks of output per calendar week. |
| **Hardware access:** no Mac, no Android test phone yet, GTX 1060 is the floor | Phones and low-end performance go unverified. | iOS via GitHub macOS runners + Sideloadly (works today). Buy one mid-range Android phone (~$200). Profile on a GTX 1060 machine at each performance checkpoint (Steam task H8). |
| **Unity Gaming Services** (Auth, Cloud Save, Leaderboards, Lobby, Relay) | The port uses local stand-ins. UGS has REST APIs, but Relay speaks Unity Transport. | **Decision gate G2 (by 2026-12-15):** (a) UGS over REST plus our own Relay-compatible transport, or (b) Steam networking (Steamworks.NET, MIT) plus our own small backend. Recommendation: (b) for PC, because Steam is the launch platform; revisit phones later. |
| **Steamworks** (3 scripts use it) | Achievements, overlay and authentication must work in the engine for a Steam build. | Steamworks.NET runs on any .NET. Wire it in C6. |
| **FMOD licensing** | The engine ships FMOD's runtime. The license is per title, not per engine, so it carries over, but confirm. | Check the FMOD indie/commercial terms before any engine build ships (G3). |
| **Legal provenance** | Engine code must not copy Unity source; two spots are queued for clean rewrites (`docs/LEGAL_REVIEW.md`). Every Unity package the game uses (Mathematics, Collections, Netcode, Transport, Entities, Burst, Cinemachine, Timeline, Rigging, Input System) is under the Unity Companion License, which covers Unity-dependent projects only, so their source can never be vendored; only UniTask is MIT (license table in the review response, B4). | Finish those rewrites before M1 sign-off. Have counsel review before shipping a commercial build on the engine (G3), including whether the engine may declare Unity's namespaces (review A1). |
| **Apple** | Free Apple ID signatures last 7 days (3 apps); TestFlight and the App Store need the $99/yr program; GL deprecation. | The program is needed only when phones go to testers. The Metal backend is tracked as a later checkpoint (C8.4). |
| **.NET 10 LTS** | Supported until Nov 2028. | Move to the next LTS in 2028. |
| **GitHub Actions** | macOS minutes are free on a public repo; artifacts are public while kept. | Fine for now. If the repo goes private, budget macOS minutes (~$0.08/min, ~5 min per build). |
| **Claude models / CLI** | The agent bridge depends on Claude Code's CLI flags and the MCP protocol. | Keep the launcher on documented flags only. The MCP server has no dependencies, so it is easy to keep current. |

## Timeline

```
2026  Oct        Nov        Dec        2027 Jan   Feb        Mar        Apr        May        Jun  ...  2027 Q3-Q4 ........ 2028 Q1
C0 foundations (done Oct 6)
      |C1 parity harness|C1b bisect|
                 |C2 particles + shaders (rolling) ------------------------------|
                 |C3 physics|
                            |C5 UI + text|
                       G2 backend decision (Dec 15)
                            |C6 online + Steam --------------|
                                       |C7 performance --------------|
                                               G6 |C8 platforms (Win/Android/iOS/Deck)|
                                                                        |C9 blind test| = M1
                                  |E1 editor groundwork (hierarchy/inspector, read-only)|
                                                                                   |E2-E8 editor ---------------------|
                                                                                                                  |E9 cut-over| = M2
```

### M1 checkpoints

| # | Checkpoint | Exit criterion (measurable) | Weeks | Depends on |
|---|---|---|---|---|
| **C0** | **Foundations** (done 2026-10-06) | `#line` maps errors to `Assets/`; incremental sync; RPCs the sync cannot intercept are warned (`PRISMA001`); execution order from `.meta` and the attribute in every phase; Unity's serialization rules with `cs-asset serialization-audit` at 0 dropped / 0 extra; CPU, allocation and GC per phase in session reports; acceptance criteria on the board; budgets and fallback for milestone sessions; GL kept behind the render boundary (test) | - | - |
| **C1** | **Parity harness** | (1) An input recorder/replayer in game code (`Assets/_Scripts/Utility`, its own Unity PR) that both engines run. (2) A Unity-side capture command producing golden frames, scores and audio events for a replay, plus `Random` sequences for a set of seeds. (3) `engine_parity` in the MCP server: replay in the engine and diff against the goldens using the per-channel tolerances in the review response (C9): exact state, RNG and event order; transforms within 1e-4 relative for 10 s; SSIM per frame. (4) GitHub CI: build the port and run `engine_smoke` for every mode on each bleeding-edge push. (5) A generated parity scoreboard (`docs/PARITY.md` + JSON for MILESTONES): every subsystem and shader Faithful / Approximate / Missing with its covering test | 4 | C0 |
| **C1b** | **Bisect** | `prisma_bisect(good, bad, check)` in the MCP server: `git bisect run` over commits touching `Port/`, each built in a scratch worktree and checked by a replay diff or an `engine_smoke` error signature; finds a planted regression in under 15 steps | 1 | C1 |
| **C2** | **Visual completeness** | A Shader Graph compiler (the 85 node types the project uses) with the 30 custom functions ported once and families keyed by guid; the 38 `.shader` files hand-translated by on-screen use; an unknown shader warns once; ParticleSystem (the modules the 26 prefabs use) and both VFX Graphs draw (VFX Graph as Approximate); the Timeline plays; SSIM >= 0.97 against goldens in every mode's first 60 s | 14 (rolling) | C1 |
| **C3** | **Physics parity** | Contact resolution for the 17 Rigidbody users, plus oriented boxes; OnCollision* fires; replays end with identical scores. Rule: never a general solver in-house; bind BepuPhysics v2 for contacts only if the census needs mesh contacts or PhysX-like friction | 3 | C1 |
| **C4** | **Audio parity** | Every replay's FMOD event sequence matches Unity's, including mixer snapshots | 1 | C1 |
| **C5** | **UI + text parity** | Every screen at 16:9, 21:9 and 4:3 matches Unity (SSIM >= 0.98). TMP rendering rewritten clean (legal) | 4 | C1 |
| **C6** | **Online parity** | An `INetTransport` seam with TCP as its first transport (can start before G2); then 4 players over the internet in each multiplayer mode; Steam auth and achievements; cloud save and leaderboards on the chosen backend | 8 | G2 |
| **C7** | **Performance parity** | On the GTX 1060: every mode at every intensity within 10% of Unity's frame time, and load times within the R7 target. Includes GPU timer queries in the session report, the `tasks` phase's allocations (279-728 KB/frame in Menu_Main, steady GC up to 1.9 ms/frame), jobs moved to `Parallel.For` only where a profile shows them, and a `SustainedLowLatency` A/B | 8 | C2 |
| **C8** | **Platforms** | (1) Windows real-machine pass. (2) Android device pass. (3) iOS sideload pass. (4) The backend plan for gate G6 (thin RHI with Metal, or wgpu-native) approved. (5) Steam Deck native Linux build | 6 | C2, C7, G6 |
| **C9** | **Blind test (= M1)** | 10 players play both builds in random order; detection no better than chance (<= 60% correct) | 2 | all |

### M2 checkpoints

Strategy: **keep Unity's file formats** (YAML scenes, prefabs, `.meta` guids) as the on-disk
format throughout. Then Unity and the Froglet editor can edit the same project during the
transition, and the cut-over is a decision, not a migration.

| # | Checkpoint | Exit criterion | Weeks |
|---|---|---|---|
| **E1** | **Editor shell** (groundwork, can start in Dec) | The launcher gains a "Prisma Editor" mode: scene hierarchy, inspector (read-only), asset browser, embedded player view | 4 |
| **E2** | **Edit and save** | Inspector edits, create/delete/reparent, prefab overrides and variants, saved through `cs-asset`'s writer; files byte-stable on round trip | 6 |
| **E3** | **Play mode** | Play from the editor with an incremental script recompile (< 5 s); edit fields live | 4 |
| **E4** | **Asset pipeline** | Import FBX, textures and audio with `.meta` generation and the import settings the game uses; GUIDs stable | 5 |
| **E5** | **Shaders** | Shader Graph JSON edited by a node editor (or by Claude via `asset-surgery`) and translated live; no Unity needed to add a material | 6 |
| **E6** | **Froglet tools** | The 91 FrogletTools tools: the top 20 by use ported first (a FrogletTool attribute host on ImGui); the rest retired or ported on demand | 8 |
| **E7** | **Animation + UI layout** | Animator controller editing and RectTransform layout editing with anchors and gizmos | 5 |
| **E8** | **Profiler, tests, release** | Frame profiler, EditMode test runner, Steam upload (steamcmd), Android/iOS from the editor | 4 |
| **E9** | **Cut-over (= M2)** | One 3-week cycle shipped without opening Unity; Unity kept read-only as the reference until M2 + 1 cycle | 3 |

### Decision gates

| Gate | When | Question |
|---|---|---|
| **G1** | end of C1 (~Nov 2) | Does the harness catch known differences? (Seed 3 deliberate ones and check that it reports all 3.) |
| **G2** | 2026-12-15 | Online backend: UGS-over-REST or Steam + own backend |
| **G3** | before any engine build ships to players | FMOD license, legal review, provenance rewrites done |
| **G4** | after C9 | Ship the Steam build on the engine, or keep Unity for the runtime and start M2 anyway |
| **G5** | after E3 | Is the editor good enough that the developer *prefers* it for scene work? If not, fix that before E4-E8 |
| **G6** | before C8 (~Mar 2027) | Graphics backend: a thin RHI (GL now, Metal later) or wgpu-native (Metal, Vulkan, D3D12 and GLES from one API; a new native dependency). Either way only `CosmicShore.Render` changes: `RenderBoundaryTests` keeps GL there |

## How to run each checkpoint (prompts)

**In Prisma:** MILESTONES lists every checkpoint from `docs/milestones.json` (status, weeks,
dependencies, exit criterion, prompt). START opens an engine session for it in plan mode with the
prompt below; the session updates `milestones.json` as it moves the checkpoint. The prompts also
work pasted into Claude Code at the repo root. Each one assumes `Port/CLAUDE.md` and this file. Every checkpoint ends with:
`engine_test` green, `engine_smoke` PASS, `unity_isolation_check` ok, docs updated, and this
file's row marked done with the date and the measurement.

**C1 - Parity harness**
> Plan checkpoint C1 of Port/docs/ROADMAP.md. Design an input recorder/replayer that lives in the game's own code (a separate Unity PR - list exactly what it adds under Assets/_Scripts/Utility and why it cannot live in Port/), a Unity-side capture step that writes golden frames, scores, FMOD events and `Random` sequences for a set of seeds, and an `engine_parity` MCP tool that replays in Prisma and diffs against those goldens with the per-channel tolerances in docs/ARCHITECTURE_REVIEW_2026-10-06.md (C9). Include the GitHub Actions job that builds the port and runs engine_smoke for every build scene on each bleeding-edge push, and a generator for the parity scoreboard (docs/PARITY.md + JSON). Measure first: how input reaches the game today (InputScript, Input System devices) and which RNG sources make a match nondeterministic.

**C1b - Bisect**
> Add `prisma_bisect(good, bad, check)` to the MCP server: run `git bisect run` over commits that touch Port/ only, build each candidate in a scratch git worktree, and judge it with a replay diff (engine_parity) or an engine_smoke error signature. Prove it by planting a regression three commits back and finding it.

**C2 - Visual completeness**
> First session: plan the Shader Graph compiler for CosmicShore.Render - parse the .shadergraph/.shadersubgraph JSON, emit GLSL for the node types the project uses (census in docs/ARCHITECTURE_REVIEW_2026-10-06.md, D10), port the 30 custom HLSL functions once as a GLSL library, key material families by shader guid, and warn once for an unknown shader. Later sessions (repeat per item): the next hand-written .shader, ParticleSystem module or VFX Graph, ranked by how many on-screen objects use it in the 19 Steam modes; prove each with game_screenshot before/after and the parity diff. Record each item in this file.

**C3 - Physics**
> Measure every Rigidbody and OnCollision* use in Assets/_Scripts (17 + 1 at last count). For each, say what contact behaviour it needs. Implement only that in CosmicShore.Engine physics, plus oriented boxes, with tests, and verify with replays that scores match Unity. Never write a general solver: if AstroLeague's ball needs mesh contacts or PhysX-like friction, propose binding BepuPhysics v2 for contacts only behind the trigger/query API, and flag the dependency before adding it.

**C5 - UI and text**
> For every screen in Menu_Main and the in-game HUD, capture the engine at 1920x1080, 2560x1080 and 1024x768, then list layout differences against the Unity goldens with game_ui_at / game_dump_ui evidence. Fix in the engine's RectTransform/layout/TMP code. Separately, plan the clean-room TMP SDF rewrite from docs/LEGAL_REVIEW.md.

**C6 - Online**
> Before G2: put an `INetTransport` seam between NetDriver and NetSocket (TCP stays the first implementation) with tests. After G2: implement the chosen backend from the boundary table in docs/ARCHITECTURE_REVIEW_2026-10-06.md (F14): transport over the internet with relay, Steam auth/achievements via Steamworks.NET, cloud save and leaderboards. Prove it with two machines in different networks (or two GitHub runners) completing a Bloomrush match.

**C7 - Performance**
> Profile the heaviest mode at intensity 4 from its session report (cpu.phaseAvgMs, memory.phaseAvgKB, steady GC) and dotnet-trace; add GPU timer queries to the report on desktop GL. Rank the top 5 costs against the GTX 1060 budget (16.6 ms). Fix the largest one per session, proving each with before/after numbers from the same replay. Known leads: the async `tasks` phase allocates 279-728 KB/frame in Menu_Main (steady GC up to 1.9 ms/frame); loading allocates ~2.5 GB.

**C8 - Platforms**
> Produce the Windows, Android and iOS builds from the launcher, run the platform checklist (boot, a full match, audio, input, suspend/resume) and log every failure as a session report. For gate G6, compare a thin RHI with a Metal backend against wgpu-native: what the renderer's GL calls map to, what changes in shaders, the effort, and the new dependency.

**E1 - Editor shell**
> Plan the Prisma Editor mode of the launcher: hierarchy, read-only inspector and asset browser over the engine's loaded scene, and the player embedded in a panel. Reuse ContentRuntime and the control-port model. List what Dear ImGui needs (docking branch?) and flag any new dependency.

**Session triage (any time)**
> LAST SESSION (launcher chip) - or: "Read every session report in <sessions folder> from this week, group the problems by engine area, rank them by how often players hit them, and propose the top 5 fixes."

## Working rhythm

- **Daily:** play a branch from the launcher, close the game, press LAST SESSION. Fix what it ranks first.
- **Weekly:** rerun PARITY CHECK on a new system; update this file's status table; prune the port-breakers list.
- **Every 3 weeks** (the Unity test-build cadence): tag an engine build, publish the launcher
  version (the launcher's UPDATE button picks it up), and record the parity matrix.
- **At each gate:** an explicit yes/no in this file with the evidence.

## Status

| Checkpoint | State | Date | Evidence |
|---|---|---|---|
| M0 - runs the real game, builds, tooling, Claude bridge | done | 2026-10-05 | PR #959 |
| C0 - foundations (architecture review) | done | 2026-10-06 | `ARCHITECTURE_REVIEW_2026-10-06.md` |
| C1 | not started | | |
