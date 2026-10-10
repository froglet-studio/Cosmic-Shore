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
| Particles / VFX | **Not drawn.** 26 prefabs carry a ParticleSystem; 2 VFX Graphs; 1 Timeline. The swarm/substrate fauna's member hearts (procedural instancing from GPU buffers) are not drawn either | §13 known gaps |
| Physics | Triggers and queries over spheres, oriented boxes and capsules; a contact pass for dynamic spheres (the Astro League ball) fires `OnCollision*`. **No general solver** and no physics library: the census needs none | ARCHITECTURE §13.1 (census, C3) |
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
12. **Editor.** Everything in M2, scoped by the 2026-10-08 decision below: FrogletTools, data
    sets and models first; scene and hierarchy editing stay with the agent. The launcher's Dear
    ImGui shell (its EDITOR page) is where it grows.

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
      |E1 Froglet tools ---------|   (started Oct 8, beside M1)
      |E2 data sets ----|
      |E3 models ---|
                                                                                   |E4-E8 editor ---------------------|
                                                                                                                  |E9 cut-over| = M2
```

### M1 checkpoints

| # | Checkpoint | Exit criterion (measurable) | Weeks | Depends on |
|---|---|---|---|---|
| **C0** | **Foundations** (done 2026-10-06) | `#line` maps errors to `Assets/`; incremental sync; RPCs the sync cannot intercept are warned (`PRISMA001`); execution order from `.meta` and the attribute in every phase; Unity's serialization rules with `cs-asset serialization-audit` at 0 dropped / 0 extra; CPU, allocation and GC per phase in session reports; acceptance criteria on the board; budgets and fallback for milestone sessions; GL kept behind the render boundary (test) | - | - |
| **C1** | **Parity harness** | (1) An input recorder/replayer in game code (`Assets/_Scripts/Utility`, its own Unity PR) that both engines run. (2) A Unity-side capture command producing golden frames, scores and audio events for a replay, plus `Random` sequences for a set of seeds. (3) `engine_parity` in the MCP server: replay in the engine and diff against the goldens using the per-channel tolerances in the review response (C9): exact state, RNG and event order; transforms within 1e-4 relative for 10 s; SSIM per frame. (4) GitHub CI: build the port and run `engine_smoke` for every mode on each bleeding-edge push. (5) A generated parity scoreboard (`docs/PARITY.md` + JSON): every subsystem and shader Faithful / Approximate / Missing with its covering test | 4 | C0 |
| **C1b** | **Bisect** (done 2026-10-08: found a planted regression in 3 steps) | `prisma_bisect(good, bad, check)` in the MCP server: `git bisect run` over commits touching `Port/`, each built in a scratch worktree and checked by a replay diff or an `engine_smoke` error signature; finds a planted regression in under 15 steps | 1 | C1 |
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

**Scope decision (2026-10-08, the developer).** Most of Cosmic Shore's content already exists, so
the editor starts where daily work happens - the tools, the data and the models - and **builds no
hierarchy, scene inspector, prefab-override UI or asset browser**. Scene and prefab structure is
the agent's job: `cs-asset` already creates, deletes, reparents and adds components, writes prefab
overrides and applies them (byte-stable), and the `asset-surgery` skill covers the rest; Prisma
shows the result in the running game. Measured that day: **95 FrogletTools** menu items (66 with
`[FrogletTool]` metadata), **1,313 ScriptableObject data files** in **363 script types**, **66 FBX
models**. E1-E3 do not depend on M1 and run beside it.

| # | Checkpoint | Exit criterion | Weeks |
|---|---|---|---|
| **E1** | **Froglet tools** (started 2026-10-08) | EDITOR > TOOLS lists every FrogletTools tool from its source (category, importance, description, docs) and hands any of them to the agent with that source; the top 20 by importance are either native in Prisma (a cs-asset command plus a card action) or a tested agent recipe in `docs/FROGLET_TOOLS.md`; every other tool is marked recipe, Unity-only (with the reason) or retired | 6 |
| **E2** | **Data sets** (started 2026-10-08) | EDITOR > DATA shows every ScriptableObject data file with Unity's labels, headers, tooltips and ranges; edits every field kind the data uses (values, enums, vectors, colours, references picked from the project, list items added and removed) through `cs-asset set`, byte-stable; creates a new data file of a type with the script's defaults | 4 |
| **E3** | **Models** (started 2026-10-08) | EDITOR > MODELS shows every FBX as Unity imports it (meshes, materials, blend shapes, bones, takes, import settings) with a preview; importing a new FBX writes its `.meta` with the settings the game's models use and a stable GUID, and the engine loads it in a prefab like the others | 4 |
| **E4** | **Play mode** | Play from the editor with an incremental script recompile (< 5 s); data-set edits apply to the running game live | 4 |
| **E5** | **Shaders** | Shader Graph JSON edited by a node editor (or by Claude via `asset-surgery`) and translated live; no Unity needed to add a material | 6 |
| **E6** | **Textures and audio** | Import textures and audio with `.meta` generation and the import settings the game uses; GUIDs stable | 3 |
| **E7** | **Animation + UI check** | Animator controllers and RectTransform layouts shown and checked against the game (anchors, overlaps at 3 aspect ratios); the agent edits them through cs-asset - no layout editor | 3 |
| **E8** | **Profiler, tests, release** | Frame profiler, EditMode test runner, Steam upload (steamcmd), Android/iOS from the editor | 4 |
| **E9** | **Cut-over (= M2)** | One 3-week cycle shipped without opening Unity; Unity kept read-only as the reference until M2 + 1 cycle | 3 |

Dropped with the scope decision: the read-only hierarchy/inspector/asset-browser shell (old E1)
and the edit-and-save UI for scenes and prefabs (old E2). If a scene job turns out to need eyes and
hands, it comes back as a narrow tool for that job, not a general hierarchy.

### Decision gates

| Gate | When | Question |
|---|---|---|
| **G1** | end of C1 (~Nov 2) | Does the harness catch known differences? (Seed 3 deliberate ones and check that it reports all 3.) |
| **G2** | 2026-12-15 | Online backend: UGS-over-REST or Steam + own backend |
| **G3** | before any engine build ships to players | FMOD license, legal review, provenance rewrites done |
| **G4** | after C9 | Ship the Steam build on the engine, or keep Unity for the runtime and start M2 anyway |
| **G5** | after E3 | Do TOOLS, DATA and MODELS replace Unity for the developer's daily content work, with the agent doing scene edits? If not, fix that before E4-E8 |
| **G6** | before C8 (~Mar 2027) | Graphics backend: a thin RHI (GL now, Metal later) or wgpu-native (Metal, Vulkan, D3D12 and GLES from one API; a new native dependency). Either way only `CosmicShore.Render` changes: `RenderBoundaryTests` keeps GL there |

## How to run each checkpoint (prompts)

Milestones are worked in Claude Code at the repository root (Prisma's MILESTONES page was retired
on 2026-10-08): paste a prompt below; the session updates `docs/milestones.json` as it moves the
checkpoint. Each one assumes `Port/CLAUDE.md` and this file. Every checkpoint ends with:
`engine_test` green, `engine_smoke` PASS, `unity_isolation_check` ok, docs updated, and this
file's row marked done with the date and the measurement.

**C1 - Parity harness**
> Plan checkpoint C1 of Port/docs/ROADMAP.md. Design an input recorder/replayer that lives in the game's own code (a separate Unity PR - list exactly what it adds under Assets/_Scripts/Utility and why it cannot live in Port/), a Unity-side capture step that writes golden frames, scores, FMOD events and `Random` sequences for a set of seeds, and an `engine_parity` MCP tool that replays in Prisma and diffs against those goldens with the per-channel tolerances in docs/ARCHITECTURE_REVIEW_2026-10-06.md (C9). Include the GitHub Actions job that builds the port and runs engine_smoke for every build scene on each bleeding-edge push, and a generator for the parity scoreboard (docs/PARITY.md + JSON). Measure first: how input reaches the game today (InputScript, Input System devices) and which RNG sources make a match nondeterministic.

**C1b - Bisect**
> Add `prisma_bisect(good, bad, check)` to the MCP server: run `git bisect run` over commits that touch Port/ only, build each candidate in a scratch git worktree, and judge it with a replay diff (engine_parity) or an engine_smoke error signature. Prove it by planting a regression three commits back and finding it.

Built 2026-10-08: `prisma_bisect` (`src/CosmicShore.Mcp/Bisect.cs`, verdicts in `src/Shared/Bisect.cs`, `BisectTests`); proof `python Port/tools/bisect_demo.py --check smoke|parity`. Evidence in docs/milestones.json.

**C2 - Visual completeness**
> First session: plan the Shader Graph compiler for CosmicShore.Render - parse the .shadergraph/.shadersubgraph JSON, emit GLSL for the node types the project uses (census in docs/ARCHITECTURE_REVIEW_2026-10-06.md, D10), port the 30 custom HLSL functions once as a GLSL library, key material families by shader guid, and warn once for an unknown shader. Later sessions (repeat per item): the next hand-written .shader, ParticleSystem module or VFX Graph, ranked by how many on-screen objects use it in the 19 Steam modes; prove each with game_screenshot before/after and the parity diff. Record each item in this file.

C2 items (each: what, where, proof).

| Date | Item | Where | Proof |
|---|---|---|---|
| 2026-10-08 | Shader Graph reader: v2+ object streams and v1 (`JSONnodeData`) files, sub-graphs resolved by guid | `src/CosmicShore.Content/Shaders/ShaderGraphAsset.cs`, `ShaderGraphCatalog.cs` | `ShaderGraphCompilerTests`: all 56 graphs + 31 sub-graphs parse |
| 2026-10-08 | Graph-to-GLSL compiler: 99 node types (2,810 instances), sub-graphs inlined, custom functions called into the library | `ShaderGraphCompiler.cs`, `ShaderGraphNodes.cs` (formulas from com.unity.shadergraph 17.3) | every graph compiles; `CosmicShore --check-shaders`: 56 of 56 link as GLSL 3.30 and GLSL ES 3.00 (RTX 5060) |
| 2026-10-08 | The 30 custom HLSL functions ported once | `src/CosmicShore.Render/Glsl/ShaderGraphLibrary.glsl` (embedded) | coverage test: every Custom Function call is defined |
| 2026-10-08 | Families keyed by shader guid; unknown shader warns once; compiled graphs draw | `MaterialFamilies.cs`, `GraphProgramCache.cs`, `SceneRenderer.Classify` | `RenamedShader_KeepsItsFamily`, `UnknownShader_WarnsOnce_BuiltinsNever`; `--shader-gallery` before/after: the 38 non-family graphs went from flat Lit/Unlit to their graphs; SkimRace replay frames 1100/1500 byte-identical before/after (no regression) |
| 2026-10-08 | Ranking input and coverage report | session report `render.shaders` (scene, shader, route, avg/peak instances); `cs-asset shadergraph-census` → `parity/shaders.json` → scoreboard | scoreboard shaders 10/67 → 54 Approximate / 23 Missing |

Route order in `Classify`: guid family → the older property heuristics (`_DarkColor`/`_BrightColor` ...) → compiled graph → generic fallback (warns). In Menu_Main and SkimRace every on-screen graph still takes a family or a heuristic (session report), so the compiler shows in other modes and the gallery first.

Next items, by on-screen use (rank with `render.shaders` from each mode's report): the heuristic-matched graphs move to the compiler one at a time once a golden shows the compiled one closer (ShepardGraph, DynamicFresnelGraph pair, ChargeCrystal, SuctionGraph); the hand-written `.shader` files that warned in play (`ForcefieldCrackleCapsule`, `ProjectileChargeField`, the `Builtin/211` particle shader); then ParticleSystem, VFX Graph, Timeline.

**C3 - Physics**
> Measure every Rigidbody and OnCollision* use in Assets/_Scripts (17 + 1 at last count). For each, say what contact behaviour it needs. Implement only that in CosmicShore.Engine physics, plus oriented boxes, with tests, and verify with replays that scores match Unity. Never write a general solver: if AstroLeague's ball needs mesh contacts or PhysX-like friction, propose binding BepuPhysics v2 for contacts only behind the trigger/query API, and flag the dependency before adding it.

**C5 - UI and text**
> For every screen in Menu_Main and the in-game HUD, capture the engine at 1920x1080, 2560x1080 and 1024x768, then list layout differences against the Unity goldens with game_ui_at / game_dump_ui evidence. Fix in the engine's RectTransform/layout/TMP code. Separately, plan the clean-room TMP SDF rewrite from docs/LEGAL_REVIEW.md.

**C6 - Online**
> Before G2: put an `INetTransport` seam between NetDriver and NetSocket (TCP stays the first implementation) with tests. After G2: implement the chosen backend from the boundary table in docs/ARCHITECTURE_REVIEW_2026-10-06.md (F14): transport over the internet with relay, Steam auth/achievements via Steamworks.NET, cloud save and leaderboards. Prove it with two machines in different networks (or two GitHub runners) completing a Bloomrush match.

**C7 - Performance**
> Profile the heaviest mode at intensity 4 from its session report (cpu.phaseAvgMs, memory.phaseAvgKB, steady GC) and dotnet-trace; add GPU timer queries to the report on desktop GL. Rank the top 5 costs against the GTX 1060 budget (16.6 ms). Fix the largest one per session, proving each with before/after numbers from the same replay. Known leads: the async `tasks` phase allocates 279-728 KB/frame in Menu_Main (steady GC up to 1.9 ms/frame); loading allocates ~2.5 GB.

**C7 session 2026-10-10 - Bloomrush at intensity 4: the profile and the first fix**

Method (CLAUDE.md "profile first"): the Release player (`dotnet build src/CosmicShore.Player -c Release`),
headless, `--replay Port/parity/replays/bloomrush-i4.json` (4,800 frames: Bootstrap, Menu_Main,
`MinigameBloomrush` from frame 1651 - Rampage's cactus reef, ~19,000 prisms and 177,345 live
behaviours by the end), `--session-report`, `COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off`, no
`--parity-out` (row 7 says why). The hot path: `dotnet-trace collect --profile
dotnet-sampled-thread-time` attached for 25 s once the scene loaded (about 2,800 of its 3,149
frames), ranked on the loop thread with `Port/tools/speedscope_top.py` (`--under GameLoop.Tick`
keeps the game's frame, `--callers` / `--children` walk it). This container has 4 cores and
carried a load average of 10-24 from other agents' builds and a training run during every
measurement, so wall figures move +-15% between identical runs; the proof below is three
INTERLEAVED pairs (HEAD binary, fixed binary) on the same replay plus the counters that do not
depend on load (allocation per frame, GC count, exception count, trace shares).

Before, steady window of `MinigameBloomrush` (3,119 frames; the report's `steady[]`, median of
the three HEAD runs): simulation tick 8.1 ms/frame of which `coroutines` 3.10, `fixed` 2.32
(`triggers` 2.3: `trig.live` 1.04, `trig.shapes` 0.82, `trig.sweep` 0.32), `update` 0.94, `tasks`
0.68, `late` 0.67; scene frames p50 5.0 ms, p95 18.4 ms, 52 of 3,149 over 33 ms; allocation
`coroutines` 155 KB/frame, `tasks` 83, `update` 18, `late` 15; 1.6 gen0 per 100 frames, 0.4-0.9 ms
of GC pause per frame, a gen2 every ~6 s on a 1.25 GB heap; 4,683 exceptions per run. Headless,
so the 16.6 ms GTX 1060 budget here is the CPU half only: the average tick fits, the over-33 ms
frames are the gen1/gen2 collections and the growth bursts.

| # | Cost (loop thread, share of `GameLoop.Tick` in the 25 s trace) | Where | Owner |
|---|---|---|---|
| 1 | **Prefab instantiation 21.6%** (3.84 s): `CloneGameObject` - `AddComponent` through `Activator.CreateInstance(type, nonPublic: true)` (the reflection binder per component, ~9 us and garbage each), `GetFields` per base type per clone, `FieldInfo.GetValue/SetValue` boxing every float, int, Vector3 and enum it copies, the remap pass re-walking the fields; plus 1.3 s of the 3.0 s of GC pauses inside Tick. The flora's `Grow` coroutines mint ~19,000 prisms (~170,000 components) while the reef grows | `Compat/EngineCompat.cs`, `SceneGraph/GameObject.cs` | ENGINE - fixed below |
| 2 | **Trigger pass 18.4%** (3.27 s): `SnapshotLive` 9.2% - `isActiveAndEnabled` re-walks `ChainActive` for ~5,000 enabled colliders every fixed step because every `SetActive` (each clone's included) bumps the ONE global hierarchy epoch; `BuildShapes` 5.9% - `ShapeMath.TryBuild` + `Transform.ValidateWorld` for ~4,000 live colliders per step, nearly all static prisms; sweep 2.2% | `SceneGraph/TriggerPass*.cs`, `GameObject.ChainActive` | ENGINE - fixed in session 2, below |
| 3 | **GC pauses 17.0%** (3.02 s) on the loop thread: 155 + 83 + 18 + 15 KB of garbage per frame (rows 1, 5, 6 and the game) | allocation sites in rows 1, 5, 6 | ENGINE + GAME |
| 4 | **Coroutine stepping 20.6% self** (`CoroutineRunner.RunFrame` 11.5%, `Step` 7.2%, `List.RemoveAll` 1.9%): ~0.4 us per step, but 2,000-2,800 steps per frame (600k-850k per 300 frames in the `CS_PORT_TRACE_CO=1` census, ~3,000 live coroutines) because every growing prism's `CreateBlockCoroutine` polls the per-frame creation budget in a `while (true)` loop | engine `SceneGraph/Coroutines.cs`; game `Assets/_Scripts/Controller/Vessel/Prism.cs:1044` | ENGINE (per-step cost) / GAME proposal: let the budget gate wake waiting prisms from a queue instead of each prism polling every frame |
| 5 | **UniTask continuations 8.2%** (`GameTaskScheduler.RunFrame` -> `UniTaskSource.Run`, `YieldCore`: `GenericPoolManager.BufferMaintenanceAsync` / `RefillAsync` -> `InstantiateAsync`, `VesselPrismController.SpawnLoopAsync`), 83 KB/frame | `Tasks/`, Compat UniTask; the game's pools | mostly GAME |
| 6 | Game `Update`/`LateUpdate` bodies 8.7%, of which `CrystalFlipWave.LateUpdate` 4.6%: `Instantiate(Mesh)` was an unsupported type in the engine, so `RhombusSkinBaker` failed in `Awake`, the component stayed enabled half-built and threw a `NullReferenceException` every frame (1.5 per frame, 4,683 per run, each caught and formatted by `InvokeGuarded`); the rest is `FlipWaveRig.Apply` on the Time crystals, never culled headless because the engine's `Renderer.isVisible` is "enabled and active" | engine gap - fixed below; `isVisible` fidelity | ENGINE gap + GAME |
| 7 | Not the game: with `--parity-out`, `ParityRun.AfterTick` is **28.5% of the loop thread** outside Tick - `HookLoadedGameData` -> `FindGameData` -> `FindObjectsByType` over all 177k behaviours EVERY frame (16.5%), `WriteState` every 30 frames (11.4%). Every `engine_parity` run pays ~2.3 ms/frame for it | `Player/ParityRun.cs` | ENGINE (player harness) - next |

The fix (row 1, with row 6's gap), all in the engine, `Port/tests/CosmicShore.Tests/InstantiateClonePlanTests.cs`:
- `ObjectUtilities.ClonePlan` (`Compat/EngineCompat.cs`): the field set Instantiate copies and remaps is
  resolved ONCE per type - the candidate fields, the initonly ones (reflection still writes those),
  the reference-typed subset the remap pass walks - and copied through a compiled expression (one
  `ldfld`/`stfld` pair per field, no boxing). Without dynamic code (iOS AOT) or on a field shape
  expressions refuse, the copier is the same reflection loop as before. Same fields, same order,
  value types by value, references by reference; `object.MemberwiseClone` is called through an
  open delegate instead of `MethodInfo.Invoke`.
- `GameObject.AddComponent`: one compiled parameterless constructor per component type instead of
  `Activator.CreateInstance(type, nonPublic: true)` on every call, and the `[RequireComponent]`
  lookup cached per type. A type with no parameterless constructor, an abstract type or an AOT
  runtime keeps the Activator path and its exceptions.
- `Instantiate(Mesh)` clones a mesh by value through `Mesh.CopyTo` (which now also carries the wide
  UV channels and topologies), as the original engine does; the omni crystals' flip wave runs.

After, same replay, same Release flags, interleaved with the HEAD binary (B = HEAD, A = fixed; the
steady `MinigameBloomrush` window; ms are wall time per frame under the load above):

| Run | tick sum | `coroutines` | `fixed` | `tasks` | `late` | KB/frame co / tasks / update / late | gen0 per 100 f | GC pause ms/f | over 33 ms | run MB allocated | gen0/1/2 | exceptions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| B1 | 8.46 | 3.10 | 2.69 | 0.73 | 0.67 | 155 / 83 / 18 / 14 | 1.6 | 0.62 | 63 | 3,835 | 194/116/17 | 4,683 |
| A1 | 7.01 | 2.57 | 2.20 | 0.60 | 0.43 | 89 / 61 / 11 / 0.2 | 1.0 | 0.45 | 49 | 3,353 | 161/113/13 | 1 |
| B2 | 7.43 | 2.61 | 2.31 | 0.66 | 0.61 | 155 / 83 / 18 / 15 | 1.6 | 0.44 | 52 | 3,841 | 188/114/11 | 4,683 |
| A2 | 7.26 | 2.56 | 2.35 | 0.65 | 0.46 | 89 / 61 / 11 / 0.2 | 1.0 | 0.37 | 38 | 3,354 | 161/114/13 | 1 |
| B3 | 8.11 | 3.17 | 2.32 | 0.68 | 0.67 | 155 / 83 / 18 / 15 | 1.6 | 0.95 | 53 | 3,842 | 189/114/13 | 4,683 |
| A3 | 8.00 | 2.65 | 2.41 | 1.17 | 0.49 | 89 / 61 / 11 / 0.2 | 1.0 | 0.84 | 54 | 3,353 | 159/111/13 | 1 |

Medians: `coroutines` 3.10 -> 2.57 ms/frame (-17%), `late` 0.67 -> 0.46 (-31%, the exception flood),
tick sum 8.11 -> 7.26 (-10%); garbage 109 KB/frame less (155 -> 89, 83 -> 61, 18 -> 11, 15 -> 0.2),
gen0 collections 1.6 -> 1.0 per 100 frames, the whole run 3,841 -> 3,353 MB (-13%), exceptions
4,683 -> 1 (the remaining one is `PortSquadView` on an empty profile). The after trace (same 25 s
window) puts the clone path at 10.8% of Tick (was 21.6%) and GC inside Tick at 13.2% (was 17.0%);
`CopyFields` is 47 ms of the window (was ~1 s across `SerializedFieldCandidates`,
`FieldAccessor.Get/SetValue` and the boxing GC). The `fixed`/`update` columns are unchanged, as they
should be - what moves between the B rows is the machine. Not touched: the GTX 1060 WINDOW figures
against Unity, the GPU timer comparison and the SustainedLowLatency A/B (the instruments landed
2026-10-08).

Next, in order of the table: (2) the trigger pass - a per-subtree activity version instead of the
global epoch so a pool's `SetActive` stops invalidating every collider's `ChainActive`, and a
world-matrix stamp so `BuildShapes` keeps a static prism's shape; (7) the parity harness - hook
`GameDataSO` on scene load and asset load only, and give `WriteState` an index instead of a
177k-behaviour scan; (1b) the container constructors the remap pass still makes through the
binder (`CloneDictionaryValue`, `CloneHashSetValue`: 2% of Tick); (4) the coroutine runner's
per-step cost, after the game-side proposal above; the game's `Renderer.isVisible` culling for
`CrystalFlipWave`.

**C7 session 2 (2026-10-10) - the trigger pass**

Same method as session 1 (Release player, `bloomrush-i4.json` headless with `--session-report`,
HEAD binary against the fixed binary in INTERLEAVED pairs, the load-independent counters as the
evidence), with one addition: this working tree is shared with an agent editing `Assets/_Scripts`
during the session, so BOTH binaries were built against a frozen `git archive HEAD` copy of the game
scripts (`-p:LiveAssetsDir=... -p:LiveSrcDir=obj/live-src-frozen`, the switch
`SyncUnitySources.targets` provides) - an A/B whose two sides compiled different game code proves
nothing. Scratch evidence: `scratchpad/c7b/` (reports `ab2B*`/`ab2A*`, parity `parB`/`parA`,
`verifyA`, `traceA`).

Before (HEAD = 1ea3d23b5, session 1's fix; steady `MinigameBloomrush` window, 3,119 frames, median of
the three B runs): `fixed` 1.98 ms/frame of which `triggers` 1.95 - `trig.live` 0.893, `trig.shapes`
0.768, `trig.sweep` 0.270; the trigger pass was 24.7% of the loop thread's `GameLoop.Tick` in session
1's after-trace (`SnapshotLive` 11.2%, of it `GameObject.ChainActive` 4.1% + 5.5% self walking the
enabled set; `BuildShapes` 8.9%, of it `ShapeMath.TryBuild` 5.2% with `Transform.ValidateWorld` 3.4%;
`SweepCandidates` 2.8%). Three causes, all engine: (1) `activeInHierarchy` was memoised against ONE
global epoch that every `SetActive` bumped - each pooled clone's, each prism's - so every step
re-walked the chain of all ~5,000 enabled colliders; (2) `SnapshotLive` asked every enabled collider
`isActiveAndEnabled` every step - two cache misses each (the collider, its object) to learn nothing
had changed; (3) `BuildShapes` rebuilt every live collider's world shape every step - five
parent-chain pose reads and the oriented-box axis maths per collider, ~4,000 of them static prisms.

The fix (`SceneGraph/GameObject.cs`, `Transform.cs`, `TriggerPass*.cs`, `Compat/EngineCompat.cs`,
`UI/PackageComponents.cs`, `Physics/ShapeMath.cs`; tests
`tests/CosmicShore.Tests/TriggerPassActivityAndShapeCacheTests.cs`, 7 facts):
- **`activeInHierarchy` is a field**, brought up to date by the only places it can change -
  `SetActive`, `ActivateSceneRoots`, `Transform.SetParent`, the destroy detach (the RectTransform
  conversion keeps every object and parent, so nothing to do). A change costs the subtree whose
  effective state flips (`PropagateHierarchyActive`, iterative) - the subtree
  `NotifyHierarchyActiveChanged` walks right after it anyway - and a read is a field. The global epoch
  and `ChainActive` are gone; `WalkActiveInHierarchy` keeps the chain walk as the reference.
- **The live set is maintained by events**, not rebuilt: `TriggerPass.NoteLiveness(collider)` from
  `Register`, `SetEnabled` and the activity flip (`GameObject.NoteCollidersLiveness` on every object
  whose field flips), `RemoveLive` from `Unregister`. `SnapshotLive` sorts a parallel list of
  sequence numbers and places the colliders - it touches no collider until it is placed. Arrivals
  (`NoteArrived`, the query scene) are untouched.
- **A collider caches its world shape** (`Collider.TryGetShape`) with the `Transform.WorldStamp`
  it was built at - the stamp changes exactly when the composed pose was recomputed, bitwise - and a
  bitwise copy of the fields `ShapeMath.TryBuild` reads (`isTrigger`; box center/size; sphere
  center/radius; capsule center/radius/height/direction; mesh reference and its `bounds`). A hit is
  what a fresh `TryBuild` would compute, bit for bit; `BuildShapes` runs inside
  `Transform.BeginReadOnlyPass` so a pose that did change is validated once per transform with
  ancestors shared, not once per read per collider.
- `COSMIC_SHORE_VERIFY_TRIGGERS=1` now also checks, every 30 frames, every registered object's
  field against the chain walk and every live collider's cached shape against a fresh build.

After, interleaved pairs on the frozen-script binaries (B = HEAD, A = fixed; ms are wall time per
frame on this 4-core container at load average 5-13; the `KB/frame`, gen0, exception and
steady-frame columns are load-independent and must not move):

| Run | tick sum | `fixed` | `triggers` | `trig.live` | `trig.shapes` | `trig.sweep` | `coroutines` | `update` | `tasks` | `late` | KB/frame co / tasks / update / fixed | gen0 per 100 f | over 33 ms | p50 / p95 ms | run MB | exceptions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| B1 | 5.46 | 1.98 | 1.95 | 0.893 | 0.753 | 0.268 | 1.80 | 0.66 | 0.45 | 0.35 | 89 / 61 / 11 / 0.6 | 0.9 | 28 | 3.7 / 12.7 | 3,351 | 1 |
| A1 | 6.53 | 1.36 | 1.34 | 0.201 | 0.756 | 0.339 | 2.77 | 1.04 | 0.60 | 0.48 | 90 / 61 / 11 / 0.6 | 0.9 | 48 | 4.7 / 17.2 | 3,358 | 1 |
| B2 | 5.32 | 1.97 | 1.94 | 0.861 | 0.768 | 0.270 | 1.67 | 0.67 | 0.46 | 0.34 | 89 / 61 / 11 / 0.6 | 0.9 | 27 | 3.4 / 12.4 | 3,352 | 1 |
| A2 | 4.17 | 0.85 | 0.82 | 0.113 | 0.448 | 0.228 | 1.77 | 0.62 | 0.45 | 0.30 | 90 / 61 / 11 / 0.6 | 0.9 | 31 | 3.2 / 9.9 | 3,356 | 1 |
| B3 | 7.00 | 2.20 | 2.16 | 0.979 | 0.839 | 0.298 | 2.60 | 0.87 | 0.58 | 0.43 | 89 / 61 / 11 / 0.6 | 0.9 | 44 | 4.5 / 17.3 | 3,354 | 1 |
| A3 | 4.84 | 1.05 | 1.03 | 0.145 | 0.570 | 0.278 | 2.03 | 0.69 | 0.47 | 0.35 | 90 / 61 / 11 / 0.6 | 0.9 | 33 | 3.3 / 12.1 | 3,355 | 1 |

Medians: `triggers` 1.95 -> 1.03 ms/frame (-47%), `trig.live` 0.893 -> 0.145 (-84%), `trig.shapes`
0.768 -> 0.570 (-26%; A1 ran at load 8.7 and is the outlier), `fixed` 1.98 -> 1.05, tick sum
5.46 -> 4.84. The `coroutines`/`update`/`tasks`/`late` columns move with the machine, not the change
(A1 is the slowest run of the six on every one of them); allocation per frame, gen0 per 100 frames,
the run's MB and the exception count are identical on both sides. An intermediate pair set (eager
field + shape cache, before the maintained live set; `scratchpad/c7b/abB*`/`abA*`) measured
`trig.live` 0.912 -> 0.334 and `trig.shapes` 0.793 -> 0.538: the field removed the chain walk, the
maintained set removed the remaining per-collider touch. The after-trace (15 s of the scene, the
loop thread) puts the trigger pass at 14.6% of `GameLoop.Tick` (was 24.7%): `SnapshotLive` 1.9%
(was 11.2%), `BuildShapes` 7.3% (was 8.9%; 5.8% of it is its own body - the stamp validation and
the shape copy per collider, see Next), `SweepCandidates` 3.3%.

Contact identity, same two binaries with `--parity-out` (what `engine_parity {against: self}`
diffs): `events.jsonl` 1,507 lines on both sides - `contact` 98 = 98 identical in order, `fmod`
1,394 identical, `collision` 0, `game` 10 identical bar one `t` (`scene:Authentication` at 0.767 vs
0.800 s, the Authentication load-time drift `parity/README.md` documents; it is the only line of the
1,507 whose `t` differs); `state.jsonl` 160 lines identical; `transforms.jsonl` 60 identical. The
verify run (`COSMIC_SHORE_VERIFY_TRIGGERS=1`, fixed binary, 158 checks over the 4,800 frames): zero
DIFFERENT; at frame 4,800, 4,024 live colliders equal to the 4,024 the walk finds in identical
order, the field equal to the chain walk on all 19,465 registered colliders' objects, every cached
shape equal to its fresh build.

Next for the pass: `BuildShapes`' remaining 5.8% is one `WorldStamp` validation (collider ->
object -> transform -> parent, cache misses on static prisms) plus a 100-byte shape copy per live
collider per step; a push-based invalidation (a moved-flag written down the subtree from
`MarkMoved`, so a static collider is never touched) would remove it, and is a change to the world
cache's comparison contract, so it gets its own session. Then, in the table's order: (3) GC pauses
on the loop thread (`PollGCWorker` 30.8% of Tick in the after-trace: the 90 + 61 + 11 KB/frame of
`coroutines`/`tasks`/`update` garbage), (7) the parity harness's per-frame `FindObjectsByType`,
(1b) the container constructors still on the binder, (4) the coroutine runner's per-step cost.

**C8 - Platforms**
> Produce the Windows, Android and iOS builds from the launcher, run the platform checklist (boot, a full match, audio, input, suspend/resume) and log every failure as a session report. For gate G6, compare a thin RHI with a Metal backend against wgpu-native: what the renderer's GL calls map to, what changes in shaders, the effort, and the new dependency.

**E1 - Froglet tools**
> Continue checkpoint E1. The TOOLS page (`src/CosmicShore.Launcher/LauncherApp.Editor.cs`) lists the FrogletTools that `cs-asset tools` reads from source (`src/CosmicShore.AssetTool/EditorData.cs`; MCP `asset_froglet_tools`). Take the next of the top 20 by importance that is not yet native or a recipe: read its source under Assets/, then either (a) port a reader as a cs-asset command plus a card action and prove it by comparing its output with what the Unity tool reports on the same files, (b) write a tested agent recipe for a writer in `docs/FROGLET_TOOLS.md` (cs-asset steps, what to check), or (c) mark it Unity-only with the reason. Never change Assets/.

**E2 - Data sets**
> Continue checkpoint E2. Measure with `asset_datasets` / `asset_dataset` which field kinds the 363 data types use that the DATA page cannot edit yet (references, list items, nested objects, flag enums, AnimationCurve, Gradient), rank them by how many files use them, and add editing for the top ones through `cs-asset set`: references picked from the project's assets of the field's type, list items added and removed. Then "new data file of this type" from the script's defaults (cs-asset already builds defaults from the C# type). Every write must round-trip byte-stable (`cs-asset roundtrip`).

**E3 - Models**
> Continue checkpoint E3. MODELS shows each model (FBX, and .blend/.ma/.mb through the installed Blender/Maya, `DccModelConverter`) through `cs-asset model` / `model-preview` (MCP `asset_model`, `asset_model_preview`), in the game's material colours (`ModelMaterialUsage`: the prefabs that draw it), with a drag turntable and VIEW IN ENGINE (`--view-model`). Next: blend-shape sliders in the preview and the viewer, then importing a new FBX - measure the ModelImporter settings across the 66 existing metas, write a new model's `.meta` with those and a stable GUID, and prove the engine loads it in a prefab like the others.

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
| C1 - parity harness | in progress | 2026-10-10 | Engine side done and the Unity half (board T-3) has landed on this branch (`1a397c3e3`, `646d599e0`: ReplayFile/Recorder/Player, DeterministicSession, ParityProbe, ParityCapture). The engine now hands `--replay` to the game's ReplayPlayer and DeterministicSession (`ParityRun.BeginSession`), hooks `GameDataSO` at asset load (the menu's `OnLaunchGame` reached events.jsonl), refuses an FMOD event no loaded bank carries (strings-bank GUID index; GUIDs.txt is stale, the Bootstrap music IS in the bank) and plays every case as a fixed returning-user profile (`Port/parity/profile`). `skimrace-status` (600 status frames) flies the vessel through the game's ReplayPlayer; `engine_parity {against: self}`: 15 PASS, 1 FAIL (bloomrush-i4 on the Authentication load-time drift, B-1), 5 MISSING. Still owed: the editor capture run (`FrogletTools > Parity > Capture Goldens`, as the `parity` player) for the goldens, and CI's first GitHub run |
| C7 - performance parity | in progress | 2026-10-10 | Bloomrush intensity 4 profiled headless in Release (session report + dotnet-trace): prefab instantiation was 21.6% of the loop thread's frame; fixed (per-type clone plan, cached constructors, `Instantiate(Mesh)`), proven on three interleaved A/B pairs of the same replay - 109 KB/frame less garbage, gen0 1.6 -> 1.0 per 100 frames, exceptions 4,683 -> 1, `coroutines` 3.10 -> 2.57 ms. Session 2 the same day: the trigger pass (24.7% of the frame after session 1) - `activeInHierarchy` an eagerly maintained field instead of a global-epoch memo, the live collider set maintained by events, each collider's world shape cached on its transform stamp - `triggers` 1.95 -> 1.03 ms/frame on three interleaved pairs, contact/state/transform channels identical. Tables under "C7 session 2026-10-10" and "C7 session 2 (2026-10-10)" above |
