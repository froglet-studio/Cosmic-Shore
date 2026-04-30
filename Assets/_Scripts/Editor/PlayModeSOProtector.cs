#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Prevents ScriptableObject assets in _SO_Assets from persisting play-mode
    /// changes to disk.
    ///
    /// Two independent problems are solved here:
    ///
    /// 1. SOAP's built-in ResetToInitialValue() relies on a non-serialized
    ///    _initialValue field that is lost during domain reload, so it resets
    ///    to default(T) instead of the pre-play-mode value.
    ///
    /// 2. Plain ScriptableObject assets (GameDataSO, CellRuntimeDataSO, etc.)
    ///    have no reset mechanism at all — Unity persists their play-mode
    ///    mutations by design.
    ///
    /// Approach:
    ///   ExitingEditMode  → snapshot every .asset file in _SO_Assets/ into
    ///                      SessionState (survives domain reload).
    ///   EnteredEditMode  → schedule a deferred restore via delayCall so it
    ///                      runs AFTER SOAP's own OnPlayModeStateChanged
    ///                      callbacks (which re-dirty the assets). Then write
    ///                      the original bytes back and force-reimport.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeSOProtector
    {
        private const string KeyPrefix = "PMSOP_";
        private const string PathListKey = KeyPrefix + "Paths";
        private const string SOAssetsRoot = "Assets/_SO_Assets";

        static PlayModeSOProtector()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    CaptureAssets();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    // Defer so we run AFTER SOAP's ResetToInitialValue() callbacks,
                    // which fire during this same EnteredEditMode dispatch and
                    // re-dirty assets with default(T) + EditorUtility.SetDirty().
                    EditorApplication.delayCall += RestoreAssets;
                    break;
            }
        }

        private static void CaptureAssets()
        {
            if (!Directory.Exists(SOAssetsRoot))
                return;

            var files = Directory.GetFiles(SOAssetsRoot, "*.asset", SearchOption.AllDirectories);
            var paths = new List<string>(files.Length);

            foreach (var file in files)
            {
                var path = file.Replace('\\', '/');
                paths.Add(path);
                SessionState.SetString(KeyPrefix + path, File.ReadAllText(path));
            }

            SessionState.SetString(PathListKey, string.Join(";", paths));
        }

        private static void RestoreAssets()
        {
            var raw = SessionState.GetString(PathListKey, "");
            if (string.IsNullOrEmpty(raw))
                return;

            var paths = raw.Split(';');
            var restoredPaths = new List<string>();

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                var key = KeyPrefix + path;
                var saved = SessionState.GetString(key, null);
                SessionState.EraseString(key);

                if (string.IsNullOrEmpty(saved) || !File.Exists(path))
                    continue;

                if (File.ReadAllText(path) == saved)
                    continue;

                File.WriteAllText(path, saved);
                restoredPaths.Add(path);
            }

            SessionState.EraseString(PathListKey);

            if (restoredPaths.Count == 0)
                return;

            // Force-reimport each restored file so Unity reloads the SO from
            // disk and clears any dirty flags left by SOAP's reset callbacks.
            foreach (var path in restoredPaths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
