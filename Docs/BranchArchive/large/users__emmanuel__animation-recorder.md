# Branch archive: `users/emmanuel/animation-recorder`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2025-12-10 by Emmanuel
- **Unmerged commits:** 17
- **Forked from:** `7618897ba` (2025-09-04, Update MinigameFreestyle.unity)
- **Tip:** `1d12e5163`
- **Files touched (339):**
  - `Assets/_Models/Animations.meta`
  - `Assets/_Models/Animations/MantaAnimatorController.controller.meta`
  - `Assets/_Models/Animations/QuadFishSwim.anim.meta`
  - `Assets/_Models/Animations/UI.meta`
  - `Assets/_Models/Animations/UI/FadeOutNavBar.anim.meta`
  - `Assets/_Models/Animations/UI/InitializingScreen.controller.meta`
  - `Assets/_Models/Animations/UI/NavBarAnimator.controller.meta`
  - `Assets/_Models/Animations/UI/ZoomClose.anim.meta`
  - `Assets/_Models/ChargeCrystalExport1_7-11-25.fbx.meta`
  - `Assets/_Models/Crystal.fbx.meta`
  - `Assets/_Models/CrystalTimeAnimController.controller.meta`
  - `Assets/_Models/DenemTriBlock.fbxbak.meta`
  - `Assets/_Models/Fauna.meta`
  - `Assets/_Models/Fauna/Brittlestar_Export.fbx.meta`
  - `Assets/_Models/Fauna/ClawfishTest.fbx.meta`
  - `Assets/_Models/Fauna/MassBrittlestarFauna.prefab.meta`
  - `Assets/_Models/Fauna/MassSharkFauna.prefab.meta`
  - `Assets/_Models/Fauna/SharkSwimAnim.anim.meta`
  - `Assets/_Models/Fauna/Shark_model.controller.meta`
  - `Assets/_Models/Fauna/Shark_model.fbx.meta`
  - `Assets/_Models/Fauna/bonita.fbx.meta`
  - `Assets/_Models/Fauna/mediumfish.fbx.meta`
  - `Assets/_Models/Fauna/worm.fbx.meta`
  - `Assets/_Models/Fauna/wormbody.fbx.meta`
  - `Assets/_Models/Fauna/wormhead.fbx.meta`
  - `Assets/_Models/Flora.meta`
  - `Assets/_Models/MassCrystalExport1_8-21-25.fbx.meta`
  - `Assets/_Models/Materials.meta`
  - `Assets/_Models/Materials/CreatureMaterial.mat.meta`
  - `Assets/_Models/Materials/Octagonal Sphere-Material.003.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign10_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign11_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign12_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign1_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign2_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign3_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign4_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign5_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign6_Material.mat.meta`
  - `Assets/_Models/Materials/WorldSpaceDesign7_Material.mat.meta`
  - … and 299 more

### `3dd1142f4` — Squashed feature branch.

_Emmanuel Eytan, 2024-06-25 17:19:39 -0700_

```text
 Assets/_Scripts/Utility/Recording/RecorderWindow.cs      | 330 +++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Recording/RecorderWindow.cs.meta |  11 ++
 Assets/_Scripts/Utility/Recording/dataHolder.cs          |  61 +++++++++
 Assets/_Scripts/Utility/Recording/dataHolder.cs.meta     |  11 ++
 4 files changed, 413 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 405 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/RecorderWindow.cs b/Assets/_Scripts/Utility/Recording/RecorderWindow.cs
new file mode 100644
index 000000000..ad4562378
--- /dev/null
+++ b/Assets/_Scripts/Utility/Recording/RecorderWindow.cs
@@ -0,0 +1,330 @@
+#if UNITY_EDITOR
+using System.Collections;
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.Playables;
+using UnityEngine.Timeline;
+using UnityEditor;
+using UnityEditor.Animations;
+
+namespace CosmicShore.Utility
+{
+    /// <summary>
+    /// A utility for tracking the transform of specific objects and save them as
+    /// animation clips in a given timeline.
+    /// </summary>
+    [InitializeOnLoadAttribute]
+    public class AnimationRecorder : EditorWindow
+    {
+        /// <summary>
+        /// The name of the serialized property for the track name in the data holder
+        /// object, for use by by Unity's property finder.
+        /// </summary>
+        private readonly string TrackName = "trackName";
+
+        /// <summary>
+        /// The name of the serialized property for the playable director
+        /// in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string Director = "director";
+
+        /// <summary>
+        /// The name of the serialized property for the objects to track
+        /// during the recording, as stored in the data holder object
+        // for use by Unity's property finder.
+        /// </summary>
+        private readonly string ObjectsToTrack = "objectsToTrack";
+
+        /// <summary>
+        /// The name of the serialized property for the delay between recording
+        /// snapshots, as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string RecordingDelay = "recordingDelay";
+
+        /// <summary>
+        /// The name of the serialized property that refrences the timeline asset,
+        /// as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string timelineAsset = "timelineAsset";
+
+        /// <summary>
+        /// The name of the serialized property for the path to the assets used by this
+        /// recorder, as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string assetsPath = "assetsPath";
+
+        /// <summary>
+        /// Whether the callbacks for when the editor changes state is registered.
+        /// It should only be necessary to register it once until the editor is closed.
+        /// </summary>
+        private static bool registeredPlayCallbacks = false;
+
+        /// <summary>
+        /// A refrence to the object that contains the data holder for the present session.
+        /// </summary>
+        private DataHolder holder;
+
+        /// <summary>
+        /// A property that will either return a refrence to a holder, or do its best to instantiate
+        /// one and then return it.
+        /// </summary>
+        /// <value>holder</value>
+        private DataHolder Holder
+        {
+            get
```

</details>

### `4689c6eb9` — Code review revisions.

_Emmanuel Eytan, 2024-07-08 17:42:25 -0700_

```text
 .../Utility/Recording/{dataHolder.cs => AnimationRecorderData.cs}     |  0
 .../Recording/{RecorderWindow.cs => AnimationRecorderWindow.cs}       | 43 ++++++++++++++++++---------------
 2 files changed, 24 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 410 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
new file mode 100644
index 000000000..698cdf5b7
--- /dev/null
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -0,0 +1,61 @@
+#if UNITY_EDITOR
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.Playables;
+using UnityEngine.Timeline;
+
+namespace CosmicShore.Utility
+{
+    /// <summary>
+    /// A simple container for the data that will be used by the Asset Recorder.
+    /// There should be one instance of this components on an object whose name is
+    /// set in the member <see cref="RecorderWindow.ManagerName" />.
+    /// </summary>
+    public class DataHolder : MonoBehaviour
+    {
+        #pragma warning disable 0414
+
+        /// <summary>
+        /// The overall manager for the timeline to record.
+        /// </summary>
+        [SerializeField]
+        private PlayableDirector director;
+
+        /// <summary>
+        /// An asset (on disk) for the contents of the timeline.
+        /// </summary>
+        [SerializeField]
+        private TimelineAsset timelineAsset;
+
+        /// <summary>
+        /// Where all items generated or expcted from this utility should be stored.
+        /// The path is relative to the project and will usually start with "Assets/".
+        /// </summary>
+        [SerializeField]
+        private string assetsPath = "Assets/Recorder";
+
+        /// <summary>
+        /// The game objects that this recorder will track.
+        /// </summary>
+        [SerializeField]
+        private Animator[] objectsToTrack;
+
+        /// <summary>
+        /// The time that the recorder will wait for between each snapshot, in seconds.
+        /// A larger number means a less detailed capture. A smaller number means
+        /// a larger amount of data will be recorder.
+        /// </summary>
+        [SerializeField]
+        private float recordingDelay = 1;
+
+        /// <summary>
+        /// An arbirtrary name to add to the recording.
+        /// Will be applied to each new track. Cannot be empty.
+        /// </summary>
+        [SerializeField]
+        private string trackName;
+
+        #pragma warning restore 0414
+    }
+}
+#endif
\ No newline at end of file
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
new file mode 100644
index 000000000..debe96f22
--- /dev/null
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -0,0 +1,335 @@
+#if UNITY_EDITOR
+using System.Collections;
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.Playables;
+using UnityEngine.Timeline;
```

</details>

### `453ac105b` — Support for lack of container folder, and multiple recordings in one session.

_Emmanuel Eytan, 2024-07-13 01:26:12 -0700_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs.meta   | 11 +++++
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs      | 97 ++++++++++++++++++++++++++++++++-----
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs.meta | 11 +++++
 3 files changed, 108 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 160 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index debe96f22..47effe017 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -6,6 +6,7 @@ using UnityEngine.Playables;
 using UnityEngine.Timeline;
 using UnityEditor;
 using UnityEditor.Animations;
+using System;
 
 namespace CosmicShore.Utility
 {
@@ -90,6 +91,10 @@ namespace CosmicShore.Utility
                 }
                 sceneData = gameObject.AddComponent<DataHolder>();
                 holder = sceneData;
+                if (gameObject.GetComponent<PlayableDirector>() == null)
+                {
+                    gameObject.AddComponent<PlayableDirector>();
+                }
                 return holder;
             }
         }
@@ -122,6 +127,13 @@ namespace CosmicShore.Utility
         /// </summary>
         private float animationStart = 0;
 
+        /// <summary>
+        /// Number for each recording of the same object.
+        /// If object X is recorded 3 times in one session, add 0, 1, and 2 to each of its
+        /// recording's temp files.
+        /// </summary>
+        private int recordingNumber = 0;
+
         /// <summary>
         /// A collection of every recorder, each one associated with a new game object to track.
         /// </summary>
@@ -132,6 +144,19 @@ namespace CosmicShore.Utility
         /// </summary>
         private static readonly int LAYOUT_VERTICAL_GAP = 10;
 
+        /// <summary>
+        /// Format in which to convert the number of each recording assets to a string,
+        /// with the right numbers of leading zeros.
+        /// </summary>
+        private static readonly string numberFormat = "D4";
+
+        /// <summary>
+        /// The salt used to keep each recording session distinct.
+        ///
+        /// <seealso href="https://en.wikipedia.org/wiki/Salt_(cryptography)">Definition of salt on wikipedia</seealso>
+        /// </summary>
+        private string salt;
+
         /// <summary>
         /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
         /// window when activated.
@@ -261,6 +286,10 @@ namespace CosmicShore.Utility
         private void EndRecording()
         {
             string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
+            if (AssetDatabase.GetMainAssetTypeAtPath(currentAssetsPath) == null)
+            {
+                CreateAssetFolder(currentAssetsPath);
+            }
             IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
             while (recordersEnumerator.MoveNext())
             {
@@ -268,8 +297,42 @@ namespace CosmicShore.Utility
                 GameObject currentGameObject = currentRecorder.root;
                 AnimationClip animationClip = new();
                 currentRecorder.SaveToClip(animationClip);
-                AssetDatabase.CreateAsset(animationClip, $"{currentAssetsPath}/{currentGameObject.name}.asset");
+                AssetDatabase.CreateAsset(animationClip, GameObjectAssetPath(currentGameObject, recordingNumber));
+            }
+            recordingNumber++;
+        }
+
+        /// <summary>
+        /// The full path of the current animaiton asset.
```

</details>

### `e6947683a` — Simplified first-time run.

_Emmanuel Eytan, 2024-07-17 21:36:57 -0700_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs |  6 +++---
 Assets/_Scripts/Utility/Recording/DefaultTimeline.playable | 22 ++++++++++++++++++++++
 2 files changed, 25 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 698cdf5b7..192f632f9 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -19,20 +19,20 @@ namespace CosmicShore.Utility
         /// The overall manager for the timeline to record.
         /// </summary>
         [SerializeField]
-        private PlayableDirector director;
+        public PlayableDirector director;
 
         /// <summary>
         /// An asset (on disk) for the contents of the timeline.
         /// </summary>
         [SerializeField]
-        private TimelineAsset timelineAsset;
+        public TimelineAsset timelineAsset;
 
         /// <summary>
         /// Where all items generated or expcted from this utility should be stored.
         /// The path is relative to the project and will usually start with "Assets/".
         /// </summary>
         [SerializeField]
-        private string assetsPath = "Assets/Recorder";
+        public string assetsPath = "Assets/Recorder";
 
         /// <summary>
         /// The game objects that this recorder will track.
```

</details>

### `32416c188` — Simplify first use.

_Emmanuel Eytan, 2024-07-25 14:53:13 -0700_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs | 66 ++++++++++++++++++++++++++++--------------
 1 file changed, 44 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 119 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index 47effe017..552b6c4fd 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -78,23 +78,7 @@ namespace CosmicShore.Utility
                 {
                     return holder;
                 }
-                GameObject gameObject = GameObject.Find(AnimationRecorderName);
-                if (gameObject == null)
-                {
-                    Debug.LogWarning($"There needs to be an object in the scene called \"{AnimationRecorderName}\". One will be added now.");
-                    gameObject = new GameObject(AnimationRecorderName);
-                }
-                DataHolder sceneData = gameObject.GetComponent<DataHolder>();
-                if (sceneData != null)
-                {
-                    return sceneData;
-                }
-                sceneData = gameObject.AddComponent<DataHolder>();
-                holder = sceneData;
-                if (gameObject.GetComponent<PlayableDirector>() == null)
-                {
-                    gameObject.AddComponent<PlayableDirector>();
-                }
+                SetupRecordingSystem();
                 return holder;
             }
         }
@@ -137,7 +121,7 @@ namespace CosmicShore.Utility
         /// <summary>
         /// A collection of every recorder, each one associated with a new game object to track.
         /// </summary>
-        private static List<GameObjectRecorder> Recorders = new();
+        private readonly static List<GameObjectRecorder> Recorders = new();
 
         /// <summary>
         /// The default vertical space between elements in the GUI.
@@ -157,6 +141,8 @@ namespace CosmicShore.Utility
         /// </summary>
         private string salt;
 
+        private readonly string defaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";
+
         /// <summary>
         /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
         /// window when activated.
@@ -164,7 +150,7 @@ namespace CosmicShore.Utility
         [MenuItem("Window/Animation Recorder")]
         public static void ShowWindow()
         {
-            EditorWindow recorder = EditorWindow.GetWindow(typeof(AnimationRecorder), false, "Animation Recorder");
+            EditorWindow recorder = EditorWindow.GetWindow(typeof(AnimationRecorder), false, AnimationRecorderName);
         }
 
         /// <summary>
@@ -186,7 +172,7 @@ namespace CosmicShore.Utility
         /// </summary>
         public void OnGUI()
         {
-            EditorGUILayout.PropertyField(serializedObject.FindProperty(Director), new GUIContent("Animation Manager"));
+            EditorGUILayout.PropertyField(serializedObject.FindProperty(Director), new GUIContent("Playable Director Container"));
             GUI.enabled = (serializedObject.FindProperty(Director).objectReferenceValue != null) && !isRecording;
             GUILayout.BeginVertical();
             EditorGUILayout.PropertyField(serializedObject.FindProperty(ObjectsToTrack), new GUIContent("Objects to track"));
@@ -397,14 +383,50 @@ namespace CosmicShore.Utility
             if (state == PlayModeStateChange.EnteredPlayMode)
             {
                 recordingNumber = 0;
-                int _salt = new System.Random().Next();
-                salt = Convert.ToBase64String(BitConverter.GetBytes(_salt)).TrimEnd('=');
+                salt = GenerateSalt();
             }
             if (state == PlayModeStateChange.ExitingPlayMode && isRecording)
             {
                 EndRecording();
             }
         }
+
+        private string GenerateSalt()
```

</details>

### `3a7afbacc` — Changed the GUI to Will's specs.

_Emmanuel Eytan, 2024-07-26 23:32:00 -0700_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs   |  5 ++---
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs | 24 ++++++++++++++----------
 2 files changed, 16 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 110 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 192f632f9..7ecbe7978 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -49,11 +49,10 @@ namespace CosmicShore.Utility
         private float recordingDelay = 1;
 
         /// <summary>
-        /// An arbirtrary name to add to the recording.
-        /// Will be applied to each new track. Cannot be empty.
+        /// The salt currently used in recording names.
         /// </summary>
         [SerializeField]
-        private string trackName;
+        internal string salt;
 
         #pragma warning restore 0414
     }
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index 552b6c4fd..23bd854e3 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -18,10 +18,10 @@ namespace CosmicShore.Utility
     public class AnimationRecorder : EditorWindow
     {
         /// <summary>
-        /// The name of the serialized property for the track name in the data holder
-        /// object, for use by by Unity's property finder.
+        /// The name of the serialized property for the salt currently in use by
+        /// the utility. Can be changed by this code but not by the user.
         /// </summary>
-        private readonly string TrackName = "trackName";
+        private readonly string SaltField= "salt";
 
         /// <summary>
         /// The name of the serialized property for the playable director
@@ -177,9 +177,10 @@ namespace CosmicShore.Utility
             GUILayout.BeginVertical();
             EditorGUILayout.PropertyField(serializedObject.FindProperty(ObjectsToTrack), new GUIContent("Objects to track"));
             EditorGUILayout.PropertyField(serializedObject.FindProperty(RecordingDelay), new GUIContent("Delay between snapshots"));
-            EditorGUILayout.PropertyField(serializedObject.FindProperty(TrackName), new GUIContent("Name of recording"));
             EditorGUILayout.PropertyField(serializedObject.FindProperty(timelineAsset), new GUIContent("Asset for this timeline"));
             EditorGUILayout.PropertyField(serializedObject.FindProperty(assetsPath), new GUIContent("Save path for recordings"));
+            GUI.enabled = false;
+            EditorGUILayout.PropertyField(serializedObject.FindProperty(SaltField), new GUIContent("Current salt"));
             serializedObject.ApplyModifiedProperties();
             GUILayout.EndVertical();
 
@@ -299,6 +300,7 @@ namespace CosmicShore.Utility
         private string GameObjectAssetPath(GameObject gameObject, int index)
         {
             string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
+            string salt = serializedObject.FindProperty(SaltField).stringValue;
             string crn = index.ToString(numberFormat);
             return $"{currentAssetsPath}/{gameObject.name}.{crn}.{salt}.asset";
         }
@@ -330,7 +332,6 @@ namespace CosmicShore.Utility
             PlayableDirector director = serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
             TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
             string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
-            string trackName = serializedObject.FindProperty(TrackName).stringValue;
             for (int currentRecordingNumber = 0; currentRecordingNumber < recordingNumber; currentRecordingNumber++)
             {
                 string crn = currentRecordingNumber.ToString(numberFormat);
@@ -340,7 +341,7 @@ namespace CosmicShore.Utility
                     GameObjectRecorder gameObjectRecorder = recordersEnumerator.Current;
                     GameObject currentGameObject = gameObjectRecorder.root;
                     Animator currentAnimator = currentGameObject.GetComponent<Animator>();
-                    AnimationTrack animationTrack = currentTimelineAsset.CreateTrack<AnimationTrack>($"{trackName} :: {currentGameObject.name} #{crn}");
+                    AnimationTrack animationTrack = currentTimelineAsset.CreateTrack<AnimationTrack>($"{currentGameObject.name} #{crn}");
                     string newAssetPath = GameObjectAssetPath(currentGameObject, currentRecordingNumber);
                     Debug.Log($"new asset path: {newAssetPath}");
                     AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath);
@@ -358,13 +359,11 @@ namespace CosmicShore.Utility
         private bool ReadyToRecord()
         {
             IEnumerator animators = serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
-            string trackName = serializedObject.FindProperty(TrackName).stringValue;
             string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
             TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
```

</details>

### `ae8f58f09` — Squashed feature branch.

_Emmanuel Eytan, 2025-11-07 15:07:31 -0800_

```text
 Assets/_Scripts/Utility/Recording/RecorderWindow.cs      | 330 +++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Recording/RecorderWindow.cs.meta |  11 ++
 Assets/_Scripts/Utility/Recording/dataHolder.cs          |  61 +++++++++
 Assets/_Scripts/Utility/Recording/dataHolder.cs.meta     |  11 ++
 4 files changed, 413 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 405 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/RecorderWindow.cs b/Assets/_Scripts/Utility/Recording/RecorderWindow.cs
new file mode 100644
index 000000000..ad4562378
--- /dev/null
+++ b/Assets/_Scripts/Utility/Recording/RecorderWindow.cs
@@ -0,0 +1,330 @@
+#if UNITY_EDITOR
+using System.Collections;
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.Playables;
+using UnityEngine.Timeline;
+using UnityEditor;
+using UnityEditor.Animations;
+
+namespace CosmicShore.Utility
+{
+    /// <summary>
+    /// A utility for tracking the transform of specific objects and save them as
+    /// animation clips in a given timeline.
+    /// </summary>
+    [InitializeOnLoadAttribute]
+    public class AnimationRecorder : EditorWindow
+    {
+        /// <summary>
+        /// The name of the serialized property for the track name in the data holder
+        /// object, for use by by Unity's property finder.
+        /// </summary>
+        private readonly string TrackName = "trackName";
+
+        /// <summary>
+        /// The name of the serialized property for the playable director
+        /// in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string Director = "director";
+
+        /// <summary>
+        /// The name of the serialized property for the objects to track
+        /// during the recording, as stored in the data holder object
+        // for use by Unity's property finder.
+        /// </summary>
+        private readonly string ObjectsToTrack = "objectsToTrack";
+
+        /// <summary>
+        /// The name of the serialized property for the delay between recording
+        /// snapshots, as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string RecordingDelay = "recordingDelay";
+
+        /// <summary>
+        /// The name of the serialized property that refrences the timeline asset,
+        /// as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string timelineAsset = "timelineAsset";
+
+        /// <summary>
+        /// The name of the serialized property for the path to the assets used by this
+        /// recorder, as stored in the data holder object, for use by Unity's property finder.
+        /// </summary>
+        private readonly string assetsPath = "assetsPath";
+
+        /// <summary>
+        /// Whether the callbacks for when the editor changes state is registered.
+        /// It should only be necessary to register it once until the editor is closed.
+        /// </summary>
+        private static bool registeredPlayCallbacks = false;
+
+        /// <summary>
+        /// A refrence to the object that contains the data holder for the present session.
+        /// </summary>
+        private DataHolder holder;
+
+        /// <summary>
+        /// A property that will either return a refrence to a holder, or do its best to instantiate
+        /// one and then return it.
+        /// </summary>
+        /// <value>holder</value>
+        private DataHolder Holder
+        {
+            get
```

</details>

### `14fc607a0` — Code review revisions.

_Emmanuel Eytan, 2025-11-07 15:27:09 -0800_

```text
# Conflicts:
#	Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
#	Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
```

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs   |  16 +-
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs |  48 ++----
 Assets/_Scripts/Utility/Recording/RecorderWindow.cs          | 330 -----------------------------------------
 Assets/_Scripts/Utility/Recording/dataHolder.cs              |  61 --------
 4 files changed, 21 insertions(+), 434 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 553 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 7ecbe7978..1b4ac984c 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -19,20 +19,20 @@ namespace CosmicShore.Utility
         /// The overall manager for the timeline to record.
         /// </summary>
         [SerializeField]
-        public PlayableDirector director;
+        private PlayableDirector director;
 
         /// <summary>
         /// An asset (on disk) for the contents of the timeline.
         /// </summary>
         [SerializeField]
-        public TimelineAsset timelineAsset;
+        private TimelineAsset timelineAsset;
 
         /// <summary>
         /// Where all items generated or expcted from this utility should be stored.
         /// The path is relative to the project and will usually start with "Assets/".
         /// </summary>
         [SerializeField]
-        public string assetsPath = "Assets/Recorder";
+        private string assetsPath = "Assets/Recorder";
 
         /// <summary>
         /// The game objects that this recorder will track.
@@ -49,11 +49,17 @@ namespace CosmicShore.Utility
         private float recordingDelay = 1;
 
         /// <summary>
-        /// The salt currently used in recording names.
+        /// An arbirtrary name to add to the recording.
+        /// Will be applied to each new track. Cannot be empty.
         /// </summary>
         [SerializeField]
-        internal string salt;
+        private string trackName;
 
+        /// <summary>
+        /// The salt currently used in recording names.
+        /// </summary>
+        /// 
+        internal string salt;
         #pragma warning restore 0414
     }
 }
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index 23bd854e3..0c872c8b4 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -6,7 +6,6 @@ using UnityEngine.Playables;
 using UnityEngine.Timeline;
 using UnityEditor;
 using UnityEditor.Animations;
-using System;
 
 namespace CosmicShore.Utility
 {
@@ -17,6 +16,12 @@ namespace CosmicShore.Utility
     [InitializeOnLoadAttribute]
     public class AnimationRecorder : EditorWindow
     {
+        /// <summary>
+        /// The name of the serialized property for the track name in the data holder
+        /// object, for use by by Unity's property finder.
+        /// </summary>
+        private readonly string TrackName = "trackName";
+
         /// <summary>
         /// The name of the serialized property for the salt currently in use by
         /// the utility. Can be changed by this code but not by the user.
@@ -78,7 +83,6 @@ namespace CosmicShore.Utility
                 {
                     return holder;
                 }
-                SetupRecordingSystem();
                 return holder;
             }
```

</details>

### `61c5e4168` — Simplified first-time run.

_Emmanuel Eytan, 2025-11-07 15:28:09 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs | 6 +++---
 1 file changed, 3 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 1b4ac984c..e0c36b361 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -19,20 +19,20 @@ namespace CosmicShore.Utility
         /// The overall manager for the timeline to record.
         /// </summary>
         [SerializeField]
-        private PlayableDirector director;
+        public PlayableDirector director;
 
         /// <summary>
         /// An asset (on disk) for the contents of the timeline.
         /// </summary>
         [SerializeField]
-        private TimelineAsset timelineAsset;
+        public TimelineAsset timelineAsset;
 
         /// <summary>
         /// Where all items generated or expcted from this utility should be stored.
         /// The path is relative to the project and will usually start with "Assets/".
         /// </summary>
         [SerializeField]
-        private string assetsPath = "Assets/Recorder";
+        public string assetsPath = "Assets/Recorder";
 
         /// <summary>
         /// The game objects that this recorder will track.
```

</details>

### `60987e1e4` — Changed the GUI to Will's specs.

_Emmanuel Eytan, 2025-11-07 15:28:14 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs | 5 ++---
 1 file changed, 2 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index e0c36b361..25085e3d1 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -49,11 +49,10 @@ namespace CosmicShore.Utility
         private float recordingDelay = 1;
 
         /// <summary>
-        /// An arbirtrary name to add to the recording.
-        /// Will be applied to each new track. Cannot be empty.
+        /// The salt currently used in recording names.
         /// </summary>
         [SerializeField]
-        private string trackName;
+        internal string salt;
 
         /// <summary>
         /// The salt currently used in recording names.
```

</details>

### `386e5464c` — At least, it builds.

_Emmanuel, 2025-11-20 18:26:55 -0800_

```text
 Assets/_Models/Animations.meta                               |   2 +-
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs   |  17 +--
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs | 261 ++++++++++++++++++++++++-----------------
 3 files changed, 166 insertions(+), 114 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 539 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 25085e3d1..8bdbe0bd2 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -28,11 +28,17 @@ namespace CosmicShore.Utility
         public TimelineAsset timelineAsset;
 
         /// <summary>
-        /// Where all items generated or expcted from this utility should be stored.
-        /// The path is relative to the project and will usually start with "Assets/".
+        /// The parent of the folder where all items generated or expected from this utility should be stored.
+        /// The path is relative to the project and must start with "Assets".
         /// </summary>
         [SerializeField]
-        public string assetsPath = "Assets/Recorder";
+        public string assetsParentPath = "Assets";
+
+        /// <summary>
+        /// The name of the directory where all the itames generated or xpected from this utility should be stored.
+        /// </summary>
+        [SerializeField]
+        public string assetsDirectoryName = "Recorder";
 
         /// <summary>
         /// The game objects that this recorder will track.
@@ -54,11 +60,6 @@ namespace CosmicShore.Utility
         [SerializeField]
         internal string salt;
 
-        /// <summary>
-        /// The salt currently used in recording names.
-        /// </summary>
-        /// 
-        internal string salt;
         #pragma warning restore 0414
     }
 }
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index 0c872c8b4..ed8a1b19c 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -1,4 +1,6 @@
 #if UNITY_EDITOR
+using System;
+using System.IO;
 using System.Collections;
 using System.Collections.Generic;
 using UnityEngine;
@@ -6,6 +8,7 @@ using UnityEngine.Playables;
 using UnityEngine.Timeline;
 using UnityEditor;
 using UnityEditor.Animations;
+using UnityEditor;
 
 namespace CosmicShore.Utility
 {
@@ -20,53 +23,59 @@ namespace CosmicShore.Utility
         /// The name of the serialized property for the track name in the data holder
         /// object, for use by by Unity's property finder.
         /// </summary>
-        private readonly string TrackName = "trackName";
+        private const string TrackName = "trackName";
 
         /// <summary>
         /// The name of the serialized property for the salt currently in use by
         /// the utility. Can be changed by this code but not by the user.
         /// </summary>
-        private readonly string SaltField= "salt";
+        private const string SaltField= "salt";
 
         /// <summary>
         /// The name of the serialized property for the playable director
         /// in the data holder object, for use by Unity's property finder.
         /// </summary>
-        private readonly string Director = "director";
+        private const string Director = "director";
 
         /// <summary>
         /// The name of the serialized property for the objects to track
         /// during the recording, as stored in the data holder object
```

</details>

### `75e87ad1b` — The contents of the window actually get displayed.

_Emmanuel, 2025-11-26 13:25:54 -0800_

```text
 .../_Scripts/Utility/SOAP/ScriptablePipData/EventListenerPipData.cs   |   5 +-
 Assets/_Scripts/Utility/SOAP/ScriptablePipData/PipData.cs             |   2 +-
 .../_Scripts/Utility/SOAP/ScriptablePipData/ScriptableEventPipData.cs |   6 +-
 .../Utility/SOAP/ScriptablePrismStats/EventListenerPrismStats.cs      |   7 +-
 .../Utility/SOAP/ScriptablePrismStats/ScriptableEventPrismStats.cs    |   6 +-
 .../Utility/SOAP/ScriptableQuaternion/EventListenerQuaternion.cs      |   7 +-
 .../Utility/SOAP/ScriptableQuaternion/ScriptableEventQuaternion.cs    |   6 +-
 .../Utility/SOAP/ScriptableShipHUDData/EventListenerShipHUDData.cs    |   7 +-
 .../Utility/SOAP/ScriptableShipHUDData/ScriptableEventShipHUDData.cs  |   6 +-
 .../SOAP/ScriptableSilhouetteData/EventListenerSilhouetteData.cs      |   7 +-
 .../SOAP/ScriptableSilhouetteData/ScriptableEventSilhouetteData.cs    |   6 +-
 .../Utility/SOAP/ScriptableTransform/EventListenerTransform.cs        |   7 +-
 .../Utility/SOAP/ScriptableTransform/ScriptableEventTransform.cs      |   6 +-
 Assets/_Scripts/Utility/SOAP/ShipPrefabContainer.cs                   |   6 +-
 Assets/_Scripts/Utility/SavePng.cs                                    |  14 +-
 Assets/_Scripts/Utility/ScreenShots/CaptureScreenShot.cs              |   9 +-
 Assets/_Scripts/Utility/SerializeProperty.cs                          | 140 +++++-----
 Assets/_Scripts/Utility/ShowIfAttribute.cs                            |  23 +-
 Assets/_Scripts/Utility/Singleton.cs                                  |  12 +-
 Assets/_Scripts/Utility/TagContainerSO.cs                             |   2 +-
 Assets/_Scripts/Utility/TagSO.cs                                      |   6 +-
 Assets/_Scripts/Utility/TeamColorPoolManager.cs                       |  14 +-
 Assets/_Scripts/Utility/TextureScale.cs                               |  22 +-
 Assets/_Scripts/Utility/Tools/AudioTester.cs                          |   3 +-
 Assets/_Scripts/Utility/Tools/DependencySpawner.cs                    |   2 +-
 Assets/_Scripts/Utility/Tools/FrogletTools.cs                         |  22 +-
 Assets/_Scripts/Utility/Tools/StartFromAnyScene.cs                    |   3 -
 Packages/manifest.json                                                |   1 +
 Packages/packages-lock.json                                           |  10 +
 330 files changed, 2106 insertions(+), 1804 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 9776 lines)</summary>

```diff
diff --git a/Assets/_Scripts/DialogueSystem/Runtime/Controller/DialogueManager.cs b/Assets/_Scripts/DialogueSystem/Runtime/Controller/DialogueManager.cs
index 713442dee..b25337e9f 100644
--- a/Assets/_Scripts/DialogueSystem/Runtime/Controller/DialogueManager.cs
+++ b/Assets/_Scripts/DialogueSystem/Runtime/Controller/DialogueManager.cs
@@ -10,8 +10,9 @@ namespace CosmicShore.DialogueSystem.Controller
     {
         public static DialogueManager Instance;
 
-        [Header("References")]
-        [SerializeField] private DialogueUIController _uiController;
+        [Header("References")] [SerializeField]
+        private DialogueUIController _uiController;
+
         [SerializeField] private Canvas _dialogueCanvas;
         [SerializeField] private DialogueSetLibrary _dialogueLibrary;
         [SerializeField] private GameObject _mainGameCanvas;
@@ -37,6 +38,7 @@ namespace CosmicShore.DialogueSystem.Controller
                 Debug.LogWarning($"DialogueManager: No DialogueSet found with ID '{setId}'.");
                 return;
             }
+
             PlayDialogueSet(set);
         }
 
@@ -100,7 +102,6 @@ namespace CosmicShore.DialogueSystem.Controller
 
         private void OnNextRequested()
         {
-
         }
 
         [ContextMenu("PlayDefualtSet")]
@@ -109,4 +110,4 @@ namespace CosmicShore.DialogueSystem.Controller
             PlayDialogueById("Monologue");
         }
     }
-}
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/DialogueSystem/Runtime/Events/DialogueEvents.cs b/Assets/_Scripts/DialogueSystem/Runtime/Events/DialogueEvents.cs
index cf23bfaff..bc0bf0100 100644
--- a/Assets/_Scripts/DialogueSystem/Runtime/Events/DialogueEvents.cs
+++ b/Assets/_Scripts/DialogueSystem/Runtime/Events/DialogueEvents.cs
@@ -7,13 +7,11 @@ namespace CosmicShore
         // Start is called once before the first execution of Update after the MonoBehaviour is created
         void Start()
         {
-        
         }
 
         // Update is called once per frame
         void Update()
         {
-        
         }
     }
-}
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueAudioBatchLinker.cs b/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueAudioBatchLinker.cs
index 971ba2c65..c3c688efe 100644
--- a/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueAudioBatchLinker.cs
+++ b/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueAudioBatchLinker.cs
@@ -24,4 +24,4 @@ namespace CosmicShore.DialogueSystem.Editor
         }
     }
 }
-#endif
+#endif
\ No newline at end of file
diff --git a/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueEditorRuntimeTester.cs b/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueEditorRuntimeTester.cs
index d86b04405..52182220e 100644
--- a/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueEditorRuntimeTester.cs
+++ b/Assets/_Scripts/DialogueSystem/Runtime/Helpers/DialogueEditorRuntimeTester.cs
@@ -19,4 +19,4 @@ namespace CosmicShore.DialogueSystem.Editor
 #endif
         }
     }
-}
+}
```

</details>

### `f95977a45` — A bit more order. (DOES NOT WORK!)

_Emmanuel, 2025-12-03 17:13:20 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs    |  18 +-
 Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs | 377 ++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs  | 441 ++++------------------------------------
 3 files changed, 431 insertions(+), 405 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 976 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index 2a5cab880..f666d368a 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -7,9 +7,11 @@ using UnityEngine.Timeline;
 namespace CosmicShore.Utility
 {
     /// <summary>
+    /// This is the Asset Recorder's model.
+    /// 
     /// A simple container for the data that will be used by the Asset Recorder.
     /// There should be one instance of this components on an object whose name is
-    /// set in the member <see cref="RecorderWindow.ManagerName" />.
+    /// set in the member <see cref="AnimationRecorderWindow.AnimationRecorderName" />.
     /// </summary>
     public class RecordingDataHolder : MonoBehaviour
     {
@@ -18,37 +20,37 @@ namespace CosmicShore.Utility
         /// <summary>
         /// The overall manager for the timeline to record.
         /// </summary>
-        [SerializeField] public PlayableDirector director;
+        [SerializeField] internal PlayableDirector director;
 
         /// <summary>
         /// An asset (on disk) for the contents of the timeline.
         /// </summary>
-        [SerializeField] public TimelineAsset timelineAsset;
+        [SerializeField] internal TimelineAsset timelineAsset;
 
         /// <summary>
         /// The parent of the folder where all items generated or expected from this utility should be stored.
         /// The path is relative to the project and must start with "Assets".
         /// </summary>
-        [SerializeField] public string assetsParentPath = "Assets";
+        [SerializeField] internal string assetsParentPath = "Assets";
 
         /// <summary>
         /// The name of the directory where all the itames generated or xpected from this utility should be stored.
         /// </summary>
-        [SerializeField] public string assetsDirectoryName = "Recorder";
+        [SerializeField] internal string assetsDirectoryName = "Recorder";
 
-        [SerializeField] public string trackName = "trackName";
+        [SerializeField] internal string trackName = "trackName";
         
         /// <summary>
         /// The game objects that this recorder will track.
         /// </summary>
-        [SerializeField] private Animator[] objectsToTrack;
+        [SerializeField] internal Animator[] objectsToTrack;
 
         /// <summary>
         /// The time that the recorder will wait for between each snapshot, in seconds.
         /// A larger number means a less detailed capture. A smaller number means
         /// a larger amount of data will be recorder.
         /// </summary>
-        [SerializeField] private float recordingDelay = 1;
+        [SerializeField] internal float recordingDelay = 1;
 
         /// <summary>
         /// The salt currently used in recording names.
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
new file mode 100644
index 000000000..e15613610
--- /dev/null
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
@@ -0,0 +1,377 @@
+using System;
+using System.Collections.Generic;
+using System.IO;
+using UnityEngine;
+using UnityEditor;
+using UnityEditor.Animations;
+using UnityEditor.SceneManagement;
+using UnityEngine.AdaptivePerformance;
+using UnityEngine.Playables;
+using UnityEngine.Timeline;
+
+namespace CosmicShore.Utility
```

</details>

### `e98d65cec` — Still more refactoring. Added methods for finding or creating objects in unity.

_Emmanuel, 2025-12-03 18:37:10 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs      |   2 +-
 Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs   | 323 ++++++++++++++++++++------------------
 Assets/_Scripts/Utility/Recording/AnimationRecorderUtilities.cs |  31 ++++
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs    |  20 +--
 4 files changed, 212 insertions(+), 164 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 553 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
index f666d368a..b0474a8fb 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderData.cs
@@ -4,7 +4,7 @@ using UnityEngine;
 using UnityEngine.Playables;
 using UnityEngine.Timeline;
 
-namespace CosmicShore.Utility
+namespace CosmicShore.Utility.Recording
 {
     /// <summary>
     /// This is the Asset Recorder's model.
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
index e15613610..7ee0f06a5 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
@@ -9,11 +9,13 @@ using UnityEngine.AdaptivePerformance;
 using UnityEngine.Playables;
 using UnityEngine.Timeline;
 
-namespace CosmicShore.Utility
+namespace CosmicShore.Utility.Recording
 {
 
     public class AnimationRecorderProcess : MonoBehaviour
     {
+        #region Member variables.
+        
         /// <summary>
         /// The salt used to keep each recording session distinct.
         ///
@@ -25,20 +27,73 @@ namespace CosmicShore.Utility
         /// Whether the callbacks for when the editor changes state is registered.
         /// It should only be necessary to register it once until the editor is closed.
         /// </summary>
-        private static bool _registeredPlayCallbacks;
+        private bool _registeredPlayCallbacks;
         
         /// <summary>
-        /// The name of the serialized property for the track name in the data holder
-        /// object, for use by by Unity's property finder.
+        /// A collection of every recorder, each one associated with a new game object to track.
         /// </summary>
-        internal const string TrackName = "trackName";
-        
+        private readonly List<GameObjectRecorder> Recorders = new();
+                
         /// <summary>
         /// Number for each recording of the same object.
         /// If object X is recorded 3 times in one session, add 0, 1, and 2 to each of its
         /// recording's temp files.
         /// </summary>
         private int recordingNumber;
+        
+        /// <summary>
+        /// A property that will either return a reference to a holder, or do its best to instantiate
+        /// one and then return it.
+        /// </summary>
+        private RecordingDataHolder Holder { get; set; }
+        
+        // /// <summary>
+        // /// Where the timeline object should be stored, unless overridden.
+        // /// </summary>
+        // private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";
+        
+        /// <summary>
+        /// A reference to the data holder that unity can use to handle serialized properties.
+        /// </summary>
+        internal SerializedObject RecorderSerializedObject;
+        
+        /// <summary>
+        /// Format in which to convert the number of each recording assets to a string,
+        /// with the right numbers of leading zeros.
+        /// </summary>
+        private const string NumberFormat = "D4";
+        
+        /// <summary>
+        /// Whether the utility is in the process of recording.
+        /// </summary>
```

</details>

### `23deb835f` — Things are now set up correctly.

_Emmanuel, 2025-12-08 16:49:25 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs   | 51 ++++++++++++++++++++++-----------------
 Assets/_Scripts/Utility/Recording/AnimationRecorderUtilities.cs |  6 ++++-
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs    |  9 ++++---
 3 files changed, 40 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 142 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
index 7ee0f06a5..ab8228bdf 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Collections.Generic;
 using System.IO;
+using Unity.Entities;
 using UnityEngine;
 using UnityEditor;
 using UnityEditor.Animations;
@@ -50,7 +51,7 @@ namespace CosmicShore.Utility.Recording
         // /// <summary>
         // /// Where the timeline object should be stored, unless overridden.
         // /// </summary>
-        // private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";
+        private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";
         
         /// <summary>
         /// A reference to the data holder that unity can use to handle serialized properties.
@@ -177,17 +178,16 @@ namespace CosmicShore.Utility.Recording
                 $"{gameObject.name}.{crn}.{salt}.asset");
         }
 
-        internal void SetupRecordingSystem2()
-        {
-            var newSalt = GenerateSalt();
-            // var newAssetPath = Path.Combine(sceneData.assetsParentPath, AssetsDirectoryName,
-            //     $"NewTimeline.{newSalt}.playable");
-            // AssetDatabase.CopyAsset(DefaultTimelinePath, newAssetPath);
-            // Holder.director = director;
-            // var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
-            // Holder.timelineAsset = newTimelineAsset;
-            // director.playableAsset = newTimelineAsset;
-        }
+        // internal void SetupRecordingSystem2()
+        // {
+        //     var newSalt = GenerateSalt();
+        //     var newAssetPath = Path.Combine(AssetsParentPath, AssetsDirectoryName,
+        //          $"NewTimeline.{newSalt}.playable");
+        //     AssetDatabase.CopyAsset(DefaultTimelinePath, newAssetPath);
+        //     var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
+        //      Holder.timelineAsset = newTimelineAsset;
+        //     director.playableAsset = newTimelineAsset;
+        // }
         
         /// <summary>
         /// Helper method that returns whether there are enough settings available to start recording.
@@ -202,7 +202,7 @@ namespace CosmicShore.Utility.Recording
                 Holder.assetsDirectoryName != "" &&
                 !Holder.timelineAsset;
         }
-        
+
         /// <summary>
         /// Makes sure that none of the elements needed for a recording are null.
         /// Called at several points while the current utility is running.
@@ -211,15 +211,23 @@ namespace CosmicShore.Utility.Recording
         internal void Initialize()
         {
             Holder ??= gameObject.GetOrAddComponent<RecordingDataHolder>();
-            RecorderSerializedObject ??= new SerializedObject(Holder);
+            // The null-coalescing operator does not seem to work with Unity game objects.
+            if (RecorderSerializedObject == null)
+            {
+                RecorderSerializedObject = new SerializedObject(Holder);
+            }
             var playableDirector = gameObject.GetOrAddComponent<PlayableDirector>();
-            Holder.director ??= playableDirector;
-            Debug.Log($"Holder : {Holder.name} :: playableDirector :  {playableDirector.playableAsset.name}");
-            Debug.Log($"Holder : {Holder.name} :: Holder.director : {Holder.director.name}");
-            var newAssetPath = GameObjectAssetPath(recordingNumber);
-            var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
-            Holder.timelineAsset = newTimelineAsset;
-            Holder.director.playableAsset = newTimelineAsset;
+            Holder.director = playableDirector;
+            // The null-coalescing operator does not seem to work with Unity game objects.
+            if (Holder.director.playableAsset == null)
+            {
```

</details>

### `1d12e5163` — More stable, but the player game object does not show up when a level starts.

_Emmanuel, 2025-12-10 18:46:16 -0800_

```text
 Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs   | 11 ++++++++++-
 Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs    | 12 +++++++++++-
 Assets/_Scripts/Utility/Recording/DefaultTimeline.playable      | 22 ----------------------
 Assets/_Scripts/Utility/Recording/DefaultTimeline.playable.meta |  8 --------
 4 files changed, 21 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
index ab8228bdf..39a2baead 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderProcess.cs
@@ -355,7 +355,7 @@ namespace CosmicShore.Utility.Recording
                 case PlayModeStateChange.ExitingEditMode:
                     break;
                 default:
-                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
+                    // throw new ArgumentOutOfRangeException(nameof(state), state, null);
             }
         }
         
@@ -395,5 +395,14 @@ namespace CosmicShore.Utility.Recording
             _timer -= Time.deltaTime;
         }
         #endregion
+        
+        #region Unity methods
+
+        void Awake()
+        {
+            DontDestroyOnLoad(this.gameObject);
+        }
+        
+        #endregion
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
index b9489d490..9b1f33ff6 100644
--- a/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
+++ b/Assets/_Scripts/Utility/Recording/AnimationRecorderWindow.cs
@@ -62,7 +62,13 @@ namespace CosmicShore.Utility.Recording
             {
                 _recordingSystemGameObject = new GameObject(ARP.AnimationRecorderName);
             }
+            InstantiateRecorderProcess();
+        }
+
+        private void InstantiateRecorderProcess()
+        {
             _recorderProcess = _recordingSystemGameObject.GetOrAddComponent<ARP>();
+            _recorderProcess.Initialize();
             _serializedObject = _recorderProcess.RecorderSerializedObject;
         }
 
@@ -86,7 +92,11 @@ namespace CosmicShore.Utility.Recording
         /// </summary>
         private void _OnGUI()
         {
-            if (_serializedObject == null)
+            if (_recorderProcess == null)
+            {
+                InstantiateRecorderProcess();
+            }
+            else if (_serializedObject == null)
             {
                 _recorderProcess.Initialize();
                 _serializedObject = _recorderProcess.RecorderSerializedObject;
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
