# Android Stripped-Performance Branch

Branch: `claude/android-performance-stripped-dap5z2`

**One objective:** hold **60 fps on a mid-tier, years-old Android device** while flying the
**Squirrel** (with skimming + controls intact) through the **Wanderway conveyor toy** in freestyle.
Everything not load-bearing for that experience is stripped. This is a deliberately throwaway
performance branch — "we can do no harm."

## How to build the APK

A self-contained Editor build script ships on this branch:
`Assets/Editor/BuildAndroid.cs` (`CosmicShore.Editor.BuildAndroid`).

- **From the Editor:** `FrogletTools ▸ Build ▸ Android APK (Development)` — writes
  `Builds/Android/CosmicShore.apk`. The Development build uses Unity's debug keystore, so **no
  signing setup is required**.
- **From the command line / CI:**
  ```
  Unity -batchmode -nographics -projectPath . \
        -executeMethod CosmicShore.Editor.BuildAndroid.Build -quit
  ```
  Optional: `-outputPath <path.apk>`, `-release` (production flags; needs a real keystore).

The script forces the Android target, builds an **APK** (not AAB), and builds only the enabled
scenes (the 3 boot scenes below).

> **Tip — verify 60 fps on device:** a **Development** build auto-spawns the on-screen
> `DiagnosticsHUD` overlay (FPS, frame time, CPU/GPU bound verdict). Prefer the Development build
> (or `FrogletTools ▸ Build ▸ Android APK (Development)`) for perf testing; it's fully stripped from
> Release builds. (A Release build previously failed to compile this file — a pre-existing
> split-`#if` bug where `using UnityEngine;` was guarded but the class declaration wasn't; now the
> whole file is guarded so it compiles in every configuration.)

## How to reach the conveyor on device

1. App boots: **Bootstrap → Authentication → Menu_Main**. The autopilot Squirrel drifts in the
   "lava lamp."
2. **Enter freestyle** (take control of the Squirrel) — tap the crystal / freestyle affordance
   (`MenuCrystalClickHandler`). The gamepad **Start** button also toggles freestyle.
3. **Fly the Squirrel into the "Wanderway" toy** station (a world-space sphere near the play
   area). The belt of little worlds starts streaming ahead of you. Fly through it again to stop;
   again to resume. **Skim** the streamed prisms and crystals as you fly.

The conveyor is inert while on autopilot (menu) — you must be in freestyle for it to trigger.

## What was stripped / changed

Everything is gated behind one kill-switch: **`Assets/_Scripts/Utility/PerfStrip.cs`**
(`PerfStrip.Enabled = true`). Flip it to `false` to restore full behaviour — every guard becomes a
no-op. Nothing was deleted; each heavy system early-returns when the strip is on.

| # | Change | Where | Win |
|---|--------|-------|-----|
| 1 | **Vessel trail killed** — vessels lay no continuous prism trail | `VesselPrismController.StartSpawn()` early-returns on `PerfStrip.TrailsDisabled`. Single chokepoint; every re-arm caller (skimmer cooldown, drift/stationary toggles) becomes a no-op. | Biggest per-frame CPU/collider/GC win. Skimming is unaffected — it skims *other* prisms (the conveyor's), not the vessel's own trail. |
| 2 | **URP graphics cut** | `Assets/_Graphics/URP_Asset.asset`: `SupportsHDR 1→0`, `MSAA 4→1` (off), `RenderScale 1→0.8` | Kills the HDR buffer bandwidth, the MSAA resolve, and ~36% of the fragment/fill-rate cost. |
| 3 | **ARM64-only** | `ProjectSettings/ProjectSettings.asset`: `AndroidTargetArchitectures 3→2` | Drops the ARMv7 slice (any "years-old mid device" is ARM64); smaller APK, IL2CPP ARM64 is faster. |
| 4 | **Build trimmed to the boot path** | `EditorBuildSettings.asset`: only `Bootstrap`, `Authentication`, `Menu_Main` enabled (10 minigame scenes disabled) | Smaller/faster build, fewer break points. |
| 5 | **Toybox is conveyor-only** | `ToyboxController.PlaceToys()` filters to `ConveyorToyDefinitionSO` on `PerfStrip.ConveyorOnlyToybox` | Drops the other 3 toys' idle cost — notably the vessel-changer's 6 mini-ship preview models. Squirrel stays the only vessel. |
| 6 | **Conveyor mass cut ~71%** | `Assets/_SO_Assets/Toys/Toy_Conveyor.asset`: `poolSize 7→5`, `prismBudgetPerScene 100→40`, `aheadTargetScenes 5→3` | Max resident conveyor prisms **700 → 200** (each is a GameObject + BoxCollider + ~5 MonoBehaviours). This is the dominant per-frame content cost. |
| 7 | **Social networking overhead off** | `HostConnectionService.Update` (1.5s UGS presence refresh) and `FriendsInitializer.HandleSignedInEvent` early-return on `PerfStrip.DisableSocialNetworking` | Removes the recurring UGS-read + main-thread-marshal GC/hitch and the Friends init. **The Relay host that spawns the Squirrel is untouched.** |

Notes on the fundamentals (see `CLAUDE.md`): the trail kill disables prism **creation** at the
source — it never ages out or culls existing mass. "Not creating mass is allowed; aging it out is
the cheat." The conveyor's own conserved-mass stock is untouched. There is **no Cell ecosystem in
Menu_Main**, so the conveyor's flora/fauna recipes never fire there — it carries prisms + crystals
only, which is already the cheaper path.

## Offline boot (no UGS) — required for builds without a Unity Gaming Services project

This build has **no UGS project configured**, so the normal boot (which calls
`UnityServices.InitializeAsync()`, signs in anonymously, and brings the NetworkManager host up as a
UGS **Relay** session) throws and crashes on launch. `PerfStrip.OfflineMode` (= `Enabled`) makes the
boot never touch UGS:

- **Auth** (`AuthenticationServiceFacade`) signs in *locally* (synthetic player id) and raises
  `OnSignedIn` — no `UnityServices`/`AuthenticationService`.
- **Host** (`MultiplayerSetup` wires the Netcode callbacks; `AuthenticationSceneController` then calls
  a plain `NetworkManager.StartHost()`) — a **local host, no Relay**. `IsListening` goes true, so the
  existing menu vessel-spawn pipeline runs unchanged and the Squirrel spawns.
- **CloudSave / Analytics / presence lobby / friends / party** (`UGSDataService`,
  `AnalyticsServiceFacade`, `HostConnectionService`, `FriendsInitializer`) all early-return offline.
- `PlayerDataService` marks itself ready with the local default profile so the flow doesn't wait on a
  cloud load.

The local host is started at **Auth-scene** timing (not Bootstrap) to match the normal Relay bring-up,
so the non-networked Bootstrap→Auth scene load never races a running host. To restore full UGS
behaviour (on a build that has a UGS project), set `PerfStrip.Enabled = false`.

## ROOT CAUSE of the crash-on-launch: R8 minification stripped WorkManager

The definitive logcat (captured via `adb logcat -b crash -d`) showed the app dying in
`handleBindApplication` — **before any Unity code runs**:

```
Unable to get provider androidx.startup.InitializationProvider:
Failed to create an instance of androidx.work.impl.WorkDatabase
```

Chain: **Unity Ads** (the project's only external Android dependency) transitively bundles
**androidx.work (WorkManager)** → WorkManager's Room database locates its generated
`WorkDatabase_Impl` class **via reflection** at app start → `AndroidMinifyRelease: 1` ran **R8**
with no keep rules, which stripped/renamed that class → the ContentProvider threw on every launch.
This killed the process before Unity initialized, which is why no on-screen tool, boot trace, or
game-code fix could ever see or affect it.

Fix:
- `AndroidMinifyRelease: 1 → 0` (R8 off — nothing is stripped/renamed; the guaranteed fix).
- `useCustomProguardFile: 1` + `Assets/Plugins/Android/proguard-user.txt` with
  `-keep` rules for `androidx.work/room/startup/lifecycle` — inert while minify is off, but makes
  it safe if anyone re-enables minification later.

Note: Development builds (`AndroidMinifyDebug: 0`) never minified, so only Release builds crashed
this way. All the tested builds were Release builds from the Build Profile window.

## Audio (FMOD) disabled — earlier crash-on-launch suspect (kept stripped)

The pre-first-frame crash ("keeps stopping", no overlay) was **FMOD loading its banks at startup**.
FMOD is the live audio engine; the `AudioSystem` in the Bootstrap scene forces FMOD to initialize
during `Awake` (before the first frame), and `RuntimeManager.Initialize()` → `LoadBanks()` native-
crashes on the incompatible `Master`/`SFX` banks. (FMOD's *system* init has a graceful NOSOUND
fallback, but bank loading does not.) Fix, in `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`:

- `BankLoadType: 0 → 2` (All → **None**) — no banks are loaded at startup, so `loadBankFile` (the
  crashing native call) never runs. FMOD still initializes; it just has no events (silent). Audio is
  axable per the strip mandate.
- `AutomaticEventLoading: 1 → 0` — belt-and-suspenders (no auto event/sample loading).

To restore audio (on a build with compatible banks), set these back to `0` / `1`.

## On-device boot tracer (no PC / adb needed)

`BootTrace` (`Assets/_Scripts/Utility/BootTrace.cs`, gated on `PerfStrip.ShowBootTrace`) diagnoses
crash-on-launch when you can't attach a debugger:

- It records boot **checkpoints** + captured errors to a file and, on the **next** launch, renders
  the **previous** run on screen. Because a crash-looping app is reopened, you see where it died last
  time even though that run crashed.
- **To use:** launch the app (it crashes) → **reopen it** → a yellow `LAST RUN GOT TO: <checkpoint>`
  line + a `SHOW BOOT LOG` panel appear. **Screenshot it.** The last checkpoint (and any red error
  text) says exactly which stage died.
- Checkpoint spine: `SubsystemRegistration → AfterAssembliesLoaded → BeforeSplashScreen →
  BeforeSceneLoad → AfterSceneLoad → AppManager.Awake → AppManager.Start[:authKicked/:done] →
  AudioSystem.Awake → Auth:preStartHost → Auth:postStartHost(listening=…) → Menu:Ready (SUCCESS)`.
  E.g. stuck at `AudioSystem.Awake` ⇒ audio/FMOD; stuck at `Auth:preStartHost` ⇒ the local host;
  reaches `Menu:Ready` ⇒ boot succeeded.
- **If NO overlay ever appears** (even after reopening): the crash is *before the first rendered
  frame* — a native crash (graphics or a plugin's static init), which no on-screen tool can show.
  The same trace is also at `Android/data/<package>/files/cs_boottrace.txt` (openable with a Files
  app). Turn the whole thing off with `PerfStrip.ShowBootTrace`/`Enabled = false`.

## Tuning dials (if 60 fps isn't held, cut here first)

1. **Conveyor budget** — `Toy_Conveyor.asset`: lower `prismBudgetPerScene` (≥6) and/or `poolSize`
   (≥2). Resident prisms = `poolSize × prismBudgetPerScene`. This is the #1 dial.
2. **Render scale** — `URP_Asset.asset` `m_RenderScale`. 0.8 is aggressive; drop to 0.7 for more
   headroom, raise toward 1.0 if you have room and want crisper prism edges.
3. **Frame cap** — `Application.targetFrameRate` is set to 60 by `AppManager.ConfigurePlatform`
   (from `BootstrapConfigSO.TargetFrameRate`, default 60). Keep it **capped** (a `<=0` uncapped
   value thermally throttles a phone → *worse* sustained fps).

## Deliberately deferred (bigger wins, but real code / scene surgery — do with Unity in hand)

These were left out because they can't be verified without the Editor and a wrong move breaks the
boot/spawn path. In rough priority:

1. **Disable the 4 non-Home menu screens** (Store / Ark / Port / Hangar) in `Menu_Main.unity` —
   the UI canvas is ~1,617 MonoBehaviours under one root and is the biggest scene-load/memory cost.
   Set their roots inactive **in the scene** (serialized `m_IsActive: 0`) and verify `ScreenSwitcher`
   doesn't hard-index a disabled entry. Highest static/memory win.
2. **Local host instead of UGS Relay** — today the NetworkManager host *is* a Relay party session
   (`HostConnectionService.EnsurePartySessionAsync`), so the boot flow waits on a live Relay session
   (`AuthenticationSceneController.WaitForRelayReadyAsync`). For a solo offline build, replace that
   with a plain `NetworkManager.StartHost()` behind an offline flag. This is **new code, not a
   guard** — get it wrong and the Squirrel never spawns. Removes network startup latency + a failure
   surface (the build currently expects network at boot).
3. **Post-processing** — with HDR off the bloom pass is already cheaper (LDR); if still tight,
   lighten/disable the freestyle post Volume (costs some of the vaporwave look — conveyor spirit).
4. **Squirrel cosmetics** — `NudgeShardPoolManager` (skimmer tube-marker FX) and the Squirrel HUD /
   element bars are safe heavy cuts (both faded/idle in freestyle) but need prefab edits.
