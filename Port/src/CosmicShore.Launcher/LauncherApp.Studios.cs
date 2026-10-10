using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// VESSEL STUDIO (<see cref="Page.Studios"/>, still <c>--page studios</c>): the Vessel Studio artifact inside Amoebius,
    /// then every other claude.ai artifact brought into the repo. Each studio card opens one vessel's studio page from
    /// Amoebius's workspace, or starts an agent chat on it; the same pages open on a phone from the web link. Catalogs:
    /// <see cref="StudioCatalog"/> (the studios) and <see cref="ArtifactLibrary"/> (every artifact, Docs/Artifacts/README.md);
    /// plan: Docs/Studios/VESSEL_STUDIO_PLAN.md.
    /// </summary>
    public sealed partial class LauncherApp
    {
        StudioCatalog? _studios;
        DateTime _studiosRead;
        ArtifactLibrary? _artLib;
        DateTime _artRead;
        string _artUrl = "", _artFile = "";
        string? _artMsg;
        bool _artMsgOk;
        int _artScrollFrames;   // --page studios:artifacts opens scrolled to the library for a few frames (docs screenshots)

        static void IconStudio(ImDrawListPtr dl, Vector2 c, uint col)
        {
            // a small vessel inside its skimmer ring
            dl.AddCircle(c, 10, col, 24, 1.6f);
            dl.AddTriangleFilled(c + new Vector2(7, 0), c + new Vector2(-5, -5), c + new Vector2(-5, 5), col);
        }

        /// <summary>
        /// Where the studio and artifact pages are read from (<see cref="StudioCatalog.PickRoot"/>): Amoebius's workspace,
        /// else the checkout Unity opened Amoebius from. Opening a studio never waits for a workspace or a build.
        /// </summary>
        string? StudioRoot => StudioCatalog.PickRoot(_ws.Dir, _ws.Exists, ClonePath);
        bool StudioFromClone => StudioRoot is { } r && !(_ws.Exists && string.Equals(Path.GetFullPath(r), Path.GetFullPath(_ws.Dir), StringComparison.OrdinalIgnoreCase));

        const string NoCheckout = "No checkout to read the studios from yet: open Amoebius from Unity (FrogletTools > Amoebius > Launch Amoebius), " +
                                  "set your Cosmic Shore folder in OPTIONS, or press START on PLAY once. The web links work meanwhile.";

        StudioCatalog Studios()
        {
            if (_studios == null || (DateTime.UtcNow - _studiosRead).TotalSeconds > 5)   // pick up a branch switch or an edited catalog
            {
                _studios = StudioRoot is { } root ? StudioCatalog.Load(root) : new StudioCatalog { Error = NoCheckout };
                _studiosRead = DateTime.UtcNow;
            }
            return _studios;
        }

        ArtifactLibrary Library()
        {
            if (_artLib == null || (DateTime.UtcNow - _artRead).TotalSeconds > 5)   // an import by the agent shows within seconds
            {
                _artLib = StudioRoot is { } root ? ArtifactLibrary.Load(root) : new ArtifactLibrary { Error = NoCheckout };
                _artRead = DateTime.UtcNow;
            }
            return _artLib;
        }

        void DrawStudios(Vector2 a, Vector2 b)
        {
            var cat = Studios();
            var lib = Library();
            PageHeader(a, "VESSEL STUDIO", "Pick a vessel, fly it, change it; below it, every artifact brought into Amoebius. OPEN IN AMOEBIUS gives a page its own window");
            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 76));
            ImGui.BeginChild("##studios", new Vector2(b.X - a.X, b.Y - a.Y - 80));

            var root = StudioRoot;
            bool pages = root != null && cat.Error == null;

            // The web links need no files: they are always here, first (a fresh Amoebius has no workspace yet).
            if (SmallButton("OPEN HUB", 130, pages)) OpenStudioWindow(StudioCatalog.PagePath(root!, cat.Hub));
            Neon.Tooltip("The Vessel Studio hub in its own window: every studio, where each platform stands, and the studio agent.");
            ImGui.SameLine(0, 8);
            if (SmallButton("WEB LINK", 130, true)) OpenUrl(cat.WebLink);
            Neon.Tooltip("The published Vessel Studio artifact on claude.ai (Sync, Ask and shared decisions work there).\n" +
                         "Open the same link on your phone: the studio opens in touch play by itself.\n" + cat.WebLink);
            ImGui.SameLine(0, 8);
            if (SmallButton("OPEN LIVE IN BROWSER", 220, true)) OpenUrl(cat.LiveUrl(cat.Hub));
            Neon.Tooltip("The live mirror of the published studio: the same build on a plain web page, in your default browser.\n" +
                         "Opens by link anywhere (a phone too) and updates in place when it is republished.\n" + cat.LiveUrl(cat.Hub));
            ImGui.SameLine(0, 8);
            if (SmallButton("FOLDER", 110, root != null && Directory.Exists(Path.Combine(root, StudioCatalog.RelativeDir)))) OpenFolder(Path.Combine(root!, StudioCatalog.RelativeDir));
            Neon.Tooltip(root != null ? Path.Combine(root, StudioCatalog.RelativeDir) : "No checkout yet.");

            if (cat.Error != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Neon.Amber);
                ImGui.TextWrapped(root == null ? cat.Error
                    : cat.Error + " This checkout's branch has no Vessel Studio: switch to Ys-bleeding-edge (or vessel-studio) in Unity, or on the GIT page for Amoebius's workspace.");
                ImGui.PopStyleColor();
            }
            else if (StudioFromClone)
                ImGui.TextColored(Neon.Dim, "Reading the studios from your checkout: " + root + "  (Amoebius's own workspace is made the first time you press START on PLAY).");

            if (pages)
            {
                var vs = lib.VesselStudio;
                if (SmallButton("UPDATE FROM ARTIFACT", 220, vs != null && _ws.Exists)) ArtifactAgent(vs!.Url, vs.Id, vs.Title);
                Neon.Tooltip("Has the agent bring back anything the published Vessel Studio has that this branch does not\n" +
                             "(the /amoebius-artifact skill). The repo pages are the source, so usually it reports no changes.");
                if (vs != null)
                    ImGui.TextColored(Neon.Dim, vs.Version != null ? $"Last matched the published artifact: version {vs.Version}, {vs.ImportedAt}." : "Not yet matched against the published artifact.");
                ImGui.Dummy(new Vector2(0, 10));
            }

            foreach (var s in cat.Studios)
            {
                ImGui.PushID(s.Id);
                var top = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                var dl = ImGui.GetWindowDrawList();
                float sumH = ImGui.CalcTextSize(s.Summary, false, w - 36).Y;   // the buttons sit under the wrapped summary, however long
                float btnY = 52 + Math.Max(sumH, 34) + 14;
                float cardH = btnY + (s.EngineMode != null ? 76 : 50);
                dl.AddRectFilled(top, top + new Vector2(w, cardH), Neon.U(Neon.Panel), 10);
                dl.AddRect(top, top + new Vector2(w, cardH), Neon.U(Neon.Cyan, 0.35f), 10);
                ImGui.SetCursorScreenPos(top + new Vector2(18, 14));
                ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Cyan, s.Name.ToUpperInvariant()); ImGui.PopFont();
                ImGui.SameLine(0, 14);
                ImGui.TextColored(Neon.Dim, s.Kind);
                ImGui.SetCursorScreenPos(top + new Vector2(18, 52));
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - 36);   // a window-local x (the card's right edge less its padding), not a screen x
                ImGui.TextColored(Neon.Ink, s.Summary);
                ImGui.PopTextWrapPos();
                ImGui.SetCursorScreenPos(top + new Vector2(18, btnY));
                var pagePath = root != null ? StudioCatalog.PagePath(root, s.File) : null;
                bool hasPage = pagePath != null && File.Exists(pagePath);
                if (SmallButton("OPEN IN AMOEBIUS", 160, hasPage)) OpenStudioWindow(pagePath!);
                Neon.Tooltip("Opens " + StudioCatalog.RelativeDir + "/" + s.File + " as its own window (no browser tabs), with its layout remembered.\n" +
                             "Uses Edge or Chrome's app mode; without either it opens in your browser.\n" + (pagePath ?? "No checkout yet."));
                ImGui.SameLine(0, 8);
                if (SmallButton("BROWSER", 100, hasPage)) OpenUrl(pagePath!);
                Neon.Tooltip("The same page in your default browser.");
                ImGui.SameLine(0, 8);
                var live = cat.LiveUrl(s.File);
                if (SmallButton("OPEN LIVE IN BROWSER", 220, true)) OpenUrl(live);
                Neon.Tooltip("This studio on the live mirror, in your default browser: the published build, updated in place.\n" + live);
                ImGui.SameLine(0, 8);
                if (s.EngineMode != null)
                {
                    if (SmallButton("PLAY IN ENGINE", 160, !_jobs.Busy)) _jobs.Play(StudioCatalog.EngineArgs(s));
                    Neon.Tooltip("Builds and starts the game in Amoebius (as PLAY does), then opens the " + s.EngineMode + " card from the main menu\n" +
                                 "and presses Start: the game's own " + s.Name + ". Press Ready in the race. A new profile answers the first-run prompts first.");
                    ImGui.SameLine(0, 8);
                }
                if (SmallButton("AGENT", 100, true)) StudioAgent(s);
                Neon.Tooltip("Opens an agent chat on this studio: it reads the plan and the studio's rules, then asks what to change.");
                if (s.Docs != null)
                {
                    ImGui.SameLine(0, 8);
                    if (SmallButton("DOCS", 90, root != null)) OpenUrl(Path.Combine(root!, s.Docs));
                    Neon.Tooltip(s.Docs);
                }
                if (s.EngineMode != null)
                {
                    ImGui.SetCursorScreenPos(top + new Vector2(18, btnY + 48));
                    ImGui.TextColored(Neon.Dim, "In engine: " + (s.EngineNote ?? s.EngineMode));
                }
                ImGui.SetCursorScreenPos(top + new Vector2(0, cardH + 12));
                ImGui.Dummy(new Vector2(w, 0));
                ImGui.PopID();
            }

            ImGui.Dummy(new Vector2(0, 8));
            ImGui.TextColored(Neon.Dim,
                "Phone: open the web link in the phone's browser (Android or iPhone). The Amoebius player APK with a studio scene, where the\n" +
                "game's own vessel flies instead of the web copy, is next (BUILD page). Unity opens this page through FrogletTools > Vessels > Vessel Studio.");
            DrawArtifactLibrary(lib);
            ImGui.EndChild();
        }

        /// <summary>
        /// ARTIFACTS: add one (an artifact link for the agent, or a downloaded page), then a card per library entry.
        /// The page reads the workspace, so an import shows here at once; the GIT page commits it for everyone else.
        /// </summary>
        void DrawArtifactLibrary(ArtifactLibrary lib)
        {
            ImGui.Dummy(new Vector2(0, 18));
            if (_artScrollFrames > 0) { ImGui.SetScrollHereY(0); _artScrollFrames--; }   // --page studios:artifacts
            ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Cyan, "ARTIFACTS"); ImGui.PopFont();
            ImGui.TextColored(Neon.Dim, "Anything you build as a claude.ai artifact, here or in another session, comes into Amoebius by its link.");
            if (!_ws.Exists)
                ImGui.TextColored(Neon.Dim, "Adding an artifact writes into Amoebius's workspace: press START on PLAY once to make it. The list below reads your checkout meanwhile.");
            else DrawArtifactAdd();
            DrawArtifactList(lib);
        }

        /// <summary>The add row: an artifact link for the agent, or a downloaded page, written into Amoebius's workspace.</summary>
        void DrawArtifactAdd()
        {

            ImGui.Dummy(new Vector2(0, 6));
            ImGui.PushItemWidth(Math.Min(560, ImGui.GetContentRegionAvail().X - 260));
            ImGui.InputTextWithHint("##arturl", "https://claude.ai/artifact/...", ref _artUrl, 300);
            ImGui.PopItemWidth();
            bool urlOk = ArtifactLibrary.IsArtifactUrl(_artUrl);
            ImGui.SameLine(0, 8);
            if (SmallButton("ADD WITH AGENT", 180, urlOk)) { ArtifactAgent(_artUrl, null, null); _artUrl = ""; }
            Neon.Tooltip("Opens an agent chat that saves every file of the artifact into Docs/Artifacts/<name>/ and lists it here\n" +
                         "(the /amoebius-artifact skill; the agent needs claude.ai access). An artifact already here is updated in place.");

            ImGui.PushItemWidth(Math.Min(560, ImGui.GetContentRegionAvail().X - 260));
            ImGui.InputTextWithHint("##artfile", "or a downloaded page: C:\\...\\page.html", ref _artFile, 1024);
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            if (SmallButton("BROWSE", 90, OperatingSystem.IsWindows()))
                Task.Run(() => { var picked = FilePicker.Open("Pick a downloaded artifact page", "Web pages|*.html;*.htm"); if (picked != null) _artFile = picked; });
            ImGui.SameLine(0, 8);
            bool fileOk = urlOk && File.Exists(_artFile.Trim().Trim('"'));
            if (SmallButton("IMPORT FILE", 130, fileOk))
            {
                try
                {
                    var id = ArtifactLibrary.ImportPage(_ws.Dir, _artFile.Trim().Trim('"'), _artUrl);
                    _artMsg = $"Imported into Docs/Artifacts/{id}. Commit it on the GIT page to share it.";
                    _artMsgOk = true; _artFile = ""; _artUrl = ""; _artLib = null;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
                {
                    _artMsg = "Not imported: " + e.Message; _artMsgOk = false;
                }
            }
            Neon.Tooltip("One page saved from the artifact, with its link above, without a session. A page that loads other files\n" +
                         "of its artifact (scripts, data) needs ADD WITH AGENT, which brings every file.");
            if (_artMsg != null) ImGui.TextColored(_artMsgOk ? Neon.Lime : Neon.Amber, _artMsg);
        }

        void DrawArtifactList(ArtifactLibrary lib)
        {
            if (lib.Error != null) { ImGui.TextColored(Neon.Dim, lib.Error + " The first import creates it."); return; }
            foreach (var group in lib.Others)
            {
                ImGui.Dummy(new Vector2(0, 10));
                ImGui.TextColored(Neon.Dim, group.Key.ToUpperInvariant());
                foreach (var e in group) DrawArtifactCard(e);
            }
            if (!lib.Others.Any()) ImGui.TextColored(Neon.Dim, "No other artifacts yet: paste a link above.");
        }

        void DrawArtifactCard(ArtifactLibrary.Entry e)
        {
            ImGui.PushID("art" + e.Id);
            var top = ImGui.GetCursorScreenPos();
            string blurb = e.Summary.Length > 0 ? e.Summary : e.Dir + "/" + e.EntryPage;
            float w = ImGui.GetContentRegionAvail().X, btnY = 46 + Math.Max(ImGui.CalcTextSize(blurb, false, w - 36).Y, 20) + 14, cardH = btnY + 52;
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(top, top + new Vector2(w, cardH), Neon.U(Neon.Panel), 10);
            dl.AddRect(top, top + new Vector2(w, cardH), Neon.U(Neon.Cyan, 0.2f), 10);
            ImGui.SetCursorScreenPos(top + new Vector2(18, 12));
            ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Ink, e.Title.ToUpperInvariant()); ImGui.PopFont();
            ImGui.SameLine(0, 14);
            ImGui.TextColored(Neon.Dim, (e.Version != null ? "version " + e.Version + ", " : "") + "imported " + (e.ImportedAt ?? "?"));
            ImGui.SetCursorScreenPos(top + new Vector2(18, 46));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - 36);
            ImGui.TextColored(Neon.Ink, blurb);
            ImGui.PopTextWrapPos();
            ImGui.SetCursorScreenPos(top + new Vector2(18, btnY));
            var root = StudioRoot ?? _ws.Dir;
            var page = ArtifactLibrary.PagePath(root, e);
            bool here = File.Exists(page);
            if (SmallButton("OPEN IN AMOEBIUS", 160, here)) OpenStudioWindow(page);
            Neon.Tooltip("Opens " + e.Dir + "/" + e.EntryPage + " as its own window.\n" +
                         "Features that need the claude.ai viewer (shared data, asking Claude) work on the web link.");
            ImGui.SameLine(0, 8);
            if (SmallButton("BROWSER", 100, here)) OpenUrl(page);
            ImGui.SameLine(0, 8);
            if (SmallButton("WEB LINK", 110, true)) OpenUrl(e.Url);
            Neon.Tooltip(e.Url);
            ImGui.SameLine(0, 8);
            if (SmallButton("UPDATE", 100, _ws.Exists)) ArtifactAgent(e.Url, e.Id, e.Title);
            Neon.Tooltip("Has the agent bring in the artifact's current version (the /amoebius-artifact skill).");
            ImGui.SameLine(0, 8);
            if (SmallButton("FOLDER", 100, Directory.Exists(Path.Combine(root, e.Dir)))) OpenFolder(Path.Combine(root, e.Dir));
            ImGui.SetCursorScreenPos(top + new Vector2(0, cardH + 10));
            ImGui.Dummy(new Vector2(w, 0));
            ImGui.PopID();
        }

        void ArtifactAgent(string url, string? id, string? title)
        {
            var chat = _chats.New(ClaudeChat.Scope.Game);
            chat.Title = "Artifact: " + (title ?? url.Trim());
            _page = Page.Chat;
            SendChat(ArtifactLibrary.ImportPrompt(url, id), ClaudeChat.Mode.Edit);
        }

        /// <summary>OPEN IN PRISMA: the page in an app-mode window of Edge or Chrome, or the default browser when neither is installed.</summary>
        static void OpenStudioWindow(string pagePath)
        {
            string profile = Path.Combine(LauncherSettings.DataDir, "studio-window");
            foreach (var exe in StudioCatalog.AppBrowserCandidates())
            {
                if (!File.Exists(exe)) continue;
                try
                {
                    var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
                    foreach (var arg in StudioCatalog.AppWindowArgs(pagePath, profile)) psi.ArgumentList.Add(arg);
                    Process.Start(psi);
                    return;
                }
                catch (Exception) { /* try the next one */ }
            }
            OpenUrl(pagePath);
        }

        void StudioAgent(StudioCatalog.Studio s)
        {
            var chat = _chats.New(ClaudeChat.Scope.Game);
            chat.Title = "Studio: " + s.Name;
            _page = Page.Chat;
            SendChat(StudioCatalog.AgentPrompt(s), ClaudeChat.Mode.Plan);
        }
    }
}
