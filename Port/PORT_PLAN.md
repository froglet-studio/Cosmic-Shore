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

## NEXT UP (after the UGS-core error/kick surface, 2026-07-08)

1. **Scope the UI-shell arc** (ToastChannel / SceneTransitionManager /
   SceneLoader): un-carries PartyInviteController's three UI-shell
   deviations (bounce toast, fade cover, splash re-arm); SceneLoader is the
   big one — SCOPE first, then port what goes live with tests per precedent.
2. **Track bleeding-edge**: merge upstream again next iteration; every merge
   reopens the drift-sync lane (survey + `docs/DRIFT_<date>.txt` per precedent).
3. Update this file, commit, push.

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
