# Branch archive: `claude/add-haptics-for-fx-sznU5`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-09 by Claude
- **Unmerged commits:** 3
- **Forked from:** `3f0ba6e1a` (2026-05-06, Merge pull request #511 from froglet-studio/claude/fix-multiplayer-domain-sele)
- **Tip:** `8be8ff9c3`
- **Files touched (8):**
  - `Assets/_Scripts/Controller/IO/HapticController.cs`
  - `Assets/_Scripts/Editor/HapticBakerMenu.cs`
  - `Assets/_Scripts/Editor/HapticClipBaker.cs`
  - `Assets/_Scripts/Editor/HapticDiagnosticsMenu.cs`
  - `Assets/_Scripts/System/Audio/AudioSystem.cs`
  - `Assets/_Scripts/Tests/EditMode/AudioHapticMappingTests.cs`
  - `Assets/_Scripts/Tests/EditMode/HapticEnvelopeAnalysisTests.cs`
  - `Assets/_Scripts/Utility/Audio/HapticEnvelopeAnalysis.cs`

### `b3ce70eb1` — feat(audio): auto-play haptics for every sound effect

_Claude, 2026-05-05 19:44:24 +0000_

```text
Every PlayGameplaySFX and PlayMenuAudio call now also fires a corresponding
haptic via HapticController, mapped per category. The mapping lives as a
static switch in AudioSystem so designers can tune it in one place.

Also fix HapticController so it lazy-resolves GameSetting via
FindFirstObjectByType when the MonoBehaviour isn't placed in the scene
(it currently isn't anywhere) — without this, every haptic call was a
silent no-op.

Adds an edit-mode test that fails if a new GameplaySFXCategory or
MenuAudioCategory is added without a haptic mapping.
```

```text
 Assets/_Scripts/Controller/IO/HapticController.cs         | 17 ++++++++++++---
 Assets/_Scripts/System/Audio/AudioSystem.cs               | 45 ++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/EditMode/AudioHapticMappingTests.cs | 53 +++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 112 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 168 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index 2b336ae2e..99600992c 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -28,6 +28,13 @@ namespace CosmicShore.Gameplay
 
         void Awake() => s_gameSetting = injectedGameSetting;
 
+        static GameSetting ResolveGameSetting()
+        {
+            if (s_gameSetting != null) return s_gameSetting;
+            s_gameSetting = FindFirstObjectByType<GameSetting>();
+            return s_gameSetting;
+        }
+
         /// <summary>
         /// Play Haptic
         /// Play haptic pattern presets when haptics are enabled.
@@ -35,10 +42,13 @@ namespace CosmicShore.Gameplay
         /// <param name="type">Haptic type</param>
         public static void PlayHaptic(HapticType type)
         {
-            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled || s_gameSetting.HapticsLevel == 0)
+            if (type == HapticType.None) return;
+
+            var settings = ResolveGameSetting();
+            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
                 return;
 
-            Lofelt.NiceVibrations.HapticController.outputLevel = s_gameSetting.HapticsLevel;
+            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
 
             var pattern = GetPatternForHapticType(type);
 
@@ -47,7 +57,8 @@ namespace CosmicShore.Gameplay
 
         public static void PlayConstant(float amplitude, float frequency, float duration)
         {
-            if (s_gameSetting == null || !s_gameSetting.HapticsEnabled)
+            var settings = ResolveGameSetting();
+            if (settings == null || !settings.HapticsEnabled)
                 return;
             HapticPatterns.PlayConstant(amplitude, frequency, duration);
         }
diff --git a/Assets/_Scripts/System/Audio/AudioSystem.cs b/Assets/_Scripts/System/Audio/AudioSystem.cs
index 3dd5e5b74..3855f2e8f 100644
--- a/Assets/_Scripts/System/Audio/AudioSystem.cs
+++ b/Assets/_Scripts/System/Audio/AudioSystem.cs
@@ -289,6 +289,7 @@ namespace CosmicShore.Core
         public void PlayMenuAudio(MenuAudioCategory category)
         {
             PlaySFXClip(MenuAudioClips[category]);
+            HapticController.PlayHaptic(GetHapticForMenuAudio(category));
         }
 
         public void PlayGameplaySFX(GameplaySFXCategory category)
@@ -297,8 +298,52 @@ namespace CosmicShore.Core
                 PlaySFXClip(clip);
             else
                 Debug.LogWarning($"AudioSystem.PlayGameplaySFX: No audio clip assigned for {category}");
+
+            HapticController.PlayHaptic(GetHapticForGameplaySFX(category));
         }
 
+        public static HapticType GetHapticForGameplaySFX(GameplaySFXCategory category) => category switch
+        {
+            GameplaySFXCategory.BlockDestroy      => HapticType.CrystalCollision,
+            GameplaySFXCategory.ShieldActivate    => HapticType.ButtonPress,
+            GameplaySFXCategory.ShieldDeactivate  => HapticType.ButtonPress,
+            GameplaySFXCategory.MineExplode       => HapticType.MineCollision,
+            GameplaySFXCategory.ProjectileLaunch  => HapticType.ButtonPress,
+            GameplaySFXCategory.CrystalCollect    => HapticType.CrystalCollision,
+            GameplaySFXCategory.VesselImpact      => HapticType.ShipCollision,
+            GameplaySFXCategory.GameEnd           => HapticType.ShipCollision,
+            GameplaySFXCategory.ScoreReveal       => HapticType.ButtonPress,
+            GameplaySFXCategory.PauseOpen         => HapticType.ButtonPress,
+            GameplaySFXCategory.PauseClose        => HapticType.ButtonPress,
+            GameplaySFXCategory.GunFire           => HapticType.ButtonPress,
+            GameplaySFXCategory.BoostActivate     => HapticType.CrystalCollision,
+            GameplaySFXCategory.Explosion         => HapticType.MineCollision,
+            GameplaySFXCategory.CreatureDeath     => HapticType.CrystalCollision,
+            GameplaySFXCategory.DriftStart        => HapticType.ButtonPress,
+            GameplaySFXCategory.DriftEnd          => HapticType.ButtonPress,
+            GameplaySFXCategory.EnergyGain        => HapticType.ButtonPress,
+            GameplaySFXCategory.SpeedBurst        => HapticType.CrystalCollision,
+            GameplaySFXCategory.CrystalSkim       => HapticType.ButtonPress,
+            _ => HapticType.None,
+        };
+
+        public static HapticType GetHapticForMenuAudio(MenuAudioCategory category) => category switch
+        {
+            MenuAudioCategory.OptionClick   => HapticType.ButtonPress,
+            MenuAudioCategory.OpenView      => HapticType.ButtonPress,
+            MenuAudioCategory.SwitchView    => HapticType.ButtonPress,
+            MenuAudioCategory.CloseView     => HapticType.ButtonPress,
+            MenuAudioCategory.SmallReward   => HapticType.CrystalCollision,
+            MenuAudioCategory.BigReward     => HapticType.ShipCollision,
+            MenuAudioCategory.Upgrade       => HapticType.CrystalCollision,
+            MenuAudioCategory.Denied        => HapticType.ButtonPress,
+            MenuAudioCategory.Confirmed     => HapticType.ButtonPress,
+            MenuAudioCategory.LetsGo        => HapticType.ShipCollision,
+            MenuAudioCategory.SwitchScreen  => HapticType.ButtonPress,
+            MenuAudioCategory.RedeemTicket  => HapticType.CrystalCollision,
+            _ => HapticType.None,
+        };
+
         public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
         {
             sfxSource.volume = SFXVolume;
diff --git a/Assets/_Scripts/Tests/EditMode/AudioHapticMappingTests.cs b/Assets/_Scripts/Tests/EditMode/AudioHapticMappingTests.cs
new file mode 100644
index 000000000..ef8049d35
--- /dev/null
+++ b/Assets/_Scripts/Tests/EditMode/AudioHapticMappingTests.cs
@@ -0,0 +1,53 @@
+using System;
+using System.Linq;
+using NUnit.Framework;
+using CosmicShore.Core;
+using CosmicShore.Gameplay;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// Verifies that every sound effect category has a corresponding haptic mapping.
+    /// If someone adds a new GameplaySFXCategory or MenuAudioCategory value, these
+    /// tests force them to wire up a haptic in AudioSystem.GetHapticFor* — otherwise
+    /// the new sound silently plays without a buzz.
+    /// </summary>
+    [TestFixture]
+    public class AudioHapticMappingTests
+    {
+        [Test]
+        public void EveryGameplaySFXCategoryHasNonNoneHaptic()
+        {
+            var unmapped = Enum.GetValues(typeof(GameplaySFXCategory))
+                .Cast<GameplaySFXCategory>()
+                .Where(c => AudioSystem.GetHapticForGameplaySFX(c) == HapticType.None)
+                .ToList();
+
+            Assert.IsEmpty(unmapped,
+                $"GameplaySFXCategory values without a haptic mapping: {string.Join(", ", unmapped)}. " +
+                "Add a case in AudioSystem.GetHapticForGameplaySFX.");
+        }
+
+        [Test]
+        public void EveryMenuAudioCategoryHasNonNoneHaptic()
+        {
+            var unmapped = Enum.GetValues(typeof(MenuAudioCategory))
+                .Cast<MenuAudioCategory>()
```

</details>

### `29fb02b0e` — feat(haptics): bake per-AudioClip haptic envelopes via editor tool

_Claude, 2026-05-09 00:16:12 +0000_

```text
Replaces the coarse category→preset mapping with one-to-one waveform-derived
haptic clips. Each AudioClip on AudioSystem now has a paired HapticClip slot;
at runtime PlayGameplaySFX/PlayMenuAudio prefer the baked clip and fall back
to the preset mapping when one isn't wired.

* CosmicShore.Utility.HapticEnvelopeAnalysis (runtime, testable)
  - Window-based peak amplitude renormalized to [0..1] with gamma shaping
  - Zero-crossing rate as a cheap spectral-centroid proxy, log-mapped to
    NiceVibrations' [0..1] haptic frequency range
  - Renders Lofelt v1.0.0 .haptic JSON

* CosmicShore.Editor.HapticClipBaker — editor-side wrapper that pulls
  AudioClip samples and writes the .haptic file (auto-imported as HapticClip
  by NiceVibrations' HapticImporter).

* HapticBakerMenu adds two entry points:
  - Assets > Cosmic Shore > Bake Haptic Clip from AudioClip
    (right-click any AudioClip to bake a sibling .haptic file under
     Assets/_Haptics/ mirroring the audio folder structure)
  - Tools > Cosmic Shore > Bake Haptics for AudioSystem
    (iterates every AudioClip field on the AudioSystem prefab, bakes each,
     and auto-wires the resulting HapticClip into the matching field by name)

* HapticController gains PlayClip(HapticClip) for runtime playback of baked
  clips through Lofelt's HapticController.Play.

* AudioSystem gains 32 HapticClip fields (12 menu + 20 gameplay) parallel
  to the existing AudioClip fields, plus init dictionaries.

* Tests cover envelope math: peak normalization, silence handling, window
  count, frequency-range invariants, pitch ordering, max-duration truncation,
  Lofelt JSON schema shape, and InterleavedToMono channel averaging.

Workflow: open Unity, run Tools > Cosmic Shore > Bake Haptics for AudioSystem
once to generate .haptic assets and wire them into AudioSystem. New SFX:
right-click the AudioClip in the Project view and bake.
```

```text
 Assets/_Scripts/Controller/IO/HapticController.cs             |  16 ++++
 Assets/_Scripts/Editor/HapticBakerMenu.cs                     | 187 ++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/HapticClipBaker.cs                     |  40 +++++++++
 Assets/_Scripts/System/Audio/AudioSystem.cs                   |  99 ++++++++++++++++++++-
 Assets/_Scripts/Tests/EditMode/HapticEnvelopeAnalysisTests.cs | 168 ++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Audio/HapticEnvelopeAnalysis.cs       | 182 ++++++++++++++++++++++++++++++++++++++
 6 files changed, 690 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 780 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index 99600992c..6c07132ae 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -63,6 +63,22 @@ namespace CosmicShore.Gameplay
             HapticPatterns.PlayConstant(amplitude, frequency, duration);
         }
 
+        /// <summary>
+        /// Play a baked .haptic clip authored by HapticClipBaker (or Lofelt Studio).
+        /// Falls through silently when disabled or when the clip is empty.
+        /// </summary>
+        public static void PlayClip(HapticClip clip)
+        {
+            if (clip == null || clip.json == null || clip.json.Length == 0) return;
+
+            var settings = ResolveGameSetting();
+            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
+                return;
+
+            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
+            Lofelt.NiceVibrations.HapticController.Play(clip);
+        }
+
         /// <summary>
         /// Get Pattern For Haptic Type
         /// Returns mapped Haptic Patterns
diff --git a/Assets/_Scripts/Editor/HapticBakerMenu.cs b/Assets/_Scripts/Editor/HapticBakerMenu.cs
new file mode 100644
index 000000000..13667fcc5
--- /dev/null
+++ b/Assets/_Scripts/Editor/HapticBakerMenu.cs
@@ -0,0 +1,187 @@
+using System.Collections.Generic;
+using System.IO;
+using CosmicShore.Core;
+using CosmicShore.Utility;
+using Lofelt.NiceVibrations;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Menu items that drive HapticClipBaker. Two entry points:
+    ///  - Project-view context: bake selected AudioClip(s) to mirrored .haptic files.
+    ///  - Tools menu: bake every AudioClip field on the AudioSystem prefab, then
+    ///    auto-wire the resulting HapticClip to a sibling field named
+    ///    "[BaseName]HapticClip" (e.g. BlockDestroyAudioClip → BlockDestroyHapticClip).
+    /// </summary>
+    public static class HapticBakerMenu
+    {
+        const string AudioRoot = "Assets/_Audio/Sounds/";
+        const string HapticRoot = "Assets/_Haptics/";
+        const string AudioSystemPrefabPath = "Assets/_Prefabs/CORE/AudioSystem.prefab";
+
+        [MenuItem("Assets/Cosmic Shore/Bake Haptic Clip from AudioClip", true)]
+        static bool ValidateBakeSelected() => Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;
+
+        [MenuItem("Assets/Cosmic Shore/Bake Haptic Clip from AudioClip", false, 2000)]
+        static void BakeSelected()
+        {
+            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
+            int baked = 0;
+            try
+            {
+                AssetDatabase.StartAssetEditing();
+                for (int i = 0; i < clips.Length; i++)
+                {
+                    var clip = clips[i];
+                    EditorUtility.DisplayProgressBar("Baking Haptics", clip.name, (float)i / clips.Length);
+                    string outPath = ResolveHapticOutputPath(clip);
+                    if (HapticClipBaker.BakeAudioClipToHapticFile(clip, outPath, HapticEnvelopeAnalysis.Settings.Default) != null)
+                        baked++;
+                }
+            }
+            finally
+            {
+                AssetDatabase.StopAssetEditing();
+                EditorUtility.ClearProgressBar();
+                AssetDatabase.Refresh();
+            }
+            Debug.Log($"[HapticBaker] Baked {baked}/{clips.Length} haptic clips.");
+        }
+
+        [MenuItem("Tools/Cosmic Shore/Bake Haptics for AudioSystem", false, 100)]
+        static void BakeAudioSystem()
+        {
+            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSystemPrefabPath);
+            if (prefab == null)
+            {
+                Debug.LogError($"[HapticBaker] AudioSystem prefab not found at {AudioSystemPrefabPath}");
+                return;
+            }
+
+            var instanceRoot = PrefabUtility.LoadPrefabContents(AudioSystemPrefabPath);
+            try
+            {
+                var audioSystem = instanceRoot.GetComponent<AudioSystem>();
+                if (audioSystem == null)
+                {
+                    Debug.LogError("[HapticBaker] AudioSystem component not found on prefab root.");
+                    return;
+                }
+
+                var so = new SerializedObject(audioSystem);
+                var pairs = CollectAudioHapticPairs(so);
+                Debug.Log($"[HapticBaker] Found {pairs.Count} AudioClip fields on AudioSystem.");
+
+                int baked = 0, wired = 0;
+                try
+                {
+                    AssetDatabase.StartAssetEditing();
+                    for (int i = 0; i < pairs.Count; i++)
+                    {
+                        var pair = pairs[i];
+                        EditorUtility.DisplayProgressBar("Baking AudioSystem Haptics", pair.audioFieldName, (float)i / pairs.Count);
+
+                        if (pair.audio == null)
+                        {
+                            Debug.LogWarning($"[HapticBaker] {pair.audioFieldName} has no AudioClip wired — skipping.");
+                            continue;
+                        }
+
+                        string outPath = ResolveHapticOutputPath(pair.audio);
+                        var bakedPath = HapticClipBaker.BakeAudioClipToHapticFile(pair.audio, outPath, HapticEnvelopeAnalysis.Settings.Default);
+                        if (bakedPath == null) continue;
+                        baked++;
+                        pair.bakedAssetPath = bakedPath;
+                    }
+                }
+                finally
+                {
+                    AssetDatabase.StopAssetEditing();
+                    EditorUtility.ClearProgressBar();
+                }
+
+                AssetDatabase.Refresh();
+
+                so.Update();
+                foreach (var pair in pairs)
+                {
+                    if (pair.hapticProperty == null || string.IsNullOrEmpty(pair.bakedAssetPath)) continue;
+                    var hapticClip = AssetDatabase.LoadAssetAtPath<HapticClip>(pair.bakedAssetPath);
+                    if (hapticClip == null)
+                    {
+                        Debug.LogWarning($"[HapticBaker] Baked file {pair.bakedAssetPath} did not import as HapticClip.");
+                        continue;
+                    }
+                    pair.hapticProperty.objectReferenceValue = hapticClip;
```

</details>

### `8be8ff9c3` — diag(haptics): one-shot diagnostic dump + Tools > Test Haptic probe

_Claude, 2026-05-09 00:41:33 +0000_

```text
When haptics fire nothing, the chain has many silent fail points (no
GameSetting, HapticsEnabled=false, level=0, device doesn't meet Lofelt's
advanced requirements, running in editor without a gamepad, etc.). Make
the chain self-describing.

* HapticController.LogDiagnostics() prints every gate's value in one block:
  Application.platform, GameSetting state, Lofelt enabled/output, and
  DeviceCapabilities (platform/version/isVersionSupported/meetsAdvanced).
* PlayHaptic gates now log a one-shot warning naming exactly which gate
  short-circuited, then auto-dump diagnostics.
* AudioSystem.Start() calls LogDiagnostics() once so the Console shows
  the haptic state at app startup without needing an SFX trigger.
* New menu items:
  - Tools > Cosmic Shore > Test Haptic (Play Mode) — force-inits Lofelt
    and fires a HeavyImpact preset, useful for verifying the chain
    independently of any gameplay SFX wiring.
  - Tools > Cosmic Shore > Dump Haptic Diagnostics — re-prints the state
    block on demand.

Common diagnosis from the dump:
  - Application.isEditor=true and no gamepad attached → expected; Lofelt
    only fires the device vibrator on iOS/Android device builds.
  - meetsAdvancedRequirements=false on a phone → device's haptic motor
    is too old (pre-iPhone-7 / Android API < 26 / pre-Haptic-V2).
  - HapticsEnabled=false → user toggled it off in settings.
```

```text
 Assets/_Scripts/Controller/IO/HapticController.cs | 120 +++++++++++++++++++++++++++++++++++++++++-----------
 Assets/_Scripts/Editor/HapticDiagnosticsMenu.cs   |  31 ++++++++++++++
 Assets/_Scripts/System/Audio/AudioSystem.cs       |   1 +
 3 files changed, 127 insertions(+), 25 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 225 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index 6c07132ae..efb56e964 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -25,6 +25,8 @@ namespace CosmicShore.Gameplay
     {
         [Inject] GameSetting injectedGameSetting;
         static GameSetting s_gameSetting;
+        static bool s_lofeltInitialized;
+        static bool s_diagnosticsLogged;
 
         void Awake() => s_gameSetting = injectedGameSetting;
 
@@ -35,6 +37,71 @@ namespace CosmicShore.Gameplay
             return s_gameSetting;
         }
 
+        /// <summary>
+        /// Initializes Lofelt's HapticController and dumps a one-shot diagnostic block
+        /// the first time we cross this path. Useful for diagnosing "I feel nothing"
+        /// reports — the log identifies platform, version support, advanced-requirement
+        /// status, and game-side settings in one place.
+        /// </summary>
+        static bool EnsureLofeltInitialized()
+        {
+            if (s_lofeltInitialized) return true;
+            s_lofeltInitialized = true;
+
+            bool meetsAdvanced = Lofelt.NiceVibrations.HapticController.Init();
+            LogDiagnostics(meetsAdvanced);
+            return meetsAdvanced;
+        }
+
+        /// <summary>
+        /// Dumps the full state of the haptic chain. Safe to call any time — useful for
+        /// the Tools > Cosmic Shore > Test Haptic menu and for ad-hoc debugging.
+        /// </summary>
+        public static void LogDiagnostics(bool? meetsAdvancedOverride = null)
+        {
+            if (s_diagnosticsLogged && meetsAdvancedOverride == null) return;
+            s_diagnosticsLogged = true;
+
+            var settings = ResolveGameSetting();
+            bool meetsAdvanced = meetsAdvancedOverride ?? Lofelt.NiceVibrations.HapticController.Init();
+
+            string settingsStr = settings == null
+                ? "null (GameSetting not in scene)"
+                : $"HapticsEnabled={settings.HapticsEnabled} HapticsLevel={settings.HapticsLevel}";
+
+            Debug.Log(
+                "[HapticController] Diagnostic dump:\n" +
+                $"  Application.platform = {Application.platform}\n" +
+                $"  Application.isEditor = {Application.isEditor}\n" +
+                $"  GameSetting:           {settingsStr}\n" +
+                $"  Lofelt.hapticsEnabled = {Lofelt.NiceVibrations.HapticController.hapticsEnabled}\n" +
+                $"  Lofelt.outputLevel    = {Lofelt.NiceVibrations.HapticController.outputLevel}\n" +
+                $"  DeviceCapabilities.platform           = {DeviceCapabilities.platform}\n" +
+                $"  DeviceCapabilities.platformVersion    = {DeviceCapabilities.platformVersion}\n" +
+                $"  DeviceCapabilities.isVersionSupported = {DeviceCapabilities.isVersionSupported}\n" +
+                $"  meetsAdvancedRequirements             = {meetsAdvanced}\n" +
+                $"  Note: Lofelt only fires the device vibrator on iOS/Android device builds. " +
+                "In Editor or on PC, haptics only emit if a gamepad with rumble is connected and " +
+                "the new Input System is installed."
+            );
+        }
+
+        /// <summary>
+        /// Manual smoke test: force-init Lofelt, dump diagnostics, then fire a HeavyImpact preset.
+        /// Use from a play-mode menu item to validate the chain end-to-end.
+        /// </summary>
+        public static void ForceTestPlay()
+        {
+            s_diagnosticsLogged = false;
+            EnsureLofeltInitialized();
+
+            var settings = ResolveGameSetting();
+            if (settings != null) Lofelt.NiceVibrations.HapticController.outputLevel = Mathf.Max(0.5f, settings.HapticsLevel);
+            HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
+            Debug.Log("[HapticController] ForceTestPlay() invoked — fired HeavyImpact preset. " +
+                      "If you felt nothing, see the diagnostic block above.");
+        }
+
         /// <summary>
         /// Play Haptic
         /// Play haptic pattern presets when haptics are enabled.
@@ -45,21 +112,21 @@ namespace CosmicShore.Gameplay
             if (type == HapticType.None) return;
 
             var settings = ResolveGameSetting();
-            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
-                return;
-
-            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
+            if (settings == null) { LogFirstFailure("GameSetting not resolved (no instance in any loaded scene)."); return; }
+            if (!settings.HapticsEnabled) { LogFirstFailure("settings.HapticsEnabled is false (PlayerPref / cloud setting)."); return; }
+            if (settings.HapticsLevel == 0) { LogFirstFailure("settings.HapticsLevel is 0 — turn it up in the settings menu."); return; }
 
-            var pattern = GetPatternForHapticType(type);
+            EnsureLofeltInitialized();
 
-            HapticPatterns.PlayPreset(pattern);
+            Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
+            HapticPatterns.PlayPreset(GetPatternForHapticType(type));
         }
 
         public static void PlayConstant(float amplitude, float frequency, float duration)
         {
             var settings = ResolveGameSetting();
-            if (settings == null || !settings.HapticsEnabled)
-                return;
+            if (settings == null || !settings.HapticsEnabled) return;
+            EnsureLofeltInitialized();
             HapticPatterns.PlayConstant(amplitude, frequency, duration);
         }
 
@@ -72,39 +139,42 @@ namespace CosmicShore.Gameplay
             if (clip == null || clip.json == null || clip.json.Length == 0) return;
 
             var settings = ResolveGameSetting();
-            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0)
-                return;
+            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel == 0) return;
+
+            EnsureLofeltInitialized();
 
             Lofelt.NiceVibrations.HapticController.outputLevel = settings.HapticsLevel;
             Lofelt.NiceVibrations.HapticController.Play(clip);
         }
 
+        static bool s_failureLogged;
+        static void LogFirstFailure(string reason)
+        {
+            if (s_failureLogged) return;
+            s_failureLogged = true;
+            Debug.LogWarning($"[HapticController] First haptic call short-circuited: {reason}");
+            LogDiagnostics();
+        }
+
         /// <summary>
         /// Get Pattern For Haptic Type
         /// Returns mapped Haptic Patterns
         /// </summary>
         /// <param name="type">Haptic Type</param>
-        /// <returns></returns>
         private static HapticPatterns.PresetType GetPatternForHapticType(HapticType type)
         {
             switch (type)
             {
-                case HapticType.ButtonPress:
-                    return HapticPatterns.PresetType.LightImpact;
```

</details>
