using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content
{
    /// <summary>A serialized object reference: <c>{fileID: N, guid: G, type: T}</c>.</summary>
    public readonly struct ObjRef : IEquatable<ObjRef>
    {
        public readonly long FileId;
        public readonly string Guid;
        public readonly int Type;

        public ObjRef(long fileId, string guid, int type) { FileId = fileId; Guid = guid; Type = type; }

        public bool IsNull => FileId == 0;
        /// <summary>True when the reference points inside the same file (no guid).</summary>
        public bool IsLocal => string.IsNullOrEmpty(Guid);

        public static ObjRef From(YNode node)
        {
            if (node is not YMap map) return default;
            return new ObjRef(map.Long("fileID"), map.Str("guid"), map.Int("type"));
        }

        public bool Equals(ObjRef o) => FileId == o.FileId && string.Equals(Guid ?? "", o.Guid ?? "", StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjRef o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(FileId, Guid ?? "");
        public override string ToString() => $"{{fileID: {FileId}, guid: {Guid}, type: {Type}}}";
    }

    /// <summary>A parsed Unity asset file: its documents indexed by fileID.</summary>
    public sealed class AssetFile
    {
        public readonly string Path;
        public readonly string Guid;
        public readonly List<UnityDocument> Documents;
        public readonly Dictionary<long, UnityDocument> ById = new();

        public AssetFile(string path, string guid, List<UnityDocument> documents)
        {
            Path = path;
            Guid = guid;
            Documents = documents;
            foreach (var d in documents) ById[d.FileId] = d;
        }

        public UnityDocument Get(long fileId) => ById.TryGetValue(fileId, out var d) ? d : null;
    }

    /// <summary>
    /// The port's view of the Unity project's <c>Assets/</c> tree: every <c>.meta</c> guid
    /// mapped to its asset path, plus lazily parsed and cached YAML asset files and
    /// importer settings. Read-only — the port never writes into the Unity project.
    /// </summary>
    public sealed class AssetDatabase
    {
        public readonly string ProjectRoot;
        public readonly string AssetsRoot;

        readonly Dictionary<string, string> _guidToPath = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> _pathToGuid = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, AssetFile> _files = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, YMap> _metas = new(StringComparer.Ordinal);

        public int AssetCount => _guidToPath.Count;

        public AssetDatabase(string projectRoot)
        {
            ProjectRoot = System.IO.Path.GetFullPath(projectRoot);
            AssetsRoot = System.IO.Path.Combine(ProjectRoot, "Assets");
            if (!Directory.Exists(AssetsRoot))
                throw new DirectoryNotFoundException($"No Assets/ folder under {ProjectRoot}");
            Scan(AssetsRoot);
            var packages = System.IO.Path.Combine(ProjectRoot, "Packages");
            if (Directory.Exists(packages)) Scan(packages);
        }

        void Scan(string root)
        {
            foreach (var meta in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string guid = ReadGuid(meta);
                if (guid == null) continue;
                string asset = meta.Substring(0, meta.Length - 5);
                _guidToPath[guid] = asset;
                _pathToGuid[asset] = guid;
            }
        }

        static string ReadGuid(string metaPath)
        {
            using var reader = new StreamReader(metaPath);
            for (int n = 0; n < 4; n++)
            {
                var line = reader.ReadLine();
                if (line == null) break;
                if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            }
            return null;
        }

        /// <summary>Walks up from <paramref name="start"/> to find the Unity project root (the folder holding Assets/ and ProjectSettings/).</summary>
        public static string FindProjectRoot(string start = null)
        {
            var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(System.IO.Path.Combine(dir.FullName, "Assets"))
                    && Directory.Exists(System.IO.Path.Combine(dir.FullName, "ProjectSettings")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            var env = Environment.GetEnvironmentVariable("COSMIC_SHORE_PROJECT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(System.IO.Path.Combine(env, "Assets"))) return env;
            return null;
        }

        public string PathOf(string guid)
            => guid != null && _guidToPath.TryGetValue(guid, out var p) ? p : null;

        public string GuidOf(string path)
        {
            var full = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(ProjectRoot, path);
            return _pathToGuid.TryGetValue(System.IO.Path.GetFullPath(full), out var g) ? g : null;
        }

        public string ProjectRelative(string fullPath)
            => System.IO.Path.GetRelativePath(ProjectRoot, fullPath).Replace('\\', '/');

        public IEnumerable<string> AllAssetPaths => _guidToPath.Values;

        /// <summary>Parsed YAML asset file for a guid (scene/prefab/asset/mat/controller…), cached.</summary>
        public AssetFile Load(string guid)
        {
            var path = PathOf(guid);
            if (path == null || !File.Exists(path)) return null;
            return _files.GetOrAdd(guid, g => new AssetFile(path, g, UnityYaml.ParseDocuments(File.ReadAllText(path))));
        }

        /// <summary>Parsed YAML asset file by project-relative or absolute path.</summary>
        public AssetFile LoadPath(string path)
        {
            var guid = GuidOf(path);
            if (guid != null) return Load(guid);
            var full = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(ProjectRoot, path);
            if (!File.Exists(full)) return null;
            return new AssetFile(full, null, UnityYaml.ParseDocuments(File.ReadAllText(full)));
        }

        /// <summary>The importer settings (.meta root map) for a guid, cached.</summary>
        public YMap Meta(string guid)
        {
            var path = PathOf(guid);
            if (path == null) return null;
            return _metas.GetOrAdd(guid, _ => UnityYaml.ParseSingle(File.ReadAllText(path + ".meta")));
        }

        /// <summary>Resolves a reference to the document it names, given the file it was written in.</summary>
        public UnityDocument Resolve(ObjRef r, AssetFile context)
        {
            if (r.IsNull) return null;
            var file = r.IsLocal ? context : Load(r.Guid);
            return file?.Get(r.FileId);
        }
    }
}
