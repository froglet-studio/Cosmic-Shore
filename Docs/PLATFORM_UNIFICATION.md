# One codebase for Windows, iOS and Android — diagnosis and plan

**Status (2026-10-05): diagnosis (§1) and inventory (§2) done. Steps 2 (touch controls) and 3
(device tiers) landed on this branch, awaiting editor/device verification
(`Docs/UNITY_VERIFICATION_CHECKLIST.md`, top two entries). Device measurements are deferred, not a
gate (owner's call).**

### Decisions recorded (2026-10-05, project owner)

These strip changes are **NOT ported**, on any tier:

| Not ported | Strip mechanism |
|---|---|
| Plants and creatures paused everywhere except the home cell (Garland) | `PerfStrip.CellLifeRuns` in `CellLifeSpawnerBase` and `Flora` |
| The toybox cut down to the light toys | `PerfStrip.LightToysOnly` in `ToyboxController`, and the Ark gate in `WanderToy` |
| Vulkan dropped for OpenGL ES only | Android `m_BuildTargetGraphicsAPIs` (Auto [Vulkan, GLES3] stays) |
Owner branch for this work: `claude/serene-edison-lfv24f`.

Today three builds come from three places:

| Platform | Built from | Performance changes | Touch-control changes |
|---|---|---|---|
| Windows | `bleeding-edge` | none | n/a (pad / keyboard / mouse) |
| iOS | `bleeding-edge` | none | **bleeding-edge's** touch code |
| Android (yours) | `claude/eloquent-meitner-9e4u2a` | **none**: it is bleeding-edge + 2 Gradle commits | bleeding-edge's touch code |
| Android (Garrett's) | `claude/android-performance-stripped-dap5z2` | 18 rounds of strips behind `PerfStrip` | **the reworked touch controls** |

The goal is one branch (`bleeding-edge`) that detects the platform and the device's capability
at runtime and behaves accordingly, with the reworked touch controls on every touch device
(iOS included) and the performance strips only on hardware that needs them.

---

## 1. Why the same game runs at ~4 FPS on the Samsung and 30–40 FPS on the iPhone

### 1.1 It is not a code difference

**Confirmed (2026-10-05): the ~4 FPS Android build was made from
`claude/eloquent-meitner-9e4u2a`.** None of Garrett's strips were in it, and the strip has not
yet been measured on this phone.

`claude/eloquent-meitner-9e4u2a` is two commits on top of bleeding-edge, and both are Gradle
template fixes (`mainTemplate.gradle`, `settingsTemplate.gradle`,
`AndroidResolverDependencies.xml`). There is no performance change in it. The Android build and
the iOS build run **identical content and code**. The 10x gap is the device.

### 1.2 This title is CPU-bound on ONE thread, and that thread is 5–10x slower on the Samsung

`Docs/PERFORMANCE_OPTIMIZATION.md` §0, capture #4: Menu_Main with ~7,000 prisms costs
**~26 ms of CPU per frame on a desktop**. Bleeding-edge boots Menu_Main into the Garland cell
(`Garland Cell Config.asset`, `BootDefault: 1`): 4,259 laid prisms at entry, ~8,100 once its
flora mature. Each prism is a GameObject with a BoxCollider and several MonoBehaviours; the
ecology, spatial index, collider LOD and Netcode host all tick on the Unity main thread.

That main thread runs on one core, so single-core speed is the number that matters:

| Device class (single-core, approx.) | Main-thread cost of that ~26 ms desktop frame | FPS |
|---|---|---|
| Desktop / Apple A15–A17 (desktop-class cores) | ~26–33 ms | **30–40** (your iPhone) |
| 4 GB Samsung A-series (Cortex-A55-class, or 2x A75/A76 + 6x A55) | ~5–10x slower → 130–260 ms | **4–8** (your Samsung) |

The GPU gap is in the same direction and larger: a Mali-G52/G57 MC2 or Adreno 610 has roughly
1/10–1/30 of the shader throughput of a recent iPhone GPU. Bleeding-edge pays full price on it:
HDR on, the 767-line procedural `HyperSeaSkybox.shader` (Voronoi + noise octaves per pixel)
behind everything, the `CapsuleMembrane` (2,562 instanced capsules, matrices rebuilt at 20 Hz),
transparent-prism overdraw, and the menu post stack.

**So the iPhone is not "un-nerfed and fine"; it is fast enough to absorb the full game. The
Samsung is not.** Any mobile plan has to treat a budget Android phone as a different hardware
tier, not a different operating system.

**Confirm on device before acting on numbers:** build `claude/eloquent-meitner-9e4u2a` as a
**Development** build and open the `DiagnosticsHUD` overlay (auto-spawns in Development builds,
touch buttons). Its verdict row reads CPU-bound / GPU-bound from `FrameTimingManager`; Advanced
mode shows the main-thread ms. Do the same on the iPhone. Also note the exact Samsung model
(Settings ▸ About phone): a Galaxy A13 (Exynos 850, eight A55 cores) and a Galaxy A15 (Helio G99,
two A76 cores) are a 3x apart in single-core speed.

### 1.3 Bleeding-edge has no device-capability detection, and its auto-detect is desktop-shaped

- **Input already detects the device.** `InputController.SelectStrategy` picks
  `TouchInputStrategy` when `SystemInfo.deviceType == DeviceType.Handheld` (iOS and Android
  alike). So any touch change made in `TouchInputStrategy` reaches iOS with no extra wiring.
- **Graphics "auto-detect" ignores the GPU and ranks phones backwards.**
  `SettingsAutoDetector.CapabilityScore()` scores CPU **core count** (8+ cores = +2), RAM
  (8 GB+ = +1) and VRAM (4 GB+ = +1). A budget Samsung has 8 slow cores → score 2 → **Low**. An
  iPhone has 6 fast cores (2P + 4E) → score 1 → **Very Low**. The weaker phone is told to render
  more: on a 1080×2400 Samsung the Low pixel budget (2.1 M) gives ~90% render scale; on a
  1170×2532 iPhone the Very Low budget (1.3 M) gives ~66%. Both also get FSR upscaling, an extra
  ALU-heavy pass on a low-end mobile GPU.
- The CPU knobs it writes (`EcosystemDensity`, `PhysicsDetail`, `AiCrowdSize`,
  `AdaptivePerformance`) are **stored but never read** by any gameplay system, so the CPU side
  gets no scaling at all.
- Adaptive Performance (+ the Samsung provider) is installed, but the Android loader is off
  (`m_InitManagerOnStart: 0`) and every scaler is disabled, so it does nothing.
- There is one URP asset (`URP_Asset.asset`: HDR on, MSAA 4x, scale 1.0) for every platform;
  every quality level has `customRenderPipeline: 0`.
- `BootstrapConfig._targetFrameRate: -1` means "30 fps" on mobile in `AppManager.ConfigurePlatform`,
  but `DisplayGraphicsSettings` re-applies the auto-detected cap (display Hz, max 120) right
  after, so it is not a cause of the gap on either phone.

---

## 2. What Garrett's branch changed (inventory)

Net diff `origin/bleeding-edge...origin/claude/android-performance-stripped-dap5z2`: 129 files,
+4,905 / −229, merge-base `06b6772fc`. The strip is 44 commits ahead and 59 behind. Its own
changelog (`Docs/MOBILE_STRIPPED.md` on that branch) records 18 rounds, and several states it
describes were later reverted (MSAA 4→1→4, `BootTrace`, the JNI haptics, the FMOD bank strip,
the overdrive drift, `touchDriftDepth`). **The net diff below is the source of truth.**

Almost everything is gated on `PerfStrip` (`Assets/_Scripts/Utility/PerfStrip.cs`), a
`static class` whose `Enabled = true` is fixed at compile time. Every other flag in it derives
from `Enabled`. That flag is why the strip cannot live in bleeding-edge as-is: it would strip
Windows too.

### 2.1 Android build / player settings (Android-only, safe)

| Setting | bleeding-edge → strip | Note |
|---|---|---|
| `AndroidTargetArchitectures` | 3 (ARMv7 + ARM64) → 2 (ARM64) | Smaller APK. Drops 32-bit-userland phones (some Android Go Samsungs). |
| `AndroidMinifyRelease` | 1 → 0 | The real launch-crash fix: R8 stripped `androidx.work.WorkDatabase_Impl`, pulled in by **Unity Ads**. Your branch removes Unity Ads, which removes the cause. |
| `useCustomProguardFile` + `proguard-user.txt` | 0 → 1, keep rules for androidx.work/room/startup/lifecycle | Defense-in-depth if R8 comes back on. |
| Android graphics APIs | Auto [Vulkan, GLES3] → manual [GLES3] | Made to test a Vulkan-crash theory that the R8 finding replaced. **Decided: not ported**; Auto stays. |
| `mainTemplate.gradle` | namespace / ndk lines **inside** the EDM4U-managed block | Your branch's `0f6b38ba5` puts them outside, where a re-resolve can't wipe them. Take yours. |
| `Assets/Editor/BuildAndroid.cs` | new FrogletTools APK build | Its Development path blanks the keystore settings, which persist into `ProjectSettings.asset` (production keystore path). Fix before merging. |

### 2.2 Touch controls / mobile feel (what iOS should get)

| Change | Gate on the strip | Port as |
|---|---|---|
| `TouchInputStrategy`: stick radius 0.6" and dead zone 0.05" (physical size, 12 px floor), one-thumb mirror at full authority, re-zero on every 1↔2-thumb change, throttle held through a lift and carried back, 75% linear + 25% cubic curve | ungated (Touch strategy only) | as-is → every touch device |
| Left/RightStickAction only on a thumb **lift**, never on first touch (the Butterfly's mode toggled / Fold started on whichever thumb landed first) | `PerfStrip.TouchStickEventsOnLiftOnly` | **ungated**: it's a touch bug fix, and iOS has the same bug |
| `VesselTransformer.touchNoseResponse` (Squirrel 9, Butterfly 5; fleet stays 1.5): the hull follows the thumb with ~14° lag instead of ~80° | `ActiveInputDevice == Touch` | **gate to the local human pilot.** On a handheld, AI players also read Touch, so AI hulls would get it too, and bleeding-edge's Skim Race AI models its own hull at the fleet's 1.5 (`VesselTransformer.RotationFollowRate`) |
| `GetTriggerSum` / `DriftAudioController`: a drift with no measured trigger travel is a full pull (touch lift, key, digital-trigger pad) | **ungated, all devices** | as-is; it also fixes digital-trigger pads on PC (they drifted at depth 0) |
| `R_VesselActionHandler`: the ability subscription is reconciled to the pause state every frame (a missed NetworkVariable edge left the vessel flying with every ability dead) + DiagnosticsHUD "Abilities" rows | ungated, fleet-wide | as-is (it's a fix) |
| `Squirrel.prefab` | `touchNoseResponse: 9`, **and `boostLoopEvent` cleared** | port the nose field; **do NOT port the audio clear**: it is a merge loss that silently reverts bleeding-edge's "new skim move" wiring (`fb564b517`) |

The vessels' `_touchActionOverrides` are unchanged in net; touch and pad bind the same drift assets.

### 2.3 GPU / render cuts (all edit SHARED assets: merged as-is they change Windows and iOS)

| Change | Where | Note |
|---|---|---|
| HDR off, render scale 0.8 (MSAA stays 4x) | `URP_Asset.asset`, the ONLY pipeline asset | the runtime applier overwrites scale/MSAA on other builds, but **not HDR**, so HDR would go off everywhere |
| Intermediate texture Always → Auto | `URP_Asset_Renderer.asset` | shared |
| 22 crystal materials brightened for LDR | `_Graphics/Materials/CrystalMaterials/*.mat` etc. | shared; with HDR on they'd over-bloom on PC |
| Skybox → baked 4096×2048 panorama (`StaticSkyPanorama.shader`, `Resources/StaticHyperSeaSkybox.mat`), post-processing only on the presenting camera, FXAA Low | `PerfStripRuntime.cs` (stomps every camera's clear/post/AA) | in `Resources`, so it ships on every platform even unused; second writer of camera AA beside `GraphicsSettingsApplier` |
| `CapsuleMembrane` (2,562 instanced capsules) → `MeshMembrane` (one 642-vertex icosphere) | 13 cell configs | Barren and Skim Race configs are shared by ~10 modes the strip doesn't ship |
| Skim Race intensity 3 → own larger membrane + `IntensityWise` | `MinigameSkimRace.unity` | also changes Skim Race's life spawner on every platform (a food-web change) |
| Butterfly fold-gate window renders only its footprint (crop + RT quantize) | `FoldGatePortalView.cs` + `FoldGatePortal.shader` | crop is platform-agnostic; must merge with the shader. Resolution cap 0.5 is `PerfStrip`-gated |
| Graphics settings menu disabled; target fps −1 → 240 | `GraphicsSettingsApplier.cs`, `BootstrapConfig.asset` | the strip turns OFF the very hook bleeding-edge already has for per-tier render settings |

### 2.4 CPU / content cuts

| Change | Gate |
|---|---|
| Vessels lay **no trail**, except freestyle (pen waits while the cell holds > 10,000 prisms), the Wanderway tether, and Skim Race / Joust with a **FIFO cap** (oldest prism consumed past 2,000 / 1,200 per vessel, shared per seat) | `TrailsDisabled`, `CappedTrailActive` |
| ~~Cell life (flora/fauna spawners, flora growth) paused except in the home world (Garland)~~ **decided: not ported** | `CellLifeRuns` |
| No cytoplasm motes in any cell | `SnowChanger.Initialize`, `Enabled` (undecided) |
| ~~Toybox: Wander (no Ark), domain changer, element charger, vessel changer~~ **decided: not ported** | `LightToysOnly` |
| Vessel changer roster narrowed to Squirrel + Butterfly | `ShipsVessel` (undecided) |
| Wanderway belt 30,000 → 1,200 resident prisms, no lifeforms | `Wander_WithoutArk.asset` — **shared, ungated** |
| `MicrosceneConveyor.MaxConcurrentArrivals` 3 → 2 | **ungated** |
| Non-HOME menu screens deactivated; HOME + NavBar deactivated in freestyle | `MenuUIStripped` |
| Top-bar glow's endless DOFade dropped | `Enabled` |

### 2.5 Build content strip and offline bypass (do NOT port)

- `EditorBuildSettings`: 7 of 38 scenes (Bootstrap, Authentication, Menu_Main, Skim Race, Joust,
  Waystation, plus WildlifeBlitz co-op left on with no card). The arcade grid hides cards whose
  scene isn't in the build (`ArcadeExploreView.IsLaunchableInThisBuild`). That filter is worth
  keeping ungated as a safety net.
- `PerfStrip.OfflineMode` / `DisableSocialNetworking`: local sign-in, no Relay / CloudSave /
  Analytics / Friends / presence (8 files). Garrett's builds had no UGS project; the real build
  does, and bleeding-edge's `OfflineModeService` already handles "no network".
- FMOD banks committed under `StreamingAssets/` (stale; bleeding-edge targets `FMODBanks`),
  `mainTemplate.gradle.backup`, and platform-switch editor churn (QualitySettings v5 keys, URP
  global-settings lists, a TMP font serializedVersion).

### 2.6 Platform-agnostic fixes worth merging on their own

- `SquirrelSkimmerImpactorDataContainer`: drop the obsolete `SkimmerFXPrismEffect` beam (closes
  the open item in `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`; the crackle becomes the sole skim
  visual, as on the Dolphin).
- `ProximityBoostAudioController`: `minTickInterval` 0.07 s stops the skim one-shot firing at frame
  rate. (Its second change, silence above 90% boost, is a tuning call for audio sign-off.)
- `VesselTransformer.DecayBoost`: raise `boostChanged` only when the value changes (it fired every
  frame at rest, fanning out to HUD + audio).
- `FoldGatePortalView`: the render target was reallocated **every frame** (requested format never
  equals the resolved `DefaultHDR`).
- `SkimRaceController.OnDestroy` hid `NetworkBehaviour.OnDestroy` (CS0114) on the strip; bleeding-edge
  doesn't have that method, so this only matters if the strip's trail-cap code is ported.

### 2.7 Things to know before porting

1. **The FIFO trail cap breaks a LOCKED rule.** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`:
   *"there is no context in which trail caps, prism TTLs, or idle cullers are acceptable."* The
   only exception is the Wanderway tether, recorded in `Docs/ECOSYSTEM.md` §0. The strip relied
   on a branch-only OK (2026-07-07). Bringing the Skim Race / Joust cap to bleeding-edge, even
   behind a mobile tier, needs that sign-off recorded there; the sanctioned alternatives are
   fauna cleanup and pausing the spawner. A phone-only life pause raises the same Universality
   question.
2. **Merge conflicts are small.** Only `HostConnectionService.cs` (offline gate vs bleeding-edge's
   presence-rejoin, so it disappears if §2.5 isn't ported) and `AndroidResolverDependencies.xml`
   (take both lines) conflict textually. No strip reference dangles on current bleeding-edge.
3. **Dead code in the strip:** `Skimmer.cs`'s forcefield kill finds no renderer; `Toy.Tick()` has
   no overrides.
4. **Editor play mode:** `m_EnterPlayModeOptions: 3` (no domain reload), and `PerfStrip`'s mutable
   statics have no `SubsystemRegistration` reset. A runtime profile must reset them.

---

## 3. Target architecture

### 3.1 Principle: tier by capability, not by operating system

"Android = nerfed, iOS = full" would ship the strip to a Galaxy S24 (faster than most iPhones)
and the full game to an old 2 GB iPhone 7 (iOS 15 still supports it). The OS is one input to a
**device tier**; the tier decides the content and render cost. With the defaults below, every
current iPhone lands on `MobileHigh` (full content + touch controls, which is what iOS runs
today), and the 4 GB Samsung lands on `MobileLow` (Garrett's strip).

```
 boot (RuntimeInitializeOnLoad, BeforeSceneLoad)
   Application.platform, SystemInfo.deviceType,
   RAM, CPU cores + max frequency, GPU name/vendor ─► DeviceTierClassifier ─► DeviceTier
                                                         ▲                  { Desktop,
                       dev override (PlayerPrefs /       │                    MobileHigh,
                       FrogletTools "Simulate tier") ────┘                    MobileLow }
                                                                                │
                                                         PlatformProfileSO (one asset per tier)
                                                         ├─ Render: URP asset, render scale cap,
                                                         │  AA, post policy, sky mode, membrane
                                                         ├─ Frame: target fps
                                                         └─ Content: trail / ecology / toybox /
                                                            menu-UI / mode availability / budgets
                                                                                │
                     ┌──────────────────────────────────────────────────────────┼──────────────┐
          SettingsAutoDetector (first-run seed;           gameplay gates read            input: unchanged —
          mobile branch reads the tier, not core count)   PlatformProfile.Current.X      Handheld ⇒ TouchInputStrategy
                                                          (replaces PerfStrip.X)
```

### 3.2 The four layers

1. **Build-time, per platform: Player Settings.** Unity already keeps these per platform
   (Android ABIs, minify, Gradle templates, graphics API list; iOS Metal). Changes here cannot
   leak across platforms, so Garrett's Android-only settings and your Gradle fixes merge as-is.
2. **Runtime: device tier + one `PlatformProfileSO` per tier.** Every knob the strip hard-codes in
   `PerfStrip` (a `static class` with `Enabled = true`) becomes a field on the profile asset, so
   `PerfStrip.TrailsDisabled` becomes `PlatformProfile.Current.TrailsDisabled` and so on. The
   `Desktop` profile is "everything off", so Windows behaves exactly as bleeding-edge does today.
   Config lives in SOs per CLAUDE.md config separation; no `#if UNITY_ANDROID` in gameplay code
   (runtime detection only, per `Docs/CONDITIONAL_COMPILATION.md`).
3. **Render pipeline per tier, not one shared asset.** Add `URP_Mobile.asset` (HDR off, 4x MSAA,
   no FSR) next to today's `URP_Asset.asset`, assign it to a mobile quality level, and make that
   level the Android/iPhone default (`m_PerPlatformDefaultQuality`). Garrett edited the shared
   `URP_Asset.asset`, crystal materials, cell configs and toy budgets in place; merged like that
   they would change Windows and iOS too. Each of those becomes a per-tier choice on the profile.
4. **Input: already device-driven.** Port the touch controls into `TouchInputStrategy` and the
   touch-only vessel fields with **no** `PerfStrip` gate. They then apply on every touch device
   (iOS and Android) and never on a pad/keyboard/mouse.

### 3.3 What does NOT come across

- **The offline/UGS bypass** (`PerfStrip.OfflineMode`, local sign-in, skipped CloudSave /
  Analytics / Friends / presence). Garrett's builds had no UGS project linked; the real Android
  build does. Bleeding-edge's `OfflineModeService` already covers "no network" properly.
- **The build scene list strip** (`EditorBuildSettings`). Every platform ships every scene; the
  tier decides which modes are offered. (Garrett's "only show cards whose scene is in the build"
  filter is still worth keeping as a safety net.)
- **Diagnostics scaffolding** that existed for crash hunting (`BootTrace` etc.) unless we still
  need it.


### 3.4 As built (Step 3)

| Piece | Where |
|---|---|
| `DeviceTier` enum (Desktop 0, MobileHigh 1, MobileLow 2) | `_Scripts/Data/Enums/DeviceTier.cs` |
| Pure classifier: `DeviceFacts`, `DeviceTierRules`, `DeviceTierClassifier.Classify` / `OsFromName` (no Unity API; compiled and run outside the editor) | `_Scripts/System/Platform/DeviceTierClassifier.cs` |
| Resolver: `PlatformProfile.Tier` / `Current` / `Reason` / `TierOverride`, read once per session, reset at `SubsystemRegistration`, logged on `CSLogChannel.Boot`, shown on the DiagnosticsHUD "Platform" rows | `_Scripts/System/Platform/PlatformProfile.cs` |
| `PlatformProfileSO` (per tier) + `PlatformProfileSetSO` (the three slots + the thresholds) | `_Scripts/ScriptableObjects/`, assets in `_SO_Assets/Platform/` and `Resources/PlatformProfiles.asset` |
| Tier-aware first-run recommendation; settings v3 re-seed | `SettingsAutoDetector.RecommendSettings` / `RecommendFromProfile`, `DisplayGraphicsSettings.ReseedUntouchedTierGraphics` |
| Editor: detected tier + facts, simulate a tier, re-run auto-detect in Play | FrogletTools ▸ Performance ▸ Device Tier (`_Scripts/Editor/DeviceTierWindow.cs`) |
| Tests | `_Scripts/Tests/Editor/DeviceTierTests.cs` |

**Classification** (thresholds and the GPU list are authored on `PlatformProfiles.asset`):

| Device | Tier |
|---|---|
| Not a handheld (`SystemInfo.deviceType`, the same signal `InputController` picks touch from) | Desktop |
| iOS with < 2,500 MB RAM (the 2 GB iPhones iOS 15 still supports) | MobileLow |
| iOS otherwise | MobileHigh |
| Android with < 5,000 MB RAM (every 4 GB phone) | MobileLow |
| Android with ≥ 5,000 MB and a GPU matching a high-end pattern (Adreno 640+, Mali-G76/G77/G78/G7xx+, Immortalis, Xclipse) | MobileHigh |
| Android otherwise, including an unrecognised GPU name | MobileLow (conservative: add the reported name to the list) |

**What changed on which platform in Step 3:** Windows — nothing (Desktop keeps the heuristic).
iPhone — nothing (MobileHigh keeps the heuristic). MobileLow devices — first-run graphics become
Very Low preset, a 1.3 M pixel budget (~71% render scale on a 1080x2400 phone), Linear upscaling
instead of FSR, FXAA, 60 fps cap; an existing install is re-seeded once if its settings are still
the old auto-detected ones. Nothing else reads the tier yet; Steps 4–5 add render and content
fields to `PlatformProfileSO`.

---

## 4. Step plan

Each step is its own PR into bleeding-edge, and each leaves Windows unchanged unless it says so.

| # | Step | Touches | Windows | iOS | Android |
|---|---|---|---|---|---|
| 0 | **Measure** (deferred, not a gate). Development builds on the Samsung and the iPhone; `DiagnosticsHUD` bound verdict + main-thread ms; Garrett's branch on the same Samsung; exact model. | nothing | — | — | — |
| 1 | **Android build plumbing.** Your two Gradle commits (`0f6b38ba5`, `359ad3d1b`; the namespace fix lives OUTSIDE the EDM4U block, the durable version of the same fix Garrett made inside it). Then decide: ARM64-only, R8 minify + Garrett's `proguard-user.txt` keep rules (the WorkManager crash came from Unity Ads, which your branch removes). Graphics APIs stay Auto (decided). | ProjectSettings (Android only), `Assets/Plugins/Android/*` | none | none | builds |
| 2 | ✅ *(landed on this branch, unverified in editor)* **Touch controls into bleeding-edge, ungated.** `TouchInputStrategy` (physical-size stick + dead zone, one-thumb mirror, re-zero on lift, throttle carry, events on lift only, 75/25 curve) + touch-only vessel tuning (`touchNoseResponse`, gated to the local human pilot) + binary drift for any unmeasured trigger + the ability-dispatch hardening (§2.2). Not the Squirrel `boostLoopEvent` clear. | `Controller/IO`, `VesselTransformer`, Squirrel/Butterfly prefabs | none (touch only) | **new controls** | **new controls** |
| 3 | ✅ *(landed on this branch, unverified in editor; see §3.4)* **Device tier foundation.** `DeviceTierClassifier`, `PlatformProfileSO` ×3, dev override, a `CSLogChannel` for it, and a mobile branch in `SettingsAutoDetector` that reads the tier. `Desktop` profile = today's behaviour. | `System/`, `Controller/Settings` | identical | correct tier | correct tier |
| 4 | **Render tier.** `URP_Mobile.asset` + mobile quality level; baked sky (`StaticSkyPanorama`), mesh membrane, post/AA policy, crystal LDR brightness, fold-gate window cap — each selected by the profile. | `_Graphics`, profile | none | per `MobileHigh` | per `MobileLow` |
| 5 | **Content tier.** Every `PerfStrip` gate becomes a profile read: trail policy, ecology, toybox, menu-UI teardown, Wander/conveyor budgets as per-tier overrides (not edits to the shared SO). The race trail cap only with decision 4 below. | gameplay | none | per `MobileHigh` | per `MobileLow` |
| 6 | **Platform-agnostic fixes** Garrett found, merged ungated (§2.6). Can go any time. | various | yes (fixes) | yes | yes |
| 7 | **Retire the branches.** Build all three platforms from bleeding-edge; device verification matrix. | — | — | — | — |

Open decisions (needed before steps 3–5):

1. **Minimum Android spec.** A phone with only Cortex-A55 cores may not reach 30 fps even fully
   stripped (Garrett targeted "mid-tier, years-old"), and that is truer now that cell life and the
   full toybox stay on every tier. Pick a floor, or accept 20–30 fps there.
2. **What `MobileHigh` gets.** Recommended: full content + touch controls (= what iOS runs today)
   plus only the free wins (none of the content strips).
3. **Where flagship Android lands.** Recommended: by the same capability test as iOS, so a
   Galaxy S2x is `MobileHigh`.
4. **The race trail cap on phones.** Skim Race and Joust on the strip cap each vessel's trail and
   consume the oldest prism, which the LOCKED rule forbids (§2.7.1). Either the design owner signs
   off a recorded exception in `Docs/ECOSYSTEM.md` §0, or `MobileLow` uses the sanctioned levers
   (pause the spawner, fauna cleanup) instead.
