using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The launcher in motion: the intro splash, pages sliding in, the rail's gliding selection,
    /// and the launcher-update flow (badge, the what's-new card, the install animation, and the
    /// "updated" splash after the restart). SETTINGS > LOOK > Animations turns the decorative part off.
    /// </summary>
    public sealed partial class LauncherApp
    {
        readonly LauncherUpdater _updater;
        float _railY = -1, _railTarget;
        Page _shownPage = (Page)(-1);
        float _pageT = 1;
        float _splashT = -1;
        bool _updateCard;
        float _installT, _doneT = -1;
        string _installRev = "";
        string[] _updatedNotes = Array.Empty<string>();

        static string ShortRev(string r) => System.Text.RegularExpressions.Regex.IsMatch(r, "^[0-9a-fA-F]{12,40}$") ? r[..7] : r;

        bool SplashDone => _splashT >= (_args.UpdatedFrom != null ? 4.5f : 1.7f);

        static float EaseOut(float t) => 1 - MathF.Pow(1 - Math.Clamp(t, 0, 1), 3);

        /// <summary>0..1 as the current page eases in after a switch.</summary>
        float PageEnter(float dt)
        {
            if (_page != _shownPage) { _shownPage = _page; _pageT = _s.Animations && _frame > 2 ? 0 : 1; }
            _pageT = Math.Min(1, _pageT + Math.Min(dt, 1f / 20) / 0.28f);
            return EaseOut(_pageT);
        }

        // ------------------------------------------------------------------ splash

        void DrawSplash(Vector2 size, float dt)
        {
            if (_splashT < 0)
            {
                bool want = _args.UpdatedFrom != null || (_s.Animations && _args.Screenshot == null && _args.Auto == null);
                _splashT = want ? 0 : 99;
                if (_args.UpdatedFrom != null)
                    Task.Run(() => _updatedNotes = _updater.ChangesBetween(_args.UpdatedFrom, LauncherUpdater.Short).ToArray());
            }
            float length = _args.UpdatedFrom != null ? 4.5f : 1.7f;
            if (_splashT >= length) return;
            _splashT += Math.Min(dt, 1f / 20);  // the first frame's dt includes startup; never skip the show
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _splashT > 0.3f) _splashT = MathF.Max(_splashT, length - 0.35f);

            var dl = ImGui.GetForegroundDrawList();
            float t = _splashT, outA = 1 - Math.Clamp((t - (length - 0.35f)) / 0.35f, 0, 1);
            var c = size * 0.5f;
            dl.AddRectFilled(Vector2.Zero, size, Neon.U(Neon.Space0, outA));

            // a warp burst: streaks fly out from the centre, fast then settling
            var rng = new Random(5);
            for (int i = 0; i < 140; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2), sp = 0.4f + (float)rng.NextDouble();
                float r0 = EaseOut(t * 0.7f * sp) * size.X * 0.7f, r1 = r0 * (0.82f - 0.3f * (float)rng.NextDouble());
                var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                var col = i % 5 == 0 ? Neon.Magenta : i % 3 == 0 ? Neon.Cyan : Neon.Ink;
                dl.AddLine(c + d * r1, c + d * r0, Neon.U(col, 0.6f * outA * (1 - Math.Clamp(t / length, 0, 1) * 0.6f)), 1.2f + sp);
            }
            // ring shockwave
            float ring = EaseOut(t / 0.9f);
            dl.AddCircle(c, 40 + ring * size.Y * 0.45f, Neon.U(Neon.Cyan, (1 - ring) * 0.8f * outA), 96, 3f);

            // the prism grows in and splits the light; the wordmark follows
            float logoIn = EaseOut(t / 0.7f);
            float ps = 150 * (0.8f + 0.2f * logoIn);
            Neon.PrismIcon(dl, c - new Vector2(30, 70), ps, logoIn * outA, t);
            float h = 0;
            var la = c + new Vector2(0, 40);
            ImGui.PushFont(Neon.Hero);
            var ws = ImGui.CalcTextSize("PRISMA");
            ImGui.PopFont();
            Neon.GlowText(dl, Neon.Hero, 46, new Vector2(c.X - ws.X * 0.5f, la.Y), Neon.Mix(Neon.Space0, Neon.Ink, logoIn * outA), "PRISMA", 0.8f * logoIn * outA);

            float textIn = Math.Clamp((t - 0.35f) / 0.4f, 0, 1) * outA;
            string line = _args.UpdatedFrom != null ? $"UPDATED   {_args.UpdatedFrom}  ->  {LauncherUpdater.Short}" : "FROGLET'S ENGINE FOR COSMIC SHORE";
            CenterText(dl, Neon.Small, 15, c.X, la.Y + 70, Neon.Mix(Neon.Space0, Neon.Cyan, textIn), line);
            if (_args.UpdatedFrom != null)
            {
                float y = la.Y + 104;
                foreach (var (note, i) in _updatedNotes.Take(6).Select((n, i) => (n, i)))
                {
                    float a = Math.Clamp((t - 0.7f - i * 0.12f) / 0.3f, 0, 1) * outA;
                    CenterText(dl, Neon.Small, 15, c.X, y + i * 22 - 6 * (1 - a), Neon.Mix(Neon.Space0, Neon.Dim, a), Trim(note, 90));
                }
            }
        }

        // ------------------------------------------------------------------ updates

        void DrawUpdateUi(Vector2 size, float dt)
        {
            var fg = ImGui.GetForegroundDrawList();
            if (_updater.Installing || _updater.Done) { DrawInstall(size, dt); return; }

            // the rail badge: pulses while a newer launcher waits on the branch
            if (_updater.Available != null)
            {
                var a = new Vector2(10, size.Y - 92); var b = new Vector2(RailW - 10, size.Y - 62);
                float pulse = 0.5f + 0.5f * MathF.Sin(Neon.Time * 3);
                Neon.ChamferFill(fg, a, b, 6, Neon.U(Neon.Lime, 0.12f + 0.10f * pulse));
                Neon.ChamferGlow(fg, a, b, 6, Neon.Lime, 0.4f + 0.6f * pulse);
                CenterText(fg, Neon.Small, 13, (a.X + b.X) * 0.5f, a.Y + 7, Neon.Lime, "UPDATE");
                if (ImGui.IsMouseHoveringRect(a, b))
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip($"A newer launcher is on {_s.Branch} ({_updater.Available.Date}). Click to see what changed.");
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _updateCard = true;
                }
            }
            if (_updateCard && _updater.Available != null) DrawUpdateCard(size);
        }

        void DrawUpdateCard(Vector2 size)
        {
            var v = _updater.Available!;
            var a = new Vector2(size.X * 0.5f - 300, size.Y * 0.5f - 200); var b = new Vector2(size.X * 0.5f + 300, size.Y * 0.5f + 200);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(size);
            ImGui.Begin("##updcard", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(Vector2.Zero, size, Neon.U(Neon.Space0, 0.7f));
            Neon.ChamferFill(dl, a, b, 14, Neon.U(Neon.Panel));
            Neon.ChamferGlow(dl, a, b, 14, Neon.Lime, 0.7f);
            dl.AddText(Neon.Heading, 24, a + new Vector2(28, 22), Neon.U(Neon.Lime), "LAUNCHER UPDATE");
            dl.AddText(Neon.Small, 15, a + new Vector2(28, 56), Neon.U(Neon.Dim), $"{v.Short}  ·  {v.Date}  ·  on {_s.Branch}      yours: {LauncherUpdater.Short} {LauncherUpdater.Date}");
            float y = a.Y + 92;
            var notes = _updater.Changes.Count > 0 ? _updater.Changes : new() { v.Subject };
            foreach (var n in notes.Take(9))
            {
                dl.AddCircleFilled(new Vector2(a.X + 34, y + 9), 2.5f, Neon.U(Neon.Lime));
                dl.AddText(Neon.Small, 15, new Vector2(a.X + 46, y), Neon.U(Neon.Ink), Trim(n, 74));
                y += 24;
            }
            ImGui.SetCursorScreenPos(new Vector2(a.X + 28, b.Y - 70));
            if (Neon.Button("updnow", "UPDATE NOW", new Vector2(220, 46), Neon.Lime, Neon.Heading, 20))
            {
                _updateCard = false;
                _installRev = _s.Branch;
                Task.Run(() => _updater.Install(_s.Branch, _jobs.Log));
            }
            ImGui.SameLine(0, 12);
            if (Neon.Button("updlater", "NOT NOW", new Vector2(160, 46), Neon.Dim, Neon.Heading, 20)) _updateCard = false;
            dl.AddText(Neon.Small, 13, new Vector2(a.X + 430, b.Y - 56), Neon.U(Neon.Dim), "Other versions:");
            dl.AddText(Neon.Small, 13, new Vector2(a.X + 430, b.Y - 40), Neon.U(Neon.Dim), "SETTINGS > ABOUT");
            ImGui.End();
        }

        /// <summary>The install animation: a reactor ring filling with progress, particles drawn into the core, then a flash and restart.</summary>
        void DrawInstall(Vector2 size, float dt)
        {
            _installT += Math.Min(dt, 1f / 20);
            var dl = ImGui.GetForegroundDrawList();
            var c = size * 0.5f;
            float fade = Math.Clamp(_installT / 0.4f, 0, 1);
            dl.AddRectFilled(Vector2.Zero, size, Neon.U(Neon.Space0, 0.9f * fade));
            float p = _updater.Progress, t = _installT;

            // particles spiralling into the core, faster as the build advances
            var rng = new Random(9);
            for (int i = 0; i < 90; i++)
            {
                float seed = (float)rng.NextDouble();
                float life = (t * (0.25f + p * 0.9f) + seed) % 1f;
                float r = (1 - life) * size.Y * 0.42f + 70;
                float ang = seed * 20f + life * 4f + t * 0.4f;
                var pos = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
                dl.AddCircleFilled(pos, 1.2f + 1.8f * life, Neon.U(i % 4 == 0 ? Neon.Magenta : Neon.Cyan, life * fade));
            }
            // counter-rotating arcs
            for (int k = 0; k < 3; k++)
            {
                float rr = 92 + k * 18, start = t * (k % 2 == 0 ? 1.3f : -0.9f) * (1 + k * 0.3f);
                for (int seg = 0; seg < 3; seg++)
                {
                    dl.PathArcTo(c, rr, start + seg * MathF.Tau / 3, start + seg * MathF.Tau / 3 + 1.1f, 24);
                    dl.PathStroke(Neon.U(k == 1 ? Neon.Magenta : Neon.Violet, 0.55f * fade), ImDrawFlags.None, 2f);
                }
            }
            // progress ring
            dl.AddCircle(c, 70, Neon.U(Neon.Cyan, 0.15f * fade), 96, 6f);
            if (p > 0.001f)
            {
                dl.PathArcTo(c, 70, -MathF.PI / 2, -MathF.PI / 2 + MathF.Tau * Math.Min(p, 1f), 96);
                dl.PathStroke(Neon.U(Neon.Cyan, fade), ImDrawFlags.None, 6f);
            }
            float core = 26 + 6 * MathF.Sin(t * 4) * (1 - p);
            for (int i = 6; i >= 1; i--) dl.AddCircleFilled(c, core * i / 6f + 8, Neon.U(Neon.Cyan, 0.06f * fade));
            CenterText(dl, Neon.Heading, 26, c.X, c.Y - 14, Neon.Mix(Neon.Space0, Neon.Ink, fade), $"{(int)(p * 100)}%");
            CenterText(dl, Neon.Heading, 20, c.X, c.Y + 168, Neon.Mix(Neon.Space0, Neon.Cyan, fade), _updater.Phase.ToUpperInvariant());
            CenterText(dl, Neon.Small, 15, c.X, c.Y + 200, Neon.Mix(Neon.Space0, Neon.Dim, fade), "Building the launcher of " + ShortRev(_installRev.Length > 0 ? _installRev : _s.Branch) + " from its own source");

            if (_updater.Error != null && !_updater.Installing)
            {
                CenterText(dl, Neon.Small, 15, c.X, c.Y + 232, Neon.Red, Trim(_updater.Error, 110));
                CenterText(dl, Neon.Small, 13, c.X, c.Y + 256, Neon.Dim, "click to close");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) { _installT = 0; _updater.ClearError(); }
            }
            if (_updater.Done)
            {
                if (_doneT < 0) _doneT = 0;
                _doneT += dt;
                float f = EaseOut(_doneT / 0.8f);
                dl.AddCircleFilled(c, f * size.X, Neon.U(Neon.Cyan, 0.5f * (1 - f)), 96);
                dl.AddCircle(c, 70 + f * size.X * 0.6f, Neon.U(Neon.Ink, 1 - f), 96, 4f);
                if (_doneT > 0.9f) _window.Close();
            }
        }

        void DrawAboutVersions()
        {
            Row("This launcher", () =>
            {
                ImGui.PushFont(Neon.Small);
                ImGui.TextColored(Neon.Ink, LauncherUpdater.Commit.Length > 0 ? $"{LauncherUpdater.Short}  ·  {LauncherUpdater.Date}" : "development build");
                ImGui.PopFont();
            });
            Row("On " + Trim(_s.Branch, 24), () =>
            {
                if (SmallButton(_updater.Checking ? "CHECKING" : "CHECK", 110, !_updater.Checking)) Task.Run(() => _updater.Check());
                ImGui.SameLine(0, 8);
                if (_updater.Available != null)
                {
                    if (SmallButton("UPDATE", 110, !_updater.Installing)) _updateCard = true;
                    ImGui.SameLine(0, 10);
                }
                ImGui.PushFont(Neon.Small);
                ImGui.TextColored(_updater.Available != null ? Neon.Lime : Neon.Dim,
                    _updater.Available != null ? $"newer: {_updater.Available.Short} ({_updater.Available.Date})"
                    : _updater.CheckedAt == default ? "not checked yet" : "up to date");
                ImGui.PopFont();
            });
            Row("Install version", () =>
            {
                ImGui.PushItemWidth(260);
                ImGui.InputTextWithHint("##instrev", "branch, tag or commit", ref _installRev, 200);
                ImGui.PopItemWidth();
                ImGui.SameLine(0, 8);
                if (SmallButton("INSTALL", 110, _installRev.Trim().Length > 0 && !_updater.Installing))
                {
                    var rev = _installRev.Trim();
                    Task.Run(() => _updater.Install(rev, _jobs.Log));
                }
                Neon.Tooltip("Builds that version of the launcher from its own source and switches to it.\nEvery version you install stays below, so you can switch back.");
            });
            var installed = LauncherUpdater.Installed();
            if (installed.Count > 0)
                Row("Installed", () =>
                {
                    foreach (var v in installed.Take(8))
                    {
                        bool current = v.Commit == LauncherUpdater.Commit;
                        ImGui.PushID(v.Commit);
                        if (SmallButton(current ? "IN USE" : "USE", 90, !current && !_updater.Installing))
                        {
                            _installRev = v.Source;
                            _updater.Use(v, _jobs.Log);
                        }
                        ImGui.SameLine(0, 10);
                        ImGui.PushFont(Neon.Small);
                        ImGui.TextColored(current ? Neon.Cyan : Neon.Ink, $"{v.Short}  {v.Date}  {Trim(v.Subject, 50)}");
                        ImGui.PopFont();
                        ImGui.PopID();
                    }
                });
        }
    }
}
