# Branch archive: `claude/toast-prefab-animation-gnzuqk`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-17 by Claude
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/607
- **Forked from:** `5e9dd0039` (2026-07-17, Merge pull request #606 from froglet-studio/claude/sparrows-guns-missile-regre)
- **Tip:** `b8af2d915`
- **Files touched (7):**
  - `Assets/Editor/ToastNotificationSetup.cs`
  - `Assets/Resources/ToastNotificationItem.prefab`
  - `Assets/Resources/ToastNotificationItem.prefab.meta`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationItem.cs`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationSettingsSO.cs`

### `b8af2d915` — refactor(ui): spawn toasts from authored prefab and animate text

_Claude, 2026-07-17 21:09:44 +0000_

```text
The runtime-auto-created ToastNotificationManager (via ToastNotificationAPI)
never wired a toast prefab, so every in-game toast fell back to the plain
code-built item instead of the authored ToastNotificationItem.prefab.

- Move ToastNotificationItem.prefab into Resources (GUID preserved) and
  resolve it in ToastNotificationManager.Awake: serialized field first,
  then Resources/ToastNotificationItem, code-built default as last resort
- Animate the message: typewriter reveal (TMP maxVisibleCharacters tween)
  joined with a slide-in from the left, using the previously-dead slideIn*
  settings; auto-dismiss slides back out left, swipe-dismiss continues right
- New settings: useTypewriterText, typewriterCharactersPerSecond,
  typewriterMaxDuration; removed dead layout fields (topMargin, leftMargin,
  stackSpacing) now owned by the container's VerticalLayoutGroup
- Replace all reflection-based field wiring with a public
  ToastNotificationManager.Configure (runtime) and SerializedObject
  assignment (editor tool); fix the setup tool's inconsistent asset paths
  to match the Resources locations the runtime actually loads from
- Harden pooling against scene changes destroying the container: purge
  destroyed items from the active list and pool before reuse
- Drop the unused legacy Show(string, Vector2, settings) and AnimateToY
  no-op overloads; smooth the swipe-cancel return with a tween
```

```text
 Assets/Editor/ToastNotificationSetup.cs                               | 103 ++++++++++++++------------------
 .../{_Prefabs/UI Elements => Resources}/ToastNotificationItem.prefab  |   0
 .../UI Elements => Resources}/ToastNotificationItem.prefab.meta       |   0
 Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs          |  26 ++------
 Assets/_Scripts/UI/ToastNotification/ToastNotificationItem.cs         |  75 ++++++++++++++++-------
 Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs      |  82 ++++++++++++++++++-------
 Assets/_Scripts/UI/ToastNotification/ToastNotificationSettingsSO.cs   |  30 +++++-----
 7 files changed, 179 insertions(+), 137 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 596 lines)</summary>

```diff
diff --git a/Assets/Editor/ToastNotificationSetup.cs b/Assets/Editor/ToastNotificationSetup.cs
index 3477212e8..ce0d46e1b 100644
--- a/Assets/Editor/ToastNotificationSetup.cs
+++ b/Assets/Editor/ToastNotificationSetup.cs
@@ -8,9 +8,13 @@ namespace CosmicShore.Editor
 {
     public static class ToastNotificationSetup
     {
-        private const string PrefabFolder = "Assets/_Prefabs/UI Elements";
-        private const string SOFolder = "Assets/_SO_Assets";
+        // Everything lives in Resources so the runtime-auto-created manager
+        // (ToastNotificationAPI) can resolve settings, channel, and prefab.
+        private const string ResourcesFolder = "Assets/Resources";
         private const string ChannelFolder = "Assets/Resources/Channels";
+        private const string PrefabPath = ResourcesFolder + "/ToastNotificationItem.prefab";
+        private const string SettingsPath = ResourcesFolder + "/ToastNotificationSettings.asset";
+        private const string ChannelPath = ChannelFolder + "/ToastNotificationChannel.asset";
 
         [MenuItem("Cosmic Shore/Toast Notification/Create All Assets", priority = 0)]
         public static void CreateAllAssets()
@@ -20,55 +24,51 @@ namespace CosmicShore.Editor
             CreatePrefab();
             CreateManagerInScene();
 
-            Debug.Log("[ToastNotification] All assets created. Customize the prefab at " +
-                      PrefabFolder + "/ToastNotificationItem.prefab");
+            Debug.Log("[ToastNotification] All assets created. Customize the prefab at " + PrefabPath);
         }
 
         [MenuItem("Cosmic Shore/Toast Notification/Create Settings Asset")]
         public static void CreateSettingsAsset()
         {
-            var path = SOFolder + "/ToastNotificationSettings.asset";
-            if (AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(path) != null)
+            if (AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(SettingsPath) != null)
             {
-                Debug.Log("[ToastNotification] Settings asset already exists at " + path);
+                Debug.Log("[ToastNotification] Settings asset already exists at " + SettingsPath);
                 return;
             }
 
-            EnsureFolder(SOFolder);
+            EnsureFolder(ResourcesFolder);
             var settings = ScriptableObject.CreateInstance<ToastNotificationSettingsSO>();
-            AssetDatabase.CreateAsset(settings, path);
+            AssetDatabase.CreateAsset(settings, SettingsPath);
             AssetDatabase.SaveAssets();
-            Debug.Log("[ToastNotification] Created settings at " + path);
+            Debug.Log("[ToastNotification] Created settings at " + SettingsPath);
         }
 
         [MenuItem("Cosmic Shore/Toast Notification/Create Channel Asset")]
         public static void CreateChannelAsset()
         {
-            var path = ChannelFolder + "/ToastNotificationChannel.asset";
-            if (AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(path) != null)
+            if (AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(ChannelPath) != null)
             {
-                Debug.Log("[ToastNotification] Channel asset already exists at " + path);
+                Debug.Log("[ToastNotification] Channel asset already exists at " + ChannelPath);
                 return;
             }
 
             EnsureFolder(ChannelFolder);
             var channel = ScriptableObject.CreateInstance<ToastNotificationChannel>();
-            AssetDatabase.CreateAsset(channel, path);
+            AssetDatabase.CreateAsset(channel, ChannelPath);
             AssetDatabase.SaveAssets();
-            Debug.Log("[ToastNotification] Created channel at " + path);
+            Debug.Log("[ToastNotification] Created channel at " + ChannelPath);
         }
 
         [MenuItem("Cosmic Shore/Toast Notification/Create Prefab")]
         public static void CreatePrefab()
         {
-            var path = PrefabFolder + "/ToastNotificationItem.prefab";
-            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
+            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
             {
-                Debug.Log("[ToastNotification] Prefab already exists at " + path);
+                Debug.Log("[ToastNotification] Prefab already exists at " + PrefabPath);
                 return;
             }
 
-            EnsureFolder(PrefabFolder);
+            EnsureFolder(ResourcesFolder);
 
             // Root object
             var root = new GameObject("ToastNotificationItem");
@@ -105,20 +105,16 @@ namespace CosmicShore.Editor
             tmp.overflowMode = TextOverflowModes.Ellipsis;
             tmp.raycastTarget = false;
 
-            // Add the toast item component
+            // Add the toast item component and wire the messageText field
             var item = root.AddComponent<ToastNotificationItem>();
-
-            // Wire the messageText field
-            var field = typeof(ToastNotificationItem).GetField("messageText",
-                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-            field?.SetValue(item, tmp);
+            SetObjectReference(item, "messageText", tmp);
 
             // Save as prefab
-            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
+            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
             Object.DestroyImmediate(root);
 
             EditorGUIUtility.PingObject(prefab);
-            Debug.Log("[ToastNotification] Created prefab at " + path +
+            Debug.Log("[ToastNotification] Created prefab at " + PrefabPath +
                       " - customize visuals here (background, font, size, etc.)");
         }
 
@@ -134,39 +130,14 @@ namespace CosmicShore.Editor
             var go = new GameObject("ToastNotificationManager");
             var mgr = go.AddComponent<ToastNotificationManager>();
 
-            // Wire settings
-            var settings = AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(
-                "Assets/Resources/ToastNotificationSettings.asset");
-            if (settings != null)
-            {
-                var settingsField = typeof(ToastNotificationManager).GetField("settings",
-                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-                settingsField?.SetValue(mgr, settings);
-            }
-
-            // Wire channel
-            var channel = AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(
-                ChannelFolder + "/ToastNotificationChannel.asset");
-            if (channel != null)
-            {
-                var channelField = typeof(ToastNotificationManager).GetField("channel",
-                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-                channelField?.SetValue(mgr, channel);
-            }
+            SetObjectReference(mgr, "settings",
+                AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(SettingsPath));
+            SetObjectReference(mgr, "channel",
+                AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(ChannelPath));
 
-            // Wire prefab
-            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
-                PrefabFolder + "/ToastNotificationItem.prefab");
+            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
             if (prefab != null)
-            {
-                var item = prefab.GetComponent<ToastNotificationItem>();
-                if (item != null)
```

</details>
