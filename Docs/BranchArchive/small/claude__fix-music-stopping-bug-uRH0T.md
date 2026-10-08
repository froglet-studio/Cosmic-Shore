# Branch archive: `claude/fix-music-stopping-bug-uRH0T`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-08 by Claude
- **Unmerged commits:** 1
- **Forked from:** `14a193123` (2026-03-07, Merge pull request #414 from froglet-studio/claude/migrate-debug-logs-fNeXt)
- **Tip:** `5aed761e7`
- **Files touched (8):**
  - `Assets/_Scripts/App/Systems/Audio/AudioSystem.cs`
  - `Assets/_Scripts/Game/Environment/Cytoplasm/NudgeShard.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Game/Environment/FlowField/Crystal.cs`
  - `Assets/_Scripts/Game/Projectiles/AOEExplosion.cs`
  - `Assets/_Scripts/Game/Projectiles/Mine.cs`
  - `Assets/_Scripts/Game/Ship/Prism.cs`
  - `ProjectSettings/AudioManager.asset`

### `5aed761e7` — Fix music stopping bug and add spatial audio for gameplay SFX

_Claude, 2026-03-08 02:16:00 +0000_

```text
- Fix crossfade coroutines using SFXVolume instead of MusicVolume, which
  caused music to fade to 0 when volumes differed
- Set music AudioSource priority to 0 (highest) to prevent voice stealing
- Add per-clip SFX cooldown (50ms) to prevent voice exhaustion from mass
  prism/crystal destruction
- Route Crystal explosion audio through AudioSystem instead of unmanaged
  AudioSource that bypassed the mixer and died with the crystal
- Fix SetMixerSFXVolume using wrong exposed parameter name ("SFXVolume"
  vs actual "EnvironmentVolume")
- Add AudioMixerGroup routing fields for proper mixer integration
- Add spatial audio pool (8 3D AudioSources) for distance-based SFX
  attenuation on world-positioned events (prisms, crystals, mines,
  explosions, creatures)
- Increase real voice count from 32 to 48 for headroom
```

```text
 Assets/_Scripts/App/Systems/Audio/AudioSystem.cs           | 118 +++++++++++++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/Environment/Cytoplasm/NudgeShard.cs   |   2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs |   2 +-
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs      |   3 +-
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs           |   2 +-
 Assets/_Scripts/Game/Projectiles/Mine.cs                   |   2 +-
 Assets/_Scripts/Game/Ship/Prism.cs                         |   4 +-
 ProjectSettings/AudioManager.asset                         |   2 +-
 8 files changed, 117 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 294 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
index 682bec922..e1dee4c06 100644
--- a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
+++ b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
@@ -1,4 +1,4 @@
-﻿using System;
+using System;
 using System.Collections;
 using System.Collections.Generic;
 using CosmicShore.Core;
@@ -64,6 +64,18 @@ namespace CosmicShore.App.Systems.Audio
         [SerializeField] float musicVolume = .1f;
         [SerializeField] float sfxVolume = .1f;
 
+        [Header("Mixer Groups")]
+        [SerializeField] AudioMixerGroup musicMixerGroup;
+        [SerializeField] AudioMixerGroup sfxMixerGroup;
+
+        [Header("SFX Throttling")]
+        [SerializeField] float sfxCooldownSeconds = 0.05f;
+
+        [Header("Spatial Audio")]
+        [SerializeField] int spatialSourcePoolSize = 8;
+        [SerializeField] float spatialMaxDistance = 500f;
+        [SerializeField] float spatialMinDistance = 10f;
+
         [Header("Menu Audio")]
         [SerializeField] AudioClip OptionClickAudioClip;
         [SerializeField] AudioClip OpenViewAudioClip;
@@ -112,6 +124,10 @@ namespace CosmicShore.App.Systems.Audio
         Dictionary<MenuAudioCategory, AudioClip> MenuAudioClips;
         Dictionary<GameplaySFXCategory, AudioClip> GameplaySFXClips;
 
+        readonly Dictionary<int, float> _lastPlayTime = new();
+        AudioSource[] _spatialPool;
+        int _spatialPoolIndex;
+
         public bool MusicEnabled { get { return musicEnabled; } }
         public bool SFXEnabled { get { return sfxEnabled; } }
         #endregion
@@ -120,6 +136,9 @@ namespace CosmicShore.App.Systems.Audio
         {
             InitializeMenuAudioClips();
             InitializeGameplaySFXClips();
+            InitializeAudioSourcePriorities();
+            InitializeMixerGroupRouting();
+            InitializeSpatialPool();
 
             musicEnabled = GameSetting.Instance.MusicEnabled;
             sfxEnabled = GameSetting.Instance.SFXEnabled;
@@ -128,6 +147,46 @@ namespace CosmicShore.App.Systems.Audio
             ChangeMusicEnabledStatus(musicEnabled);
         }
 
+        void InitializeAudioSourcePriorities()
+        {
+            musicSource1.priority = 0;
+            musicSource2.priority = 0;
+            sfxSource.priority = 128;
+        }
+
+        void InitializeMixerGroupRouting()
+        {
+            if (musicMixerGroup != null)
+            {
+                musicSource1.outputAudioMixerGroup = musicMixerGroup;
+                musicSource2.outputAudioMixerGroup = musicMixerGroup;
+            }
+            if (sfxMixerGroup != null)
+            {
+                sfxSource.outputAudioMixerGroup = sfxMixerGroup;
+            }
+        }
+
+        void InitializeSpatialPool()
+        {
+            _spatialPool = new AudioSource[spatialSourcePoolSize];
+            for (int i = 0; i < spatialSourcePoolSize; i++)
+            {
+                var go = new GameObject($"SpatialSFX_{i}");
+                go.transform.SetParent(transform);
+                var src = go.AddComponent<AudioSource>();
+                src.spatialBlend = 1f;
+                src.rolloffMode = AudioRolloffMode.Logarithmic;
+                src.minDistance = spatialMinDistance;
+                src.maxDistance = spatialMaxDistance;
+                src.priority = 200;
+                src.playOnAwake = false;
+                if (sfxMixerGroup != null)
+                    src.outputAudioMixerGroup = sfxMixerGroup;
+                _spatialPool[i] = src;
+            }
+        }
+
         void OnEnable()
         {
             GameSetting.OnChangeMusicEnabledStatus += ChangeMusicEnabledStatus;
@@ -146,7 +205,7 @@ namespace CosmicShore.App.Systems.Audio
         {
             CSDebug.Log($"AudioSystem.OnChangeAudioEnabledStatus - status: {status}");
 
-            musicEnabled = status;            
+            musicEnabled = status;
             SetMixerMusicVolume(musicEnabled ? musicVolume : 0);
         }
         void ChangeSFXEnabledStatus(bool status)
@@ -199,8 +258,8 @@ namespace CosmicShore.App.Systems.Audio
 
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                // Fade out original clip masterVolume
-                activeAudioSource.volume = SFXVolume * (1 - (t / transitionTime));
+                // Fade out original clip volume
+                activeAudioSource.volume = MusicVolume * (1 - (t / transitionTime));
                 yield return null;
             }
 
@@ -211,8 +270,8 @@ namespace CosmicShore.App.Systems.Audio
 
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                // Fade in new clip masterVolume
-                activeAudioSource.volume = SFXVolume * (t / transitionTime);
+                // Fade in new clip volume
+                activeAudioSource.volume = MusicVolume * (t / transitionTime);
                 yield return null;
             }
         }
@@ -239,8 +298,8 @@ namespace CosmicShore.App.Systems.Audio
         {
             for (float t = 0; t < transitionTime; t += Time.deltaTime)
             {
-                originalSource.volume = SFXVolume * (1 - (t / transitionTime));
-                newSource.volume = SFXVolume * (t / transitionTime);
+                originalSource.volume = MusicVolume * (1 - (t / transitionTime));
+                newSource.volume = MusicVolume * (t / transitionTime);
                 yield return null;
             }
 
@@ -271,6 +330,14 @@ namespace CosmicShore.App.Systems.Audio
                 Debug.LogWarning($"AudioSystem.PlayGameplaySFX: No audio clip assigned for {category}");
         }
 
+        public void PlayGameplaySFX(GameplaySFXCategory category, Vector3 worldPosition)
+        {
+            if (GameplaySFXClips.TryGetValue(category, out var clip) && clip != null)
+                PlaySpatialSFX(clip, worldPosition);
+            else
+                Debug.LogWarning($"AudioSystem.PlayGameplaySFX: No audio clip assigned for {category}");
+        }
```

</details>
