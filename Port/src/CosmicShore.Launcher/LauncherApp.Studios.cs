using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// STUDIOS: the Vessel Studio. Each card opens one vessel's studio page from Prisma's workspace in the
    /// browser, or starts an agent chat on it. The same pages open on a phone from the web link. Catalog:
    /// <see cref="StudioCatalog"/>; plan: Docs/Studios/VESSEL_STUDIO_PLAN.md.
    /// </summary>
    public sealed partial class LauncherApp
    {
        StudioCatalog? _studios;
        DateTime _studiosRead;

        static void IconStudio(ImDrawListPtr dl, Vector2 c, uint col)
        {
            // a small vessel inside its skimmer ring
            dl.AddCircle(c, 10, col, 24, 1.6f);
            dl.AddTriangleFilled(c + new Vector2(7, 0), c + new Vector2(-5, -5), c + new Vector2(-5, 5), col);
        }

        StudioCatalog Studios()
        {
            if (_studios == null || (DateTime.UtcNow - _studiosRead).TotalSeconds > 5)   // pick up a branch switch or an edited catalog
            {
                _studios = _ws.Exists ? StudioCatalog.Load(_ws.Dir) : new StudioCatalog { Error = "Prisma's workspace is not set up yet (PLAY page)." };
                _studiosRead = DateTime.UtcNow;
            }
            return _studios;
        }

        void DrawStudios(Vector2 a, Vector2 b)
        {
            var cat = Studios();
            PageHeader(a, "STUDIOS", "Vessel studios: pick a vessel, fly it, change it. OPEN IN PRISMA gives it its own window; PLAY IN ENGINE flies the game's own vessel");
            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 76));
            ImGui.BeginChild("##studios", new Vector2(b.X - a.X, b.Y - a.Y - 80));

            if (cat.Error != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Neon.Amber);
                ImGui.TextWrapped(cat.Error + " Switch the workspace on the GIT page to a branch that has the Vessel Studio (Docs/Studios/VesselStudio): vessel-studio, or claude/peaceful-rubin-hhw49n for the Stoat's latest studio and its engine run.");
                ImGui.PopStyleColor();
            }

            if (_ws.Exists && cat.Error == null)
            {
                if (SmallButton("OPEN HUB", 130, true)) OpenStudioWindow(StudioCatalog.PagePath(_ws.Dir, cat.Hub));
                Neon.Tooltip("The Vessel Studio hub: every studio, where each platform stands, and the studio agent.");
                ImGui.SameLine(0, 8);
                if (SmallButton("WEB LINK", 130, cat.Web != null)) OpenUrl(cat.Web!);
                Neon.Tooltip("The published studio on claude.ai. Open the same link on your phone, pick a studio and tap Play on phone.\n" + (cat.Web ?? "(none in the catalog)"));
                ImGui.SameLine(0, 8);
                if (SmallButton("FOLDER", 110, true)) OpenFolder(Path.Combine(_ws.Dir, StudioCatalog.RelativeDir));
                ImGui.Dummy(new Vector2(0, 10));
            }

            foreach (var s in cat.Studios)
            {
                ImGui.PushID(s.Id);
                var top = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                var dl = ImGui.GetWindowDrawList();
                float cardH = s.EngineMode != null ? 176 : 150;
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
                ImGui.SetCursorScreenPos(top + new Vector2(18, 100));
                if (SmallButton("OPEN IN PRISMA", 160, _ws.Exists)) OpenStudioWindow(StudioCatalog.PagePath(_ws.Dir, s.File));
                Neon.Tooltip("Opens " + StudioCatalog.RelativeDir + "/" + s.File + " from Prisma's workspace as its own window (no browser tabs),\n" +
                             "with Prisma's layout remembered. Uses Edge or Chrome's app mode; without either it opens in your browser.");
                ImGui.SameLine(0, 8);
                if (SmallButton("BROWSER", 100, _ws.Exists)) OpenUrl(StudioCatalog.PagePath(_ws.Dir, s.File));
                Neon.Tooltip("The same page in your default browser.");
                ImGui.SameLine(0, 8);
                if (s.EngineMode != null)
                {
                    if (SmallButton("PLAY IN ENGINE", 160, _ws.Exists && !_jobs.Busy)) _jobs.Play(StudioCatalog.EngineArgs(s));
                    Neon.Tooltip("Builds and starts the game in Prisma (as PLAY does), then opens the " + s.EngineMode + " card from the main menu\n" +
                                 "and presses Start: the game's own " + s.Name + ". Press Ready in the race. A new profile answers the first-run prompts first.");
                    ImGui.SameLine(0, 8);
                }
                if (SmallButton("AGENT", 100, true)) StudioAgent(s);
                Neon.Tooltip("Opens an agent chat on this studio: it reads the plan and the studio's rules, then asks what to change.");
                if (s.Docs != null)
                {
                    ImGui.SameLine(0, 8);
                    if (SmallButton("DOCS", 90, _ws.Exists)) OpenUrl(Path.Combine(_ws.Dir, s.Docs));
                    Neon.Tooltip(s.Docs);
                }
                if (s.EngineMode != null)
                {
                    ImGui.SetCursorScreenPos(top + new Vector2(18, 140));
                    ImGui.TextColored(Neon.Dim, "In engine: " + (s.EngineNote ?? s.EngineMode));
                }
                ImGui.SetCursorScreenPos(top + new Vector2(0, cardH + 12));
                ImGui.Dummy(new Vector2(w, 0));
                ImGui.PopID();
            }

            ImGui.Dummy(new Vector2(0, 8));
            ImGui.TextColored(Neon.Dim,
                "Phone: open the web link in the phone's browser (Android or iPhone). The Prisma player APK with a studio scene, where the\n" +
                "game's own vessel flies instead of the web copy, is next (BUILD page). Unity opens this page through FrogletTools > Vessels > Vessel Studio.");
            ImGui.EndChild();
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
