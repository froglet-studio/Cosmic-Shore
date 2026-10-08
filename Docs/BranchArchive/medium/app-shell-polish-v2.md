# Branch archive: `app-shell-polish-v2`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-20 by dbrutus
- **Unmerged commits:** 5
- **Forked from:** `afeb359b1` (2026-04-16, Merge pull request #482 from froglet-studio/claude/add-octahedron-shield-YCmDg)
- **Tip:** `1112ccec0`
- **Files touched (471):**
  - `Assets/Plugins/FMOD.meta`
  - `Assets/Plugins/FMOD/Cache.meta`
  - `Assets/Plugins/FMOD/Cache/Editor.meta`
  - `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`
  - `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset.meta`
  - `Assets/Plugins/FMOD/FMODUnity.asmdef`
  - `Assets/Plugins/FMOD/FMODUnity.asmdef.meta`
  - `Assets/Plugins/FMOD/LICENSE.txt`
  - `Assets/Plugins/FMOD/LICENSE.txt.meta`
  - `Assets/Plugins/FMOD/README.txt`
  - `Assets/Plugins/FMOD/README.txt.meta`
  - `Assets/Plugins/FMOD/Resources.meta`
  - `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`
  - `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset.meta`
  - `Assets/Plugins/FMOD/addons.meta`
  - `Assets/Plugins/FMOD/addons/Haptics.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODHaptics.cs`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODHaptics.cs.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODUnityHaptics.asmdef`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODUnityHaptics.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FMODUnityResonanceEditor.asmdef`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FMODUnityResonanceEditor.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FmodResonanceAudioRoomEditor.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FmodResonanceAudioRoomEditor.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/Localization.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/Localization.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/zh_hans.po`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/zh_hans.po.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FMODUnityResonance.asmdef`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FMODUnityResonance.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudio.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudio.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudioRoom.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudioRoom.cs.meta`
  - `Assets/Plugins/FMOD/images.meta`
  - `Assets/Plugins/FMOD/images/AddIcon.png`
  - … and 431 more

### `605011e64` — FMOD engine sounds added to squirell

_aradia1, 2026-04-27 14:09:25 -0400_

```text
new script made to add engine sounds that dynamically react to player movement
```

```text
 Assets/Plugins/FMOD/src/RuntimeManager.cs.meta                        |   19 +
 Assets/Plugins/FMOD/src/RuntimeUtils.cs                               |  645 +++++
 Assets/Plugins/FMOD/src/RuntimeUtils.cs.meta                          |   19 +
 Assets/Plugins/FMOD/src/Settings.cs                                   | 1027 ++++++++
 Assets/Plugins/FMOD/src/Settings.cs.meta                              |   19 +
 Assets/Plugins/FMOD/src/StudioBankLoader.cs                           |  122 +
 Assets/Plugins/FMOD/src/StudioBankLoader.cs.meta                      |   19 +
 Assets/Plugins/FMOD/src/StudioEventEmitter.cs                         |  415 ++++
 Assets/Plugins/FMOD/src/StudioEventEmitter.cs.meta                    |   19 +
 Assets/Plugins/FMOD/src/StudioGlobalParameterTrigger.cs               |   56 +
 Assets/Plugins/FMOD/src/StudioGlobalParameterTrigger.cs.meta          |   18 +
 Assets/Plugins/FMOD/src/StudioListener.cs                             |  181 ++
 Assets/Plugins/FMOD/src/StudioListener.cs.meta                        |   19 +
 Assets/Plugins/FMOD/src/StudioParameterTrigger.cs                     |   63 +
 Assets/Plugins/FMOD/src/StudioParameterTrigger.cs.meta                |   19 +
 Assets/Plugins/FMOD/src/fmod.cs                                       | 4090 +++++++++++++++++++++++++++++++
 Assets/Plugins/FMOD/src/fmod.cs.meta                                  |   17 +
 Assets/Plugins/FMOD/src/fmod_dsp.cs                                   | 1093 +++++++++
 Assets/Plugins/FMOD/src/fmod_dsp.cs.meta                              |   17 +
 Assets/Plugins/FMOD/src/fmod_errors.cs                                |  106 +
 Assets/Plugins/FMOD/src/fmod_errors.cs.meta                           |   17 +
 Assets/Plugins/FMOD/src/fmod_studio.cs                                | 2260 +++++++++++++++++
 Assets/Plugins/FMOD/src/fmod_studio.cs.meta                           |   17 +
 Assets/_Prefabs/Spacevessels/Squirrel.prefab                          |   58 +-
 Assets/_Scripts/Controller/Vessel/.DS_Store                           |  Bin 0 -> 6148 bytes
 Assets/_Scripts/Controller/Vessel/Audio.meta                          |    8 +
 Assets/_Scripts/System/Audio/ShipAudioController.cs                   | 1205 +++++++++
 Assets/_Scripts/System/Audio/ShipAudioController.cs.meta              |    2 +
 fmod_editor.log                                                       |  374 +++
 459 files changed, 48522 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 36106 lines)</summary>

```diff
diff --git a/Assets/Plugins/FMOD/FMODUnity.asmdef b/Assets/Plugins/FMOD/FMODUnity.asmdef
new file mode 100644
index 000000000..a9fdfc32a
--- /dev/null
+++ b/Assets/Plugins/FMOD/FMODUnity.asmdef
@@ -0,0 +1,51 @@
+{
+    "name": "FMODUnity",
+    "references": [
+        "Unity.Timeline",
+        "Unity.Addressables",
+        "Unity.ResourceManager",
+        "Unity.RenderPipelines.Universal.Runtime"
+    ],
+    "includePlatforms": [],
+    "excludePlatforms": [],
+    "allowUnsafeCode": true,
+    "overrideReferences": false,
+    "precompiledReferences": [],
+    "autoReferenced": true,
+    "defineConstraints": [
+        "UNITY_2021_3_OR_NEWER"
+    ],
+    "versionDefines": [
+        {
+            "name": "com.unity.timeline",
+            "expression": "1.0.0",
+            "define": "UNITY_TIMELINE_EXIST"
+        },
+        {
+            "name": "com.unity.addressables",
+            "expression": "1.0.0",
+            "define": "UNITY_ADDRESSABLES_EXIST"
+        },
+        {
+            "name": "com.unity.modules.physics",
+            "expression": "1.0.0",
+            "define": "UNITY_PHYSICS_EXIST"
+        },
+        {
+            "name": "com.unity.modules.physics2d",
+            "expression": "1.0.0",
+            "define": "UNITY_PHYSICS2D_EXIST"
+        },
+        {
+            "name": "com.unity.urp",
+            "expression": "1.0.0",
+            "define": "UNITY_URP_EXIST"
+        },
+        {
+            "name": "com.unity.ugui",
+            "expression": "1.0.0",
+            "define": "UNITY_UI_EXIST"
+        }
+    ],
+    "noEngineReferences": false
+}
\ No newline at end of file
diff --git a/Assets/Plugins/FMOD/LICENSE.txt b/Assets/Plugins/FMOD/LICENSE.txt
new file mode 100644
index 000000000..2ce86fa43
--- /dev/null
+++ b/Assets/Plugins/FMOD/LICENSE.txt
@@ -0,0 +1,1053 @@
+                    FMOD END USER LICENCE AGREEMENT
+                    ===============================
+
+This End User Licence Agreement (EULA) is a legal agreement between you and 
+Firelight Technologies Pty Ltd (ACN 099 182 448) (us or we) and governs your
+use of FMOD Studio and FMOD Engine, together the Software.
+
+1. GRANT OF LICENCE
+
+1.1 FMOD Studio
+
+This EULA grants you the right to use FMOD Studio, being the desktop 
+application for adaptive audio content creation, for all use, including
+Commercial use, subject to the following:
+
+    i.  FMOD Studio is used to create content for use with the FMOD Engine 
+        only;
+    ii. FMOD Studio is not redistributed in any form.
+
+1.2 FMOD Engine
+
+This EULA grants you the right to use the FMOD Engine, 'FMOD Ex' or 'FMOD 3' 
+being the run-time engines for adaptive audio playback, without payment, for
+personal (hobbyist), educational (students and teachers) or Non-Commercial use,
+subject to the following:
+
+    i.   FMOD Engine is integrated and redistributed in a software application
+         (Product) only;
+    ii.  FMOD Engine is not distributed as part of a game engine or tool set;
+    iii. FMOD Engine is not used in any Commercial enterprise or for any
+         Commercial production or subcontracting, except for the purposes of
+         Evaluation or Development of a Commercial Product;
+    iv.  Non-Commercial use does not involve any form of monetisation,
+         sponsorship or promotion;
+    v.   Product includes attribution in accordance with Clause 3.
+
+This EULA grants you the right to use FMOD Engine, for limited Commercial
+use, subject to the following:
+
+    i.   Development budget of the project is less than $600k USD (Refer to
+         https://www.fmod.com/licensing#licensing-faq) for information);
+    ii.  Total gross revenue / funding per year for the developer, before
+         expenses, is less than $200k USD (Refer to 
+         https://www.fmod.com/licensing#licensing-faq for information);
+    iii. FMOD Engine is integrated and redistributed in a game application
+         (Product) only;
+    iv.  FMOD Engine is not distributed as part of a game engine or tool set;
+    v.   FMOD Engine is not distributed as part of a Product with the intent
+         to be commercially exploited by another business or institution;
+    vi.  Project is registered in your profile page at 
+         https://www.fmod.com/profile#projects;
+    vii. Product includes attribution in accordance with Clause 3.
+
+This EULA does not grant you the right to use 'FMOD Ex' or 'FMOD 3', for
+Commercial use. A custom commercial license must be acquired from Firelight 
+Technologies by contacting sales@fmod.com.
+
+1.3 FMOD SDK
+
+This EULA grants you the right to use the FMOD SDK, interfacing either 'FMOD
+Engine', 'FMOD Ex' or 'FMOD 3', comprising the programming interface 
+specification, documentation and examples subject to the following:
+
+    i.   FMOD SDK is used to develop a software application using the FMOD
+         Engine;
+    ii.  FMOD SDK is used to develop an external DSP, Output or Codec plugin;
+    iii. FMOD SDK files are not be distributed with the exception of the FMOD 
+         Engine run-time libraries (.DLL, .SO for example).
+
+1.4 FMOD example code and media
+
+This EULA grants you the right to use FMOD example code, being all or snippets 
+of source code files located in the FMOD API examples folder and scripting 
+examples in the documentation, for all use, including Commercial use, subject
+to the following:
+
+    i.   Media files included in FMOD examples, including wav, ogg, mp3, fsb
+         and bank files, are not to be redistributed.
+
+2.OTHER USE
+
+For all Commercial use, and any Non Commercial use not permitted by this
+license, a separate license is required. Refer to www.fmod.com/licensing for
+information.
+
+3. CREDITS
```

</details>

### `c890737f8` — Element sound system added

_aradia1, 2026-04-27 15:10:30 -0400_

```text
All 4 elements have unique sound sigs which react via the audio system.
```

```text
 Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset                |  46 ++---
 Assets/_Scripts/Controller/Vessel/Audio/.DS_Store                     | Bin 0 -> 6148 bytes
 .../{System => Controller/Vessel}/Audio/ShipAudioController.cs        |  52 +++--
 .../{System => Controller/Vessel}/Audio/ShipAudioController.cs.meta   |   0
 fmod_editor.log                                                       | 349 +-------------------------------
 5 files changed, 63 insertions(+), 384 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1235 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs b/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
new file mode 100644
index 000000000..85b105e07
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
@@ -0,0 +1,1229 @@
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Gameplay;
+using FMOD.Studio;
+using FMODUnity;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay.Audio
+{
+    /// <summary>
+    /// Drives the FMOD "space ship engine main" loop for a single vessel.
+    ///
+    /// Usage:
+    ///   - Attach to the vessel root prefab (same GameObject as the Vessel /
+    ///     IVesselStatus components).
+    ///   - Assign <see cref="engineEvent"/> to "event:/space ship engine main"
+    ///     in the inspector.
+    ///
+    /// Velocity source:
+    ///   Cosmic Shore vessels are moved via transform.position, not a
+    ///   Rigidbody. This component measures world-space velocity each frame
+    ///   as (position - lastPosition) / deltaTime, smooths it, normalises it
+    ///   against <see cref="maxShipVelocity"/>, and pushes the result into
+    ///   the event's "Speed" parameter.
+    ///
+    /// Spatialisation:
+    ///   - By default (<see cref="onlyAudibleToController"/> = true) the
+    ///     engine audio is ONLY created for the ship the current client is
+    ///     controlling. Remote ships and AI ships never instantiate the
+    ///     FMOD event on this client, so other players' ships are silent
+    ///     here. This matches the intent "ship audio is only heard by the
+    ///     person controlling the ship".
+    ///   - If <see cref="onlyAudibleToController"/> is disabled, the
+    ///     legacy behaviour applies: remote ships attach to their own
+    ///     transform (normal 3D attenuation), and the LOCAL player's ship
+    ///     attaches to the FMOD StudioListener instead, so the instance's
+    ///     3D position is always the listener's position. That makes it
+    ///     effectively 2D -> always audible, even when the camera pulls
+    ///     away from the ship.
+    ///   - If the local-vs-remote check fails (e.g. Player not set), the
+    ///     instance falls back to attaching to the ship transform.
+    ///
+    /// Ownership is not guaranteed to be known at Start() (VesselController
+    /// assigns the Player after Initialize()), so instance creation is
+    /// deferred until we can resolve local-vs-remote. See
+    /// <see cref="TryEvaluateAndCreate"/>.
+    /// </summary>
+    [DisallowMultipleComponent]
+    public class ShipAudioController : MonoBehaviour
+    {
+        enum AttachMode
+        {
+            /// <summary>No routing decision yet (instance freshly created).</summary>
+            None,
+            /// <summary>Attached to this ship's transform (3D spatialisation).</summary>
+            Ship,
+            /// <summary>Attached to the listener's transform (effectively 2D / always audible).</summary>
+            Listener
+        }
+
+        [Header("FMOD Event")]
+        [SerializeField, Tooltip("FMOD event for the looping ship engine.")]
+        EventReference engineEvent;
+
+        [SerializeField, Tooltip(
+            "Extra FMOD events to play in parallel with the main engine event " +
+            "(Option C: driving nested child events directly from code). " +
+            "Use this when the parent engine event contains nested/referenced " +
+            "child events that don't receive the parent's local parameter " +
+            "values. List each child event here and this controller will " +
+            "create, attach, start, and parameter-drive them alongside the " +
+            "parent. IMPORTANT: if you list a child event here, remove or " +
+            "disable its nested event instrument inside the parent event in " +
+            "FMOD Studio, otherwise you'll hear double playback.")]
+        EventReference[] additionalEngineLayers;
+
+        [SerializeField, Tooltip("FMOD parameter name driven by ship velocity.")]
+        string speedParameterName = "Speed";
+
+        [SerializeField, Tooltip(
+            "FMOD parameter name driven by ship pitch + yaw rate " +
+            "(bipolar: negative = one direction, positive = the other).")]
+        string tiltParameterName = "Tilt Acel";
+
+        [Header("Speed Mapping")]
+        [SerializeField, Tooltip(
+            "Ship velocity magnitude (world units / second) that maps to " +
+            "paramAtMaxVelocity. Velocities above this are clamped.")]
+        float maxShipVelocity = 100f;
+
+        [SerializeField, Tooltip(
+            "FMOD parameter value when ship velocity is 0. Set this to the " +
+            "baseline you want the engine to sit at during normal play.")]
+        float paramAtZeroVelocity = 40f;
+
+        [SerializeField, Tooltip(
+            "FMOD parameter value when ship velocity hits maxShipVelocity.")]
+        float paramAtMaxVelocity = 100f;
+
+        [SerializeField, Range(0f, 30f), Tooltip(
+            "Exponential smoothing rate for measured velocity. " +
+            "0 = no smoothing (instant, jittery). ~5-10 = responsive but stable.")]
+        float velocitySmoothing = 6f;
+
+        [Header("Tilt (pitch + yaw rate)")]
+        [SerializeField, Tooltip(
+            "Angular rate (degrees/second) that maps to paramAtMaxTilt. " +
+            "Rates above this are clamped. Typical ships tilt at 90-300 deg/s.")]
+        float maxTiltRate = 180f;
+
+        [SerializeField, Tooltip(
+            "FMOD param value when pitch+yaw rate is at the maximum (positive).")]
+        float paramAtMaxTilt = 100f;
+
+        [SerializeField, Tooltip(
+            "FMOD param value when pitch+yaw rate is at the maximum in the " +
+            "opposite direction (negative). For a symmetric bipolar FMOD " +
+            "parameter this should equal -paramAtMaxTilt.")]
+        float paramAtMinTilt = -100f;
+
+        [SerializeField, Tooltip(
+            "FMOD param value when ship is not tilting (zero angular rate).")]
+        float paramAtZeroTilt = 0f;
+
+        [SerializeField, Range(-2f, 2f), Tooltip(
+            "How much pitch rate (local X-axis rotation) contributes to the " +
+            "signed tilt signal. Set negative to invert sign.")]
+        float pitchWeight = 1f;
+
+        [SerializeField, Range(-2f, 2f), Tooltip(
+            "How much yaw rate (local Y-axis rotation) contributes to the " +
+            "signed tilt signal. Set negative to invert sign.")]
+        float yawWeight = 1f;
+
+        [SerializeField, Range(0f, 30f), Tooltip(
+            "Exponential smoothing rate for the signed tilt value. " +
+            "Higher = snappier, lower = smoother.")]
+        float tiltSmoothing = 8f;
+
+        [Header("Drift")]
+        [SerializeField, Range(0f, 1f), Tooltip(
+            "Multiplier applied to the FMOD param value while " +
+            "VesselStatus.IsDrifting is true (drift trigger held). " +
+            "1 = no effect, 0 = fully silenced.")]
```

</details>

### `f24938021` — Added infrastructure for skim sfx

_aradia1, 2026-04-27 16:39:28 -0400_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 ++------------------------------
 Assets/_Audio/Sounds/SFX/skim cosmic shore.wav                        | Bin 0 -> 432080 bytes
 Assets/_Audio/Sounds/SFX/skim cosmic shore.wav.meta                   |  23 +++
 Assets/_Graphics/Materials/CrystalMaterials/TimeCrystalMaterial.mat   |   2 +
 Assets/_Graphics/Materials/ShieldedCrystalMaterial.mat                |   3 +
 Assets/_Scenes/Bootstrap.unity                                        |   7 +-
 Assets/_Scripts/System/Audio/AudioSystem.cs                           |   3 +
 7 files changed, 47 insertions(+), 280 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/Audio/AudioSystem.cs b/Assets/_Scripts/System/Audio/AudioSystem.cs
index 17c27a5da..d369b185e 100644
--- a/Assets/_Scripts/System/Audio/AudioSystem.cs
+++ b/Assets/_Scripts/System/Audio/AudioSystem.cs
@@ -51,6 +51,7 @@ namespace CosmicShore.Core
         DriftEnd = 17,
         EnergyGain = 18,
         SpeedBurst = 19,
+        Skim = 20,
     }
 
     [DefaultExecutionOrder(-1)]
@@ -102,6 +103,7 @@ namespace CosmicShore.Core
         [SerializeField] AudioClip DriftEndAudioClip;
         [SerializeField] AudioClip EnergyGainAudioClip;
         [SerializeField] AudioClip SpeedBurstAudioClip;
+        [SerializeField] AudioClip SkimAudioClip;
 
         public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
         public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }
@@ -363,6 +365,7 @@ namespace CosmicShore.Core
                 {GameplaySFXCategory.DriftEnd, DriftEndAudioClip},
                 {GameplaySFXCategory.EnergyGain, EnergyGainAudioClip},
                 {GameplaySFXCategory.SpeedBurst, SpeedBurstAudioClip},
+                {GameplaySFXCategory.Skim, SkimAudioClip},
             };
         }
     }
```

</details>

### `b9ea39734` — analog drift sounds added

_aradia1, 2026-04-28 14:16:51 -0400_

```text
 Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset     | 154 +++++++++++-
 Assets/_Prefabs/Spacevessels/Squirrel.prefab               |  44 ++++
 Assets/_Scenes/Bootstrap.unity                             |  10 +
 Assets/_Scripts/Controller/FX/DriftAudioController.cs      | 581 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/FX/DriftAudioController.cs.meta |   2 +
 fmod_editor.log                                            |   2 +-
 6 files changed, 785 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 587 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/FX/DriftAudioController.cs b/Assets/_Scripts/Controller/FX/DriftAudioController.cs
new file mode 100644
index 000000000..c449489bb
--- /dev/null
+++ b/Assets/_Scripts/Controller/FX/DriftAudioController.cs
@@ -0,0 +1,581 @@
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
+using FMOD.Studio;
+using FMODUnity;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay.Audio
+{
+    /// <summary>
+    /// Drives an FMOD drift SFX event for a single vessel (Squirrel by
+    /// default — racing/drift class). The event exposes one parameter
+    /// ("Drift Amount" by default):
+    ///
+    ///   0 = single drift trigger held
+    ///   1 = both drift triggers held
+    ///
+    /// Lifecycle:
+    ///   - Drift START (IsDrifting goes true): create + start the FMOD
+    ///     event instance, push the current trigger-driven amount.
+    ///   - Drift HOLD: push the smoothed amount each frame (0 single, 1
+    ///     double). The two trigger sources come from
+    ///     IInputStatus.LeftTriggerAnalog / RightTriggerAnalog above
+    ///     <see cref="triggerDeadzone"/>.
+    ///   - Drift END (IsDrifting goes false): fire the optional one-shot
+    ///     <see cref="releaseEvent"/> ("trigger off" SFX) and, if
+    ///     <see cref="driveParamToOneOnRelease"/> is true, also drive the
+    ///     parameter to 1 to play any 'trigger let go' stage inside the
+    ///     main drift event. The main drift instance is held alive for
+    ///     <see cref="releaseHoldSeconds"/> so any in-event tail can
+    ///     finish, then stopped + released. Internal state resets so the
+    ///     next drift starts the event fresh from 0.
+    ///
+    /// Attach this to the same GameObject as the vessel root (the one
+    /// that owns IVesselStatus and the InputController). Recommended for
+    /// the Squirrel vessel prefab.
+    /// </summary>
+    [DisallowMultipleComponent]
+    public class DriftAudioController : MonoBehaviour
+    {
+        enum AttachMode { None, Ship, Listener }
+
+        enum DriftPhase
+        {
+            /// <summary>No event playing, waiting for IsDrifting to go true.</summary>
+            Idle,
+            /// <summary>Event playing, drift_amount tracks live trigger state.</summary>
+            Active,
+            /// <summary>Drift just ended — ramping drift_amount to 1 ("let go"), event still playing.</summary>
+            Releasing
+        }
+
+        [Header("FMOD Event")]
+        [SerializeField, Tooltip(
+            "FMOD drift event. Should be the looping drift SFX (the one " +
+            "wired up under SFX/drifts in the FMOD project). Plays only " +
+            "while the vessel is drifting; stopped on drift end after the " +
+            "release tail.")]
+        EventReference driftEvent;
+
+        [SerializeField, Tooltip(
+            "FMOD parameter name on the drift event that takes 0..1: " +
+            "0 = single drift trigger held, 1 = both triggers held. On " +
+            "release this parameter is optionally driven to 1 to play any " +
+            "'trigger let go' stage inside the main drift event (see " +
+            "driveParamToOneOnRelease).")]
+        string driftAmountParameterName = "Drift Amount";
+
+        [SerializeField, Tooltip(
+            "One-shot FMOD event fired the moment drift ends (when both " +
+            "triggers are released). Typically the 'trigger off' release " +
+            "SFX. Optional — leave unassigned if the main drift event " +
+            "handles its own release tail. The one-shot is fired and " +
+            "released independently of the main drift instance, so it " +
+            "always plays to completion even after the drift event has " +
+            "stopped.")]
+        EventReference releaseEvent;
+
+        [SerializeField, Tooltip(
+            "When true, the one-shot release event is attached to the " +
+            "ship transform so it spatialises with the vessel. When false " +
+            "(or no listener attachment is needed), it plays as a one-shot " +
+            "at the ship's current world position. Has no effect if no " +
+            "releaseEvent is assigned.")]
+        bool attachReleaseEventToShip = true;
+
+        [SerializeField, Tooltip(
+            "When true, the main drift event's drift_amount parameter is " +
+            "ramped to 1 during the release phase, in addition to firing " +
+            "the one-shot releaseEvent above. Off by default once a " +
+            "releaseEvent is assigned — the dedicated 'trigger off' event " +
+            "should fully cover the let-go SFX. Turn on if your main " +
+            "drift event also has a trigger-let-go stage gated at " +
+            "drift_amount = 1.")]
+        bool driveParamToOneOnRelease = false;
+
+        [Header("Trigger Mapping")]
+        [SerializeField, Range(0f, 1f), Tooltip(
+            "Analog deadzone for each drift trigger. Trigger reads above " +
+            "this count as 'pressed' for the purpose of single-vs-double " +
+            "drift detection. Matches GamepadInputStrategy's trigger " +
+            "deadzone.")]
+        float triggerDeadzone = 0.05f;
+
+        [SerializeField, Tooltip(
+            "When true, the drift_amount parameter scales smoothly with " +
+            "the analog trigger values: a half-pressed second trigger " +
+            "drags the amount partway to 1, instead of a binary single/" +
+            "double snap. Off by default — the FMOD design treats 0 and " +
+            "1 as discrete states.")]
+        bool useAnalogScaling = false;
+
+        [SerializeField, Range(0f, 30f), Tooltip(
+            "Exponential smoothing rate for the drift_amount parameter " +
+            "during the active phase. 0 = snap, ~8-12 = quick but smooth. " +
+            "Prevents zipper noise when the second trigger is tapped.")]
+        float amountSmoothing = 10f;
+
+        [Header("Release / 'Trigger Let Go'")]
+        [SerializeField, Range(0f, 30f), Tooltip(
+            "Smoothing rate for the parameter ramp-to-1 during the " +
+            "release phase. Higher = the parameter snaps to 1 faster on " +
+            "drift end. The ramp runs in parallel with the release hold " +
+            "timer below.")]
+        float releaseSmoothing = 20f;
+
+        [SerializeField, Range(0f, 5f), Tooltip(
+            "How long (seconds) to keep the FMOD event alive after drift " +
+            "ends, with drift_amount driven to 1. This is the window for " +
+            "the 'trigger let go' tail in the FMOD event to play before " +
+            "the instance is stopped + released. After this, internal " +
+            "state resets so the next drift starts the event fresh.")]
+        float releaseHoldSeconds = 0.4f;
+
+        [Header("Spatialisation")]
+        [SerializeField, Tooltip(
+            "When true, drift audio is only created when this client is " +
+            "the controlling user of the vessel. Remote and AI vessels " +
+            "stay silent on this client. Mirrors ShipAudioController.")]
+        bool onlyAudibleToController = true;
+
+        [SerializeField, Tooltip(
+            "Force the instance to attach to the FMOD listener instead " +
+            "of the ship transform — keeps the drift always audible " +
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
