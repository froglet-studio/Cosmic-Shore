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

        /// <param name="EngineMode">An arcade mode (a <c>GameModes</c> name) PLAY IN ENGINE opens in the Prisma player: the game's own vessel.</param>
        /// <param name="EngineNote">What that engine run is, in a line (it may differ from the studio's design).</param>
        public sealed record Studio(string Id, string Name, string File, string Kind, string Summary, string? Docs, string? EngineMode = null, string? EngineNote = null);

        public string? Web { get; init; }
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
                return new StudioCatalog { Web = Str(root, "web"), Hub = Str(root, "hub") ?? "index.html", Studios = list };
            }
            catch (JsonException e) { return new StudioCatalog { Error = "The catalog is not valid JSON: " + e.Message }; }
        }

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
