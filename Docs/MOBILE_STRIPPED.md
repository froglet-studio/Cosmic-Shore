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
| 5 | **Toybox is conveyor-only** (since Round 18: the light toys, see `PerfStrip.LightToysOnly`) | `ToyboxController.PlaceToys()` filters to the light toys (`WanderToyDefinitionSO`, offering Without Ark only, plus domain changer / element charger / vessel changer) on `PerfStrip.LightToysOnly` | Drops the other 3 toys' idle cost — notably the vessel-changer's 6 mini-ship preview models. Squirrel stays the only vessel. |
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

## Static skybox (bake once in the editor) *(superseded by Round 17: baked offline, no editor)*

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

### Round 9 addendum — the resync put the brake back (2026-09-29)

The 1104-commit resync brought upstream's **one-action drift** (2026-09-23): the Squirrel no longer
binds `SquirrelSharpDriftAction` anywhere, and `SquirrelDriftAction` was retuned to the old sharp
values (×1.8 / grip 0.25) at FULL trigger pull, with the old ×1.4 / 0.5 sitting at about half pull.
On a pad that is strictly better — the trigger feathers across the whole range. On **touch** it
silently re-created exactly the defect Round 9 fixed, because a thumb-lift is binary and binary
means full pull: 106° peak slip, 73% of speed carried. `touch_drift_slip.py --check` caught it on
the first run after the merge — which is the reason it reads the shipped assets rather than a copy
of the numbers.

The fix could no longer live in the asset (the pad owns it now), so it moved to the one place that
knows the drift has no depth: `VesselTransformer.touchDriftDepth` (default 1, fleet unchanged; the
Squirrel authors **0.5**). A touch drift with no sharp tier bound reads that depth instead of a
full pull, landing on ×1.4 / grip 0.625 — the same 111.6 °/s as Round 9 with slightly MORE grip,
peak slip **81°**, 125% of speed carried. The gate is negative-controlled: at depth 1 it fails.


## Round 10 — re-evaluating the frame after the 1,104-commit resync (2026-09-29)

The resync brought in a lot of platform work that runs on every frame or every menu entry. Each item
was measured against the three experiences this build exists for (Squirrel freestyle, the
Wanderway, Skim Race). Four were paying for nothing, one was broken, two cheap toys came back.

### Cut or fixed

| # | What | Why | Change |
|---|------|-----|--------|
| 1 | **Wanderway had no way home** | `WanderwayRun`'s return station rides the TAIL of the vessel's own trail (the rolling tether). The strip kills the trail at `StartSpawn`, so the tail never existed and the station never planted — the run could only be left through the overview button. | `PerfStrip.WanderwayTetherActive`: `WanderwayRun.Begin` lifts the trail kill and kicks the spawner, `End` puts it back. The tether is already bounded (100 prisms, recycled into its own pool — fixed memory), so it does not use the Skim Race FIFO. |
| 2 | **Menu_Main booted Garland** | Upstream made `Garland Cell Config` the `BootDefault`: 4,259 laid prisms at every Menu_Main entry (boot and every return from a race) — 3.5x this build's whole Wanderway belt, for a backdrop behind the menu. The run swaps to the bare canvas anyway. | `PerfStrip.BootBareMenuCell`: `Cell.ResolveBootIndex` prefers the bare canvas (Barren) under the strip. Flip it off to get the furnished home screen back. |
| 3 | **Skim Race lost Bloom + Panini after load** | Upstream's load-screen preview (`ConnectingArenaPreview`) mutes the gameplay camera and restores "exactly what it had". It captures the flag while the camera still carries the MENU's value (no post — the menu authors none) and restores that after the strip's deferred passes have granted post for the race — so the race ran without the two effects Round 6 kept it for. | `RestoreGameplayCamera` re-queues the strip's camera passes (`PerfStripRuntime.ScheduleApply`), so the scene decides again. |
| 4 | **Skim Race trail scaled with seats** | The 2,000-prism cap is per VESSEL and the card seats up to 12 with AI backfill: 24,000 live prisms worst case. | One race-wide budget: `PerfStrip.SkimRaceTrailPrismsPerVessel` — 1-3 vessels keep 2,000 each, 4 → 1,500, 6 → 1,000, 8+ → 800 (one lap). Worst case 9,600. Quick Play (solo) is unchanged. |
| 5 | **`SkimRaceController.OnDestroy` hid the base** | The strip's own `void OnDestroy()` (CS0114) hid `NetworkBehaviour.OnDestroy`, so the controller's NetworkVariables never disposed — a native leak per race (`ArcadeConfigSyncManager` records the same finding). | Now `public override` + `base.OnDestroy()`. |
| 6 | **Top-bar glow breathed forever** | `DomainScorePanel` runs an endless DOFade loop per domain column: a UI colour change every frame is a HUD canvas re-batch every frame of the race, for a decoration. | Under the strip the glow rests at its tint and still punches on a score change; only the idle breath is dropped. |

### Brought back (cheap)

`PerfStrip.ConveyorOnlyToybox` became **`LightToysOnly`**: the toybox now also ships the
**domain changer** (two switch rings — repaints your trail and HUD) and the **element charger**
(one station opening into four accent-material crystals — lets a pilot actually reach the
Squirrel's level-5 upgrades). Both are a handful of meshes and no prisms. Still out: the vessel
changer (other hulls are not tuned here), painting (it IS trail), cell selector (34-69k-prism
worlds), spawn matrix (flora/fauna are paused) and Arkway (three satellite cells).

### Measured and kept (free or near-free)

- **Prism shader splices** (occlusion corridor, Lit, cradle): every one early-outs on a UNIFORM
  (`Params.x <= 0 && peerCount <= 0`, `count <= 0`), so they cost a branch per pixel when idle.
  `PrismLit` publishes nothing when no light is live.
- **Vessel vision band, speed tunnel**: a few `SetGlobal*` per frame.
- **The arcade card's preview window**: the looking phase is a SCALE MODEL (one mesh, no prisms);
  shadows are unsupported in `URP_Asset`, and post only follows `Camera.main`, which the menu does
  not grant. Tap-in borrows the gameplay camera rather than adding one.
- **Upstream retired the picture-in-picture camera** (`Pip` is default-off): the Squirrel's
  `PipCamera` no longer renders a second view into a RenderTexture every frame. A free GPU win.
- The belt stays at 1,200 resident prisms against upstream's 30,000.

### Candidates to bring in next (not done — each needs the editor)

1. ~~**Joust**~~ — brought in, Round 11.
2. ~~**The static skybox**~~ — baked offline and shipped, Round 17.
3. **Render scale** — if MSAA + FXAA still read soft/jaggy on device, the next lever is
   `m_RenderScale` 0.8 → 0.9 (+27% pixels) or the FSR upscaler; measure first.

### Not verified in the editor

No Unity here. The eight out-of-editor gates pass; nothing has been compiled against the real
assemblies. Check on device: (1) a Wanderway run plants a return station behind you that you can
fly back into; (2) Menu_Main comes up on an empty cell; (3) a Skim Race shows bloom and the speed
tunnel's bend after the load screen drops; (4) the toybox shows three toys.

## Round 11 — Joust (2026-09-29)

Joust is the second mode on the strip. It earned the slot on cost: it is **Squirrel-only**, its
verb is the Squirrel's own (overtake a slower rival with your skimmer), and its arena is the
**Barren** cell — no authored environment, no flora, no fauna — the cheapest arena in the game.
Its scene's `BigMembraneVariant` skybox model is already switched off
(`disable_scene_skybox_model.py --check`), and it instances the same `CORE/GameCanvas` as Skim
Race, so it inherits the same Bloom + Panini grant.

| # | Change | Where |
|---|--------|-------|
| 1 | Scene enabled in the build (5 scenes now: Bootstrap, Authentication, Menu_Main, Skim Race, Joust) | `ProjectSettings/EditorBuildSettings.asset` |
| 2 | **Capped trail for Joust.** In an empty cell the only thing to skim for speed is the other pilots' ribbon; with the strip's trail kill every Squirrel would cruise at one pace and there is nothing to out-run a rival with. Same mode Skim Race uses: set in `Awake`, sized per seat in `Start`, cleared in an `OnDestroy` OVERRIDE. | `JoustController`, `PerfStrip.JoustTrailPrismsPerVessel` — 2-3 seats 1,200 each, 4 → 1,000, 8 → 500, 10+ → 400; worst case (12 seats) 4,800 live prisms |
| 3 | **The arcade grid only shows modes this build can load.** Before this, the Arcade hub drew every card in the game (~25) and only Skim Race's scene shipped — every other card was a launch that could not load. Now a card is drawn only if `Application.CanStreamedLevelBeLoaded(card.SceneName)`: asked of the BUILD, so enabling a scene is the whole of bringing a card back. The weekly challenge card reads "UNAVAILABLE" when this week's draw is an unbuilt mode. | `ArcadeExploreView.IsLaunchableInThisBuild`, `WeeklyChallengeCard.Redraw`, `PerfStrip.HideUnbuiltModes` |
| 4 | Shared budget helper: both modes size their per-vessel cap as a share of one match-wide budget. | `PerfStrip.CappedTrailPrismsPerVessel` |

**How to reach it:** HOME → Arcade → Joust (the grid now holds Joust and Skim Race). Joust needs
at least 2 players and 2 domains, so a solo launch backfills one AI Squirrel on the other team.

**Known gaps, not fixed here:** the Arena hub now opens an empty grid (no arena mode is in the
build), and the Maelstrom control still points at a scene that is not in the build. Neither is
new — both were dead launches before this round too.

**Not verified in the editor.** Check on device: (1) the Arcade grid shows exactly Joust and Skim
Race; (2) a solo Joust spawns one AI Squirrel on the opposing team; (3) both Squirrels leave a
ribbon you can skim for speed, and overtaking the AI scores a joust; (4) the match ends at the
joust target and the scoreboard's Play Again reloads Joust.

## Round 12 — touch controls, Garland, freestyle trails, the Butterfly (2026-09-29)

### Touch: what a thumb does (shared by every hull — `TouchInputStrategy`)

| Gesture | Before | Now |
|---|---|---|
| Lift one thumb | The other thumb was mirrored onto both sticks AT ITS CURRENT DEFLECTION. Throttle on this mix is the thumbs' horizontal spread, so at cruise both thumbs sit pushed outward — the remaining one read as a hard yaw toward its own side. Lifting a thumb yanked the vessel. | Every change between one and two thumbs **re-zeroes the sticks where the thumbs are.** The vessel keeps flying straight; steering resumes from wherever the thumbs rest. |
| Put the thumb back | Its new touch point was a fresh origin, the other thumb kept its deflection — another yank, and neutral thumbs meant half throttle. | Re-zeroed again, and the throttle you had carries back (`throttleCarry`): neutral thumbs = the speed you had, fading out toward full spread or full squeeze so both ends stay reachable. **Lift, fire the boost ring, put the thumb back = straight through the ring at speed.** |
| Drift (Squirrel) — **superseded by Round 13** | Lift the right thumb. Half the steering gone, the lift itself pulled the vessel, fixed depth. | **Both thumbs hard over into the turn, then past the rim.** "Turn harder than full lock" — the push past the rim is the drift's analog depth (published on `LeftTriggerAnalog`, the pad trigger's channel; DriftAudio follows it). Both thumbs stay down. `PerfStrip.TouchOverdriveDrift`; the Squirrel binds its drift to `BothSticksAction` on touch. |
| First thumb down | Raised Left/RightStickAction — for the Butterfly, that toggled Mass/Dust (right thumb first) or started a Fold (left thumb first) that teleported on the second touch. | Those events fire only when a thumb is alone because the other was **lifted** (`PerfStrip.TouchStickEventsOnLiftOnly`). Which thumb happens to land first is not a decision; lifting one is. |

*(Superseded by Round 13 — the overdrive never drifted on device and is retired.)* Drift depth was capped by the Squirrel's `touchDriftDepth` **0.35**: every overdrive drift is flown
at full yaw, so the ceiling is what keeps a hairpin under 90° of slip (where nose thrust starts
braking). `Tools/Build/touch_drift_slip.py --check` models a 180° hairpin at full overdrive:
87.6° peak slip, 124% speed carried; 0.5 fails it (negative control run). Dial:
`DriftOverdriveRadii` (1 stick radius of combined push = full depth) in `TouchInputStrategy`.

### Garland and trails in freestyle

- **Menu_Main boots Garland again** (Round 10's bare boot removed).
- **Its garden grows.** `PerfStrip.CellLifeRuns`: flora/fauna spawners and flora growth run in the
  cell config that declares itself the home world (`BootDefault` — Garland only). Garland's roster
  is hard-capped (4 phyllotactic flora + 3 fauna, 37 heart colliders, mature ≈ 8,100 prisms incl.
  its 4,259-prism boughs). The races keep their life paused.
- **A Wanderway run gives Garland back when it ends** — the strip ships no Cell Selector, so a
  wander used to leave you in the bare canvas for the session.
- **Freestyle lays trail again — uncapped.** A cap/TTL on the freestyle trail is the rejected cheat
  (CLAUDE.md, "the menu trail cap"), so it is bounded by the two things the law sanctions: the
  **food web** (Garland's fauna graze it — its nucleus exterior is voraciously edible) and a
  **spawner that waits** (`PerfStrip.FreestyleCellPrismBudget` 10,000 live prisms in the cell; the
  pen lifts at the budget and comes back down at 9,700 once grazing has made room). Nothing is
  removed to make room. Mature Garland leaves ~1,900 prisms of trail — about two minutes of flight
  before the pen waits on the fauna. The menu's autopilot lava lamp lays no trail; the flag is set
  on freestyle enter/exit and reset on every scene load.

### The Butterfly and Waystation

- **The vessel changer ships** (`LightToysOnly`), its roster narrowed by `PerfStrip.ShipsVessel` to
  the Squirrel and the Butterfly — fly it to swap hulls.
- **Butterfly on glass**: both abilities are binary, and both land on a thumb lift — **lift the left
  thumb** to toggle Mass/Dust, **lift the right thumb and hold** to reach a Fold, **put it back** to
  go. The re-zero means neither lift pulls the vessel off its line.
- *(Superseded by Round 14 — windows are back, rendering only their footprint.)* **Fold-gate windows are off** (`PerfStrip.FoldGateWindows`): a window is a whole second render of
  the world every frame a gate is on screen — exactly when you're threading one. Gates still carry
  you across; the camera cuts over with the ship instead of being carried through a window.
- **Waystation** (the Butterfly's Time race) is in the build and appears in the Arcade grid on its
  own (Round 11's filter). It plays in the Skim Race cell with life paused; the Butterfly lays no
  wake in a race (trails stay killed outside freestyle and the capped modes).

### Not verified in the editor

No Unity here; the eight out-of-editor gates and `touch_drift_slip.py --check` pass. On device:
1. Squirrel at cruise, lift the left thumb (boost ring), put it back: no yaw on either edge, still
   at speed, through the ring.
2. Both thumbs hard right, push further: the drift engages and deepens with the push; relax and it
   releases.
3. Right thumb down first on the Butterfly: nothing toggles. Lift left thumb: Mass/Dust toggles.
   Lift right thumb, hold, replace: a Fold.
4. Menu_Main comes up on Garland; flora grows and fauna swim; freestyle leaves a trail; after a
   long flight the trail pauses rather than anything vanishing, and resumes as fauna graze.
5. End a Wanderway run: Garland blooms back.
6. The vessel changer offers the Butterfly; Waystation launches from the Arcade grid.

## Round 13 — the drift goes back on a thumb, Panini in freestyle (2026-09-29)

Reported after Round 12: *"the post processing still needs the panini. the squirrel didnt drift or
lay rings. butterfly was good"*.

### The drift: back on the right-thumb lift, with depth from the steering thumb

The two-thumb overdrive (Round 12) is **retired**. It was out of reach in practice: throttle on the
dual-stick mix is the thumbs' horizontal *spread*, so at cruise the thumbs sit on OPPOSITE sides of
their origins, and "both thumbs past the rim on the same side" meant the inside thumb travelling
more than two stick radii (~1.2" at 0.6"/radius). And where it did engage, a tenth of a radius past
the rim bought 3.5% of the drift's ceiling (`0.35 × 0.1`) — nothing anyone can feel.

What the old lift drift got wrong was never the lift. It yanked the vessel (fixed in Round 12 by
re-zeroing the sticks on every one↔two-thumb change) and its depth was a constant. So:

| Gesture | Squirrel on touch |
|---|---|
| Lift the **left** thumb | Boost ring (`OnlyRightStickAction` 11 — unchanged). Put it back: straight through at speed. |
| Lift the **right** thumb | **Drift** (`OnlyLeftStickAction` 12), held while it is up. The left thumb steers alone, re-zeroed where it rests (no pull). **How far you steer sideways is how deep it slides** — floored at `LiftDriftDepthFloor` 0.5 so it is felt the instant it engages, full depth at the rim. Put the thumb back: the drift ends and the speed carries. |

The depth is published on `LeftTriggerAnalog` (the pad trigger's channel, `PerfStrip.TouchLiftDriftDepth`)
by `TouchInputStrategy.UpdateLiftDriftDepth`, and scaled by the Squirrel's `touchDriftDepth` ceiling —
now **0.5**, up from 0.35, because one-thumb steering (`OneThumbDriftTurnGain` 0.70) turns less than
the overdrive's full two-thumb yaw. `Tools/Build/touch_drift_slip.py --check` now models the LIFT
(one thumb at full deflection, the worst case: full yaw and full depth at once): **79.1° peak slip,
125% speed carried** through a 180° hairpin; the negative control at 0.8 fails (92.4°).

### The ring: not reproduced — so the ability path now heals itself and says what it did

Static inspection found nothing that stops the lift-left event reaching the ring: the event is raised
(`HandleDriftTransitions`), the Squirrel's touch override binds it, device resolution cannot matter
(under ANY device the same lift would also have raised `RightStickAction`, which the gamepad
override binds to the ring), the executor is wired to the right prism channel, and the Boost pool is
set. Two things were changed anyway, both general:

1. **`R_VesselActionHandler`'s button subscription now follows the pause STATE, not only its
   EVENTS.** For the local pilot it is meant to listen exactly while input is un-paused; that was
   kept only by edges (`OnToggleInputPaused`, plus explicit calls at spawn/handover). `n_paused` is
   a NetworkVariable whose `OnValueChanged` fires on a *change* only, and `OnDisable` dropped both
   the button channels and the pause source with nothing re-attaching them — a missed edge left the
   vessel FLYING (flight reads `InputStatus` directly) with every ability silently dead. It is now
   reconciled once a frame for the local pilot (one bool compare), and `OnEnable` re-attaches the
   pause source. Fleet-wide, but a no-op wherever the edges already worked.
2. **An "Abilities" section on the on-screen `DiagnosticsHUD`** (Development builds; compiled out,
   arguments included, in Release): `listening` (is the handler subscribed), `press` (the last bound
   press and the device it resolved against — or `ignored (autopilot)` / `suppressed` / `muted`),
   `unbound` (the last press with no binding — IdleAction and the straight-line gestures land here),
   `ran` (the last dispatch that reached the actions, with how many), and `boost ring` (`laid Nu
   ahead` or `cooldown Ns`). Those five rows separate every way "the ability did nothing" can
   happen, on the phone, where the console is out of reach. The ring's authored shape is worth
   knowing when reading them: **one** ring of 8 danger prisms, radius 8, 100 u ahead, 20 s cooldown.

### Panini in freestyle *(Round 14 extends this to the whole menu)*

Post-processing on the strip was granted only to scenes with a minigame controller — so never to
Menu_Main, including **freestyle**, where a phone pilot does most of their flying. The profile's
Panini (distance 0.7, which the speed tunnel relaxes with speed) needs post on the presenting camera,
so freestyle had half a speed tunnel. `PerfStrip.FreestyleFlying` (set with the trail flag on
freestyle enter/exit, cleared on every scene load) now grants the post stack for freestyle's
duration; the menu's own autopilot lava lamp stays post-free. Cost: freestyle now pays what a race
already pays (Bloom + Panini on the one presenting camera).

### Not verified in the editor

No Unity here; the eight out-of-editor gates, a Roslyn syntax parse of every changed file, and
`touch_drift_slip.py --check` pass. On device (Development build, so the overlay is up):
1. Squirrel, freestyle: lift the **right** thumb — it drifts; steer harder, it slides deeper; put the
   thumb back — it straightens out at speed.
2. Lift the **left** thumb — a ring 100 u ahead. If none appears, read the overlay's Abilities rows:
   `listening no` = subscription; `press` never shows `OnlyRightStickAction` = the gesture; `ran`
   shows it but `boost ring` does not say `laid` = the executor; `laid` but nothing visible = render.
3. Freestyle has the Panini curvature at rest and it relaxes as you speed up; the menu lava lamp
   behind the UI does not.
4. The Butterfly still toggles on a left lift and folds on a right lift.

## Round 14 — post across the menu, binary drift, the portal window back (2026-10-01)

Reported after Round 13: *"the ring works, panini is looking good. the butterfly is working.
Lets bring the postprocessing like bloom etc. to the lavalamp view not just freestyle. the
transition was jarring. the drift is wrong. just turn it into a binary drift fallback from analog.
we should be doing the same on gamepad if they have a non analog trigger. just treat lifting a
thumb as a full trigger pull and switch to one thumb flying. the butterfly portal is small, try to
render only what you need to performantly bring in the portal view."*

### Post-processing across the whole menu

Round 13 granted the post stack to freestyle only, so it switched on and off at the freestyle
boundary — in the middle of the camera blend, which read as a cut. It now runs in every scene that
shows the world: any minigame scene and Menu_Main (probed by its `MainMenuController`), lava lamp
and freestyle alike; only the boot/auth scenes, which show nothing but UI, stay post-free.
`PerfStrip.FreestyleFlying` is retired with nothing left to read it. The menu canvas is
screen-space overlay, so bloom never reaches the UI. Cost: the lava lamp now pays Bloom + Panini
on the one presenting camera, as freestyle and the races already did.

### Drift: analog when measured, a full pull when not *(the 0.70 gain and its speed gate: superseded by Round 16)*

`VesselTransformer.GetTriggerSum` now has ONE rule for every device: if trigger travel is measured
it is the drift's depth; if it is not, a drift that is on is a **full pull**. "Not measured" is every
non-gamepad device (a touch thumb lift, a key) — and, new, a **gamepad with a digital trigger**,
detected as a drift that is running while the trigger reports no travel (before, a digital trigger
started the drift and fed it a depth of zero, i.e. no drift). Binary drifts ease in and out over
`DRIFT_EASE_SPEED`, analog ones ride the trigger, and `DriftAudioController` follows the same rule.

On touch: **lift the right thumb = a full trigger pull**, and the left thumb flies alone (mirrored
onto both sticks, pitch and yaw only, at `OneThumbDriftTurnGain` 0.70). Retired: Round 13's
depth-from-the-steering-thumb (the slide changed under you as you steered) and with it
`VesselTransformer.touchDriftDepth` (removed from the code and from `Squirrel.prefab`) and
`PerfStrip.TouchLiftDriftDepth`. Nothing on touch writes the trigger channel any more; zero travel
is how the vessel knows to take the binary path.

`Tools/Build/touch_drift_slip.py` now gates the FELT invariant: a full-pull lift drift held at full
lock through a 180° hairpin must never drop below its entry speed and must leave at least as fast.
At gain 0.70: **slowest 100%, exit 111%**. The slide does pass 90° for a moment (peak 101°) — Round 9
gated on that angle as a proxy for braking, and it is only a proxy: `ShapeSpeed` floors the speed at
its pre-thrust magnitude, so the drift carries rather than brakes. Negative control: gain 1.0 loses
7% and fails (run). If the drift ever feels like it scrubs speed, the dial is `OneThumbDriftTurnGain`.

### The Butterfly's portal window, back — rendering only its footprint

The window is a disc that is usually small on screen, and the far-side camera was rendering the
whole screen to show it. Now (`FoldGatePortalView`):
- the window disc's bounding square is projected each frame to get its **rectangle of the screen**
  (padded 3 px, clamped; the whole screen once a corner is behind the near plane — the carry);
- the far-side projection is **cropped** to that rectangle (rows 0/1 of the clip transform, after
  the oblique near plane, which only touches row 2) — so **culling drops everything the window
  cannot show**, not only the pixels;
- the target is sized to the rectangle's pixels × `portalWindowRenderScale`, capped on the strip at
  `PerfStrip.FoldGateWindowMaxRenderScale` 0.5, in 32-texel steps with 1.25x growth headroom (an
  approaching gate grows into it instead of reallocating every frame; one 1.6x too big on both axes
  is reallocated smaller);
- the shader maps its screen UV into the rectangle (`_FoldGatePortalUV`, identity when full screen).
  The crop and the remap are proven to agree to 1e-14 offline.
`PerfStrip.FoldGateWindows` is retired; the chase camera is carried through the mouth again.
Also fixed on the way: the target's format check compared the REQUESTED format with what
`DefaultHDR` resolved to on the device, which could never match and reallocated it every frame.

### Not verified in the editor

No Unity here; the eight out-of-editor gates, a Roslyn syntax parse of every changed file, and
`touch_drift_slip.py --check` pass. On device:
1. Menu_Main: the lava lamp has bloom and the Panini curve; entering and leaving freestyle changes
   nothing about the look.
2. Squirrel: lift the right thumb — a full drift at once, steer with the left thumb; put it back —
   it straightens out at speed. On a pad with digital triggers, the drift trigger drifts.
3. Butterfly: a distant gate's window shows the far side; flying in, the camera is carried through
   and the window fills the screen without a hitch; the frame rate holds while a gate is in view.

## Round 15 — the mesh membrane back, a Squirrel that holds its line on glass (2026-10-02)

Reported after Round 14: *"the drift feels better now, portal works. lets use the older mesh
membrane instead of the capsule membrane. the squirrel's controls still feel less responsive which
leads to overcorrecting. when it isn't drifting it needs to feel like it has more grip and control
if touch controls are going to compete with a controller."*

### The membrane: the MembraneBase icosphere, at the capsule's radius

The "older mesh membrane" is `MembraneBase.prefab` — the 642-vertex icosphere (`SkyboxModel.fbx`)
on `SkyboxModelGraphMaterial` that Barren and Blob used until `dc22f9f9b` (2026-03) switched them to
`CapsuleMembrane`. It is unlit, opaque and **faces inward** (measured: all 1,280 triangles wind
inward), so from inside it is the cell's sky and from outside it shows only its far wall, behind
whatever the cell holds.

It comes back as **`MeshMembrane.prefab`, a prefab VARIANT of `MembraneBase` at scale 1200** — not
`MembraneBase` itself, which sits at 1000. `Cell.MembraneRadius` reads a membrane without a
`CapsuleMembrane` component as its `localScale.x`, so at 1200 every radius reader is exactly where
it was (toy ring, planting bands, density grids, the connecting preview's clamp, the race shells).
The mesh's own radius is ~1.04 units, so the wall stands at ~1250 and nothing laid inside 1200 is
behind it (Garland's farthest prism is 1,074).

Repointed: the thirteen cell configs the strip ships — Menu_Main's twelve (Garland, Lattice,
Arboretum, Barren and the freestyle worlds) plus Skim Race (shared by Waystation). The 42 configs
of modes the strip does not build still point at `CapsuleMembrane`. The three generators that own
a repointed config (`author_garland_cell.py`, `author_lattice_cell.py`, `author_arboretum_cell.py`)
were moved with it and are `--check` clean.

Cost: one 642-vertex draw where the capsule membrane drew **2,562 instanced capsules** (subdivision
4) every frame and rebuilt 2,562 matrices at 20 Hz; and from inside, the opaque wall writes depth in
front of the skybox, so the sky shader is early-z rejected under it.

Stated consequence *(resolved in Round 17 - intensity 3 gets its own, larger membrane)*: **Skim Race intensity 3** runs a lobe out to x = -3,480, outside the wall. From
inside the cell that part of the track is behind the wall until you cross it; from outside, the
membrane is transparent toward you. That is how Skim Race looked before March; the other three
intensities stay within 700.

### The Squirrel on glass: the nose follows the thumb

The lag was not in the input — it was in the hull. `VesselTransformer.RotateShip` slerps the hull
onto the commanded rotation at `LERP_AMOUNT 1.5 x dt`: a first-order lag with a **0.67 s time
constant**, so while turning at full yaw (120°/s) the nose trails the command by **80°** and keeps
swinging for a second after the thumb stops. A stick hides most of that — its spring recentres it
the moment you let go, and the pad's cosine curve keeps mid-stick rates low. Glass has neither: the
thumb has to be walked back to an origin it cannot feel while the hull is still coming round, the
pilot reads the swing as their own, and counter-steers into it. The Squirrel's camera is
hard-attached (its settings asset has no `mode`, so `FixedCamera`), so this slerp was the whole of
the lag.

- **`VesselTransformer.touchNoseResponse`** (0 = the fleet's 1.5, so every other hull is untouched;
  **Squirrel 9**): for a TOUCH pilot outside a drift, the hull follows at that rate. The steady turn
  RATE is unchanged — only the lag behind it shrinks: **80° → 14°** at full yaw, 40° → 7° at half.
- **Drift unchanged** *(superseded by Round 16 - the tight response now runs through the drift)*:
  the rate blends back to the fleet's by `DriftBlend01`, so the slide you approved is exactly what it was.
- **Drift exit without a whip** *(retired in Round 16 with the blend it existed for)*: a held drift leaves the nose ~95° behind the command. The response
  falls to the fleet's the frame a drift starts and climbs back at 10/s (`NoseResponseRisePerSecond`),
  so that leftover closes as a swell peaking near 210°/s, done in **~0.9 s** — against ~3.5 s to
  settle at the fleet response, and a snap at the cap if it jumped straight to 9. A catch-up cap at
  1.5x the hull's combined pitch+yaw+roll rate backstops large gaps (a flip); it never binds in
  steady flight, because an exponential follower chasing a command at w moves at most w.
- **Touch dead zone is a physical size**: `DeadZoneInches` 0.05 (8% of the 0.6" travel), floored
  at the old 12 px. It was 12 px flat — under a millimetre on a 400+ dpi phone, a centre nobody
  could find by feel, so a thumb walked back to "straight" kept a small turn alive.

Gamepad and keyboard flight are unchanged; so is the AI unless it is flying a hull whose input
device reads Touch (the local Squirrel on autopilot in the lava lamp, which now steers crisper).

### Not verified in the editor

No Unity here; the eight out-of-editor gates, a Roslyn syntax parse of every changed file,
`touch_drift_slip.py --check`, and the three cell generators' `--check` pass. On device:
1. Menu_Main / freestyle: the cell's sky is the mesh membrane (no capsules); toys, Garland and the
   Wanderway look right inside it; the frame rate is at least what Round 14 had.
2. Squirrel on touch: a turn stops when the thumbs come back to centre — no continued swing to
   correct; small corrections land without overshoot. A held drift feels as before, and on exit the
   hull comes round onto the line in under a second without snapping.
3. Skim Race intensity 3: the far lobe appears as you cross the wall (expected, see above).

## Round 16 — the touch drift is the pad drift, and a softer curve (2026-10-02)

Reported after Round 15: *"now lets make the drift consistent with the not drift changes you just
made, the squirrel drift should be very similar to gamepad just the analog is replaced with a full
pull of the trigger when the thumb is released. the remaining thumb control should be just as
responsive and enable the player to turn sharper (again this is not something new, this is just how
the drift works on a controller). I feel like previous attempts at making the squirrel more
responsive may have overtuned its response curve to compensate for the issue you found. give it a
slightly less steep response curve bringing it closer to the gamepad curve."*

### The drift: a pad drift with the trigger replaced by a full pull

- **No one-thumb gain.** Rounds 12-15 cut the mirrored thumb to 0.70 authority while a thumb was
  lifted (`OneThumbDriftTurnGain`), so a full-lock drift could not scrub speed. That was a
  touch-only steering cut: on a pad, two full sticks and a full trigger command the full-lock yaw it
  removed. It is gone (with `OneThumbTurnGain`, which was 1 and therefore nothing): one thumb at the
  rim commands exactly what two full pad sticks command, and the drift's `Mult` (1.8) turns it
  sharper the way it does on a pad - **216 deg/s at full lock, pad 215.8**. The mix and the fan-out
  in `Reparameterize` are the same stick again, so the split that existed to keep the gain off the
  fan-out is collapsed.
- **The nose stays tight through the drift.** `touchNoseResponse` (9) now applies in a drift as out
  of one; Round 15's blend back to the fleet's 1.5 and the post-drift slew it needed are deleted.
  At full lock the nose trails the command by 24 degrees where the fleet response let it fall 144
  behind, so the remaining thumb steers the slide as crisply as straight flight - and a drift exit
  leaves only that 24-degree gap, closed at the drift's own turn rate, so the exit needs no
  special-casing. The catch-up cap stays for discontinuities (a flip).
- **Same tuning on both devices**, already: the touch lift (InputEvent 12) and the pad trigger
  (InputEvent 2) bind the same `SquirrelDriftAction` + `DriftTrailAction` assets.

`Tools/Build/touch_drift_slip.py` was rebuilt around that rule. `--check` now fails if touch and pad
drift DIFFER: different drift assets on the two overrides, any one-thumb gain below 1 or a scaled
mirror, or a touch curve that does not reach the pad's full-deflection authority. `--self-test`
proves all four fire (Round 15's 0.70 restored, the mirror scaled, a curve topping out at 0.9, touch
bound to a different drift asset). What the shared drift does to speed is now REPORTED: a full-lock
180 degree hairpin at full pull bottoms out at **93.2%** of entry speed (peak slip 117 degrees) - on
both devices, because it is the drift action's tuning; the dials are its `Mult` and `driftDamping`.
Below ~0.9 deflection it carries speed (100% slowest, 107-125% exit, `--sweep`).

### The curve: back to 75/25

`TouchInputStrategy.Ease` goes from 90% linear + 10% cubic to **75% linear + 25% cubic** - the curve
it shipped with before July's 90/10. That pass answered "touch feels less responsive than a pad" by
steepening the curve; the cause was the hull's 0.67 s nose lag (Round 15), and a steeper curve on a
lagging nose is just a bigger turn to overshoot. Output at quarter / half / three-quarter deflection:
**0.191 / 0.406 / 0.668**, against the old 0.227 / 0.463 / 0.717 and the pad's cosine 0.076 / 0.293 /
0.617. Full deflection is 1 on every curve, so the turn-rate ceiling and the full-lock drift are
unchanged. The Butterfly flies the same touch curve, so its centre softens too.

### Not verified in the editor

No Unity here; the eight out-of-editor gates, a Roslyn syntax parse of both changed files, and
`touch_drift_slip.py --check` / `--self-test` pass. On device:
1. Squirrel on touch, no drift: small corrections are finer than Round 15 near centre; full
   deflection turns as hard as before.
2. Lift the right thumb at full left-thumb lock: the drift turns as sharply as a pad drift with both
   sticks over and the trigger pulled, and the nose follows the thumb without lag; put the thumb back
   and the hull settles at once.
3. A full-lock 180 bleeds a little speed (~7%), as on the pad; anything less than full lock carries.

## Round 17 — the sky baked offline, Skim Race's far lobe, the Butterfly on glass (2026-10-02)

Reported after Round 16: *"please continue. and making the skybox bigger in the intensity 3 is fine.
but i just played wander way and it was pitch black in the skybox which is not ideal."*

### Resync

61 upstream commits merged clean (no overlap with strip files). Brought in, among others: a crystal
`Material` leak per colour change (`Crystal.LerpCrystalMaterialCoroutine`), the freestyle camera
losing the ship, the pad releasing held triggers on pause/strategy switch, Skim Race Blue pickups
and AI readiness. The merge also exposed a Round 15 miss: a comment-only edit to
`SpawnableGarland.cs` had staled `author_garland_cell.py --check`'s source hash. Re-measured with
`garland_harness/run.sh`: every number identical (4,259 prisms, 2,177,499 volume).

### The sky: the authored HyperSea sky, baked without the editor

Wanderway was black because the strip clears to a solid deep-space colour - the procedural sky
(`HyperSeaSkybox.shader`, 767 lines, two 3x3x3 Voronoi searches and ~20 noise octaves per pixel) is
too expensive to run, and the editor bake that was meant to replace it never produced an asset.
Inside a cell nobody saw it (the mesh membrane is the sky there since Round 15); a Wanderway run
flies out of the cell and saw only the clear.

`Tools/Build/bake_static_skybox.py` bakes it OFFLINE. The sky is pure math with no textures, so the
tool transpiles the SHIPPED shader's CGINCLUDE block to C++ (a 90-line HLSL shim,
`static_skybox_harness/hlsl_shim.h`), compiles it with clang, and evaluates it per texel with the
SHIPPED material's values (colours linearized as Unity uploads them in a Linear project; vectors
left alone) into a **4096x2048 equirectangular panorama**, 3x3 supersampled in linear light,
8-bit sRGB with a +-1 LSB dither against banding in the dark nebulae. 6.7 MB PNG, ~70 s on 4 cores.
- **Runtime cost: one texture sample per pixel.** `StaticSkyPanorama.shader` (a 20-line URP skybox
  in the same shape as `HyperSeaSkybox.shader`), `Resources/StaticHyperSeaSkybox.mat` - the name
  `PerfStripRuntime` has always loaded, so no code changed. Inside a cell the opaque membrane is in
  front of it, so early-z rejects it.
- **LDR is exact, not a compromise:** the strip renders LDR (`URP_Asset` `m_SupportsHDR: 0`), so a
  clamped 8-bit value is what the procedural sky would have put on screen anyway.
- **Static:** time 0 - the authored drift and star twinkle do not animate. A foreground star smaller
  than a texel is averaged rather than point-sampled, so it reads fainter than the procedural's
  per-pixel sparkle; galaxies, the galactic band, Andromeda and the nebulae are intact.
- **Proved:** the shader's direction→UV and the bake's UV→direction round-trip to 3e-14; the
  longitude seam's colour step (1.98) is below the interior neighbour step (2.62), i.e. no line.
- **`--check`** fails if the sky shader, its material, the shim or the bake code changed since the
  bake, or if any authored asset drifted. It hashes the CODE that moves pixels and not the whole
  script, so a docstring edit is not a stale bake (proved both ways: a docstring edit passes, a
  `_StarBrightness` change fails) - the trap the Garland hash just demonstrated.
- **Retired:** `Assets/Editor/BakeStaticSkybox.cs` (FrogletTools ▸ Bake Static HyperSea Skybox). It
  wrote the same `Resources` path as a cubemap material; two writers to one asset is whichever ran
  last, and the offline bake is the one that exists.

Also visible now: the boot/auth scenes and anything a RenderTexture camera sees outside a cell
(the Butterfly's gate window) show the sky instead of the clear.

### Skim Race intensity 3: its own, larger membrane

Intensity 3's barbell track runs out to x = -3,480; the 1,200 membrane hid that lobe until you flew
through the wall. Per CLAUDE.md, a Cell-owned visual is resized by a config pointing at a resized
prefab: `MeshMembraneLarge.prefab` (variant of `MembraneBase`, scale **3,800** - wall at ~3,860
after the mesh's own radius and ripple, ~375 clear of the lobe) and `Skim Race Cell Config 3`
(identical to the Skim Race config but for the membrane). The Skim Race scene's cell now picks by
intensity - `IntensityWise` over [config, config, config 3, config] - so intensities 1, 2 and 4 are
byte-for-byte the cell they were. Waystation shares `Skim Race Cell Config` and is untouched.

Stated cost: `IntensityWise` also selects the timer-driven `IntensityWiseLifeSpawner` instead of the
prey-linked `RandomLifeSpawner` for this cell. On the strip that is inert - cell life runs only in
the home world (`PerfStrip.CellLifeRuns`, gated in `CellLifeSpawnerBase`) - but it is a real change
to Skim Race's food web in a non-strip build.

### The Butterfly on glass

`touchNoseResponse` **5** on `Butterfly.prefab` (Squirrel 9): the Butterfly turns at 45 deg/s, so the
fleet response left its nose 30 degrees behind the command; at 5 it is 9. Gentler than the Squirrel
on purpose - it is the slow, meditative hull - and it makes the Fold's line (aimed before the press)
land where the pilot pointed. `waystation_course.py` and the Butterfly asset generators still pass.

### Not verified in the editor

No Unity here; the eight out-of-editor gates, a Roslyn parse, `bake_static_skybox.py --check`,
`touch_drift_slip.py --check` and the cell/course generators pass. On device:
1. Wanderway: the sky behind the belt is the HyperSea sky (galactic band, Andromeda, nebulae), not
   black; frame rate holds. Inside a cell nothing changes (the membrane is in front of it).
2. Skim Race intensity 3: the whole barbell is visible from the start; intensities 1, 2, 4 unchanged.
3. Butterfly on touch: turns settle where the thumb stops; a Fold lands on the line you aimed.

## Round 18 — the Wander toy: a compile fix, and the Ark stays home (2026-10-02)

Reported: `ToyboxController.cs(137,34): error CS0234: The type or namespace name
'ConveyorToyDefinitionSO' does not exist in the namespace 'CosmicShore.ScriptableObjects'`.

### Cause

Upstream `bc6b98d52` merged the Wanderway (Conveyor) and Arkway toys into one **Wander** toy with
two choices, Without Ark and With Ark, and deleted `ConveyorToyDefinitionSO`. The strip's toybox
filter still named the old type. Every gate passed the merge, because none of them can see this:
the Roslyn harness is syntax-only for monolith files and abandons type binding, and
`check_using_directives.py` resolves unqualified names, not a fully-qualified one. A scan of every
type the strip's own diff names (`is` / `as` / `new` / `typeof` / `or` patterns and qualified
`CosmicShore.*` names) against every type declared in `Assets/` found this one and no other.
The mobile belt tune did carry through the rename: `Wander_WithoutArk.asset` still holds
poolSize 8, prismBudgetPerScene 150, aheadTargetScenes 4, maxCrystalsPerScene 2, lifeformScenes 0.

### Fix

- `ToyboxController` filters to `WanderToyDefinitionSO` (with the domain changer, element
  charger and vessel changer, as before).
- **With Ark is gated off inside `WanderToy`** (`ArkShips => !PerfStrip.LightToysOnly`). The
  merge would otherwise have smuggled the Arkway in: a voyage stands a corridor of three satellite
  cells, which `PerfStrip.LightToysOnly` already listed as skipped. It is gated at the toy's ONE
  declaration (`BuildOptions`), so the fly-through station and the Toy Box card drop it together.
- **The emblem's core is a microscene**, not a miniature Ark - a build that does not offer the Ark
  must not advertise one.
- **A pass starts the wander directly.** With one choice there is nothing to choose, so the toy
  is the one-ring toggle the Wanderway toy was before the merge: fly it to leave, fly it (or the
  return station on the tether's tail) to come home. Upstream's behaviour returns the moment
  `PerfStrip.LightToysOnly` is false.

### Not verified in the editor

No Unity here; the out-of-editor gates and a Roslyn parse pass. On device:
1. The project compiles; the toybox shows the Wander toy (microscene emblem), domain changer,
   element charger and vessel changer.
2. Flying the Wander toy starts the Wanderway immediately (no station matrix); flying it again,
   or the return station, brings you home.
