using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio catalog, <c>Docs/Studios/VesselStudio/studios.json</c> in Prisma's workspace: which
    /// vessel studios exist, the page that opens each, and the web link that also works on a phone. Read
    /// only; the STUDIOS page lists it. A branch without the file simply has no studios.
    /// </summary>
    internal sealed class StudioCatalog
    {
        public const string RelativeDir = "Docs/Studios/VesselStudio";
        public const string FileName = "studios.json";

        /// <summary>
        /// The one Vessel Studio artifact and its live mirror, used when no catalog can be read yet (no workspace, no
        /// checkout): the web links are the one part of the page that needs no files, so they always work.
        /// Keep in step with <c>web</c> and <c>mirror</c> in studios.json (StudioCatalogTests holds them together).
        /// </summary>
        public const string DefaultWeb = "https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa";
        public const string DefaultMirror = "https://yskhan61.github.io/vessel-studio/";

        /// <param name="EngineMode">An arcade mode (a <c>GameModes</c> name) PLAY IN ENGINE opens in the Prisma player: the game's own vessel.</param>
        /// <param name="EngineNote">What that engine run is, in a line (it may differ from the studio's design).</param>
        public sealed record Studio(string Id, string Name, string File, string Kind, string Summary, string? Docs, string? EngineMode = null, string? EngineNote = null);

        public string? Web { get; init; }
        /// <summary>
        /// The live mirror: a plain https copy of the same build (<c>build_artifact.py</c>'s output, published to a static
        /// site), so the hub and each studio open by link in any browser and update in place when the mirror is republished.
        /// A mirror of the one artifact, never a second source (/vessel-studio D12). Null when the catalog has none or it is
        /// not an http(s) URL.
        /// </summary>
        public string? Mirror { get; init; }
        public string Hub { get; init; } = "index.html";
        public IReadOnlyList<Studio> Studios { get; init; } = Array.Empty<Studio>();
        public string? Error { get; init; }

        /// <summary>The catalog under <paramref name="workspaceDir"/>, or an empty one carrying the reason.</summary>
        public static StudioCatalog Load(string workspaceDir)
        {
            var path = Path.Combine(workspaceDir, RelativeDir, FileName);
            if (!System.IO.File.Exists(path)) return new StudioCatalog { Error = $"No {RelativeDir}/{FileName} on this branch." };
            try { return Parse(System.IO.File.ReadAllText(path)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new StudioCatalog { Error = "Could not read the catalog: " + e.Message }; }
        }

        /// <summary>Parses the catalog JSON. Entries missing an id, a name or a file are skipped, never fatal.</summary>
        public static StudioCatalog Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var root = doc.RootElement;
                var list = new List<Studio>();
                if (root.TryGetProperty("studios", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in arr.EnumerateArray())
                    {
                        string? id = Str(s, "id"), name = Str(s, "name"), file = Str(s, "file");
                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file)) continue;
                        if (file.Contains("..") || Path.IsPathRooted(file)) continue;   // a page inside the studio folder only
                        list.Add(new Studio(id, name, file, Str(s, "kind") ?? "", Str(s, "summary") ?? "", Str(s, "docs"), Str(s, "engineMode"), Str(s, "engineNote")));
                    }
                }
                return new StudioCatalog { Web = Str(root, "web"), Mirror = HttpUrl(Str(root, "mirror")), Hub = Str(root, "hub") ?? "index.html", Studios = list };
            }
            catch (JsonException e) { return new StudioCatalog { Error = "The catalog is not valid JSON: " + e.Message }; }
        }

        /// <summary>
        /// OPEN LIVE: <paramref name="file"/> on the live mirror (the hub is the mirror's root), or null without a mirror.
        /// The build keeps the folder's file names, so a page is the mirror plus its file.
        /// </summary>
        public string? MirrorUrl(string file)
        {
            if (Mirror == null) return null;
            var root = Mirror.EndsWith("/") ? Mirror : Mirror + "/";
            return file == Hub ? root : root + Uri.EscapeDataString(file);
        }

        /// <summary>The plan every studio follows: the hub's DOCS.</summary>
        public const string PlanDoc = "Docs/Studios/VESSEL_STUDIO_PLAN.md";

        /// <summary>
        /// One thing the VESSEL STUDIO page acts on: a vessel's studio, or the hub (every studio at once). The page is ONE
        /// picker over these and ONE action row for the picked one, never a row of buttons per vessel: every action exists
        /// once, and a vessel added to studios.json gets all of them with no page change.
        /// </summary>
        public sealed record Target(string Key, string Label, string File, Studio? Studio)
        {
            public bool IsHub => Studio == null;
            public string? Docs => IsHub ? PlanDoc : Studio!.Docs;
            public string? EngineMode => Studio?.EngineMode;
        }

        /// <summary>The picker's items: each vessel's studio in catalog order, then the hub.</summary>
        public IReadOnlyList<Target> Targets()
        {
            var list = new List<Target>();
            foreach (var s in Studios) list.Add(new Target(s.Id, s.Name.ToUpperInvariant(), s.File, s));
            list.Add(new Target("hub", "ALL STUDIOS", Hub, null));
            return list;
        }

        /// <summary>The picked target by key; an unknown key (a vessel removed from the catalog) falls back to the first.</summary>
        public Target Pick(string? key)
        {
            var all = Targets();
            foreach (var t in all) if (t.Key == key) return t;
            return all[0];
        }

        /// <summary>The artifact link to open: the catalog's, else <see cref="DefaultWeb"/>.</summary>
        public string WebLink => Web ?? DefaultWeb;

        /// <summary>A page on the live mirror: the catalog's mirror, else <see cref="DefaultMirror"/>.</summary>
        public string LiveUrl(string file) => MirrorUrl(file) ?? (file == Hub ? DefaultMirror : DefaultMirror + Uri.EscapeDataString(file));

        /// <summary>
        /// Where the studio pages are read from. The pages are plain files, so they need no build and no workspace:
        /// Amoebius's workspace when it carries the catalog, else the user's own checkout (the one Unity opened Amoebius
        /// from), else whichever of the two exists. Null when there is neither.
        /// </summary>
        public static string? PickRoot(string? workspaceDir, bool workspaceExists, string? cloneDir)
        {
            bool Has(string? d) => d != null && System.IO.File.Exists(Path.Combine(d, RelativeDir, FileName));
            if (workspaceExists && Has(workspaceDir)) return workspaceDir;
            if (Has(cloneDir)) return cloneDir;
            return workspaceExists ? workspaceDir : cloneDir;
        }

        static string? HttpUrl(string? s) =>
            s != null && Uri.TryCreate(s.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.AbsoluteUri : null;

        /// <summary>The local page for a studio file (or the hub), under the workspace.</summary>
        public static string PagePath(string workspaceDir, string file) => Path.Combine(workspaceDir, RelativeDir, file);

        /// <summary>
        /// OPEN IN PRISMA: the studio as its own window, with no browser tabs or address bar. Chromium-based browsers
        /// have an app mode (<c>--app=URL</c>); a profile of Prisma's own keeps that window separate from the user's
        /// browser and its layout remembered between opens. The page gets <c>#prisma</c>, so it says it runs in Prisma.
        /// The candidates are where Edge (always on Windows 10/11) and Chrome install; none found means the plain browser.
        /// </summary>
        public static IEnumerable<string> AppBrowserCandidates()
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("LOCALAPPDATA") })
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    yield return Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
                    yield return Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
                yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                yield return "/Applications/Chromium.app/Contents/MacOS/Chromium";
            }
            else
            {
                foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                    foreach (var exe in new[] { "microsoft-edge", "google-chrome", "chromium", "chromium-browser" })
                        if (dir.Length > 0) yield return Path.Combine(dir, exe);
            }
        }

        /// <summary>The app-mode arguments for <paramref name="pagePath"/>, with a window profile under <paramref name="profileDir"/>.</summary>
        public static List<string> AppWindowArgs(string pagePath, string profileDir) => new()
        {
            "--app=" + PageUri(pagePath),
            "--user-data-dir=" + profileDir,
            "--window-size=1600,960",
            "--no-first-run",
            "--no-default-browser-check",
        };

        /// <summary>The page as a file URI that tells it it is running in Prisma (<c>#prisma</c>).</summary>
        public static string PageUri(string pagePath) => new Uri(Path.GetFullPath(pagePath)).AbsoluteUri + "#prisma";

        /// <summary>The player arguments PLAY IN ENGINE adds: open the studio's arcade mode from the main menu.</summary>
        public static IReadOnlyList<string> EngineArgs(Studio s) =>
            string.IsNullOrWhiteSpace(s.EngineMode) ? Array.Empty<string>() : new[] { "--arcade", s.EngineMode! };

        /// <summary>The prompt that opens an agent chat on a target: one studio, or the Vessel Studio as a whole.</summary>
        public static string AgentPrompt(Target t) => t.Studio != null ? AgentPrompt(t.Studio) :
            $"Work on the Vessel Studio ({RelativeDir}): the hub and the rules every studio shares.\n" +
            $"Read {PlanDoc} and the /vessel-studio skill first (one artifact, the universal panel rules, the one look).\n" +
            "Ask me what to change, or propose the next most useful improvement, and plan it before editing.";

        /// <summary>The prompt that opens an agent chat on one studio.</summary>
        public static string AgentPrompt(Studio s) =>
            $"Work on the {s.Name} studio of the Vessel Studio ({RelativeDir}/{s.File}).\n" +
            "Read Docs/Studios/VESSEL_STUDIO_PLAN.md and Docs/Studios/VesselStudio/README.md first. A studio is a reader of the game's " +
            "shipped numbers, never their authority: every value it shows names the asset it came from" +
            (s.Docs != null ? $", and {s.Docs} describes the vessel" : "") + ".\n" +
            "Ask me what to change, or propose the next most useful improvement, and plan it before editing.";

        static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
