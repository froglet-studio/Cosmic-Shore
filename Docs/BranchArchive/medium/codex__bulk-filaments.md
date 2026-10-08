# Branch archive: `codex/bulk-filaments`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-06-27 by Todd VanTongeren
- **Unmerged commits:** 6
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/569
- **Forked from:** `00d6781a7` (2026-06-18, cleaned up the lighning effect for the shield)
- **Tip:** `2a448836b`
- **Files touched (116):**
  - `Assets/Resources/Audio.meta`
  - `Assets/Resources/Audio/BulkFilaments.meta`
  - `Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3`
  - `Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3.meta`
  - `Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3`
  - `Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3.meta`
  - `Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3`
  - `Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3.meta`
  - `Assets/Resources/Audio/Music.meta`
  - `Assets/Resources/Audio/Music/Dopamine.mp3`
  - `Assets/Resources/Audio/Music/Dopamine.mp3.meta`
  - `Assets/Resources/Textures.meta`
  - `Assets/Resources/Textures/BulkFilaments.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkFaunaSquidSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkFaunaSquidSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkFilamentFlowSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkFilamentFlowSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkLatchRingFlareSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkLatchRingFlareSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkNaniteInsectSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkNaniteInsectSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkPowerDiamondSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkPowerDiamondSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkRootFlareSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkRootFlareSheet.png.meta`
  - `Assets/Resources/Textures/BulkFilaments/BulkTetherCrackleSheet.png`
  - `Assets/Resources/Textures/BulkFilaments/BulkTetherCrackleSheet.png.meta`
  - `Assets/_Graphics/ElementShapes.meta`
  - `Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader`
  - `Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader.meta`
  - `Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader`
  - `Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader.meta`
  - `Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader`
  - `Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader.meta`
  - `Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader`
  - `Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader.meta`
  - `Assets/_SO_Assets/Games/ArcadeGameTheBulkFilaments.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameTheBulkFilaments.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/AllGames.asset`
  - `Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset`
  - … and 76 more

### `82178fcde` — Add Bulk Filaments arcade prototype

_Todd VanTongeren, 2026-06-19 08:02:59 -0400_

```text
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Visuals.cs  |  171 +++
 .../Controller/Arcade/BulkFilamentsController.Visuals.cs.meta         |   11 +
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  291 +++++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs.meta     |   11 +
 Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs           |    4 +-
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    |    2 +-
 Assets/_Scripts/Controller/IO/InputController.cs                      |    9 +-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs    |    8 +-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs  |    5 +-
 Assets/_Scripts/Controller/Player/Player.cs                           |    3 +-
 Assets/_Scripts/Controller/Player/PlayerSpawner.cs                    |    7 +-
 Assets/_Scripts/Controller/Player/PlayerSpawnerAdapterBase.cs         |    4 +-
 Assets/_Scripts/Controller/Vessel/VesselSpawner.cs                    |    7 +-
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  |    3 +-
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    3 +-
 Assets/_Scripts/Editor/BulkFilamentsPlayModeDriver.cs                 |  265 ++++
 Assets/_Scripts/Editor/BulkFilamentsPlayModeDriver.cs.meta            |    2 +
 Assets/_Scripts/System/AuthenticationSceneController.cs               |   16 +
 Assets/_Scripts/System/SceneLoader.cs                                 |    7 +-
 Assets/_Scripts/System/Squads/SquadSystem.cs                          |   35 +-
 Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs                  |    4 +-
 Assets/_Scripts/UI/Elements/SquadMemberCard.cs                        |   19 +-
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |    3 +-
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 |   11 +-
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                         |   39 +-
 .../Utility/DataContainers/BulkFilamentsEndGameCinematicController.cs |   31 +
 .../DataContainers/BulkFilamentsEndGameCinematicController.cs.meta    |   11 +
 Docs/SCENES.md                                                        |   18 +
 ProjectSettings/EditorBuildSettings.asset                             |    3 +
 59 files changed, 4159 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2325 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index ff02af9d8..54c305773 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -6,7 +6,6 @@ using CosmicShore.Utility;
 using CosmicShore.Gameplay;
 using Obvious.Soap;
 using CosmicShore.Data;
-using Reflex.Attributes;
 using System.Linq;
 
 namespace CosmicShore.Gameplay
@@ -60,7 +59,7 @@ namespace CosmicShore.Gameplay
         [Header("Targeting")]
         [Tooltip("When true, AI targets enemy players instead of crystals/items (used for Joust)")]
         [SerializeField] bool seekPlayers;
-        [Inject] GameDataSO gameData;
+        GameDataSO gameData;
         [Tooltip("Cadence (seconds) for re-selecting which opponent to chase while one is locked.")]
         [SerializeField] float playerSeekUpdateInterval = 0.5f;
         [Tooltip("Faster re-scan cadence (seconds) used while the AI has NO opponent locked, so it re-acquires promptly (e.g. a 1v1 opponent mid-respawn).")]
@@ -462,4 +461,4 @@ namespace CosmicShore.Gameplay
 
         #endregion
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md b/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
new file mode 100644
index 000000000..80455fc6d
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
@@ -0,0 +1,59 @@
+# The Bulk Filaments
+
+First playable prototype for `GameModes.TheBulkFilaments`.
+
+## Fantasy
+
+Arks sometimes send vessels out of a cell and into the Bulk, the higher-dimensional
+embedding around the known universe. A pilot rides energy filaments through a
+wormhole-like shortcut; if the vessel survives, the Ark's navigation system can
+follow the computed route and deliver information faster than rival couriers.
+
+## Current V1
+
+- Single-player only.
+- Squirrel-only via `ArcadeGameTheBulkFilaments.asset`.
+- Scene: `MinigameBulkFilaments`.
+- Controller: `BulkFilamentsController`.
+- Music: `Resources/Audio/Music/Dopamine.mp3`.
+- Arcade Explore card: `OrganicRematchGames.asset` is the runtime-injected menu list.
+- Runtime-generated wormhole, filaments, crystals, latch rings, nanites, and simple
+  HUD telemetry.
+
+## Controls
+
+- Left stick up/down: bias speed along the current filament.
+- Right stick left/right: orbit around the current filament.
+- Left or right trigger: fire the auto-aimed latch rings at the next filament.
+- Keyboard fallback: W/S throttle, A/D or arrows orbit, Space/Enter latch.
+
+## Loop
+
+1. The active filament is green.
+2. The next filament shifts from red/orange/yellow toward green near closest approach.
+3. Trigger inside the timing window to transfer.
+4. Trigger too early/late to miss; if the vessel runs out of filament, it respawns at
+   the previous filament while the clock and nanite chase continue.
+5. Filament nanites rise from below and catch the player if they fail to keep pace.
+6. Finish after an intensity-scaled number of transfers.
+
+## Intensity
+
+Transfer count scales as `12 / 18 / 24 / 30` for intensities 1-4.
+
+## Scoring
+
+V1 uses golf-style scoring:
+
+`elapsed time + respawn penalties - crystal time credits`
+
+Lower score is better. Crystals collected are also written to `RoundStats`.
+
+## Follow-Up Polish
+
+- Replace cloned Wildlife Blitz HUD/endgame references with Bulk-specific HUD/endgame
+  presentation.
+- Replace runtime primitive placeholders with authored shader/sprite assets.
+- Add actual audio analysis or authored beat map for Dopamine lightning/surges.
+- Add multiplayer lanes where each player gets a distinct filament chain in the same
+  wormhole volume.
diff --git a/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
index 6e6f3e6bd..492bc06d9 100644
--- a/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
+++ b/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
@@ -13,7 +13,7 @@ namespace CosmicShore.Gameplay
     public abstract class BaseScoreTracker : NetworkBehaviour, IScoreTracker
     {
         [SerializeField] protected ScriptableEventNoParam OnClickToMainMenu;
-        [Inject] protected GameDataSO gameData;
+        [SerializeField, Inject] protected GameDataSO gameData;
         [SerializeField] protected bool golfRules;
         [SerializeField] protected ScoringConfig[] scoringConfigs;
 
@@ -161,4 +161,4 @@ namespace CosmicShore.Gameplay
         public ScoringModes Mode;
         public float Multiplier;
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
new file mode 100644
index 000000000..ba21efcc2
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
@@ -0,0 +1,55 @@
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    public partial class BulkFilamentsController
+    {
+        void EnsureSingleAudioListener()
+        {
+            AudioListener keeper = FindEnabledAudioListener();
+            if (!keeper)
+            {
+                if (!_mainCamera)
+                    _mainCamera = Camera.main;
+
+                if (_mainCamera)
+                {
+                    keeper = _mainCamera.GetComponent<AudioListener>();
+                    if (!keeper)
+                        keeper = _mainCamera.gameObject.AddComponent<AudioListener>();
+                }
+                else if (_runtimeRoot)
+                {
+                    keeper = _runtimeRoot.GetComponent<AudioListener>();
+                    if (!keeper)
+                        keeper = _runtimeRoot.AddComponent<AudioListener>();
+                }
+
+                if (keeper)
+                    keeper.enabled = true;
+            }
+
```

</details>

### `5e8d4aca5` — Polish Bulk Filaments playable prototype

_Todd VanTongeren, 2026-06-23 22:17:05 -0400_

```text
 Assets/Resources/Audio/BulkFilaments.meta                             |   8 +
 Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3              | Bin 0 -> 36261 bytes
 Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3.meta         |  23 +++
 Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3               | Bin 0 -> 88269 bytes
 Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3.meta          |  23 +++
 Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3             | Bin 0 -> 39429 bytes
 Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3.meta        |  23 +++
 Assets/_Graphics/ElementShapes.meta                                   |   4 -
 Assets/_Scenes/Singleplayer Scenes/MinigameBulkFilaments.unity        |  78 ++++++---
 Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md                   |  43 +++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs    | 273 ++++++++++++++++++++++++++++++++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.CameraInput.cs | 107 ++++++++++++-
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs  |   4 +-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Latch.cs    | 166 +++++++++++++++++++
 .../Arcade/BulkFilamentsController.Latch.cs.meta}                     |   2 +-
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Lightning.cs   | 220 +++++++++++++++++++++++++
 .../Controller/Arcade/BulkFilamentsController.Lightning.cs.meta       |  11 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Run.cs      |  81 +++++++---
 .../_Scripts/Controller/Arcade/BulkFilamentsController.StartFlow.cs   | 134 ++++++++++++++++
 .../Controller/Arcade/BulkFilamentsController.StartFlow.cs.meta       |  11 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Visuals.cs  | 120 ++++++++++----
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Waveform.cs | 131 +++++++++++++++
 .../Controller/Arcade/BulkFilamentsController.Waveform.cs.meta        |  11 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  62 +++++---
 .../_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs  |  65 +++++++-
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |  56 ++++++-
 .../Utility/DataContainers/BulkFilamentsEndGameCinematicController.cs |  31 ----
 Docs/BulkFilaments/DEVLOG.md                                          |  75 +++++++++
 Docs/BulkFilaments/PRD.md                                             | 120 ++++++++++++++
 29 files changed, 1724 insertions(+), 158 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2191 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md b/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
index 80455fc6d..eb76fd98c 100644
--- a/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
+++ b/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
@@ -2,6 +2,11 @@
 
 First playable prototype for `GameModes.TheBulkFilaments`.
 
+Living docs:
+
+- `Docs/BulkFilaments/PRD.md`
+- `Docs/BulkFilaments/DEVLOG.md`
+
 ## Fantasy
 
 Arks sometimes send vessels out of a cell and into the Bulk, the higher-dimensional
@@ -16,30 +21,45 @@ follow the computed route and deliver information faster than rival couriers.
 - Scene: `MinigameBulkFilaments`.
 - Controller: `BulkFilamentsController`.
 - Music: `Resources/Audio/Music/Dopamine.mp3`.
+- Latch SFX: `Resources/Audio/BulkFilaments/BulkGrappleFire.mp3` and
+  `Resources/Audio/BulkFilaments/BulkLatchSurge.mp3`.
+- Power crystal SFX: `Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3`.
 - Arcade Explore card: `OrganicRematchGames.asset` is the runtime-injected menu list.
-- Runtime-generated wormhole, filaments, crystals, latch rings, nanites, and simple
-  HUD telemetry.
+- Runtime-generated wormhole, filaments, live music waveform overlays, crystals,
+  latch rings, nanites, and simple HUD telemetry.
 
 ## Controls
 
-- Left stick up/down: bias speed along the current filament.
+- Left stick up/down: look ahead/down the wormhole with the follow camera.
+- Left stick left/right: zoom the follow camera out/in.
 - Right stick left/right: orbit around the current filament.
-- Left or right trigger: fire the auto-aimed latch rings at the next filament.
-- Keyboard fallback: W/S throttle, A/D or arrows orbit, Space/Enter latch.
+- Right stick up/down: bias speed along the current filament.
+- Right trigger: fire the front latch ring at the next filament.
+- Left trigger: fire the rear latch ring after the front ring locks.
+- Keyboard fallback: W/S throttle, A/D or left/right arrows orbit,
+  up/down arrows camera look, Space front latch, Enter rear latch.
 
 ## Loop
 
 1. The active filament is green.
 2. The next filament shifts from red/orange/yellow toward green near closest approach.
-3. Trigger inside the timing window to transfer.
-4. Trigger too early/late to miss; if the vessel runs out of filament, it respawns at
+3. Pull or hold right trigger inside the timing window to lock the front ring.
+4. After the front ring energizes, pull left trigger within 2 seconds to complete
+   the transfer.
+5. Trigger too early/late to miss; if the vessel runs out of filament, it respawns at
    the previous filament while the clock and nanite chase continue.
-5. Filament nanites rise from below and catch the player if they fail to keep pace.
-6. Finish after an intensity-scaled number of transfers.
+6. Filament nanites rise from below and catch the player if they fail to keep pace.
+7. Power crystals add speed, lightning pickup bursts, and time credit.
+8. Wormhole lightning crawls along the walls; filament-to-filament bolts can reset
+   speed if they hit the vessel.
+9. Live Dopamine waveform ribbons pulse over the filaments opposite the vessel's
+   travel direction at 8x vessel speed.
+10. Finish after the Bulk controller's 20-30 transfer chain is completed.
 
 ## Intensity
 
-Transfer count scales as `12 / 18 / 24 / 30` for intensities 1-4.
+Transfer count scales from roughly `24` to `30` transfers for intensities 1-4,
+with the `Dopamine.mp3` length used as a lower bound when available.
 
 ## Scoring
 
@@ -54,6 +74,7 @@ Lower score is better. Crystals collected are also written to `RoundStats`.
 - Replace cloned Wildlife Blitz HUD/endgame references with Bulk-specific HUD/endgame
   presentation.
 - Replace runtime primitive placeholders with authored shader/sprite assets.
-- Add actual audio analysis or authored beat map for Dopamine lightning/surges.
+- Extend live Dopamine analysis from filament waveforms into authored lightning
+  and fauna surge beats.
 - Add multiplayer lanes where each player gets a distinct filament chain in the same
   wormhole volume.
diff --git a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
index ba21efcc2..42dfa18a4 100644
--- a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
+++ b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
@@ -1,9 +1,266 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
 {
     public partial class BulkFilamentsController
     {
+        const string GrappleFireResourcePath = "Audio/BulkFilaments/BulkGrappleFire";
+        const string LatchSurgeResourcePath = "Audio/BulkFilaments/BulkLatchSurge";
+        const string PowerCrystalResourcePath = "Audio/BulkFilaments/BulkPowerCrystal";
+
+        AudioSource _sfxSource;
+        AudioClip _grappleFireClip;
+        AudioClip _latchSurgeClip;
+        AudioClip _latchMissClip;
+        AudioClip _powerCrystalClip;
+        readonly List<AudioSourceSnapshot> _bulkAudioSnapshots = new();
+        bool _audioStartupLogged;
+        bool _bulkMixApplied;
+
+        void StartMusic()
+        {
+            EnsureBulkAudioSources();
+            if (!_musicSource)
+                return;
+
+            if (!_musicSource.clip)
+                _musicSource.clip = Resources.Load<AudioClip>(MusicResourcePath);
+
+            _musicSource.loop = true;
+            _musicSource.playOnAwake = false;
+            _musicSource.pitch = 1f;
+            _musicSource.volume = 1f;
+            _musicSource.spatialBlend = 0f;
+            _musicSource.ignoreListenerPause = true;
+            _musicSource.mute = false;
+            _musicSource.outputAudioMixerGroup = null;
+
+            AudioListener.pause = false;
+            AudioListener.volume = 1f;
+            EnsureSingleAudioListener();
+            ApplyBulkAudioMix();
+
+            if (_musicSource.clip)
+            {
+                if (!_musicSource.isPlaying)
+                    _musicSource.Play();
+                CSDebug.Log($"[BulkFilaments] Music playing: {_musicSource.clip.name}, length={_musicSource.clip.length:0.0}s, listenerVolume={AudioListener.volume:0.00}.");
+            }
+            else
+            {
+                CSDebug.LogWarning($"[BulkFilaments] Missing music resource at Resources/{MusicResourcePath}.");
+            }
+        }
+
+        void PlayLatchFireSound()
+        {
+            EnsureBulkAudioSources();
+            PlayOneShotClip(_grappleFireClip, 1.2f, "grapple_fire");
+        }
+
```

</details>

### `e3e2dca17` — Upgrade Bulk Filaments visuals

_Todd VanTongeren, 2026-06-24 21:53:37 -0400_

```text
 Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader             |  67 +++++++++++++
 Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader.meta        |  10 ++
 Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader           | 119 +++++++++++++++++++++++
 Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader.meta      |  10 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs    |  17 ++++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.CameraInput.cs |   5 +-
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs  | 166 ++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Geometry.cs |  88 ++++++++++++++++-
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Lightning.cs   | 130 ++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Run.cs      |  80 +++++++++++++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Visuals.cs  | 112 ++++++++++++++++++++-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Waveform.cs |   3 +-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  91 ++++++++++++++++-
 Assets/_Scripts/Editor/BulkFilamentsPlayModeDriver.cs                 | 159 ++++++++++++++++++++++++++++++
 Docs/BulkFilaments/DEVLOG.md                                          |  39 ++++++++
 15 files changed, 1067 insertions(+), 29 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1591 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader b/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader
new file mode 100644
index 000000000..645d38a2e
--- /dev/null
+++ b/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader
@@ -0,0 +1,67 @@
+Shader "CosmicShore/BulkEnergyUnlit"
+{
+    Properties
+    {
+        _BaseColor ("Base Color", Color) = (0.2, 1, 0.8, 1)
+        _Color ("Color", Color) = (0.2, 1, 0.8, 1)
+        _EmissionColor ("Emission Color", Color) = (0.2, 1, 0.8, 1)
+        _Alpha ("Alpha", Range(0, 1)) = 1
+        _Pulse ("Pulse", Range(0, 4)) = 0
+    }
+    SubShader
+    {
+        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
+        Blend SrcAlpha One
+        ZWrite Off
+        Cull Off
+
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #include "UnityCG.cginc"
+
+            fixed4 _BaseColor;
+            fixed4 _Color;
+            fixed4 _EmissionColor;
+            float _Alpha;
+            float _Pulse;
+
+            struct appdata
+            {
+                float4 vertex : POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            struct v2f
+            {
+                float4 vertex : SV_POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            v2f vert(appdata v)
+            {
+                v2f o;
+                o.vertex = UnityObjectToClipPos(v.vertex);
+                o.uv = v.uv;
+                o.color = v.color;
+                return o;
+            }
+
+            fixed4 frag(v2f i) : SV_Target
+            {
+                float stripe = sin((i.uv.x * 31.0 + _Time.y * 5.5) * 6.2831853) * 0.5 + 0.5;
+                float glow = 1.0 + _Pulse * 0.55 + stripe * 0.22;
+                fixed4 color = lerp(_BaseColor, _EmissionColor, saturate(0.42 + _Pulse * 0.18));
+                color.rgb *= glow;
+                color.a = saturate(_Alpha * _BaseColor.a * (0.72 + _Pulse * 0.16 + stripe * 0.12));
+                return color * i.color;
+            }
+            ENDCG
+        }
+    }
+    FallBack "Unlit/Color"
+}
diff --git a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
new file mode 100644
index 000000000..9888860c3
--- /dev/null
+++ b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
@@ -0,0 +1,119 @@
+Shader "CosmicShore/BulkVoronoiMirror"
+{
+    Properties
+    {
+        _BaseColor ("Mirror Tint", Color) = (0.12, 0.5, 0.95, 0.58)
+        _Color ("Color", Color) = (0.12, 0.5, 0.95, 0.58)
+        _LineColor ("Cell Line Color", Color) = (0.03, 0.9, 1, 1)
+        _Alpha ("Alpha", Range(0, 1)) = 0.58
+        _Pulse ("Pulse", Range(0, 4)) = 0
+        _MirrorStrength ("Mirror Strength", Range(0, 1)) = 0.9
+        _Distortion ("Facet Distortion", Range(0, 2)) = 0.55
+    }
+    SubShader
+    {
+        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
+        Blend SrcAlpha OneMinusSrcAlpha
+        ZWrite Off
+        Cull Off
+
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #include "UnityCG.cginc"
+
+            fixed4 _BaseColor;
+            fixed4 _Color;
+            fixed4 _LineColor;
+            float _Alpha;
+            float _Pulse;
+            float _MirrorStrength;
+            float _Distortion;
+
+            struct appdata
+            {
+                float4 vertex : POSITION;
+                float3 normal : NORMAL;
+                float2 uv : TEXCOORD0;
+            };
+
+            struct v2f
+            {
+                float4 vertex : SV_POSITION;
+                float3 worldPos : TEXCOORD0;
+                float3 worldNormal : TEXCOORD1;
+                float2 uv : TEXCOORD2;
+            };
+
+            float2 hash22(float2 p)
+            {
+                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
+                return frac(sin(p) * 43758.5453);
+            }
+
+            float voronoiEdge(float2 uv)
+            {
+                float2 g = floor(uv);
+                float2 f = frac(uv);
+                float d1 = 8.0;
+                float d2 = 8.0;
+
+                for (int y = -1; y <= 1; y++)
+                {
+                    for (int x = -1; x <= 1; x++)
+                    {
+                        float2 lattice = float2(x, y);
+                        float2 offset = hash22(g + lattice);
+                        offset = 0.5 + 0.48 * sin(_Time.y * 0.22 + 6.2831853 * offset);
+                        float2 r = lattice + offset - f;
+                        float d = dot(r, r);
```

</details>

### `4ca148e11` — Add Bulk Filaments depth and finale effects

_Todd VanTongeren, 2026-06-24 22:08:41 -0400_

```text
 Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader             |  80 +++++++++++++++++
 Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader.meta        |  10 +++
 Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader           |   6 ++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs  | 149 +++++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Latch.cs    |   2 +
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Lightning.cs   |  83 ++++++++++++-----
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Run.cs      | 139 +++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Visuals.cs  | 155 ++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  55 ++++++++++++
 Assets/_Scripts/Editor/BulkFilamentsPlayModeDriver.cs                 |  20 +++++
 Docs/BulkFilaments/DEVLOG.md                                          |  36 ++++++++
 11 files changed, 699 insertions(+), 36 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1176 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader b/Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader
new file mode 100644
index 000000000..f3f62eafe
--- /dev/null
+++ b/Assets/_Graphics/Materials/Shaders/BulkGlyphSprite.shader
@@ -0,0 +1,80 @@
+Shader "CosmicShore/BulkGlyphSprite"
+{
+    Properties
+    {
+        _BaseColor ("Base Color", Color) = (0.02, 0.04, 0.06, 0.9)
+        _Color ("Color", Color) = (0.02, 0.04, 0.06, 0.9)
+        _DarkColor ("Dark Color", Color) = (0, 0.004, 0.012, 0.95)
+        _AccentColor ("Accent Color", Color) = (0.04, 0.95, 1, 0.75)
+        _Alpha ("Alpha", Range(0, 1)) = 0.85
+        _Pulse ("Pulse", Range(0, 4)) = 0
+        _Phase ("Phase", Float) = 0
+    }
+    SubShader
+    {
+        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" }
+        Blend SrcAlpha OneMinusSrcAlpha
+        ZWrite Off
+        Cull Off
+
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #include "UnityCG.cginc"
+
+            fixed4 _BaseColor;
+            fixed4 _Color;
+            fixed4 _DarkColor;
+            fixed4 _AccentColor;
+            float _Alpha;
+            float _Pulse;
+            float _Phase;
+
+            struct appdata
+            {
+                float4 vertex : POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            struct v2f
+            {
+                float4 vertex : SV_POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            v2f vert(appdata v)
+            {
+                v2f o;
+                o.vertex = UnityObjectToClipPos(v.vertex);
+                o.uv = v.uv;
+                o.color = v.color;
+                return o;
+            }
+
+            fixed4 frag(v2f i) : SV_Target
+            {
+                float2 uv = i.uv;
+                float time = _Time.y + _Phase;
+                float border = 1.0 - smoothstep(0.0, 0.08, min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));
+                float scan = step(0.62, frac(uv.y * 9.0 + sin(uv.x * 15.0 + time * 1.7) * 0.18 + time * 0.42));
+                float circuit = smoothstep(0.026, 0.0, abs(frac(uv.x * 4.0 + time * 0.07) - 0.5) - 0.18);
+                float notch = step(0.78, frac((uv.x + uv.y) * 5.0 + sin(time * 0.8)));
+                float glyph = saturate(border * 0.72 + scan * 0.46 + circuit * 0.62 + notch * 0.28);
+                float vignette = smoothstep(0.0, 0.35, uv.x) * smoothstep(0.0, 0.35, uv.y) *
+                                 smoothstep(0.0, 0.35, 1.0 - uv.x) * smoothstep(0.0, 0.35, 1.0 - uv.y);
+
+                fixed4 color;
+                color.rgb = lerp(_DarkColor.rgb, _BaseColor.rgb, 0.38 + _Pulse * 0.08);
+                color.rgb = lerp(color.rgb, _AccentColor.rgb * (0.55 + _Pulse * 0.22), glyph * 0.34);
+                color.a = saturate(_Alpha * vignette * (0.18 + glyph * 0.78));
+                return color * i.color;
+            }
+            ENDCG
+        }
+    }
+    FallBack "Sprites/Default"
+}
diff --git a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
index 9888860c3..745950c0d 100644
--- a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
+++ b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
@@ -31,6 +31,8 @@ Shader "CosmicShore/BulkVoronoiMirror"
             float _Pulse;
             float _MirrorStrength;
             float _Distortion;
+            UNITY_DECLARE_TEXCUBE(unity_SpecCube0);
+            float4 unity_SpecCube0_HDR;
 
             struct appdata
             {
@@ -102,8 +104,12 @@ Shader "CosmicShore/BulkVoronoiMirror"
                 float facet = sin(dot(floor(i.uv * 7.0), float2(13.7, 41.3)) + _Time.y * 0.6);
                 float reflection = sin((i.worldPos.x + i.worldPos.z) * 0.028 + facet * _Distortion + _Time.y * 0.85) * 0.5 + 0.5;
                 float edge = 1.0 - smoothstep(0.025, 0.105, voronoiEdge(i.uv * 2.8));
+                float3 reflectDir = reflect(-viewDir, normalize(n + float3(sin(facet) * 0.08, cos(facet) * 0.04, sin(facet * 1.7) * 0.08)));
+                half4 encodedReflection = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir);
+                float3 probeReflection = DecodeHDR(encodedReflection, unity_SpecCube0_HDR);
 
                 float3 mirror = lerp(_BaseColor.rgb * 0.36, float3(0.85, 0.96, 1.0), reflection * _MirrorStrength);
+                mirror = lerp(mirror, probeReflection * (0.42 + fresnel * 0.85) + _BaseColor.rgb * 0.18, _MirrorStrength * 0.48);
                 mirror += fresnel * float3(0.18, 0.55, 1.0);
                 mirror = lerp(mirror, _LineColor.rgb * (1.0 + _Pulse * 0.55), edge);
 
diff --git a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs
index 50fed3d71..219218d59 100644
--- a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs
+++ b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs
@@ -1,5 +1,6 @@
 using CosmicShore.Utility;
 using UnityEngine;
+using UnityEngine.Rendering;
 
 namespace CosmicShore.Gameplay
 {
@@ -18,6 +19,7 @@ namespace CosmicShore.Gameplay
             _mirrorWallMaterial = MakeMirrorWallMaterial();
             _gateMaterial = MakeMaterial("Bulk Pulse Gate", new Color(0.25f, 0.82f, 1f, 0.9f));
             _shardMaterial = MakeMaterial("Bulk Speed Shards", new Color(1f, 0.56f, 1f, 0.92f));
+            _glyphMaterial = MakeGlyphMaterial();
         }
 
         int ResolveTargetTransferCount()
@@ -42,6 +44,22 @@ namespace CosmicShore.Gameplay
             return material;
         }
 
+        Material MakeGlyphMaterial()
+        {
+            Shader shader = Shader.Find("CosmicShore/BulkGlyphSprite")
+                            ?? Shader.Find("Sprites/Default")
+                            ?? Shader.Find("Universal Render Pipeline/Unlit")
+                            ?? Shader.Find("Unlit/Color");
+            var material = new Material(shader) { name = "Bulk Dark Animated Glyphs" };
+            SetMaterialColor(material, new Color(0.015f, 0.035f, 0.052f, 0.88f));
+            if (material.HasProperty("_AccentColor"))
+                material.SetColor("_AccentColor", new Color(0.04f, 0.95f, 1f, 0.72f));
+            if (material.HasProperty("_DarkColor"))
+                material.SetColor("_DarkColor", new Color(0f, 0.004f, 0.012f, 0.96f));
+            SetMaterialFloat(material, "_Alpha", 0.84f);
+            return material;
+        }
```

</details>

### `aca5dcd53` — Fix Bulk Filaments music and wall art

_Todd VanTongeren, 2026-06-24 22:46:41 -0400_

```text
 Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader           | 24 ++++++----
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs    | 83 +++++++++++++++++++++++++++++++--
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs  |  8 ++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  1 +
 Docs/BulkFilaments/DEVLOG.md                                          | 25 ++++++++++
 5 files changed, 123 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 250 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
index 745950c0d..5847dc15d 100644
--- a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
+++ b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
@@ -2,12 +2,12 @@ Shader "CosmicShore/BulkVoronoiMirror"
 {
     Properties
     {
-        _BaseColor ("Mirror Tint", Color) = (0.12, 0.5, 0.95, 0.58)
-        _Color ("Color", Color) = (0.12, 0.5, 0.95, 0.58)
+        _BaseColor ("Mirror Tint", Color) = (0.025, 0.13, 0.24, 0.82)
+        _Color ("Color", Color) = (0.025, 0.13, 0.24, 0.82)
         _LineColor ("Cell Line Color", Color) = (0.03, 0.9, 1, 1)
-        _Alpha ("Alpha", Range(0, 1)) = 0.58
+        _Alpha ("Alpha", Range(0, 1)) = 0.82
         _Pulse ("Pulse", Range(0, 4)) = 0
-        _MirrorStrength ("Mirror Strength", Range(0, 1)) = 0.9
+        _MirrorStrength ("Mirror Strength", Range(0, 1)) = 0.48
         _Distortion ("Facet Distortion", Range(0, 2)) = 0.55
     }
     SubShader
@@ -103,19 +103,23 @@ Shader "CosmicShore/BulkVoronoiMirror"
                 float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 2.4);
                 float facet = sin(dot(floor(i.uv * 7.0), float2(13.7, 41.3)) + _Time.y * 0.6);
                 float reflection = sin((i.worldPos.x + i.worldPos.z) * 0.028 + facet * _Distortion + _Time.y * 0.85) * 0.5 + 0.5;
-                float edge = 1.0 - smoothstep(0.025, 0.105, voronoiEdge(i.uv * 2.8));
+                float cellEdge = voronoiEdge(i.uv * 3.6);
+                float edge = 1.0 - smoothstep(0.055, 0.18, cellEdge);
+                float circuit = smoothstep(0.035, 0.0, abs(frac(i.uv.y * 18.0 + sin(i.uv.x * 9.0 + _Time.y * 0.7) * 0.18) - 0.5) - 0.18);
+                float darkFacet = sin(dot(floor(i.uv * 9.0), float2(17.3, 29.7)) + _Time.y * 0.35) * 0.5 + 0.5;
                 float3 reflectDir = reflect(-viewDir, normalize(n + float3(sin(facet) * 0.08, cos(facet) * 0.04, sin(facet * 1.7) * 0.08)));
                 half4 encodedReflection = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir);
                 float3 probeReflection = DecodeHDR(encodedReflection, unity_SpecCube0_HDR);
 
-                float3 mirror = lerp(_BaseColor.rgb * 0.36, float3(0.85, 0.96, 1.0), reflection * _MirrorStrength);
-                mirror = lerp(mirror, probeReflection * (0.42 + fresnel * 0.85) + _BaseColor.rgb * 0.18, _MirrorStrength * 0.48);
-                mirror += fresnel * float3(0.18, 0.55, 1.0);
-                mirror = lerp(mirror, _LineColor.rgb * (1.0 + _Pulse * 0.55), edge);
+                float3 mirror = lerp(_BaseColor.rgb * (0.28 + darkFacet * 0.18), float3(0.36, 0.72, 0.95), reflection * _MirrorStrength * 0.62);
+                mirror = lerp(mirror, probeReflection * (0.22 + fresnel * 0.42) + _BaseColor.rgb * 0.55, _MirrorStrength * 0.24);
+                mirror += fresnel * float3(0.08, 0.35, 0.65);
+                float lineMask = saturate(edge + circuit * 0.5);
+                mirror = lerp(mirror, _LineColor.rgb * (1.15 + _Pulse * 0.65), lineMask);
 
                 fixed4 color;
                 color.rgb = mirror;
-                color.a = saturate(_Alpha * (0.3 + fresnel * 0.5 + edge * 0.7 + _Pulse * 0.08));
+                color.a = saturate(_Alpha * (0.42 + fresnel * 0.28 + lineMask * 0.72 + _Pulse * 0.08));
                 return color;
             }
             ENDCG
diff --git a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
index b83c8b1a0..a059297f6 100644
--- a/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
+++ b/Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs
@@ -22,6 +22,8 @@ namespace CosmicShore.Gameplay
         readonly List<AudioSourceSnapshot> _bulkAudioSnapshots = new();
         bool _audioStartupLogged;
         bool _bulkMixApplied;
+        bool _bulkMusicSettingsSubscribed;
+        float _nextBulkMixEnforceTime;
 
         void StartMusic()
         {
@@ -35,22 +37,22 @@ namespace CosmicShore.Gameplay
             _musicSource.loop = true;
             _musicSource.playOnAwake = false;
             _musicSource.pitch = 1f;
-            _musicSource.volume = 1f;
             _musicSource.spatialBlend = 0f;
             _musicSource.ignoreListenerPause = true;
             _musicSource.mute = false;
             _musicSource.outputAudioMixerGroup = null;
 
             AudioListener.pause = false;
-            AudioListener.volume = 1f;
             EnsureSingleAudioListener();
+            SubscribeBulkMusicSettings();
+            ApplyBulkMusicVolume();
             ApplyBulkAudioMix();
 
             if (_musicSource.clip)
             {
                 if (!_musicSource.isPlaying)
                     _musicSource.Play();
-                CSDebug.Log($"[BulkFilaments] Music playing: {_musicSource.clip.name}, length={_musicSource.clip.length:0.0}s, listenerVolume={AudioListener.volume:0.00}.");
+                CSDebug.Log($"[BulkFilaments] Music playing: {_musicSource.clip.name}, length={_musicSource.clip.length:0.0}s, volume={_musicSource.volume:0.00}.");
             }
             else
             {
@@ -109,10 +111,58 @@ namespace CosmicShore.Gameplay
             }
 
             AudioListener.pause = false;
-            AudioListener.volume = 1f;
             _sfxSource.PlayOneShot(clip, volumeScale);
         }
 
+        void SubscribeBulkMusicSettings()
+        {
+            if (_bulkMusicSettingsSubscribed)
+                return;
+
+            _bulkMusicSettingsSubscribed = true;
+            GameSetting.OnChangeMusicEnabledStatus += OnBulkMusicEnabledChanged;
+            GameSetting.OnChangeMusicLevel += OnBulkMusicLevelChanged;
+        }
+
+        void UnsubscribeBulkMusicSettings()
+        {
+            if (!_bulkMusicSettingsSubscribed)
+                return;
+
+            _bulkMusicSettingsSubscribed = false;
+            GameSetting.OnChangeMusicEnabledStatus -= OnBulkMusicEnabledChanged;
+            GameSetting.OnChangeMusicLevel -= OnBulkMusicLevelChanged;
+        }
+
+        void OnBulkMusicEnabledChanged(bool _) => ApplyBulkMusicVolume();
+
+        void OnBulkMusicLevelChanged(float _) => ApplyBulkMusicVolume();
+
+        void ApplyBulkMusicVolume()
+        {
+            if (!_musicSource)
+                return;
+
+            bool enabled = true;
+            float level = 1f;
+            var setting = GameSetting.Instance;
+            if (setting)
+            {
+                enabled = setting.MusicEnabled;
+                level = setting.MusicLevel;
+            }
+            else
+            {
+                enabled = PlayerPrefs.GetInt(nameof(GameSetting.PlayerPrefKeys.MusicEnabled), 1) == 1;
+                level = PlayerPrefs.GetFloat(nameof(GameSetting.PlayerPrefKeys.MusicLevel), 1f);
+            }
+
+            // Match AudioSystem's legacy music source scaling so the settings
+            // slider has the same loudness curve inside Bulk Filaments.
+            _musicSource.volume = enabled ? Mathf.Clamp01(level) / 5f : 0f;
+            _musicSource.mute = !enabled;
+        }
+
         float BeatPulse()
         {
             float sourceTime = _musicSource && _musicSource.isPlaying ? _musicSource.time : Time.time;
```

</details>

### `2a448836b` — Checkpoint Bulk Filaments visuals and startup recovery

_Todd VanTongeren, 2026-06-27 07:01:11 -0400_

```text
 Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader             |   8 +-
 Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader             |  81 ++++++++
 Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader.meta        |  10 +
 Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader           |  12 +-
 Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md                   |  11 +-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Audio.cs    |  10 +-
 .../Controller/Arcade/BulkFilamentsController.CameraEffects.cs        |  35 ++++
 .../Controller/Arcade/BulkFilamentsController.CameraEffects.cs.meta   |  11 ++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.CameraInput.cs |  19 +-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Fauna.cs    | 208 ++++++++++++++++++++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Fauna.cs.meta  |  11 ++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Generation.cs  |  65 +++----
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Geometry.cs |   1 +
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Lightning.cs   |  21 +-
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Nanites.cs  | 228 ++++++++++++++++++++++
 .../Controller/Arcade/BulkFilamentsController.Nanites.cs.meta         |  11 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Roots.cs    | 114 +++++++++++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.Roots.cs.meta  |  11 ++
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Run.cs      |  22 ++-
 .../Controller/Arcade/BulkFilamentsController.SpriteOverlayTypes.cs   |  67 +++++++
 .../Arcade/BulkFilamentsController.SpriteOverlayTypes.cs.meta         |  11 ++
 .../Controller/Arcade/BulkFilamentsController.SpriteOverlays.cs       | 334 ++++++++++++++++++++++++++++++++
 .../Controller/Arcade/BulkFilamentsController.SpriteOverlays.cs.meta  |  11 ++
 .../_Scripts/Controller/Arcade/BulkFilamentsController.StartFlow.cs   | 110 +++++++++--
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.Visuals.cs  | 115 +----------
 Assets/_Scripts/Controller/Arcade/BulkFilamentsController.cs          |  27 ++-
 Assets/_Scripts/Controller/Player/MiniGamePlayerSpawnerAdapter.cs     |  31 ++-
 Docs/BulkFilaments/DEVLOG.md                                          | 157 +++++++++++++++
 Docs/BulkFilaments/PRD.md                                             |  15 +-
 45 files changed, 2252 insertions(+), 217 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2283 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader b/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader
index 645d38a2e..c6a0dde5d 100644
--- a/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader
+++ b/Assets/_Graphics/Materials/Shaders/BulkEnergyUnlit.shader
@@ -11,7 +11,7 @@ Shader "CosmicShore/BulkEnergyUnlit"
     SubShader
     {
         Tags { "Queue"="Transparent" "RenderType"="Transparent" }
-        Blend SrcAlpha One
+        Blend SrcAlpha OneMinusSrcAlpha
         ZWrite Off
         Cull Off
 
@@ -54,10 +54,10 @@ Shader "CosmicShore/BulkEnergyUnlit"
             fixed4 frag(v2f i) : SV_Target
             {
                 float stripe = sin((i.uv.x * 31.0 + _Time.y * 5.5) * 6.2831853) * 0.5 + 0.5;
-                float glow = 1.0 + _Pulse * 0.55 + stripe * 0.22;
-                fixed4 color = lerp(_BaseColor, _EmissionColor, saturate(0.42 + _Pulse * 0.18));
+                float glow = 0.82 + _Pulse * 0.28 + stripe * 0.12;
+                fixed4 color = lerp(_BaseColor, _EmissionColor, saturate(0.28 + _Pulse * 0.12));
                 color.rgb *= glow;
-                color.a = saturate(_Alpha * _BaseColor.a * (0.72 + _Pulse * 0.16 + stripe * 0.12));
+                color.a = saturate(_Alpha * _BaseColor.a * (0.62 + _Pulse * 0.08 + stripe * 0.08));
                 return color * i.color;
             }
             ENDCG
diff --git a/Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader b/Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader
new file mode 100644
index 000000000..832e60658
--- /dev/null
+++ b/Assets/_Graphics/Materials/Shaders/BulkSpriteSheet.shader
@@ -0,0 +1,81 @@
+Shader "CosmicShore/BulkSpriteSheet"
+{
+    Properties
+    {
+        _MainTex ("Sprite Sheet", 2D) = "white" {}
+        _TintColor ("Tint", Color) = (1, 1, 1, 1)
+        _Frame ("Frame", Float) = 0
+        _Columns ("Columns", Float) = 4
+        _Rows ("Rows", Float) = 3
+        _Alpha ("Alpha", Range(0, 1)) = 1
+        _Glow ("Glow", Range(0, 4)) = 1
+        _BlackToAlpha ("Black To Alpha", Range(0, 1)) = 0
+    }
+    SubShader
+    {
+        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
+        Blend SrcAlpha OneMinusSrcAlpha
+        ZWrite Off
+        Cull Off
+
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #include "UnityCG.cginc"
+
+            sampler2D _MainTex;
+            fixed4 _TintColor;
+            float _Frame;
+            float _Columns;
+            float _Rows;
+            float _Alpha;
+            float _Glow;
+            float _BlackToAlpha;
+
+            struct appdata
+            {
+                float4 vertex : POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            struct v2f
+            {
+                float4 vertex : SV_POSITION;
+                float2 uv : TEXCOORD0;
+                fixed4 color : COLOR;
+            };
+
+            v2f vert(appdata v)
+            {
+                v2f o;
+                o.vertex = UnityObjectToClipPos(v.vertex);
+                float columns = max(1.0, _Columns);
+                float rows = max(1.0, _Rows);
+                float frameCount = columns * rows;
+                float frame = floor(fmod(max(0.0, _Frame), frameCount));
+                float column = fmod(frame, columns);
+                float rowFromTop = floor(frame / columns);
+                float rowFromBottom = rows - 1.0 - rowFromTop;
+                o.uv = float2((v.uv.x + column) / columns, (v.uv.y + rowFromBottom) / rows);
+                o.color = v.color;
+                return o;
+            }
+
+            fixed4 frag(v2f i) : SV_Target
+            {
+                fixed4 sample = tex2D(_MainTex, i.uv);
+                float glowAlpha = saturate(max(sample.r, max(sample.g, sample.b)) * 1.35);
+                sample.a = lerp(sample.a, glowAlpha, _BlackToAlpha);
+                fixed4 color = sample * _TintColor * i.color;
+                color.rgb *= _Glow;
+                color.a = sample.a * _TintColor.a * _Alpha * i.color.a;
+                return color;
+            }
+            ENDCG
+        }
+    }
+    FallBack "Sprites/Default"
+}
diff --git a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
index 5847dc15d..377737a79 100644
--- a/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
+++ b/Assets/_Graphics/Materials/Shaders/BulkVoronoiMirror.shader
@@ -31,8 +31,6 @@ Shader "CosmicShore/BulkVoronoiMirror"
             float _Pulse;
             float _MirrorStrength;
             float _Distortion;
-            UNITY_DECLARE_TEXCUBE(unity_SpecCube0);
-            float4 unity_SpecCube0_HDR;
 
             struct appdata
             {
@@ -111,15 +109,15 @@ Shader "CosmicShore/BulkVoronoiMirror"
                 half4 encodedReflection = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir);
                 float3 probeReflection = DecodeHDR(encodedReflection, unity_SpecCube0_HDR);
 
-                float3 mirror = lerp(_BaseColor.rgb * (0.28 + darkFacet * 0.18), float3(0.36, 0.72, 0.95), reflection * _MirrorStrength * 0.62);
-                mirror = lerp(mirror, probeReflection * (0.22 + fresnel * 0.42) + _BaseColor.rgb * 0.55, _MirrorStrength * 0.24);
-                mirror += fresnel * float3(0.08, 0.35, 0.65);
+                float3 mirror = lerp(_BaseColor.rgb * (0.2 + darkFacet * 0.16), float3(0.18, 0.26, 0.46), reflection * _MirrorStrength * 0.46);
+                mirror = lerp(mirror, probeReflection * (0.14 + fresnel * 0.24) + _BaseColor.rgb * 0.5, _MirrorStrength * 0.18);
+                mirror += fresnel * float3(0.05, 0.08, 0.24);
                 float lineMask = saturate(edge + circuit * 0.5);
-                mirror = lerp(mirror, _LineColor.rgb * (1.15 + _Pulse * 0.65), lineMask);
+                mirror = lerp(mirror, _LineColor.rgb * (0.72 + _Pulse * 0.28), lineMask * 0.68);
 
                 fixed4 color;
                 color.rgb = mirror;
-                color.a = saturate(_Alpha * (0.42 + fresnel * 0.28 + lineMask * 0.72 + _Pulse * 0.08));
+                color.a = saturate(_Alpha * (0.32 + fresnel * 0.18 + lineMask * 0.48 + _Pulse * 0.04));
                 return color;
             }
             ENDCG
diff --git a/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md b/Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md
index eb76fd98c..b53ac293d 100644
```

</details>
