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

## Audio RESTORED (+ skim/prism/crystal SFX & haptics)

The FMOD bank-load strip below is **reverted** (`FMODStudioSettings.asset` is byte-identical to
bleeding-edge again): the launch crash it guarded against turned out to be the R8/WorkManager
ContentProvider crash, not bank loading. Audio works everywhere; the committed StreamingAssets
banks were refreshed from `Cosmic Shore/Build/Desktop` (includes the "ui sounds" update).

Feedback wiring fixed in the same pass:

- **Haptics were dead game-wide** (also on bleeding-edge): `HapticController` was never placed in
  any scene, so its `GameSetting` reference stayed null and every haptic call silently no-oped.
  It now resolves settings lazily via the `GameSetting` persistent singleton — no scene placement
  needed — and `PlayConstant` honors the haptics-strength slider like presets do. The already-
  authored Squirrel haptics now fire: **skim** (Success preset, per prism entered), **prism hit**
  (HeavyImpact), **crystal** (MediumImpact). Editor is a structural no-op (native path is
  device-only) — feel them on the phone.
- **Skim SFX had a stale event path**: the Squirrel's skim tick pointed at `event:/SFX/Skim`, but
  the bank event is `event:/SFX/Oneshots/Gameplay sfx/Skim` and FMOD resolves by Path — fixed on
  the prefab. Prism-hit (`Vessel impact`/`Track collide`) and crystal-collect (`Crystal Collect` +
  the four elemental receive events) were already wired and exist in the banks.
- **Elemental-crystal haptics**: the conveyor lays *elemental* crystals, which route to the
  per-element effect lists on `SquirrelImpactorDataContainer` — those were empty, so
  `VesselHapticsByCrystalEffect` (MediumImpact) is now wired into all four. Also repaired the
  stale serialization on the shared `VesselHapticsByPrismEffect.asset` (other vessels deserialized
  it as `None`).
- If per-prism skim haptics feel spammy on dense trails, add a cooldown field to
  `SkimmerHapticsByPrismEffectSO` (anti-spam belongs in the SO config).

## ~~Audio (FMOD) disabled~~ — earlier crash-on-launch suspect (REVERTED, see above)

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

## The 200fps push (batches 1 + 2)

After the build first ran ("feels like 4 FPS"), a four-agent sweep of the boot-to-freestyle path
found and fixed, in two batches:

**Frame cap:** `BootstrapConfig._targetFrameRate` was `-1`, which Unity treats as **30 FPS on
Android** — the game ran under a 30fps ceiling the whole time. Now **240** (Swappy clamps to the
display's real refresh). `GraphicsSettingsApplier` is PerfStrip-gated so a stale saved settings
snapshot on device can't re-cap it.

**GPU:** the 767-line procedural HyperSea skybox (two 27-cell Voronoi loops + 7 FBM octaves per
pixel) is nulled at runtime (`PerfStripRuntime`) — cameras clear to deep-space solid color; the
post-processing chain (LUT render + UberPost + final blit bought for all-neutral overrides) is
disabled per camera; the skimmer's near-fullscreen double-sided additive forcefield sphere renderer
is off (collider + skim gameplay untouched); URP renderer `IntermediateTextureMode` Always→Auto.

**Menu ecosystem (creation-side pause only — invariant-clean):** Menu_Main ships a Cell with 6
BranchingFlora (12,000-volume growth ceiling, 0s intervals) and a ~300-mote cytoplasm field.
Under PerfStrip: cell life spawners stay paused (`CellLifeSpawnerBase.Start`), flora never
plant/grow (`Flora.Initialize`), cytoplasm never instantiates (`SnowChanger.Initialize`), and the
conveyor's `lifeformScenes: 0`. Nothing existing is culled or decayed — mass conservation intact.

**Menu UI:** `ScreenSwitcher` deactivates all non-HOME screens at Awake (their hidden per-frame
tickers — DailyRewardCard's every-frame `DateTime`+`string.Format`+TMP rebuild, QuestTrackView
parallax, InfiniteScroll, Pulse tints — never start; navigation already routes around
`disabledScreens`), and once the freestyle transition settles it deactivates HOME + NavBar too
(CanvasGroup alpha=0 does NOT stop Updates/TMP/canvas rebuilds). Everything restores on exit.
Gamepad arcade/settings modal shortcuts are gated.

**Conveyor smoothing (feel preserved: 5×40 worlds, 510m stream):** populate 1 prism/frame;
`RearrangeInto` amortized to 5 prisms/frame across ~8 frames while the scene sits suctioned at
~zero scale (was a 2–8ms single-frame spike on every recycle); `MaxConcurrentArrivals` 3→2;
mid-transition spatial-index notifies every 3rd frame; `FadeIn` material double-clone leak fixed.

**Micro:** `VesselTransformer.DecayBoost` only raises the SOAP boost event when the value actually
changes (was every frame at rest, fanning out HUD + audio listeners).

**Known remaining (next dials, in order):** renderScale 0.8→0.65; skimmer trigger layer-masking
(put crystals on a dedicated layer so prism OnTriggerStay pairs vanish — needs editor layer setup);
FMOD RuntimeManager tick (~0.3–1ms, banks don't load anyway); netcode netvar writes per frame
(solo host, nothing sent).

## Conveyor mode (cell power-down + breadcrumb trail home)

While the Wanderway belt flows (all PerfStrip-gated, in `ConveyorToy`):

- **The menu Cell powers down** (`SetActive(false)` — membrane/nucleus stop rendering and ticking)
  and powers back on when the belt stops. A direct perf win while flying the belt.
- **The vessel lays a breadcrumb trail home**, capped at **300 prisms**
  (`PerfStrip.ConveyorBreadcrumbMaxPrisms`; cap explicitly authorized by the design owner for this
  branch). Past the cap the OLDEST prism is consumed via the sanctioned `Prism.Consume` path — a
  visible implode into the toy switch (continuity honored, never a silent despawn).
- **The toy switch rides the trail's tail** (`ConveyorToy.Tick`, 4Hz): follow your own trail
  backward and you always reach the switch. Toggling it stops the belt, stops the trail (what's
  laid stays — conserved), re-lights the cell, and re-homes the toy beside it (regrow bloom).

## Bleeding-edge conveyor parity (merged 2026-07-07)

`origin/bleeding-edge` was merged in: the Wanderway now has the **hybrid ribbon path** (bends with
gentle turns, breaks + re-lays on sharp ones), the **unified spawning primitives**
(`PrismTrailBuilder`/`PrismGeometry`/`PrismKinds`), **palette theming + expanded recipe diversity**,
and bleeding-edge's density — `poolSize 8 × prismBudget 100` (800 belt prisms), `aheadTargetScenes 4`.
The strip's smoothing was re-applied onto the new code: populate lays **1 prism/frame** under
PerfStrip, re-poses are amortized (5/frame while suctioned), `MaxConcurrentArrivals = 2`.
If 800 + ≤300 breadcrumb prisms proves too heavy on device, `prismBudgetPerScene` is still dial #1.

## Static skybox (bake once in the editor)

Run **FrogletTools ▸ Bake Static HyperSea Skybox** once (and after any skybox shader change): it
renders the procedural HyperSea sky into a 512px/face cubemap and saves a `Skybox/Cubemap` material
at `Assets/Resources/StaticHyperSeaSkybox.mat`. `PerfStripRuntime` picks it up automatically —
the full vaporwave sky at **one texture sample per pixel**. Until it's baked (or if the bake fails
on this pipeline — the tool logs a Reflection-Probe fallback recipe), the build uses the solid
deep-space clear.

## Skim Race (HexRace) — enabled on the strip

"Skim Race" is the player-facing name of **HexRace** (`GameModes.HexRace = 33`,
`MinigameHexRace.unity` — re-enabled in the build list). Launch it from the **arcade modal on
HOME** (touch button, or gamepad **South** — the pad shortcuts were re-opened). The whole flow is
offline-clean: launch is a plain Netcode scene load on the local host (no UGS session is created),
and `UGSStatsManager` no-ops when not signed in.

**The trail is the mechanic here**, so the strip's trail kill is lifted for the race via the shared
capped-trail mode (`PerfStrip.CappedTrailActive`/`CappedTrailLimit`, set by `HexRaceController`
Awake/OnDestroy): every vessel lays its trail, capped at **2,000 prisms**. Sizing: the track is
~4,000u per circuit and the Squirrel lays a prism every 5–7u ⇒ ~600–800 prisms per lap — so 2,000
guarantees **at least two full laps of skimmable trail** after lap one (typically ~3). Past the
cap the oldest prism implodes in place via `Prism.Consume` (visible transition, never a silent
despawn). The conveyor's breadcrumb uses the same mechanism at limit 300 with the toy as anchor.

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

## Follow-up fixes — throttle audio buzz + dim crystals

Two regressions surfaced in play-testing the stripped build:

1. **Throttle "crashing sound"** — `ProximityBoostAudioController.FireTickOneShot()` fired the
   Skim one-shot (`event:/SFX/Oneshots/Gameplay sfx/Skim`) on *every* rising edge of
   `BoostMultiplier`, with no minimum interval. In the dense continuous skimming of conveyor /
   skim-race the boost pins at max, where per-frame decay (`VesselTransformer.DecayBoost`) + a
   per-frame skimmer-prism contact (`SkimmerBoostPrismEffectSO.Execute` adds `0.1` > `tickEpsilon`
   `0.02`) makes **every frame** a fresh rising edge. The one-shot machine-gunned at frame rate
   (60–200/sec) and fused into a harsh buzz that masked the engine loop — worse the harder you
   throttle (higher speed sustains the continuous-skim state). Fix: added a serialized
   `minTickInterval` (default **0.07 s** ≈ 14 clicks/sec, unscaled-time gated) that rate-limits the
   one-shot only. The boost gameplay and the (unused-on-Squirrel) loop layer are untouched.
   File: `_Scripts/Controller/FX/ProximityBoostAudioController.cs`.

2. **Crystals too dim (no bloom)** — the crystal shader (ShepardGraph) fresnel-blends
   `_BrightCrystalColor` → `_DullCrystalColor`. Gameplay authored these to be lifted by HDR + the
   gameplay Bloom override (threshold 2.5, needs HDR); the perf strip turns HDR **and**
   post-processing off, so the raw LDR colors show through and read dark (the menu/omni crystal was
   dark green `0.048,0.265,0`). Re-enabling HDR + full post would reverse the three biggest perf
   wins, so instead the crystals' intrinsic LDR brightness was raised: for each of the 23 crystal
   materials, the displayed (HDR-clamped) color is scaled so its brightest channel reaches a target
   (**bright → 1.0, dull → 0.85**), hue and alpha preserved, and **never darkened** — HDR-authored
   cores (Time/Charge/Exploding/ActiveTime, channels already >1) are left as-is; only dim LDR
   crystals brighten. Perf-free (no HDR, no bloom pass). Flora/fauna spindle materials that share
   the shader (`*Fringe*`, `*Spindle*`, `Inverse*`) were excluded.
   Files: `_Graphics/Materials/CrystalMaterials/*.mat`, `ShieldedCrystalMaterial.mat`,
   `ActiveSpaceCrystalMaterial.mat`, `Graphs/TimeCrystalGraph.mat`.

   *If the actual glow halo is wanted back (not just brightness), the cheapest path is a bloom-only
   pass with HDR left off: keep `renderPostProcessing` on in `PerfStripRuntime`, set the Bloom
   override `threshold < 1` (so LDR near-white crystals bloom) on the GamePlay + MainMenu profiles.
   That costs one post pass — deferred in favor of the perf-free brightness lift.*

## Round 2 — haptics on-device + residual skim-buzz

**No haptics at all.** The full wiring chain is actually correct: `SkimmerHapticsByPrismEffect`
(type 2) is on the Squirrel's *skimmer* prism-effect list (the same list as the boost effect that
produces the audible skim click, so it provably executes), all haptic effect assets deserialize to
valid types, `GameSetting` resolves (audio proves the same PlayerPref path), the `!AutoPilotEnabled`
gate passes in freestyle, the arm64 `liblofelt_sdk.so` ships in `LofeltHaptics.aar`, and that AAR
declares `android.permission.VIBRATE`. The break is the **Lofelt runtime**: `HapticPatterns.PlayPreset`
only reaches the motor when the device meets Lofelt's "advanced requirements" (amplitude-controlled
haptics) or its version-supported fallback fires — on a mid/older phone that can silently no-op, and
in the Editor nothing vibrates at all.

Fix: added `AndroidHaptics` (`_Scripts/Controller/IO/AndroidHaptics.cs`) — a direct
`android.os.Vibrator` JNI path (VibratorManager on API 31+, one-shot `VibrationEffect` on 26+,
legacy `vibrate(ms)` below), fully try/caught so failure is a silent no-op. `HapticController` now
routes on-device Android through it (short amplitude pulses per `HapticType`, scaled by the strength
slider) and keeps the NiceVibrations path for Editor/iOS. Also added a **per-type rate limit** (0.08 s)
so the per-contact skim/crystal effects can't machine-gun the motor into a solid buzz. **Haptics only
fire on a physical device — the Editor has no vibration motor.**

**Residual "weird audio" (delayed onset).** The 0.07 s tick cap thinned the machine-gun but the buzz
still onset after a few seconds of skimming: boost *climbs* to its ceiling over those seconds, then
sits pinned at max where per-frame decay + a per-frame skim keep re-crossing the rising threshold, so
the tick sustained a ~14/sec buzz. Added `maxTickNormalized` (0.9) to `ProximityBoostAudioController`
— the skim click fires only while boost is genuinely *climbing* (below 90% of max) and goes silent
while you *hold* the cap. Clicks on engage, quiet at full boost.

Files: `_Scripts/Controller/IO/HapticController.cs`, `AndroidHaptics.cs`,
`_Scripts/Controller/FX/ProximityBoostAudioController.cs`.

## Round 3 — course-correct onto upstream (548-commit resync)

Upstream independently shipped platform versions of three of this branch's workarounds, so the
merge RETIRES the strip's copies in their favor:

1. **Haptics → the two-feel policy** (`Docs/HAPTICS.md`). Upstream rewrote `HapticController`:
   `PlaySkim` (proximity-scaled reward pulse train, driven by the same `SkimmerHapticsByPrismEffect`
   already on the Squirrel's skimmer container) + `PlayPunish` (prism-hit thud), priority/rate-limit
   gated, runtime `.haptic` clip + gamepad rumble generation; every legacy `PlayHaptic`/`PlayConstant`
   call site is now a deliberate no-op. The strip's `AndroidHaptics` JNI bypass and `HapticController`
   rework are **deleted** — superseded. Skim + prism haptics need zero strip wiring now. Note the
   policy deliberately silences crystal-collection haptics (the collect has its audio beat instead);
   the strip follows the platform policy. If the device is STILL silent, debug inside the two-feel
   system (`Docs/HAPTICS.md` has in-editor verification) — do not resurrect the JNI path first.
2. **Conveyor breadcrumb → the Wanderway rolling tether.** Upstream's `WanderwayRun` is the
   sanctioned version of the breadcrumb design: `tetherPrisms: 100` rolling ribbon (sole authorized
   mass-conservation exception, `Docs/ECOSYSTEM.md §0`), the return station riding the tail, and
   `revertCellOnStart: 1` (bare-canvas cell swap — replaces the strip's Cell `SetActive(false)`
   hack). `ConveyorToy`/`Microscene` taken from upstream wholesale (the C8 clock-driven recycle also
   obsoletes the strip's CPU amortization); the strip's breadcrumb mode, `BreadcrumbAnchor`,
   `TryGetBreadcrumbTail`, `Toy.Tick()` riding, and `ConveyorBreadcrumbPrisms` are **removed**.
   The **capped trail survives for Skim Race only** (`PerfStrip.CappedTrailActive` +
   `SkimRaceTrailPrisms 2000`, ≥2 laps, set by `HexRaceController`).
3. **Offline boot → `OfflineModeService`** (`Docs/OFFLINE_MODE.md`, CORE IMPLEMENTED upstream).
   The strip's local-host bypass in `AuthenticationSceneController` and the `PlayerDataService`
   early-return are **replaced**: `PerfStrip.OfflineMode` now simply ORs into upstream's
   `offlinePreferred`, which skips the Relay attempts and starts the sanctioned 127.0.0.1 offline
   session with the `LocalCloudDataCache` profile.

**Belt re-tuned for mobile** (upstream authored a desktop-scale stock): `Toy_Conveyor.asset`
poolSize 20→8, prismBudgetPerScene 1500→150 (30,000 → 1,200 resident prisms), aheadTargetScenes
5→4, maxCrystalsPerScene 6→2, lifeformScenes 1→0. This remains tuning dial #1.

Also carried through the merge: the corrected SFX bank ("music fixed", SFX.bank 8.8→39.5 MB —
likely relevant to the reported broken audio), the `ExplodingBlockGraph` transparent/alpha-clip fix,
microscene scene-envelope bounds, the one-thumb/mouse input overhaul, and the game-mode top bar
redesign. The strip's gates all survived: `ConveyorOnlyToybox` (new painting/cell-selector/lifeform
toys auto-excluded), `MenuUIStripped`, graphics/frame-cap gates, minify/ARM64/proguard settings,
crystal LDR brightening (except `ChargeCrystalMaterial`, which upstream moved to its own
plasma-discharge shader).

## Round 4 — juicing skim race back toward the PC feel

Three things the strip removed were specifically the ones that sell a RACE. Two are restored here.

**1. Gameplay post-processing is back (bloom + the speed tunnel's other half).**
`PerfStripRuntime` was disabling `renderPostProcessing` on every camera in every scene, on a comment
asserting the overrides were all neutral. That is true of the *MainMenu* profile, and false of the
one that actually runs: a single persistent Volume rides the Bootstrap `PostProcessingManager` as
DontDestroyOnLoad (the gameplay scenes contain no Volume of their own) carrying the **GamePlay**
profile, whose two active overrides are **Bloom** and **PaniniProjection**.

Losing them cost more than a look. `Docs/SPEED_TUNNEL.md` is a platform law, and its FOV half is a
direct `Camera.fieldOfView` write that survived — but `PostProcessingManager.SetSpeedTunnelPanini`
drives a Volume override, so **half the speed tunnel had been silently amputated while the law still
appeared intact**: a race kept the dolly-zoom and lost the bend that reads as speed.

The bloom is affordable, and that is measured rather than hoped — **`threshold 0.2` with
`clamp 0.5`** means it needs no HDR at all (it never reads above the LDR range it is clamped into,
which is also why the earlier "bloom needs HDR, threshold 2.5" note in this doc was wrong — that
was a misread of the YAML field ORDER), and **`maxIterations 4` / `skipIterations 6`** is an
already-cheap, low-resolution pyramid. HDR stays OFF; nothing about the URP asset changed.

The gate is the **scene**, not the profile — that one persistent Volume is equally "active" in the
menu, where the lava lamp / conveyor would pay the UberPost blit and the 32³ colour-grading LUT for
a look the strip deliberately traded away. `PerfStripRuntime.IsGameplayScene()` probes for the
scene's `MiniGameControllerBase` (exactly one per gameplay scene, none elsewhere — the same
self-resolving idiom `Docs/GAMECANVAS.md` uses) so a new mode gets its authored look with nothing to
register. Kill switch: **`PerfStrip.AllowAuthoredPostProcessing = false`** reclaims the blit + LUT.
This is now the one dial that trades the race's look for frame time; it belongs beside render scale
in the tuning list above.

Synergy worth noting: the crystals brightened in the earlier round now sit well above `threshold
0.2`, so they bloom properly instead of merely being lighter.

**2. The Squirrel's skim visual is no longer drawn twice.** `SkimmerFXPrismEffectSO` is
`[Obsolete("Replaced by SkimmerForcefieldCracklePrismEffectSO")]`, and CLAUDE.md records that a
container holding BOTH draws a beam to every prism in the sphere on top of the crackle — with the
Dolphin already converted and the Squirrel named as the open item. Removed the beam from
`SquirrelSkimmerImpactorDataContainer`; the crackle is now the sole skim visual, matching the
Dolphin. Slightly cheaper too (the beam was per-skimmed-prism VFX).

**A false alarm worth recording, because the next person will hit it.** Grepping
`Squirrel.prefab` for `ForcefieldCrackleController`'s guid returns **0**, which reads as "the skim
crackle is dead on the race vessel". It is not: the Squirrel *nests* `Skimmer.prefab`, which carries
the controller and its `ForcefieldCrackleOverlay` renderer, and **a nested prefab instance never
lists its source's component guids in the parent asset** (`Docs/VESSEL_CONSTRUCTION.md` records this
class of false positive/negative). Resolve a component question on a vessel by checking
`m_SourcePrefab` guids for nested instances BEFORE concluding anything from a guid grep.

**Still open (next pass):** `SquirrelSkimmerImpactorDataContainer.skimmerCrystalEffectsSO` is empty,
so skimming a crystal produces no skimmer-side feedback at all; and the in-race HUD (elemental
petal bars / boost gauge) has not been checked against the strip's UI teardown.

## Round 5 — why round 4 did not actually land, and the skybox

Round 4 restored gameplay post-processing in code and **nothing changed on screen**. Two separate
causes, both now fixed in `PerfStripRuntime`.

**1. The pass could not see the camera that renders the game.** `Camera.allCameras` returns only
ENABLED cameras — and the gameplay scenes contain **no camera at all**. `CameraManager` owns a
persistent set in Bootstrap (`CM PlayerCam` / `Camera` / `CM EndCam` / `CM DeathCam`, every one
authored `m_RenderPostProcessing: 1`) and enables one at a time. So the menu pass switched post off
on whichever camera was live then, and the gameplay pass could not switch it back on for a camera
that was still inactive. The strip was the only thing ever disabling post, so that one miss was the
whole bug. Now enumerated with `FindObjectsByType<Camera>(FindObjectsInactive.Include, …)`.

**2. One pass at `sceneLoaded` is too early.** The decisions depend on objects that do not exist
yet — the vessel's camera arrives after `preSpawnDelayMs`, the cell after `InitDelayMs` (~1 s). A
hidden DontDestroyOnLoad host (`PerfStripRuntimeHost`) now re-runs the pass a few times over the
first ~3 s of each scene. General shape: *a one-shot decision at scene load cannot describe a scene
that is still assembling itself.*

**The skybox is back, and it is now static without an editor step.** The strip cleared
`RenderSettings.skybox` and fell back to a solid colour whenever `Resources/StaticHyperSeaSkybox`
was absent — and it was absent, because it only exists if a human runs
FrogletTools ▸ Bake Static HyperSea Skybox. That shipped a black void. Now the authored sky is
**baked to a cubemap at runtime, once per authored material**, and the sky is **never cleared** —
a failed bake keeps the procedural sky (correct look, full cost) instead of deleting it.

The bake is cheap by construction: six 256 px faces is ~0.4 MP, i.e. *less* pixel work than a single
1080p frame of that 767-line shader, paid once. Afterwards the sky is one texture sample per pixel.
Keyed **per authored material** because the strip walks scenes with different skies — Bootstrap is
`BlackSkybox`, menu and gameplay are the procedural `HyperSeaSkybox` — and Bootstrap loads first, so
a single cached bake would have pinned the black one onto every later scene.

`Assets/Editor/BakeStaticSkybox.cs` is now redundant for shipping and is kept only as the way to
produce a *committed* cubemap asset if the runtime bake ever proves unreliable on a device.

## Round 5b — one-thumb flight for two-stick hulls

A two-stick hull (the Squirrel) flown with a SINGLE thumb now mirrors that thumb onto both virtual
sticks. This is the state the vessel enters the moment a thumb is **lifted to trigger an ability** —
drift is literally a `2+ → 1` touch transition (`HandleDriftTransitions`) — so it is the normal way
the mode is reached, not an edge case.

It is not a special case bolted onto the mix; it falls out of the existing one. `DualStickMix` is
`XSum = yaw`, `YSum = pitch`, `XDiff = throttle`, `YDiff = roll`, all over `right ± left`, so
mirroring (`left = right = s`) yields exactly the requested mode:

| term | mirrored | effect |
|---|---|---|
| `XDiff` | `(s.x − s.x + 2)/4` = **0.5** | throttle pinned neutral |
| `YDiff` | `Ease(s.y − s.y)` = **0** | no roll |
| `XSum` / `YSum` | `Ease(2s)` | pitch + yaw at **full** authority |

Flying one-thumbed previously did the opposite of all three, because the idle stick is lerped toward
zero and that decaying value was still read as real input: `XDiff` drifted with sideways thumb travel
(**a turn silently changed SPEED**), `YDiff` picked up **roll** from vertical travel, and pitch/yaw
ran at `Ease(s)` — about **0.29** of full authority. So "faster turning, pitch and yaw only" is one
change, and the speed-up is inherent (0.29 → 1.0) rather than a tuned multiplier;
`OneThumbTurnBoost` exists at 1.0 if it still reads sluggish.

> **Corrected in Round 9.** The `0.29` above is `BaseInputStrategy`'s **gamepad cosine**, not the
> curve this class actually runs — `TouchInputStrategy` overrides `Ease` and gives `Ease(1) =
> 0.4625`, so the mirror is a **2.162×** speed-up, not 3.4×. And "drift is unaffected" was wrong in
> the direction that mattered: see Round 9.

Drift is unaffected: its `XDiff = 1.0` full-throttle override is applied *after* `Reparameterize`.
Applied for every touch hull, not gated to two-stick ones — a one-thumb hull reads only
`EasedLeftJoystickPosition`, so under the old code touching the RIGHT side of the screen gave it
nothing, and mirroring fixes that too.

## Round 6 — dialled back on measurement: post-processing stays, the skybox goes

Round 5 cost frames. Post-processing is kept (it is the race's feel); the two things that made the
build slow are reverted. **Round 5's skybox section is superseded by this one.**

**1. The runtime skybox bake is REVERTED.** Restoring the sky was a regression in both of its
states, which is the part I got wrong: a *successful* bake still costs a full-screen sample plus
background overdraw that the solid deep-space clear does not, and a *failed* bake was far worse —
it deliberately kept the authored sky, which is the 767-line procedural HyperSea shader at full
per-pixel price on every frame. Since `Resources/StaticHyperSeaSkybox` was never committed, the
failure path was the only path the device could take.

The strip is back to killing the skybox. `PerfStripRuntime` still *loads* a pre-baked material if
one exists, so the honest way to have the sky back is to bake it **offline**
(FrogletTools ▸ Bake Static HyperSea Skybox) and commit the asset — then the cost is one texture
sample and never a shader. General rule: **a fallback that "keeps the correct look" is not a safe
fallback when the correct look is the thing you stripped for performance.**

**2. Post-processing is now granted to ONE camera class, not every camera.** `ApplyPostProcessing`
set `renderPostProcessing` on every camera it could find. That is wrong for any camera that renders
somewhere other than the screen — **the Squirrel nests a `PipCamera` drawing into a RenderTexture**
— so the build ran the whole post chain (bloom pyramid + UberPost + LUT) more than once per frame.
The flag is now granted only to a **Base** camera with no `targetTexture`, and explicitly cleared on
everything else (URP runs post once on the base of a stack, never per overlay).

Net: gameplay keeps bloom and the speed tunnel's Panini; the second post stack and the skybox are
gone. Still true from Round 5: the deferred passes and inactive-camera enumeration are what make the
grant reach the camera that actually renders, and they are one-time per scene, not per-frame.

Kept deliberately (zero frame cost, and separately requested): **one-thumb flight** (Round 5b) and
the crystal LDR brightening. The skim-beam removal is itself a small perf win and also stays.

## Round 7 — FXAA: the mobile bang-for-buck anti-aliasing pick

Jaggy prism edges got no anti-aliasing at all. Added **FXAA (Fast Approximate Anti-Aliasing), Low
quality**, on the same presenting camera `PerfStripRuntime` already scopes post-processing to.

**Why FXAA over the other two URP options, for this exact build:**

- **MSAA** (`URP_Asset.m_MSAA`, currently off) needs a multisampled render target and an explicit
  resolve. On tile-based mobile GPUs that is bandwidth *per sample* across the whole frame — not a
  fixed one-time cost the way FXAA's single full-screen pass is. And with render scale at 0.8 (an
  upscale blit already in the pipeline), MSAA would be smoothing edges in a buffer the final blit
  immediately resamples anyway. Left off.
- **TAA** needs per-object motion vectors and a history buffer it re-projects every frame — real
  extra GPU/bandwidth cost on top of FXAA's, and it specifically ghosts on thin, fast-moving
  geometry, which is exactly what a trail of prisms is. Not used.
- **SMAA** (URP's other cheap option) looks better than FXAA but is multi-pass (edge detection,
  blend-weight calculation, neighbourhood blend) against FXAA's one.

FXAA is also architecturally **independent of the Volume-driven post stack** it reads no
`VolumeProfile`; URP applies it in its own final blit — so unlike Bloom/Panini it is **not** gated
to gameplay scenes: it costs nothing extra with post-processing off, and one pass everywhere it's
on, including the menu / conveyor. It's also a good match for this project's prism transparency,
which is dithered alpha-clip rather than real blending (`Docs/PRISM_ANIMATION.md §4.7`) — that
dithering produces exactly the sub-pixel-noisy edges FXAA's edge-detect blur was built to soften.

`AntialiasingQuality` (Low/Medium/High) is a URP field read only by SMAA — inert for FXAA, set
anyway so a future bump to SMAA starts at the cheap tier rather than URP's Medium default.

Refactored the "is this the camera that actually presents to the screen" check (no `targetTexture`,
`renderType == Base`) into one shared `PresentsToScreen` helper used by both
`ApplyPostProcessing` and the new `ApplyAntiAliasing` — Round 6's bug was exactly two copies of that
condition disagreeing (post-processing was granted to the Squirrel's RenderTexture-targeted
`PipCamera` too), and a shared helper is what keeps it from happening a second time.

Kill switch: `PerfStrip.AllowAntiAliasing = false`.

**Not verified in-Editor.** This sandbox has no Unity Editor / package cache to compile against —
`UniversalAdditionalCameraData.antialiasing` / `.antialiasingQuality` and the `AntialiasingMode.
FastApproximateAntialiasing` / `AntialiasingQuality.Low` enum members are long-stable, unchanged
public URP API since package v7 through the pinned 17.0.4, but this still needs a real compile pass
in your next Editor session before it ships.

## Round 8 — 4x MSAA (correcting Round 7's reasoning about it)

FXAA alone left the build "still very aliased", which is the expected outcome and my Round 7
reasoning for skipping MSAA was wrong. Recording the correction, because it is the useful part:

**I dismissed MSAA with desktop immediate-mode-renderer logic** — "bandwidth per sample across the
whole frame". That is not how it works on the tile-based GPUs this build targets. On a mobile tiler
the framebuffer tile is held **on-chip** at N samples and resolves to single-sample when the tile is
written out, so the extra traffic to system memory is **zero**; the cost is tile memory and a little
extra edge rasterisation. Unity's, ARM's and Qualcomm's mobile guidance all recommend 4x MSAA on
mobile forward rendering for this reason. **MSAA is the cheap option on mobile and the expensive one
on desktop — the intuition inverts, and I applied the desktop one.**

FXAA was also the wrong *tool* for this content independently of cost: it is a post-process
heuristic that infers edges from a finished image, and it is weakest on exactly what fills this
screen — thousands of thin, high-contrast prism silhouettes. MSAA solves those directly, because
they are real geometry edges with real coverage.

**This pipeline is configured about as well for cheap MSAA as it gets**, which is why the change is
one number:

| setting | value | why it matters for MSAA |
|---|---|---|
| `m_RenderingMode` | `0` (Forward) | MSAA is a forward-rendering feature |
| `m_RequireDepthTexture` | `0` | no MSAA **depth resolve** — the usual hidden cost |
| `m_RequireOpaqueTexture` | `0` | no extra resolve/copy of the colour buffer |
| `m_DepthPrimingMode` | `0` (Disabled) | depth priming + MSAA is the bad combination |
| `m_SupportsHDR` | `0` | 32bpp colour, so 4x samples fit tile memory comfortably |

That last row is why **4x** rather than 2x: with HDR off the tile budget is not under pressure, and
on a tiler the 2x→4x delta is small.

`URP_Asset.m_MSAA: 1 → 4`. It sticks because `GraphicsSettingsApplier` — the only thing that writes
`urp.msaaSampleCount` at runtime — is strip-gated and early-returns, so the authored value is what
ships.

**FXAA is kept on** alongside it: MSAA antialiases geometry coverage only, and it cannot touch the
two aliasing sources this project creates in shaders — the dithered alpha-clip prism transparency
(`Docs/PRISM_ANIMATION.md §4.7`, whose screen-door edges are all-or-nothing per fragment) and bright
fresnel rims. If the combination now reads soft rather than jaggy, drop FXAA first
(`PerfStrip.AllowAntiAliasing = false`) and keep MSAA — that is the better of the two for this
content.

**The remaining aliasing lever, deliberately not pulled: `m_RenderScale: 0.8`.** Rendering at 80%
and bilinear-upscaling both discards samples and re-introduces stair-stepping *after* AA has run, so
it works against everything above. Raising it to 1.0 is the single most effective anti-aliasing
change available — and costs **+56% fragment work**, which is not "cheap" and is the opposite of the
Round 6 dial-back. Left at 0.8 on purpose. If MSAA is not enough, the honest options in cost order
are: render scale 0.9 (+27% pixels), FSR upscaling instead of bilinear
(`m_UpscalingFilter`, edge-aware reconstruction, ~one extra pass), then render scale 1.0.


## Round 9 — the one-thumb drift was a brake (reported: "doesn't feel right")

Round 5b's mirror was correct and Round 5b's claim that "drift is unaffected" was not. Two
multipliers were stacking, and the result is not a feel preference — past a certain slip angle the
Squirrel's drift **subtracts speed**, which is the opposite of what a racing drift is for.

### The mechanism

`VesselTransformer` runs the vector flight model in three steps: grip slerps the velocity
*direction* toward the nose, thrust is added **along the nose**, then `ShapeSpeed` bounds the gain.
Step 2 is the trap:

```csharp
float along = Vector3.Dot(_velocity, transform.forward);
return StepTowardTarget(along, ComputeThrottleTarget(), dt) - along;   // added along +forward
```

Once **slip** — the angle between the velocity and the nose — passes **90°**, the velocity's forward
component is negative, so a delta added along `+forward` is *shortening* the velocity. Nothing logs
it, nothing clamps it; it is only ever felt, as a drift that washes off speed.

Slip is driven by commanded yaw against grip, and touch was feeding it two independent
over-multiplications:

1. **The touch override bound the SHARP tier.** `Squirrel.prefab._touchActionOverrides` for the
   one-thumb drift event listed `SquirrelSharpDriftAction` **and** `SquirrelDriftAction`. Both run,
   both call `BeginDrift`, and `GetTriggerSum`'s non-gamepad branch is binary and prefers sharp
   (`if (_sharpDriftActive) return 2f`) — so touch always got Mult **1.8** / Grip **0.25**, the tier
   the gamepad only reaches by burying the trigger. The gamepad path picks its tier from analog
   travel and was always fine.
2. **The mirror's 2.162× stacked on top of that 1.8.** Each was calibrated as if it were the only
   multiplier. Commanded yaw at full deflection: `120 × 1.8 × Ease(2) = 216 °/s`.

### Measured

`Tools/Build/touch_drift_slip.py` transcribes the shipped vector path and reads every input from the
shipped files (the gain from `TouchInputStrategy.cs`, `Mult`/`driftDamping` from whichever drift
assets the **touch** override actually binds, `YawScaler` from the prefab), so a retune of any one of
them is checked rather than assumed. A held, full-deflection one-thumb drift:

| | commanded yaw | peak slip | speed carried (2 s) |
|---|---|---|---|
| **before** (sharp tier + full mirror) | 216 °/s | **132°** | **46%** |
| prefab fix alone (single tier, full mirror) | 168 °/s | 105° | 72% |
| gain fix alone (sharp tier, gain 0.70) | 143 °/s | 106° | 73% |
| **after** (both) | 112 °/s | **86°** | **125%** |

("Speed carried" is end ÷ start over a two-second held drift; it is invariant in throttle, so the
rows compare directly even though the old code pinned `XDiff = 1.0` and the new one holds it.)

Either half alone still crosses 90° and still brakes — **both are load-bearing**. Together the drift
never crosses the sign change and now *gains* speed through the corner, which is what it was for.

### What changed

- **`Squirrel.prefab`** — dropped `SquirrelSharpDriftAction` from the **touch** override only. The
  gamepad override (`InputEvent: 2`) is untouched and still spans both tiers on trigger travel.
- **`OneThumbDriftTurnGain = 0.70`** — applied to the **mix only** while one thumb is flying
  *because a thumb was lifted to fire an ability*. Lands the mirrored thumb on `Ease(1.4) = 0.6643`
  → **111.6 °/s**, still faster than the **99.9 °/s** one thumb produced before the mirror existed.
  It is a **calibration**: `--sweep` prints the cliff (0.8 → 94° and already losing speed).
- **The mix is now split from the fan-out.** `EasedLeft/RightJoystickPosition` and the normalized
  pair keep the **full** mirrored thumb; only `XSum/YSum/XDiff/YDiff` take the gain. Reducing both
  would have silently moved every `|stick| ≥ 1` ability perimeter inward — a one-thumb pilot could
  no longer reach the rim.
- **Throttle is held, not pinned.** Round 5b pinned `XDiff = 1.0` on any one-thumb ability. That was
  an unasked-for full-throttle lurch on drift entry; the mirror's structural `XDiff = 0.5` would
  have been a silent halving. Neither is what the pilot asked for, so the throttle they had when
  they lifted the thumb is replayed (`heldXDiff`).
- **The write-only `isDrifting` flag is retired.** It was set on **both** single-thumb transitions,
  so it never meant "a drift is running" — only "one touch remains". On the Squirrel the two really
  differ: a lifted **right** thumb raises `OnlyLeftStickAction` (12) → drift, a lifted **left** thumb
  raises `OnlyRightStickAction` (11) → the tube ability. The old flag therefore pinned full throttle
  for an ability that is not a drift. `OneThumbAbilityActive` replaces it and says only what is true.

**Roll stays at zero during one-thumb flight.** A yaw-coupled bank was considered and dropped:
`Roll()` applies a rotation **rate** about forward, so coupling it to yaw would corkscrew at up to
`RollScaler 130 × 1.4 = 182 °/s` rather than settle into a bank — and "control just pitch and yaw"
is the mode as specified.

### Not verified in the editor

No Unity play-mode run. The numbers above are from the transcribed model, not from the game. The
gain is the one value expected to need a pass on device: **lower toward 0.5 if a held drift still
washes speed off, raise toward 0.8 if it reads sluggish** — and re-run
`python3 Tools/Build/touch_drift_slip.py --check --sweep`, which fails on anything that crosses 90°.
