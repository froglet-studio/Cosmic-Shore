using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CosmicShore.Froglet
{
    /// <summary>
    /// The Froglet Engine's own Project Settings - Player, Scenes in Build and Quality - kept in
    /// <c>Port/ProjectSettings/FrogletProject.json</c> so the engine never edits Unity's
    /// <c>ProjectSettings/</c>. Every field is an OVERRIDE: null (or an absent file) inherits what
    /// Unity authors, so a fresh branch behaves exactly like the Unity project.
    ///
    /// Compiled into the build tool, the player and the launcher (linked source, no dependencies).
    /// </summary>
    public sealed class FrogletProjectSettings
    {
        public const string RelativePath = "Port/ProjectSettings/FrogletProject.json";

        public PlayerSection Player { get; set; } = new();
        /// <summary>Null = Unity's EditorBuildSettings list. Otherwise the build list, in order (scene 0 boots).</summary>
        public List<SceneEntry>? Scenes { get; set; }
        public QualitySection Quality { get; set; } = new();

        public sealed class PlayerSection
        {
            public string? CompanyName { get; set; }
            public string? ProductName { get; set; }
            public string? Version { get; set; }
            public string? AndroidBundleId { get; set; }
            public string? IosBundleId { get; set; }
            public int? AndroidVersionCode { get; set; }
            public string? IosBuildNumber { get; set; }
            public string? DefaultResolution { get; set; }
            public bool? Fullscreen { get; set; }
        }

        public sealed class SceneEntry
        {
            public string Path { get; set; } = "";
            public string Guid { get; set; } = "";
            public bool Enabled { get; set; } = true;
        }

        public sealed class QualitySection
        {
            /// <summary>0, 2, 4 or 8. Unity's URP asset authors 4.</summary>
            public int? Msaa { get; set; }
            public float? RenderScale { get; set; }
            public int? Anisotropy { get; set; }
            public bool? VSync { get; set; }
            /// <summary>0 = unlimited (with vsync off).</summary>
            public int? TargetFps { get; set; }
        }

        static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string PathIn(string projectRoot) => System.IO.Path.Combine(projectRoot, RelativePath);

        public static FrogletProjectSettings Load(string projectRoot)
        {
            try
            {
                var p = PathIn(projectRoot);
                if (File.Exists(p)) return JsonSerializer.Deserialize<FrogletProjectSettings>(File.ReadAllText(p), Json) ?? new();
            }
            catch (Exception) { /* a broken file must not stop a build or the game: inherit Unity's settings */ }
            return new();
        }

        public void Save(string projectRoot)
        {
            var p = PathIn(projectRoot);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
            File.WriteAllText(p, JsonSerializer.Serialize(this, Json) + "\n");
        }

        // ------------------------------------------------------------ what Unity authors (the inherited values)

        /// <summary>Read-only view of Unity's ProjectSettings, used as the defaults every override falls back to.</summary>
        public sealed class UnityDefaults
        {
            readonly string _player = "", _build = "";

            public UnityDefaults(string projectRoot)
            {
                try { _player = File.ReadAllText(System.IO.Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset")); } catch { }
                try { _build = File.ReadAllText(System.IO.Path.Combine(projectRoot, "ProjectSettings", "EditorBuildSettings.asset")); } catch { }
            }

            string Setting(string key)
            {
                var m = Regex.Match(_player, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$");
                return m.Success ? m.Groups[1].Value : "";
            }

            string Block(string block, string key)
            {
                var b = Regex.Match(_player, block + @":\s*\n((?:\s+\w+:.*\n)*)");
                if (!b.Success) return "";
                var m = Regex.Match(b.Groups[1].Value, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(\S+)");
                return m.Success ? m.Groups[1].Value : "";
            }

            public string CompanyName => Setting("companyName") is { Length: > 0 } v ? v : "Froglet";
            public string ProductName => Setting("productName") is { Length: > 0 } v ? v : "Cosmic Shore";
            public string Version => Setting("bundleVersion") is { Length: > 0 } v ? v : "0.1.0";
            public string AndroidBundleId => Block("applicationIdentifier", "Android") is { Length: > 0 } v ? v : Fallback;
            public string IosBundleId => Block("applicationIdentifier", "iPhone") is { Length: > 0 } v ? v : Fallback;
            public int AndroidVersionCode => int.TryParse(Setting("AndroidBundleVersionCode"), out var v) && v > 0 ? v : 1;
            public string IosBuildNumber => Block("buildNumber", "iPhone") is { Length: > 0 } v ? v : "1";
            string Fallback => "com." + Regex.Replace(CompanyName, "[^A-Za-z0-9]", "").ToLowerInvariant() + "." + Regex.Replace(ProductName, "[^A-Za-z0-9]", "").ToLowerInvariant();

            public List<SceneEntry> Scenes()
            {
                var list = new List<SceneEntry>();
                foreach (Match m in Regex.Matches(_build, @"- enabled:\s*(\d+)\s*\n\s*path:\s*(.+?)\s*\n\s*guid:\s*([0-9a-f]{32})"))
                    list.Add(new SceneEntry { Enabled = m.Groups[1].Value == "1", Path = m.Groups[2].Value, Guid = m.Groups[3].Value });
                return list;
            }
        }

        // ------------------------------------------------------------ resolved values (override ?? Unity)

        public string CompanyName(UnityDefaults u) => Nz(Player.CompanyName) ?? u.CompanyName;
        public string ProductName(UnityDefaults u) => Nz(Player.ProductName) ?? u.ProductName;
        public string Version(UnityDefaults u) => Nz(Player.Version) ?? u.Version;
        public string AndroidBundleId(UnityDefaults u) => Nz(Player.AndroidBundleId) ?? u.AndroidBundleId;
        public string IosBundleId(UnityDefaults u) => Nz(Player.IosBundleId) ?? u.IosBundleId;
        public int AndroidVersionCode(UnityDefaults u) => Player.AndroidVersionCode is int v && v > 0 ? v : u.AndroidVersionCode;
        public string IosBuildNumber(UnityDefaults u) => Nz(Player.IosBuildNumber) ?? u.IosBuildNumber;
        public List<SceneEntry> BuildScenes(UnityDefaults u) => Scenes is { Count: > 0 } s ? s : u.Scenes();

        static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
