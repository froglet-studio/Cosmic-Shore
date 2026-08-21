#if UNITY_EDITOR
using System;
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
    ///    have no reset mechanism at all - Unity persists their play-mode
    ///    mutations by design.
    ///
    /// Approach:
    ///   ExitingEditMode  → copy every .asset file in _SO_Assets/ into a
    ///                      snapshot folder under Library/ (outside the
    ///                      AssetDatabase), alongside an index recording each
    ///                      file's pre-play write time + length.
    ///   EnteredEditMode  → schedule a deferred restore via delayCall so it
    ///                      runs AFTER SOAP's own OnPlayModeStateChanged
    ///                      callbacks (which re-dirty the assets). Then write
    ///                      the original bytes back and force-reimport.
    ///
    /// Performance notes (this runs on the play-mode boundary, inside the
    /// window the user sees as "Run managed callbacks" / a frozen editor):
    ///
    /// * The snapshot lives on disk rather than in SessionState. The previous
    ///   implementation held ~11 MB of asset text across ~790 SessionState
    ///   string keys for the whole play session.
    /// * Restore is gated on write time + length, so the common case (an asset
    ///   play mode never touched) costs one stat call instead of reading the
    ///   file back and comparing its full contents.
    /// * The reimports are batched inside StartAssetEditing/StopAssetEditing.
    ///   Unbatched, each ImportAsset triggers its own synchronous refresh.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeSOProtector
    {
        private const string SOAssetsRoot = "Assets/_SO_Assets";
        private const string SnapshotDirName = "PlayModeSOSnapshot";
        private const string IndexFileName = "index.tsv";

        // Small SessionState flag (not the payload) so a snapshot left behind by
        // an editor crash is never replayed into a later editor session.
        private const string SnapshotValidKey = "PMSOP_SnapshotValid";

        static PlayModeSOProtector()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static string ProjectRoot =>
            Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? string.Empty;

        private static string SnapshotRoot =>
            Path.Combine(ProjectRoot, "Library", SnapshotDirName);

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
            SessionState.EraseBool(SnapshotValidKey);

            if (!Directory.Exists(SOAssetsRoot))
                return;

            var snapshotRoot = SnapshotRoot;

            try
            {
                if (Directory.Exists(snapshotRoot))
                    Directory.Delete(snapshotRoot, true);

                Directory.CreateDirectory(snapshotRoot);

                var files = Directory.GetFiles(SOAssetsRoot, "*.asset", SearchOption.AllDirectories);
                var index = new List<string>(files.Length);

                foreach (var file in files)
                {
                    var assetPath = file.Replace('\\', '/');
                    var relative = assetPath.Substring(SOAssetsRoot.Length).TrimStart('/');
                    var snapshotPath = Path.Combine(snapshotRoot, relative);

                    Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));
                    File.Copy(assetPath, snapshotPath, true);

                    var info = new FileInfo(assetPath);
                    index.Add($"{relative}\t{info.LastWriteTimeUtc.Ticks}\t{info.Length}");
                }

                File.WriteAllLines(Path.Combine(snapshotRoot, IndexFileName), index);
                SessionState.SetBool(SnapshotValidKey, true);
            }
            catch (Exception e)
            {
                // A failed snapshot must not block entering play mode; it only
                // means play-mode SO mutations will persist this once.
                UnityEngine.Debug.LogWarning($"[PlayModeSOProtector] Snapshot failed: {e.Message}");
                SessionState.EraseBool(SnapshotValidKey);
            }
        }

        private static void RestoreAssets()
        {
            if (!SessionState.GetBool(SnapshotValidKey, false))
                return;

            SessionState.EraseBool(SnapshotValidKey);

            var snapshotRoot = SnapshotRoot;
            var indexPath = Path.Combine(snapshotRoot, IndexFileName);

            if (!File.Exists(indexPath))
                return;

            var restoredPaths = new List<string>();

            try
            {
                foreach (var line in File.ReadAllLines(indexPath))
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var parts = line.Split('\t');
                    if (parts.Length != 3)
                        continue;

                    if (!long.TryParse(parts[1], out var ticks) || !long.TryParse(parts[2], out var length))
                        continue;

                    var relative = parts[0];
                    var assetPath = $"{SOAssetsRoot}/{relative}";
                    var snapshotPath = Path.Combine(snapshotRoot, relative);

                    if (!File.Exists(assetPath) || !File.Exists(snapshotPath))
                        continue;

                    // Untouched by play mode: one stat call, no read-back.
                    var info = new FileInfo(assetPath);
                    if (info.LastWriteTimeUtc.Ticks == ticks && info.Length == length)
                        continue;

                    // Rewritten, but possibly byte-identical (Unity re-serializing
                    // to the same result). Only the changed subset is read.
                    if (FilesMatch(assetPath, snapshotPath))
                        continue;

                    File.Copy(snapshotPath, assetPath, true);
                    restoredPaths.Add(assetPath);
                }

                if (restoredPaths.Count == 0)
                    return;

                // Force-reimport each restored file so Unity reloads the SO from
                // disk and clears any dirty flags left by SOAP's reset callbacks.
                // Batched: an unbatched ImportAsset triggers a full synchronous
                // refresh per asset.
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var path in restoredPaths)
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PlayModeSOProtector] Restore failed: {e.Message}");
            }
            finally
            {
                TryDeleteSnapshot(snapshotRoot);
            }
        }

        private static bool FilesMatch(string a, string b)
        {
            var bytesA = File.ReadAllBytes(a);
            var bytesB = File.ReadAllBytes(b);

            if (bytesA.Length != bytesB.Length)
                return false;

            for (int i = 0; i < bytesA.Length; i++)
            {
                if (bytesA[i] != bytesB[i])
                    return false;
            }

            return true;
        }

        private static void TryDeleteSnapshot(string snapshotRoot)
        {
            try
            {
                if (Directory.Exists(snapshotRoot))
                    Directory.Delete(snapshotRoot, true);
            }
            catch (Exception)
            {
                // Leftover snapshot is harmless - the SessionState flag gates replay.
            }
        }
    }
}
#endif
