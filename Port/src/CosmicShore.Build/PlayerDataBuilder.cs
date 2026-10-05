using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CosmicShore.Build
{
    /// <summary>
    /// The player data of a build: the part of the Unity project that ships, laid out as a
    /// project (Assets/, ProjectSettings/) so the port's content runtime reads it exactly as it
    /// reads the editable project. What ships is decided by Unity's own inclusion rules:
    ///
    ///   ROOTS  every ENABLED scene in EditorBuildSettings, every asset under a Resources/ folder
    ///          that is not inside an Editor/ folder, and ProjectSettings' preloadedAssets.
    ///   EDGES  every guid an asset (or its .meta — importer remaps count) references, followed
    ///          transitively. Nothing under an Editor/ folder ever ships.
    ///
    /// Two things differ from copying files, both because a Unity player differs from a project:
    /// SOURCE does not ship (a script's identity — namespace + class — is read at build time into
    /// <c>ScriptTypes.tsv</c>), and the FMOD header version is recorded in the manifest rather
    /// than read from the integration's source at run time.
    /// </summary>
    public sealed class PlayerDataBuilder
    {
        public sealed class Report
        {
            public string OutputDirectory = "";
            public int Roots, Scenes, ResourcesAssets, Preloaded;
            public int Assets, Scripts, LfsPointers;
            public long Bytes;
            public List<string> Missing = new();
            public List<string> LfsPointerFiles = new();
            public List<string> BuildScenes = new();
            public string ContentHash = "";
            public uint FmodHeaderVersion;
            public int Banks;
        }

        static readonly Regex GuidRx = new("[0-9a-f]{32}", RegexOptions.Compiled);
        static readonly Regex NamespaceRx = new(@"^\s*namespace\s+([\w\.]+)", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>Assets whose text Unity reads for references (everything else is binary or leaf data).</summary>
        static readonly HashSet<string> ParseExt = new(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".playable", ".shadergraph",
            ".shadersubgraph", ".anim", ".overridecontroller", ".spriteatlas", ".mixer", ".preset",
            ".vfx", ".terrainlayer", ".physicmaterial", ".fontsettings", ".guiskin", ".flare",
            ".cubemap", ".rendertexture", ".lighting", ".giparams", ".signal", ".inputactions",
            ".shader", ".compute", ".hlsl", ".cginc",
        };

        readonly string _project;
        readonly Dictionary<string, string> _pathOf = new(StringComparer.Ordinal);   // guid → full asset path

        public PlayerDataBuilder(string projectRoot)
        {
            _project = Path.GetFullPath(projectRoot);
            foreach (var meta in Directory.EnumerateFiles(Path.Combine(_project, "Assets"), "*.meta", SearchOption.AllDirectories))
            {
                var guid = OwnGuid(meta);
                if (guid != null) _pathOf[guid] = meta.Substring(0, meta.Length - 5);
            }
        }

        /// <summary>The Unity project above <paramref name="start"/> (a folder holding Assets/ and ProjectSettings/).</summary>
        public static string? FindProjectRoot(string? start = null)
        {
            var env = Environment.GetEnvironmentVariable("COSMIC_SHORE_PROJECT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "Assets"))) return env;
            for (var dir = new DirectoryInfo(start ?? Environment.CurrentDirectory); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                    return dir.FullName;
            return null;
        }

        static string? OwnGuid(string meta)
        {
            try
            {
                using var reader = new StreamReader(meta);
                for (int n = 0; n < 4; n++)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
                }
            }
            catch (IOException) { }
            return null;
        }

        static bool UnderEditorFolder(string relative)
            => relative.Replace('\\', '/').Split('/').Contains("Editor");

        string Rel(string full) => Path.GetRelativePath(_project, full).Replace('\\', '/');

        /// <summary>The enabled build scenes, in build order (scene 0 boots).</summary>
        public List<(string Path, string Guid)> EnabledScenes()
        {
            var text = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "EditorBuildSettings.asset"));
            return Regex.Matches(text, @"- enabled:\s*(\d+)\s*\n\s*path:\s*(.+?)\s*\n\s*guid:\s*([0-9a-f]{32})")
                .Where(m => m.Groups[1].Value == "1")
                .Select(m => (m.Groups[2].Value, m.Groups[3].Value))
                .ToList();
        }

        /// <summary>Every guid the build ships, from Unity's three roots.</summary>
        public HashSet<string> Reachable(Report report)
        {
            var roots = new HashSet<string>(StringComparer.Ordinal);
            var scenes = EnabledScenes();
            foreach (var (path, guid) in scenes) { roots.Add(guid); report.BuildScenes.Add(path); }
            report.Scenes = scenes.Count;

            foreach (var (guid, path) in _pathOf)
            {
                var rel = Rel(path);
                var parts = rel.Split('/');
                int res = Array.IndexOf(parts, "Resources");
                if (res < 0 || parts.Take(res + 1).Contains("Editor") || Directory.Exists(path)) continue;
                roots.Add(guid);
                report.ResourcesAssets++;
            }

            var settings = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "ProjectSettings.asset"));
            var pre = Regex.Match(settings, @"preloadedAssets:\s*\n((?:\s*-\s*\{.*\n)*)");
            if (pre.Success)
                foreach (Match g in Regex.Matches(pre.Groups[1].Value, @"guid:\s*([0-9a-f]{32})"))
                    if (roots.Add(g.Groups[1].Value)) report.Preloaded++;
            report.Roots = roots.Count;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(roots);
            while (queue.Count > 0)
            {
                var guid = queue.Dequeue();
                if (!seen.Add(guid)) continue;
                if (!_pathOf.TryGetValue(guid, out var path)) continue;   // a package or built-in asset
                if (UnderEditorFolder(Rel(path))) { seen.Remove(guid); continue; }
                foreach (var next in Outbound(path))
                    if (!seen.Contains(next)) queue.Enqueue(next);
            }
            seen.RemoveWhere(g => !_pathOf.ContainsKey(g));
            return seen;
        }

        static IEnumerable<string> Outbound(string asset)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in new[] { asset, asset + ".meta" })
            {
                if (!file.EndsWith(".meta", StringComparison.Ordinal) && !ParseExt.Contains(Path.GetExtension(file))) continue;
                if (!File.Exists(file)) continue;
                string text;
                try { text = File.ReadAllText(file); } catch (IOException) { continue; }
                foreach (Match m in GuidRx.Matches(text)) found.Add(m.Value);
            }
            return found;
        }

        /// <summary>A Git LFS pointer standing in for a file the clone never fetched.</summary>
        public static bool IsLfsPointer(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 1024) return false;
                using var reader = new StreamReader(path);
                return (reader.ReadLine() ?? "").StartsWith("version https://git-lfs.github.com/spec/", StringComparison.Ordinal);
            }
            catch (IOException) { return false; }
        }

        /// <summary>Writes the player data into <paramref name="outDir"/> (replacing what is there).</summary>
        public Report Build(string outDir, Action<string>? log = null)
        {
            log ??= _ => { };
            var report = new Report { OutputDirectory = Path.GetFullPath(outDir) };
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
            Directory.CreateDirectory(outDir);

            var reach = Reachable(report);
            log($"roots: {report.Scenes} build scenes, {report.ResourcesAssets} Resources assets, {report.Preloaded} preloaded → {reach.Count} assets reachable");

            var scripts = new StringBuilder();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var guid in reach.OrderBy(g => Rel(_pathOf[g]), StringComparer.Ordinal))
            {
                var path = _pathOf[guid];
                var rel = Rel(path);
                var dest = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(path + ".meta", dest + ".meta", overwrite: true);
                if (Directory.Exists(path)) continue;   // a folder asset: its meta is all it is
                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    // Source does not ship: record what the runtime pairs the guid with.
                    string? ns = null;
                    try { var m = NamespaceRx.Match(File.ReadAllText(path)); if (m.Success) ns = m.Groups[1].Value; } catch (IOException) { }
                    scripts.Append(guid).Append('\t').Append(ns ?? "").Append('\t').Append(Path.GetFileNameWithoutExtension(path)).Append('\n');
                    report.Scripts++;
                    continue;
                }
                if (!File.Exists(path)) { report.Missing.Add(rel); continue; }
                if (IsLfsPointer(path)) { report.LfsPointers++; report.LfsPointerFiles.Add(rel); }
                File.Copy(path, dest, overwrite: true);
                report.Assets++;
                report.Bytes += new FileInfo(path).Length;
                hash.AppendData(Encoding.UTF8.GetBytes(rel));
                hash.AppendData(BitConverter.GetBytes(new FileInfo(path).Length));
                hash.AppendData(BitConverter.GetBytes(File.GetLastWriteTimeUtc(path).Ticks));
            }
            File.WriteAllText(Path.Combine(outDir, "ScriptTypes.tsv"), scripts.ToString());
            hash.AppendData(Encoding.UTF8.GetBytes(scripts.ToString()));

            // ProjectSettings ships whole (the player reads build scenes, time, physics, graphics, tags).
            CopyTree(Path.Combine(_project, "ProjectSettings"), Path.Combine(outDir, "ProjectSettings"), hash);

            // FMOD: the banks the Studio project built (sourceBankPath) and the header version.
            report.FmodHeaderVersion = FmodHeaderVersion();
            var bankRel = FmodBankPath();
            var banks = Path.Combine(_project, bankRel, "Desktop");
            if (Directory.Exists(banks))
                foreach (var bank in Directory.EnumerateFiles(banks, "*.bank"))
                {
                    var dest = Path.Combine(outDir, bankRel, "Desktop", Path.GetFileName(bank));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(bank, dest, overwrite: true);
                    if (IsLfsPointer(bank)) { report.LfsPointers++; report.LfsPointerFiles.Add(Rel(bank)); }
                    report.Banks++;
                    report.Bytes += new FileInfo(bank).Length;
                    hash.AppendData(Encoding.UTF8.GetBytes(Rel(bank)));
                    hash.AppendData(BitConverter.GetBytes(new FileInfo(bank).Length));
                }
            else log($"warning: no FMOD banks at {bankRel}/Desktop — the build will be silent");

            report.ContentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant().Substring(0, 16);
            var manifest = new Dictionary<string, object>
            {
                ["product"] = ProductName(),
                ["version"] = BundleVersion(),
                ["contentHash"] = report.ContentHash,
                ["builtUtc"] = DateTime.UtcNow.ToString("o"),
                ["buildScenes"] = report.BuildScenes,
                ["assets"] = report.Assets,
                ["scripts"] = report.Scripts,
                ["bytes"] = report.Bytes,
                ["fmodHeaderVersion"] = report.FmodHeaderVersion,
                ["fmodBankPath"] = bankRel,
            };
            File.WriteAllText(Path.Combine(outDir, ManifestFile), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            log($"player data: {report.Assets} assets ({report.Bytes / (1024 * 1024)} MB), {report.Scripts} scripts, {report.Banks} FMOD banks → {report.OutputDirectory}");
            if (report.Missing.Count > 0) log($"warning: {report.Missing.Count} reachable asset(s) missing on disk (first: {report.Missing[0]})");
            if (report.LfsPointers > 0)
                log($"warning: {report.LfsPointers} file(s) are Git LFS pointers, not content (first: {report.LfsPointerFiles[0]}) — run `git lfs pull` before a release build");
            return report;
        }

        public const string ManifestFile = "PlayerData.json";

        static void CopyTree(string from, string to, IncrementalHash hash)
        {
            if (!Directory.Exists(from)) return;
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
                hash.AppendData(File.ReadAllBytes(file));
            }
        }

        uint FmodHeaderVersion()
        {
            var src = Path.Combine(_project, "Assets", "Plugins", "FMOD", "src", "fmod.cs");
            if (File.Exists(src))
            {
                var m = Regex.Match(File.ReadAllText(src), @"number\s*=\s*0x([0-9A-Fa-f]{8})");
                if (m.Success) return Convert.ToUInt32(m.Groups[1].Value, 16);
            }
            return 0x00020313;
        }

        string FmodBankPath()
        {
            var settings = Path.Combine(_project, "Assets", "Plugins", "FMOD", "Resources", "FMODStudioSettings.asset");
            if (File.Exists(settings))
            {
                var m = Regex.Match(File.ReadAllText(settings), @"(?m)^\s*sourceBankPath:\s*(.+?)\s*$");
                if (m.Success && m.Groups[1].Value.Length > 0) return m.Groups[1].Value;
            }
            return "Cosmic Shore/Build";
        }

        string Setting(string key)
        {
            var text = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "ProjectSettings.asset"));
            var m = Regex.Match(text, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$");
            return m.Success ? m.Groups[1].Value : "";
        }

        public string ProductName() => Setting("productName") is { Length: > 0 } n ? n : "Cosmic Shore";
        public string CompanyName() => Setting("companyName") is { Length: > 0 } n ? n : "Froglet";
        public string BundleVersion() => Setting("bundleVersion") is { Length: > 0 } v ? v : "0.1.0";

        /// <summary>The application id Player Settings authors for a platform (Unity's applicationIdentifier).</summary>
        public string ApplicationIdentifier(string platform)
        {
            var text = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "ProjectSettings.asset"));
            var block = Regex.Match(text, @"applicationIdentifier:\s*\n((?:\s+\w+:.*\n)*)");
            if (block.Success)
            {
                var m = Regex.Match(block.Groups[1].Value, @"(?m)^\s*" + Regex.Escape(platform) + @":\s*(\S+)");
                if (m.Success) return m.Groups[1].Value;
            }
            return "com." + Regex.Replace(CompanyName(), "[^A-Za-z0-9]", "").ToLowerInvariant() + "." + Regex.Replace(ProductName(), "[^A-Za-z0-9]", "").ToLowerInvariant();
        }

        /// <summary>The default icon Player Settings authors (m_BuildTargetIcons, the platform-less entry), as a file.</summary>
        public string? DefaultIcon()
        {
            var text = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "ProjectSettings.asset"));
            var m = Regex.Match(text, @"m_BuildTargetIcons:\s*\n\s*-\s*m_BuildTarget:\s*\n\s*m_Icons:\s*\n(?:.*\n){0,2}?\s*m_Icon:\s*\{[^}]*guid:\s*([0-9a-f]{32})");
            if (!m.Success || !_pathOf.TryGetValue(m.Groups[1].Value, out var path)) return null;
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && File.Exists(path) && !IsLfsPointer(path) ? path : null;
        }

        /// <summary>The iOS build number Player Settings authors (buildNumber.iPhone).</summary>
        public string IosBuildNumber()
        {
            var text = File.ReadAllText(Path.Combine(_project, "ProjectSettings", "ProjectSettings.asset"));
            var block = Regex.Match(text, @"buildNumber:\s*\n((?:\s+\w+:.*\n)*)");
            var m = block.Success ? Regex.Match(block.Groups[1].Value, @"(?m)^\s*iPhone:\s*(\S+)") : Match.Empty;
            return m.Success ? m.Groups[1].Value : "1";
        }

        /// <summary>The Android bundle version code Player Settings authors.</summary>
        public int AndroidVersionCode() => int.TryParse(Setting("AndroidBundleVersionCode"), out var v) && v > 0 ? v : 1;

        /// <summary>Packs player data into one archive the app unpacks on first launch (Unity's data.unity3d / obb role).</summary>
        public static void Pack(string dataDir, string archive)
        {
            if (File.Exists(archive)) File.Delete(archive);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archive))!);
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
            foreach (var file in Directory.EnumerateFiles(dataDir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(dataDir, file).Replace('\\', '/');
                var ext = Path.GetExtension(file).ToLowerInvariant();
                // Already-compressed media gains nothing from deflate; store it and keep unpacking fast.
                var level = ext is ".png" or ".jpg" or ".jpeg" or ".bank" or ".mp4" or ".ogg" or ".mp3" or ".wav" or ".ttf" or ".otf"
                    ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
                zip.CreateEntryFromFile(file, rel, level);
            }
        }
    }
}
