# Branch archive: `claude/audio-matched-haptics-0r6ib8`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-17 by Claude
- **Unmerged commits:** 3
- **Forked from:** `1f558502a` (2026-07-17, Update ProjectSettings.asset)
- **Tip:** `39de3192d`
- **Files touched (43):**
  - `Assets/Resources/ElementalBarsConfig.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/SquirrelSkimmerImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Vessel Prism Effects/SquirrelVesselHapticsByPrismEffect.asset`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs`
  - `Assets/_Scripts/Controller/IO/HapticController.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs`
  - `Assets/_Scripts/Controller/Vessel/SilhouetteController.cs`
  - `Assets/_Scripts/Editor/AudioHapticsBaker.cs`
  - `Assets/_Scripts/Editor/AudioHapticsBaker.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs`
  - `Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs`
  - `Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs.meta`
  - `Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs`
  - `Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs.meta`
  - `Assets/_Scripts/System/Audio/AudioSystem.cs`
  - `Assets/_Scripts/System/Audio/FmodSfxBusMeter.cs`
  - `Assets/_Scripts/System/Audio/FmodSfxBusMeter.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/AudioHapticsBakedDefaultsTests.cs`
  - `Assets/_Scripts/Tests/EditMode/AudioHapticsBakedDefaultsTests.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/HapticEnvelopeFollowerTests.cs`
  - `Assets/_Scripts/Tests/EditMode/HapticEnvelopeFollowerTests.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/HapticPatternBuilderTests.cs`
  - `Assets/_Scripts/Tests/EditMode/HapticPatternBuilderTests.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/HapticTransientArbiterTests.cs`
  - `Assets/_Scripts/Tests/EditMode/HapticTransientArbiterTests.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/HapticWaveformAnalyzerTests.cs`
  - `Assets/_Scripts/Tests/EditMode/HapticWaveformAnalyzerTests.cs.meta`
  - `Assets/_Scripts/UI/View/ElementalBarsView.cs`
  - `Assets/_Scripts/Utility/Audio.meta`
  - `Assets/_Scripts/Utility/Audio/HapticEnvelopeFollower.cs`
  - `Assets/_Scripts/Utility/Audio/HapticEnvelopeFollower.cs.meta`
  - `Assets/_Scripts/Utility/Audio/HapticPatternBuilder.cs`
  - `Assets/_Scripts/Utility/Audio/HapticPatternBuilder.cs.meta`
  - `Assets/_Scripts/Utility/Audio/HapticTransientArbiter.cs`
  - `Assets/_Scripts/Utility/Audio/HapticTransientArbiter.cs.meta`
  - `Assets/_Scripts/Utility/Audio/HapticWaveformAnalyzer.cs`
  - … and 3 more

### `2d831ba62` — feat(haptics): audio-matched haptics — waveform-baked transients + FMOD-metered continuous bed

_Claude, 2026-07-17 03:01:28 +0000_

```text
Replace the preset-based haptics with a system that tracks the audio
experience itself, two layers arbitrated onto the single Lofelt channel:

- Transients: per-category envelopes MEASURED from each SFX's source
  waveform in the FMOD Studio project (windowed-peak amplitude, ZCR
  frequency, attack-flux emphasis), fired from AudioSystem at the same
  call that starts the FMOD one-shot, with the category's audio gain and
  listener-distance falloff for spatialized events. Shipped as generated
  code defaults (AudioHapticsBakedDefaults) covering every
  GameplaySFXCategory + MenuAudioCategory — zero asset wiring.
- Continuous bed: envelope follower on live FMOD SFX-bus input metering
  (RMS) + one FFT DSP (SPECTRAL_CENTROID) so engine hum, drift layers,
  boost swells and one-shot tails all translate to touch with no
  per-emitter wiring. iOS/gamepads modulate a looping clip in real time;
  Android re-issues short chunks (no realtime modulation); old devices
  degrade to strength-matched presets.
- HapticTransientArbiter: one haptic channel — priorities, per-category
  cooldowns, global interval, tail takeover; the bed yields to
  transients and resumes to carry the measured tail.

Also:
- HapticController facade keeps its API; PlayConstant now drives bed
  pulses (4 slots) instead of thrashing the channel with per-frame clip
  loads; new world-position overload distance-attenuates.
- Fix haptic leaks from remote players (HapticSpec, skimmer scale,
  overtake, elemental bars now gate on IsLocalUser; AstroLeague ball
  rumble is distance-attenuated instead of full-strength on every peer).
- AudioHapticsConfigSO: every tunable in one asset (optional —
  Resources/AudioHapticsConfig), plus Tools > Cosmic Shore > Audio
  Haptics editor menu to author the asset and re-bake envelopes from the
  FMOD project source wavs (chunk-walking WAV reader).
- Legacy PlaySFXClip(AudioClip) path analyzes clip waveforms at runtime
  (cached) so unmigrated sounds get matched haptics too.
- Edit-mode tests: pattern rendering/JSON format, follower DSP,
  arbitration policy, analyzer on synthetic signals, full-enum coverage
  of baked defaults.
- Docs/HapticsSystem/ARCHITECTURE.md + CLAUDE.md index entries.
```

```text
 Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs             | 619 ++++++++++++++++++++++++++++++
 Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs.meta        |  11 +
 Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs              | 660 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs.meta         |  11 +
 Assets/_Scripts/System/Audio/AudioSystem.cs                           |  48 +++
 Assets/_Scripts/System/Audio/FmodSfxBusMeter.cs                       | 220 +++++++++++
 Assets/_Scripts/System/Audio/FmodSfxBusMeter.cs.meta                  |  11 +
 Assets/_Scripts/Tests/EditMode/AudioHapticsBakedDefaultsTests.cs      | 141 +++++++
 Assets/_Scripts/Tests/EditMode/AudioHapticsBakedDefaultsTests.cs.meta |  11 +
 Assets/_Scripts/Tests/EditMode/HapticEnvelopeFollowerTests.cs         | 154 ++++++++
 Assets/_Scripts/Tests/EditMode/HapticEnvelopeFollowerTests.cs.meta    |  11 +
 Assets/_Scripts/Tests/EditMode/HapticPatternBuilderTests.cs           | 254 ++++++++++++
 Assets/_Scripts/Tests/EditMode/HapticPatternBuilderTests.cs.meta      |  11 +
 Assets/_Scripts/Tests/EditMode/HapticTransientArbiterTests.cs         | 151 ++++++++
 Assets/_Scripts/Tests/EditMode/HapticTransientArbiterTests.cs.meta    |  11 +
 Assets/_Scripts/Tests/EditMode/HapticWaveformAnalyzerTests.cs         | 151 ++++++++
 Assets/_Scripts/Tests/EditMode/HapticWaveformAnalyzerTests.cs.meta    |  11 +
 Assets/_Scripts/UI/View/ElementalBarsView.cs                          |   9 +-
 Assets/_Scripts/Utility/Audio.meta                                    |   8 +
 Assets/_Scripts/Utility/Audio/HapticEnvelopeFollower.cs               | 122 ++++++
 Assets/_Scripts/Utility/Audio/HapticEnvelopeFollower.cs.meta          |  11 +
 Assets/_Scripts/Utility/Audio/HapticPatternBuilder.cs                 | 302 +++++++++++++++
 Assets/_Scripts/Utility/Audio/HapticPatternBuilder.cs.meta            |  11 +
 Assets/_Scripts/Utility/Audio/HapticTransientArbiter.cs               | 129 +++++++
 Assets/_Scripts/Utility/Audio/HapticTransientArbiter.cs.meta          |  11 +
 Assets/_Scripts/Utility/Audio/HapticWaveformAnalyzer.cs               | 285 ++++++++++++++
 Assets/_Scripts/Utility/Audio/HapticWaveformAnalyzer.cs.meta          |  11 +
 CLAUDE.md                                                             |   2 +
 Docs/HapticsSystem/ARCHITECTURE.md                                    | 132 +++++++
 39 files changed, 4133 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 4245 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
index 6a784f201..798d291b9 100644
--- a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
+++ b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
@@ -967,7 +967,7 @@ namespace CosmicShore.Gameplay
         {
             EmitBurst(position, Vector3.up, settings.goalParticleBurst);
             ShakeCamera(settings.goalShakeIntensity, settings.goalShakeDuration, position);
-            HapticController.PlayHaptic(HapticType.MineCollision);
+            HapticController.PlayHaptic(HapticType.MineCollision, position);
         }
 
         /// <summary>
@@ -980,7 +980,8 @@ namespace CosmicShore.Gameplay
             TriggerFlash(intensity * 0.6f);
             EmitBurst(position, normal, (int)(settings.impactParticleBurst * 0.5f * Mathf.Max(0.4f, intensity)));
             ShakeCamera(settings.strikeShakeIntensity * intensity * 0.35f, settings.strikeShakeDuration, position);
-            HapticController.PlayHaptic(HapticType.ShipCollision);
+            // Distance-attenuated: a carom across the arena is a murmur, not a slam on every peer.
+            HapticController.PlayHaptic(HapticType.ShipCollision, position);
         }
 
         void TriggerFlash(float intensity) =>
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index 2b336ae2e..f67fe2565 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -1,5 +1,4 @@
 using CosmicShore.Core;
-using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Lofelt.NiceVibrations;
 using Reflex.Attributes;
@@ -21,6 +20,14 @@ namespace CosmicShore.Gameplay
         MineCollision = 5,
     }
 
+    /// <summary>
+    /// Static gameplay-facing haptics facade. Calls route through
+    /// <see cref="AudioHapticsOrchestrator"/>, which plays envelopes measured
+    /// from the corresponding sound's actual waveform and arbitrates the single
+    /// haptic channel (see Docs/HapticsSystem/ARCHITECTURE.md). When no
+    /// orchestrator exists (tests, stripped scenes), calls degrade to the old
+    /// direct preset behavior so haptics never silently die.
+    /// </summary>
     public class HapticController : MonoBehaviour
     {
         [Inject] GameSetting injectedGameSetting;
@@ -29,35 +36,113 @@ namespace CosmicShore.Gameplay
         void Awake() => s_gameSetting = injectedGameSetting;
 
         /// <summary>
-        /// Play Haptic
-        /// Play haptic pattern presets when haptics are enabled.
+        /// Plays the audio-matched haptic for a gameplay haptic type at full
+        /// strength. Prefer the world-position overload for events that happen
+        /// somewhere in the world rather than "on" the player.
         /// </summary>
-        /// <param name="type">Haptic type</param>
         public static void PlayHaptic(HapticType type)
         {
-            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
+            if (type == HapticType.None) return;
+
+            var orchestrator = AudioHapticsOrchestrator.Instance;
+            if (orchestrator != null)
+            {
+                RouteThroughOrchestrator(orchestrator, type, null);
                 return;
+            }
 
-            Lofelt.NiceVibrations.HapticController.outputLevel = s_gameSetting.HapticsLevel;
+            PlayFallbackPreset(type);
+        }
+
+        /// <summary>
+        /// Plays the audio-matched haptic for an event at
+        /// <paramref name="worldPosition"/>, attenuated by listener distance the
+        /// same way the sound is — a far-off detonation is a murmur, not a slam.
+        /// </summary>
+        public static void PlayHaptic(HapticType type, Vector3 worldPosition)
+        {
+            if (type == HapticType.None) return;
 
-            var pattern = GetPatternForHapticType(type);
+            var orchestrator = AudioHapticsOrchestrator.Instance;
+            if (orchestrator != null)
+            {
+                RouteThroughOrchestrator(orchestrator, type, worldPosition);
+                return;
+            }
 
-            HapticPatterns.PlayPreset(pattern);
+            PlayFallbackPreset(type);
         }
 
+        /// <summary>
+        /// Continuous haptic drive (skim proximity scaling, elemental debuff
+        /// buzz). Mixed into the orchestrator's continuous bed rather than
+        /// reloading a clip per call, so repeated/per-frame callers no longer
+        /// evict one-shot haptics from the channel.
+        /// </summary>
         public static void PlayConstant(float amplitude, float frequency, float duration)
         {
-            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled)
+            var orchestrator = AudioHapticsOrchestrator.Instance;
+            if (orchestrator != null)
+            {
+                orchestrator.PulseContinuous(amplitude, frequency, duration);
+                return;
+            }
+
+            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
                 return;
             HapticPatterns.PlayConstant(amplitude, frequency, duration);
         }
 
+        /// <summary>
+        /// The legacy 5-type vocabulary maps onto the audio categories whose
+        /// baked envelopes carry the matching sound's real shape.
+        /// </summary>
+        static void RouteThroughOrchestrator(AudioHapticsOrchestrator orchestrator, HapticType type, Vector3? worldPosition)
+        {
+            switch (type)
+            {
+                case HapticType.ButtonPress:
+                    orchestrator.PlayMenuTransient(MenuAudioCategory.OptionClick);
+                    return;
+                case HapticType.PrismCollision:
+                    PlayGameplay(orchestrator, GameplaySFXCategory.CrystalSkim, worldPosition);
+                    return;
+                case HapticType.ShipCollision:
+                    PlayGameplay(orchestrator, GameplaySFXCategory.VesselImpact, worldPosition);
+                    return;
+                case HapticType.CrystalCollision:
+                    PlayGameplay(orchestrator, GameplaySFXCategory.CrystalCollect, worldPosition);
+                    return;
+                case HapticType.MineCollision:
+                    PlayGameplay(orchestrator, GameplaySFXCategory.MineExplode, worldPosition);
+                    return;
+                default:
+                    CSDebug.LogErrorFormat("{0} - {1} - Unsupported haptic type.", nameof(HapticController), nameof(RouteThroughOrchestrator));
+                    return;
+            }
+        }
+
+        static void PlayGameplay(AudioHapticsOrchestrator orchestrator, GameplaySFXCategory category, Vector3? worldPosition)
+        {
+            if (worldPosition.HasValue) orchestrator.PlayGameplayTransient(category, worldPosition.Value);
+            else orchestrator.PlayGameplayTransient(category);
```

</details>

### `2ba746a99` — fix(haptics): apply adversarial-review findings

_Claude, 2026-07-17 03:05:32 +0000_

```text
- Guarantee a zero-amplitude terminal breakpoint in HapticWaveformAnalyzer
  (sounds loud at their end — e.g. the rising boost whoosh — had none) and
  patch the generated BoostActivate table to match.
- Restore full-strength AstroLeague GOAL haptic on every peer (a goal is a
  match-wide moment like the replicated shake); wall bounces stay
  distance-attenuated.
- New HapticController.IsLocalPlayerVessel / IsLocalHumanPilot predicates:
  the IsLocalUser-only gates suppressed haptics for the local human in the
  non-networked single-player spawn path (Player never network-spawns
  there); the helpers cover both paths and exclude AI, and all four gated
  call sites now use them.
- Move legacy-clip mixing tunables (gain/priority/cooldown) into
  AudioHapticsConfigSO per the config-separation rule.
- Android: pulses shorter than the chunk cadence are extended so they
  can't expire unheard between ticks; OnValidate keeps chunk clips longer
  than the cadence.
- Test hygiene: arbiter global-interval test now also covers the admit
  case and documents the tail interaction; culture test drops a no-op
  Replace.
```

```text
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs             |  4 +++-
 Assets/_Scripts/Controller/IO/HapticController.cs                            | 26 ++++++++++++++++++++++++++
 Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs     | 11 ++++++-----
 .../Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs         |  2 +-
 .../EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs      |  7 ++++---
 Assets/_Scripts/Controller/Vessel/SilhouetteController.cs                    |  9 ++++++---
 Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs                    | 11 +++++++++++
 Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs                     | 18 ++++++++++--------
 8 files changed, 67 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 174 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
index 798d291b9..dbcfa57d9 100644
--- a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
+++ b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
@@ -967,7 +967,9 @@ namespace CosmicShore.Gameplay
         {
             EmitBurst(position, Vector3.up, settings.goalParticleBurst);
             ShakeCamera(settings.goalShakeIntensity, settings.goalShakeDuration, position);
-            HapticController.PlayHaptic(HapticType.MineCollision, position);
+            // Deliberately NOT distance-attenuated: a goal is a match-wide moment
+            // every peer must feel at full strength, like the replicated shake.
+            HapticController.PlayHaptic(HapticType.MineCollision);
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index f67fe2565..185d70fdf 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -2,6 +2,7 @@ using CosmicShore.Core;
 using CosmicShore.Utility;
 using Lofelt.NiceVibrations;
 using Reflex.Attributes;
+using Unity.Netcode;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
@@ -35,6 +36,31 @@ namespace CosmicShore.Gameplay
 
         void Awake() => s_gameSetting = injectedGameSetting;
 
+        /// <summary>
+        /// Whether this vessel belongs to the human holding THIS device — the
+        /// gate for every vessel-attributed haptic. Networked sessions use
+        /// IsLocalUser (owner + non-AI). The non-networked single-player path
+        /// (PlayerSpawner) never network-spawns its Players, so IsLocalUser is
+        /// structurally false there — but no remote humans can exist either, so
+        /// any non-AI player IS the local human.
+        /// </summary>
+        public static bool IsLocalPlayerVessel(IVesselStatus status)
+        {
+            var player = status?.Player;
+            if (player == null) return false;
+            if (player.IsLocalUser) return true;
+            bool networkSpawned = player is NetworkBehaviour networkBehaviour && networkBehaviour.IsSpawned;
+            return !networkSpawned && !player.IsInitializedAsAI;
+        }
+
+        /// <summary>
+        /// <see cref="IsLocalPlayerVessel"/> plus manual control — for haptics
+        /// that should only fire while the player is actually flying the vessel
+        /// (collision/skim feedback), not while it drifts on autopilot.
+        /// </summary>
+        public static bool IsLocalHumanPilot(IVesselStatus status)
+            => status != null && !status.AutoPilotEnabled && IsLocalPlayerVessel(status);
+
         /// <summary>
         /// Plays the audio-matched haptic for a gameplay haptic type at full
         /// strength. Prefer the world-position overload for events that happen
diff --git a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
index 28026782e..a15806160 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
@@ -10,11 +10,12 @@ namespace CosmicShore.Gameplay
 
         public void PlayIfManual(IVesselStatus status)
         {
-            if (status == null) return;
-            // Only the locally-piloted vessel may buzz this device — without the
-            // IsLocalUser gate, a REMOTE human's collisions (autopilot off) leak
-            // haptics onto every peer's hands.
-            if (!status.AutoPilotEnabled && status.IsLocalUser)
+            // Only the vessel the local human is actively flying may buzz this
+            // device — without the identity gate, a REMOTE human's collisions
+            // (autopilot off) leak haptics onto every peer's hands. The helper
+            // also covers the non-networked single-player spawn path, where
+            // IsLocalUser alone is structurally false.
+            if (HapticController.IsLocalHumanPilot(status))
                 HapticController.PlayHaptic(_type);
         }
     }
diff --git a/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs b/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
index cec885858..718a91389 100644
--- a/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
+++ b/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
@@ -257,9 +257,12 @@ namespace CosmicShore.Gameplay
                 elementBars = CreateDefaultElementBars();
             if (!elementBars) return;
 
-            // Every vessel gets the flower display, but only the local pilot's
-            // debuffs may vibrate this device.
-            elementBars.HapticsAllowed = _status?.IsLocalUser ?? true;
+            // Every vessel gets the flower display, but only the local player's
+            // vessel may vibrate this device on debuffs. Null status keeps the
+            // view's permissive default (matches pre-gate behavior for
+            // standalone/test usage).
+            if (_status != null)
+                elementBars.HapticsAllowed = HapticController.IsLocalPlayerVessel(_status);
 
             elementBars.Build();
 
diff --git a/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs b/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
index fd18e676d..ab5685f18 100644
--- a/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
@@ -162,6 +162,15 @@ namespace CosmicShore.ScriptableObjects
         [Tooltip("Envelope analysis settings used for legacy AudioClips (window, budget, transient detection). Fixed at code defaults; exposed here only for the master duration cap.")]
         [Min(0.2f)] public float legacyClipMaxSeconds = 1.5f;
 
+        [Tooltip("Gain on legacy-AudioClip haptics (no per-category authoring exists for arbitrary clips).")]
+        [Range(0f, 1f)] public float legacyClipGain = 0.6f;
+
+        [Tooltip("Arbitration priority for legacy-AudioClip haptics.")]
+        [Range(0, 100)] public int legacyClipPriority = 45;
+
+        [Tooltip("Minimum seconds between two haptics of the same legacy AudioClip.")]
+        [Min(0f)] public float legacyClipCooldown = 0.1f;
+
         HapticTransientArbiter.Settings _cachedArbiterSettings;
         bool _arbiterSettingsCached;
 
@@ -189,6 +198,8 @@ namespace CosmicShore.ScriptableObjects
             _arbiterSettingsCached = false;
             bedFftWindowSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(bedFftWindowSize, 128, 4096));
             spatialCutoffDistance = Mathf.Max(spatialCutoffDistance, spatialFullStrengthDistance + 1f);
+            // Chunks must outlive the cadence or the Android bed stutters with gaps.
+            androidChunkClipSeconds = Mathf.Max(androidChunkClipSeconds, androidChunkIntervalSeconds + 0.05f);
         }
 
         /// <summary>Finds the spec for a gameplay category; null when absent (caller falls back to baked defaults).</summary>
diff --git a/Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs b/Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs
index bef82f110..a2d6b8446 100644
--- a/Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs
+++ b/Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs
@@ -62,10 +62,6 @@ namespace CosmicShore.Core
         const int MenuKeyOffset = 1000;
         const int LegacyClipKeyOffset = unchecked((int)0x80000000);
 
-        // Mixing constants for the legacy AudioClip path (no authored spec exists for arbitrary clips).
-        const float LegacyClipGain = 0.6f;
-        const int LegacyClipPriority = 45;
-        const float LegacyClipCooldown = 0.1f;
 
         AudioHapticsConfigSO _config;
         GameSetting _gameSetting;
@@ -249,10 +245,10 @@ namespace CosmicShore.Core
             var request = new HapticTransientArbiter.Request
             {
                 CategoryKey = LegacyClipKeyOffset | (clip.GetInstanceID() & 0x7FFFFFFF),
-                Priority = LegacyClipPriority,
```

</details>

### `39de3192d` — feat(haptics): playtest feel pass — sparse haptics, hero skim pulse-train + prism punish thud

_Claude, 2026-07-17 20:08:53 +0000_

```text
Playtest verdict: haptics never stopped, and the one that mattered most —
the Squirrel skimming prisms — was imperceptible. Root cause of the silent
skim: SkimmerScaleHapticWithDistanceByPrismSO read SkimmerImpactor.
CombinedWeight, which is never written since the block-stay rework, so the
haptic scale was always zero; what remained routed to a priority-30 spec
that everything else preempted, under an always-on bed that masked it.

Feel doctrine (now enforced by Doctrine_* tests + documented in
Docs/HapticsSystem/ARCHITECTURE.md): haptics are sparse signals, not a
soundtrack.

- Hero #1 — skim pulse train (the reward): dedicated designed ~70ms bright
  snap (AudioHapticsBakedDefaults.SkimPulse, priority 80, 30ms cooldown);
  each prism entering the skimmer fires one pulse, chaining into a
  continuous train on dense trails. SkimmerScaleHapticWithDistanceByPrismSO
  now computes real proximity (prism vs skimmer sphere) so center passes
  hit harder than edge grazes, and the Squirrel skimmer container wires it.
- Hero #2 — prism punish thud (the mistake): designed low-frequency thud
  (freq 0.15, priority 88 so a crash cuts through the train, 0.25s
  cooldown); SquirrelVesselHapticsByPrismEffect retyped to ShipCollision.
- Everything else toned way down or to zero: all menu/UI haptics silent;
  only MineExplode 0.35, Explosion 0.30, CrystalCollect 0.20 keep a gain.
- Continuous bed now OFF by default (following the engine hum = vibration
  that never stops); machinery kept for experiments + PlayConstant pulses.
- New audioEventGain per spec: vessel-attributed categories (VesselImpact,
  TrackImpact, CrystalCollect) opt out of the automatic audio hook so AI
  events nearby can't buzz the device; local-gated effect SOs own them.
- Legacy AudioClip haptics off by default; ElementalBars debuff buzz off;
  AstroLeague wall-bounce haptic removed (a carom is nobody's action);
  short clips render gamepad rumble at 16ms steps for crispness.
```

```text
 Assets/Resources/ElementalBarsConfig.asset                            |   2 +-
 .../SkimmerContainers/SquirrelSkimmerImpactorDataContainer.asset      |   2 +-
 .../Vessel Prism Effects/SquirrelVesselHapticsByPrismEffect.asset     |   2 +-
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs      |   5 +-
 Assets/_Scripts/Controller/IO/HapticController.cs                     |   5 +-
 .../Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs  |  37 +++++-
 Assets/_Scripts/Editor/AudioHapticsBaker.cs                           |   2 +
 Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs             |  15 ++-
 Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs            |   2 +-
 Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs             | 197 ++++++++++++++++----------------
 Assets/_Scripts/System/Audio/AudioHapticsOrchestrator.cs              |  36 +++++-
 Assets/_Scripts/System/Audio/AudioSystem.cs                           |  17 +--
 Assets/_Scripts/Tests/EditMode/AudioHapticsBakedDefaultsTests.cs      |  93 +++++++++++++++
 CLAUDE.md                                                             |   2 +-
 Docs/HapticsSystem/ARCHITECTURE.md                                    |  50 +++++++-
 15 files changed, 338 insertions(+), 129 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 942 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
index dbcfa57d9..aa40e141f 100644
--- a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
+++ b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs
@@ -982,8 +982,9 @@ namespace CosmicShore.Gameplay
             TriggerFlash(intensity * 0.6f);
             EmitBurst(position, normal, (int)(settings.impactParticleBurst * 0.5f * Mathf.Max(0.4f, intensity)));
             ShakeCamera(settings.strikeShakeIntensity * intensity * 0.35f, settings.strikeShakeDuration, position);
-            // Distance-attenuated: a carom across the arena is a murmur, not a slam on every peer.
-            HapticController.PlayHaptic(HapticType.ShipCollision, position);
+            // No haptic: a ball carom is nobody's action. The screen shake +
+            // burst carry it; haptics are reserved for events the local player
+            // caused or suffered (feel doctrine, Docs/HapticsSystem).
         }
 
         void TriggerFlash(float intensity) =>
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index 185d70fdf..d62aaeffd 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -131,7 +131,10 @@ namespace CosmicShore.Gameplay
                     orchestrator.PlayMenuTransient(MenuAudioCategory.OptionClick);
                     return;
                 case HapticType.PrismCollision:
-                    PlayGameplay(orchestrator, GameplaySFXCategory.CrystalSkim, worldPosition);
+                    // The skimmer's per-prism event — THE hero haptic. Each prism
+                    // entering the skimmer fires one bright pulse; skimming down a
+                    // trail chains them into a continuous rewarding train.
+                    orchestrator.PlaySkimPulse();
                     return;
                 case HapticType.ShipCollision:
                     PlayGameplay(orchestrator, GameplaySFXCategory.VesselImpact, worldPosition);
diff --git a/Assets/_Scripts/Editor/AudioHapticsBaker.cs b/Assets/_Scripts/Editor/AudioHapticsBaker.cs
index 48de20192..c19da64ff 100644
--- a/Assets/_Scripts/Editor/AudioHapticsBaker.cs
+++ b/Assets/_Scripts/Editor/AudioHapticsBaker.cs
@@ -112,6 +112,8 @@ namespace CosmicShore.Editor
                     category = category,
                     spec = AudioHapticsBakedDefaults.ForMenu(category).Clone(),
                 });
+
+            config.skimPulse = AudioHapticsBakedDefaults.SkimPulse().Clone();
         }
 
         static bool TryBakeSpec(HapticTransientSpec spec, string sourceRoot)
diff --git a/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs b/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
index ab5685f18..335c00ccb 100644
--- a/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs
@@ -24,6 +24,9 @@ namespace CosmicShore.ScriptableObjects
         [Tooltip("Minimum seconds between two haptics of this category. Guards burst categories from turning the actuator into mush.")]
         [Min(0f)] public float cooldownSeconds = 0.08f;
 
+        [Tooltip("Extra multiplier applied ONLY when this haptic is triggered automatically by the audio one-shot hook in AudioSystem (any actor, anywhere). Explicit gameplay calls (HapticSpec / HapticController) ignore it. Set 0 for vessel-attributed feedback (impacts, pickups) that a local-player-gated effect SO already owns — otherwise every AI collision nearby would buzz the device.")]
+        [Range(0f, 1f)] public float audioEventGain = 1f;
+
         [Tooltip("Envelope breakpoints. Time is seconds from the audio event start so the haptic tracks the sound's actual shape. Bake from source audio or author by hand.")]
         public List<HapticBreakpoint> envelope = new();
 
@@ -44,6 +47,7 @@ namespace CosmicShore.ScriptableObjects
                 gain = gain,
                 priority = priority,
                 cooldownSeconds = cooldownSeconds,
+                audioEventGain = audioEventGain,
                 bakedFrom = bakedFrom,
             };
             if (envelope != null)
@@ -94,6 +98,9 @@ namespace CosmicShore.ScriptableObjects
         [Tooltip("Per menu-audio-category haptic envelopes. Missing categories fall back to the baked code defaults.")]
         public List<MenuEntry> menuTransients = new();
 
+        [Tooltip("THE hero haptic: the Squirrel skimmer's per-prism skim pulse. Each prism the skimmer passes fires one bright, snappy pulse — riding a dense trail turns them into a continuous rewarding train. Leave empty to use the designed code default.")]
+        public HapticTransientSpec skimPulse = new();
+
         [Header("Transient arbitration (one haptic channel)")]
         [Tooltip("Requests weaker than this are dropped — below the actuator sensation threshold.")]
         [Range(0f, 0.2f)] public float transientMinStrength = 0.02f;
@@ -108,8 +115,8 @@ namespace CosmicShore.ScriptableObjects
         [Range(0f, 0.3f)] public float transientTailSeconds = 0.06f;
 
         [Header("Continuous bed (follows the measured SFX bus signal)")]
-        [Tooltip("Drive a continuous haptic layer from real-time FMOD bus metering: engine hum, drift, boost swells and one-shot tails all translate to touch with zero per-emitter wiring.")]
-        public bool bedEnabled = true;
+        [Tooltip("Drive a continuous haptic layer from real-time FMOD bus metering (engine hum, drift, boost swells, one-shot tails). OFF by default after playtest: a bed that follows the engine hum vibrates whenever the vessel moves — haptics that never stop numb the hand and mask the discrete pulses that matter. Enable only for experiments.")]
+        public bool bedEnabled = false;
 
         [Tooltip("FMOD bus whose output the bed follows. Empty = AudioSystem's SFX bus path (default \"bus:/\"). Music runs on the legacy Unity path, so it never contaminates this signal.")]
         public string bedBusPathOverride = "";
@@ -156,8 +163,8 @@ namespace CosmicShore.ScriptableObjects
         [Min(1f)] public float spatialCutoffDistance = 200f;
 
         [Header("Legacy AudioClip path")]
-        [Tooltip("Analyze AudioClips played through the legacy PlaySFXClip path at runtime (waveform envelope, cached per clip) so even unmigrated sounds get matched haptics.")]
-        public bool analyzeLegacyClips = true;
+        [Tooltip("Analyze AudioClips played through the legacy PlaySFXClip path at runtime (waveform envelope, cached per clip) so even unmigrated sounds get matched haptics. OFF by default after playtest — UI stingers don't earn a buzz (haptics are sparse signals, not a soundtrack).")]
+        public bool analyzeLegacyClips = false;
 
         [Tooltip("Envelope analysis settings used for legacy AudioClips (window, budget, transient detection). Fixed at code defaults; exposed here only for the master duration cap.")]
         [Min(0.2f)] public float legacyClipMaxSeconds = 1.5f;
diff --git a/Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs b/Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs
index 8ddf80aae..37c11406a 100644
--- a/Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/ElementalBarsConfigSO.cs
@@ -64,7 +64,7 @@ namespace CosmicShore.ScriptableObjects
         public float debuffShakeStrength = 8f;
 
         [Header("Juice - haptics")]
-        public bool  hapticOnDebuff        = true;
+        public bool  hapticOnDebuff        = false; // playtest: haptics are sparse signals — a stat dip doesn't earn a buzz
         public float debuffHapticAmplitude = 0.6f;
         public float debuffHapticFrequency = 0.5f;
         public float debuffHapticDuration  = 0.15f;
diff --git a/Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs b/Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs
index ae7639ee2..9d7af3558 100644
--- a/Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs
+++ b/Assets/_Scripts/System/Audio/AudioHapticsBakedDefaults.cs
@@ -1,13 +1,17 @@
 // ------------------------------------------------------------------------------------
-// GENERATED DATA - measured haptic envelopes
+// GENERATED DATA + PLAYTEST FEEL PASS - per-category haptic defaults
 //
-// Each envelope below was analyzed from the corresponding source audio file in the
-// FMOD Studio project ("Cosmic Shore/Assets/*.wav", windowed-peak amplitude +
-// zero-crossing-rate frequency + attack-flux emphasis; see HapticWaveformAnalyzer).
-// These are the ZERO-WIRE defaults: they apply when a category has no override in
-// the AudioHapticsConfig asset. To retune, bake fresh envelopes into the config
-// asset via Tools > Cosmic Shore > Audio Haptics (which runs the same analysis in
-// the editor) - do not hand-edit the tables here.
+// Envelopes were measured from the corresponding source audio in the FMOD Studio
+// project ("Cosmic Shore/Assets/*.wav"; windowed-peak amplitude + zero-crossing-rate
+// frequency + attack-flux emphasis - see HapticWaveformAnalyzer). The MIXING values
+// (gain / priority / cooldown / audioEventGain) are hand-tuned from playtest, and the
+// two hero envelopes (VesselImpact / TrackImpact punish thud, and SkimPulse) are
+// DESIGNED rather than measured.
+//
+// FEEL DOCTRINE (playtest-locked - see Docs/HapticsSystem/ARCHITECTURE.md):
+// haptics are sparse signals, not a soundtrack. Most categories default to gain 0;
+// the envelopes are kept so re-enabling one is a single gain edit (here or in the
+// AudioHapticsConfig asset, which overrides these values per category).
 // ------------------------------------------------------------------------------------
 using System.Collections.Generic;
 using CosmicShore.ScriptableObjects;
@@ -16,37 +20,52 @@ using CosmicShore.Utility;
 namespace CosmicShore.Core
 {
     /// <summary>
-    /// Code-default <see cref="HapticTransientSpec"/>s for every audio category,
-    /// with envelopes measured from the game's actual source SFX waveforms.
-    /// Used by AudioHapticsOrchestrator whenever the config asset is missing or
-    /// has no entry for a category, so audio-matched haptics work with zero
```

</details>
