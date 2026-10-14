using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Amoebius's artifact library, <c>Docs/Artifacts/artifacts.json</c> in the workspace: every claude.ai artifact
    /// brought into the repo, where its files live and the page that opens it. The VESSEL STUDIO page lists it (the
    /// Vessel Studio first). A session adds or updates an entry with <c>Tools/Build/amoebius_artifacts.py</c> (the
    /// <c>/amoebius-artifact</c> skill); <see cref="ImportPage"/> is the same import for one downloaded page, so
    /// IMPORT FILE works without a session. Both write the same schema; the script's <c>check</c> is the gate.
    /// </summary>
    internal sealed class ArtifactLibrary
    {
        public const string RelativePath = "Docs/Artifacts/artifacts.json";
        public const string LibraryDir = "Docs/Artifacts";
        public const string VesselStudioId = "vessel-studio";

        public sealed record Entry(string Id, string Title, string Url, string Dir, string EntryPage, string Group, string Summary,
                                   string? Version, string? ImportedAt);

        public IReadOnlyList<Entry> Entries { get; init; } = Array.Empty<Entry>();
        public string? Error { get; init; }

        /// <summary>The Vessel Studio's own entry, when the library has one.</summary>
        public Entry? VesselStudio => Entries.FirstOrDefault(e => e.Id == VesselStudioId);

        /// <summary>Every other entry, grouped as the page shows them.</summary>
        public IEnumerable<IGrouping<string, Entry>> Others =>
            Entries.Where(e => e.Id != VesselStudioId).GroupBy(e => e.Group);

        static readonly Regex UrlRe = new(@"^https://claude\.ai/(artifact/[A-Za-z0-9]+|code/artifact/[0-9a-fA-F-]{36})$");
        static readonly Regex IdRe = new(@"^[a-z0-9][a-z0-9-]{0,62}$");

        public static bool IsArtifactUrl(string? url) => url != null && UrlRe.IsMatch(url.Trim());

        public static ArtifactLibrary Load(string workspaceDir)
        {
            var path = Path.Combine(workspaceDir, RelativePath);
            if (!File.Exists(path)) return new ArtifactLibrary { Error = $"No {RelativePath} on this branch." };
            try { return Parse(File.ReadAllText(path)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new ArtifactLibrary { Error = "Could not read the library: " + e.Message }; }
        }

        /// <summary>Entries missing a field, or whose folder or page would leave Docs/, are skipped, never fatal.</summary>
        public static ArtifactLibrary Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var list = new List<Entry>();
                if (doc.RootElement.TryGetProperty("artifacts", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in arr.EnumerateArray())
                    {
                        string? id = Str(a, "id"), title = Str(a, "title"), url = Str(a, "url"), dir = Str(a, "dir"), entry = Str(a, "entry");
                        if (id == null || title == null || url == null || dir == null || entry == null) continue;
                        if (!IdRe.IsMatch(id) || !IsArtifactUrl(url) || !SafeDir(dir) || !SafeName(entry)) continue;
                        list.Add(new Entry(id, title, url, dir, entry, Str(a, "group") ?? "Artifacts", Str(a, "summary") ?? "",
                                           Str(a, "version"), Str(a, "importedAt")));
                    }
                }
                return new ArtifactLibrary { Entries = list };
            }
            catch (JsonException e) { return new ArtifactLibrary { Error = "The library is not valid JSON: " + e.Message }; }
        }

        /// <summary>The entry's page under the workspace.</summary>
        public static string PagePath(string workspaceDir, Entry e) => Path.Combine(workspaceDir, e.Dir, e.EntryPage);

        /// <summary>
        /// IMPORT FILE: one downloaded artifact page into <c>Docs/Artifacts/&lt;id&gt;/index.html</c>, and its entry into
        /// the library (an entry with the same url is updated in place). Returns the entry id, or throws with the reason.
        /// A page that loads other files of its artifact needs the session import (<c>/amoebius-artifact</c>) instead.
        /// </summary>
        public static string ImportPage(string workspaceDir, string htmlPath, string url, string? title = null, string? today = null)
        {
            url = url.Trim();
            if (!IsArtifactUrl(url)) throw new ArgumentException("Not a claude.ai artifact link: " + url);
            var bytes = File.ReadAllBytes(htmlPath);
            var html = Encoding.UTF8.GetString(bytes);
            var catPath = Path.Combine(workspaceDir, RelativePath);
            var root = File.Exists(catPath) ? JsonNode.Parse(File.ReadAllText(catPath), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }) as JsonObject : null;
            root ??= new JsonObject { ["comment"] = "Amoebius's artifact library. Docs/Artifacts/README.md.", ["artifacts"] = new JsonArray() };
            if (root["artifacts"] is not JsonArray arts) root["artifacts"] = arts = new JsonArray();

            var entry = arts.OfType<JsonObject>().FirstOrDefault(a => (string?)a["url"] == url);
            if (entry == null)
            {
                string t = title ?? PageTitle(html) ?? Path.GetFileNameWithoutExtension(htmlPath);
                string id = Slug(t);
                if (arts.OfType<JsonObject>().Any(a => (string?)a["id"] == id)) id = Slug(t + "-" + url[(url.LastIndexOf('/') + 1)..]);
                entry = new JsonObject
                {
                    ["id"] = id, ["title"] = t, ["url"] = url, ["dir"] = LibraryDir + "/" + id, ["entry"] = "index.html",
                    ["group"] = "Artifacts", ["summary"] = "",
                };
                arts.Add(entry);
            }
            else if (title != null) entry["title"] = title;

            string dir = (string?)entry["dir"] ?? "", page = (string?)entry["entry"] ?? "index.html";
            if (!SafeDir(dir) || !SafeName(page)) throw new InvalidOperationException($"The library entry for {url} points outside Docs/ ({dir}/{page}).");
            var dest = Path.Combine(workspaceDir, dir, page);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, bytes);
            var files = entry["files"] as JsonObject ?? new JsonObject();
            files[page] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            entry["files"] = files;
            entry["importedAt"] = today ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
            File.WriteAllText(catPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            return (string)entry["id"]!;
        }

        /// <summary>The prompt that has the agent import (or refresh) an artifact with the /amoebius-artifact skill.</summary>
        public static string ImportPrompt(string url, string? id = null) =>
            $"/amoebius-artifact {url.Trim()}\n\n" +
            (id != null ? $"This updates the library entry '{id}' from the artifact's current version. " : "Add this artifact to Amoebius's artifact library. ") +
            "Follow .claude/skills/amoebius-artifact/SKILL.md: save every file of the artifact, run Tools/Build/amoebius_artifacts.py import, then check, " +
            "and show me the report. The VESSEL STUDIO page reads the workspace, so it lists the artifact at once; leave the change uncommitted " +
            "for me to commit and push on the GIT page.";

        internal static string Slug(string text)
        {
            var s = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (s.Length > 62) s = s[..62].TrimEnd('-');
            return s.Length == 0 ? "artifact" : s;
        }

        internal static string? PageTitle(string html)
        {
            var m = Regex.Match(html, "<title>([^<]{1,120})</title>", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        static bool SafeName(string name) =>
            name.Length > 0 && !name.StartsWith('/') && !name.Contains(':') && !name.Contains('\\') &&
            !name.Split('/').Any(p => p is "" or "." or "..");

        static bool SafeDir(string dir) => dir.StartsWith("Docs/", StringComparison.Ordinal) && SafeName(dir);

        static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
