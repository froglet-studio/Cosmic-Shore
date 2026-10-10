using System.Linq;
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
        public sealed record Studio(string Id, string Name, string File, string Kind, string Summary, string? Docs, string? EngineMode = null, string? EngineNote = null)
        {
            /// <summary>The hub card's chip (<c>chip</c>, the hub's text exactly; /vessel-studio D34). Falls back to <see cref="Kind"/>.</summary>
            public string Chip { get; init; } = "";
            /// <summary>The rows under the summary on the hub card (<c>spec</c>: key, value).</summary>
            public IReadOnlyList<SpecRow> Spec { get; init; } = Array.Empty<SpecRow>();
            /// <summary>The card's preview thumbnail, relative to the studio folder (<c>preview</c>, baked from the hub's canvas).</summary>
            public string? Preview { get; init; }
            /// <summary>The card colour: a domain key (studio-domains.js) or a #hex.</summary>
            public string? Accent { get; init; }
            /// <summary>Unity has a TUNE IN UNITY page for it (<c>tuner</c>).</summary>
            public bool Tuner { get; init; }
        }

        public sealed record SpecRow(string Key, string Value);

        /// <summary>
        /// One button of the studio card (<c>cardActions</c>, D34): the same buttons in the same order on every native card.
        /// <paramref name="On"/> is the hosts it works on (web, amoebius, unity); <paramref name="Needs"/> what the studio
        /// must have (engineMode, tuner, mirror).
        /// </summary>
        public sealed record CardAction(string Id, string Label, IReadOnlySet<string> On, string? Needs, string? Tip);

        /// <summary>The card's buttons when the catalog predates D34: the hub's open link and the native ones, in the D34 order.</summary>
        public static readonly IReadOnlyList<CardAction> DefaultCardActions = new[]
        {
            new CardAction("open", "Open studio →", new HashSet<string> { "web", "amoebius", "unity" }, null, null),
            new CardAction("engine", "PLAY IN ENGINE", new HashSet<string> { "amoebius", "unity" }, "engineMode", null),
            new CardAction("tune", "TUNE IN UNITY", new HashSet<string> { "unity" }, "tuner", null),
            new CardAction("live", "OPEN LIVE IN BROWSER", new HashSet<string> { "amoebius", "unity" }, "mirror", null),
        };

        /// <summary>The hub's lede, the vessels without a studio, the line under them, and the card's buttons.</summary>
        public string? Lede { get; init; }
        public IReadOnlyList<string> Fleet { get; init; } = Array.Empty<string>();
        public string? FleetNote { get; init; }
        public IReadOnlyList<CardAction> CardActions { get; init; } = DefaultCardActions;

        /// <summary>
        /// Whether card button <paramref name="a"/> works for <paramref name="s"/> on <paramref name="host"/>, and if not, why:
        /// a button that does not apply stays in its place, disabled, saying so (D34). Pure: the same answer on every host.
        /// </summary>
        public (bool ok, string? why) Applies(CardAction a, Studio s, string host)
        {
            if (!a.On.Contains(host))
                return (false, a.Id == "tune" ? "In Unity: FrogletTools > Vessels > Vessel Studio, this card." : "Not on " + host + ".");
            return a.Needs switch
            {
                "engineMode" when string.IsNullOrEmpty(s.EngineMode) => (false, "This vessel has no game mode in the engine yet."),
                "tuner" when !s.Tuner => (false, "No Unity tuning page for this vessel yet."),
                "mirror" when Mirror == null => (false, "No live mirror in the catalog."),
                _ => (true, null),
            };
        }

        /// <summary>A studio's preview thumbnail under <paramref name="root"/>, or null when it has none on this branch.</summary>
        public static string? PreviewPath(string root, Studio s)
        {
            if (string.IsNullOrEmpty(s.Preview) || s.Preview.Contains("..") || Path.IsPathRooted(s.Preview)) return null;
            var p = Path.Combine(root, RelativeDir, s.Preview);
            return System.IO.File.Exists(p) ? p : null;
        }

        /// <summary>The game's domain colours by key (<c>studio-domains.js</c>, generated from the palette): #rrggbb.</summary>
        public static Dictionary<string, string> ParseDomains(string js)
        {
            var d = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(js, @"key:\s*'(\w+)'[^}]*?color:\s*'(#[0-9a-fA-F]{6})'"))
                d[m.Groups[1].Value] = m.Groups[2].Value;
            return d;
        }

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
                        var spec = new List<SpecRow>();
                        if (s.TryGetProperty("spec", out var sp) && sp.ValueKind == JsonValueKind.Array)
                            foreach (var r in sp.EnumerateArray())
                                if (Str(r, "k") is { } k && Str(r, "v") is { } v) spec.Add(new SpecRow(k, v));
                        list.Add(new Studio(id, name, file, Str(s, "kind") ?? "", Str(s, "summary") ?? "", Str(s, "docs"), Str(s, "engineMode"), Str(s, "engineNote"))
                        {
                            Chip = Str(s, "chip") ?? Str(s, "kind") ?? "", Spec = spec, Preview = Str(s, "preview"), Accent = Str(s, "accent"),
                            Tuner = s.TryGetProperty("tuner", out var tu) && tu.ValueKind == JsonValueKind.True,
                        });
                    }
                }
                var actions = new List<CardAction>();
                if (root.TryGetProperty("cardActions", out var ca) && ca.ValueKind == JsonValueKind.Array)
                    foreach (var a in ca.EnumerateArray())
                        if (Str(a, "id") is { } aid && Str(a, "label") is { } lab)
                            actions.Add(new CardAction(aid, lab, new HashSet<string>((Str(a, "on") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                                Str(a, "needs"), Str(a, "tip")));
                var fleet = new List<string>();
                if (root.TryGetProperty("fleet", out var fl) && fl.ValueKind == JsonValueKind.Array)
                    foreach (var f in fl.EnumerateArray()) if (f.ValueKind == JsonValueKind.String) fleet.Add(f.GetString()!);
                return new StudioCatalog
                {
                    Web = Str(root, "web"), Mirror = HttpUrl(Str(root, "mirror")), Hub = Str(root, "hub") ?? "index.html", Studios = list,
                    Lede = Str(root, "lede"), Fleet = fleet, FleetNote = Str(root, "fleetNote"),
                    CardActions = actions.Count > 0 ? actions : DefaultCardActions,
                };
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

        /// <summary>
        /// The picker's items: ALL STUDIOS first (the hub: the whole Vessel Studio, exactly as the claude.ai artifact shows it),
        /// then each vessel's studio in catalog order.
        /// </summary>
        public IReadOnlyList<Target> Targets()
        {
            var list = new List<Target> { new Target("hub", "ALL STUDIOS", Hub, null) };
            foreach (var s in Studios) list.Add(new Target(s.Id, s.Name.ToUpperInvariant(), s.File, s));
            return list;
        }

        /// <summary>
        /// <c>--page studios:&lt;x&gt;</c>: the target whose key or page file is <paramref name="keyOrFile"/> ("hub", "stoat",
        /// "stoat.html", "index.html"), any case; anything else is the hub.
        /// </summary>
        public Target Find(string? keyOrFile)
        {
            var all = Targets();
            return all.FirstOrDefault(t => string.Equals(t.Key, keyOrFile, StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(t.File, keyOrFile, StringComparison.OrdinalIgnoreCase)) ?? all[0];
        }

        /// <summary>The picked target by key; none or an unknown key (a vessel removed from the catalog) falls back to ALL STUDIOS.</summary>
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
        /// Which checkout the studio is built from (/vessel-studio D33). Started from Unity (<paramref name="preferClone"/>:
        /// FrogletTools passes <c>--clone</c>), the Unity checkout, so the studio shows the branch Unity has open. Otherwise
        /// Amoebius's workspace when it carries the catalog, else the user's own checkout, else whichever of the two exists.
        /// Null when there is neither.
        /// </summary>
        public static string? PickRoot(string? workspaceDir, bool workspaceExists, string? cloneDir, bool preferClone = false)
        {
            bool Has(string? d) => d != null && System.IO.File.Exists(Path.Combine(d, RelativeDir, FileName));
            if (preferClone && Has(cloneDir)) return cloneDir;
            if (workspaceExists && Has(workspaceDir)) return workspaceDir;
            if (Has(cloneDir)) return cloneDir;
            return workspaceExists ? workspaceDir : cloneDir;
        }

        static string? HttpUrl(string? s) =>
            s != null && Uri.TryCreate(s.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.AbsoluteUri : null;

        /// <summary>The local page for a studio file (or the hub), under the workspace.</summary>
        public static string PagePath(string workspaceDir, string file) => Path.Combine(workspaceDir, RelativeDir, file);

        /// <summary>
        /// OPEN IN AMOEBIUS: the studio as its own window, with no browser tabs or address bar. Chromium-based browsers
        /// have an app mode (<c>--app=URL</c>); a profile of Amoebius's own keeps that window separate from the user's
        /// browser and its layout remembered between opens. The URL is the studio server's (<see cref="StudioServer.PageUrl"/>:
        /// the build over http, with <c>#amoebius</c>), never a file.
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

        /// <summary>
        /// The app-mode arguments for <paramref name="url"/>, with a window profile under <paramref name="profileDir"/>.
        /// <c>PRISMA_STUDIO_BROWSER_ARGS</c> adds flags (a GPU-less test machine: <c>--use-gl=swiftshader --enable-unsafe-swiftshader</c>).
        /// </summary>
        public static List<string> AppWindowArgs(string url, string profileDir)
        {
            var list = new List<string>
            {
                "--app=" + url,
                "--user-data-dir=" + profileDir,
                "--window-size=1600,960",
                "--no-first-run",
                "--no-default-browser-check",
            };
            var extra = Environment.GetEnvironmentVariable("PRISMA_STUDIO_BROWSER_ARGS");
            if (!string.IsNullOrWhiteSpace(extra)) list.AddRange(extra.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return list;
        }

        /// <summary>A page file as a file URI: only for the OTHER artifacts of the library; the Vessel Studio is always served (D33).</summary>
        public static string PageUri(string pagePath) => new Uri(Path.GetFullPath(pagePath)).AbsoluteUri;

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
