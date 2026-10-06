# One codebase for Windows, iOS and Android — diagnosis and plan

**Status (2026-10-05): diagnosis (§1) and inventory (§2) done. Steps 2 (touch controls), 3 (device
tiers), 4 (render tier), 5 (content tier) and 6 (platform-agnostic fixes) landed on this branch,
awaiting editor/device verification (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, top five entries). The
whole branch's runtime C# compiles with 0 project errors against real Unity references
(`Tools/Build/unity_refcompile`, §3.7). Device measurements are
deferred, not a gate (owner's call).**

### Decisions recorded (2026-10-05, project owner)

These strip changes are **NOT ported**, on any tier:

| Not ported | Strip mechanism |
|---|---|
| Plants and creatures paused everywhere except the home cell (Garland) | `PerfStrip.CellLifeRuns` in `CellLifeSpawnerBase` and `Flora` |
| The toybox cut down to the light toys | `PerfStrip.LightToysOnly` in `ToyboxController`, and the Ark gate in `WanderToy` |
| Vulkan dropped for OpenGL ES only | Android `m_BuildTargetGraphicsAPIs` (Auto [Vulkan, GLES3] stays) |

Ported for phones on the owner's word ("step 5 with Garrett's implementation for phones"): the Skim
Race / Joust trail cap, as a `MobileLow`-only exception to the no-trail-cap law, recorded in
`Docs/ECOSYSTEM.md` §0 (decision 4 below, §3.6).
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
| `GetTriggerSum` / `DriftAudioController`: a drift with no measured trigger travel is a full pull (touch lift, key, digital-trigger pad) | **ungated, all devices** | **not ported** (reverted in the 2026-10-06 ship review). Touch and keyboard already drift at a full pull on bleeding-edge; the change only reaches GAMEPADS, and there the "digital trigger" case cannot occur (`GamepadInputStrategy` raises the drift only above 0.05 travel). What it did reach: a party client's release window (the release arrives by RPC while the trigger already reads 0, so the drift surged to full for a round trip) and AI pilots, which read `Gamepad` on a PC with a pad connected and drift at trigger 0 |
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
| Vessels lay **no trail**, except freestyle (pen waits while the cell holds > 10,000 prisms), the Wanderway tether, and Skim Race / Joust with a **FIFO cap** (oldest prism consumed past 2,000 / 1,200 per vessel, shared per seat) | `TrailsDisabled`, `CappedTrailActive` → **MobileLow, §3.6** (menu + freestyle + the race cap; not "trails off everywhere") |
| ~~Cell life (flora/fauna spawners, flora growth) paused except in the home world (Garland)~~ **decided: not ported** | `CellLifeRuns` |
| No cytoplasm motes in any cell | `SnowChanger.Initialize`, `Enabled` → **MobileLow, §3.6** |
| ~~Toybox: Wander (no Ark), domain changer, element charger, vessel changer~~ **decided: not ported** | `LightToysOnly` |
| Vessel changer roster narrowed to Squirrel + Butterfly | `ShipsVessel` → **not ported** (a feature removal, §3.6) |
| Wanderway belt 30,000 → 1,200 resident prisms, no lifeforms | `Wander_WithoutArk.asset` — **shared, ungated** → **MobileLow override, §3.6** |
| `MicrosceneConveyor.MaxConcurrentArrivals` 3 → 2 | **ungated** → **MobileLow override, §3.6** |
| Non-HOME menu screens deactivated; HOME + NavBar deactivated in freestyle | `MenuUIStripped` → **freestyle half on MobileLow, §3.6**; non-HOME screens not ported |
| Top-bar glow's endless DOFade dropped | `Enabled` → **MobileLow, §3.6** |

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

1. **The FIFO trail cap breaks a LOCKED rule.** *(Resolved 2026-10-05: the owner granted it for
   `MobileLow` only - decision 4, §3.6; the paragraph below is the analysis that asked for it.)*
   `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`:
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
   `PerfStrip.TrailsDisabled` becomes a profile read (as built, the fields have their own names:
   §3.4-§3.6 list them; there is no `TrailsDisabled`). The
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
instead of FSR, FXAA (4x MSAA since Step 4), 60 fps cap; an existing install is re-seeded once if its settings are still
the old auto-detected ones. Nothing else reads the tier yet; Steps 4–5 add render and content
fields to `PlatformProfileSO`.


### 3.5 As built (Step 4)

Every render field on `PlatformProfileSO` defaults to "no change", and the Desktop and MobileHigh
assets keep the defaults (pinned by `DeviceTierTests.DesktopAndMobileHigh_ChangeNothingAboutRendering`).

| MobileLow gets | How | Garrett's version (not taken) |
|---|---|---|
| **HDR off** | `disableHdr` → `GraphicsSettingsApplier.ApplyQuality` sets `urp.supportsHDR = false` (it never turns HDR on) | edited the shared `URP_Asset.asset`, which would have switched HDR off on every platform |
| **4x MSAA** (was FXAA in Step 3) | first-run recommendation on `PlatformProfile_MobileLow.asset` | 4x MSAA + FXAA on the shared asset / per camera. With HDR off, 4x MSAA fits a tile GPU's on-chip memory (Garrett's Round 8) |
| **Baked HyperSea sky** (one texture sample per pixel instead of the 767-line procedural shader) | `skyboxReplacements`: `HyperSeaSkybox.mat` → the Resources path `PlatformSkyboxes/StaticHyperSeaSkybox`, loaded and applied on scene load by `PlatformRenderApplier` (Play mode only). A path, not a reference: every tier loads the whole profile set, so a reference kept the 4096x2048 bake resident on Windows and iPhones too (ship review; pinned by `ProfileSet_DoesNotHoldTheBakedSkyResident`). Assets and `Tools/Build/bake_static_skybox.py` (`--check` is OK against bleeding-edge's sky) taken as-is | `PerfStripRuntime` replaced every scene's sky and every camera's clear |
| **Membrane at 642 capsules instead of 2,562** | `membraneMaxSubdivisions: 3` → `CapsuleMembrane` draws the first 642 baked capsules. The icosphere generator only appends, so that prefix IS a level-3 membrane (same seed, same jitter, same bake): proven by running the real generator, pinned by `Icosphere_LowerLevelIsAPrefixOfHigherLevel` | swapped 13 shared cell configs to an opaque inward-facing `MeshMembrane`. Opaque hides everything past the radius from inside (it needed a special larger membrane for Skim Race intensity 3) and would have changed those 10+ modes on every platform. Still available as a design choice if the lattice look is not wanted on phones |
| **Fold-gate window render capped at 0.5** | `foldGateWindowMaxRenderScale` → `FoldGatePortalView` | `PerfStrip.FoldGateWindowMaxRenderScale` |

**Changed on every platform (platform-agnostic, ported with the cap):** the Butterfly's fold-gate
window now renders only its own on-screen FOOTPRINT (projection cropped to the window's rectangle,
target sized to it, `_FoldGatePortalUV` remap in `FoldGatePortal.shader`), and its render target is no
longer reallocated every frame: the format check compared the requested format against what
`DefaultHDR` resolved to, and - found at ship review, in Garrett's code too - the reuse test rejected the
target the allocation had just made at the 32-texel floor, so every DISTANT gate reallocated every
frame (`TargetFits` / `TargetSize`, pinned by `FoldGateTarget_AFreshTargetAlwaysFitsItsOwnFootprint`).
The window renders HDR only when the pipeline does (MobileLow's HDR-off asset gets an LDR window). Visually identical by construction: the crop maps viewport u to
(u - xMin) / width, which is exactly the shader's `uv * (1/w) - xMin/w`. One operand order was
changed from Garrett's code (`row3 * cx` for `cx * row3`) so the Froglet Engine compiles it too.

**Editor hygiene:** `UrpAssetPlayModeRestore` (Editor) snapshots the URP asset's HDR, render scale,
MSAA and upscaler when Play starts and restores them when it ends, so simulating MobileLow cannot
leave `URP_Asset.asset` with HDR off.

**Not ported, and why:**

- **Crystal LDR brightening (22 shared materials).** It was made while post-processing was OFF on
  the strip; Garrett restored post (bloom threshold 0.2 / clamp 0.5, which needs no HDR) later. With
  post on, an LDR crystal colour looks the same with HDR on or off; only values above 1 clamp. Revisit
  only if crystals read dim on the phone.
- **Post-processing policy.** The strip's win was taking post off cameras that render into textures;
  on bleeding-edge the picture-in-picture camera is retired and the fold-gate camera forces post off
  itself (ported above). Bloom and the Panini half of the speed tunnel stay on every tier.
- **Renderer intermediate texture Always → Auto.** At MobileLow's ~71% render scale URP needs the
  intermediate texture anyway, so it buys nothing there.
- **Render scale 0.8 on the asset.** Replaced by the tier's pixel budget (Step 3).

**Build size and memory:** the baked sky (4096x2048, no mips) lives under `Resources/`, so it ships
in every platform's build (~4 MB compressed), including Windows where it is unused - but it is LOADED
only on a tier whose profile swaps to it.

### 3.6 As built (Step 5)

Every content field on `PlatformProfileSO` defaults to "no change", and the Desktop and MobileHigh
assets keep the defaults (pinned by `DeviceTierTests.DesktopAndMobileHigh_ChangeNothingAboutContent`),
so Windows and iOS run exactly what bleeding-edge ran. MobileLow carries Garrett's numbers (pinned by
`MobileLow_RunsTheStripsContentNumbers`).

| MobileLow gets | How | Garrett's version |
|---|---|---|
| **Skim Race / Joust trail cap** (Skim Race 6,000 shared, 800–2,000 per vessel; Joust 4,000 shared, 400–1,200) | `skimRaceTrail` / `joustTrail` → `RaceTrailCap`, added by `SkimRaceController` / `JoustController` in `OnNetworkSpawn` only when the tier sets a budget. Every 0.2 s it holds each vessel's two ribbons at its share; the oldest prism withers (0.8 s, the tether's recipe) and returns to its pool. Seats = max(selected players, live vessels); selected players already counts AI backfill. Each ribbon is cut in one pass (`Trail.RemoveOldest(int)`, one re-index), and a prism eaten while it withers is never pool-returned. **An owner-authorized exception to the no-trail-cap law**, recorded in `Docs/ECOSYSTEM.md` §0 | same numbers and share formula; a FIFO inside `VesselPrismController` (a knob on the shared system) that `Prism.Consume`d the oldest: an implosion per prism, the object left destroyed-but-live (no memory back), and a shielded prism only lost its shield. |
| **Menu lava lamp lays no trail** | `menuAutopilotLaysNoTrail` → `MenuCrystalClickHandler` holds every vessel's trail creation (`VesselPrismController.SetTierHold`, its own bool beside the pen, so it never fights a fold, a painting or a cell swap) whenever the menu is not in freestyle, except while a mode preview runs (`ModePreviewSession.AnyActive`) - a preview shows a mode as it plays. Creation-side only: nothing laid is removed | `PerfStrip.TrailsDisabled` refused `StartSpawn` everywhere outside freestyle / race / tether (the strip shipped no other mode) |
| **Freestyle trail waits above 10,000 cell prisms**, resumes at 9,700 | `freestyleCellPrismBudget` / `Resume` → the same handler, once a second, per cell with hysteresis. Never during a Wanderway run (`WanderwayRun.AnyRunning`): the tether needs a trail - re-evaluated the frame a run or preview starts or stops, not a second later. A spawner that waits is the sanctioned lever, not an exception | same numbers, checked inside the spawn loop |
| **Menu screens and nav bar DEACTIVATED while flying** | `deactivateMenuWhileFlying` → `ScreenSwitcher`: on the enter blend's end it switches off every active screen root EXCEPT HOME, and the nav bar, and switches exactly those back on at the start of the exit, before the fade-in. HOME stays live because it carries listeners a flight must not silence (ship review): the party-invite popup subscribes in `OnEnable`, and `HomeScreen` subscribes to profile changes in `Start` but drops them in `OnDisable`. `FlipUI` re-applies the last phone flip when re-enabled | HOME + nav bar in flight, plus every non-HOME screen disabled for the whole session (not ported: those screens are features). Garrett's build was offline, so it had no invites to miss |
| **HUD domain glow rests instead of breathing** | `quietScoreGlow` → `DomainScorePanel.ArmGlow`; the score-change punch still plays | same |
| **No cytoplasm motes** | `disableCytoplasm` → `SnowChanger.Initialize` | same |
| **Wanderway belt 30,000 → 1,200 resident prisms, no lifeform scenes, 4 ahead, 2 crystals, 2 arrivals at once** | `wanderwayBudget` → `WanderToy.Configure` writes it onto the BUILT `ConveyorConfig`; the settings asset is untouched. `MaxConcurrentArrivals` moved from a constant onto `ConveyorConfig` (default 3) for it | edited the shared `Wander_WithoutArk.asset` and the constant, so every platform got the small belt |

**Not ported, and why:**

- **Every non-HOME menu screen disabled** (`MenuUIStripped` at `Awake`) and **the vessel changer
  narrowed to Squirrel + Butterfly** (`ShipsVessel`). Both remove features rather than cost; on
  this branch every tier keeps every screen and hull. Say so if phones should lose either.
- **Arcade cards hidden when their scene is not in the build** (`IsLaunchableInThisBuild`). A
  safety net for a 7-scene build; bleeding-edge ships every scene. Belongs with Step 6 if wanted.
- **Trails off everywhere else.** The strip shipped only Menu_Main, Skim Race, Joust and Waystation;
  every other mode is trail gameplay, so "no trail outside the exceptions" does not translate.
  On this branch the phone tier changes only the menu's trail and the two races'.
- **`RestoreHomeWorld` after a wander** and **`WanderToy`'s Ark gate**: both existed because the
  strip cut the Cell Selector toy and the Ark (the toybox decision, not ported).

**Mixed-device matches:** trail prisms are local to each peer, so the race cap is per device. In a
phone-vs-PC race the phone's ribbons end a lap or two back while the PC still draws (and can skim)
the whole trail. Accepted with the exception (`Docs/ECOSYSTEM.md` §0).

### 3.7 As built (Step 6)

These change every platform, Windows included - they are fixes, not tiers.

| Fix | What Windows notices |
|---|---|
| **`VesselTransformer.DecayBoost` raises `boostChanged` only when the multiplier moved** since this transformer last raised it (Garrett's change), and re-raises once after `Initialize` / `ResetTransformer`, which write the multiplier without raising. The channel is global and `DecayBoost` runs for every vessel on every peer, so at rest each vessel used to fan out to every vessel's HUD and boost audio every frame | nothing: the HUD and the boost audio get the same values, just not the same value again 60-240 times a second. Only the Squirrel authors `decayBoost: 1`, and it is the only vessel with `boostChanged` listeners (its HUD and boost audio); every other writer of its multiplier (skim boost, reset-boost) raises the event itself. The silent writers (`GrowSkimmer`, `RampBoost`, Manta's turn boost) are on vessels that never run `DecayBoost` |
| **`ProximityBoostAudioController.minTickInterval` 0.07 s**: the Squirrel's skim-tick one-shot fires at most ~14 times a second | dense skimming reads as rapid clicks instead of a buzz. The buzz scaled with frame rate, so a 144-240 Hz PC had it worst. The loop layer and the boost itself are untouched |
| **The `[Obsolete]` `SkimmerFXPrismEffect` beam dropped from `SquirrelSkimmerImpactorDataContainer`** (owner's decision, 2026-10-06; closes `Docs/ElementalAbilitySystem/BACKLOG.md` item 21) | the Squirrel no longer draws a beam to every prism it skims; the forcefield crackle is its only skim visual, as on the Dolphin. Skim boost, steal and haptics are separate entries in the same container and unchanged |
| `FoldGatePortalView` render target no longer reallocated every frame | already landed with Step 4 (§3.5) |

**Not ported, and why:**

- **The "silent above 90% boost" half of the tick change** (`maxTickNormalized`). A tuning call for
  audio sign-off, as §2.6 said.
- **`ArcadeExploreView.IsLaunchableInThisBuild`** (cards hidden when their scene is not in the
  build). Bleeding-edge gates the same thing at build time - `Tools/Build/check_gamelist_scenes.py`
  fails on any card naming an unloadable scene, and every list is green - and the runtime filter
  would hide in-progress modes in the Editor whenever their scene is not in the build list yet.
- **`DiagnosticsHUD`'s qualified base class**: bleeding-edge already keeps `using UnityEngine`
  outside the `#if`.
- **`SkimRaceController.OnDestroy`**: the trail cap here needs no such override (`RaceTrailCap` is
  its own component), so there is nothing to hide.
- **`SquirrelImpactorDataContainer`'s per-element crystal lists** (the strip filled all four with
  one effect): a gameplay change, not a fix; not in this inventory.

**Verification:** `bash Tools/Build/unity_refcompile/run.sh` (real Unity 6000.0 reference
assemblies + every package at its locked source) reports **0 errors in project code across 91
player assemblies** with Steps 2-6 in place; a call to a missing member planted in `RaceTrailCap`
fails it with CS1061, tagged `[CHANGED-TONIGHT]`. The approximate `--config editor` run reports 4
errors, none in this branch's files: they are `'Editor' is a namespace` in three runtime
`#if UNITY_EDITOR` files, which appear only because that config compiles changed Editor-folder files
(declaring `namespace CosmicShore.Editor`) into the runtime compilation. Unity compiles those into
Assembly-CSharp-Editor, which runtime code cannot see.

### 3.8 Ship review (2026-10-06)

Three independent adversarial reviews of Steps 2-6 found two blockers and a set of should-fixes, all
fixed on this branch before the PR:

| Found | Fixed by |
|---|---|
| **Blocker:** the Step 2 "binary drift for an unmeasured trigger" changed GAMEPAD drift on every platform - a party client's drift surged to full for a round trip at every release, and AI drift on a PC with a pad attached ran at full instead of inert. The "digital-trigger pad" it was for cannot occur | reverted: `GetTriggerSum` and `DriftAudioController` are byte-identical to bleeding-edge (§2.2) |
| **Blocker:** fold-gate target reallocated every frame for distant gates (above, §3.5) | `TargetFits` / `TargetSize` + test |
| Baked sky resident on Windows and iPhones through the profile set | Resources path, loaded on swap; test |
| Fold-gate window HDR on a pipeline with HDR off | format follows the pipeline |
| Menu teardown silenced party invites / profile updates / phone flips | HOME stays active; `FlipUI` re-syncs on enable |
| Mode previews laid no trail on MobileLow | previews exempt; exemptions apply the same frame |
| Race cap could pool-return a prism eaten mid-wither, re-indexed a ribbon per prism, stalled silently | release re-checks; one cut per ribbon + `ProfilerMarker`; a stall warns |
| A 3→1 thumb lift fired the one-thumb ability without re-zeroing | re-zero on every one↔several change |
| Dying hull could re-subscribe to the button channel between despawn and destroy | `DetachInputPause` in `OnNetworkDespawn` |
| Smaller: settings v3 re-seed under a SIMULATED tier, sky swap outside Play, URP restore covering only the default asset, raw `Debug.LogError`, diagnostics work surviving release builds, a near-zero `Screen.dpi` dividing by zero, stale comments | each fixed in place |

**Follow-ups and debt (rows, not fixed here):**

- **`QuestTrackView` rebuilds every quest card on `OnEnable`** (ProfileScreen), so on MobileLow each
  freestyle exit pays that rebuild. Measure on the Samsung; if it hitches, make the rebuild lazy or
  keep ProfileScreen active too.
- **Two copies of the destroyed-safe player → trail-controller lookup** (`RaceTrailCap.ControllerOf`,
  `MenuCrystalClickHandler.TrailControllerOf`). Fold into one helper on the player side if a third
  caller appears.
- **`SkimmerFXPrismEffectSO` is now referenced by no live container** - `Docs/ElementalAbilitySystem/BACKLOG.md`
  items 21-22 own its deletion.
- **The tick change's other half** (`maxTickNormalized`, silence above 90% boost) waits on audio sign-off (§3.7).
- **The Ability diagnostics rows filter on `IsLocalUser`**, so the non-networked legacy spawn shows none.
- **A Wanderway run still flies up to 0.2 s under the lava-lamp hold on exit** (the run ends on its own
  next tick). Accepted: the run is ending.
- **Pre-existing, found in passing (task suggested):** AI Squirrels never drift on a PC -
  `SkimRacePilot` resolves the drift's TOUCH input, which the PC's gamepad/keyboard overrides reject.
- **Tooling (task suggested):** `unity_refcompile --config editor` false positives, recorded in its README.
---

## 4. Step plan

Each step is its own PR into bleeding-edge, and each leaves Windows unchanged unless it says so.

| # | Step | Touches | Windows | iOS | Android |
|---|---|---|---|---|---|
| 0 | **Measure** (deferred, not a gate). Development builds on the Samsung and the iPhone; `DiagnosticsHUD` bound verdict + main-thread ms; Garrett's branch on the same Samsung; exact model. | nothing | — | — | — |
| 1 | **Android build plumbing.** Your two Gradle commits (`0f6b38ba5`, `359ad3d1b`; the namespace fix lives OUTSIDE the EDM4U block, the durable version of the same fix Garrett made inside it). Then decide: ARM64-only, R8 minify + Garrett's `proguard-user.txt` keep rules (the WorkManager crash came from Unity Ads, which your branch removes). Graphics APIs stay Auto (decided). | ProjectSettings (Android only), `Assets/Plugins/Android/*` | none | none | builds |
| 2 | ✅ *(landed on this branch, unverified in editor)* **Touch controls into bleeding-edge, ungated.** `TouchInputStrategy` (physical-size stick + dead zone, one-thumb mirror, re-zero on lift, throttle carry, events on lift only, 75/25 curve) + touch-only vessel tuning (`touchNoseResponse`, gated to the local human pilot) + the ability-dispatch hardening (§2.2). Not the Squirrel `boostLoopEvent` clear, and not the gamepad half of the binary-drift change (reverted at ship review, §2.2). | `Controller/IO`, `VesselTransformer`, Squirrel/Butterfly prefabs | none intended: the touch changes are touch-only; the ability-subscription reconcile runs on every device and only re-asserts the subscription the pause state already implies | **new controls** | **new controls** |
| 3 | ✅ *(landed on this branch, unverified in editor; see §3.4)* **Device tier foundation.** `DeviceTierClassifier`, `PlatformProfileSO` ×3, dev override, a `CSLogChannel` for it, and a mobile branch in `SettingsAutoDetector` that reads the tier. `Desktop` profile = today's behaviour. | `System/`, `Controller/Settings` | identical | correct tier | correct tier |
| 4 | ✅ *(landed on this branch, unverified in editor; see §3.5)* **Render tier.** MobileLow: HDR off, 4x MSAA, baked sky, membrane capped at 642 capsules, fold-gate window capped at 0.5 — each a `PlatformProfileSO` field. Everywhere: the fold-gate window renders only its footprint. | `_Graphics`, profile, `CapsuleMembrane`, `FoldGatePortalView` | fold-gate footprint only | none | per `MobileLow` |
| 5 | ✅ *(landed on this branch, unverified in editor; see §3.6)* **Content tier.** Every `PerfStrip` gate the owner kept becomes a profile read: menu/freestyle trail policy, the Skim Race / Joust trail cap (decision 4: granted), menu-UI teardown while flying, HUD glow, cytoplasm, Wander/conveyor budgets as per-tier overrides (not edits to the shared SO). | gameplay | none | none (`MobileHigh` sets nothing) | per `MobileLow` |
| 6 | ✅ *(landed on this branch, unverified in editor; see §3.7)* **Platform-agnostic fixes** Garrett found, merged ungated (§2.6): the boost event quiet at rest, the skim-tick rate limit, the Squirrel's obsolete beam retired (owner's call). | various | yes (fixes) | yes | yes |
| 7 | **Retire the branches.** Build all three platforms from bleeding-edge; device verification matrix. | — | — | — | — |

Open decisions (needed before steps 3–5):

1. **Minimum Android spec.** A phone with only Cortex-A55 cores may not reach 30 fps even fully
   stripped (Garrett targeted "mid-tier, years-old"), and that is truer now that cell life and the
   full toybox stay on every tier. Pick a floor, or accept 20–30 fps there.
2. **What `MobileHigh` gets.** Recommended: full content + touch controls (= what iOS runs today)
   plus only the free wins (none of the content strips).
3. **Where flagship Android lands.** Recommended: by the same capability test as iOS, so a
   Galaxy S2x is `MobileHigh`.
4. ~~**The race trail cap on phones.**~~ **Resolved 2026-10-05: granted for `MobileLow`.** Skim
   Race and Joust on the strip cap each vessel's trail and consume the oldest prism, which the
   LOCKED rule forbids (§2.7.1). The owner asked for Garrett's implementation on phones; it is
   recorded as the second authorized exception in `Docs/ECOSYSTEM.md` §0 and fenced to that tier
   and those two modes (§3.6).
