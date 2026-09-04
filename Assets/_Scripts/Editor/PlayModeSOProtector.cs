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
    ///   During play      → an AssetModificationProcessor records exactly which
    ///                      _SO_Assets paths UNITY SAVES while playing (Ctrl+S,
    ///                      AssetDatabase.SaveAssets from a tool, quit-save).
    ///                      That set — not "whatever differs on disk" — is what
    ///                      the protector is allowed to restore.
    ///   EnteredEditMode  → schedule a deferred restore via delayCall so it
    ///                      runs AFTER SOAP's own OnPlayModeStateChanged
    ///                      callbacks (which re-dirty the assets). Restore ONLY
    ///                      the tracked, actually-different files, then batch
    ///                      force-reimport them.
    ///
    /// Why the tracked set is load-bearing (2026-08-21): the previous version
    /// restored ANY file whose write time + length differed from the snapshot.
    /// A git branch switch (or pull) while the editor sat in play mode rewrites
    /// hundreds of _SO_Assets files — the restore then read every one of them
    /// as "changed during play", overwrote the NEW branch's content with the
    /// old branch's snapshot (silent working-tree corruption), and spent
    /// minutes doing it on the play-exit boundary. External writes never pass
    /// through Unity's save pipeline, so keying the restore on
    /// OnWillSaveAssets makes the protector structurally blind to git — which
    /// is exactly the blindness it needs. The tracked set lives in
    /// SessionState so it survives the play-exit domain reload when Enter Play
    /// Mode Options is off.
    ///
    /// Performance notes (this runs on the play-mode boundary):
    ///
    /// * The snapshot lives on disk rather than in SessionState. An earlier
    ///   implementation held ~11 MB of asset text across ~790 SessionState
    ///   string keys for the whole play session.
    /// * The common case — nothing was saved during play — is now one
    ///   SessionState read and a snapshot delete. No stats, no read-backs.
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

        // Newline-joined asset paths Unity saved while in play mode. SessionState
        // (not a static) so it survives the play-exit domain reload when Enter
        // Play Mode Options is disabled.
        private const string SavedDuringPlayKey = "PMSOP_SavedDuringPlay";

        static PlayModeSOProtector()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static string ProjectRoot =>
            Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? string.Empty;

        private static string SnapshotRoot =>
            Path.Combine(ProjectRoot, "Library", SnapshotDirName);

        /// <summary>
        /// Called by <see cref="PlayModeSOSaveTracker"/> for every batch of paths
        /// Unity saves while in play mode. This ledger is the ONLY evidence the
        /// restore acts on: a file that changed on disk without passing through
        /// Unity's save pipeline changed for an external reason (git, a text
        /// editor) and must be left alone.
        /// </summary>
        internal static void RecordPlayModeSaves(string[] paths)
        {
            List<string> tracked = null;
            foreach (var p in paths)
            {
                if (p != null && p.StartsWith(SOAssetsRoot + "/", StringComparison.Ordinal)
                              && p.EndsWith(".asset", StringComparison.Ordinal))
                    (tracked ??= new List<string>()).Add(p);
            }

            if (tracked == null)
                return;

            var existing = SessionState.GetString(SavedDuringPlayKey, string.Empty);
            var set = new HashSet<string>(
                existing.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var p in tracked) set.Add(p);
            SessionState.SetString(SavedDuringPlayKey, string.Join("\n", set));
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
            SessionState.EraseBool(SnapshotValidKey);
            SessionState.EraseString(SavedDuringPlayKey);

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
            var trackedRaw = SessionState.GetString(SavedDuringPlayKey, string.Empty);
            SessionState.EraseString(SavedDuringPlayKey);

            bool snapshotValid = SessionState.GetBool(SnapshotValidKey, false);
            SessionState.EraseBool(SnapshotValidKey);

            var snapshotRoot = SnapshotRoot;

            // Fast path — the overwhelmingly common case: Unity saved nothing under
            // _SO_Assets during play, so there is nothing to restore, whatever git
            // or any other external tool did to the working tree in the meantime.
            if (!snapshotValid || trackedRaw.Length == 0)
            {
                TryDeleteSnapshot(snapshotRoot);
                return;
            }

            var indexPath = Path.Combine(snapshotRoot, IndexFileName);
            if (!File.Exists(indexPath))
            {
                TryDeleteSnapshot(snapshotRoot);
                return;
            }

            var tracked = new HashSet<string>(
                trackedRaw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
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

                    // Restore is opt-in per file: only paths Unity itself saved
                    // during play are candidates. Everything else is left exactly
                    // as the working tree has it.
                    if (!tracked.Contains(assetPath))
                        continue;

                    var snapshotPath = Path.Combine(snapshotRoot, relative);
                    if (!File.Exists(assetPath) || !File.Exists(snapshotPath))
                        continue;

                    // Saved but byte-stable (write time + length unchanged, or the
                    // re-serialization produced identical bytes): nothing leaked.
                    var info = new FileInfo(assetPath);
                    if (info.LastWriteTimeUtc.Ticks == ticks && info.Length == length)
                        continue;
                    if (FilesMatch(assetPath, snapshotPath))
                        continue;

                    File.Copy(snapshotPath, assetPath, true);
                    restoredPaths.Add(assetPath);
                }

                if (restoredPaths.Count == 0)
                    return;

                UnityEngine.Debug.Log(
                    $"[PlayModeSOProtector] Restored {restoredPaths.Count} _SO_Assets file(s) that " +
                    "Unity saved during play mode back to their pre-play content.");

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

    /// <summary>
    /// Feeds <see cref="PlayModeSOProtector.RecordPlayModeSaves"/>. Top-level on
    /// purpose: Unity discovers AssetModificationProcessor subclasses by type
    /// scanning, and a top-level class is the one shape every Unity version
    /// reliably finds.
    /// </summary>
    sealed class PlayModeSOSaveTracker : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && paths != null && paths.Length > 0)
                PlayModeSOProtector.RecordPlayModeSaves(paths);
            return paths;
        }
    }
}
#endif
