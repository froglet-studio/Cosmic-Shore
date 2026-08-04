using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Remembers which asset files each editor tool wrote, so its output can be committed
    /// deliberately instead of being noticed (or not) in a `git status` days later.
    ///
    /// The failure this exists to close: an agent writes a one-off editor tool, the human runs
    /// it, it rewrites a scene / prefab / SO, and the branch merges with the TOOL committed and
    /// its OUTPUT still sitting dirty in the working tree. The code lands, the data does not,
    /// and the feature is broken in a way no review would catch.
    ///
    /// Lives in <c>Library/</c> — machine-local, gitignored, and it survives editor restarts,
    /// which is exactly the window in which the forgetting happens. Entries are cleared only
    /// when their paths are actually committed.
    /// </summary>
    public static class FrogletToolChangeLedger
    {
        const string FileName = "FrogletToolChangeLedger.json";

        [Serializable]
        sealed class Entry
        {
            public string tool;

            /// <summary>Asset paths the tool created or modified.</summary>
            public List<string> paths = new();

            /// <summary>The tool's OWN source files, so a one-off can be retired from anywhere.</summary>
            public List<string> scripts = new();
        }

        [Serializable]
        sealed class Ledger
        {
            public List<Entry> entries = new();
        }

        static Ledger _cache;

        static string FilePath => Path.Combine(FrogletGit.RepoRoot, "Library", FileName);

        // ── Recording ────────────────────────────────────────────────────────────

        /// <summary>Record one asset path (or folder) a tool created or modified.</summary>
        public static void Record(string toolName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return;
            var normalized = Normalize(assetPath);
            if (normalized == null) return;

            var ledger = Load();
            var entry = FindOrCreate(ledger, toolName);
            if (!entry.paths.Contains(normalized))
            {
                entry.paths.Add(normalized);
                Save(ledger);
            }
        }

        public static void Record(string toolName, IEnumerable<string> assetPaths)
        {
            if (assetPaths == null || string.IsNullOrWhiteSpace(toolName)) return;

            var ledger = Load();
            var entry = FindOrCreate(ledger, toolName);
            var dirty = false;

            foreach (var p in assetPaths)
            {
                var normalized = Normalize(p);
                if (normalized == null || entry.paths.Contains(normalized)) continue;
                entry.paths.Add(normalized);
                dirty = true;
            }

            if (dirty) Save(ledger);
        }

        /// <summary>Record the asset a Unity object lives in. Silently ignores scene objects.</summary>
        public static void RecordObject(string toolName, UnityEngine.Object asset)
        {
            if (asset == null) return;
            Record(toolName, AssetDatabase.GetAssetPath(asset));
        }

        /// <summary>
        /// Record every currently-open scene. Call this from a tool that edits scene contents —
        /// the scene file is the output, and it is the one people forget most.
        /// </summary>
        public static void RecordOpenScenes(string toolName)
        {
            var count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (var i = 0; i < count; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                    Record(toolName, scene.path);
            }
        }

        /// <summary>
        /// Declare which source files ARE this tool, so it can be retired from the Pending Tool
        /// Changes window as well as from its own panel. Idempotent.
        /// </summary>
        public static void RegisterToolScripts(string toolName, IEnumerable<string> scriptPaths)
        {
            if (scriptPaths == null || string.IsNullOrWhiteSpace(toolName)) return;

            var ledger = Load();
            var entry = FindOrCreate(ledger, toolName);
            entry.scripts ??= new List<string>();

            var dirty = false;
            foreach (var p in scriptPaths)
            {
                var normalized = Normalize(p);
                if (normalized == null || entry.scripts.Contains(normalized)) continue;
                entry.scripts.Add(normalized);
                dirty = true;
            }

            if (dirty) Save(ledger);
        }

        public static IReadOnlyList<string> ScriptsFor(string toolName)
        {
            foreach (var e in Load().entries)
                if (string.Equals(e.tool, toolName, StringComparison.Ordinal))
                    return e.scripts ?? new List<string>();
            return Array.Empty<string>();
        }

        // ── Reading ──────────────────────────────────────────────────────────────

        public static IReadOnlyList<string> PathsFor(string toolName)
        {
            var ledger = Load();
            foreach (var e in ledger.entries)
                if (string.Equals(e.tool, toolName, StringComparison.Ordinal))
                    return e.paths;
            return Array.Empty<string>();
        }

        public static IReadOnlyList<string> Tools()
        {
            var ledger = Load();
            var names = new List<string>(ledger.entries.Count);
            foreach (var e in ledger.entries)
                if (e.paths.Count > 0 || (e.scripts != null && e.scripts.Count > 0))
                    names.Add(e.tool);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static IReadOnlyList<string> AllPaths()
        {
            var all = new List<string>();
            foreach (var e in Load().entries)
                foreach (var p in e.paths)
                    if (!all.Contains(p))
                        all.Add(p);
            return all;
        }

        /// <summary>Which tool claims this path, or null.</summary>
        public static string OwnerOf(string assetPath)
        {
            var normalized = Normalize(assetPath);
            if (normalized == null) return null;

            foreach (var e in Load().entries)
                foreach (var p in e.paths)
                    if (string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
                        return e.tool;

            return null;
        }

        // ── Clearing ─────────────────────────────────────────────────────────────

        /// <summary>Drop a tool's whole record. Call after its paths are committed.</summary>
        public static void Forget(string toolName)
        {
            var ledger = Load();
            var removed = ledger.entries.RemoveAll(e =>
                string.Equals(e.tool, toolName, StringComparison.Ordinal)) > 0;
            if (removed) Save(ledger);
        }

        /// <summary>Drop specific paths from a tool's record, keeping any that remain uncommitted.</summary>
        public static void Forget(string toolName, IEnumerable<string> assetPaths)
        {
            if (assetPaths == null) return;

            var ledger = Load();
            Entry entry = null;
            foreach (var e in ledger.entries)
                if (string.Equals(e.tool, toolName, StringComparison.Ordinal))
                    entry = e;
            if (entry == null) return;

            var dirty = false;
            foreach (var p in assetPaths)
            {
                var normalized = Normalize(p);
                if (normalized != null && entry.paths.Remove(normalized)) dirty = true;
            }

            // Keep the entry alive while it still names the tool's own scripts — that is what
            // makes "retire this tool" reachable after its output is committed.
            if (entry.paths.Count == 0 && (entry.scripts == null || entry.scripts.Count == 0))
                ledger.entries.Remove(entry);
            if (dirty) Save(ledger);
        }

        public static void ForgetAll()
        {
            _cache = new Ledger();
            Save(_cache);
        }

        // ── Storage ──────────────────────────────────────────────────────────────

        static Entry FindOrCreate(Ledger ledger, string toolName)
        {
            foreach (var e in ledger.entries)
                if (string.Equals(e.tool, toolName, StringComparison.Ordinal))
                    return e;

            var created = new Entry { tool = toolName };
            ledger.entries.Add(created);
            return created;
        }

        /// <summary>Repo-relative, forward slashes, inside the project. Null if it is neither.</summary>
        static string Normalize(string path)
        {
            var p = FrogletGit.ToRepoRelative(path);
            if (p == null) return null;

            // Only project data. A tool has no business recording anything else.
            if (!FrogletToolShipPanel.IsProjectPath(p)) return null;

            // The .meta sibling is resolved at commit time, not recorded separately.
            if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(0, p.Length - 5);

            return p.Length == 0 ? null : p;
        }

        static Ledger Load()
        {
            if (_cache != null) return _cache;

            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _cache = JsonUtility.FromJson<Ledger>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FrogletToolChangeLedger] Could not read {FilePath}: {ex.Message}");
            }

            _cache ??= new Ledger();
            _cache.entries ??= new List<Entry>();
            return _cache;
        }

        static void Save(Ledger ledger)
        {
            _cache = ledger;
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(ledger, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FrogletToolChangeLedger] Could not write {FilePath}: {ex.Message}");
            }
        }
    }
}
