using System;
using System.Collections.Generic;
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
        /// Which checkout the studio is built from (<see cref="StudioCatalog.PickRoot"/>): started from Unity, the Unity
        /// checkout (its branch); else Amoebius's workspace, else the checkout Unity last opened Amoebius from.
        /// Opening a studio never waits for a workspace or an engine build (the studio build takes a second).
        /// </summary>
        string? StudioRoot => StudioCatalog.PickRoot(_ws.Dir, _ws.Exists, ClonePath, preferClone: !string.IsNullOrWhiteSpace(_args.ClonePathArg));

        // ---------------------------------------------------------------- the studio server (/vessel-studio D33)
        //
        // Every way into the Vessel Studio opens the BUILD (build_artifact.py's output, StudioBuild) served over
        // http://127.0.0.1 by StudioServer, which also plays the claude.ai viewer's backend: Sync, Ask, Requests and
        // Decisions work here. Never a raw repo page, never file://.

        StudioServer? _studioSrv;
        readonly object _studioSrvLock = new();
        volatile string? _studioMsg;
        volatile bool _studioBusy;
        /// <summary><c>--page studios:&lt;id|file|hub&gt;</c> (Unity's Vessel Studio home, OPEN STUDIO): open that studio once the page is up.</summary>
        string? _studioOpenAtStart;

        StudioServer StudioSrv()
        {
            lock (_studioSrvLock)
                return _studioSrv ??= new StudioServer(new StudioServer.Options(
                    Path.Combine(LauncherSettings.DataDir, "studio"),
                    _tools.Git ?? "git",
                    () => StudioRoot,
                    Ask: (prompt, onText, ct) => StudioAsk.Run(_chats.Cli.Cli ?? _chats.Cli.Detect(), prompt, StudioRoot ?? _ws.Dir,
                        _s.ClaudeModel, _s.AnthropicApiKey, onText, ct),
                    Log: line => _jobs.Log.Add(LogKind.Info, line))).Start();
        }

        /// <summary>Builds the studio from the checkout (if it changed) and opens <paramref name="file"/>: its own window, or the default browser.</summary>
        void OpenServedStudio(string file, bool window)
        {
            if (_studioBusy) return;
            _studioBusy = true;
            _studioMsg = "Building the Vessel Studio from the checkout...";
            Task.Run(() =>
            {
                try
                {
                    var srv = StudioSrv();
                    var info = srv.EnsureBuilt();
                    var url = srv.PageUrl(file);
                    if (window) OpenStudioWindow(url); else OpenUrl(url);
                    _studioMsg = $"Serving {info.Branch} @ {info.PathSha[..7]} \"{info.Subject}\" on 127.0.0.1:{srv.Port} (Sync, Ask and Decisions run in Amoebius).";
                }
                catch (Exception e) { _studioMsg = "Could not open the studio: " + e.Message; }
                finally { _studioBusy = false; }
            });
        }
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

        // ---------------------------------------------------------------- the page: one picker, one card, one action row
        //
        // Pick a vessel (or ALL STUDIOS), then act on it. Every action exists exactly once, in the same place and order
        // whatever is picked; one that does not apply stays visible, disabled, and says why. A vessel added to studios.json
        // gets every action with no change here. The artifact library below uses the same picker, card and action row.

        /// <summary>One button of an action row: its label, whether it applies now, what it does (or why not), and the action.</summary>
        readonly record struct PageAction(string Label, bool Enabled, string Tip, Action Run);

        string _studioPick = "", _artPick = "";

        void DrawStudios(Vector2 a, Vector2 b)
        {
            var cat = Studios();
            var lib = Library();
            var root = StudioRoot;
            PageHeader(a, "VESSEL STUDIO", "Pick a vessel, then open it, play it in the engine or work on it with the agent. Below, every artifact brought into Amoebius");
            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 76));
            ImGui.BeginChild("##studios", new Vector2(b.X - a.X, b.Y - a.Y - 80));

            if (cat.Error != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Neon.Amber);
                ImGui.TextWrapped(root == null ? cat.Error
                    : cat.Error + " This checkout's branch has no Vessel Studio: switch to Ys-bleeding-edge in Unity, or on the GIT page for Amoebius's workspace.");
                ImGui.PopStyleColor();
                ImGui.Dummy(new Vector2(0, 6));
            }

            var targets = cat.Targets();
            if (_studioOpenAtStart != null && cat.Error == null && root != null)
            {
                var want = cat.Find(_studioOpenAtStart);
                _studioPick = want.Key;
                _studioOpenAtStart = null;
                OpenServedStudio(want.File, window: true);
            }
            var pick = cat.Pick(_studioPick);
            Picker("studio", targets.Select(t => t.Label).ToArray(), targets.ToList().FindIndex(t => t.Key == pick.Key), i => _studioPick = targets[i].Key);
            ImGui.Dummy(new Vector2(0, 8));

            var s = pick.Studio;
            PickedCard(pick.IsHub ? "ALL STUDIOS" : s!.Name.ToUpperInvariant(),
                pick.IsHub ? "the hub" : s!.Kind,
                pick.IsHub ? "Every vessel's studio in one place, with where each platform stands and the studio agent." : s!.Summary,
                s?.EngineMode != null ? "In engine: " + (s.EngineNote ?? s.EngineMode) : null,
                StudioActions(cat, root, pick));

            // the quiet links: the published artifact, the folder, matching the artifact, and where the pages are read from
            ImGui.Dummy(new Vector2(0, 2));
            if (_studioMsg is { } sm) { ImGui.TextColored(sm.StartsWith("Could not", StringComparison.Ordinal) ? Neon.Amber : Neon.Dim, sm); ImGui.Dummy(new Vector2(0, 2)); }
            if (Link("claude.ai artifact", true, "The published Vessel Studio on claude.ai (its decisions and requests are shared with everyone who opens it). Phones open it in touch play.\n" + cat.WebLink))
                OpenUrl(cat.WebLink);
            ImGui.SameLine(0, 18);
            if (Link("live mirror", true, "The published build on a plain web page (GitHub Pages), updated in place when it is republished.\n" + cat.LiveUrl(cat.Hub)))
                OpenUrl(cat.LiveUrl(cat.Hub));
            ImGui.SameLine(0, 18);
            var folder = root != null ? Path.Combine(root, StudioCatalog.RelativeDir) : null;
            if (Link("folder", folder != null && Directory.Exists(folder), folder ?? "No checkout yet.")) OpenFolder(folder!);
            ImGui.SameLine(0, 18);
            var vs = lib.VesselStudio;
            if (Link("update from the artifact", vs != null && _ws.Exists, _ws.Exists
                    ? "Has the agent bring back anything the published Vessel Studio has that this branch does not (the /amoebius-artifact skill)."
                    : "Needs Amoebius's workspace: press START on PLAY once.")) ArtifactAgent(vs!.Url, vs.Id, vs.Title);
            if (root != null)
            {
                ImGui.SameLine(0, 18);
                ImGui.TextColored(Neon.Dim, (StudioFromClone ? "read from your checkout: " : "read from Amoebius's workspace: ") + root);
            }

            DrawArtifactLibrary(lib);
            ImGui.EndChild();
        }

        /// <summary>The actions for the picked studio (or the hub): the same five, in the same order, every time.</summary>
        List<PageAction> StudioActions(StudioCatalog cat, string? root, StudioCatalog.Target t)
        {
            var page = root != null ? StudioCatalog.PagePath(root, t.File) : null;
            bool local = page != null && File.Exists(page);
            var live = cat.LiveUrl(t.File);
            var docs = root != null && t.Docs != null ? Path.Combine(root, t.Docs) : null;
            const string Served = "The studio BUILT from this checkout's branch (the same files as the claude.ai artifact and the live mirror), served by Amoebius\n" +
                                  "on 127.0.0.1; Sync, Ask, Requests and Decisions run here, kept on this computer.";
            return new List<PageAction>
            {
                new("OPEN IN AMOEBIUS", local && !_studioBusy,
                    local ? "Its own window (Edge or Chrome app mode; your browser without either), layout remembered.\n" + Served + "\nFrom: " + root
                          : "The page is not in this checkout: switch to Ys-bleeding-edge.",
                    () => OpenServedStudio(t.File, window: true)),
                new("OPEN IN BROWSER", !_studioBusy,
                    local ? "The same, in your default browser.\n" + Served
                          : "No checkout with the studio: opens the live mirror (the published build) instead.\n" + live,
                    () => { if (local) OpenServedStudio(t.File, window: false); else OpenUrl(live); }),
                new("PLAY IN ENGINE", t.EngineMode != null && !_jobs.Busy,
                    t.EngineMode == null ? (t.IsHub ? "Pick a vessel to play its mode in the engine." : "This vessel has no game mode in the engine yet.")
                    : "Builds and starts the game in Amoebius, opens the " + t.EngineMode + " card and presses Start: the game's own vessel.",
                    () => _jobs.Play(StudioCatalog.EngineArgs(t.Studio!))),
                new("AGENT", true, "An agent chat on " + (t.IsHub ? "the Vessel Studio as a whole" : "this studio") + ": it reads the plan and the rules, then asks what to change.",
                    () => StudioAgent(t)),
                new("DOCS", docs != null && File.Exists(docs), docs ?? "No document for this one yet.",
                    () => OpenUrl(docs!)),
            };
        }

        /// <summary>A picker over a few names: the segmented control, or a dropdown once the names no longer fit in a row.</summary>
        void Picker(string id, string[] items, int current, Action<int> set)
        {
            if (items.Length == 0) return;
            ImGui.PushFont(Neon.Small);
            float need = items.Sum(i => ImGui.CalcTextSize(i).X + 36) + 4;
            ImGui.PopFont();
            if (need <= ImGui.GetContentRegionAvail().X) { Segmented(id, items, Math.Max(0, current), set, Neon.Cyan); return; }
            int c = Math.Max(0, current);
            ImGui.PushItemWidth(Math.Min(420, ImGui.GetContentRegionAvail().X));
            if (ImGui.Combo("##" + id, ref c, items, items.Length) && c != current) set(c);
            ImGui.PopItemWidth();
        }

        /// <summary>The picked item's card: its name and kind, what it is, one optional note, and its action row.</summary>
        static void PickedCard(string title, string kind, string summary, string? note, IReadOnlyList<PageAction> actions)
        {
            var top = ImGui.GetCursorScreenPos();
            float w = ImGui.GetContentRegionAvail().X;
            float sumH = ImGui.CalcTextSize(summary, false, w - 36).Y;
            float rowY = 52 + Math.Max(sumH, 20) + 14;
            float cardH = rowY + 38 + (note != null ? 36 : 0) + 16;
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(top, top + new Vector2(w, cardH), Neon.U(Neon.Panel), 10);
            dl.AddRect(top, top + new Vector2(w, cardH), Neon.U(Neon.Cyan, 0.35f), 10);
            ImGui.SetCursorScreenPos(top + new Vector2(18, 14));
            ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Cyan, title); ImGui.PopFont();
            ImGui.SameLine(0, 14);
            ImGui.TextColored(Neon.Dim, kind);
            ImGui.SetCursorScreenPos(top + new Vector2(18, 52));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - 36);
            ImGui.TextColored(Neon.Ink, summary);
            ImGui.PopTextWrapPos();
            ImGui.SetCursorScreenPos(top + new Vector2(18, rowY));
            ActionRow(actions, w - 36);
            if (note != null)
            {
                ImGui.SetCursorScreenPos(top + new Vector2(18, rowY + 48));
                ImGui.TextColored(Neon.Dim, note);
            }
            ImGui.SetCursorScreenPos(top + new Vector2(0, cardH + 10));
            ImGui.Dummy(new Vector2(w, 0));
        }

        /// <summary>The action buttons in one row (wrapping only when the window is narrow), each with its tooltip.</summary>
        static void ActionRow(IReadOnlyList<PageAction> actions, float width)
        {
            float x = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                var act = actions[i];
                ImGui.PushFont(Neon.Small);
                float bw = Math.Max(110, ImGui.CalcTextSize(act.Label).X + 44);
                ImGui.PopFont();
                if (i > 0)
                {
                    if (x + 8 + bw <= width) { ImGui.SameLine(0, 8); x += 8; }
                    else x = 0;
                }
                if (SmallButton(act.Label, bw, act.Enabled)) act.Run();
                Neon.Tooltip(act.Tip);
                x += bw;
            }
        }

        /// <summary>A quiet text link (for the page's secondary actions): cyan when it applies, dim when not, with its tooltip.</summary>
        static bool Link(string text, bool enabled, string tip)
        {
            ImGui.TextColored(enabled ? Neon.Cyan : Neon.Dim, text);
            bool hov = ImGui.IsItemHovered();
            if (hov && enabled)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var r0 = ImGui.GetItemRectMin(); var r1 = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddLine(new Vector2(r0.X, r1.Y), r1, Neon.U(Neon.Cyan, 0.8f));
            }
            Neon.Tooltip(tip);
            return enabled && ImGui.IsItemClicked();
        }

        /// <summary>
        /// ARTIFACTS: add one (an artifact link for the agent, or a downloaded page), then the same picker, card and action
        /// row as the studios. The page reads the workspace, so an import shows here at once; the GIT page commits it.
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
            var all = lib.Others.SelectMany(g => g).ToList();
            if (all.Count == 0) { ImGui.TextColored(Neon.Dim, _ws.Exists ? "No other artifacts yet: paste a link above." : "No other artifacts yet."); return; }
            ImGui.Dummy(new Vector2(0, 10));
            var e = all.FirstOrDefault(x => x.Id == _artPick) ?? all[0];
            Picker("artifact", all.Select(x => x.Title.ToUpperInvariant()).ToArray(), all.IndexOf(e), i => _artPick = all[i].Id);
            ImGui.Dummy(new Vector2(0, 8));
            var root = StudioRoot ?? _ws.Dir;
            var page = ArtifactLibrary.PagePath(root, e);
            bool here = File.Exists(page);
            PickedCard(e.Title.ToUpperInvariant(),
                e.Group + " - " + (e.Version != null ? "version " + e.Version + ", " : "") + "imported " + (e.ImportedAt ?? "?"),
                e.Summary.Length > 0 ? e.Summary : e.Dir + "/" + e.EntryPage, null,
                new List<PageAction>
                {
                    new("OPEN IN AMOEBIUS", here, here ? "Opens " + e.Dir + "/" + e.EntryPage + " as its own window." : "Not in this checkout.", () => OpenStudioWindow(StudioCatalog.PageUri(page))),
                    new("OPEN IN BROWSER", true, "The published artifact on claude.ai (shared data and asking Claude work there).\n" + e.Url, () => OpenUrl(e.Url)),
                    new("AGENT", _ws.Exists, _ws.Exists ? "Has the agent bring in the artifact's current version (the /amoebius-artifact skill)." : "Needs Amoebius's workspace: press START on PLAY once.",
                        () => ArtifactAgent(e.Url, e.Id, e.Title)),
                });
        }

        void ArtifactAgent(string url, string? id, string? title)
        {
            var chat = _chats.New(ClaudeChat.Scope.Game);
            chat.Title = "Artifact: " + (title ?? url.Trim());
            _page = Page.Chat;
            SendChat(ArtifactLibrary.ImportPrompt(url, id), ClaudeChat.Mode.Edit);
        }

        /// <summary>OPEN IN AMOEBIUS: <paramref name="url"/> in an app-mode window of Edge or Chrome, or the default browser when neither is installed.</summary>
        static void OpenStudioWindow(string url)
        {
            string profile = Path.Combine(LauncherSettings.DataDir, "studio-window");
            foreach (var exe in StudioCatalog.AppBrowserCandidates())
            {
                if (!File.Exists(exe)) continue;
                try
                {
                    var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
                    foreach (var arg in StudioCatalog.AppWindowArgs(url, profile)) psi.ArgumentList.Add(arg);
                    Process.Start(psi);
                    return;
                }
                catch (Exception) { /* try the next one */ }
            }
            OpenUrl(url);
        }

        void StudioAgent(StudioCatalog.Target t)
        {
            var chat = _chats.New(ClaudeChat.Scope.Game);
            chat.Title = t.IsHub ? "Vessel Studio" : "Studio: " + t.Studio!.Name;
            _page = Page.Chat;
            SendChat(StudioCatalog.AgentPrompt(t), ClaudeChat.Mode.Plan);
        }
    }
}
