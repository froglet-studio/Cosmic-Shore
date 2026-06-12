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
2. **Real trails = prisms**: VesselPrismController spawns real Prisms (visible
   blocks, conserved mass); real Skimmer contact grants energy through the trigger
   pipeline (contact arc landed).
3. **Real crystals/impactors**: claims via OnTriggerEnter → CrystalImpactor family
   (landed for the CLI round; bring to the client).
4. **Real scoring + HUD semantics**: HexRaceScoringRuleSO domain-aggregated end,
   golf standings; domains share totals.
5. **Real look**: SO_ColorSet domain palettes + SO_MaterialSet-driven visuals.
6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
   the real systems.

## NEXT UP (iteration 17)

1. **Rung 2**: real prism trails + Skimmer contact in the client. Replace
   SkimRaceSim's visual `TrailPoint` ribbons with real `VesselPrismController`
   spawning (StopSpawn before any test return — async-void trap) and grant
   trail-skim energy through the real Skimmer trigger pipeline (contact arc
   landed). Trails must render as prism blocks; skim detection must come from
   `OnTriggerEnter/Exit`, not distance checks.
2. Then rung 3 (real crystal claims via OnTriggerEnter → CrystalImpactor family
   in the client; landed for the CLI round already).
3. Update this file, commit, push.

Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — info
logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
Gate BOTH configs when touching logging paths.

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

## Loop protocol (every iteration)

1. `export PATH=/opt/dotnet:$PATH` (reinstall SDK via dotnet-install.sh if container is fresh).
2. `git checkout claude/quirky-cannon-sk8a02 && git pull origin claude/quirky-cannon-sk8a02`.
3. Read **NEXT UP**. Implement. `dotnet build && dotnet test` green.
4. Update status tables, Deviations log, and **NEXT UP** for the following iteration.
5. Commit (`feat(port): …` per GIT_RULES.md) and push.
6. Re-arm the wakeup (~25 min) with the original /loop prompt.
