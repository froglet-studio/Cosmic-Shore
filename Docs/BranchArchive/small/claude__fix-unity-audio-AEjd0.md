# Branch archive: `claude/fix-unity-audio-AEjd0`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-05 by Claude
- **Unmerged commits:** 1
- **Forked from:** `edac93aae` (2026-03-05, Update SquirrelSkimmerImpactorDataContainer.asset)
- **Tip:** `60f85da60`
- **Files touched (2):**
  - `Assets/_Scripts/App/Systems/Audio/AudioSystem.cs`
  - `Assets/_Scripts/Game/Settings/GameSetting.cs`

### `60f85da60` — Fix silent SFX: PlayerPrefs stored audio levels as int but read as float

_Claude, 2026-03-05 20:14:43 +0000_

```text
The root cause was SetPlayerPrefDefault only having an int overload,
so SFXLevel/MusicLevel/HapticsLevel were stored via SetInt but read
via GetFloat — which returns 0 on many platforms when the key was
written as int. This made sfxVolume = 0, silencing all sound effects.

Additional fixes:
- Add float overload for SetPlayerPrefDefault
- Fix missing OnChangeMusicLevel/OnChangeSFXLevel unsubscription in
  AudioSystem.OnDisable (event leak)
- Convert linear volume to decibels for AudioMixer (was passing 0-0.2
  linear to a mixer expecting dB)
- Fix music fade coroutines using SFXVolume instead of MusicVolume
```

```text
 Assets/_Scripts/App/Systems/Audio/AudioSystem.cs | 27 +++++++++++++++++----------
 Assets/_Scripts/Game/Settings/GameSetting.cs     | 11 ++++++++---
 2 files changed, 25 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
index 682bec922..339db49ed 100644
--- a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
+++ b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
@@ -140,6 +140,8 @@ namespace CosmicShore.App.Systems.Audio
         {
             GameSetting.OnChangeMusicEnabledStatus -= ChangeMusicEnabledStatus;
             GameSetting.OnChangeSFXEnabledStatus -= ChangeSFXEnabledStatus;
+            GameSetting.OnChangeMusicLevel -= ChangeMusicLevel;
+            GameSetting.OnChangeSFXLevel -= ChangeSFXLevel;
         }
 
         void ChangeMusicEnabledStatus(bool status)
@@ -199,8 +201,8 @@ namespace CosmicShore.App.Systems.Audio
 
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                // Fade out original clip masterVolume
-                activeAudioSource.volume = SFXVolume * (1 - (t / transitionTime));
+                // Fade out original clip volume
+                activeAudioSource.volume = MusicVolume * (1 - (t / transitionTime));
                 yield return null;
             }
 
@@ -211,8 +213,8 @@ namespace CosmicShore.App.Systems.Audio
 
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                // Fade in new clip masterVolume
-                activeAudioSource.volume = SFXVolume * (t / transitionTime);
+                // Fade in new clip volume
+                activeAudioSource.volume = MusicVolume * (t / transitionTime);
                 yield return null;
             }
         }
@@ -239,8 +241,8 @@ namespace CosmicShore.App.Systems.Audio
         {
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                originalSource.volume = SFXVolume * (1 - (t / transitionTime));
-                newSource.volume = SFXVolume * (t / transitionTime);
+                originalSource.volume = MusicVolume * (1 - (t / transitionTime));
+                newSource.volume = MusicVolume * (t / transitionTime);
                 yield return null;
             }
 
@@ -284,14 +286,19 @@ namespace CosmicShore.App.Systems.Audio
         }
         #region Mixer Methods
 
-        public void SetMixerMusicVolume(float value)
+        public void SetMixerMusicVolume(float linearVolume)
         {
-            masterMixer.SetFloat("MusicVolume", value);
+            masterMixer.SetFloat("MusicVolume", LinearToDecibels(linearVolume));
         }
 
-        public void SetMixerSFXVolume(float value)
+        public void SetMixerSFXVolume(float linearVolume)
         {
-            masterMixer.SetFloat("SFXVolume", value);
+            masterMixer.SetFloat("SFXVolume", LinearToDecibels(linearVolume));
+        }
+
+        static float LinearToDecibels(float linear)
+        {
+            return linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
         }
         #endregion
 
diff --git a/Assets/_Scripts/Game/Settings/GameSetting.cs b/Assets/_Scripts/Game/Settings/GameSetting.cs
index 6bd594e6e..909688f1d 100644
--- a/Assets/_Scripts/Game/Settings/GameSetting.cs
+++ b/Assets/_Scripts/Game/Settings/GameSetting.cs
@@ -87,9 +87,9 @@ namespace CosmicShore.Core
             SetPlayerPrefDefault(PlayerPrefKeys.InvertThrottleEnabled, 0);
             SetPlayerPrefDefault(PlayerPrefKeys.JoystickVisualsEnabled, 1);
 
-            SetPlayerPrefDefault(PlayerPrefKeys.MusicLevel, 1);
-            SetPlayerPrefDefault(PlayerPrefKeys.SFXLevel, 1);
-            SetPlayerPrefDefault(PlayerPrefKeys.HapticsLevel, 1);
+            SetPlayerPrefDefault(PlayerPrefKeys.MusicLevel, 1f);
+            SetPlayerPrefDefault(PlayerPrefKeys.SFXLevel, 1f);
+            SetPlayerPrefDefault(PlayerPrefKeys.HapticsLevel, 1f);
 
             PlayerPrefs.Save();
 
@@ -285,5 +285,10 @@ namespace CosmicShore.Core
         {
             if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
         }
+
+        void SetPlayerPrefDefault(PlayerPrefKeys key, float value)
+        {
+            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetFloat(key.ToString(), value);
+        }
     }
 }
\ No newline at end of file
```

</details>
