# One codebase for Windows, iOS and Android — diagnosis and plan

**Status (2026-10-05): Step 0 (diagnosis + inventory) done. Nothing ported yet.**
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

*Pending: the file-by-file classification of
`origin/bleeding-edge...origin/claude/android-performance-stripped-dap5z2` (129 files) lands
here next.* The branch's own 18-round changelog is `Docs/MOBILE_STRIPPED.md` on that branch.

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

---

## 4. Step plan

Each step is its own PR into bleeding-edge, and each leaves Windows unchanged unless it says so.

| # | Step | Touches | Windows | iOS | Android |
|---|---|---|---|---|---|
| 0 | **Measure.** (a) Development build of `claude/eloquent-meitner-9e4u2a` on the Samsung and the iPhone; read `DiagnosticsHUD` (bound verdict, main-thread ms). (b) Build Garrett's branch on the SAME Samsung: what the full strip buys on this hardware decides the minimum-spec question below. Record the exact Samsung model. | nothing | — | — | — |
| 1 | **Android build plumbing.** Your two Gradle commits (`0f6b38ba5`, `359ad3d1b`; the namespace fix lives OUTSIDE the EDM4U block, the durable version of the same fix Garrett made inside it). Then decide: ARM64-only, R8 minify + Garrett's `proguard-user.txt` keep rules (the WorkManager crash came from Unity Ads, which your branch removes), Vulkan vs GLES3. | ProjectSettings (Android only), `Assets/Plugins/Android/*` | none | none | builds |
| 2 | **Touch controls into bleeding-edge, ungated.** `TouchInputStrategy` (physical-size stick + dead zone, one-thumb mirror, re-zero on lift, throttle carry, events on lift only, 75/25 curve) + touch-only vessel tuning (`touchNoseResponse`, touch action overrides, binary drift on a thumb lift) + the ability-dispatch hardening. | `Controller/IO`, `VesselTransformer`, Squirrel/Butterfly prefabs | none (touch only) | **new controls** | **new controls** |
| 3 | **Device tier foundation.** `DeviceTierClassifier`, `PlatformProfileSO` ×3, dev override, a `CSLogChannel` for it, and a mobile branch in `SettingsAutoDetector` that reads the tier. `Desktop` profile = today's behaviour. | `System/`, `Controller/Settings` | identical | correct tier | correct tier |
| 4 | **Render tier.** `URP_Mobile.asset` + mobile quality level; baked sky (`StaticSkyPanorama`), mesh membrane, post/AA policy, crystal LDR brightness, fold-gate window cap — each selected by the profile. | `_Graphics`, profile | none | per `MobileHigh` | per `MobileLow` |
| 5 | **Content tier.** Every `PerfStrip` gate becomes a profile read: trail policy, ecology, toybox, menu-UI teardown, Wander/conveyor budgets as per-tier overrides (not edits to the shared SO). | gameplay | none | per `MobileHigh` | per `MobileLow` |
| 6 | **Platform-agnostic fixes** Garrett found, merged ungated (see §2, category G). | various | yes (fixes) | yes | yes |
| 7 | **Retire the branches.** Build all three platforms from bleeding-edge; device verification matrix. | — | — | — | — |

Open decisions (needed before steps 3–5):

1. **Minimum Android spec.** A phone with only Cortex-A55 cores may not reach 30 fps even fully
   stripped (Garrett targeted "mid-tier, years-old"). Pick a floor, or accept 20–30 fps there.
2. **What `MobileHigh` gets.** Recommended: full content + touch controls (= what iOS runs today)
   plus only the free wins (none of the content strips).
3. **Where flagship Android lands.** Recommended: by the same capability test as iOS, so a
   Galaxy S2x is `MobileHigh`.
