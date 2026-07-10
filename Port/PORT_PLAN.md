# Cosmic Shore Port — Master Plan & Live Status

> **This file is the persistent state of the autonomous porting loop.** Each loop
> iteration: (1) read **NEXT UP**, (2) implement + test the next chunk, (3) update the
> status tables and NEXT UP, (4) commit + push to `claude/quirky-cannon-sk8a02`.
> The goal, verbatim from the prompter: *"remake the entire game … wholly ours without
> any dependency on unity or anything else that prevents you from developing in your
> own loop without a human's involvement. We want everything replicated. Loose nothing."*

## Ground rules

1. **Lose nothing.** Every behavior, tuned value, enum ID, event contract, and system
   in the Unity project gets a counterpart here. When a Unity feature can't be ported
   verbatim, record the deviation in the **Deviations log** below.
2. **Headless first.** The simulation must build, run, and verify with `dotnet test`
   alone. Rendering/audio/input are pluggable backends added later; they may never
   become load-bearing for game logic.
3. **Verbatim porting.** Same namespaces, file names, member names. Only the
   using-directive substitutions in README.md are allowed without justification.
4. **All architecture rules from /CLAUDE.md carry over**: SOAP for cross-system
   communication, single-writer pattern, fail-loud (no null guards on event channels),
   config in data assets not code, conserved mass / emergent-systems design philosophy,
   frozen enum numeric values.
5. **Tests accompany every port.** Enum values get freeze tests; logic gets behavior
   tests. The Unity project's existing edit-mode tests get ported alongside their
   subjects.
6. **Always shippable progress builds** (prompter requirement, 2026-06-10): the branch
   must always offer a one-command way for a human to try the current state — see
   "Progress builds — testing protocol" below. Growing the testable surface is part of
   every phase, not an afterthought.

## Progress builds — testing protocol

The prompter tests progress without prompting the loop. Contract:

1. **Green branch invariant.** Every push to `claude/quirky-cannon-sk8a02` has
   `cd Port && dotnet build && dotnet test` green. The branch is always safe to pull.
   This includes **buildable from a fresh clone**: `git ls-files Port | grep -c 'csproj$'`
   must equal 7 — the root Unity `.gitignore` (`*.csproj`/`*.sln`) silently excluded
   every project file until 2026-06-11 (negations added), which shipped a branch whose
   source couldn't build on the prompter's machine.
2. **Runnable harness: `CosmicShore.Cli`** (`src/CosmicShore.Cli`, lands iteration 2).
   One command to exercise the current port on any machine with the .NET 10 SDK:
   `cd Port && dotnet run --project src/CosmicShore.Cli`. It grows with the port:
   - now → engine smoke: boot loop, SOAP wiring, state machine walk, version banner
   - phase 2 → scripted simulation: AI vessels, prisms, crystals, cells; deterministic
     via `--seed`; emits a readable match transcript + final stats
   - phase 3 → full game-mode rounds headless (`--mode hexrace --players 4 --seed 42`)
   - phase 5 → `--render` flag for the interactive window; headless stays the default
3. **Visual artifacts before the renderer is interactive.** From the first render-capable
   milestone, headless render-to-PNG (and short MP4) smoke outputs are committed under
   `Port/artifacts/` (small, curated) and sent into the chat at milestones so progress
   is visible with zero local setup.
4. **Milestone log + notification.** Each numbered milestone (M1 first CLI sim, M2 first
   full game-mode round, M3 first picture, M4 first interactive build, …) is recorded in
   the milestone log below with its commit hash, and the prompter gets a push
   notification with the exact command (or file) to try. (Annotated `port-mN` tags are
   created locally, but this environment's git proxy only accepts branch pushes — the
   log + commit message are the durable record.)
5. **Standalone binaries ON REQUEST ONLY** (prompter, 2026-06-12: "i don't need the
   zip anymore my powershell workflow is working great" — from-source is the primary
   channel; stop refreshing `dist/` zips on build changes; the committed zips remain
   as a fallback for SDK-less machines). When requested, build with:
   `dotnet publish <proj> -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
   (swap `-r` for `linux-x64` / `osx-arm64`; ~39 MB exe; no trimming — the engine's
   reflective lifecycle discovery forbids it). **Do NOT add
   `IncludeNativeLibrariesForSelfExtract`** — verified 2026-06-11: Silk.NET's loader
   cannot load self-extracted natives ("GlfwPlatform - not applicable"); it only finds
   natives BESIDE the exe (same root cause as the `CopySilkNativesBesideApp` build
   target). Without the flag, glfw3/soft_oal land beside the exe in the publish dir —
   **zip them together with the exe** (the one historical bad ship was a bare-exe zip
   missing those loose natives). The exe holds its console window open when
   double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
6. **Fast test loop for the prompter — PRIMARY (confirmed working 2026-06-12)**:
   .NET 10 SDK + VC++ redist installed once, then in PowerShell:
   `git pull` + `dotnet run -c Release --project Port\src\CosmicShore.Client`
   builds and runs from source incrementally (seconds per iteration).
   `Port/play-latest.bat` (zip channel) remains as fallback only.
7. **Local prerequisites for the prompter** (only for running from source):
   `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
   then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.

### Milestone log

| Tag | What became testable | Command | Status |
|---|---|---|---|
| `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 (commit `25b5582d`) |
| `port-m2` | First full headless game-mode round (AI vs AI) | `cd Port && dotnet run --project src/CosmicShore.Cli -- --mode hexrace --players 4 --seed 42` | ✅ 2026-06-12 (commit `dc2eb876`) |
| `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
| `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |

## HARD RULE (prompter, 2026-06-11): never merge OUT of this branch

**NEVER merge, rebase, or cherry-pick anything from `claude/quirky-cannon-sk8a02`
into `bleeding-edge` or any other branch.** Merging INTO this branch (as was done
with bleeding-edge and brahmagupta) is fine; the reverse direction is prohibited.
Push only to `claude/quirky-cannon-sk8a02`.

## Session resume protocol (fresh session / fresh container)

The loop's memory is THIS FILE plus the branch — no chat context is load-bearing.
To resume in a brand-new session, the prompter pastes exactly:

> /loop run Port/setup.sh if the toolchain is missing, then continue the Cosmic Shore
> port per Port/PORT_PLAN.md: SPRINT MODE (SkimRace toward full Cosmic Shore parity)
> + the fidelity arc (docs/VESSEL_LAYER.md, V6 keystone next). Keep dotnet test green,
> update PORT_PLAN, commit and push ONLY to claude/quirky-cannon-sk8a02 — NEVER merge
> anything from this branch into bleeding-edge or any other branch. Ship playable zips
> to Port/dist and tell me the download link when builds change.

That re-arms the heartbeat, rebuilds the toolchain (~3-5 min via setup.sh), and picks
up at NEXT UP. Mid-iteration work is never stranded: every iteration ends pushed.
(Only ONE session should run this loop at a time; the 2026-06-11 session stopped its
heartbeat on handoff.)

## Toolchain (re-verify each fresh container)

- .NET 10 SDK at `/opt/dotnet` (installed via `dotnet-install.sh --channel LTS`;
  `export PATH=/opt/dotnet:$PATH`, persisted in `~/.bashrc`). nuget.org reachable.
  crates.io blocked; npm available (unused).
- Build: `cd Port && dotnet build && dotnet test` — must be green before every commit.

## SPRINT MODE (prompter directive, 2026-06-11)

> "Sprint toward gameplay, visuals, etc. … /loop until we have a clone of skim race
> that I can test."

Priorities inverted until **SkimRace** ships: a windowed, flyable crystal-skimming race
(HexRace rules — steer down a neon track, collect the crystal target, finish time is
the score) built on the already-ported sim (ResourceSystem, RoundStats, SOAP). Stack:
`src/CosmicShore.Client` on Silk.NET (MIT: windowing/OpenGL/input), verified headlessly
here via Xvfb+Mesa screenshots, shipped as win-x64 zips in `Port/dist/`. The verbatim
fidelity arc (VESSEL_LAYER.md V1-V19, phases 2-8) continues underneath sprint
iterations — sprint code reuses ported systems wherever they exist and must not fork
their semantics. SkimRace milestones: **S1-S3 SHIPPED** (windowed race: track/crystals/trail/HUD/finish,
win-x64 zip in dist). **S4 in progress**: AI rival shipped (contested crystals, overtake
targeting, rubber-band boost+speed) + gamepad input; KNOWN ISSUE — rival can't beat a
perfect autopilot in AI-vs-AI demos (sweeps happen); vs humans it contests missed
crystals. Shipped since: bloom post chain (bright-pass → half-res gaussian → tonemapped
composite) with camera-proximity fades for trails/bursts; mouse steering; **procedural
audio** (AudioEngine: synthesized PCM via OpenAL Soft — engine hum scaling with speed,
boost layer, crystal chimes, countdown/go beeps, win/lose jingle; fail-safe silent mode
when no device). **FIRST HUMAN FLIGHT 2026-06-11** (prompter, gamepad). Feedback applied same-day:
SkimRace now uses the AUTHENTIC control scheme — the ported GamepadInputStrategy runs
against a Silk→shim device bridge (XSum/YSum/XDiff/YDiff: dual-stick sums = pitch/yaw,
difference = roll, stick spread = throttle), and the flight model is VesselTransformer
parity (free AngleAxis rotation about own axes, scalers 130/130/130, throttle 50·XDiff
+ 10 min speed, LERP 1.5, boost as throttle multiplier). Keyboard fallback: WASD+arrows
as two sticks. **SQUIRREL HULL SHIPPED**: first-party binary-FBX extractor (Python, in-session) parsed
SquirrelVessel_CosmicShoresTest1.fbx (13,660 tris) → axis-remapped/normalized →
Assets/squirrel.mesh embedded resource → flat-lit at load in jade/ruby palettes; dart
remains fallback. **Gamepad yaw inverted** post-strategy (prompter preference; XDiff/
YDiff semantics untouched). **Gamepad roll inverted** post-strategy alongside it
(prompter, 2026-06-11 second feedback round, first from-source flight): yaw-inverted
steering banks opposite the turn unless roll flips too; AI and keyboard paths
untouched. Both dist zips rebuilt on the corrected publish recipe (loose natives
beside the exe — see testing-protocol item 5). **S5 SHIPPED** (prompter directives,
2026-06-11 third feedback round): **closed circuit** (seeded ring, integer-harmonic
radius/altitude undulation so the loop closes exactly; loop-frame rails + gates;
loop-angle AI/autopilot targeting replaces the old "+z ahead" logic), **persistent
trails** (sim-owned skimmable race state, emitted along the velocity path, never
culled — reset is the only sink, per conserved-mass rules), **trail-skim energy**
(rival's trail always counts, own trail after 3s aging — lap back onto it; linear
falloff over 7u; passive regen cut 0.12→0.04 so skimming is THE energy source),
**energy raises top speed** (throttle term ×(1+0.6·energy)), **analog trigger drift**
(triggers decouple velocity from the nose: course re-aligns at 7/s gripped → 0.55/s
at full pull; drift-skimming charges up to 2×; keyboard Shift = full drift; magenta
HUD gauge + energy bar flares while skimming). Crystals respawn 12s after claim so
every lap is live; win target raised to the full station count (default 30) so races
span 2+ laps and the lap-back-onto-your-trail loop actually engages. Verified
headlessly: lap-1 diagnostic shows skim False (no ribbon to ride yet), lap-2 shows
skim True for both pilots with the trailing rival charging off the leader's ribbon —
an emergent slipstream catch-up. Artifact: `artifacts/skimrace_s5_circuit_trails.png`.
**S6 SHIPPED** ("go hard, I want everything" directive): **a field of rivals**
(`--rivals N`, default 3, max 7 — Ruby/Gold/Blue domain palettes for hulls AND
ribbons, seeded temperaments: TurnSkill/DriftIQ/Aggression), **elemental crystals**
(every station carries an element, claims permanently raise that level via the
ported ResourceSystem; levels tune the vessel — Charge→skim rate, Mass→wider
longer-reach trail baked per TrailPoint, Space→turn rate, Time→cooler boost burn;
every 7th station is Omni = +1 all; crystal octahedra tinted per element),
**AI v2** (loop-aware multi-rival overtake, drift-to-charge when low on energy +
drift through hard corners per DriftIQ, aggression-scaled boost reserve),
**minimap** (circuit outline + domain-colored pilot markers, bottom-right),
**lap + position HUD**, **finish scoreboard** (whole field ranked: domain diamond,
position, crystals), **skim shimmer + drift rush audio layers** (gain/pitch follow
contact strength and trigger pull). Verified headlessly: 4-pilot race, frame-1300
diag `crystals [24,19,4,1], P1 lap 2, levels C13/M6/S8/T6, skim True, trail 5004`
— the field is COMPETITIVE now (drift-charging rivals; was 30-9 in S5). Artifact:
`artifacts/skimrace_s6_field_elements.png`. Post-S6: finished pilots now fly a
victory lap (scoreboard renders over the prismscape, not the void); long-run verify
produced a **30-29 photo finish with the rival winning** — the drift-charging field
is competitive with a perfect autopilot (`artifacts/skimrace_s6_photo_finish.png`). NEXT: prompter verifies the field race
feel (drift, elements, rival pressure), Squirrel nose orientation in motion (flip
is one sign in the extractor); deeper Cosmic Shore content (more vessel classes,
cells/fauna ambience, shape modes) on request. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
(parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
BoostChangedPayload+event, InputEventBlockPayload, CellItem; ApplicationLifecycleManager
IsQuitting extraction). **V2 DONE** (engine E1 inert input-device shim: Keyboard/Gamepad/Mouse/AttitudeSensor/
EnhancedTouch; ported IInputStatus [Deviation #10a open: InputController property +
GetGyroRotation commented until V5], IInputStrategy, BaseInputStrategy,
KeyboardInputStrategy — the live loose-file strategy per the survey). **V3 DONE** (TouchInputStrategy + GamepadInputStrategy verbatim; shim grew Screen.dpi/
currentResolution + unified TouchPhase — the original shares one enum across
namespaces; InputStrategyTests: all three strategies run inert without faulting, invert
toggles write through). **V4 DONE** (DualMouseInputStrategy, MultiMouse folder — Win32 raw-input provider
compiles out under our defines, verbatim — DeviceOrientationHandler; shim grew Mouse.all/
displayName, InputSystem.devices, Accelerometer, AttitudeSensor.enabled, Cursor/
CursorLockMode, Screen autorotate flags). The IO SCC is now fully ported except
InputStatus(V7) + InputController(V5). **V5 DONE** (GameSetting [282L; Deviation #14: UGS cloud-settings paths commented until
services phase; pure PlayerSettingsCloudData model ported] + InputController [275L;
Deviation #10b: IVessel field/usages commented, restore at V6; Deviation #15:
TryAddInputStatus fail-loud until concrete InputStatus at V7]; **Deviation #10a
CLOSED** — IInputStatus verbatim again). **V6 DONE** (keystone: IVessel + IPlayer +
IVesselStatus [Deviation #10c open — 13 members commented pending V7-V19 types] +
ElementalFloat + ElementalShipComponent + IVesselHUDController +
R_ShipElementStatsHandler + ShipActionSO/ShipActionExecutorBase/ActionExecutorRegistry/
legacy ShipAction; AudioSystem shell pulled forward as Deviation #11; **Deviations #9
and #10b CLOSED** — ResourceSystem : ElementalShipComponent + RequireComponent restored,
InputController.vessel field live). **V7 DONE** (engine E2 renderer data stubs:
Renderer/MeshRenderer/SkinnedMeshRenderer [blend-shape store]/TrailRenderer/Camera;
InputStatus verbatim — IsSpawned-switched local/NetworkVariable storage, owner-gated
writes, **Deviation #15 CLOSED** [TryAddInputStatus → GetOrAdd&lt;InputStatus&gt; verbatim];
VesselAnimation verbatim + its IVesselStatus member uncommented [#10c partial restore]).
Next: V8 (VesselTransformer + member restore), rival balance from prompter feedback.

## Phase roadmap

| Phase | Scope | Status |
|---|---|---|
| **0 — Foundation** | Toolchain, solution, engine math/SOAP/attrs/net-primitives, Data layer, test harness | ✅ **DONE** (iteration 1) |
| **1 — Engine core** | First-party async (UniTask replacement ✅), DI container (Reflex replacement ✅), update loop + scheduler ✅, logging ✅, GameObject/Transform/MonoBehaviour/Scene model ✅ (see `docs/ENGINE_CORE.md`); asset registry (.asset → JSON) deferred to content phase | ✅ **DONE** (iteration 2) — remaining: asset registry, prefab factories, multi-scene |
| **2 — Simulation core** | ResourceSystem, VesselStatus + transformer/controller, Prism/Trail/TrailFollower, impact-effects matrix (impactors × effect SOs), crystals, cells (CellPhase/aggression), flora/fauna ecosystem (conserved mass!), elementals | ⬜ |
| **3 — Game modes** | MiniGameControllerBase hierarchy (template method: rounds→turns→countdown→gameplay→end), turn monitors, scoring (incl. golf rules), AI pilot/gunner, all 36 GameModes' controllers (priority: Freestyle, CellularDuel, WildlifeBlitz, HexRace, Joust, CrystalCapture) | ⬜ |
| **4 — Networking** | First-party transport + replication: NetworkVariable wire sync, RPC equivalent, server-authoritative session flow, host/client lifecycle, replacing Unity Netcode + UGS Relay/Sessions/Lobby with self-hosted session server | ⬜ |
| **5 — Presentation** | Renderer backend (evaluate: custom GL via first-party bindings vs software-rendered headless screenshots first), camera system (CameraSettingsSO port), input strategies (Keyboard/Gamepad/Touch via IInputStrategy), HUD/UI framework, VFX/shader ports (HLSL sources exist in repo), audio backend (Wwise replacement) | ⬜ |
| **6 — Services** | Replace UGS/PlayFab/Firebase: auth, cloud save, leaderboards, friends/presence, parties/invites, analytics — self-hosted service + local-first fallback | ⬜ |
| **7 — Content pipeline** | Extract all `_SO_Assets/**/*.asset` (Unity YAML) → JSON for the asset registry; scene descriptions → first-party scene format; models/textures/audio export | ⬜ |
| **8 — Integration** | Full playable loop: boot → menu → game mode → end-game → menu, multiplayer session E2E, performance passes | ⬜ |

## Status — detailed inventory

### Phase 0 (✅ done, iteration 1 — 2026-06-10)

| Item | Port location | Tests |
|---|---|---|
| Solution + projects (net10.0) | `Port/CosmicShore.slnx`, `src/`, `tests/` | `dotnet test` green: 259/259 |
| Math: Vector2/3/4, Quaternion (YXZ Euler convention verified), Mathf (incl. SmoothDamp), Color | `src/CosmicShore.Engine/Math/` | `MathTests.cs` |
| Time (frame clock, harness-driven `Advance`) | `src/CosmicShore.Engine/Time.cs` | — |
| Attributes: SerializeField/Header/Tooltip/Range/Min/TextArea/CreateAssetMenu | `src/CosmicShore.Engine/Attributes.cs` | — |
| ScriptableObject base + CreateInstance | `src/CosmicShore.Engine/ScriptableObject.cs` | — |
| SOAP: ScriptableVariable<T> (+8 concrete), ScriptableEvent<T>/NoParam (+6 concrete), ScriptableList<T> | `src/CosmicShore.Engine/Soap/` | `SoapTests.cs` |
| Networking primitives: NetworkBehaviour (Spawn/Despawn lifecycle), NetworkVariable<T> (perm-aware, change callbacks), FixedString64Bytes (61-byte UTF-8 cap) | `src/CosmicShore.Engine/Networking/`, `Collections/` | `NetworkingTests.cs` |
| **Data layer: all 26 enum files + 5 struct files ported verbatim** (incl. RoundStats NetworkBehaviour, IRoundStats with default-method Cleanup, DomainStats) | `src/CosmicShore.Data/` | `EnumFreezeTests.cs` (full numeric freeze), `RoundStatsTests.cs` (both lifecycle modes) |

### Unity-project inventory still to port (tracked at directory granularity; refine as phases open)

| Source (under `Assets/`) | Files | Target phase | Status |
|---|---|---|---|
| `_Scripts/Data/` | 31 | 0 | ✅ done |
| `_Scripts/Utility/` (ClassExtensions, DataContainers incl. GameDataSO, PoolsAndBuffers, Effects, DataPersistence) | ~80 | 1–2 | ⬜ |
| `_Scripts/ScriptableObjects/` (SO_* defs + 16 SOAP subdirs + VesselPrefabContainer) | ~70 | 1–2 | ⬜ |
| `_Scripts/Controller/Vessel/` (VesselStatus, Prism, Trail, actions, ResourceSystem) | ~150 | 2 | ⬜ |
| `_Scripts/Controller/ImpactEffects/` (11 impactors, 20+ effect SO types) | ~60 | 2 | ⬜ |
| `_Scripts/Controller/Environment/` (cells, crystals, flora/fauna, flow/warp fields, spawners) | ~100 | 2 | ⬜ |
| `_Scripts/Controller/Managers/` (PrismScaleManager, MaterialStateManager, …) | ~15 | 2/5 | ⬜ |
| `_Scripts/Controller/Projectiles/` | ~30 | 2 | ⬜ |
| `_Scripts/Controller/Arcade/` (controllers, scoring, turn monitors) | ~70 | 3 | ⬜ |
| `_Scripts/Controller/AI/` (AIPilot, AIGunner) | ~10 | 3 | ⬜ |
| `_Scripts/Controller/Player/` + `Multiplayer/` + `Party/` | ~50 | 3–4 | ⬜ |
| `_Scripts/Controller/IO/` (input strategies) | ~10 | 5 | ⬜ |
| `_Scripts/Controller/Camera/`, `Animation/`, `FX/`, `Assemblers/`, `Prisms/`, `ECS/`, `XP/`, `Settings/` | ~40 | 2/5 | ⬜ |
| `_Scripts/System/` (Bootstrap, AppManager, state machine, scene loader, auth/friends facades, audio, instrumentation, dialogue runtime, rewind, quests, XP, ads, IAP…) | ~126 | 1/3/6 | ⬜ |
| `_Scripts/UI/` (HUD controllers/views, screens, modals, toast, event feed, elements) | ~188 | 5 | ⬜ |
| `FTUE/` (tutorial system, 25 files) | 25 | 5 | ⬜ |
| `_Scripts/DialogueSystem/` + `System/Runtime/` dialogue | ~30 | 3/5 | ⬜ |
| `_Scripts/Editor/` (tooling — re-imagine as CLI tools where still needed) | ~40 | 7 | ⬜ |
| `_Scripts/Tests/` (port existing edit-mode tests alongside subjects) | ~25 | rolling | ⬜ |
| `_SO_Assets/` (48+ dirs of .asset instances → JSON) | ~hundreds | 7 | ⬜ |
| Shaders/HLSL (`Assets/Materials/Graphs/`, prism/skybox/crackle shaders) | ~dozens | 5 | ⬜ |
| Scenes (`_Scenes/` — 3 core + 3 SP + 7 MP + tools) | 16+ | 7 | ⬜ |
| Prefabs (`_Prefabs/` — vessels, pools, trails, UI) | ~hundreds | 7 | ⬜ |

### Third-party dependency replacement map

| Unity-era dependency | Replacement strategy | Phase | Status |
|---|---|---|---|
| Unity engine core (GameObject/MonoBehaviour/scenes) | ✅ First-party component/scene model in `CosmicShore.Engine` (`docs/ENGINE_CORE.md`); Instantiate/prefabs + multi-scene deferred to content phase; physics deferred to phase 2 | 1 | ✅ core |
| UniTask | ✅ `CosmicShore.Engine.Tasks` (`GameTask.*`, structural main-thread affinity, synchronous cancellation parity) — `.AsMainThread()` retired | 1 | ✅ |
| Obvious.Soap | ✅ `CosmicShore.Engine.Soap` | 0 | ✅ |
| Reflex DI | ✅ `CosmicShore.Engine.Injection.Container` (RegisterValue/RegisterFactory-lazy, `[Inject]`, child scopes, InjectGameObject) | 1 | ✅ |
| Unity Netcode for GameObjects | First-party replication over UDP/TCP (NetworkVariable sync + RPC); API contract already established in `Engine.Networking` | 4 | ⬜ |
| UGS (Auth, Relay, Sessions/Lobby, Friends, CloudSave, Leaderboards, Analytics) | Self-hosted session/identity service (single small server, JSON protocol) + local-first offline mode | 6 | ⬜ |
| PlayFab (economy, catalog) | Same self-hosted service | 6 | ⬜ |
| Firebase (analytics) | First-party event log → pluggable sink | 6 | ⬜ |
| Wwise | First-party audio mixer over a low-level output lib (decide at phase 5; candidate: miniaudio P/Invoke, vendored) | 5 | ⬜ |
| DOTween | First-party tween library on engine update loop | 1–2 | ⬜ |
| Cinemachine | First-party camera rig (follow/zoom per CameraSettingsSO) | 5 | ⬜ |
| Unity Input System | IInputStrategy pattern already platform-agnostic; first-party backends | 5 | ⬜ |
| NiceVibrations | Haptics abstraction; platform backends when mobile target lands | 5 | ⬜ |
| Unity Jobs/Burst | System.Threading + SIMD (System.Numerics); profile before optimizing | 2+ | ⬜ |
| URP/ShaderGraph/VFXGraph | First-party renderer + ported HLSL (sources already in repo) | 5 | ⬜ |

## Deviations log

| # | Deviation | Rationale |
|---|---|---|
| 1 | `Mathf.Round` uses banker's rounding (MidpointRounding.ToEven) — same as Unity. `Vector3.SmoothDamp` is component-wise (Unity clamps the change vector once); identical for maxSpeed=∞ usage. | Documented at the source; revisit if a ported system clamps SmoothDamp speed. |
| 2 | `NetworkVariable<T>` change callback uses `EqualityComparer<T>.Default` dedup; Unity Netcode dedups on serialized-value equality. Behaviorally identical for the value types used. | — |
| 3 | SOAP `EventListener*` components are ported and functional in code (`Engine.Soap.EventListenerGeneric` + `Engine.Events.UnityEvent`); inspector wiring arrives with the scene-asset pipeline (phase 7). The `UnityEvent` type NAME is kept for verbatim porting — it is first-party code in `CosmicShore.Engine.Events`. | — |
| 4 | **Upstream latent bug fixed in port**: `new TrainingGameProgress()` zero-initialized (null `Progress`, intensity 0) — C# 9 couldn't express the intended parameterless init, and the 16 TrainingGameProgressTests documenting the contract were silently red upstream (no CI). Port adds a real parameterless ctor chaining to the dummy-arg one. Worth fixing upstream. | Test-documented contract wins. |
| 5 | **Upstream stale test fixed in port**: `GameModes_HasExpectedMemberCount` expected 34; the enum has 35 members (MultiplayerCrystalCapture added without updating). Updated to 35. | — |
| 6 | **Upstream latent red test reframed**: `ImpactEffects_AllValuesAreUnique` contradicts the shipped enum, which intentionally merges legacy effect groups sharing values 1-8, 10. Values are wire format — port freezes the exact duplicate set instead so NEW collisions still fail. | Enum values untouchable. |
| 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
| 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |
| 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. **CLOSED at V6 (2026-06-11)** — both restored verbatim. | Unblocks the elemental core. |
| 10c | `IVesselStatus` landed (V6) with 13 members commented pending their types: `AIPilot`, `AICinematicBehavior`, `AutoPilotEnabled` (→V18), `AttachedPrism` (→V15), `VesselAnimation` (→V7), `VesselTransformer` (→V8), `Customization` (→V9), `NearFieldSkimmer`/`FarFieldSkimmer` (→V16), `VesselPrismController`/`ActionHandler` (→V17), `VesselCameraCustomizer`/`Silhouette` (→V19). Each restore iteration uncomments its members; V19 closes. **CLOSED 2026-06-12 — IVesselStatus diff-verified verbatim.** | Stages the vessel SCC per VESSEL_LAYER.md. |
| 11 | `AudioSystem` type-preserving shell (`Instance`, two `PlayGameplaySFX` overloads; bodies no-op) — pulled forward from V15 to V6 because `ActionExecutorRegistry` needs the type. Real port with the phase-5 audio backend. | Keeps ActionExecutorRegistry verbatim. |
| 14-ext | `PlayerDataService` (C2) stages its UGS CloudSave surface exactly like GameSetting's #14: `UGSDataService` injection, repo read/write, ready-event hooks commented with `PORT Deviation #14 (C2, restore when UGSDataService ports)` markers (7 sites); local default profile, crystal/XP math, reward unlocks, and the OnProfileChanged → GameDataSO sync are live. | Services phase owns the restore. |
| 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
| 17 | **Controller-chain arc scene deviations** (all marked `PORT Deviation (scene arc, …)` / `(UI shell, …)`): `MultiplayerMiniGameControllerBase` — the `SceneTransitionManager` fade field + its 2 call sites and the `nm.SceneManager.LoadScene` replay reload block are commented (no scene manager yet; everything in-scene — turn/round state machine, replay AI despawns, config sync — verbatim). `CountdownTimer` — the DOTween/Image/Sprite/beep presentation is replaced by a timing-equivalent GameTask beat loop (4 × countdownDuration unscaled → onComplete; `_seq?.Kill()` → CTS cancel parity). `CameraManager` shell grew a no-op `SnapPlayerCameraToTarget()` (Deviation #12 surface). Restore with the scene-management / UI arcs. | Smallest surface for the scene/UI gaps; the round/turn/score flow is verbatim. |
| 18 | **AstroLeague arc**: engine gains E18 ballistic Rigidbody dynamics (linear/angular velocity + damping + `AddTorque` on a unit inertia tensor, integrated once per fixed step after the FixedUpdate phase; gravity not simulated — the HyperSea has none), data-only `PhysicsMaterial`/`Light`/`BlendMode`/material keywords+renderQueue, `FixedString32Bytes`, `NetworkManager.ConnectedClientsIds`, ISession `Deleted`/`PlayerLeaving` events, `FindAnyObjectByType`. `AstroLeagueBall` deviations (all presentation, marked): ~~icosphere mesh swap (mesh arc)~~ **mesh half CLOSED by the mesh arc** — the faceted-icosphere swap + owned-mesh destroy are live (inert in the CLI harness, whose ball GO carries no MeshFilter — the `meshFilter != null` guard is the original's); still staged: ParticleSystem aura/burst rig + haptics (presentation arc); the engine dispatches no `OnCollisionEnter/Stay`/`OnTriggerStay`, so the hull-collider strike path is carried as commented source and vessel contacts flow through the verbatim `OnTriggerEnter` path (the original's trigger-only-ship route — Serpent/Sparrow). `AstroLeagueArena`'s editor-only `OnDrawGizmos` body commented (no Gizmos). | Physics core verbatim on E18; the solver-dependent + render-side pieces restore with their phases. |
| 19 | **Tournament (Maelstrom) arc**: engine gains the `CosmicShore.Engine.SceneManagement` surface (`SceneManager.sceneLoaded` + `LoadSceneMode`; loads are announced by harnesses via `NotifySceneLoaded` until real scene management lands — the controller's subscription + `HandleSceneLoaded` are verbatim), Netcode 2.x `[Rpc(SendTo.…)]`/`RpcParams` metadata (local-invoke), and the headless `Engine.UI.Button` shim. `TournamentSceneView` deviations (all presentation, marked): DOTween pulse/typewriter bodies no-op; round/summary card population commented (TournamentRoundCard / TournamentSummaryPlayerCard / TournamentPlayerCard / TournamentDomainScoreView are Image/CanvasGroup/DOTween prefab views — unported, restore with the UI arc); ScrollRect auto-scroll + LayoutRebuilder; SO_AIProfileList avatar branch; `NetworkManager.SpawnManager`/`LocalClient` roster/local-domain fallbacks. CLI `--mode tournament` legs are simulated by the headless `HexRaceRound` regardless of the drawn mode until the Joust / Crystal Capture controllers port (stated in the transcript). | Meta logic (fold, race-to-6, draw, phases) verbatim + tested; render-side pieces restore with their phases. |

## Iteration log

- **Iteration 1** (2026-06-10): Phase 0 — toolchain, solution, engine math/SOAP/attrs/
  net primitives, full Data layer, 259 tests.
- **Iteration 2** (2026-06-11): Phase 1 — component/scene model (GameObject, Transform,
  MonoBehaviour with reflective lifecycle discovery, fake-null Object contract),
  GameLoop (fixed-step accumulator, phase ordering), `Engine.Tasks` (GameTask awaitables,
  synchronous-cancellation parity, GameSynchronizationContext), `Engine.Injection`
  Container, Debug/ColorUtility/LayerMask, `CosmicShore.Game` project with first ported
  Utility files (CSDebug, DebugExtensions, GameObjectExtension, TransformExtensions with
  GameTask), ported IRoundStatsCleanupTests, `docs/ENGINE_CORE.md`, **CosmicShore.Cli
  milestone M1** (tag `port-m1`). 322 tests.

- **Iteration 3** (2026-06-11): custom SOAP layer + pure-logic substrate — engine
  additions (UnityEvent/UnityEvent<T> in `Engine.Events`, EventListenerGeneric/
  EventResponse in `Engine.Soap`, AddComponentMenu/FormerlySerializedAs/Preserve
  attributes); 48/58 SOAP custom-type files ported verbatim (10 deferred on unported
  gameplay types — see Deviations); struct/enum extractions (StatsManager stat structs,
  PrismType, MenuAudioCategory/GameplaySFXCategory); Utility ports (DisposableGroup,
  GeometryUtils, SceneNameListSO, CellPhaseThresholds + CellPhaseRules); **new
  `tests/CosmicShore.Tests.Ported` project (NUnit 3, mirrors Unity Test Framework) with
  12 original EditMode test files ported verbatim** — which immediately caught three
  latent upstream issues (fixed in port, see Deviations #4-6); CLI section [4] (SOAP
  channels + cell phase hysteresis). 528 tests green (322 xunit + 206 NUnit).

- **Iteration 4** (2026-06-11): phase-2 entry — **engine coroutines** (CoroutineRunner:
  synchronous first step, yield-null next frame, WaitForSeconds scaled time, nesting,
  death-with-owner; `IEnumerator Start` auto-runs via LifecycleHooks), RequireComponent/
  HideInInspector attributes, standalone-binary protocol (win-x64 exe delivered to
  prompter); ported: Resource, **ResourceSystem** (elemental levels: GetLevel floor math,
  AdjustLevel, temporary-effect linear decay over base levels — 2 deviations, see #9),
  GenericDataSO/IntDataSO/StringDataSO, RuntimeCollectionSO, CameraSettingsSO (+ their
  3 EditMode test files verbatim); xunit coroutine + ResourceSystem suites; CLI section
  [5] (crystal pickups, danger-prism debuff decay). 578 tests green (329 + 249).

- **Iteration 5** (2026-06-11, short): XpData struct extracted from PlayFab-coupled
  XpHandler + XpDataTests verbatim (585 tests green: 329 + 256). **Key finding for the
  next iterations**: ElementalFloat → IVessel → IVesselStatus/IPlayer closure spans the
  whole vessel layer (~12 classes: AIPilot, Prism, Skimmer, InputController,
  VesselTransformer, SilhouetteController, VesselPrismController, HUD controllers,
  action handlers, Material/Pose engine types). Deviation #9 stays open; plan the
  vessel layer as a dedicated multi-iteration arc (survey → engine Material/Pose →
  leaf classes → interfaces → restore deviations).

- **Iteration 8** (2026-06-12, double): **V8 + V9 in one pass.** V8: `VesselTransformer`
  ported verbatim (518L — the flight model: accumulated-rotation pitch/yaw/roll,
  Slerp-smoothed orientation, throttle/boost/charged-boost speed composition, analog
  drift with single/sharp tiers + non-gamepad MoveTowards easing + course decoupling,
  throttle/velocity modifier stacks with engine-flare hooks, pose controls, reset);
  10 flight-model tests freeze throttle convergence, boost composition, slow/velocity
  modifiers, drift scaling + course lag, pose, reset, and gating. V9: engine E9
  `ObjectPool<T>` + `MonoBehaviour.destroyCancellationToken` + `ColorUsage` +
  `Camera.backgroundColor` + `Instantiate(original, parent, worldSpace)`; ported
  verbatim: `ShipHelper` (VesselHelper.cs), `ThemeManagerDataContainerSO`,
  `SO_MaterialSet`, `SO_ColorSet` (+DomainColorSet/EnvironmentColorSet),
  `VesselCustomization`, `GenericPoolManager` (documented GameTask substitutions);
  mapping structs extracted from R_VesselActionHandler per Deviation #8 pattern.
  **#10c partial restores: VesselTransformer (V8) + Customization (V9) members live.**
  12 new tests (pool semantics, pool-manager lifecycle, ShipHelper action wiring +
  material application + theme push, customization paint). **632 tests green
  (380 + 252)**; client smoke unaffected.

- **Infra rescue** (2026-06-11, fresh session): prompter's first from-source run
  (`dotnet run --project Port\src\CosmicShore.Client`) failed — "Couldn't find a
  project to run". Root cause: the Unity root `.gitignore` ignores `*.csproj`/`*.sln`,
  so all seven csproj files had NEVER been pushed (the `.slnx` survived only because
  `*.sln` doesn't match it). Fixed: `.gitignore` negations (`!Port/**/*.csproj` etc.)
  + all seven csprojs reconstructed and committed (graph: Engine ← Data ← Game ←
  Cli/Client; xunit project needs `<Using Include="Xunit" />`; Client pins
  Silk.NET 2.22.0 + Silk.NET.OpenAL.Soft.Native 1.23.1, embeds `Assets/squirrel.mesh`,
  AllowUnsafeBlocks). Second find: Silk.NET's loader does not probe
  `runtimes/<rid>/native/` on this stack ("GlfwPlatform - not applicable" despite the
  native restoring; verified `dlopen`/`NativeLibrary` load it fine) — new
  `CopySilkNativesBesideApp` target in the Client csproj copies the building machine's
  `$(NETCoreSdkRuntimeIdentifier)` natives flat into the output, fixing `dotnet run`
  on every OS. Verified: build clean, **582 tests green (330 xunit + 252 NUnit)**, CLI
  smoke PASS, headless Client screenshot smoke reaches `Racing` with the squirrel hull
  rendering. Dist zips untouched (game code unchanged).

- **Iteration 6** (2026-06-11): **V6 keystone — the vessel-layer trio lands.**
  IVessel, IPlayer, IVesselStatus (Deviation #10c: 13 members commented pending
  V7-V19 types), ElementalFloat (LerpUnclamped scaling over levels -5..15),
  ElementalShipComponent (reflective ElementalFloat binding), IVesselHUDController,
  R_ShipElementStatsHandler, ShipActionSO + ShipActionExecutorBase +
  ActionExecutorRegistry + legacy ShipAction; AudioSystem type-preserving shell
  (Deviation #11, pulled forward from V15 — ActionExecutorRegistry needs the type).
  **Deviations #9 and #10b CLOSED**: ResourceSystem : ElementalShipComponent +
  `[RequireComponent(typeof(IVesselStatus))]` restored verbatim;
  `InputController.vessel` field live again. CS0169 added to NoWarn (Deviation #16);
  CellItem duplicate-using substitution artifact fixed. New ElementalFloatTests:
  scaling theory across the full level range, disabled-float inertness, reflective
  name composition (`Type.field`), registry init/lookup/fallback, IVesselStatus
  null-player defaults frozen ("No-name" / Jade). **594 tests green (342 + 252)**;
  headless client smoke unaffected.

- **Iteration 7** (2026-06-11): **V7 — input/animation layer.** Engine E2 renderer
  data stubs (`Renderer` material array + non-cloning `material` [documented engine
  deviation], `SkinnedMeshRenderer` blend-shape weight store, `TrailRenderer`,
  data-only `Camera` with first-enabled `main`). Ported verbatim: `InputStatus`
  (292L — IsSpawned-switched local/NetworkVariable storage for all 25 input channels,
  owner-gated writes, pause toggle event on both local and replicated paths,
  ResetForReplay preserving player invert preferences) and `VesselAnimation` (152L —
  abstract puppetry driver: idle/dual-stick/single-stick routing, element→blend-shape
  mapping, engine/body flare material writes). **Deviation #15 CLOSED**
  (`TryAddInputStatus` → `gameObject.GetOrAdd<InputStatus>()` verbatim); #10c partial
  restore (`VesselAnimation` member live in IVesselStatus). Test doubles extracted to
  shared `VesselLayerTestDoubles.cs`; 16 new tests (InputStatus spawn/ownership/reset
  matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
  **610 tests green (358 + 252)**; client smoke unaffected.

- **Iteration 17** (2026-06-12): **Convergence rung 2 — real prism trails + real
  Skimmer contact in the playable client.** New `SkimRacePrisms.cs`
  (SkimRacePrismFactory answering the real VesselPrismController spawn channel with
  the full V15 prism GameObject family; PrismGrowthDriver replicating
  PrismScaleManager's growth math per prism; SkimContactTracker mirroring trigger
  enter/exit overlap state; SkimRaceTrailSkimEnergyEffectSO — the skim-energy race
  rule as a concrete SkimmerPrismEffectSO inside the real impactor dispatch).
  SkimRaceSim: TrailPoint ribbons + distance-skim deleted; near-field skimmer rigged
  (trigger sphere + SkimmerImpactor + Mass-bound Scale ElementalFloat);
  prism-controller wiring (channel, skimmer ref, BaseScale 5×1×6, wavelength 6);
  director Shutdown(); theme grew real (data-only) block-state materials.
  RaceWindow renders the REAL prisms (oriented slabs from each Prism's transform,
  per-corner camera fade) and reports real prism counts; burst cull made
  expansion-aware. Engine perf, behavior-preserving: TriggerPass trigger-indexed
  pair scan (identical visitation order; O(n·T) instead of O(n²) over conserved
  prism fields), GameLoop.UnregisterBehaviour binary search (mass teardown was
  quadratic). 4 new ClientConvergenceTests (spawn-through-real-controller,
  conserved-mass soak, skimmer-trigger-enter energy grant, full race → Finished
  with golf-scored winner). **1088 tests green (836 + 252) in BOTH configs**;
  headless 300/1200-frame runs exit 0 with rung-1-identical claim determinism and
  real-prism trail counts.

- **Iteration 18** (2026-06-12): **Convergence rung 3 — the real CrystalImpactor
  family + the real crystal respawn chain in the playable client.** Ported verbatim:
  `CrystalManager` (abstract base: anchor placement, stable ids, batch spawn,
  per-crystal anchor progression) + `LocalCrystalManager` +
  `SkimmerAdjustElementLevelByCrystalEffectSO` + `VesselIncrementLevelByCrystalEffectSO`.
  Crystal shell grew the manager surface verbatim (CrystalManager/InjectDependencies,
  CanBeCollected, SphereRadius, MoveToNewPos, ChangeDomain + DecayingTheftCoroutine,
  Explode + WaitForImpact — render-side bodies stripped with markers) and the staged
  CT1 deviations CLOSED: `Crystal.Respawn()` → `CrystalManager.RespawnCrystal`,
  `NotifyManagerToExplodeCrystal` → `ExplodeCrystal`, `OmniCrystalImpactor.IsNetworkClient`
  verbatim. Client: new `SkimRaceCrystals.cs` — `SkimRaceCrystalManager` (game-mode
  manager in the real family; per-kind station rigs: Omni / Team (domain-locked, slot
  3 of every 7) / Elemental (skimmer-claimed, consumed via the real fly-to-vessel
  collection); claimed vessel-claim crystals go dark IN PLACE and relight after 12s
  through the real Respawn chain; consumed elemental stations respawn fresh) + the
  race rules as effect assets (`SkimRaceOmniCrystalSurgeEffectSO`,
  `SkimRaceCrystalEnergyEffectSO`, `SkimRaceElementalClaimEffectSO`). Director: all
  level/energy grants deleted (effects own them); claim bookkeeping via the manager's
  claim reports; courses re-sort event-driven off CourseData.OnCellItemsUpdated with
  the real CanBeCollected filter. CLI HexRaceRound wires a manager (its crystals now
  require one — the real chain). Engine: **clone rule extended — `Dictionary<K,V>`
  fields get fresh containers per clone** (CopyFields was overwriting the clone's
  field-initializer dict with the template's reference: every pilot shared ONE
  ResourceSystem.ElementalLevels, so any rival's claim leaked levels onto the human —
  caught by the rung-3 exact-grant test); `Random.insideUnitSphere`/`onUnitSphere`;
  `Instantiate(original, pos, rot, parent)`. RaceWindow draws live crystal transforms
  (claimed elemental crystals visibly fly to their claimer; dark stations hide).
  New tests: CrystalManagerTests (7 — LocalCrystalManager batch/relocate/turn-end/
  explode + shell chain + CanBeCollected), ClientConvergenceTests rung-3 trio
  (exact-element grant through the pipeline; omni dark/relight respawn semantics +
  re-claim; team domain locks in courses AND claims), clone dictionary freeze test.
  **1119 tests green in BOTH configs (867 + 252)**; headless 300/1200-frame runs
  exit 0 — frame 1200: `crystals [7,2,5,2], claims 16, levels C6/M0/S1/T0, trail 786`
  (per-pilot level attribution now exact; prism determinism preserved vs rung 2).

- **Iteration 19** (2026-06-12): **Convergence rung 4 — real scoring + HUD semantics
  in the playable client.** Ported verbatim: `NetworkCrystalCollisionTurnMonitor`
  (NetworkVariable target sync → `GameDataSO.CrystalTargetCount`; CheckForEndOfTurn
  delegates to `gameData.ScoringRule.IsObjectiveReached`; domain-deficit remaining
  display) + `TurnMonitorController` (OnMiniGameTurnStarted → StartMonitors;
  per-frame end check → `InvokeGameTurnConditionsMet`). Client: the scoring rig
  (monitor + controller + `ElementalComebackSystem`) is built per race, host-mode
  `Spawn()`ed (IsServer=true — the scene-placed-NetworkBehaviour contract) and torn
  down in `Shutdown()` (the monitor's async heartbeat must not outlive the race);
  GameDataSO gains the full turn-event set, `GameMode=HexRace`,
  `RequestedDomainCount`, a `HexRaceScoringRuleSO` instance, and reflection-mirrored
  `LocalPlayer`/`LocalRoundStats` (single-process: the human IS the local user).
  Rivals are balanced over the ACTIVE domains (`ActiveDomains[(i+1)%3]` — a 4th
  pilot becomes the human's Jade teammate; Blue no longer races, it can't win a
  domain-aggregated objective). The director's per-pilot win count is DELETED — the
  race finishes off `OnMiniGameTurnEnd` with the HexRaceController end flow
  collapsed to single-process (AssignScores: winners = finishTime, losers =
  10000 + domain deficit tying within a domain; SortRoundStats(golf);
  CalculateDomainStats; SetResults(BuildResults); InvokeWinnerCalculated +
  InvokeMiniGameEnd); mid-race restart abandons the turn unscored through the same
  protocol. Director publishes per-domain `SetDomainMetricSum`
  (MultiplayerDomainGamesController's server role) → RaceWindow HUD shows ally vs
  opposing DOMAIN totals + the monitor's remaining; the finish scoreboard renders
  `gameData.RoundStatsList` golf order and VICTORY/DEFEAT is by `WinnerDomain`.
  Comeback runs verbatim with the real HexRaceComebackProfile values (Squirrel:
  Space 3 / Time 3, CrystalsCollected source) — trailing domains rise through the
  elementals fundamental; composition note: the verbatim comeback overwrites base
  levels to baseline+bonus each 1s tick while a turn runs, so claim-earned levels
  are transient mid-race (identical to the original HexRace — levels are
  comeback-anchored; Mass/Charge weights are 0 in the real profile). **Energy
  economy balanced (NEXT-UP item 2 closed): `GainPerPrism` 0.045 → 0.025
  (~0.25/s plain ribbon ride, ~0.5/s drift-skimming), boost drain 0.45 → 0.55/s**
  — boosting now outpaces plain skim gain (-0.3/s net) and only drift-skimming
  nearly sustains it (-0.05/s), so the bar breathes instead of pegging while
  boosting (idle non-boosting riders still cap — energy is spent by boosting).
  New tests: ClientConvergenceTests rung-4 quartet — domain-aggregated end (split
  target across teammates, zero director claims), golf standings (winners share
  finishTime; losing domains tie on Encode(deficit); RoundStatsList sorted;
  Results ranked), comeback buffs trailing domain only, full short race through
  the real pipeline. **1143 tests green in BOTH configs (891 + 252)**; headless
  300/1200-frame runs exit 0 — frame 1200: `crystals [6,1,4,1], claims 12,
  domains J7/R1/G4 (remaining 23), trail 786` — identical across repeat runs
  (prism determinism preserved; claim pattern re-baselined by the domain remap +
  comeback Space/Time buffs).

## PRIME AXIS — CLIENT CONVERGENCE (prompter reorientation, 2026-06-12)

> "our goal is to convert everything over so a player cannot tell the difference
> between the original and port. I feel like i should be seeing bigger steps toward
> closing the gaps with each play."

The headless engine reached verbatim fidelity (V1-V19, C1-C6, scoring, port-m2,
contact arc) but the PLAYABLE CLIENT still runs the sprint sim — pulls felt
unchanging because the convergence was invisible from the cockpit. From now on
**every iteration ships a player-feelable convergence step**: replace a client
stand-in with the real ported system. Fidelity arcs continue only in service of
the next rung.

### Convergence ladder (each rung feelable on `git pull` + dotnet run)

1. **Real flight + real AI** ✅ (iteration 16): vessels are real VesselController/
   VesselStatus/VesselTransformer rigs (`SkimRacePilot` in SkimRaceSim.cs); player
   input writes the rig's real `InputStatus` (`RaceWindow._playerStatus = _pilot.Input`,
   SkimInputStatus deleted); rivals are flown by the real AIPilot with per-pilot
   `CellRuntimeDataSO` course registries for crystal retargeting. Verified headless:
   4 pilots claiming (frame 1200: crystals [8,3,8,3], 22 claims), exit 0, HUD/minimap
   intact, yaw+roll gamepad inversions preserved.
2. **Real trails = prisms** ✅ (iteration 17): every trail block is a REAL `Prism`
   spawned by the rig's real `VesselPrismController` async loop — the client's
   `SkimRacePrismFactory` (SkimRacePrisms.cs) answers the spawn channel with the
   full V15 prism family (MeshRenderer/BoxCollider/4 managers/Prism/PrismImpactor/
   ImpactCollider + a `PrismGrowthDriver` per-prism stand-in replicating the
   unported PrismScaleManager's exact growth math). Conserved mass: nothing decays
   prisms; the only sink is race-restart `DespawnAll`. Trail-skim energy flows
   through the REAL skimmer pipeline: near-field `Skimmer` (Scale ElementalFloat
   bound to **Mass** — claims grow your skim reach) + trigger SphereCollider +
   `SkimmerImpactor` → engine TriggerPass → `SkimRaceTrailSkimEnergyEffectSO`
   (the per-vessel effect-asset pattern; drift + Charge bonuses inside). Own fresh
   trail can't self-charge — the verbatim `waitTillOutsideSkimmer` arming delay is
   the protection; lapping back re-arms it. Distance-check skim code deleted.
   `SkimRaceDirector.Shutdown()` winds down all async spawn/AI loops. Engine perf
   (behavior-preserving, prism-scale scenes): TriggerPass pair scan walks trigger
   indices (identical pair visitation order), GameLoop.UnregisterBehaviour binary
   search. Verified headless at frame 1200: claims identical to rung 1
   ([8,3,8,3], 22 claims — determinism preserved), `trail 786` = real prism count.
3. **Real crystals/impactors** ✅ (iteration 18): the whole CrystalImpactor family
   runs the client course — Omni stations claim through OmniCrystalImpactor
   (vessel contact, any domain), every 7th-slot-3 station is a TEAM crystal
   (TeamCrystalImpactor, domain-locked via the real Crystal.ChangeDomain +
   CanBeCollected; AI courses filter on it so no pilot orbits a station it can't
   take), and elemental stations claim through ElementalCrystalImpactor (skimmer
   collection — the crystal flies to its claimer and is consumed). Element levels
   move ONLY through impactor-side effect SOs: the real
   SkimmerAdjustElementLevelByCrystalEffectSO (crystal-side, exactly where the
   original's flora/fauna prefabs wire it) + VesselIncrementLevelByCrystalEffectSO
   (team claims), with the race rules (omni all-four surge, claim energy kickers,
   claim reporting) as SkimRace* effect assets in the same dispatch chains —
   the director's level/energy pokes are deleted. Crystal lifetime runs the REAL
   Crystal.Respawn()/NotifyManagerToExplodeCrystal → CrystalManager chain:
   CrystalManager + LocalCrystalManager ported verbatim (closing the staged CT1
   deviations in Crystal + OmniCrystalImpactor), and the race's
   SkimRaceCrystalManager (a game-mode manager in the real family, like
   Local/NetworkCrystalManager) owns station placement: vessel-claim crystals
   survive their claim and relight in place after the 12s window; consumed
   elemental crystals respawn fresh. Engine: clone rule extended — Dictionary
   fields get fresh containers per clone (a template-shared runtime dict was
   bleeding one pilot's ElementalLevels into the whole field); Random gained
   insideUnitSphere/onUnitSphere; Instantiate gained the (pos, rot, parent)
   overload. Renderer draws live crystal transforms (claimed elemental crystals
   visibly fly to their claimer).
4. **Real scoring + HUD semantics** ✅ (iteration 19): the race ends through the
   REAL pipeline — `NetworkCrystalCollisionTurnMonitor` + `TurnMonitorController`
   ported verbatim and Spawn()ed host-mode in the client; the monitor publishes the
   crystal target into `GameDataSO.CrystalTargetCount` and its `CheckForEndOfTurn`
   delegates to `HexRaceScoringRuleSO.IsObjectiveReached` over
   `ScoringMetrics.SumByDomain` — teammates share their domain total (rivals are
   now balanced over the ACTIVE domains, `ActiveDomains[(i+1)%3]`, so a 4th pilot
   is the human's Jade teammate; Blue no longer races). The director's claim-count
   win check is deleted: it finishes the race off `OnMiniGameTurnEnd`
   (HexRaceController.OnTurnEndedCustom + SyncFinalScores_ClientRpc collapsed to
   single-process): `rule.AssignScores` (winners = finishTime; losers =
   10000 + domain deficit, tying within a domain), `SortRoundStats(golf)`,
   `CalculateDomainStats`, `SetResults(rule.BuildResults)`, `InvokeWinnerCalculated`
   + `InvokeMiniGameEnd`. The RaceWindow scoreboard renders `gameData.RoundStatsList`
   golf order; VICTORY = your DOMAIN won (`gameData.WinnerDomain`); the in-race HUD
   shows ally-domain total vs opposing-domain totals via
   `GameDataSO.GetDomainMetricSum` (the director publishes SumByDomain — the
   MultiplayerDomainGamesController server role) + the monitor's display-channel
   remaining. `ElementalComebackSystem` runs verbatim beside the race with the real
   HexRaceComebackProfile values (Squirrel: Space 3 / Time 3,
   ScoreDifferenceSource.CrystalsCollected) — trailing DOMAINS rise through the
   elementals fundamental; note the verbatim comeback overwrites base element
   levels to baseline+bonus each 1s tick while a turn is active, so claim-earned
   levels are transient during a race (the original HexRace behaves identically —
   levels are comeback-anchored; Mass/Charge weights are 0 in the real profile).
   Mid-race restart ends the turn through the real protocol
   (`InvokeGameTurnConditionsMet` with the director already out of Racing →
   unscored). LocalPlayer/LocalRoundStats are reflection-mirrored onto GameDataSO
   (single-process: the human IS the local user). Energy economy balanced (see
   iteration log). Verified headless frame 1200: `crystals [6,1,4,1], claims 12,
   domains J7/R1/G4 (remaining 23), trail 786` — deterministic across runs.
5. **Real look**: SO_ColorSet domain palettes + SO_MaterialSet-driven visuals.
6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
   the real systems.

## REORIENTATION 2 — TRACK BLEEDING-EDGE (prompter, 2026-06-13)

> "pull latest from bleeding-edge … /loop until this has closed a massive number
> of gaps between what it is like to play on bleeding edge with this port"

bleeding-edge merged INTO this branch at 842c825c (325 commits, merge-base moved
29b5f422 → c833c580). The port now chases a LIVE game. Two standing lanes join the
convergence ladder, and every future bleeding-edge merge reopens them:

0. **TOYS ✅ (ported, iteration 21; menu-swap deviations CLOSED by the
   vessel-initializer arc — see below)** — all 11 Controller/Toys + 5 SO files
   verbatim (mesh arc + UI-shell deviations still marked; domain changer AND
   vessel changer now work end-to-end — RequestSetDomain RPC + the real
   MenuServerPlayerVesselInitializer.RequestSwap). Engine gains
   LineRenderer, GameObject.CreatePrimitive, ShadowCastingMode, the TMPro
   data-shim (`using TMPro;` → `using CosmicShore.Engine.UI;` — README updated),
   and CreateInstance<T> without the non-original new() constraint. 21 tests.
   Client integration notes live in the toys agent report (ToyboxController needs
   _gameData/_freestyleEvents + freestyle transition events in the client scene).
1. **DRIFT-SYNC ✅ (complete, iteration 21)** — 57/57 resolved (45 verbatim, 11
   with carried deviations, 1 deleted-upstream). Headliners: PrismAOERegistry →
   `PrismSpatialIndex` (upstream 1,003-line rewrite: bucket occupancy grid,
   TryReserve, QuerySphere), Cell volume-is-the-spine (LiveVolume/OpposingVolume,
   Calm/Restless/Frenzy), RoundStats B10 (n_Domain retired) + GoalsScored +
   ClearEventSubscriptions, fauna index-served senses + sealed Die→crystal drop,
   upstream's own revert of the menu trail cap. New deps ported:
   PrismColliderLodManager, EndConditionOverridesSO, LifeFormCrystal,
   ElementalCrystalSetSO. Engine: Mathf.PerlinNoise, layer-masked
   OverlapSphereNonAlloc, DisallowMultipleComponent, Crystal.ActivateCrystal.
   Per-line markers in Port/docs/DRIFT_2026-06-13.txt. Was: 57 already-ported
   files changed upstream (full list:
   `Port/docs/DRIFT_2026-06-13.txt`). Ported copies must be re-verbatimed against
   the new merge-base: for each file apply `git diff 29b5f422..c833c580 -- <unity
   file>` onto the ported copy with the mechanical substitutions. Load-bearing
   first: VesselTransformer, VesselStatus, VesselPrismController, Prism, Trail,
   AIPilot, Cell, GameDataSO, RoundStats, scoring/monitors, Boid/Fauna family.
2. **NEW SYSTEMS** — shipped on bleeding-edge, absent from the port:
   - **Toys** (`Controller/Toys` 11 files + `ScriptableObjects/Toys` 5): freestyle
     toy system (domain/vessel changer flip-sets, mini ship models, painting toy) —
     player-facing in the lava-lamp/freestyle flow.
   - **AstroLeague ✅ (ported, controller-chain iteration)** (`Controller/Arcade/AstroLeague/`
     7 files + `AstroLeagueObjectiveProvider` + `IObjectiveProvider`): the full mode runs
     headless through the real chain — `dotnet run --project src/CosmicShore.Cli -- --mode
     astroleague [--players N] [--seed S]`. See Deviation #18 + the iteration log.
   - **Tournament ✅ (ported, tournament arc)** (`Controller/Arcade/Tournament/` 4 files +
     `TournamentDataSO` + `TournamentStandingsFormatter` + `DomainColorPaletteSO`): the
     Maelstrom session meta runs headless through the real chain — `dotnet run --project
     src/CosmicShore.Cli -- --mode tournament [--players N] [--seed S]`. See Deviation #19 +
     the iteration log. Still open in this lane: TournamentRoundCard / TournamentPlayerCard /
     TournamentSummaryPlayerCard / TournamentDomainScoreView (UI card prefab views) +
     Scoreboard's tournament wallet credit.
   - SandboxBenchmarkController, Settings additions, CloudData, Privacy UI.

Gap-closure definition for this /loop: drift-sync complete + toys playable in the
client + AstroLeague headless round running + remaining ladder rungs (5: real look,
6: ambience/modes) — each iteration ships a player-feelable step, per Reorientation 1.

## NEXT UP (milestone pivot, 2026-07-08)

**North star (prompter, 2026-07-08):** `/loop` until a milestone where the
**menu-UI is testable AND multiple game modes are playable**, as close to the
Unity build as we can get. *"Don't skip steps or rush. Build a foundation for
excellence, not a proof of concept."* The full sequenced plan is in
**"MILESTONE: MENU-UI + MULTI-MODE PLAYABLE"** below (8 arcs, A→I). The
services-phase deviation backlog is drained (see the shipped arcs) — the loop
now works that milestone plan.

> **Drift-sync note (2026-07-09):** bleeding-edge moved `97d4ab29` → `f2b8f5aa`
> (PR #581 painting-toy rework) and was merged + drift-synced this iteration —
> see `docs/DRIFT_2026-07-09.txt` (8 new files, 8 changed, 7 engine growths,
> 5 tests updated). Arc A deferred one iteration by that drift-sync. **Gate
> value change:** the freestyle client diag is now **`toys 12`** (was `toys 9`)
> — intended + deterministic: the painting toy fans its 4-station default
> gallery. race@1200 unchanged (`trail 786`). **Deferred follow-up:** port
> upstream's new `PaintingPresetLibraryTests.cs` (221L NUnit) into the Ported
> suite for additive preset-geometry coverage.

1. **Arc G part 2 / Arc I entry — the menu→game→menu HANDOFF.** Part 1 (below)
   proved the windowed mode host: `--mode play` steps the REAL HexRace round
   (the Setup/Step/Finish `HexRaceRoundHandle` split; CLI `Run` is the same
   handle, transcript-pinned) and renders it. Now connect the seams: in the
   windowed client, `GameDataSO.OnLaunchGame` (which the configure modal
   already raises end-to-end in menushell) tears down the MENU world
   (dispose its GameLoop — fresh-world statics make menu/game loops mutually
   exclusive) and stands up the mode-host world for `gameData.GameMode`;
   on `FinishAndScore` + a beat, dispose the round handle and REBUILD the
   menu world (BuildMenu is already a from-scratch constructor — Unity's
   scene-unload/reload semantics). Extend the handle split to a second mode
   (CrystalCaptureRound is the nearest sibling) so the host switches on
   GameMode. Remaining Arc F screens (Hangar / Leaderboards / Store) queue
   behind this spine.
2. **Track bleeding-edge**: merge upstream again next iteration; every merge
   reopens the drift-sync lane (survey + `docs/DRIFT_<date>.txt` per precedent).
3. Update this file, commit, push.

### Arc G part 1 ✅ (2026-07-10) — the windowed MODE HOST stands up a real round

**The Setup/Step/Finish split (`HexRaceRoundHandle`):** `HexRaceRound.Run`'s
world construction, frame loop, and scoring were factored into a steppable
handle — `Setup(options, liveLog)` builds the full round world (loop, theme,
GameDataSO + scoring rule, Cell + course registry, crystal manager, prefab
fixture, AI field, first staged crystal), `StepFrame()` is ONE engine frame +
the turn-monitor-shaped objective check, `CompleteStepping()` /
`FinishAndScore()` are the CLI's post-loop branches, and `Dispose()` is the
CLI's finally block (wind-down, cell unregister, destroy flush, singleton
resets, sink restore + EngineErrors flush) followed by loop disposal. `Run`
is now the handle stepped in a while-loop — **pre/post transcripts diffed
byte-identical for `--mode hexrace` AND `--mode tournament`** (3 legs), and a
new `SteppedHandle_MatchesBlockingRun_Exactly` test pins the parity
permanently. The handle exposes the world a renderer needs (GameData,
Players, ActiveCrystal, Course(+Elements), CourseIndex, Target,
RaceStartTime, FramesStepped).

**`ModeHostWindow` (`--mode play [--players N] [--target N]`):** the windowed
twin of the CLI round — one `StepFrame()` per window update (fixed 1/60,
deterministic), wireframe render pass in the RaceWindow pos3+color4 idiom
(seeded System.Random starfield — NEVER the engine RNG, the sim owns that
stream; upcoming-course crosses; the active crystal as a spinning
element-tinted octahedron; each AI vessel a domain-tinted arrow; sim-derived
chase camera, no wall-clock smoothing) + UiRenderer HUD (domain sums,
per-pilot crystals, and the WINNER + standings block once the objective
lands). Client gained a ProjectReference to CosmicShore.Cli — the round
drivers ARE the shared world constructors (still 7 csproj). GL-state finding
encoded: `UiRenderer.End` hands back sim state (depth ON, additive blend), so
a host must clear DEPTH and own its blend/depth setup each frame — the
mode-host pass runs depth-off additive over the cleared indigo.

**Verified:** full round to completion IN THE WINDOW (target 6: objective at
29.32s/1759 frames, winner AI-1 Jade, golf standings rendered). **New gate
line `play@1200`** (`--mode play --seed 42 --players 4 --target 6`) →
`t 20.00, claims 3, jade 3 ruby 0 gold 0, state Racing, winner none` —
byte-stable two runs. **1633 tests green in BOTH configs (1295 + 338)**;
5 CLI modes exit 0; race `trail 786` byte-stable ×2; freestyle `toys 12`;
uidemo + menushell@216 unchanged. bleeding-edge unmoved at `f2b8f5aa`.

### Arc F part 2b-iii(b) ✅ (2026-07-10) — the configure modal; the ARCADE UNIT IS COMPLETE

**`ArcadeGameConfigureModal` (1381L) ported verbatim** — zero behavior
deviations: the whole host flow (SetSelectedGame → Screen 1 game defaults →
commit-once OnConfirmConfiguration → Screen 2 → ready-up → launch), the
client-RPC flow (HandleConfigOpenedOnClient / Closed / ScreenChanged +
ApplyHostOnlyInteractability), the per-player chip lifecycle
(NetDomain.OnValueChanged reparent delegates, late-joiner spawn watch,
despawn-on-close), `ShouldLocalPlayerLaunch` launch authority,
DC-bounded-by-PC stepper math with per-game MinDomainsAllowed floors, ship
cycling with the 4-rule default (prev class → saved loadout → Dolphin →
first), and the GameDataSO sync trio (config / ship / local-player vessel
type). **Engine growths:** `Object.GetInstanceID()` (session-unique,
construction-ordered ids) and `VideoPlayer.clip` + `VideoClip` stub.
**Port growths:** `SO_GameModeQuestData` (verbatim — quest-chain data +
`QuestTargetType`), `GameModeProgressionService` shell grew
`GetMaxUnlockedIntensity`/`IsIntensityUnlocked`/`GetQuestForMode` (shell:
all unlocked / null quest — callers null-guard exactly like upstream),
`ToastNotificationAPI` log-only shell (its lone caller is unreachable while
the progression shell returns null quests), `InternalsVisibleTo("CosmicShore.
Tests")` on CosmicShore.Game (upstream internals were same-assembly-visible).
**Un-carries:** ExploreView's last two deviation lines RESTORED (modal field +
SelectGame's `ModalWindowIn`/`SetSelectedGame`) — ExploreView is now fully
live; ArcadeScreen's menushell wiring completed (Explore/Loadout pair + both
toggles — the 2b-ii guarded NREs are gone). **Menushell configure flow
(shipping code end-to-end):** frame 90 HEX RACE card → Screen 1, 120
intensity 2, 132 PC “+” (stepper → 2), 150 CONFIRM → Screen 2 (Jade selected
green, Ruby/Gold dimmed per DC, Dolphin summary), 180 START → solo path →
`launch HexRace@MinigameHexRace players=2 ai=1 intensity=2 dc=1` + LAUNCHING
banner. Two shell findings encoded: engine `Selectable.OnEnable` lazily
re-adopts a target graphic, so components that own their visuals get
`transition = Transition.None` (not a null target); the configure modal's
CanvasGroup hide is a scaled-time `WaitForSeconds` and the menu holds
`timeScale = 0` (pause-on-non-HOME), so the visual hide defers while the
modal STACK pops immediately — faithful to upstream semantics, and the
capture shows the arcade modal back on top (`arcadeModal ARCADE`).
**Tests: +10 `ArcadeGameConfigureModalTests`** (Screen-1 defaults sync,
per-game DC floor, commit-once guard, solo-launch GameDataSO payload +
launch-event single-fire + modal hide, close-resets-config + guard re-arm,
intensity clamp, PC→DC re-bound, ship cycling wrap + class broadcast, Blue
sentinel hidden + DC dimming, ShouldLocalPlayerLaunch truth table).
**1632 tests green in BOTH configs (1294 + 338)**; 5 CLI modes exit 0; race
canonical `--seed 42 --frames 1200` → `trail 786` byte-stable AND
pixel-identical to pre-change HEAD (an off-baseline `--rivals 2` probe this
iteration measured `trail 607` — different field, not drift); freestyle
`toys 12`; uidemo unchanged. **New gate line: `menushell@216`** →
`active HANGAR, slideX -5120.0, modalStack open, arcadeModal ARCADE,
gameCards 3, configIntensity 0, configPlayers 0, configDomains 1, launch
HexRace@MinigameHexRace players=2 ai=1 intensity=2 dc=1, paused True` (the
config zeros PROVE the post-launch ResetState). bleeding-edge unmoved at
`f2b8f5aa`.

### Arc F part 2b-iii(a) ✅ (2026-07-10) — the modal's foundation + the Arcade launcher

**Nine verbatim ports:** `ArcadeGameConfigSO` (the modal's runtime config
state), `IntStepper` (the generic ± stepper both PC and DC selection reuse),
`IntensitySelectButton` (selected/active/locked tri-state with the
OnLockedSelect path), `FavoriteIcon`, `DomainInfoData` (per-domain tile +
avatar strip; the #if UNITY_EDITOR OnValidate block compiles out like a
player build), `DomainAvatarChip`, **`ArcadeConfigSyncManager`** (243L — the
full Netcode config relay: host commit → NetDomain Jade reset →
OpenConfigOnClients_ClientRpc, close/screen-change relays, the ready-up
ServerRpc counter with AllPlayersReady broadcast — compiles against the
port's live RPC surface), **`Arcade` launcher** (198L SingletonPersistent —
all three launch paths write GameDataSO and fire InvokeGameLaunch, the real
SOAP seam; only the Animator field is deviation-marked, consumed solely by
commented legacy code upstream), `SO_Mission` + `SO_MissionList` +
`SO_TrainingGameList` (the launcher's lookup surfaces; Threat/SpawnMode
included). **Un-carry:** ExploreView's `PlaySelectedGame` now calls the REAL
`Arcade.Instance.LaunchArcadeGame` (RESTORED marker). **1622 tests green in
BOTH configs (1284 + 338)**; 5 CLI modes exit 0; all four diags
byte-identical (menushell unchanged — the launcher path isn't in the
screenshot flow until the modal lands). bleeding-edge unmoved at `f2b8f5aa`.

### Arc F part 2b-ii ✅ (2026-07-10) — the arcade family renders through the shipping modal

**Five verbatim ports:** `GameCard` (favorites star, lock tint, CTA click),
`DailyChallengeCard` (self-disabling COMING SOON), `LoadoutCard`,
`ArcadeExploreView` (the real populate/sort pipeline: favorites-first then
alphabetical, progression lock gating, dpad grid registration; ONLY the
ArcadeGameConfigureModal open + Arcade.Instance launch lines are
deviation-marked — 2b-iii's unit), `ArcadeLoadoutView` (fully live — launches
through gameData.SyncFromArcadeGame + InvokeGameLaunch, the real SOAP path),
`ArcadeScreen` (CanvasGroup view toggling). **Three shells (documented):**
`CatalogManager` + `Inventory` (name-lookup surface; empty inventory ==
shipping default since RespectInventoryForGameSelection is false),
`GameModeProgressionService` (Instance/OnProgressionChanged/
IsGameModeUnlocked→true; the real 788L quest service is its own unit),
`MiniGame` STATIC shell (the launch-config statics verbatim; the 482L legacy
instance machinery stays unported). **Engine fix (faithful):**
GraphicRaycaster now honors `CanvasGroup.blocksRaycasts=false` — a hidden
modal's alpha-0 full-screen dim was swallowing every click (the original
culls such subtrees from raycasts; ignoreParentGroups re-opt-in deferred
until a consumer arrives).

**menushell re-baselined:** a sixth nav button (ARCADE) drives the shipping
`OnClickArcadeNav → ArcadeModal.ModalWindowIn` path at frame 60 (after the
frame-30 HANGAR slide) — the REAL ModalWindowManager + ArcadeScreen +
ExploreView populate a 3-card GameCard grid from hand-authored SO_ArcadeGame
entries, alphabetically sorted by the real code (CRYSTAL CAPTURE / HEX RACE /
JOUST), with the daily-challenge card COMING-SOON-disabled. White card faces
are the verbatim SetLocked(false) tint — color arrives with Arc-E sprite
pixels. Scene services grew CallToActionSystem + AudioSystem. New gate line
(byte-stable): `menushell@90` → `active HANGAR, slideX -5120.0, modalStack
open, arcadeModal ARCADE, gameCards 3, paused True`. **1622 tests green in
BOTH configs (1284 + 338)**; 5 CLI modes exit 0; race/freestyle/uidemo diags
byte-identical. bleeding-edge unmoved at `f2b8f5aa`.

> **Arcade dependency graph (mapped 2026-07-10):** ArcadeScreen → {ArcadeExploreView,
> ArcadeLoadoutView, Toggle✅}; ExploreView → {SO_GameList✅, GameCard,
> ArcadeDPadNav✅, DailyChallengeCard, ArcadeGameConfigureModal(2b-iii),
> CatalogManager(shell), GameModeProgressionService(shell), FavoriteSystem✅,
> LoadoutSystem✅, MiniGame statics(2b-iii), Arcade launcher(2b-iii),
> CallToActionTarget✅, FTUEEventManager✅, VesselClassTypeVariable✅,
> SO_Vessel✅}; GameCard → {SO_GameList✅, FavoriteSystem✅, FTUEEventManager✅,
> AudioSystem✅, Button✅/Image✅/TMP✅}. ✅ = live in the port.

### Arc F part 2b-i ✅ (2026-07-10) — the arcade service foundation

Six verbatim ports + one engine growth, the layer the arcade cards/views
stand on: **`Loadout`** + **`ArcadeGameLoadout`** (launch-config models with
the all-defaults `Initialized` sentinel), **`LoadoutSystem`** (per-game +
player loadout persistence through the real DataAccessor JSON store),
**`FavoriteSystem`** (toggle/notify/persist; `OnFavoriteChanged` event),
**`CallToActionTarget`** (registers against the live CallToActionSystem),
**`FTUEEventManager`** (the FTUE static event hub — first FTUE-directory
port), **`ArcadeDPadNav`** (the arcade grid's dpad navigation over real
Buttons/ScrollRect — needed the engine **`DpadControl` growth** on Gamepad:
up/down/left/right ButtonControls). **4 headless tests**
(`ArcadeServiceFoundationTests`): favorite toggle flips state + notifies +
leaves no net state, game-loadout save/load round-trip through the real
store, unknown-mode fallback returns the uninitialized sentinel, active-slot
writes follow the index. **1622 tests green in BOTH configs (1284 + 338)**;
5 CLI modes exit 0; all four diags byte-identical. bleeding-edge unmoved at
`f2b8f5aa`.

### Arc F part 2a ✅ (2026-07-10) — the switcher contract, test-pinned

**8 headless tests** (`ScreenSwitcherTests`) drive the REAL ported
ScreenSwitcher in a transcribed Menu_Main shell (canvas + scaler + Screens
root on the world-(0,0) pivot contract + 5 panels + UserActionSystem; the
freestyle test wires a real `MenuFreestyleEventsContainerSO` through the
inactive-GO-then-activate pattern so OnEnable subscribes): HOME landing +
viewport panel layout (1920-unit panels at i×1920, root slid −2 viewports in
world pixels), arrow navigation skipping disabled screens BOTH directions
(PORT and ARK hops + off-the-end stays put), direct-nav rejection of disabled
screens, IScreen exit-before-enter ordering, the pause-on-non-HOME rule
through the real PauseSystem (reset in Dispose — static), return-state
consumed across switcher GENERATIONS (new GameLoop + new shell lands on the
persisted screen and deletes the key), the modal stack (stacked top-wins,
ReturnToModal PlayerPrefs written on push / cleared at empty), and the
freestyle handoff (sendNavigationEvents flip + screens CanvasGroup hide +
navigation blocked while flying + IScreen re-entry on exit). PlayerPrefs
return-state hygiene via the switcher's own RunOnStart before AND after every
test. **1618 tests green in BOTH configs (1280 + 338)**; 5 CLI modes exit 0;
all FOUR client diags byte-identical (`trail 786` / `toys 12` / uidemo /
menushell). bleeding-edge unmoved at `f2b8f5aa`.

### Arc F part 1 ✅ (2026-07-10) — the menu shell is LIVE on screen

**The milestone's "menu renders + navigates + is testable" sub-goal is now
literal**: `--mode menushell` hosts the REAL ported `ScreenSwitcher` on the
full first-party stack — five viewport-wide screen panels (STORE/ARK/HOME/
PORT/HANGAR in shipping visual order), a nav bar of real Buttons wired to the
shipping `OnClick*Nav` handlers, `HomeScreen` living on the HOME panel, and
PORT/ARK disabled exactly like the shipping menu. A synthetic pointer click
(full raycast/dispatch) presses the HANGAR nav at frame 30; the switcher
slides one viewport per index over its 0.5s coroutine easing (fixed-step
ticking → the frame-90 screenshot always lands on the settled layout);
`paused True` in the diag is the shipping "pause on non-HOME screens" rule
firing through the real `PauseSystem`.

**Ports (verbatim + standard substitutions):** `IScreen`, `MenuAudio`,
`ModalWindowManager` (Animator clips deviation-marked; the CanvasGroup
show/hide carrying all observable state is live), **`ScreenSwitcher` (854L)**
— modal stack, PlayerPrefs return-state, gamepad trigger nav, IScreen
lifecycle, freestyle handoff (`sendNavigationEvents` flip), nav-bar icon
toggling, viewport layout + slide; only the HangarScreen/LeaderboardsMenu
references are deviation-marked (they port in part 2). `HomeScreen` (95L).
**Growths:** engine `WaitForEndOfFrame` yield type (runner's default-case
next-frame resume, documented), `AudioSystem.PlayMenuAudio` no-op surface.
**Scene-contract findings baked into the menushell:** `NavigateTo` writes
`transform.position` with y=0, so the Screens container's pivot must rest at
world (0,0) (bottom-left anchor/pivot); Menu_Main's `UserActionSystem`
singleton must exist (HANGAR nav completes ViewHangarMenu through it); hosts
invoke the data-only `[RuntimeInitializeOnLoadMethod]` `RunOnStart` clear.
**Font fix:** glyph advance 0.75→0.875 (font8x8 fills 7 of 8 columns; large
titles overlapped) — uidemo PNG baseline rebased, diag line unchanged.

**Gate lines (byte-stable, two runs each):** `menushell@90` →
`active HANGAR, slideX -5120.0, modalStack empty, paused True`; `uidemo@60`
unchanged diag. **1610 tests green in BOTH configs (1272 + 338)**; 5 CLI
modes exit 0; race/freestyle diags byte-identical (`trail 786` / `toys 12`).
bleeding-edge unmoved at `f2b8f5aa`.

> **Flake note:** the known pre-existing Release flake re-occurred once
> during the Arc-C gate (clean on immediate re-run) — still not a regression.

### Arc C ✅ (2026-07-09) — client render primitives; the UI stack is VISIBLE

The first pixels out of the first-party UI framework — and the whole
milestone stack in one deterministic image (`--mode uidemo`): Arc-A geometry
scaled by the CanvasScaler, an Arc-B fitter-hugged VerticalLayoutGroup menu
panel, an Arc-D synthetic click leaving its selected tint on a Button, all
rasterized by the new Arc-C pass.

**Client files:** `UiFont` — embedded 8×8 public-domain bitmap font
(font8x8_basic, Daniel Hepper / Marcel Sondaar-IBM VGA lineage, fetched
verbatim; 95 ASCII glyphs) laid on a 128×48 atlas with the last cell baked
SOLID white so rects and text share ONE texture/batch; `UiRenderer` — the UI
quad pass: one textured shader alongside the sims' vertex-color one,
pos2+uv2+rgba triangle batch, pixel-space y-up ortho, `DrawRect`/`DrawText`
(monospace 0.75-advance, newline stacking)/`MeasureText`, NEAREST filtering
(crisp + driver-deterministic), standard alpha inside Begin/End with the
sims' additive+depth state restored on End; `UiCanvasBridge` — renders the
live engine canvas tree with the EXACT GraphicRaycaster walk (hierarchy
order = draw order, nested canvases own their subtrees), root canvases
back-to-front by sortingOrder, CanvasGroup alpha multiplying down the tree,
Image/RawImage → tinted rect at world corners (sprite PIXELS ride with the
Arc-E content pipeline), TMP_Text → atlas text with the TMP alignment
bit-layout (low byte horizontal / high byte vertical) and **fontSize × the
rect's world scale** (canvas units → pixels exactly as the corners convert —
caught visually in the first screenshot and fixed); `UiDemoWindow` +
`--mode uidemo` in Program — the verification host, screenshot + diag line
at frame 60.

**New gate line (byte-stable, two runs):** `uidemo@60` →
`scale 0.6667, graphics 6, texts 6, panel 712x449, clicks 1, selected
Row_HANGAR`. **1610 tests green in BOTH configs (1272 + 338)**; 5 CLI modes
exit 0; race/freestyle diags byte-identical (`trail 786` / `toys 12`) — the
UI pass touches nothing in the sims. bleeding-edge unmoved at `f2b8f5aa`.

### Arc D part 2 ✅ (2026-07-09) — navigation + used controls; ARC D CLOSED

The gamepad nav-ring foundation + the interactive controls the project
actually uses (usage-scanned: Toggle 36 files / Slider 8 / ScrollRect 6 /
TMP_Dropdown 5 / TMP_InputField 7 — InputField/Dropdown growth rides with
their Arc-F consumers since ported code only reads `.text` today; Scrollbar/
Dropdown/ToggleGroup 0 uses, skipped):

**`Engine.UI`:** `Navigation.cs` (MoveDirection, AxisEventData, IMoveHandler,
the Navigation struct — None/Horizontal/Vertical/Automatic/Explicit +
selectOn* links); **`Selectable` grown** — live registry (`allSelectables`,
GameLoop fresh-world reset alongside the raycaster registry), `navigation`
property, `FindSelectableOnLeft/Right/Up/Down` (Explicit follows authored
links; Automatic searches the registry from the rect's edge with the
original dot ÷ distance² scoring, skipping non-interactable/None/inactive
candidates), `OnMove` navigating via `eventData.selectedObject`;
**`StandaloneInputModule`** grew `Move`/`Submit`/`Cancel` — all gated on
`EventSystem.sendNavigationEvents` (the exact flag `ScreenSwitcher` flips for
freestyle pad ownership); **`Toggle`** (isOn/onValueChanged/
SetIsOnWithoutNotify, click+submit flip, checkmark alpha — instant-apply like
the Selectable tints); **`Slider`** (the REAL value model: clamp, wholeNumbers
rounding, normalizedValue lerp, silent writes; handle/fill visual mapping
documented as Arc-C work); **`ScrollRect`** (rides the proven drag pipeline:
begin-drag anchors post-threshold exactly like the original, per-axis gates,
Clamped slack bounds, wheel × scrollSensitivity, programmatic `velocity`
flings decaying by decelerationRate in LateUpdate — the GameEventFeed's
usage — normalized-position travel mapping both ways; Elastic clamps like
Clamped headless, documented).

**11 headless tests** (`EngineUiNavigationTests`): automatic-graph stepping
(including off-the-end stays put), the sendNavigationEvents gate both ways,
explicit links overriding positions, non-interactable candidates skipped,
Submit driving the selected Button (nav-gated), Toggle flip/notify/silent
write/checkmark alpha/interactable gate, Slider clamp/round/notify/silent/
normalized round-trip, ScrollRect drag pan with threshold anchoring + slack
clamp, wheel sensitivity, velocity fling decay in the loop (600-tick tail to
zero, never past slack), normalized both-axis round-trips. **1610 tests green
in BOTH configs (1272 + 338)**; 5 CLI modes exit 0; both client diags
byte-identical (`trail 786` / `toys 12`). bleeding-edge unmoved at `f2b8f5aa`.

### Arc D part 1 ✅ (2026-07-09) — the event-system core (headless)

The full pointer pipeline over the solved rects, proven with synthetic events
(the original's module split: hardware backends and tests share one injection
seam, so the dispatch rules are proven long before a window exists):

**`Engine.UI` new:** `EventInterfaces` (IEventSystemHandler + the
enter/exit/down/up/click/drag/scroll/select/deselect/submit/cancel family —
the original's EventSystems namespace folds into Engine.UI), `EventData`
(BaseEventData used/selectedObject; PointerEventData with press/drag/hover
context; RaycastResult), `ExecuteEvents` (Execute / ExecuteHierarchy /
GetEventHandler ancestor walks + the static functor set; disabled Behaviours
ineligible), `BaseRaycaster` (self-registering registry, the original's
RaycasterManager), **`GraphicRaycaster` REAL** (hierarchy walk = draw order,
later siblings on top; raycastTarget + activeInHierarchy gating; containment
via Arc-A world corners; nested canvases own their subtrees),
`RectTransformUtility` (point-in-quad RectangleContainsScreenPoint over the
clockwise corner winding + ScreenPointToLocalPointInRectangle), `EventSystem`
(current, sendNavigationEvents, selection with deselect→select ordering +
re-entrancy guard, RaycastAll sorted sortingOrder→depth, pixelDragThreshold),
`StandaloneInputModule` (the pointer state machine: enter/exit walked to the
common hover root, press target = down-handler else click-handler owner,
click only when release lands on the press target and eligibility survived,
drag threshold + cross-object drags release the press and kill the click,
scroll bubbling), `Selectable` (ColorBlock + Transition; state machine
Normal/Highlighted/Pressed/Selected/Disabled driving targetGraphic tint —
instant-apply documented deviation vs CrossFade — pointer-down self-select,
interactable-off deselects), **`Button` grown** from the shim onto
Selectable + IPointerClickHandler + ISubmitHandler (onClick contract intact —
all existing harness `onClick.Invoke()` call sites unaffected).

**Engine fix (fresh-world reset):** GameLoop's constructor now resets static
UI state (`BaseRaycaster.ResetRegistry`, `LayoutRebuilder.ResetQueue`,
`EventSystem.current = null`) — same rationale as `Time.Reset()`: loop
disposal skips OnDisable, so a prior world's registrations would leak into
the next (surfaced as cross-test contamination; would equally bite any
sequential-loop host).

**12 headless tests** (`EngineUiEventTests`, synthetic injection only):
topmost-sibling raycast ordering, target/active/bounds gating, down→up→click
on one object with position payload, click landing on the ancestor handler
when the child is hit, click suppressed on cross-object release, drag
threshold + endDrag, cross-object drag releasing the press and killing the
click (the ScreenSwitcher shape), enter/exit walking to the common hover
root, select/deselect ordering, Selectable pointer-down self-select +
outside-press deselect, Button on the full stack (pressed/selected/disabled
tints + onClick + interactable gate), EventSystem.current +
IsPointerOverGameObject tracking. **1599 tests green in BOTH configs
(1261 + 338)**; 5 CLI modes exit 0; both client diags byte-identical
(`trail 786` / `toys 12`). bleeding-edge unmoved at `f2b8f5aa`.

> **Known flake (pre-existing, logged 2026-07-09):** full Release suite fails
> ~1-in-10 under CPU load (`SpawnerAdapterC6Tests` or `HeadlessRoundTests`) —
> verified present on 8053b22f before the milestone arcs; not a regression.
> Worth a dedicated diagnosis iteration if it worsens.

### Arc B part 2 ✅ (2026-07-09) — the Graphic/Image family; ARC B CLOSED

The render-facing component model over the layout core, split per convention —
REAL where layout consumes it, data where only pixels do (Arc C rasterizes):

**Engine growths:** `Vector2Int` (mirrors Vector3Int; Arc D screen coords),
`Texture`/`Texture2D` (Rendering; width/height data), **`Sprite` grown** from
the empty V11 stub to real geometry — texture/rect/pivot/border (L,B,R,T)/
pixelsPerUnit + the `Create` factory; `Canvas.referencePixelsPerUnit` (same
pull-from-scaler rule as scaleFactor). **`Engine.UI` new:** `Graphic` (abstract:
color→SetVerticesDirty, raycastTarget, cached `rectTransform` that CONVERTS a
plain-Transform host in place — the RequireComponent equivalent via the Arc-A
converter, canvas walk, SetAllDirty/SetLayoutDirty→MarkLayoutForRebuild +
no-op vertex/material hooks, OnEnable/OnDisable dirty marks),
`MaskableGraphic` (maskable), **`Image`** (sprite/overrideSprite/type/fill*/
preserveAspect/pixelsPerUnitMultiplier + the REAL ILayoutElement face:
preferred = sprite rect ÷ (sprite ppu ÷ canvas reference ppu) × 1/multiplier,
Sliced/Tiled → border sums, min 0/flexible −1/priority 0, SetNativeSize),
**`RawImage`** (texture/uvRect/SetNativeSize×uvSpan; faithfully NOT an
ILayoutElement — no size opinion), `Mask`/`RectMask2D` (clipper data until
Arc C), `GraphicRaycaster` (data until Arc D makes the raycast real).

**Un-carries:** `SceneTransitionManager.CreateFadeOverlay`+`AdoptSplashOverlay`
— the full overlay dressing restored verbatim (Canvas ScreenSpaceOverlay
sortingOrder 32767 + CanvasScaler ScaleWithScreenSize 1920×1080 +
GraphicRaycaster + full-stretch RectTransform + Image fadeColor/raycastTarget);
`CountdownTimer` — the `Image countdownDisplay` field + ALL non-tween
presentation restored (display activation, per-beat sprite swap/scale
reset/urgent tint with the original's null-animSettings defaults, between-beat
alpha reset, final hide); only the DOTween tweens, beep SFX, and
HUDAnimationSettingsSO stay deviation-marked. The 3 CLI rounds + the chain
test now transcribe the scene's display wiring (Image child via
SetPrivateField).

**12 headless tests** (`EngineUiGraphicTests`): preferred-size maths (ppu
variants, multiplier, sliced border, canvas reference-ppu division), the
marquee (Image-with-sprite inside a fitter-hugged group sizes the panel),
sprite-swap → canvas-slot re-solve (dirty propagation through the GameLoop
tick), LayoutElement priority override, SetNativeSize both kinds, RawImage's
no-opinion, Transform→RectTransform conversion on first rectTransform read,
state defaults + fill clamp + clipper/raycaster round-trips. **1587 tests
green in BOTH configs (1249 + 338)**; 5 CLI modes exit 0; both client diags
byte-identical to the pre-change baseline (`trail 786` / `toys 12`).
bleeding-edge unmoved at `f2b8f5aa`.

### Arc B part 1 ✅ (2026-07-09) — the layout core (headless)

The full uGUI layout pipeline over the Arc-A geometry core, PUSH-BASED like
the original (groups WRITE child RectTransform state — pin anchors upper-left,
set sizeDelta/anchoredPosition — because layout is imperative in uGUI and the
menu's transcribed scenes will expect exactly those driven values):

**Engine files (`Engine.UI` unless noted):** `TextAnchor` (engine ns, 9-cell
grid), `RectOffset` (engine ns), `LayoutInterfaces` (ILayoutElement /
ILayoutController / ILayoutGroup / ILayoutSelfController / ILayoutIgnorer),
`LayoutUtility` (highest-priority-wins property resolution, negative = no
opinion, preferred clamped ≥ min, disabled Behaviours skipped),
`LayoutElement` (all sizes default −1, priority 1), `LayoutGroup` (abstract:
padding/childAlignment, rectChildren collection ILayoutIgnorer-filtered,
totals per axis, GetStartOffset alignment maths, SetChildAlongAxis both
overloads with the pivot-aware anchored back-solve),
`HorizontalOrVerticalLayoutGroup` (CalcAlongAxis min/preferred/flexible sums
with trailing-gap removal; SetChildrenAlongAxis: minMaxLerp shrink between min
and preferred, flexible-surplus distribution by weight, no-flexible run
alignment, cross-axis control/expand/align per childControl/childForceExpand)
+ `HorizontalLayoutGroup`/`VerticalLayoutGroup` concretes, `GridLayoutGroup`
(fixed-column/row + flexible cell counts, startCorner mirroring, row/column-
major fill, two-pass sizing — axis 0 pins cells, axis 1 positions once both
dims are known), `ContentSizeFitter` (ILayoutSelfController hugging min/
preferred via SetSizeWithCurrentAnchors), `LayoutRebuilder` (two-phase solve:
input bottom-up then control top-down, horizontal fully before vertical;
self-controllers before group controllers per node; MarkLayoutForRebuild
walks to the topmost active-group root, deduped queue;
`FlushQueuedRebuilds` snapshot-drains so mid-rebuild marks land next tick).
**GameLoop** grew the canvas-update slot: `LayoutRebuilder.FlushQueuedRebuilds()`
after LateUpdate, before end-of-frame.

**11 headless tests** (`EngineUiLayoutTests`, driven-rect assertions in a
top-left/y-down parent frame): vertical stack + width stretch, flexible
surplus by weight, min↔preferred shrink when space is short, run alignment
when nothing flexes, horizontal mirror, ILayoutIgnorer skip, grid row-major
fill with padding+spacing, grid UpperRight corner mirroring, ContentSizeFitter
hug, nested groups in ONE rebuild (outer hands cells, inner subdivides), and
the GameLoop canvas-slot flush (mark → tick → solved). **1575 tests green in
BOTH configs (1237 + 338)**; 5 CLI modes exit 0; both client diags
byte-identical (`trail 786` / `toys 12`). bleeding-edge unmoved at `f2b8f5aa`.

### Arc A ✅ (2026-07-09) — the engine UI geometry core (headless)

Engine growths (`CosmicShore.Engine`): **`Rect`** (full struct: min/max/center/
size, edge setters, MinMaxRect, Contains/Overlaps, normalized-point maps);
**`Transform` unsealed** with `localPosition`/`localScale` converted to virtual
properties (verified zero CS1612/ref/reflection breakers repo-wide;
`localRotation` stays a field — nothing drives it) + internal
`AdoptHierarchyFrom` (hierarchy-first, pose-last transplant so back-solves land
against the real parent rect); **`GameObject`** grew the component-type-list
constructor (`new GameObject(name, typeof(RectTransform))`) and Transform-
derived `AddComponent` now CONVERTS the transform in place (same sibling slot,
children in order, local pose preserved; adding plain `Transform` or an
already-converted type returns the existing one); **`RectTransform`** — the
anchor solve, PULL-BASED (rect + localPosition compute from live anchor state
+ parent chain on every read → always consistent, zero dirty-tracking/update
ordering, headless-deterministic): anchorMin/Max, pivot, anchoredPosition(3D),
sizeDelta, `rect`, offsetMin/Max (round-tripping views), GetLocalCorners/
GetWorldCorners (BL,TL,TR,BR), SetSizeWithCurrentAnchors,
SetInsetAndSizeFromParentEdge, localPosition setter back-solves
anchoredPosition (what makes reparenting + conversion keep world pose);
**`Canvas`** (+`RenderMode`) — rootCanvas walk, isRootCanvas, sortingOrder/
overrideSorting/worldCamera/pixelRect; a root screen-space canvas DRIVES its
RectTransform (rect = screen/scaleFactor, pose = screen centre, scale =
scaleFactor); **`CanvasScaler`** (`Engine.UI`) — ConstantPixelSize +
ScaleWithScreenSize (MatchWidthOrHeight in log space, Expand, Shrink).
Documented pull-based deviation: the original pushes scaleFactor during its
render pass; the port's Canvas pulls from the scaler on read — identical
steady-state, never stale.

**15 headless tests** (`EngineUiGeometryTests`): point/corner/stretch anchors,
offset round-trips through an asymmetric pivot, localPosition back-solve,
reparent-keeps-world-pose, nested stretch chain world corners, sizing helpers,
Transform→RectTransform conversion (hierarchy slot/children/pose/component
list), the ctor idiom, canvas-driven root rect at scale factors (world corners
stay pixels), full-stretch child covering the screen, IMMEDIATE screen-resize
propagation (the pull-based payoff), CanvasScaler ratio maths (1.6/1.2/
geometric mean/expand/shrink) + reference-resolution rect, nested-canvas
factor inheritance. **Bonus un-carry:** ToyFactory's label deviation
(`new GameObject("Label", typeof(RectTransform))`) — ToyFactory is now FULLY
live; zero RectTransform deviations remain repo-wide. **1564 tests green in
BOTH configs (1226 + 338)**; 5 CLI modes exit 0; both client diags
byte-identical (`trail 786` / `toys 12`). bleeding-edge unmoved at `f2b8f5aa`.

## MILESTONE: MENU-UI + MULTI-MODE PLAYABLE (prompter 2026-07-08)

Grounded by a full read-only scope of the port's render/UI/menu/mode/content
surface (2026-07-08). Key findings:

- **Client renderer** (`src/CosmicShore.Client`, Silk.NET + raw OpenGL 3.3):
  renders 3D gameplay (SkimRace + Freestyle lava-lamp) built **in code** via
  `new GameObject().AddComponent<>()` against the REAL ported systems. The only
  2D is a **procedural line HUD** (seven-segment digits drawn as `GL.Lines` in
  an ortho pass) — **no font atlas, no textured-quad/sprite path, no
  arbitrary-string rendering**. One vertex-color shader + a bloom chain.
- **Engine UI types are all data-only shims** (`Button`, `TMP_Text` family,
  `CanvasGroup`, `Sprite` stub) — no layout, no render. **Missing entirely:**
  `RectTransform`, `Canvas`, `CanvasScaler`, `Image`/`Graphic`, `LayoutGroup`,
  `EventSystem`/`PointerEventData`, `Selectable`, etc. `Transform` is pure 3D
  (no anchors/pivot/sizeDelta) — the hard blocker.
- **Menu UI:** `ScreenSwitcher` (855L, UGUI-heavy) + `IScreen` + 6 screens +
  ~227 `_Scripts/UI` files — **~10% ported, all the non-visual data/service/
  feed slice** (incl. the already-live headless `MainMenuController` state
  machine + `MainMenuCameraController`). `Menu_Main.unity` is a 3.7 MB /
  123k-line YAML scene: **1142 RectTransform, 988 CanvasRenderer, 1 Canvas** —
  layout+sprites+wiring live in the scene, and **nothing in the port reads it**.
- **Game modes:** all five modes' REAL controllers ARE ported and run headless
  via the CLI (`MultiplayerJoustController`, `MultiplayerCrystalCaptureController`,
  `AstroLeagueController`, `HexRaceController`, `TournamentController`). But the
  **windowed client instantiates none of them** — it renders two bespoke rigs
  (SkimRace `SkimRaceDirector`, Freestyle). The mode LOGIC is done + proven; the
  gap is a windowed host + per-mode rendering/HUD/input.
- **Content pipeline:** none. Scenes/prefabs/SO `.asset`s (556 assets, 381
  prefabs) are **hand-transcribed to C#** (e.g. `SkimRaceTheme.cs` transcribes
  the color-palette asset verbatim). No YAML→JSON extractor, no asset registry,
  no `Port/tools/`.

**Sequenced arcs** (foundation-first; headless where possible; each is
several loop iterations, each ends green+pushed):

| Arc | Scope | Phase | Track | Headless-testable |
|---|---|---|---|---|
| **A** ✅ | Engine UI geometry: `RectTransform` (anchors/pivot/sizeDelta/anchoredPosition/rect) + `Canvas` + `CanvasScaler` + rect-solve layout pass | 5 | UI framework | ✅ fully |
| **B** ✅ | UGUI component model: `Image`/`Graphic`, `LayoutGroup` (H/V/Grid), `LayoutElement`, `ContentSizeFitter`, `Mask`/`RectMask2D` — real layout-participating components | 5 | UI framework | ✅ fully |
| **C** ✅ | Client render primitives: embedded bitmap-font atlas + textured-quad shader + `DrawText`/`DrawRect` on the ortho pass; draw laid-out rects from A/B (sprite-PIXEL rendering rides with the Arc-E content pipeline — tinted rects until then) | 5 | render | screenshot byte-check |
| **D** ✅ | Input/event layer: `EventSystem` + `PointerEventData` + `Selectable`/`Button` hit-testing + gamepad nav-ring (Silk.NET hardware feed deferred to Arc H — same synthetic-injection seam) | 5 | UI framework | ✅ synthetic events |
| **E** | Content bridge: a Unity YAML→first-party-scene extractor (new `Port/tools/`) scoped to the UGUI subset (RectTransform/Canvas/Image/TMP/Button/LayoutGroups/CanvasGroup) + script-GUID→type map, so `Menu_Main` imports rather than being hand-transcribed. **Decision point when it opens** — confirm extractor-vs-hand-authoring scope with the prompter (large sub-project). | 7 | content | round-trip parity |
| **F** ✅(spine) | Port `ScreenSwitcher` + `IScreen` + the 6 screens + `ModalWindowManager`, wired to the live `MainMenuController` + `PlayerDataService`. **Milestone sub-goal: menu renders + navigates + is testable.** ARCADE SPINE COMPLETE (2b-iii(b)): nav → arcade modal → GameCards → configure modal → `OnLaunchGame`. Hangar/Leaderboards/Store screen units queue behind Arc G→I. | 5 | UI framework | ✅ + screenshot |
| **G** ✅(part 1) | Windowed game-mode host: the CLI `*Round.cs` world constructors split into steppable handles (`HexRaceRoundHandle`), driven per-frame by `ModeHostWindow` (`--mode play`) with a wireframe render + HUD — a REAL round stands up, steps, scores, and renders in a window (transcript-pinned to the CLI). Part 2 = menu→game handoff on `OnLaunchGame` + a second mode handle. | 3/5 | mode host | ✅ construction |
| **H** | Per-mode rendering + HUD: extend the renderer beyond stars/rails/crystals/vessels to each mode's objects (joust field, capture crystals, AstroLeague ball/goal/boundary, hex course) + each mode's HUD; route real human input | 5 | render | screenshot |
| **I** | Menu→game→menu loop: connect `MainMenuController`'s LaunchGame SOAP path to the windowed mode host and back, satisfying the controllers' ready-button/scoreboard UI seams with the real UI. **FINAL MILESTONE.** | 8 | integration | ✅ E2E |

**Critical path:** A→B→D (the UI framework from scratch — the long pole) and
G→H (windowed mode host — largely integration since mode logic is done) can run
as two parallel tracks after Arc A, converging at Arc I. C is the first visible
output; F is the first testable menu; I is the milestone. **No shortcuts:** the
content bridge (E) is a real extractor, not hand-transcription of 123k scene
lines; the windowed host (G) instantiates the REAL controllers, not new bespoke
rigs. The existing SkimRace/Freestyle clients + 5 headless CLI modes remain the
always-runnable progress build throughout.

### #14 consumers un-carried ✅ (2026-07-08) — the services deviation backlog is drained

`PlayerDataService` (12 markers) + `GameSetting` (7 markers) restored verbatim
against the live `UGSDataService`, so both now persist through the real
CloudData repositories. PlayerDataService: the `_ugsDataService` [Inject]
field, the Start ready-event fork, the OnDestroy unsubscribe, HandleDataServiceReady's
unsubscribe, MergeCloudProfile's real `ds.ProfileRepo.Data` read (null seed
deleted), SyncCurrentProfileToRepo's repo write + MarkDirty, and
SaveProfileImmediateAsync's `async void` repo.SaveAsync (the AnalyticsServiceFacade
`_analytics` inject stays carried — separate arc). GameSetting: the
`_ugsDataService` field, Awake's cloud-settings fork, OnDestroy unsubscribe,
HandleCloudDataReady, and SyncToCloud's PlayerSettingsRepository write.
**6 behavior tests** (`PlayerDataCloudTests`): returning-player cloud merge over
local defaults (+ gameData mirror), new-player push-defaults-to-repo,
SetDisplayName write-through, no-service local fallback, GameSetting cloud-apply
over PlayerPrefs, and GameSetting change→SyncToCloud. Also **updated the
pre-existing C2 staged-path test** to drive the now-live path and **fixed a race
in my own CloudData offline-recovery test** (poll the persisted value, not the
`!IsDirty` flag the debounce loop clears before the cross-thread write lands).
**1549 tests green in BOTH configs (1211 + 338)**; 5 CLI modes exit 0; both
client diags byte-identical. bleeding-edge unmoved at `97d4ab29`.

> **Known intermittent (pre-existing, NOT this branch's regression):** the full
> Release suite flakes ~1-in-10 runs under CPU load, alternating between
> `SpawnerAdapterC6Tests.MiniGameAdapter_SpawnAIAtStart_PreSpawnsDefaults_InitializeGameDedupes`
> (human player resolved "HumanJade"-style instead of the profile name — 2 players,
> no "CloudName") and `HeadlessRoundTests.Round_SameSeed_ProducesIdenticalTranscript`.
> Attribution verified 2026-07-08: the HeadlessRoundTests flake reproduces 1/10 on
> the prior shipped commit `8053b22f` with an identical binary; class-alone stress
> (15×) never fails. Same-binary nondeterminism → suspect process-level effects
> (randomized string hashing → dictionary iteration order, or real-time leakage
> into the sim under contention). Worth a dedicated diagnosis iteration; both
> tests are pre-existing arcs (spawner C6 / headless round), untouched since.

### CloudData core ✅ (2026-07-09) — the full CloudSave family + UGSDataService live

Engine growth: **`CloudSaveSdk.cs`** (`Engine/Services`) — the UGS Cloud Save
placeholder per the MultiplayerSdk precedent: `CloudSaveService` (settable
`Instance` + `Reset()`), `ICloudSaveService.Data.Player` →
`IPlayerDataApi.LoadAsync(HashSet<string>) / SaveAsync(Dictionary<string,object>)`,
`Item` + `IDeserializable.GetAs<T>()`, and the shared `CloudSaveJson.Options`
(`IncludeFields = true` — the models are Unity-style field-based classes,
which System.Text.Json ignores by default; this is load-bearing). The default
**`LocalCloudSaveService`** serializes on save and deserializes on load — a
REAL JSON round-trip (dictionary fields survive, reference identity does
not), empty on fresh process.

**30 files ported** (all verbatim; substitutions noted in headers):
`UGSKeys` (all cloud keys + analytics event names), the 3 CloudData
interfaces (`ICloudSaveProvider`, `ICloudDataReader/Writer/Repository`,
`IUGSDataService`), 8 CloudData models (Hangar/Loadout/Squad/Episode/
DailyChallenge/Training/CaptainProgress + the already-ported PlayerSettings),
`PlayerStatsProfile` + the 4 per-mode stats profiles, `VesselStatsCloudData`,
`GameModeProgressionData`, `CloudDataRepository` (debounced-save base; the
raw `Task.Delay` debounce is upstream's own — real-time, kept verbatim) + all
12 domain repositories (incl. CaptainProgress, unreferenced by UGSDataService
but part of the family — lose nothing), `UGSCloudSaveProvider` (availability
gate, legacy-string load fallback via System.Text.Json-for-Newtonsoft,
retry-with-backoff + once-per-episode `OnSaveFailed`; ONE carried deviation:
the `ToastNotificationAPI.Show` failure toast, UI shell), `SO_VesselList`,
and **`UGSDataService` (245L, replaces the Deviation-#14 shell)** — the
auth-driven init over all 11 repositories, hangar→SO_Vessel unlock sync,
flush-dirty-only, and reset-all run FULLY live.

**12 behavior tests** (`CloudDataTests` + `UgsDataServiceTests`): the local
store's real round-trip (dictionaries survive, missing keys absent), the
provider's availability gate / save-load round-trip / legacy-string fallback
/ cancelled-backoff arm (no failure episode), the repository lifecycle
(empty-cloud default + OnDataChanged, debounced dirty→clean flush,
keep-dirty-offline → flush-on-reconnect, reset-persists), and UGSDataService
end to end (signed-in-at-start init + hangar unlock sync onto the SO asset,
sign-in-later via the SOAP event, flush-dirty-only + reset-all through the
cloud). **1543 tests green in BOTH configs (1205 + 338)**; all 5 CLI modes
exit 0; both client diags byte-identical. bleeding-edge unmoved at
`97d4ab29`.

### Party-ring session surface ✅ (2026-07-09) — PartySessionService + PresenceLobbyService fully live

All 14 carried services-phase regions across the party ring RESTORED against
the engine `MultiplayerSdk` placeholder — both files now run **verbatim, zero
deviations** (normalized-diff-verified against upstream; only the sanctioned
`UniTask.Delay → GameTask.Delay` mapping differs). **`PartySessionService`**
(6): the use-time `_multiplayerService` resolver (never ctor-cached, Q10),
CreateAsync's private+Relay SessionOptions + CreateSessionAsync (host-conflict
/ 429 / transient retry ladder now exercises real calls), JoinByIdAsync's
JoinSessionOptions + JoinSessionByIdAsync, and IsRateLimitException's real 429
arm (`CosmicShore.Engine.Services.RequestFailedException.ErrorCode == 429` —
the engine type from the error/kick arc replaces the carried `false` stub).
**`PresenceLobbyService`** (8): the using line, the CS1998 pragma REMOVED
(every method now awaits), the `_multiplayerService` resolver,
TryQueryAndJoinAsync's full query → ordinal-sort → join-first body,
ConvergeToCanonicalAsync's query + smallest-id fold + migrate-then-release,
and CreateAsync's PRESENCE_LOBBY-tagged public SessionOptions + retry loop.
Engine growth: `SessionException` gained the SDK-shaped inner-exception ctor
(the transient classifier matches `InnerException is NullReferenceException`).

**10 behavior tests** (`PartySessionFlowTests` + `PresenceLobbyFlowTests` in
PartyServicesRing3Tests.cs, fake `IMultiplayerService` via the settable
Instance per the MultiplayerSessionTests precedent): create stamps
time/wires-unwires the PlayerLeaving relay/sends private+Relay options with
all 8 identity keys, create no-ops on an active session, host-conflict retry
(no delay), 429 back-off through the restored RequestFailedException arm,
transient-NRE join retry, join-or-create creates own PUBLIC
PRESENCE_LOBBY-tagged lobby (initial + converge re-query = 2 queries),
smallest-id-first join, converge migrates to the canonical id and releases
the race-lost lobby AFTER the join lands, converge keeps ours when already
canonical, and the query-throw create fallback. `NetworkTransitionService`
(12 markers) intentionally untouched — transport phase. **1531 tests green
in BOTH configs (1193 + 338)**; all 5 CLI modes exit 0; both client diags
byte-identical. bleeding-edge unmoved at `97d4ab29`.

### UGS Multiplayer session surface ✅ (2026-07-08) — MultiplayerSetup fully live

Engine growths: **`MultiplayerSdk.cs`** (`Engine/Networking`) — the full UGS
Multiplayer placeholder per the Friends-SDK precedent: `PropertyIndex` /
`FilterField` / `FilterOperation` enums, `SessionProperty`, `FilterOption`,
`QuerySessionsOptions`/`QuerySessionsResults`, `ISessionInfo`,
`SessionOptions` + `WithRelayNetwork()` (records `UseRelay` for the
transport phase), `JoinSessionOptions`, `IMultiplayerService`
(Create/JoinById/Query), and static `MultiplayerService` with a settable
`Instance` + `Reset()`. The default is **`LocalMultiplayerService`** —
honest single-process semantics (the NetworkSceneManager precedent):
creation returns deterministic in-process `IHostSession`s
(`local-session-N`, no clock/RNG), discovery sees no remote sessions, and
join-by-id throws `SessionException(SessionNotFound)` — so the matchmaking
flow converges on host-a-fresh-session with real observable behavior.
Plus `AuthenticationService.GetPlayerNameAsync()` (virtual, returns the
stored PlayerName — the SDK's observable contract).

**All 8 carried regions in `MultiplayerSetup` RESTORED** — the file is now
FULLY live: ExecuteMultiplayerSetup's query → filter (`IsJoinableSessionInfo`:
room + not locked + not passworded) → `TryJoinFirstAvailable` (race-filled
sessions skipped, rate-limit pause) → `StartSessionAsHost` (property maps +
`.WithRelayNetwork()` + exponential rate-limit backoff `2000·2^attempt`,
max 3 retries) tail, `JoinSessionAsClientById`, `QuerySessions` (String1 =
gameMode, String2 = maxPlayers filters + retry loop),
`GetPlayerProperties` (playerName → Member visibility via the grown
`GetPlayerNameAsync`), `GetSessionProperties` (gameMode → String1,
maxPlayers → String2, Public), and the `using Unity.Services.Multiplayer`
line (resolves through `CosmicShore.Engine.Networking`). The CS1998 pragma
stays for OnAuthenticationSignedInAsync's verbatim awaitless dispatcher
shape. **7 behavior tests** (`MultiplayerSessionTests`): the no-remote-
sessions end-to-end through LocalMultiplayerService (local host shut down
for the local→Relay transition + `local-session-1` hosted + OnSessionStarted
raised once), the existing-party-session fast path (no shutdown/query/
create), the captured SessionOptions property-map assertions, the join-race
fall-through (oldest first, filled candidate skipped, no create), the
unjoinable filter (locked/full/passworded → host instead), the rate-limit
backoff (throw "Too Many Requests" once → retry succeeds, 2 create calls),
and the LocalMultiplayerService semantics unit (deterministic ids, carried
MaxPlayers, SessionNotFound join, empty query, Reset() restores the id
counter). **1521 tests green in BOTH configs (1183 + 338)**; all 5 CLI
modes exit 0; both client diags byte-identical. bleeding-edge unmoved at
`97d4ab29`.

### Bootstrap-arc remainder ✅ (2026-07-08) — the bootstrap arc is COMPLETE

Engine growths: the `TMP_InputField` data-only shim and the auth shim's
`UpdatePlayerNameAsync` (stores locally, returns the name — the SDK's
observable contract). **Both files ported FULLY LIVE, zero deviations:**
**`SplashToAuthFlow`** (126L — the splash hold, the in-flight-auth settle
wait with timeout, and the always-route-through-Authentication load; even a
signed-in user routes through the auth scene so the network host starts
before Menu_Main loads via Netcode) and **`AuthenticationSceneController`**
(549L — the safety-timeout race per the LeavePartyAsync WhenAny precedent,
the already-signed-in auto-skip, cached sign-in with timeout, the
auth-panel/auto-sign-in fork, guest login, the post-auth PlayerDataService
wait + username-needed check, username confirm through the grown shim, the
BootStatusPanel SOAP surface, and the networked Menu_Main load —
WaitForRelayReadyAsync's dual-condition gate, the 3-attempt
EnsurePartySessionAsync re-kick, the manual-retry surface, the splash
re-cover, and the Netcode load through the NetworkManager.SceneManager
placeholder). Even the UI panel wiring is live against the engine's
data-only Button/TMP shims — no UI-shell deviations needed.

**The bootstrap arc is COMPLETE**: Bootstrap (AppManager DI root + platform
config + services) → splash → Authentication (auto-skip / guest login /
username setup + the relay gate) → networked Menu_Main load → SceneLoader's
splash release on OnClientReady — every file on the app-entry path is fully
live. **6 behavior tests** (`AuthFlowTests`): the splash hold-then-route,
the auto-skip through the networked menu load (+ splash kept opaque), the
no-panel anonymous auto-sign-in, the panel fork + guest-login click-through,
the username fork (short-name reject, confirm → local + shim persist), and
the relay gate ignoring the lobby-join fire until Netcode listens. Rig
lesson: stage authored-inactive panels inactive at creation, or activeSelf
polls exit before the flow runs. **1514 tests green in BOTH configs
(1176 + 338)**; all 5 CLI modes exit 0; both client diags byte-identical.
bleeding-edge unmoved.

### AppManager ✅ (2026-07-08) — the DI root is live end to end

**`NetworkMonitor` (102L) ported FULLY LIVE** (reachability poll against the
engine's settable `Application.internetReachability` mirror, driving the
NetworkMonitorData online flag + lost/found SOAP raises), plus the
`DontDestroyOnLoad` marker component (the persistence stamp
`EnsurePersistent` checks), and engine growths: `SleepTimeout` constants
(+ `Screen.sleepTimeout` → int), `QualitySettings.vSyncCount`,
`Object.FindObjectOfType<T>` (pre-2023 alias so the `#else` forks compile),
`Scene.buildIndex`, the AnalyticsServiceFacade shell's 9-arg bootstrap ctor,
and a `PrismFactory` shell (the one AppManager binding that was neither
ported nor shelled).

**`AppManager` (618L) ported FULLY LIVE, zero deviations** — the whole DI
root: platform config from BootstrapConfigSO (framerate / vsync / sleep),
the bootstrapped-once guard, TryResolveManagersEarly + the persistence
stamp, InstallBindings (11 RegisterAsset values, 15 RegisterManagerSingleton
lazy factories with deferred scene search, the service quartet + analytics +
tournament + the 9 party-ring factories), ConfigureGameData menu defaults,
StartAuthentication / StartNetworkMonitor (incl. NetworkDiagnostics init),
the RunBootstrapAsync splash-hold → OnBootstrapComplete → Authenticating →
Authentication-scene handoff through SceneTransitionManager, Shutdown, and
the two RuntimeInitializeOnLoadMethod statics (data-only marker — harnesses
call them directly).

**4 behavior tests** (`AppManagerTests`): InstallBindings registering the
full surface (assets + managers + services + the party ring, singleton
identity), the bootstrap flow end to end (platform config, menu defaults,
auth signed-in exactly once, monitor online, Bootstrapping →
splash hold → HasBootstrapped + OnBootstrapComplete → Authenticating + the
Authentication scene actually loaded through STM), the NetworkMonitor
lost/found flips, and the bootstrapped-once duplicate guard. Two rig
lessons recorded: **Awake runs at AddComponent time on an active GO** (stage
composition roots inactive → configure → inject → activate), and GameDataSO
rigs that hit `ResetAllData` need `VesselClassSelectedIndex` wired (the
guarded lifecycle invoke swallows the NRE and silently aborts Start).
**1508 tests green in BOTH configs (1170 + 338)**; all 5 CLI modes exit 0;
both client diags byte-identical. bleeding-edge unmoved.

### AppManager groundwork ✅ (2026-07-08) — the Reflex builder surface + manager shells

The engine Injection layer grew the **Reflex builder surface**:
`Lifetime`/`Resolution` enums (placeholder-local values) and
**`ContainerBuilder`** — deferred registrations applied to a fresh
`Container` at `Build(parent?)`, so factories see every sibling binding
regardless of registration order (the contract AppManager's party-ring
factories rely on); `Resolution.Eager` resolves at Build, Lazy on first
inject; non-Singleton lifetimes fail loud (`NotSupportedException`) rather
than silently caching — the codebase registers only singletons.
**`IInstaller` migrated** from the interim `InstallBindings(Container)`
shape to the upstream `InstallBindings(ContainerBuilder)` signature (one
test implementor updated — the installer now installs into a builder and
the host Builds the scope).

**Six manager shells added** per the AudioSystem/CameraManager precedent, on
the upstream namespaces/paths/base classes: `UGSStatsManager` (UI/, UGS
Leaderboards — services phase), `CaptainManager`
(System/Playfab/Economy/, `SingletonPersistent<T>` — meta-economy phase),
`IAPManager` (System/, UGS Purchasing — services phase),
`PostProcessingManager` (Controller/Managers/, `Singleton<T>` — rendering
phase), `StatsManager` (Controller/Managers/ — scoring arc's stats pass),
and `UGSDataService` (System/CloudData/ — services phase; restoring it later
un-carries the PlayerDataService/GameSetting deviation-#14 inject sites).

**6 new tests** (in `InjectionTests`): order-independent sibling resolution
through Build, lazy-singleton once-only construction, eager-at-Build,
non-singleton fail-loud, parent-chained Build scopes, and the six-shell
family registrable + resolvable through the builder. **1504 tests green in
BOTH configs (1166 + 338)**; all 5 CLI modes exit 0; both client diags
byte-identical. bleeding-edge unmoved.

### Bootstrap arc — service layer ✅ (2026-07-08)

Engine growths: `RuntimeInitializeOnLoadMethod` attribute +
`RuntimeInitializeLoadType` (data-only marker — no domain reload exists, so
harnesses call tagged resets directly), `SceneManager.sceneUnloaded` +
`NotifySceneUnloaded`, `UnityServices.InitializeAsync()` (flips State →
Initialized, the observable contract InitializeCore depends on), and the
**auth shim grown to the full facade surface**: `SessionTokenExists`,
virtual `SignInAnonymouslyAsync`/`SignOut`/`ClearSessionToken` (local
sign-in mirrors the real SDK's observable sequence — identity set, token
cached, SignedIn raised; subclass-and-swap via the settable Instance is the
failure-test seam), and the four auth notifications.

**Four files ported FULLY LIVE, zero deviations:** `BootstrapConfigSO`
(38L), `ApplicationLifecycleEventsContainerSO` (SOAP container), the **full
`ApplicationLifecycleManager`** (114L — replaces the statics shell on the
upstream class shape: dual static+SOAP raise pipeline, sceneLoaded/
sceneUnloaded bridge, IsQuitting latch, domain-reload static reset; the
OnApplication* messages are host-raised when the app shell lands, tests
drive them directly), and **`AuthenticationServiceFacade`** (318L — startup
guard + coalesced init, anonymous sign-in with the double-raise dedup, the
cached-session three-branch flow, sign-out + token clear, auth event wiring,
single-writer AuthenticationData state + SOAP raises, provider stubs; the
`#if UNITY_EDITOR` MPPM block compiles out verbatim).

**6 behavior tests** (`BootstrapArcTests`): the full anonymous sign-in flow
(state ladder, identity, exactly-one OnSignedIn despite the event+await
double path, the startup guard), the cached-sign-in three branches, sign-out
+ token clear, the failure path through a throwing shim subclass + the
ResetStartupState retry, the lifecycle manager's dual pipelines + scene
bridge + IsQuitting latch, and the BootstrapConfigSO defaults. **1498 tests
green in BOTH configs (1160 + 338)**; all 5 CLI modes exit 0; both client
diags byte-identical. bleeding-edge unmoved.

### ApplicationStateMachine ✅ (2026-07-08) — SceneLoader FULLY LIVE

The `ApplicationLifecycleManager` statics shell grew the **`OnAppPaused` /
`OnAppQuitting`** static C# events (+ `NotifyPaused`, and `NotifyQuitting`
now raises) — the full MonoBehaviour lifecycle manager still ports with the
bootstrap arc. **`ApplicationStateMachine` (229L) ported verbatim, FULLY
LIVE, zero deviations**: the table-driven transition graph, the special
states (ShuttingDown always allowed; Paused from any non-terminal state with
restore-to-previous; Disconnected from any active state), the SOAP
auto-wiring (OnSessionStarted → InGame, OnMiniGameEnd → GameOver, the
lifecycle statics, OnNetworkLost → Disconnected), and the PreviousState +
OnStateChanged publication. **Un-carried:** SceneLoader's last deviation
(the `[Inject]` + both `TransitionTo` calls) — **SceneLoader now carries
ZERO deviations.** Upstream's own **26-test EditMode suite ported verbatim**
into CosmicShore.Tests.Ported (valid/invalid graph walks, same-state no-op,
ShuttingDown/Paused/Disconnected specials, HandleAppPaused round trip,
PreviousState tracking). **1492 tests green in BOTH configs (1154 + 338)**;
all 5 CLI modes exit 0; both client diags byte-identical. bleeding-edge
unmoved.

### UI-shell arc ✅ (2026-07-08) — PartyInviteController FULLY LIVE

Engine growths: a synchronous `SceneManager.LoadScene` (the defensive
fallback loaders take — same minimal re-designate + announce semantic) and a
`NetworkManager.SceneManager` placeholder (`NetworkSceneManager.LoadScene` →
the engine async load, fire-and-forget per Netcode's void contract — in the
single-process port a "network" scene load IS a local load; client
replication arrives with the transport phase).

**Three toast files ported fully live** (`ToastAnimation` frozen 0-2,
`ChatToastRequest`, `ToastChannel` — the SO event channel; the view layer
ToastService/ToastItemView ports later). **`SceneTransitionManager` (389L)
ported** — the whole fade/transition core is LIVE on the engine CanvasGroup:
LoadSceneAsync (fade-out → load → settle → OnSceneLoadComplete → fade-in,
incl. the sync-fallback catch), LoadNetworkSceneAsync (server/client/no-NM
branches), manual fades, SetFadeImmediate, the unscaled-time lerp, and
CreateFadeOverlay's GameObject+CanvasGroup construction; deviations: the
UnityEngine.UI dressing (Canvas/CanvasScaler/GraphicRaycaster/Image/
RectTransform) + the retired-boundary MainThreadDispatcher canary.
**`SceneLoader` (332L) ported** — the full launch/return/session-end flow is
LIVE (SOAP subscriptions, splash cover + FadeFromSplashOnReady arm, the MPPM
client-defer guards, Tournament splash-dwell read, server Netcode load
through the placeholder, ClearPlayerVesselReferences AI-despawn ordering,
quit cleanup); one deviation: the ApplicationStateMachine inject + 2
TransitionTo calls (bootstrap arc — NEXT UP 1).

**Un-carried:** PartyInviteController's three UI-shell deviations — the
bounce toast, both SetFadeImmediate screen covers, and the
ArmSplashFadeOnNextClientReady splash re-arm. **PartyInviteController now
carries ZERO deviations — fully live.**

**10 new tests + the enum freeze**: toast payloads (prefix/postfix/countdown
+ onDone), the programmatic overlay + immediate fades, the STM local load
flow (fade-load-announce-fade), the STM network load through the Netcode
placeholder, the SceneLoader launch flow (splash cover → load → fade on
OnClientReady), the client-defer guard (cover applies, no local load),
return-to-menu + splash re-arm, and the PIC bounce test now also asserting
the covered screen + the B10-ordered bounce toast. **1466 tests green in
BOTH configs (1154 + 312)**; all 5 CLI modes exit 0; both client diags
byte-identical. bleeding-edge unmoved.

### UGS-core error/kick surface ✅ (2026-07-08) — HostConnectionService FULLY LIVE

The engine grew **`RequestFailedException`** (with `ErrorCode`, in
`CosmicShore.Engine.Services` next to the UnityServices shim — real
construction sites arrive with the services phase) and
**`IHostSession.RemovePlayerAsync`** (host-privilege kick; all six test
fakes updated). Un-carried with them, the last carried code in the party
orchestrator: `KickPartyMemberAsync`'s UGS-side kick
(`ActiveSession.AsHost().RemovePlayerAsync`, CS1998 pragma retired) and the
HTTP-404 arm in `IsDefiniteSessionGoneException` — plus
`NetworkDiagnostics.ClassifyException`'s typed request-layer arm
(429→RateLimit, 404→SessionGone, 5xx→Transient, -1/0→Offline,
default→Transient). **The 2,078-line `HostConnectionService` now carries
ZERO deviations — fully live**; `NetworkDiagnostics` likewise (its
Unity-namespace string branches stay verbatim-dead until the real SDK).
**12 new tests**: the kick flow through the rig (non-host guard, host path
= local roster removal + OnPartyMemberKicked + recorded UGS kick, self-kick
guard), the 7-case ClassifyException error-code map + the one-layer
AggregateException unwrap, and the session-gone classifier's 404 read +
inner-chain walk. **1456 tests green in BOTH configs (1144 + 312)**; all 5
CLI modes exit 0; both client diags byte-identical. bleeding-edge unmoved.

### Scene-management surface ✅ (2026-07-08) — 3 scene-phase deviations un-carried

The engine `SceneManager` grew **`GetActiveScene()`** (one GameLoop owns one
Scene, so the current loop's scene IS the active scene; a no-loop call
returns an unnamed placeholder mirroring the original's invalid-scene
`.name == ""` read) and a **minimal `LoadSceneAsync(name, mode)`** — the
port has no scene assets to instantiate yet, so it preserves the two
observables ported code depends on: completes asynchronously through the
loop's continuation pump (`GameTask.Yield`), re-designates the loop-owned
scene's name, and raises `sceneLoaded` once with the ACTIVE scene instance
(what HostConnectionService.OnSceneLoaded keys on). Content teardown +
instantiation arrive with the full loader in the content phase.

**Un-carried (3):** `HostConnectionService.IsOnMenuScene` now reads
`SceneManager.GetActiveScene().name` verbatim (was the GameLoop scene-name
stand-in), and both of `PartyInviteController`'s Menu_Main loads (leave +
recovery) are real awaited `LoadSceneAsync` calls again (`.ToUniTask(ct)` →
`Task.WaitAsync(ct)`), replacing the announce-only `NotifySceneLoaded`
stand-ins. HCS deviations now: **1 services-phase pair only** (RemovePlayerAsync
kick + RequestFailedException 404 arm — NEXT UP 1); PIC deviations now:
UI-shell arc only.

**2 engine tests** (`SceneManagementTests`): GetActiveScene resolves the
loop-owned scene; LoadSceneAsync settles through the pump, re-designates the
active scene, and announces exactly once with the active instance + mode.
The PIC leave-flow test now also asserts the active scene reads "Menu_Main"
after a real load. **1444 tests green in BOTH configs (1132 + 312)**; all 5
CLI modes exit 0; both client diags byte-identical. bleeding-edge unmoved.

### MainMenuCameraController (camera arc) ✅ (2026-07-08)

Groundwork: engine `Transform.Find(string)` (direct children + '/'-paths);
`RotateAroundOrigin` ported verbatim (15L, fully live); the CameraManager
shell grew the activation quartet MainMenuCameraController drives —
`SetMainMenuCameraActive` / `SetupGamePlayCameras` / `SetupEndCameraFollow` /
`DeactivateAllCameras` — as no-ops into an observable `ShellCameraState`
mirror (+ `LastGameplayFollowTarget`).

**`MainMenuCameraController` (1028L) ported.** LIVE: the `MenuCameraMode`
enum (frozen 0-3 in EnumFreezeTests) + all tuning fields, `Mode` +
`ApplyModeChange`, **`ActiveTransitionDuration`** (the read
`MenuCrystalClickHandler` un-carries below), `IsVesselMode`, the full SOAP
wiring (OnClientReady → immediate menu-camera activation,
OnGameStateTransitionStart/OnMenuStateTransitionStart → the two transitions,
OnCrystalSpawned → orbit-rig placement), the transform-side crystal-orbit rig
(follow-target parked at `crystal + back·radius`, world-origin
RotateAroundOrigin disabled/re-enabled, per-frame unscaled-time orbit in
`UpdateMenuOrbit`), the randomized mode-switch loop (`GameTask.Delay`
unscaled), and the CTS lifecycle (BeginTransition preemption + OnDestroy
teardown). Both transitions resolve through **upstream's own no-bridge /
no-player-camera fallback branches** (FallbackActivateGameplayCamera /
ActivateMenuCameraImmediate → the shell mirror) — the same "live on
upstream's own null branch" precedent as LeavePartyAsync pre-un-carry.
Deviations: one family, **camera arc — Unity.Cinemachine** (vCam
caching/creation/config, brain-blend override + FOV punch, blend polling,
priority juggling, the BindingMode field), all carried as commented source;
the two spots where a vCam-null guard would kill live transform work
(SetMenuVCamTarget entry, ActivateMenuCameraImmediate's CrystalOrbit branch)
carry the guard itself as the deviation.

**Un-carried:** `MenuCrystalClickHandler`'s last deviation pair — the
`cameraController` SerializeField + the `CurrentTransitionDuration` per-mode
read (controller wins, serialized fallback otherwise).

**6 behavior tests** (`MainMenuCameraControllerTests`) + the enum freeze:
per-mode durations (2s orbit / 0.5s all vessel modes) with the Mode setter
re-activating the menu family; the un-carried duration read (fallback vs
controller); OnClientReady activation; crystal-spawn orbit-rig placement +
orbit math (radius preserved, yaw-only) + rotator arbitration; the freestyle
enter/exit round trip through the shell mirror (Gameplay handoff with the
vessel's follow target, then back to MainMenu); and the no-vessel no-op.
**1442 tests green in BOTH configs (1130 + 312)**; all 5 CLI modes exit 0;
both client diags byte-identical. bleeding-edge unmoved.

### Friends system ✅ (2026-07-08) — party-system arc file inventory COMPLETE

The engine grew a **Friends SDK placeholder surface** per the ISession
precedent (`CosmicShore.Engine.Services.Friends`, one flat namespace mapped
from `Unity.Services.Friends[.Exceptions|.Models|.Notifications]` — README
table updated): Availability/RelationshipType/MemberRole enums,
Profile/Presence(+GetActivity&lt;T&gt;)/Member/Relationship shapes,
FriendsServiceException, the three notification event interfaces,
IFriendsService (full read/write/presence/refresh/notification surface), and
a static `FriendsService.Instance` settable test hook (the
NetworkManager.Singleton pattern). Against it, THREE files ported FULLY LIVE
with **zero deviations**: `FriendsDataSO` (SOAP container: 4 reactive lists +
4 events + ready event + computed counts + ResetRuntimeData),
`FriendsServiceFacade` (534L single-writer: init + event wiring, the whole
relationship API, presence, refresh, IsFriend/IsBlocked, SDK→SOAP sync incl.
StripUgsDiscriminator + AvailabilityToInt; `FriendsService.Instance` null
until the real SDK binds — InitializeAsync's own catch degrades that to a
warning, upstream's exact failure path), and `FriendsInitializer` (236L
bridge: sign-in bootstrap, party-presence subscriptions, all presence
helpers, sign-out reset). `Controller/Party/` is now **fully ported** — the
party-system arc's file inventory is complete. **8 behavior tests**
(`FriendsSystemTests`) drive initializer+facade through a fake IFriendsService:
sign-in bootstrap → init + "In Menu" presence; SDK→SOAP sync with
discriminator strip / availability mapping / activity read-back / no-profile
fallback; notification routing (Friend→OnFriendAdded,
Source-FriendRequest→OnFriendRequestReceived, Target-request→nothing,
delete→OnFriendRemoved); party presence following member join / last-remote
leave; accept-request resync via an SDK side-effect hook; the pre-init
mutation guard; sign-out reset + notification unwire; OnDestroy→Offline.
**1432 tests green in BOTH configs (1120 + 312)**; all 5 CLI modes exit 0;
both client diags byte-identical. bleeding-edge unmoved.

### PartyInviteController + orchestrator un-carry ✅ (2026-07-08)

**`PartyInviteController` (506L → 562 with header/markers) ported** — the
party-system arc's last transition orchestrator. FULLY LIVE: singleton
lifecycle (Awake guard / OnDestroy CTS teardown), `IsTransitioning` + the
test-reflected `_transitioning` field (name preserved), the five-step
accept-invite orchestration (NM shutdown → ClearStaleReferences →
HCS.AcceptInviteAsync → honored connect-wait bounce →
WaitForClientReadyAsync terminal watchdog → OnPartyJoinCompleted +
ForceRefreshNow in an isolated try/catch), DeclineInviteAsync, the
leave-lobby cold-boot sequence (DestroyPlayerAndVessel + ResetRuntimeData →
LeavePartySessionAsync → ShutdownAsync → Menu_Main → EnsurePartySessionAsync),
no-op TransitionToPartyHostAsync, idempotent HandleHostLossAsync (+
fire-and-forget ClearJoinedPartyAsync), BounceToSoloMenuAsync,
RecoverFromFailedTransitionAsync, all timeout/linked-CTS handling, and every
NetDiag line. Deviations: UI-shell/scene arc (ToastChannel bounce toast,
SceneTransitionManager fade cover, SceneLoader splash re-arm — types absent,
carried as commented source) and scene phase (2× SceneManager.LoadSceneAsync
— continuation kept LIVE, announced via `NotifySceneLoaded(Menu_Main)` so
HCS.OnSceneLoaded's invite-state reset + presence republish observe the load
as upstream's real load would).

**Orchestrator un-carry:** with the controller in the build,
`HostConnectionService`'s 4 party-system deviations are RESTORED — the full
LeavePartyAsync body (invite resolve → member ClearWithEvents → bounded
ClearJoinedPartyAsync via Task.WhenAny over an eagerly-bridged GameTask.Delay
arm → controller.LeavePartyAndReturnToMenuAsync) and the 3 IsTransitioning
guards (RefreshAsync entry + catch, RefreshPartyMembersAsync catch). HCS
deviations now: 2 services-phase + 1 scene-phase only. **`MultiplayerSetup`'s
3 same-condition deviations un-carried in the same pass**: the
`ReconcilePartyMembersNow` hard-drop backstop (restore condition met when HCS
ported last iteration) and both `HandleHostLossAsync` self-rescue routes
(OnClientDisconnect host-loss `.Forget()` branch + the awaited
OnTransportFailure branch with legacy-teardown fallback).

**9 behavior tests** (`PartyInviteControllerTests`) drive the real HCS+PIC
pairing through fake lobby/session seams + a counting transition fake:
singleton take/release, the duplicate-accept guard, the accept happy path
(join completes, host seeded into PartyMembers, IsPartyHost false, no
Menu_Main reload), the connect-failure bounce (Menu_Main announced, solo
session restored, 3 shutdowns), HandleHostLoss idempotence + single recovery,
the leave cold-boot sequence, the un-carried HCS.LeavePartyAsync routing
through the controller (and the null-controller first-exit), and the
un-carried RefreshAsync entry guard skipping the tick while transitioning.
**1424 tests green in BOTH configs (1112 + 312)**; all 5 CLI modes exit 0;
both client diags byte-identical. bleeding-edge unmoved.

### HostConnectionService behavior tests ✅ (2026-07-08)

Six tests drive the orchestrator's LIVE flows end-to-end through fake
lobby/session services on the [Inject] seams (real InviteService /
AcceptanceSignalService / LobbyPropertyWriter / SoapPartyEventBus /
PartyMemberService / LobbyRefreshScheduler composed underneath):
send-invite tracks + publishes the composite `invite_payloads` (with the
REAL relay session id) onto the lobby CurrentPlayer + raises OnInviteSent;
cancel clears the tracker, republishes WITHOUT the line, and fires
OutgoingInviteCleared; the refresh cycle diffs OnlinePlayers (self excluded,
leavers dropped) and detects an incoming invite from a remote player's
payload line (LastPendingInvite + OnInviteReceived) with repeat-refresh
DEDUP; decline resolves the pending invite (OnInviteResolved); and
IPartyStateQuery mirrors the state machine. Private RefreshAsync is driven
inside a Tick per the C4/C6 sync-context discipline. **1415 tests green in
BOTH configs (1103 + 312)**; all 5 CLI modes exit 0; both client diags
byte-identical. bleeding-edge unmoved. PartyInviteController deferred to
next iteration (no room this round).

### HostConnectionService port ✅ (2026-07-08) — the party orchestrator lands

`NetworkDiagnostics` ported first (engine grew
`Application.internetReachability` + `NetworkReachability`; the engine
SessionException gets a structured-Error classify branch mirroring the
upstream string branch, which stays verbatim-dead until the real SDK; the
RequestFailedException arm is the only deviation) — un-carrying all 8
diagnostics deviation lines across ring 3. Then the **2,078-line
`HostConnectionService`** ported (2,159 lines with header + markers; verified
by mechanical-transform diff, 15 hunks all accounted for). FULLY LIVE: the
whole refresh cycle (mutex, converge throttle, online-player diff,
incoming-invite scan/dedup, acceptance-signal scan + republish, B8
joined-party cross-check, full RefreshPartyMembersAsync error matrix +
reconnect escalation), invite send/cancel/accept/decline (three-phase accept
incl. leave-own → JoinByIdAsync), member reconcile + PlayerLeaving relay,
session lifecycle (EnsurePartySessionAsync with NM shutdown via
NetworkTransitionService), presence-lobby join + identity republish, state
machine, SOAP bus, all NetDiag lines. Deviations: 2 services-phase
(IHostSession.RemovePlayerAsync kick; RequestFailedException 404 arm), 4
party-system (PartyInviteController-dependent: LeavePartyAsync body + 3
IsTransitioning guards — all fall through on upstream's own null-controller
truth values), 1 scene-phase (GetActiveScene → GameLoop scene-name read).
Behavior tests intentionally DEFERRED to the next iteration (loop directive —
the file lands build-green this round). **1409 tests green in BOTH configs
(1097 + 312)**; all 5 CLI modes exit 0; both client diags byte-identical.
bleeding-edge unmoved.

### Party-services ring 3 ✅ (2026-07-08)

The three UGS-adjacent services are IN with their state logic live:
`NetworkTransitionService` (shutdown gate live end-to-end on the synchronous
engine Shutdown; client-connection wait on live flags; scene-sync wait takes
upstream's own missing-SceneManager fail-soft branch; 7 transport-phase
deviations for the not-yet-ported client-transport surface),
`PartySessionService` (full LeaveAsync host-delete/member-leave teardown,
PlayerLeaving relay, retry loops + transient classification LIVE against the
new engine `SessionException`/`SessionError`; only the MultiplayerService
create/join call statements are services-phase gated), and
`PresenceLobbyService` (SavePropertiesAsync mutex flow, identity-property
build, LeaveAsync, ForceReset live; query/join/create interiors gated). The
engine placeholder surface grew `ISession.LeaveAsync`/`AsHost()`,
`IHostSession.DeleteAsync`, `SessionError`, and `SessionException` (structured
`.Error` — classification code matches codes, never message text), which also
let `MultiplayerSetup`'s LAST services-phase teardown deviation be RESTORED
(host-delete/leave on transport failure). Recovery note: the
PartySessionService worker died on an API server error mid-write and was
resumed from its transcript to complete the file. Tests (+9):
session teardown paths + refresh delegation, lobby property-save/leave/reset,
transition shutdown/wait/fail-soft. **1409 tests green in BOTH configs
(1097 + 312)**; all 5 CLI modes exit 0; both client diags byte-identical.
bleeding-edge unmoved.

### Party-services ring 2 ✅ (2026-07-08)

Four more services live, ZERO deviations end-state: `LobbyRefreshScheduler`
(cadence + 0.75s boost window + deferred reset), `InviteService` (per-player
invite-slot payloads — `targetId|localId|sessionId|name|avatar` lines, PENDING
→ real-session rewrite, unscaled-time expiry), `LobbyPropertyWriter`
(mutex → refresh → set → save-with-retry; the retired `.AsMainThread()`
boundary maps to a plain await under GameSynchronizationContext), and
`AcceptanceSignalService` (lobby scan for accepted_invite signals + publish
through the writer mutex — its two carried `CurrentPlayer.SetProperty` sites
were RESTORED in-iteration). The engine ISession placeholder grew
`CurrentPlayer` (new writable `IPlayer` : IReadOnlyPlayer + SetProperty),
`RefreshAsync()`, `SaveCurrentPlayerDataAsync()`. Engine `IPlayer` collides by
design with `CosmicShore.Gameplay.IPlayer` (both are original contract names) —
14 harness/test files that import both namespaces take a
`using IPlayer = CosmicShore.Gameplay.IPlayer;` alias; files in the Gameplay
namespace need nothing (enclosing namespace wins). Tests (+8):
`LobbyRefreshSchedulerTests` (interval/boost-window/deferred),
`InviteServiceTests` (payload format, PENDING rewrite, expiry),
`AcceptanceSignalServiceTests` (scan matching only OUR invites; publish
writes accepted_invite through the real mutex flow). **1400 tests green in
BOTH configs (1088 + 312)**; all 5 CLI modes exit 0; both client diags
byte-identical. bleeding-edge unmoved.

### Party-system arc groundwork ✅ (2026-07-08)

The no-UGS subset of the party layer is IN (11 files): `HostConnectionDataSO`
(pure SOAP container — all its ScriptablePartyData types were already ported),
all six `Controller/Party/Interfaces/` contracts (UniTask → Task; the
`Unity.Services.Multiplayer` using maps to `CosmicShore.Engine.Networking`),
`PartyState` + `PartyStateMachine` (transition table live; PartyState values
frozen in EnumFreezeTests: Disconnected 0 … Reconnecting 6), `SoapPartyEventBus`
(implements no interface — verified upstream), and `PartyMemberService` FULLY
live: the engine ISession placeholder grew `Players`
(IReadOnlyList&lt;IReadOnlyPlayer&gt;) plus new `IReadOnlyPlayer` /
`PlayerProperty` / `VisibilityPropertyOptions` placeholders, so
`SyncFromSession`'s roster reconcile runs verbatim (no deviations anywhere in
the batch). Tests: `PartyStateMachineTests` (3 — happy path, invalid-transition
rejection, from/to event) + `PartyMemberServiceTests` (2 — identity-property
parsing with fallbacks; reconcile adds joiners / removes leavers / never evicts
the local player, events through the real bus) + the PartyState freeze theory.
**1392 tests green in BOTH configs (1080 + 312)**; all 5 CLI modes exit 0; both
client diags byte-identical. bleeding-edge unmoved.

### CanvasGroup / UI-fade arc ✅ (2026-07-08)

Engine grew **`CanvasGroup`** (data-only alpha/interactable/blocksRaycasts —
original contract, render backend gives alpha a visual meaning later) and
**`GameTask.WhenAll`** (`Task[]` join + the `(Task, DelayAwaitable)` overload
that bridges the delay arm to a Task EAGERLY, so both arms run in parallel
exactly as upstream — a DelayAwaitable starts its clock at await-time, so
sequential awaiting would wrongly serialize it). ALL seven CanvasGroup
deviation sites in `MenuCrystalClickHandler` are RESTORED — the field pair +
fadeDuration + `_savedMenuAlphas` + Start's initial hide + the saved-alpha
capture + BOTH `UniTask.WhenAll` fade arms (now `GameTask.WhenAll`) + the
entire 7-method `#region UI Fade` verbatim. The only deviations left in the
file are the `MainMenuCameraController` pair (camera arc). TournamentSceneView
needed no restore — its CanvasGroup mention lives inside the unported card
prefab views (UI-card arc), not in discrete blocks. Tests:
`MenuFreestyleFadeTests` (4 — initial hide; enter-fade landing mid-blend while
the camera arm still gates, proving the WhenAll arms are parallel; exit
restoring SAVED menu alphas so hidden panels stay hidden; click-spam gate).
Test-rig note: toggles are driven INSIDE a Tick via a TickAction driver — the
C4/C6 xunit-sync-context trap makes direct calls race their Task tails on the
thread pool. **1380 tests green in BOTH configs (1068 + 312)**; all 5 CLI
modes exit 0; both client diags byte-identical. bleeding-edge unmoved.

### Menu_Main scene-controller arc ✅ (2026-07-08)

`MainMenuController` ported verbatim to `System/MainMenuController.cs` — the
menu sub-state machine (None → Initializing → Ready ⇄ Freestyle →
LaunchingGame, table-validated), `ConfigureMenuGameData`,
`ApplyMenuVesselClassToHost` (owner-writable NetDefaultVesselType push through
`NetworkManager.LocalClient`), `HandlePlayerPairInitialized` client-side
autopilot activation, `ActivateLocalPlayerAutopilot`, and the `OnStateChanged`
event — fully live, zero commented deviations in the file itself. Engine grew
`NetworkManager.LocalClient` (original surface). `AnalyticsServiceFacade`
gets a Deviation-#11-style type-preserving SHELL (`RecordMenuReady` +
shell-only `MenuReadyThisSession` observability; real UGS Analytics facade
lands with the instrumentation phase). The 4 carried
`MenuFreestyleToggleTests` methods are RESTORED (live), plus 4 new
`MainMenuControllerTests` drive the state machine end-to-end through the real
SOAP events (full flow, invalid-transition rejection, launch-from-freestyle,
menu game-data config). **1376 tests green in BOTH configs (1064 + 312)**;
all 5 CLI modes exit 0; both client diags byte-identical. bleeding-edge
unmoved this iteration.

### Transport-callback arc ✅ (2026-07-07)

The engine NetworkManager grew the full Netcode callback surface —
`ConnectionApprovalCallback` (public delegate field, as in Netcode) +
`ConnectionApprovalRequest`/`ConnectionApprovalResponse` (nested, Netcode 2.x
field set), `OnClientDisconnectCallback` / `OnTransportFailure` events with
`NotifyClientDisconnect`/`NotifyTransportFailure` transport-driver entry
points, `StartHost()` (runs the local client through approval — a rejection
aborts the start, matching Netcode host self-approval), and `Shutdown()`
(synchronous: stops listening, drops role flags, clears client tables — the
original's `WaitUntil(!IsListening)` completes on first check). ALL six
transport-phase deviation blocks in `MultiplayerSetup` are RESTORED — callback
wiring/unwiring (3 sites), `OnConnectionApprovalCallback` (approve +
auto-create player objects), and both `Shutdown()` sites — making the host
lifecycle fully live end-to-end. Tests: `NetworkManagerCallbackTests` (6:
approval flow incl. rejection, listening guard, teardown, notify entry
points) + `MultiplayerSetupTests` (3: wiring, double-wire guard, and the
REAL transport-failure handler tearing down session + manager + raising
OnSessionEnded after its 500 ms delay on the ticked loop). Still deviations
in `MultiplayerSetup`: the UGS Multiplayer session surface (services phase)
and the HostConnectionService/PartyInviteController self-rescue branches
(party system). **1368 tests green in BOTH configs (1060 + 308)**; all 5 CLI
modes exit 0; both client diags byte-identical.

### Initializer-remainder arc ✅ (2026-07-07)

The three remaining vessel-initializer files are IN (DomainAssigner needs no
port — **retired upstream**; domain assignment now lives in the initializers
themselves): `ServerPlayerVesselInitializerWithAI` verbatim (zero deviations —
AI backfill, `GetBalancedDomain` enum-order tie-break, `NormalizeUnassignedHumans`,
`destroyWithScene: false` AI spawns, AIPilot game-mode config; `SO_GameList`
ported alongside), `MenuCrystalClickHandler` (state machine, ownership guards,
autopilot toggles, `IsMultiplayerSession` timeScale guard, and all four SOAP
bracket raises live; CanvasGroup fades + `MainMenuCameraController` carried as
marked deviations per the #17 camera/UI-shell precedent), and `MultiplayerSetup`
(host-lifecycle once-guard + signed-in flow + disconnect/transport-failure
handler bodies live; the UGS Multiplayer session surface + Netcode callback
wiring carried as services-phase / transport-phase deviations — see NEXT UP 1;
`HostConnectionService` / `PartyInviteController` self-rescue branches carried
as party-system deviations). Upstream tests ported alongside:
`ServerPlayerVesselInitializerWithAITests` (14 tests, fully live) +
`MenuFreestyleToggleTests` (9 live, 4 carried pending `MainMenuController`).
The "wire the menu-swap chain into the playable client scene" clause was
already satisfied — `ToyboxController` self-discovers the client's
`MenuServerPlayerVesselInitializer` (vessel-changer e2e test covers it).
**1359 tests green in BOTH configs (1051 + 308)**; all 5 CLI modes exit 0;
both client diags byte-identical.

### Wanderway client content ✅ (2026-07-07, second pass)

The freestyle client now feeds the conveyor REAL conserved prisms:
`SkimRacePrismFactory.AddPrismComponents` builds the plain environment Prism
template (V15 family, caller-owned, mirror of the HealthPrism builder), and
`FreestyleFactory.WireConveyorPrismPrefab` pre-resolves the controller's
zero-config default toybox (reflection — the verbatim `ToyboxController` stays
untouched) and wires the template through the upstream
`ConveyorToyDefinitionSO.SetRuntimePrismPrefab` hook. End-to-end proof:
`ConveyorFlythrough_StreamsMicroscenes_OfRealConservedPrisms` (fly through
Toy_conveyor in the real freestyle scene → belt spawns as a toybox-root
sibling → scenes bloom with plain-`Prism` clones carrying colliders → second
pass stops the flow with every laid prism conserved). **1335 tests green in
BOTH configs (1051 + 284)**; all 5 CLI modes exit 0; both client diags
byte-identical (conveyor correctly inert on the autopilot lava-lamp — it only
flows in freestyle).

### Rung-5 leftovers (optional polish, from iteration 20)

- Team-crystal renderer polish: domain-colored ring/tint so the lock is readable
  before contact.
- AI boost balance: rivals never boost (the real AIPilot doesn't drive the
  SkimRace boost rule), so their energy bars sit full.

## Vessel-initializer (menu-swap) arc (landed after the controller-chain arc — dedicated agent arc)

The networked player→vessel spawn/swap chain is IN: `ServerPlayerVesselInitializer`
+ `ClientPlayerVesselInitializer` + `MenuServerPlayerVesselInitializer` ported
verbatim to `src/CosmicShore.Game/Controller/Multiplayer/` (GameTask mappings;
RPCs local-invoke per the Player/RoundStats precedent — targeted ClientRpc fan-outs
iterate the empty `ConnectedClientsList` in single-process host-mode; no UGS
surface in these files, so no services-phase deviations), plus `NetcodeHooks`
(`Utility/Network/`, verbatim). Engine grew the original-contract **NetworkObject
component** (authored on prefab roots; `SpawnWithOwnership`/`Spawn`/`Despawn` fan
out to every NetworkBehaviour on the object+children, all sharing ONE object id —
`NetworkBehaviour.SpawnWithId` — so id round-trips like `NetVesselId` →
`TryGetVesselByNetworkObjectId` resolve regardless of component order; the E12
per-behaviour handle property now lazily resolves/adds the component, same
Despawn(destroy) contract), `NetworkManager.LocalClientId` / `ConnectedClients` /
`ConnectedClientsList` / `SpawnManager` (+ `NetworkClient`, `NetworkSpawnManager`),
and the RPC params structs (`ClientRpcParams`/`ClientRpcSendParams`/
`ServerRpcParams`/`ServerRpcReceiveParams`). **All 10 toy menu-swap deviations
restored** (ToyContext.VesselInitializer, ToyboxController context wiring,
VesselChangerToySet.Apply → `RequestSwap` + swap-wait loop): the vessel-changer
toy works end-to-end headless — fly into the toy → RequestSwap → despawn/spawn/
ReInitializePair → toy flips to the class you left → freestyle control restored.
Tests: `VesselInitializerChainTests` (NetworkObject component contract; host
spawn chain incl. server-side menu domain reset + autopilot; RequestSwap swap +
pose snapshot + registry rewire; same-class no-op) and
`ToySystemTests.VesselChangerEndToEndTests` (the full toy→swap→restore loop on
the real chain). Test-rig note: calling `RequestSwap` (or anything that reaches
`VesselPrismController.StartSpawn`) DIRECTLY from test code starts the spawn-loop
async-void on xunit's AsyncTestSyncContext — stop the loops before the test
method returns (the C4/C6 trap; the runner waits ahead of Dispose). **1212 tests
green in BOTH configs (954 + 258)**; client screenshot smoke byte-identical to
the pre-arc baseline (`frame 300, crystals [4,0,1,1] … trail 126`). Remaining for
a later arc: `ServerPlayerVesselInitializerWithAI` (game-scene AI backfill),
`MenuCrystalClickHandler`, `MultiplayerSetup`/`DomainAssigner`, and wiring the
menu-swap chain into the playable client scene.

## Controller-chain + AstroLeague arc (landed after iteration 21 — dedicated agent arc)

The real game-controller chain is IN (the arc flagged by the SegmentSpawner note
below): `MiniGameControllerBase` → `MultiplayerMiniGameControllerBase` →
`MultiplayerDomainGamesController` → `HexRaceController` ported verbatim to
`src/CosmicShore.Game/Controller/Arcade/` (RPCs local-invoke per the Player/RoundStats
precedent; GameTask mappings; scene-reload/fade surfaces deviation-marked — Deviation
#17), plus `CountdownTimer` (timing-equivalent headless beat loop). **AstroLeague** is
IN on top of it (all 7 `AstroLeague/` files + `AstroLeagueObjectiveProvider` +
`UI/Interfaces/IObjectiveProvider`; ball physics verbatim on new engine E18 rigidbody
dynamics; presentation deviations marked — Deviation #18). New headless CLI round:
`--mode astroleague` (`src/CosmicShore.Cli/AstroLeagueRound.cs`) drives the WHOLE match
through the real chain — ready → 3-2-1 countdown → kickoff parking → trigger-pass
vessel strikes → goal-plane detection → RoundStats.GoalsScored → celebration/kickoff
loops → mercy / full-time / golden-goal → `SyncFinalScores` → ranked
`GameDataSO.Results` — deterministic per seed, exit 0 (seed sweep 1/2/3/7/42/99/2026
and players 2/4/6 all PASS at the harness tuning: `settings.maxSpeed=100→35` for
AI-catchable play, mouth 60, boundary 170). Engine additions: E18 Rigidbody
integration step in `GameLoop.RunFixedSteps`, `PhysicsMaterial`, `Light`, `BlendMode`,
material keywords/renderQueue, `FixedString32Bytes`,
`NetworkManager.ConnectedClientsIds`, ISession events, `FindAnyObjectByType`.
Tests: `MiniGameControllerChainTests` (template-method rounds→turns→end via
GameDataSO turn events; countdown beats; HexRace domain-aggregated winner + golf
sentinels + ranked Results + race-over latch) and `AstroLeagueTests` (scoring-rule
mercy/points/results/reveal; match-monitor clock/pause/OT/ForceEnd; ball seeded
bit-identical trace + boundary containment; full-match integration incl. the
golden-goal path and seed purity). **1203 tests green in BOTH configs (945 + 258)**;
hexrace CLI + client screenshot smoke byte-identical to the pre-arc baseline
(`frame 300, crystals [4,0,1,1] … trail 126`). Remaining chain siblings for a later
arc: SinglePlayer*, Joust/CellularDuel/CrystalCapture/Freestyle/WildlifeBlitz
controllers (Tournament landed — see the Tournament arc below).

## Tournament (Maelstrom) arc (landed after the controller-chain arc — dedicated agent arc)

The session-level meta chaining the domain minigames is IN: all four
`Controller/Arcade/Tournament/` files (`TournamentController` — the persistent
network-free brain, `TournamentStateMachine`, `TournamentLobbyNetwork`,
`TournamentSceneView`) + `Utility/DataContainers/Tournament/` (`TournamentDataSO`,
`TournamentStandingsFormatter`) + `DomainColorPaletteSO` ported (fold/race-to-6/
draw/phase logic verbatim; deviations in #19). Engine additions:
`CosmicShore.Engine.SceneManagement` (`LoadSceneMode` + static `SceneManager` with
the original `sceneLoaded` event; port surface `NotifySceneLoaded` /
`ResetSceneLoadedSubscribers` until real scene transitions land), Netcode 2.x
universal-RPC metadata (`RpcAttribute(SendTo)` / `RpcParams.Receive.SenderClientId` —
local-invoke), and a headless `Engine.UI.Button` shim (onClick UnityEvent) beside
the TMPro shim. New headless CLI session: `--mode tournament`
(`src/CosmicShore.Cli/TournamentRound.cs`) drives lobby → host random draw
(mode + intensity ∈ [1..ceiling], no immediate repeat) → headless leg → per-domain
{2,1,0} standings fold from the synced `Results` → hub → … → race-to-6 / cap-7 →
summary via `FormatFinal` — deterministic per seed (repeat-run transcripts
byte-identical), exit 0. Every leg is simulated by the real headless
`HexRaceRound` until the Joust / Crystal Capture controllers port (the drawn mode
still exercises the real draw/repeat-avoidance path). Tests:
`TournamentSystemTests` (17 — state-machine table incl. the Lobby→Complete
race-to-6 route, fresh-start/ceiling capture, menu-return teardown, 3-game fold +
cross-peer determinism from identical synced Results, race-to-6 hub/summary
routing, game cap, the authoritative phase-independent summary decision at the
Maelstrom load, Play-Again reset keeping the ceiling, splash-dwell window, seeded
draw determinism + repeat avoidance, formatter (You)-tag/ordering, lobby-network
arm/all-ready-snap/one-shot BeginNextRound) + `TournamentDataSOTests` ported
verbatim into Tests.Ported (18 — the Unity edit-mode fold suite). **1238 tests
green in BOTH configs (962 + 276)**; hexrace/astroleague/smoke CLI modes still
exit 0.

Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — info
logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
Gate BOTH configs when touching logging paths.

Track infrastructure (landed alongside iteration 20): `SegmentSpawner` ported
(the HexRace deterministic-track spawner — seeded segment placement, prism trails
per segment, intensity-scaled spacing). ~~One deviation: the diagnostic
super-shield block restores when `PrismStellatedOctahedronShield` ports with the
engine Mesh/MeshFilter arc~~ **CLOSED by the mesh arc (below)** — the super-shield
diagnostic block is verbatim again. Engine gains the `Transform.Rotate(axis, angle)`
overload. This unblocked the real `HexRaceController` chain
(MiniGameControllerBase → Multiplayer → DomainGames → HexRace, 1,112L) — **DONE**,
see "Controller-chain + AstroLeague arc" above; replacing SkimRaceDirector with the
real controller in the CLIENT remains a convergence-ladder follow-up.

## Mesh arc (landed after the cell-ecology completion — dedicated agent arc)

The engine carries REAL MESH DATA now; every `PORT Deviation (mesh arc, …)` marker in
`src/` is restored (grep count: 0 remaining). Engine additions
(`src/CosmicShore.Engine/`): original-contract **`Mesh`** (Rendering/Mesh.cs —
vertices/normals/uv/colors buffers with the original copy-on-get semantics,
triangles ↔ submeshes + `subMeshCount`/`SetTriangles` (auto-grow port convenience,
documented), settable `bounds` + `RecalculateBounds`, smooth `RecalculateNormals`,
`Clear`, no-op `MarkDynamic`, `indexFormat` + `Engine.Rendering.IndexFormat`);
**`MeshFilter`** (`sharedMesh` plain ref; `mesh` instance-on-access — clones the
shared mesh into a cached "<name> Instance" and repoints sharedMesh, the original
contract); **`SkinnedMeshRenderer.sharedMesh` + `BakeMesh`** (headless: the bake IS
the bind pose — deep copy); **`MeshCollider`** (`sharedMesh`/`convex`; the
TriggerPass overlaps it as its mesh-bounds world AABB — rotation-ignored like the
phase-2 box convention, null mesh never overlaps; participates in
OverlapSphere/CheckBox queries too); **`GameObject.CreatePrimitive` fills real
shared primitive meshes** (PrimitiveMeshes.cs: Cube 24-vert flat, Sphere r=0.5
icosphere [documented deviation: icosphere not UV-sphere], Capsule r=0.5 h=2,
Cylinder, Plane 10×10 [single quad, documented], Quad; non-sphere colliders sized
to mesh bounds); **`Renderer.bounds` refined** — a sibling MeshFilter (or SMR
sharedMesh) supplies real extents via an 8-corner TRS sweep, unit-cube convention
kept when meshless; **`Matrix4x4`** (Math/Matrix4x4.cs, in `Engine.Rendering` for
the same System.Numerics CS0104 reason as PrimitiveType — TRS/MultiplyPoint3x4/
MultiplyVector/columns/product); **`RenderParams` + `Graphics.RenderMeshInstanced`**
(data-only SUBMISSION RECORDER, ring-bounded at 16 so per-frame callers never grow
memory over soaks; thread-safe); **`AnimationCurve`/`Keyframe`** (cubic Hermite,
clamp outside keys, EaseInOut/Linear/Constant factories); **`[ContextMenu]`**
(inert marker attribute). Ported verbatim (README substitutions only; the
qualified-name map gains `UnityEngine.Rendering.X` → `CosmicShore.Engine.Rendering.X`
for ShadowCastingMode/IndexFormat): `Utility/OctahedronMeshGenerator` +
`Utility/StellatedOctahedronMeshGenerator` + `Utility/IcosphereMeshGenerator`,
`Controller/Vessel/PrismOctahedronShield` + `PrismStellatedOctahedronShield` (the
full engage-bloom / shatter-overlay state machines, Box ↔ convex MeshCollider swap,
mass = ρ·8abc ↔ ρ·36abc/ρ·108abc — no presentation deviations needed: AnimationCurve
+ Mesh cover everything). **Deviations RESTORED, diff-verified vs Assets**:
`SegmentSpawner.SuperShieldSpawnedPrisms` (the stellated super-shield diagnostic —
file now fully verbatim), `VesselModelBuilder` (all 31 markers — the
MeshFilter/SkinnedMeshRenderer harvest + AddMesh + pose math live; TryBuild returns
TRUE for mesh rigs, so the vessel-changer toy shows real mini hulls when prefab
meshes exist), `CapsuleMembrane` (all 19 draw-internal markers — Matrix4x4 TRS
arrays, RenderParams, per-frame `Graphics.RenderMeshInstanced`, preset/fallback
matrix paths, `GetBuiltinCapsuleMesh`; sole remaining marker is the
`OnDrawGizmosSelected` body, re-tagged "(restore when engine Gizmos lands)"),
`AstroLeagueBall` (icosphere mesh swap + owned-mesh destroy — part of Deviation #18,
now closed for the mesh half; ParticleSystem/haptics halves still staged). Tests:
`MeshArcTests.cs` (35 — Mesh buffer/submesh/bounds/normals contracts, MeshFilter
instancing, BakeMesh, MeshCollider trigger enter/exit + spatial queries + null-mesh
inertness, CreatePrimitive real+shared meshes, Matrix4x4 TRS vs transform math,
Graphics recorder ring bound, AnimationCurve smoothstep shape, octahedron/stellation
vertex+face counts + ContainsPointLocal boundary cases (face/vertex/corner exactly
on the L1/tetrahedral surfaces) + FaceScale topology stability + shatter normals,
icosphere subdivision counts, both shields' instant/animated engage + shatter
overlay + OnDisable snap + IsPointInsideShield, SegmentSpawner super-shield
integration on a real PrismRig, CapsuleMembrane fallback path submitting 42-instance
draws of the real capsule primitive) + `ToySystemTests.VesselModelBuilderTests`
(3 — TryBuild TRUE for MeshFilter rigs with material/shadow flags + normalize-to-
radius scale, skinned-mesh harvest recentring, meshless rig still falls back false).
**1302 tests green in BOTH configs (1026 + 276)**; all 5 CLI modes exit 0.

Quest/action systems (landed alongside iteration 19, part 2): `QuestSystem` +
`UserActionSystem` + `CallToActionSystem` ported verbatim — the full
quest/user-action/CTA orchestration chain is in (SingletonPersistent-based,
event-driven quest progress on UserActionSystem.OnUserActionCompleted).

Quest-data groundwork (landed alongside iteration 19): `Quest` + `SO_QuestChain`
+ `CallToAction` + `UserAction` + `VirtualItem` + `ItemPrice` ported verbatim.
SA1 quest deviation CLOSED — `SO_TrainingGame.SO_QuestChain` is a real field
again (file now fully verbatim). `QuestSystem` (105L, MonoBehaviour orchestrator)
remains for the systems phase.

Ecosystem + content completion (landed alongside iteration 19): the spawnable
family is COMPLETE — SpawnableCrystal, SpawnableFlora, SpawnableGyroid,
SpawnableWall, SpawnableLSystem, SpawnableWaypointTrack ported verbatim (all
blockers cleared by the rung-3 CrystalManager and the Assemblers arc). Rung-6
concretes complete too: BranchingFlora, LightFauna(+Manager/DataSO/ManagerDataSO),
BodySegmentFauna, Bone, Worm, WormManager, LerpUtilities — all verbatim. Engine
gains `Random.rotation`. NOTE: the CrystalCollisionTurnMonitor `optionalEnvironment`
waypoint deviation can now be restored — deferred until rung-4 integration (the
rung-4 agent owns TurnMonitors/).

Ecosystem groundwork part 4 (landed alongside iteration 18): the Assemblers
family + Boid fauna chain ported verbatim — `Assembler` (base) + `GyroidAssembler`
+ `GyroidBondMate`/`GyroidBondMateData`/`GyroidBondMateDataContainer` (48-entry
baked bond table) + `CornerSiteType` + `WallAssembler` + `SchwarzPAssembler`, plus
`AssembledFlora` (carries `GrowthInfo`; one CT2 deviation: `crystal.GrowCrystal`
restores with the crystal-growth arc), `Boid`, `BoidManager`, and `BoidController`.
Engine gains original-contract API: `Physics.OverlapSphere` (allocating, optional
layer mask), `Physics.CheckBox` (AABB semantics, oriented overload accepted),
`LayerMask` defaults extended with the project's TagManager layer table (3D UI=6 …
TrailBlockOcclusion=18, incl. Mound=17 for the boid mound scan), and
`GetComponentsInChildren<T>` now returns `T[]` (original array contract — Boid
indexes by `.Length`). Two serializer deviations: `Boid.collisionEffects` and
`BoidManager.Boids` init inline (the original engine's deserializer auto-creates
serialized lists; the port engine has none). 23 tests across
AssemblerFamilyTests (bond table coverage, growth-site resolution through the real
CheckBox probe, wall alternation, Schwarz P surface stepping + reservation),
BoidChainTests (goal steering, Attach grazing + Explode combat through the real
spatial query — opposing mass only, conserved-mass clean), and PhysicsOverlapTests
(CheckBox, layer-mask filtering). Still open in this lane: LightFauna and
BranchingFlora for rung-6 ambience.

Shape-content groundwork part 2 (landed alongside iteration 18): 25 more
spawnables ported verbatim (BaseballCurve, Batman, CardioidSmear, CliffordTorus,
Comet, DartBoard, DriftCourse, Helicoid, HopfFibration, Infinity, Lightning,
LinkedRings, Pumpkin, RaceTrack, SchwarzPSurface, SingleTrailBlock, Smiley,
Spherene, Spiral, Star, TorusKnot, Tube, Wave, Zigzag, ShapeSign). Still blocked:
SpawnableCrystal/SpawnableFlora/SpawnableWaypointTrack (CrystalManager — rung-3
lane), SpawnableGyroid/SpawnableWall (Assembler family — assembler-agent lane),
SpawnableLSystem (check deps when lanes clear).

Shape-content groundwork (landed alongside iteration 18): `SpawnableShapeBase` +
8 shape spawnables (Circle, Ellipsoid, Helix, Cylinder, Diamond, Arrow, Heart,
FiveRings) + `ShapeDefinition` + `ShapeCollisionTrigger` + `ShapeSign`(+Events)
ported (verbatim; ShapeSign's two TMP_Text labels are UI-shell deviations until a
TMPro shim lands — the trigger flow + static event bus are verbatim). Engine gains
`Bounds` (full original contract), `Renderer.bounds` (unit-cube convention,
documented), and a kinematic `Rigidbody` placeholder (trigger physics needs no
rigidbodies; satisfies [RequireComponent] so authored setup ports verbatim).
5 tests. These shapes are the Phase-2 shape-drawing content (lava-lamp
freestyle) and general track decoration for the client.

Ecosystem groundwork part 4 — cell-ecology completion (V12 families CLOSED):
`SpawnProfileSO` + `FloraConfigurationSO` (engine gains the inert `MinMaxAttribute`
shim for the original's `Unity.Entities.UI.MinMax`), the full spawner chain
(`ICellLifeSpawner` + `CellLifeSpawnerBase` + `RandomLifeSpawner` +
`IntensityWiseLifeSpawner`), `CellModifier` + `ExtraOmniCrystals`, `SnowChanger`
(cytoplasm — fully live headless, shards are plain GameObjects), and
`CapsuleMembrane` + `CapsuleMembraneAnimationSO`
(`src/CosmicShore.Game/Game/Environment/`, mirrors `_Scripts/Game/Environment/`) —
all verbatim modulo the README substitutions. **All 41 deviation markers of these
families restored** (36 in `Cell.cs` — spawner fields/StartSpawnerForMode/
StopSpawner, SpawnProfile in CurrentFaunaSpawnPeriod, ApplyModifiers,
SpawnCytoplasm + both cytoplasm destroys, CapsuleMembrane in MembraneRadius, the
`using CosmicShore.Game` directive; 5 in `CellConfigDataSO.cs` —
CytoplasmPrefab/CellModifiers/SpawnProfile) — a Cell now runs FULLY ALIVE
headless: crystal-triggered post-init → modifiers → cytoplasm → real spawner
seeding flora + fauna. CapsuleMembrane's simulation surface (Radius — the
Cell.MembraneRadius read — icosphere layout, placement noise, offline bake math)
is live; only the instanced-draw internals carry 19 new
`PORT Deviation (mesh arc, …)` markers (Mesh/MeshFilter/Matrix4x4/RenderParams/
Graphics/Gizmos — same staging as VesselModelBuilder). New `CellEcologyTests` (8)
exercise the restored paths end-to-end AND freeze the locked invariants
(Docs/ECOSYSTEM.md): a 2-sim-minute soak proving a seeded population NEVER
shrinks without an active force (no imposed death), starvation → wither → the
ONE elemental crystal reparented to the cell (mass conserved, LifeFormCrystal
fast path), fauna seeding in the ONE controlling color + flora never Blue (no
domain asymmetry), and the phase ladder still climbing on LiveVolume with a live
spawner attached. Test-only finding, preserved as-is: `Cell.UpdateCellStats`
writes `LifeFormsInCell` onto a COPY (CellStats is a struct) — verbatim upstream
quirk, worth fixing upstream.

Ecosystem groundwork part 3 (landed alongside iteration 18):
`Physics.OverlapSphereNonAlloc` implemented against the TriggerPass collider
registry (trigger + non-trigger, deterministic registration order, capacity
truncation) — the spatial query Boid behavior scans with. 4 tests.

Ecosystem groundwork part 2 (landed alongside iteration 18): `Flora` + `Fauna` +
`FaunaConfigurationSO` + `FaunaReproductionRules` ported verbatim. V12 fauna
deviations in Cell CLOSED: liveFauna registry, per-species lineage counts
(`GetLiveFaunaCount(FaunaConfigurationSO)`), and `GetLiveHerbivoreCount` now use
the real types (diet + alive-prey filtering live again). CellTests registry test
rewritten against real lineage semantics (AssignLineage → register; OnDestroy →
unregister + species decrement). Still open in this lane: concrete fauna
(Boid/BoidManager, LightFauna) and concrete flora (BranchingFlora, AssembledFlora)
for rung 6 ambience.

Ecosystem groundwork (landed alongside iteration 18): the LifeForm family ported —
`LifeForm` + `HealthPrism` + `Spindle` + `HealthBlockTracker` + `SpindleTracker` +
`ILifeFormEntity` + `ITeamAssignable` (verbatim modulo substitutions; one CT1
deviation: the `crystal.ActivateCrystal()` call in `Die` restores with
CrystalManager). SA1 deviation CLOSED: `LifeFormsKilledScoring` subscribes the real
static `LifeForm.OnLifeFormDeath` again. Engine gains original-contract API:
`Random.onUnitSphere`/`insideUnitSphere`, 4-arg `Object.Instantiate(original, pos,
rot, parent)`, `Scene.isLoaded` (flips false on GameLoop.Dispose). 6 tests
(maturity one-way, lethality at min blocks, dedup add, embedded-prism binding,
death event + destroy, SetTeam propagation). Conserved mass: health prisms die
only through the active Damage path — no decay anywhere in the family.

Track-content groundwork (landed alongside iteration 18): `SpawnableBase` +
`SpawnPoint` + `SpawnTrailData` ported verbatim (one dropped using:
UnityEngine.Serialization — FormerlySerializedAs lives in CosmicShore.Engine).
6 tests (caching by parameter hash, invalidation, leaf spawning, child-tree
nesting). Next in this lane: `SpawnableWaypointTrack` (539L) — needs
`CrystalPositionSet` from CrystalManager.cs (352L, unported; deferred until
rung-3 integration to avoid colliding with the crystal-respawn work) — which
then restores the CrystalCollisionTurnMonitor waypoint deviation.

Rung-4 groundwork (landed alongside iteration 17, part 2): `TurnMonitor` (base) +
`CrystalCollisionTurnMonitor` ported (UniTask→GameTask mechanical mappings; one
deviation: `optionalEnvironment` waypoint-derived target restores when
`SpawnableWaypointTrack` ports — explicit target + 39 fallback work now). 5 tests.
CS0414 added to NoWarn (verbatim serialized fields whose only consumer is a
deviated path). When rung 4 lands in the client: the race's crystal-target end
condition runs through this monitor against the shared RoundStats.

Rung-4 groundwork (landed alongside iteration 17): `ElementalComebackSystem` +
`SO_ElementalComebackProfile` ported verbatim — comeback buffs sized to the DOMAIN
deficit through the elementals fundamental (leading-domain players get nothing even
when personally trailing; clamped to the 0.0–1.5 band so base pips stay reserved
for the overtake effect). 7 tests cover profile selection, domain aggregation,
clamping, initial levels, and turn-end deactivation. When rung 4 lands in the
client: attach alongside the race director with a profile + the shared GameDataSO.

Rung-5 groundwork (landed alongside iteration 17): `ThemeManager` ported verbatim
(the single writer of `ThemeManagerDataContainerSO.TeamMaterialSets` — generates the
4 per-domain SO_MaterialSet copies from SO_ColorSet, and hands the ColorSet to
`GameFeedAPI`). GameFeedAPI + GameFeedPayload + ScriptableEventGameFeedPayload
ported (GameEventFeed/GameFeedEntry TMP views + GameFeedSettingsSO (DOTween Ease)
deferred with the UI-shell deviations). Engine `Resources` gained the path-keyed
`Register(path, asset)` / `Load<T>(path)` the original Resources.Load contract
needs. When rung 5 lands in the client: instantiate a ThemeManager with a wired
container at startup and read `GetTeam*Material` per domain for prism/vessel draw
colors.

## Drift-sync 2026-07-07 — bleeding-edge c18af492 merged in (takeover iteration)

bleeding-edge merged INTO this branch (42 commits, 87fdbc6b → c18af492: microscene/
environment-spawning unification + Wanderway content PRs #576-#580, elemental drain,
AstroLeague court boundaries, exit-gated toy re-arm). Full record:
`Port/docs/DRIFT_2026-07-07.txt`. **28 ported files re-verbatimed** (headliners:
ResourceSystem elemental drain, Toy exit-gated re-arm + 5s regrow, ToyboxController
four-toy default, VesselModelBuilder hull-filter + preview material, AstroLeague
court system, Prism pool-reuse scale-animator re-arm, IVessel/VesselController/
VesselTransformer SetInitialSpeed, FaunaReproductionRules PreyAvailable) and
**14 new files ported** (the whole microscene/conveyor family + spawning helpers +
PrismKind + AstroLeagueBoundary + VesselChangeSpeedByPrismEffectSO + upstream's own
MicroscenePatternsTests). Engine grew `Vector3.Normalize(Vector3)` and
`Collider.bounds` (Box/Sphere/Mesh overrides on the TriggerPass rotation-ignored
AABB convention). New drift deviations: VesselModelBuilder
MaterialGlobalIlluminationFlags, Microscene FadeIn (both render-arc). Golden-goal
seed re-swept 3 → 2 (court boundaries changed ball trajectories — CLI sweep
validated seeds 2/4/5/7/8 reach overtime). **1334 tests green in BOTH configs
(1050 + 284)**; all 5 CLI modes exit 0; race diag byte-identical
(crystals [6,1,4,1], trail 786); freestyle diag identical except toys 8 → 9 —
the Wanderway conveyor joining the default toybox ring.

## Loop protocol (every iteration)

1. `export PATH=/opt/dotnet:$PATH` (reinstall SDK via dotnet-install.sh if container is fresh).
2. `git checkout claude/quirky-cannon-sk8a02 && git pull origin claude/quirky-cannon-sk8a02`.
3. Read **NEXT UP**. Implement. `dotnet build && dotnet test` green.
4. Update status tables, Deviations log, and **NEXT UP** for the following iteration.
5. Commit (`feat(port): …` per GIT_RULES.md) and push.
6. Re-arm the wakeup (~25 min) with the original /loop prompt.
