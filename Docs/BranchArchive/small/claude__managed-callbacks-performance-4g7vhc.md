# Branch archive: `claude/managed-callbacks-performance-4g7vhc`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-20 by Claude
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/763
- **Forked from:** `eb85e1e30` (2026-08-17, Merge pull request #737 from froglet-studio/claude/crystal-sizing-lifeform-lev)
- **Tip:** `90db4fe2f`
- **Files touched (3):**
  - `Assets/_Scripts/Editor/PlayModeSOProtector.cs`
  - `Assets/_Scripts/Editor/SceneBootstrapper.cs`
  - `Docs/PERFORMANCE_OPTIMIZATION.md`

### `90db4fe2f` — perf(editor): cut domain-reload cost at the play-mode boundary

_Claude, 2026-08-20 17:45:21 +0000_

```text
Both fixes target work that runs inside the window the editor spends in
"Run managed callbacks", where the main thread is blocked and Windows
marks the editor Not Responding.

PlayModeSOProtector: the play-mode SO snapshot held ~11 MB of asset text
across ~790 SessionState string keys for the whole play session, read
every file back on exit to compare contents, and called ImportAsset with
ForceUpdate one asset at a time (each triggering its own synchronous
refresh). It now snapshots to Library/PlayModeSOSnapshot, gates restore
on write time + length so an untouched asset costs one stat call instead
of a full read-back, and batches the reimports inside
StartAssetEditing/StopAssetEditing. A small SessionState bool keeps a
crash-orphaned snapshot from being replayed into a later editor session.
Assets created or deleted during play are still left alone.

SceneBootstrapper: the OnValidate-noise auto-save ran
EditorSceneManager.SaveScene on the active scene after every domain
reload and every play-mode exit whenever Cinemachine/URP/NetworkManager
OnValidate dirtied it -- Menu_Main is 4.1 MB. It is now opt-in and off
by default, toggled from FrogletTools > Scene Setup > Testing
Multiplayer. With it off the scene reads as dirty and saving is the
developer's call.

Docs: PERFORMANCE_OPTIMIZATION.md Task 10 records what the phase is, the
five ranked contributors, the Editor.log "Domain Reload Profiling:"
block as the measurement that ends the guessing, and the three deferred
items with the reason each needs its own pass.
```

```text
 Assets/_Scripts/Editor/PlayModeSOProtector.cs | 192 +++++++++++++++++++++++++++++++++++++++++++++-----------
 Assets/_Scripts/Editor/SceneBootstrapper.cs   |  51 ++++++++++++++-
 Docs/PERFORMANCE_OPTIMIZATION.md              |  94 +++++++++++++++++++++++++++
 3 files changed, 300 insertions(+), 37 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 463 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/PlayModeSOProtector.cs b/Assets/_Scripts/Editor/PlayModeSOProtector.cs
index 1d40f15c4..7a1f82310 100644
--- a/Assets/_Scripts/Editor/PlayModeSOProtector.cs
+++ b/Assets/_Scripts/Editor/PlayModeSOProtector.cs
@@ -1,4 +1,5 @@
 #if UNITY_EDITOR
+using System;
 using System.Collections.Generic;
 using System.IO;
 using UnityEditor;
@@ -20,25 +21,49 @@ namespace CosmicShore.Editor
     ///    mutations by design.
     ///
     /// Approach:
-    ///   ExitingEditMode  → snapshot every .asset file in _SO_Assets/ into
-    ///                      SessionState (survives domain reload).
+    ///   ExitingEditMode  → copy every .asset file in _SO_Assets/ into a
+    ///                      snapshot folder under Library/ (outside the
+    ///                      AssetDatabase), alongside an index recording each
+    ///                      file's pre-play write time + length.
     ///   EnteredEditMode  → schedule a deferred restore via delayCall so it
     ///                      runs AFTER SOAP's own OnPlayModeStateChanged
     ///                      callbacks (which re-dirty the assets). Then write
     ///                      the original bytes back and force-reimport.
+    ///
+    /// Performance notes (this runs on the play-mode boundary, inside the
+    /// window the user sees as "Run managed callbacks" / a frozen editor):
+    ///
+    /// * The snapshot lives on disk rather than in SessionState. The previous
+    ///   implementation held ~11 MB of asset text across ~790 SessionState
+    ///   string keys for the whole play session.
+    /// * Restore is gated on write time + length, so the common case (an asset
+    ///   play mode never touched) costs one stat call instead of reading the
+    ///   file back and comparing its full contents.
+    /// * The reimports are batched inside StartAssetEditing/StopAssetEditing.
+    ///   Unbatched, each ImportAsset triggers its own synchronous refresh.
     /// </summary>
     [InitializeOnLoad]
     static class PlayModeSOProtector
     {
-        private const string KeyPrefix = "PMSOP_";
-        private const string PathListKey = KeyPrefix + "Paths";
         private const string SOAssetsRoot = "Assets/_SO_Assets";
+        private const string SnapshotDirName = "PlayModeSOSnapshot";
+        private const string IndexFileName = "index.tsv";
+
+        // Small SessionState flag (not the payload) so a snapshot left behind by
+        // an editor crash is never replayed into a later editor session.
+        private const string SnapshotValidKey = "PMSOP_SnapshotValid";
 
         static PlayModeSOProtector()
         {
             EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
         }
 
+        private static string ProjectRoot =>
+            Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? string.Empty;
+
+        private static string SnapshotRoot =>
+            Path.Combine(ProjectRoot, "Library", SnapshotDirName);
+
         private static void OnPlayModeStateChanged(PlayModeStateChange state)
         {
             switch (state)
@@ -57,59 +82,154 @@ namespace CosmicShore.Editor
 
         private static void CaptureAssets()
         {
+            SessionState.EraseBool(SnapshotValidKey);
+
             if (!Directory.Exists(SOAssetsRoot))
                 return;
 
-            var files = Directory.GetFiles(SOAssetsRoot, "*.asset", SearchOption.AllDirectories);
-            var paths = new List<string>(files.Length);
+            var snapshotRoot = SnapshotRoot;
 
-            foreach (var file in files)
+            try
             {
-                var path = file.Replace('\\', '/');
-                paths.Add(path);
-                SessionState.SetString(KeyPrefix + path, File.ReadAllText(path));
-            }
+                if (Directory.Exists(snapshotRoot))
+                    Directory.Delete(snapshotRoot, true);
+
+                Directory.CreateDirectory(snapshotRoot);
+
+                var files = Directory.GetFiles(SOAssetsRoot, "*.asset", SearchOption.AllDirectories);
+                var index = new List<string>(files.Length);
 
-            SessionState.SetString(PathListKey, string.Join(";", paths));
+                foreach (var file in files)
+                {
+                    var assetPath = file.Replace('\\', '/');
+                    var relative = assetPath.Substring(SOAssetsRoot.Length).TrimStart('/');
+                    var snapshotPath = Path.Combine(snapshotRoot, relative);
+
+                    Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));
+                    File.Copy(assetPath, snapshotPath, true);
+
+                    var info = new FileInfo(assetPath);
+                    index.Add($"{relative}\t{info.LastWriteTimeUtc.Ticks}\t{info.Length}");
+                }
+
+                File.WriteAllLines(Path.Combine(snapshotRoot, IndexFileName), index);
+                SessionState.SetBool(SnapshotValidKey, true);
+            }
+            catch (Exception e)
+            {
+                // A failed snapshot must not block entering play mode; it only
+                // means play-mode SO mutations will persist this once.
+                UnityEngine.Debug.LogWarning($"[PlayModeSOProtector] Snapshot failed: {e.Message}");
+                SessionState.EraseBool(SnapshotValidKey);
+            }
         }
 
         private static void RestoreAssets()
         {
-            var raw = SessionState.GetString(PathListKey, "");
-            if (string.IsNullOrEmpty(raw))
+            if (!SessionState.GetBool(SnapshotValidKey, false))
+                return;
+
+            SessionState.EraseBool(SnapshotValidKey);
+
+            var snapshotRoot = SnapshotRoot;
+            var indexPath = Path.Combine(snapshotRoot, IndexFileName);
+
+            if (!File.Exists(indexPath))
                 return;
 
-            var paths = raw.Split(';');
             var restoredPaths = new List<string>();
 
-            foreach (var path in paths)
+            try
             {
-                if (string.IsNullOrEmpty(path))
-                    continue;
-
-                var key = KeyPrefix + path;
-                var saved = SessionState.GetString(key, null);
-                SessionState.EraseString(key);
+                foreach (var line in File.ReadAllLines(indexPath))
+                {
+                    if (string.IsNullOrEmpty(line))
+                        continue;
+
```

</details>
