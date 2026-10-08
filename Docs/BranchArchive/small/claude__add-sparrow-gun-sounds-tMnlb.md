# Branch archive: `claude/add-sparrow-gun-sounds-tMnlb`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-23 by Claude
- **Unmerged commits:** 2
- **Forked from:** `f740df7cf` (2026-02-23, Merge branch 'claude/debug-logging-control-zZlM6' into development)
- **Tip:** `80e9d27aa`
- **Files touched (12):**
  - `Assets/_Audio/Sounds/Gameplay/DoubleDriftEnd.wav`
  - `Assets/_Audio/Sounds/Gameplay/DoubleDriftEnd.wav.meta`
  - `Assets/_Audio/Sounds/Gameplay/DoubleDriftStart.wav`
  - `Assets/_Audio/Sounds/Gameplay/DoubleDriftStart.wav.meta`
  - `Assets/_Audio/Sounds/Gameplay/SparrowGunFireEnd.wav`
  - `Assets/_Audio/Sounds/Gameplay/SparrowGunFireEnd.wav.meta`
  - `Assets/_Audio/Sounds/Gameplay/SparrowGunFireStart.wav`
  - `Assets/_Audio/Sounds/Gameplay/SparrowGunFireStart.wav.meta`
  - `Assets/_Prefabs/CORE/AudioSystem.prefab`
  - `Assets/_Scripts/App/Systems/Audio/AudioSystem.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/DriftActionSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs`

### `80e9d27aa` — Add Sparrow full-auto gun sounds and fix double-drift release audio

_Claude, 2026-02-23 19:10:57 +0000_

```text
Sparrow Full_Auto gun:
- Add SparrowGunFireStart (ascending mechanical whir, gun spinning up) and
  SparrowGunFireEnd (descending whir, spinning down) SFX categories
- Play start sound in FullAutoActionExecutor.Begin() and end sound in End()
- Synthesize matching WAV clips (ascending/descending frequency sweeps)
- Wire new clips into AudioSystem prefab

Double drift release sound:
- Add DoubleDriftStart and DoubleDriftEnd SFX categories for the sharp drift
- DoubleDriftStart: two-layer ascending sweep (wider range, more intense)
- DoubleDriftEnd: inverted mirror of DoubleDriftStart (descending, two layers)
- DriftActionSO now routes single drift to DriftStart/DriftEnd and sharp
  (double) drift to DoubleDriftStart/DoubleDriftEnd, making release sound
  match the same intensity profile as the press
```

```text
 Assets/_Audio/Sounds/Gameplay/DoubleDriftEnd.wav                            | Bin 0 -> 48554 bytes
 Assets/_Audio/Sounds/Gameplay/DoubleDriftEnd.wav.meta                       |  22 ++++++++++++++++++++++
 Assets/_Audio/Sounds/Gameplay/DoubleDriftStart.wav                          | Bin 0 -> 48554 bytes
 Assets/_Audio/Sounds/Gameplay/DoubleDriftStart.wav.meta                     |  22 ++++++++++++++++++++++
 Assets/_Audio/Sounds/Gameplay/SparrowGunFireEnd.wav                         | Bin 0 -> 39734 bytes
 Assets/_Audio/Sounds/Gameplay/SparrowGunFireEnd.wav.meta                    |  22 ++++++++++++++++++++++
 Assets/_Audio/Sounds/Gameplay/SparrowGunFireStart.wav                       | Bin 0 -> 30912 bytes
 Assets/_Audio/Sounds/Gameplay/SparrowGunFireStart.wav.meta                  |  22 ++++++++++++++++++++++
 Assets/_Prefabs/CORE/AudioSystem.prefab                                     |   4 ++++
 Assets/_Scripts/App/Systems/Audio/AudioSystem.cs                            |  12 ++++++++++++
 Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/DriftActionSO.cs    |   8 ++++++--
 Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs |   4 ++++
 12 files changed, 114 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
index 682bec922..8d8669aff 100644
--- a/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
+++ b/Assets/_Scripts/App/Systems/Audio/AudioSystem.cs
@@ -51,6 +51,10 @@ namespace CosmicShore.App.Systems.Audio
         DriftEnd = 17,
         EnergyGain = 18,
         SpeedBurst = 19,
+        DoubleDriftStart = 20,
+        DoubleDriftEnd = 21,
+        SparrowGunFireStart = 22,
+        SparrowGunFireEnd = 23,
     }
 
     [DefaultExecutionOrder(-1)]
@@ -98,6 +102,10 @@ namespace CosmicShore.App.Systems.Audio
         [SerializeField] AudioClip DriftEndAudioClip;
         [SerializeField] AudioClip EnergyGainAudioClip;
         [SerializeField] AudioClip SpeedBurstAudioClip;
+        [SerializeField] AudioClip DoubleDriftStartAudioClip;
+        [SerializeField] AudioClip DoubleDriftEndAudioClip;
+        [SerializeField] AudioClip SparrowGunFireStartAudioClip;
+        [SerializeField] AudioClip SparrowGunFireEndAudioClip;
 
         public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
         public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }
@@ -337,6 +345,10 @@ namespace CosmicShore.App.Systems.Audio
                 {GameplaySFXCategory.DriftEnd, DriftEndAudioClip},
                 {GameplaySFXCategory.EnergyGain, EnergyGainAudioClip},
                 {GameplaySFXCategory.SpeedBurst, SpeedBurstAudioClip},
+                {GameplaySFXCategory.DoubleDriftStart, DoubleDriftStartAudioClip},
+                {GameplaySFXCategory.DoubleDriftEnd, DoubleDriftEndAudioClip},
+                {GameplaySFXCategory.SparrowGunFireStart, SparrowGunFireStartAudioClip},
+                {GameplaySFXCategory.SparrowGunFireEnd, SparrowGunFireEndAudioClip},
             };
         }
     }
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
index a03c5d82b..a9314eb0d 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
@@ -2,6 +2,7 @@
 using System.Threading;
 using UnityEngine;
 using Cysharp.Threading.Tasks;
+using CosmicShore.App.Systems.Audio;
 using CosmicShore.Core;
 using CosmicShore.Game;
 using CosmicShore.Game.Projectiles;
@@ -87,6 +88,7 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         _cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
         var token = _cts.Token;
 
+        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.SparrowGunFireStart);
         FireLoopAsync(so, token).Forget();
     }
 
@@ -95,6 +97,8 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         if (_cts == null)
             return;
 
+        AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.SparrowGunFireEnd);
+
         try
         {
             if (!_cts.IsCancellationRequested)
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
