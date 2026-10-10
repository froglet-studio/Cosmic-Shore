# Architecture review response (2026-10-06)

An outside review proposed 20 changes to Amoebius (A1-H20). It was written from the project
description, not the source, so every item was checked against the repository before deciding.
This file records what the repository showed, the decision, and where the work went.

Two rules decided the close calls:

- A decision already recorded in `Port/docs/` stands, unless the review's reasoning changes it.
- Each item is weighed by what it buys toward **M1, gameplay parity (~Q2 2027)**, in the next
  eight months.

Measurements in this file were taken on 2026-10-06 on the branch `cece/focused-planck-cj46y3`.

## Summary

| # | Review item | Verdict | Where it went |
|---|---|---|---|
| A1 | Drop the namespace rewrite, or at least emit `#line` | `#line` done; the rewrite stays | done |
| A2 | IL-weave RPCs instead of inserting text | Silent skips made loud; weaver deferred | done; C6 |
| A3 | Rewrite only changed files | Done | done |
| B4 | Compile upstream source for MIT packages | Policy kept: only UniTask is MIT, and it is not worth the reversal | table below |
| B5 | Make the Jobs/Burst stand-ins fast | Accepted, after a profile | C7 |
| C6 | Unity-exact `Random` and `Mathf` | `Mathf` already matches; `Random` via goldens | C1 |
| C7 | Execution order from every source, in every phase | Done | done |
| C8 | Serialization audit and Unity's rules | Done | done |
| C9 | Parity tolerances per subsystem | Decided (table below) | C1 exit |
| D10 | A Shader Graph compiler | Accepted as C2's main route | C2 |
| D11 | A backend abstraction (or wgpu) | Boundary enforced now; RHI vs wgpu decided at new gate G6 | done; G6 |
| D12 | Shuriken first, VFX Graph approximate | Accepted (already the plan) | C2 |
| E13 | Bind Bepu or Jolt for contacts | Recorded scope kept, with a decision rule | C3 |
| F14 | Abstract the transport; define the UGS boundary | Accepted; the seam opens C6 | C6, G2 |
| G15 | Instrument before optimising | Done (GPU timer queries in C7) | done; C7 |
| G16 | GC configuration; NativeAOT | GC stated, hot-path reflection removed; no NativeAOT for M1 | done; C8 |
| H17 | Acceptance criteria on board items and milestones | Done | done |
| H18 | A generated parity scoreboard | Accepted | C1 |
| H19 | Budget and fallback for milestone sessions | Done; worktrees deferred | done |
| H20 | `prisma_bisect` | Accepted, after the harness | new C1b |

"Done" means implemented, tested and measured in this pass. The work is recorded as checkpoint
**C0 (Foundations)** in `ROADMAP.md` and `milestones.json`.

---

## A. Source sync and compile

### A1. The namespace rewrite

**Review.** Declare `namespace UnityEngine`, `TMPro`, `Unity.Netcode` and so on in the engine,
so `Assets/_Scripts` compiles with no copy step. If the rewrite stays, emit `#line`.

**Found.**
- The rewrite is a table-driven pass in `src/CosmicShore.Live/SyncUnitySources.targets`.
- Several source namespaces fold into one engine namespace:
  - `TMPro`, `UnityEngine.UI` and `UnityEngine.EventSystems` all become `CosmicShore.Engine.UI`.
  - `Unity.Netcode`, `UnityEngine.Networking` and `Unity.Services.Multiplayer` all become
    `CosmicShore.Engine.Networking`.
- Engine namespaces are referenced from:
  - 783 files under `Port/src` (5,122 occurrences), of which 484 are the hand-ported
    `CosmicShore.Game`;
  - 127 test files.
- No `#line` was emitted, so errors and stack traces pointed at `obj/live-src`.
- No document records why the engine uses its own namespaces.

**Decision.** Keep the rewrite for M1.
- `#line` delivers the review's main benefit in half a day. The rename would take 3-5 days across
  ~900 files and buys no parity.
- The copy step also inserts the RPC prologue (A2). It is the one place to adapt Unity-side
  changes, and it now costs ~70 ms (A3).
- Revisit at E3 (play mode with a < 5 s incremental recompile) if the copy step ever shows up in
  that timing.
- **Legal question for G3:** may a non-Unity product declare Unity's namespaces? It is raised
  here, not assumed either way.

**Done.** Every synced file starts with `#line 1 "<absolute path under Assets/>"`. Diagnostics,
stack traces, debugger steps and `[CallerFilePath]` now name the original file and line. For
example:
- a compiler warning now reads `Assets/_Scripts/UI/MiniGameHUD.cs(12,7)`;
- the known `Squad` exception now traces to `Assets/_Scripts/System/Squads/Squad.cs:line 19`.

### A2. The RPC prologue

**Review.** Replace the text-inserted RPC prologue with Mono.Cecil IL weaving, or a source
generator.

**Found.**
- Insertion is a regex plus a hand scanner.
- All 142 RPC attributes in the tree (102 `ClientRpc`, 40 `ServerRpc`) get the prologue.
- These shapes were skipped **silently**:
  - generic methods;
  - attributes split across lines;
  - `#if` between the attribute and the method;
  - non-void returns;
  - Netcode 2's `[Rpc(SendTo...)]`, which the game does not use yet.

**Decision.** The risk was the silence, not the technique.
- A skipped RPC runs locally instead of over the network, and it surfaces as a multiplayer bug
  far from its cause.
- So every skip is now reported.
- The weaver is deferred:
  - Mono.Cecil would be a new dependency.
  - A Roslyn syntax-tree rewrite (1-2 days) is the better upgrade, done when the first
    unsupported shape appears or when C6 starts.

**Done.**
- The sync reports each RPC it cannot intercept as warning **`PRISMA001`**, at the original file
  and line.
- The incremental manifest keeps the warning, so an unchanged file reports it on every build.
- Today's tree gives 0 warnings.
- **Negative control.** A throwaway script holding a generic RPC, a split attribute and
  `[Rpc(SendTo.Server)]` produced exactly three warnings, at lines 4, 6 and 9. A comment that
  mentions `[ServerRpc]`, and a normal RPC, produced none. The script was deleted afterwards.

### A3. Incremental sync

**Found.** Every build re-read and re-transformed all 1,840 scripts. The review's count of
2,090 includes the `Editor/` and `Tests/` folders, which are skipped.

**Done.**
- A manifest records each source file's size and timestamp, so unchanged files are neither read
  nor transformed.
- The manifest is stamped with the rules file's timestamp and the source path. A rule change, or a
  moved checkout (which changes every `#line`), re-maps everything.
- Measured: ~1 s per sync call before, ~70 ms after. There are two calls per build, so about 1.9 s
  is saved on each build.

---

## B. Compat: real source where licenses allow

### B4. Vendoring upstream source

**Review.** Compile upstream source for the MIT packages (UniTask, Unity.Mathematics,
Unity.Collections, Netcode for GameObjects). Keep re-implementations for the Unity Companion
License (UCL) and proprietary ones. Document the decision per package.

**Found.**
- Every package in Compat is a behaviour-level re-implementation.
- The policy is recorded in four places:
  - `PROGRESS_2026-09-29.md`: "No Unity or third-party source is copied."
  - `src/CosmicShore.Compat/README.md`.
  - `THIRD_PARTY_NOTICES.md`.
  - `LEGAL_REVIEW.md`, which gives the reasoning: API re-implementation (Google v. Oracle), the
    Unity EULA, and UnityCsReference being reference-only.
- The license of each exact version the game pins was read from that package's own `LICENSE.md`:

| Package | Version | License | Decision |
|---|---|---|---|
| UniTask (Cysharp) | git, `Packages/manifest.json` | **MIT** | Re-implemented (policy). See below |
| Unity.Mathematics | 1.3.2 | UCL, "for Unity-dependent projects" | Must stay re-implemented |
| Unity.Collections | 2.6.6 | UCL | Must stay re-implemented |
| Netcode for GameObjects | 2.5.0 | UCL (the 2.x release branches; only the old `develop` branch says MIT) | Must stay re-implemented (`Engine/Networking`) |
| Unity Transport | 2.6.0 | UCL | Not used; Amoebius has its own wire format |
| Entities / Entities Graphics | 1.4.2 / 1.4.15 | UCL | Must stay re-implemented |
| Burst | 1.8.29 | UCL | Must stay re-implemented (inline stand-in) |
| Cinemachine | 3.1.2 | UCL | Must stay re-implemented |
| Timeline | 1.8.9 | UCL | Must stay re-implemented |
| Animation Rigging | 1.3.0 | UCL | Must stay re-implemented |
| Input System | 1.14.2 | UCL | Must stay re-implemented (`Engine/InputSystem`) |
| DOTween | (Asset Store) | Proprietary | Must stay re-implemented |
| FMOD Studio | runtime binary | FMOD license, per title | The real native runtime is used, not its source (check at G3) |

**Decision.** The recorded policy stands.
- Three of the four packages the review believed MIT are under the UCL. The UCL licenses code for
  Unity-dependent projects only, which is the opposite of what Amoebius is.
- That leaves UniTask, which is MIT. Its upstream source is built on Unity's PlayerLoop
  (`UnityEngine.LowLevel`, `PlayerLoopHelper`), `AsyncOperation` and `UnityWebRequest`, so Amoebius
  would still have to implement those.
- Amoebius's UniTask is about 1.2k lines covering what the game uses.
- Vendoring it would reverse a recorded decision for little gain, and would need updated notices
  and a legal sign-off (G3).
- Revisit only if C1's harness finds UniTask fidelity bugs.

### B5. Fast Jobs and Burst stand-ins

**Found.**
- `Compat/Jobs/Jobs.cs` runs every job inline at `Schedule` time, in index order.
- `JobHandle.IsCompleted` is always true.
- Unity.Mathematics is scalar code. There is no SIMD anywhere.

**Decision.** Accepted, but measure first. It moves to C7.
- Inline jobs are deterministic, which C1's replays need. Parallel jobs stay deterministic only
  when each index writes its own output, so each job is checked before it moves.
- G15 now times each loop phase. C7 moves a job to `Parallel.For` only when a profile puts it among
  the top five costs.
- C7 adds a SIMD `floatN` only if math still shows up after that.

---

## C. Engine core and fidelity

### C6. Unity-exact `Random` and `Mathf`

**Found.**
- `Engine/Math/Random.cs` is xorshift128 with Unity's known `InitState` seeding. `value` and both
  `Range` overloads follow the documented behaviour.
- `onUnitSphere`, `insideUnitSphere` and `insideUnitCircle` use rejection sampling. That consumes a
  variable number of draws, so the sequence diverges from Unity after their first use.
- `rotation` and `rotationUniform` are unverified.
- `Mathf.Approximately`, `Repeat`, `LerpAngle` and `DeltaAngle` already match Unity's documented
  formulas.

**Decision.** Accepted, through goldens rather than Unity's source (the provenance policy).
- C1's Unity PR (the same one that adds the replay recorder) adds a capture step. It writes N draws
  of every `Random` API, for a set of seeds, to `Port/tests/goldens/random.json`.
- Amoebius's tests compare those sequences exactly. The geometric draws are re-derived until they
  match.

### C7. Execution order

**Found.**
- Only `[DefaultExecutionOrder]` was read.
- Update, LateUpdate and FixedUpdate were sorted. Awake and OnEnable ran in hierarchy order, and
  Start was first-in, first-out.
- The per-script `executionOrder` in `.meta` files was never read. Eleven metas set one: 6 game
  scripts and 5 FMOD scripts.
- `ProjectSettings/MonoManager.asset` does not exist in this project. Modern Unity keeps the
  Script Execution Order settings in each script's `.meta`, so there are two sources, not three.

**Done.**
- `AssetDatabase.ScriptExecutionOrders()` reads the metas, and `ContentRuntime` registers them at
  boot. Ten of the eleven resolve to runtime types.
- A `.meta` order overrides the attribute, as in Unity.
- A scene load wakes behaviours (Awake, then OnEnable) in (order, hierarchy) order. If an Awake
  deactivates a later object, that object stays asleep.
- Start drains in execution order.
- 5 tests: `tests/CosmicShore.Tests/ExecutionOrderTests.cs`.

### C8. Serialization audit and Unity's rules

**Found.** The reader was far more permissive than Unity:
- It accepted any instance field, including private ones without `[SerializeField]`.
- It accepted property setters and `m_` spellings on script types.
- It skipped `[field: SerializeField]` backing fields.
- It never called `OnAfterDeserialize`.
- It had no `[SerializeReference]` support.

**Done.**
- `Content/Serialization/UnitySerializationRules.cs` states Unity's rules for script types:
  - The field is public or `[SerializeField]`, and not `[NonSerialized]`, readonly or const.
  - `[field: SerializeField]` backing fields are read under `<Name>k__BackingField`.
  - `[FormerlySerializedAs]` names are honoured.
  - The field's type is one Unity can serialize:
    - Allowed: primitives, enums, strings, object references, engine built-ins, `[Serializable]`
      classes and structs (generic ones too), and one-dimensional arrays or `List<T>` of those.
    - Not allowed: `Dictionary`, interfaces, abstract types, nested collections.
  - Engine and Compat built-ins keep their native `m_` aliases.
- The reader applies these rules and calls `ISerializationCallbackReceiver.OnAfterDeserialize`.
- `cs-asset serialization-audit [path...] [--json]` classifies every YAML key:

  | Class | Meaning |
  |---|---|
  | DROPPED | Unity reads it, Amoebius doesn't |
  | EXTRA | Amoebius reads it, Unity doesn't |
  | STALE | Neither reads it |
  | MANAGED | A `[SerializeReference]` block |

  It exits 1 on any DROPPED or EXTRA key, so it can gate CI.

**Audit result** (all of `Assets/`: 14,319 script instances, 112,941 keys):

| | Before | After |
|---|---|---|
| DROPPED | 52: vessel `ResourceSystem` levels (Charge, Mass, Space, Time) on 13 vessels, all authored as 0, so the bug was latent | 0 |
| EXTRA | 78 keys over 36 fields: stale references and zeros loaded into properties and private fields | 0 |
| STALE | | 7,070 keys over 630 fields (spot-checked: genuinely left over) |
| MANAGED | | 0 |

Adding the field-type rule afterwards changed no count. Its unit test proves it skips what Unity
skips.

**Next.** Load `[SerializeReference]` when the game first uses it. The audit reports any
`references:` block as MANAGED, so that moment will not be missed.

### C9. Parity tolerances

**Decided now, before C1 records any goldens.** The values are a starting point; gate G1
calibrates them by planting three known differences and checking that all three are reported.
A channel within tolerance counts as *Faithful*; one with a deliberately looser bar (VFX Graph)
is *Approximate*.

| Channel | What is compared | Tolerance |
|---|---|---|
| Gameplay state | Score, crystals, lives, timers, match result, at each replay checkpoint and at the end | Exact |
| Random draws | The sequence per seed (C6 goldens) | Exact |
| Game and FMOD events | Ordered event sequence | Exact order and count; timestamps within one fixed step (0.04 s) |
| Transforms | Vessel and projectile position and rotation at checkpoints during a replay's first 10 s | Position within 1e-4 of the distance from the origin (1 mm per 10 m); rotation within 0.1°. After 10 s, compare outcomes, not paths |
| Physics | Contact events and their outcomes | Exact events and outcomes; trajectories as transforms |
| Frames | Every captured frame, with particle regions masked until C2 | SSIM ≥ 0.97 in gameplay, ≥ 0.98 on UI screens (C2, C5) |
| Audio mix | Bus and VCA levels at snapshot changes | Within 1 dB |
| Performance | p95 frame time per mode and intensity | Within 10% of Unity (C7) |

Why these bars:
- Gameplay state is discrete, so any difference is a bug.
- Random is integer math, so it is either identical or wrong.
- The fixed step quantises when events fire.
- Small float differences grow chaotically in free flight, which is why transforms are compared
  as paths only for the first 10 s.
- PhysX and Amoebius's contacts will never match bit for bit.
- Rendering differs at the bit level by design.

---

## D. Rendering

### D10. A Shader Graph compiler

**Found.**
- `ShaderPropertyCatalog` reads only a graph's properties.
- `SceneRenderer.Classify` picks one of 9 material families **by shader name**. An unknown graph
  silently renders as Lit or Unlit, with only its base colour and texture.

**Census.**

| Asset | Count |
|---|---|
| Shader Graphs | 56, plus 31 sub-graphs |
| Node instances | 2,165 |
| Node types | 85. Most are arithmetic, texture sampling, UV, time, Fresnel and blend |
| Custom Function nodes | 74, calling 30 distinct HLSL functions. Most are the game's own `Prism*` functions; 9 come from include files |
| Hand-written `.shader` files | 38 (21 in `_Graphics`, 14 in a third-party folder) |

**Decision.** Accepted. A compiler is C2's main route, replacing "translate the remaining
shaders one by one". It has four parts:
1. A graph-to-GLSL compiler for the node types this project uses (10-20 days).
2. The 30 custom functions, ported to GLSL once, as a library.
3. The hand-tuned families, kept as the fast path for the hottest materials (prisms, vessels).
4. The 38 `.shader` files, which a graph compiler cannot reach, hand-translated in order of how
   often they appear on screen.

The same work keys the mapping on the shader's guid instead of its name (a renamed graph falls
back silently today), and an unknown shader logs a one-time warning naming the asset.

### D11. A backend abstraction (or wgpu)

**Found.**
- There is no backend interface.
- GL calls sit in 9 of the 11 `Render` files (about 77 in `SceneRenderer`) and in
  `PlayerWindow`'s present and read-back.
- The launcher (ImGui) and the legacy `Client` also call GL.
- Recorded decisions:
  - `ROADMAP.md` lack #9: a Metal (or Vulkan + MoltenVK) backend is needed before Apple removes GL,
    and "the material families are the right seam".
  - C8.4: a Metal backend plan approved.

**Decision.** The recorded timing stands. M1 is Windows-first, and GL runs on Windows, Linux and
Android and still runs on iOS.
- **Accepted now: the review's boundary rule.** No backend-specific code goes outside
  `CosmicShore.Render`, except `PlayerWindow`'s present and read-back.
- `tests/CosmicShore.Tests/RenderBoundaryTests.cs` fails when any other file imports the GL
  binding.
  - It exempts the launcher and the legacy `Client`.
  - Its negative control: a probe file outside the boundary made it fail and name the file.
- The review's choice of approach becomes **gate G6**, decided before C8 with the C8.4 plan as
  input:
  - (a) a thin RHI: GL now, Metal later;
  - (b) wgpu-native: one API over Metal, Vulkan, D3D12 and GLES, but a new native dependency.

### D12. Particles: Shuriken first, VFX Graph approximate

**Found.**
- `ParticleSystem` holds the module data but only counts particles: it keeps no positions.
- `Render` has no particle code.
- `VisualEffect` is identity plus exposed properties ("the port has no VFX runtime").
- 26 prefabs use ParticleSystem; there are 2 VFX Graphs.

**Decision.** Accepted. This already matched C2.
- The Shuriken modules those 26 prefabs use: simulated on the CPU, drawn as billboards or meshes
  (6-10 days).
- The 2 VFX Graphs: drawn by a simple billboard family and marked *Approximate* on the
  scoreboard (H18).

---

## E. Physics

### E13. Bind a contact solver

**Found.**
- No physics library is referenced.
- Triggers use sort-and-sweep. Spheres are exact; boxes are axis-aligned with rotation ignored;
  mesh colliders are their bounds.
- `Rigidbody` integrates velocity with damping. There is no gravity and no contact, so
  `OnCollision*` never fires.
- Recorded decisions: C3 is "contact resolution for the 17 Rigidbody users", and lack #5 says
  "No general-purpose solver".

**Census.**
- 17 scripts reference `Rigidbody`. They set `linearVelocity` (12 sites), `isKinematic` (7),
  `useGravity` (3) and `velocity` (3), and call `AddTorque` (2).
- No joints, `AddForce`, `MovePosition` or `CharacterController` anywhere.
- `OnCollisionEnter` and `OnCollisionStay` appear only in `AstroLeagueBall`.

**Decision.** The recorded scope stands. The review's warning becomes C3's decision rule: **never
write a general solver in-house.**
- If the needs stay where the census puts them, implement exactly that:
  - velocity-driven bodies;
  - gravity on three of them;
  - one ball that bounces;
  - plus oriented boxes (~2 days), which triggers need anyway.
- If AstroLeague's ball needs contacts against mesh colliders, or friction and restitution that
  feel like PhysX, bind **BepuPhysics v2** for contacts only, behind Amoebius's trigger and query
  API:
  - Its license is Apache-2.0, and it is pure C#.
  - It needs no per-platform native binary, which matters for the Android and iOS builds.
  - Jolt (through JoltPhysicsSharp, MIT) is the alternative, but it ships native libraries.
  - Either one is a new dependency, flagged before it is added.

---

## F. Networking and services

### F14. A transport seam and the UGS boundary

**Found.**
- `NetDriver` is a static class holding a concrete TCP `NetSocket`, with no interface between
  them.
- `NetworkManager` calls `NetDriver.StartServer` and `StartClient` directly.
- Every UGS service is a stub or kept on local disk.
- Recorded decision: gate G2 (2026-12-15) chooses the online backend: UGS over REST, or Steam
  networking plus Froglet's own backend. The recommendation is Steam for PC.

**Decision.** Accepted.
- The `INetTransport` seam (2-3 days) helps whichever way G2 goes, so it opens C6, ahead of the
  gate. TCP is its first implementation.
- Netcode's own transport interface is not available to vendor: Netcode 2.5.0 and Unity Transport
  2.6.0 are under the UCL (B4).
- **Landed 2026-10-08:** `INetTransport` / `INetTransportFactory`
  (`src/CosmicShore.Engine/Networking/Wire/INetTransport.cs`), with `NetSocket` as the TCP
  implementation and `NetDriver.TransportFactory` as the one place a backend plugs in.

The UGS boundary, stated explicitly (the G2 columns are candidates, not commitments):

| Service | Amoebius today | G2 (a): UGS over REST | G2 (b): Steam + own backend |
|---|---|---|---|
| Authentication | Local stub; anonymous id from `PlayerBoot` | UGS Authentication | Steam auth tickets (Steamworks.NET) + own session service |
| Cloud Save | Local file (`ugs-cloudsave.json`) | UGS Cloud Save | Own backend, or Steam Cloud for small files |
| Leaderboards | In memory, local player only | UGS Leaderboards | Steam leaderboards |
| Lobby and Relay | Session files in a shared folder (LAN); in-process when networking is off | UGS Lobby + Relay (needs a Relay client for Amoebius's transport) | Steam lobbies + Steam Datagram Relay |
| Friends | Local stub | UGS Friends | Steam friends |
| Analytics | Stub | UGS Analytics | Own endpoint, or PostHog (the game already integrates it) |

---

## G. Performance

### G15. Instrument before optimising

**Found.**
- Phase timings, GC counts and render-pass timings went only to the console, behind
  `COSMIC_SHORE_RENDER_TIMING=1`.
- The session report held total frame time only.
- There were no GPU timer queries and no allocation data.

**Done.** Every session report now carries:
- `cpu`: simulation and render-submission percentiles, and the average milliseconds of each loop
  phase (start, fixed, update, triggers, coroutines, tasks, animator, late, destroy).
- `memory`:
  - the allocation rate per frame (p50, p95, worst);
  - the heap size;
  - gen0, gen1 and gen2 collection counts and total pause time;
  - steady-state figures from frame 30 on: collections per minute, gen2 per minute, and GC pause
    per frame;
  - the kilobytes per frame each loop phase allocates.
- Phase timing turns on with a session report. Its cost is one timestamp per phase.

How Amoebius uses those numbers:
- `PrismaTracks` carries the CPU and GC figures per run, and the agent's brief shows them.
- A run whose steady GC pause tops 1 ms per frame becomes a *perf* problem. It goes to the board
  with an acceptance criterion like any other.
- TRACKS > PERFORMANCE has a *Frame budget* card.

**First numbers** (Linux; the first two runs are headless, so their render figures are zero):

| Run | Allocated per frame (p50 / p95) | Steady GC pause per frame | Steady gen2 per minute | Biggest allocating phases (per frame) |
|---|---|---|---|---|
| Headless: boot to the sign-in screen, 900 frames | 8 / 176 KB | 0.04 ms | 0 | tasks 28 KB, update 12 KB |
| Headless: boot to `Menu_Main` with its free-flight scene (66k live behaviours), 2,400 frames | 24 / 208 KB | 0.71 ms | 4.4 | tasks 279 KB, coroutines 62 KB, update 35 KB |
| Windowed (xvfb, software GL): boot to `Menu_Main`, 900 frames | 344 / 624 KB | 1.9 ms | 0.3 | tasks 728 KB, coroutines 171 KB, start 40 KB |

- Loading dominates the totals: 2.5-2.7 GB allocated on the way into `Menu_Main`, and up to 534 MB
  in a single frame. That is a load-time target for C7, not a gameplay hitch.
- The `tasks` phase (the async scheduler) is the clearest steady-state allocator, at 279-728 KB per
  frame in the menu. With the renderer running, steady GC reaches 1.9 ms per frame. That is about
  11% of a 60 fps budget, over Amoebius's own 1 ms problem line, and C7's first lead.
- Under xvfb, frames take over 100 ms, so the render-time histogram (which tops out at 100 ms)
  reads 100. Real GPUs fall well inside it.

*A correction to my first reading:* averaged over a short run, the data said 2.7 MB and 3 ms of GC
per frame, because loading dominated. The report now separates the steady state from loading.

**Next (C7).** GPU timer queries: `GL_TIME_ELAPSED` is core in desktop GL 3.3, but GLES 3.0
needs `EXT_disjoint_timer_query`, so they are measured on desktop.

### G16. GC configuration; NativeAOT

**Found.**
- No GC setting anywhere, so the .NET defaults apply: workstation GC with background collections.
- `GCSettings.LatencyMode` is never used.
- `GarbageCollector.CollectIncremental` ran a blocking full collection and returned true ("work
  remains"). A caller looping until it returns false would force a full GC every frame. The game
  does not call it.
- The game's own three `GC.Collect()` calls run during loads by design: two in `SceneLoader` and
  one in `MultiplayerMiniGameControllerBase`.
- Mobile runtimes:
  - Android runs Mono JIT, with no trimming and no profiled AOT.
  - iOS runs the Mono interpreter (`UseInterpreter=true`, `MtouchLink=None`), not NativeAOT.
  - Trim warnings are suppressed.
- Reflection is everywhere: the Reflex stand-in, the UnityEvent binder (`CreateDelegate`,
  `Invoke`, `MakeGenericMethod`), `SerializedReader`, `ScriptTypeMap`, and lifecycle discovery.
  Lifecycle used `Expression.Compile`, which falls back to the slow LINQ interpreter under iOS's
  Mono interpreter and does not exist under NativeAOT.

**Done.**
- The player's project now states the GC mode (workstation, concurrent), and why.
- `CollectIncremental` starts a background collection and returns false.
- Lifecycle methods (Awake ... OnDestroy, OnTrigger*) are bound as open-instance delegates through
  generic wrappers instantiated over reference types. There is no `Expression.Compile` left on that
  path, which runs every Update.
- Covered by the full engine suite (1,585 tests) and the 352 ported tests, plus a windowed boot to
  `Menu_Main` with 0 errors.

**Decision.** NativeAOT is not an M1 goal.
- It would break type lookup from YAML, `[Inject]`, serialized-field binding and UnityEvent hookup
  until those move to source generators (8-12 days). That fits M2, or C8 if phone performance
  demands it.
- `SustainedLowLatency` is decided by a session-report A/B in C7, not switched on blind.

---

## H. Agent, app and roadmap

### H17. Acceptance criteria

**Done.** Every board item has a **done when** criterion:
- A bug Amoebius found in the tracks carries the tracks' own check: the problem is not seen again in
  3 runs through its scene.
  - After every ingest, `PrismaBoard.Verify` applies it.
  - A passing card gets a green **MET** pill and a notification offering **MARK DONE**. Moving it
    stays the user's call.
  - A card whose problem comes back loses MET. A DONE card reopens to TO DO.
- `prisma_board_suggest` **requires** a `criterion`.
- A milestone task carries its checkpoint's exit criterion.
- The user's own items take an optional "Done when..." field.
- The game agent is told to run a card's check and show the result before calling it done.
- Milestone sessions are told never to mark a checkpoint done until its exit criterion was run and
  passed, with the command and result in the note.
- 4 tests: `PrismaTracksBoardTests`.

### H18. A parity scoreboard

**Accepted for C1.** Most rows already have a data source:
- the shader census (D10);
- the particle and VFX inventory (D12);
- the physics census (E13);
- audio problems from the session reports;
- the serialization audit (C8);
- the RPC guard (A2).

A generator (`Port/tools/parity_scoreboard.py`) writes `docs/PARITY.md`, plus JSON that the
MILESTONES page draws. It rates every subsystem and every shader Faithful, Approximate or Missing,
with the test that covers it. No per-subsystem contract files exist yet, so the generator's
inputs are the contract until they do.

### H19. Session budget and fallback

**Done.**
- Every milestone run has a budget:

  | Limit | Default | Enforced by |
  |---|---|---|
  | Agentic turns | 80 | `--max-turns` |
  | Wall-clock minutes | 60 | Amoebius kills the process tree |
  | Dollars | none (optional) | `--max-budget-usd` |

  All three are in Settings > CLAUDE > Milestone budget.
- A run that hits a limit or fails leaves a record instead of a half-done branch:
  - a suggested board task, "Milestone Cx stopped at ...", listing every tool call the run made,
    its last message, and the checkpoint's exit criterion;
  - a dated note on the checkpoint in `milestones.json`;
  - a notification with **CONTINUE** (same conversation, fresh budget) and **BOARD**.
- The user's own STOP never files one.
- **Verified end to end.** With a 2-turn budget, twice:
  - the C1 session stopped and the transcript said so;
  - the board task (T-4 in the screenshot) listed every tool call the run made;
  - the checkpoint got its note;
  - the banner offered CONTINUE. See `docs/architecture/prisma_milestone_stopped.png`.
- `milestones.json` is now written without `'`-style escaping. The verification run exposed
  that older bug.

**Deferred: worktrees and PRs.** The developer works on one branch in GitHub Desktop. A worktree
session produces a second branch that has to be merged, and Amoebius has no merge or review view
yet. When two milestone sessions need to run at once, each will run in `claude --worktree <id>`
(the CLI supports it) on a `prisma/<checkpoint>` branch, with a PR opened from Amoebius. Until then,
the deny rules and one session at a time keep every change reviewable in GitHub Desktop.

### H20. `prisma_bisect`

**Accepted, after C1, as checkpoint C1b.** `prisma_bisect(good, bad, check)`:
- runs `git bisect run` over commits that touch `Port/` only, so Unity-side commits never move the
  window;
- builds each candidate in a scratch worktree;
- runs the check: a replay compared against goldens, or (before the harness) an `engine_smoke`
  error signature.

---

## What changed in the roadmap

- **C0 Foundations** (done, 2026-10-06): A1, A2, A3, C7, C8, G15, the G16 fixes, H17 and H19,
  with the evidence above.
- **C1:** the exit criterion gains
  - the tolerance table (C9);
  - Random goldens from Unity (C6);
  - the generated scoreboard (H18).
- **C1b** (new, 1 week, after C1): `prisma_bisect` (H20).
- **C2:** a Shader Graph compiler, the custom-function library and guid-keyed families, plus
  Shuriken and approximate VFX Graph (D10, D12).
- **C3:** the decision rule. Implement what the census needs; bind Bepu for contacts if mesh
  contacts are needed; never write a general solver (E13).
- **C6:** the transport seam first, and the UGS boundary table (F14).
- **C7:** GPU timer queries, the `tasks` allocation lead, load-time allocation, jobs moved to
  `Parallel.For` only from a profile, and the `SustainedLowLatency` A/B (B5, G15, G16).
- **Gates:** new **G6**, RHI versus wgpu-native, before C8 (D11).
